namespace Etiket.Api;

public sealed record Belge(long Id, string DosyaAdi, DateTime Tarih);
public sealed record BelgeListesi(IReadOnlyList<Belge> Belgeler, int Baslangic, int Adet, long Toplam);
public sealed record RezervasyonIstegi(Guid IslemId, string? BelgeTuru);
public sealed record Rezervasyon(Guid IslemId, string DosyaAdi, string Durum, DateTime Tarih);

public static class BelgeDogrulama
{
    public static bool DosyaAdiGecerliMi(string? ad)
    {
        if (string.IsNullOrEmpty(ad) || ad.Length > 32) return false;
        var uzanti = ad[0] == 'P' ? ".SLDPRT" : ad[0] == 'M' ? ".SLDASM" : null;
        if (uzanti is null || !ad.EndsWith(uzanti, StringComparison.Ordinal)) return false;
        var numara = ad.AsSpan(1, ad.Length - uzanti.Length - 1);
        return numara.Length >= 5 && numara.IndexOfAnyExceptInRange('0','9') < 0
            && numara.IndexOfAnyExcept('0') >= 0;
    }
}
