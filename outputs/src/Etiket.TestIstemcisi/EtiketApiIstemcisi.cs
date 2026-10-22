using System.Net.Http.Json;
using System.Text.Json;
namespace Etiket.TestIstemcisi;
public sealed record BelgeKaydi(long Id,string DosyaAdi,DateTime Tarih);
public sealed record KayitListesi(IReadOnlyList<BelgeKaydi> Belgeler,int Baslangic,int Adet);
public sealed record ServisDurumu(string Durum,string Veritabani);
public sealed record RezervasyonKaydi(Guid IslemId,string DosyaAdi,string Durum,DateTime Tarih);
public sealed class IstemciHatasi(string mesaj,int? durumKodu=null,bool sonucBelirsiz=false):Exception(mesaj)
{
    public int? DurumKodu {get;}=durumKodu;
    public bool SonucBelirsiz {get;}=sonucBelirsiz;
}
public sealed class EtiketApiIstemcisi(HttpClient http)
{
    // İşlem kimliği çağıran tarafından bir kez oluşturulur; bağlantı kaybında değiştirilmez.
    public Task<RezervasyonKaydi> AyirAsync(Guid id,string tur,CancellationToken t=default)=>Gonder<RezervasyonKaydi>(HttpMethod.Post,"api/rezervasyonlar",new{islemId=id,belgeTuru=tur},t);
    public Task<RezervasyonKaydi> RezervasyonBulAsync(Guid id,CancellationToken t=default)=>Gonder<RezervasyonKaydi>(HttpMethod.Get,$"api/rezervasyonlar/{id}",null,t);
    public Task<BelgeKaydi> OnaylaAsync(Guid id,CancellationToken t=default)=>Gonder<BelgeKaydi>(HttpMethod.Post,$"api/rezervasyonlar/{id}/onay",null,t);
    public Task<RezervasyonKaydi> IptalEtAsync(Guid id,CancellationToken t=default)=>Gonder<RezervasyonKaydi>(HttpMethod.Post,$"api/rezervasyonlar/{id}/iptal",null,t);
    public Task<BelgeKaydi> BulAsync(string ad,CancellationToken t=default)=>Gonder<BelgeKaydi>(HttpMethod.Get,"api/documents/"+Uri.EscapeDataString(ad),null,t);
    public Task<KayitListesi> ListeleAsync(int baslangic=0,int adet=100,CancellationToken iptal=default)=>Gonder<KayitListesi>(HttpMethod.Get,$"api/documents?baslangic={baslangic}&adet={adet}",null,iptal);
    public Task<ServisDurumu> SaglikAsync(CancellationToken t=default)=>Gonder<ServisDurumu>(HttpMethod.Get,"api/health",null,t);
    private async Task<T> Gonder<T>(HttpMethod metot,string yol,object? govde,CancellationToken t)
    {
        using var istek=new HttpRequestMessage(metot,yol);
        if(govde is not null)istek.Content=JsonContent.Create(govde);
        var yazma=metot==HttpMethod.Post;
        try
        {
            using var yanit=await http.SendAsync(istek,t);
            if(!yanit.IsSuccessStatusCode)throw new IstemciHatasi($"İstek tamamlanamadı (HTTP {(int)yanit.StatusCode}).",(int)yanit.StatusCode,yazma&&(int)yanit.StatusCode>=500);
            return await yanit.Content.ReadFromJsonAsync<T>(cancellationToken:t)??throw new JsonException();
        }
        catch(HttpRequestException){throw new IstemciHatasi("Servis bağlantısı kurulamadı veya kesildi.",sonucBelirsiz:yazma);}
        catch(OperationCanceledException){throw new IstemciHatasi("İşlem iptal edildi veya zaman aşımına uğradı.",sonucBelirsiz:yazma);}
        catch(Exception h) when(h is JsonException or NotSupportedException){throw new IstemciHatasi("Servis yanıtı okunamadı.",sonucBelirsiz:yazma);}
    }
}
