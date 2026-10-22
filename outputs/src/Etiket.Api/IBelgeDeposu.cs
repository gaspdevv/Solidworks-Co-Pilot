namespace Etiket.Api;

public interface IBelgeDeposu
{
    Task<Rezervasyon> AyirAsync(RezervasyonIstegi istek, CancellationToken iptal);
    Task<Rezervasyon?> RezervasyonBulAsync(Guid islemId, CancellationToken iptal);
    Task<Belge> OnaylaAsync(Guid islemId, CancellationToken iptal);
    Task<Rezervasyon> IptalEtAsync(Guid islemId, CancellationToken iptal);
    Task<Belge?> BulAsync(string dosyaAdi, CancellationToken iptal);
    Task<BelgeListesi> ListeleAsync(int baslangic, int adet, string? arama, string? tur, CancellationToken iptal);
    Task SaglikKontroluAsync(CancellationToken iptal);
}
public sealed class VeritabaniErisimHatasi(Exception kaynak) : Exception("Veritabanına erişilemiyor.", kaynak);
public sealed class BelgeIstegiHatasi(string mesaj) : Exception(mesaj);
public sealed class IslemCakismasiHatasi(string mesaj) : Exception(mesaj);
public sealed class KayitBulunamadiHatasi() : Exception("İşlem bulunamadı.");
