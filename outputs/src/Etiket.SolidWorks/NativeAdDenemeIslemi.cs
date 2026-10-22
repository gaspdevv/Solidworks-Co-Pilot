using System;
using System.IO;
namespace Etiket.SolidWorks
{
 public static class NativeKayitPenceresiKurali
 {
  public static bool Eslesiyor(string baslik,string dugme)
  {
   var b=(baslik??"").Trim();var d=(dugme??"").Replace("&","").Trim();
   return (string.Equals(b,"Farklı Kaydet",StringComparison.OrdinalIgnoreCase)||string.Equals(b,"Save As",StringComparison.OrdinalIgnoreCase))
    &&(string.Equals(d,"Kaydet",StringComparison.OrdinalIgnoreCase)||string.Equals(d,"Save",StringComparison.OrdinalIgnoreCase));
  }
 }
 // Önizleme veya gerçek kayıt için aynı rezervasyonu taşır; CAD yazımı dışarıda yapılır.
 public sealed class NativeAdDenemeIslemi
 {
  readonly KaydetmeIslemYoneticisi yonetici;
  KaydetmeIslemi bekleyen;
  public NativeAdDenemeIslemi(KaydetmeIslemYoneticisi yonetici){this.yonetici=yonetici;}
  public bool Aktif=>bekleyen!=null;
  public Guid IslemId=>bekleyen?.IslemId??Guid.Empty;
  public bool KaydaDevredildi {get;private set;}
  public string BelgeTuru=>bekleyen?.BelgeTuru;
  public string BekleyenAsama=>bekleyen?.Asama??"";
  public string Hazirla(string tur,string baslangicKlasoru,bool gercekKayit=false,bool standartKayit=false)
  {
   if(bekleyen!=null&&bekleyen.Asama=="IptalBekliyor")IptalEt();
   if(KaydaDevredildi)throw new InvalidOperationException("Önceki bağımsız kayıt işleminin sonucu bekleniyor; yeni numara ayrılmadı.");
   if(bekleyen==null)bekleyen=yonetici.Baslat(tur,baslangicKlasoru,standartKayit?KaydetmeBicimi.StandartPencere:gercekKayit?KaydetmeBicimi.BagimsizBilesen:KaydetmeBicimi.FarkliKaydet);
   if(bekleyen.BelgeTuru!=tur)throw new InvalidOperationException("Önce önceki pencere denemesinin iptalini tamamlayın.");
   return Path.GetFileName(yonetici.AdiAl(bekleyen));
  }
  public string HedefKlasoruBagla(string klasor){if(bekleyen==null)throw new InvalidOperationException("Rezervasyon yok.");return yonetici.HedefKlasoruBagla(bekleyen,klasor);}
  public KaydetmeIslemi KaydaAktar(string klasor,string dosyaAdi)
  {
   if(bekleyen==null||KaydaDevredildi)throw new InvalidOperationException("Kayıt zaten devredildi veya rezervasyon yok.");
   var ad=BagimsizDosyaAdiKurali.Tamamla(dosyaAdi,bekleyen.BelgeTuru);
   HedefKlasoruBagla(klasor);
   if(bekleyen.KaydetmeBicimi==KaydetmeBicimi.StandartPencere)yonetici.StandartKaydiHazirla(bekleyen,ad);else yonetici.BagimsizKaydiSirayaAl(bekleyen,ad);
   KaydaDevredildi=true;return bekleyen;
  }
  public void KayitSonucunuAl()
  {if(bekleyen!=null&&(bekleyen.Asama=="Onaylandi"||bekleyen.Asama=="Iptal")){bekleyen=null;KaydaDevredildi=false;}}
  public void IptalEt()
  {
   if(KaydaDevredildi)return; // Pencerenin programatik kapanışı, kayıt iptali değildir.
   if(bekleyen==null)return;
   yonetici.IptalEt(bekleyen);bekleyen=null;
  }
 }
}
