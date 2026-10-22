// Tarihsel etiket-kolonu modeli: yeni rezervasyon akışında kullanılmaz.
using Etiket.SolidWorks;

namespace Etiket.Api.Testler;

internal static class KaydetmeKarariTestleri
{
    public static void Calistir(Action<bool, string> kontrol)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Kaydetme yolu kuralları Windows üzerinde test edilmelidir.");
        var sayi = 0;
        void Bekle(bool basarili, string? onceki, string? hedef, string? etiket,
            KaydetmeSonrasiKarar beklenen, string senaryo)
        {
            kontrol(KaydetmeKarari.Degerlendir(basarili, onceki, hedef, etiket) == beklenen, senaryo);
            sayi++;
        }
        const string parca = @"C:\CAD\Mil.SLDPRT";
        const string montaj = @"C:\CAD\AnaMontaj.SLDASM";
        Bekle(false, null, null, null, KaydetmeSonrasiKarar.IslemYok, "Yeni belge Save öncesi etiket istememeli.");
        Bekle(false, null, parca, null, KaydetmeSonrasiKarar.IslemYok, "İptal/başarısız ilk Save etiket istememeli.");
        Bekle(false, parca, @"C:\CAD\BuyukMil.SLDPRT", "P00001", KaydetmeSonrasiKarar.IslemYok, "Başarısız Save As etiket istememeli.");
        Bekle(true, null, null, null, KaydetmeSonrasiKarar.IslemYok, "Hedef bildirilmeden işlem başlamamalı.");
        Bekle(true, null, parca, null, KaydetmeSonrasiKarar.YeniEtiketGerekli, "İlk Part Save yeni etiket gerektirir.");
        Bekle(true, "", montaj, null, KaydetmeSonrasiKarar.YeniEtiketGerekli, "İlk Assembly Save yeni etiket gerektirir.");
        Bekle(true, null, parca, "P00001", KaydetmeSonrasiKarar.YeniEtiketGerekli, "Yeni belgedeki kopyalanmış etiket yeniden kullanılmamalı.");
        Bekle(true, parca, parca, "P00001", KaydetmeSonrasiKarar.MevcutEtiketiKoru, "Normal Part Save etiketi korur.");
        Bekle(true, montaj, montaj, "M00001", KaydetmeSonrasiKarar.MevcutEtiketiKoru, "Normal Assembly Save etiketi korur.");
        Bekle(true, parca, @"c:\cad\MIL.sldprt", "P00001", KaydetmeSonrasiKarar.MevcutEtiketiKoru, "Windows yol harf farkı yeni dosya sayılmamalı.");
        Bekle(true, parca, @"C:\CAD\Alt\..\Mil.SLDPRT", "P00001", KaydetmeSonrasiKarar.MevcutEtiketiKoru, "Normalize edilen aynı yol yeni dosya sayılmamalı.");
        Bekle(true, parca, @"C:\CAD\BuyukMil.SLDPRT", "P00001", KaydetmeSonrasiKarar.YeniEtiketGerekli, "Save As farklı ad için yeni etiket gerektirir.");
        Bekle(true, parca, @"C:\Diger\Mil.SLDPRT", "P00001", KaydetmeSonrasiKarar.YeniEtiketGerekli, "Aynı ad farklı klasörde yeni dosyadır.");
        Bekle(true, montaj, @"C:\CAD\YeniMontaj.SLDASM", "M00001", KaydetmeSonrasiKarar.YeniEtiketGerekli, "Assembly Save As yeni etiket gerektirir.");
        Bekle(true, parca, parca, null, KaydetmeSonrasiKarar.EtiketDogrulamaGerekli, "Mevcut dosyada eksik etiket otomatik yeni kayıt açmamalı.");
        foreach (var etiket in new[] { "", "P00000", "P001", "M00001", "p00001", "P00A01", " P00001", "P00001 ", "P００００１" })
            Bekle(true, parca, parca, etiket, KaydetmeSonrasiKarar.EtiketDogrulamaGerekli, "Bozuk/uyuşmayan mevcut etiket doğrulanmalı: " + etiket);
        Bekle(true, parca, parca, "P100000", KaydetmeSonrasiKarar.MevcutEtiketiKoru, "Beş haneden uzun etiket korunmalı.");
        Bekle(true, null, @"C:\CAD\Cizim.SLDDRW", null, KaydetmeSonrasiKarar.IslemYok, "Drawing P/M kaydı açmamalı.");
        Bekle(true, parca, @"C:\CAD\Mil.STEP", "P00001", KaydetmeSonrasiKarar.IslemYok, "Dışa aktarım etiket istememeli.");
        Bekle(true, parca, "Mil.SLDPRT", "P00001", KaydetmeSonrasiKarar.EtiketDogrulamaGerekli, "Göreli hedef yol otomatik karara dönüşmemeli.");
        Bekle(true, "Mil.SLDPRT", parca, "P00001", KaydetmeSonrasiKarar.EtiketDogrulamaGerekli, "Göreli eski yol otomatik karara dönüşmemeli.");
        Bekle(true, null, "C:\\CAD\\Mil\0.SLDPRT", null, KaydetmeSonrasiKarar.EtiketDogrulamaGerekli, "Geçersiz hedef yol yeni kayıt açmamalı.");
        Bekle(true, "C:\\CAD\\Mil\0.SLDPRT", parca, "P00001", KaydetmeSonrasiKarar.EtiketDogrulamaGerekli, "Geçersiz eski yol yeni kayıt açmamalı.");
        Bekle(true, @"\\sunucu\cad\Mil.SLDPRT", @"\\SUNUCU\cad\Mil.SLDPRT", "P00001", KaydetmeSonrasiKarar.MevcutEtiketiKoru, "Aynı UNC yolu etiketi korumalı.");
        Bekle(true, @"\\sunucu\cad\Mil.SLDPRT", @"\\sunucu\diger\Mil.SLDPRT", "P00001", KaydetmeSonrasiKarar.YeniEtiketGerekli, "Farklı UNC paylaşımı yeni dosyadır.");
        Console.WriteLine($"Kaydetme karar testleri: {sayi} kontrol geçti.");
    }
}
