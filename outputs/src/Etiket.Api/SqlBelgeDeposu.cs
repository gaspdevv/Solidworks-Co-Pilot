using System.Data;
using Microsoft.Data.SqlClient;
namespace Etiket.Api;

public sealed class SqlBelgeDeposu(string baglantiDizesi) : IBelgeDeposu
{
    private async Task<T> Calistir<T>(string sql, Action<SqlParameterCollection> parametreler,
        Func<SqlDataReader, CancellationToken, Task<T>> oku, CancellationToken iptal, bool prosedur=false)
    {
        try
        {
            await using var baglanti = new SqlConnection(baglantiDizesi);
            await baglanti.OpenAsync(iptal);
            await using var komut = new SqlCommand(sql, baglanti)
            { CommandTimeout=15, CommandType=prosedur ? CommandType.StoredProcedure : CommandType.Text };
            parametreler(komut.Parameters);
            await using var okuyucu = await komut.ExecuteReaderAsync(iptal);
            return await oku(okuyucu,iptal);
        }
        catch(SqlException h) when(h.Number==51101) { throw new KayitBulunamadiHatasi(); }
        catch(SqlException h) when(h.Number is 51102 or 51104 or 2601 or 2627)
        { throw new IslemCakismasiHatasi("İşlem durumu veya ayrılan dosya adı istekle uyuşmuyor."); }
        catch(SqlException h) when(h.Number==51103) { throw new BelgeIstegiHatasi("İşlem kimliği ve belge türünü kontrol edin."); }
        catch(SqlException h) { throw new VeritabaniErisimHatasi(h); }
    }
    private static void Kimlik(SqlParameterCollection p,Guid id) => p.Add("@IslemId",SqlDbType.UniqueIdentifier).Value=id;
    private static Belge BelgeOku(SqlDataReader r) => new(r.GetInt64(0),r.GetString(1),DateTime.SpecifyKind(r.GetDateTime(2),DateTimeKind.Utc));
    private static Rezervasyon RezervasyonOku(SqlDataReader r) => new(r.GetGuid(0),r.GetString(1),
        r.GetByte(2) switch {0=>"Ayrildi",1=>"Onaylandi",2=>"Iptal",_=>throw new InvalidOperationException("Bilinmeyen işlem durumu.")},
        DateTime.SpecifyKind(r.GetDateTime(3),DateTimeKind.Utc));
    private static async Task<Rezervasyon> TekRezervasyon(SqlDataReader r,CancellationToken t) =>
        await r.ReadAsync(t) ? RezervasyonOku(r) : throw new InvalidOperationException("İşlem sonucu boş.");
    public Task<Rezervasyon> AyirAsync(RezervasyonIstegi istek,CancellationToken t) =>
        Calistir("dbo.DosyaAdiAyir",p=>{Kimlik(p,istek.IslemId);p.Add("@BelgeTuru",SqlDbType.NVarChar,10).Value=istek.BelgeTuru!;},TekRezervasyon,t,true);
    public Task<Rezervasyon?> RezervasyonBulAsync(Guid id,CancellationToken t) =>
        Calistir<Rezervasyon?>("SELECT IslemId,DosyaAdi,Durum,Tarih FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;",p=>Kimlik(p,id),
            async(r,c)=>await r.ReadAsync(c)?RezervasyonOku(r):null,t);
    public Task<Rezervasyon> IptalEtAsync(Guid id,CancellationToken t) => Calistir("dbo.RezervasyonIptalEt",p=>Kimlik(p,id),TekRezervasyon,t,true);
    public Task<Belge> OnaylaAsync(Guid id,CancellationToken t) =>
        Calistir("dbo.RezervasyonOnayla",p=>Kimlik(p,id),async(r,c)=>await r.ReadAsync(c)?BelgeOku(r):throw new InvalidOperationException("Onay sonucu boş."),t,true);
    public Task<Belge?> BulAsync(string ad,CancellationToken t) =>
        Calistir<Belge?>("SELECT ID,DosyaAdi,Tarih FROM dbo.Documents WHERE DosyaAdi=@DosyaAdi;",p=>p.Add("@DosyaAdi",SqlDbType.NVarChar,255).Value=ad,
            async(r,c)=>await r.ReadAsync(c)?BelgeOku(r):null,t);
    public Task<BelgeListesi> ListeleAsync(int baslangic,int adet,string? arama,string? tur,CancellationToken t) =>
        Calistir("""
            SELECT COUNT_BIG(*) FROM dbo.Documents
            WHERE (@Arama IS NULL OR CHARINDEX(@Arama,DosyaAdi)>0)
              AND (@Tur IS NULL OR LEFT(DosyaAdi,1)=@Tur);
            SELECT ID,DosyaAdi,Tarih FROM dbo.Documents
            WHERE (@Arama IS NULL OR CHARINDEX(@Arama,DosyaAdi)>0)
              AND (@Tur IS NULL OR LEFT(DosyaAdi,1)=@Tur)
            ORDER BY ID OFFSET @Baslangic ROWS FETCH NEXT @Adet ROWS ONLY;
            """,
            p=>{
                p.Add("@Baslangic",SqlDbType.Int).Value=baslangic;
                p.Add("@Adet",SqlDbType.Int).Value=adet;
                p.Add("@Arama",SqlDbType.NVarChar,64).Value=(object?)arama??DBNull.Value;
                p.Add("@Tur",SqlDbType.Char,1).Value=(object?)tur??DBNull.Value;
            },
            async(r,c)=>{
                await r.ReadAsync(c);var toplam=r.GetInt64(0);
                await r.NextResultAsync(c);
                var liste=new List<Belge>();while(await r.ReadAsync(c))liste.Add(BelgeOku(r));
                return new BelgeListesi(liste,baslangic,adet,toplam);
            },t);
    public async Task SaglikKontroluAsync(CancellationToken t)
    {
        var hazir=await Calistir("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.Documents',N'U') IS NOT NULL
            AND COL_LENGTH(N'dbo.Documents',N'Etiket') IS NULL
            AND COL_LENGTH(N'dbo.Documents',N'DosyaAdi') IS NOT NULL
            AND OBJECT_ID(N'dbo.Rezervasyonlar',N'U') IS NOT NULL
            AND OBJECT_ID(N'dbo.DosyaAdiAyir',N'P') IS NOT NULL
            AND OBJECT_ID(N'dbo.RezervasyonOnayla',N'P') IS NOT NULL
            AND OBJECT_ID(N'dbo.RezervasyonIptalEt',N'P') IS NOT NULL
            AND OBJECT_ID(N'dbo.ParcaSayaci',N'SO') IS NOT NULL
            AND OBJECT_ID(N'dbo.MontajSayaci',N'SO') IS NOT NULL
            AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Documents') AND name=N'UQ_Documents_DosyaAdi' AND is_unique=1)
            AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Rezervasyonlar') AND name=N'UX_Rezervasyonlar_AktifDosyaAdi' AND is_unique=1 AND has_filter=1)
            THEN 1 ELSE 0 END;
            """,_=>{},async(r,c)=>await r.ReadAsync(c)&&r.GetInt32(0)==1,t);
        if(!hazir)throw new VeritabaniErisimHatasi(new InvalidOperationException("Yeni SQL kurulumu eksik."));
    }
}
