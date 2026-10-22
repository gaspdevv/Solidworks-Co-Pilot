// Tarihsel etiket-kolonu modeli: yeni rezervasyon akışında kullanılmaz.
using System;
using System.IO;

namespace Etiket.SolidWorks
{
    public enum KaydetmeSonrasiKarar
    {
        IslemYok,
        YeniEtiketGerekli,
        MevcutEtiketiKoru,
        EtiketDogrulamaGerekli
    }

    // Saf karar kuralı: dosya yazmaz, API çağırmaz veya numara üretmez.
    // Önceki yol ve etiket Save olayından ÖNCE alınmış olmalıdır.
    public static class KaydetmeKarari
    {
        public static KaydetmeSonrasiKarar Degerlendir(
            bool kaydetmeBasarili, string? oncekiDosyaYolu,
            string? kaydedilenDosyaYolu, string? oncekiEtiket)
        {
            if (!kaydetmeBasarili || string.IsNullOrWhiteSpace(kaydedilenDosyaYolu))
                return KaydetmeSonrasiKarar.IslemYok;

            string hedef;
            try
            {
                if (!Path.IsPathFullyQualified(kaydedilenDosyaYolu))
                    return KaydetmeSonrasiKarar.EtiketDogrulamaGerekli;
                hedef = Path.GetFullPath(kaydedilenDosyaYolu);
            }
            catch (Exception hata) when (hata is ArgumentException || hata is NotSupportedException || hata is PathTooLongException)
            {
                return KaydetmeSonrasiKarar.EtiketDogrulamaGerekli;
            }

            var uzanti = Path.GetExtension(hedef);
            var onEk = uzanti.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase) ? 'P'
                : uzanti.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) ? 'M' : '\0';
            // Drawing ve dışa aktarım dosyaları P/M kaydı açmaz.
            if (onEk == '\0') return KaydetmeSonrasiKarar.IslemYok;
            if (string.IsNullOrWhiteSpace(oncekiDosyaYolu))
                return KaydetmeSonrasiKarar.YeniEtiketGerekli;

            string onceki;
            try
            {
                if (!Path.IsPathFullyQualified(oncekiDosyaYolu))
                    return KaydetmeSonrasiKarar.EtiketDogrulamaGerekli;
                onceki = Path.GetFullPath(oncekiDosyaYolu);
            }
            catch (Exception hata) when (hata is ArgumentException || hata is NotSupportedException || hata is PathTooLongException)
            {
                return KaydetmeSonrasiKarar.EtiketDogrulamaGerekli;
            }

            if (!string.Equals(onceki, hedef, StringComparison.OrdinalIgnoreCase))
                return KaydetmeSonrasiKarar.YeniEtiketGerekli;
            // Eski/eksik etiketli dosyada otomatik yeni kayıt açarak sorunu gizleme.
            return EtiketGecerli(oncekiEtiket, onEk)
                ? KaydetmeSonrasiKarar.MevcutEtiketiKoru
                : KaydetmeSonrasiKarar.EtiketDogrulamaGerekli;
        }

        private static bool EtiketGecerli(string? etiket, char onEk)
        {
            if (etiket == null || etiket.Length < 6 || etiket[0] != onEk) return false;
            var sifirDisi = false;
            for (var i = 1; i < etiket.Length; i++)
            {
                if (etiket[i] < '0' || etiket[i] > '9') return false;
                if (etiket[i] != '0') sifirDisi = true;
            }
            return sifirDisi;
        }
    }
}
