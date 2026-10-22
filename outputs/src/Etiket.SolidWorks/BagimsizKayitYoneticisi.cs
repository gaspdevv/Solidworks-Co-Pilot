using System;
using System.IO;
namespace Etiket.SolidWorks
{
 // COM çağrıları bu arayüzü uygulayan adaptörde, SolidWorks UI thread'inde yapılır.
 public interface IBagimsizBilesenKaydi
 {
  string KaynakYolu {get;}
  bool SecimGecerliMi();
  bool Kaydet(string hedef);
  string YeniReferansYolu {get;}
 }
 public sealed class BagimsizKayitYoneticisi
 {
  readonly KaydetmeIslemYoneticisi yonetici;
  readonly KaliciIslemGunlugu gunluk;
  public BagimsizKayitYoneticisi(KaydetmeIslemYoneticisi yonetici,KaliciIslemGunlugu gunluk)
  {this.yonetici=yonetici;this.gunluk=gunluk;}
  // Çağıran kimliği elinde tutar; HTTP yanıt kaybında AdiAl aynı işlemle tekrar edilir.
  public KaydetmeIslemi Baslat(string tur,string klasor)=>yonetici.Baslat(tur,klasor,KaydetmeBicimi.BagimsizBilesen);
  public string AdiAl(KaydetmeIslemi islem)
  {
   if(islem.KaydetmeBicimi!=KaydetmeBicimi.BagimsizBilesen)throw new InvalidOperationException("Bağımsız kayıt işlemi gerekli.");
   return yonetici.AdiAl(islem);
  }
  public void Kaydet(KaydetmeIslemi islem,IBagimsizBilesenKaydi bilesen,string dosyaAdi=null)
  {
   if(islem==null||islem.KaydetmeBicimi!=KaydetmeBicimi.BagimsizBilesen)throw new InvalidOperationException("Bağımsız kayıt işlemi gerekli.");
   // Diskteki aşama, belirsiz bir native yazımın ikinci kez başlatılmasını önler.
   var kalici=gunluk.Oku(islem.IslemId);
   if((kalici.Asama!="Ayrildi"&&kalici.Asama!="BagimsizKuyrukta")||islem.Asama!=kalici.Asama)throw new InvalidOperationException("Bu işlem yeniden CAD yazımı başlatamaz. Bekleyen sonuç kontrol edilmeli.");
   if(!DosyaAdiKurali.AyniYol(kalici.HedefDosya,islem.HedefDosya))throw new InvalidDataException("Hedef işlem günlüğüyle uyuşmuyor.");
   var ayrilan=string.IsNullOrWhiteSpace(kalici.AyrilanDosyaAdi)?Path.GetFileName(kalici.HedefDosya):kalici.AyrilanDosyaAdi;
   var sonAd=BagimsizDosyaAdiKurali.Tamamla(dosyaAdi??ayrilan,islem.BelgeTuru);
   if(string.IsNullOrWhiteSpace(sonAd)||sonAd!=Path.GetFileName(sonAd)||sonAd.IndexOfAny(Path.GetInvalidFileNameChars())>=0
    ||!string.Equals(Path.GetExtension(sonAd),islem.BelgeTuru=="P"?".SLDPRT":".SLDASM",StringComparison.OrdinalIgnoreCase))throw new IOException("Geçerli CAD dosya adı gerekli.");
   var gercekHedef=Path.Combine(islem.HedefKlasor,sonAd);
   if(kalici.Asama=="BagimsizKuyrukta"&&!DosyaAdiKurali.AyniYol(kalici.GercekDosya,gercekHedef))throw new IOException("Kuyruğa alınan son ad veya klasör değişti.");
   var kaynak=bilesen.KaynakYolu;
   if(!bilesen.SecimGecerliMi()||!File.Exists(kaynak))throw new IOException("Seçili kaynak bileşen doğrulanamadı.");
   if(DosyaAdiKurali.AyniYol(kaynak,gercekHedef)||File.Exists(gercekHedef)||Directory.Exists(gercekHedef))throw new IOException("Hedef mevcut; üzerine yazılmayacak.");
   if(!DosyaAdiKurali.AyniYol(Path.GetDirectoryName(islem.HedefDosya),islem.HedefKlasor)||!Directory.Exists(islem.HedefKlasor)||!DosyaAdiKurali.Gecerli(Path.GetFileName(islem.HedefDosya),islem.BelgeTuru))throw new IOException("Hedef klasör veya merkezi dosya adı geçersiz.");
   // Yazım öncesinde kalıcı belirsizlik sınırı. Hata/çökmede iptal ve ikinci yazım yapılmaz.
   islem.GercekDosya=gercekHedef;islem.Asama="BagimsizYazimBasladi";gunluk.Yaz(islem);
   if(!bilesen.Kaydet(gercekHedef))throw new IOException("SolidWorks bağımsız kaydı doğrulamadı. İşlem ve numara korunuyor.");
   if(!File.Exists(gercekHedef)||!DosyaAdiKurali.AyniYol(gercekHedef,bilesen.YeniReferansYolu))throw new IOException("Yeni dosya veya montaj referansı doğrulanamadı; numara tutuluyor.");
   if(!KaydedilenAdKurali.MerkeziAdMi(ayrilan,sonAd))
   {yonetici.OzelAdlaKaydetmeTamamlandi(islem,gercekHedef,true,bilesen.YeniReferansYolu);return;}
   yonetici.DogrulanmisOtomatikKaydiTamamla(islem,islem.HedefDosya);
  }
  static void Dogrula(KaydetmeIslemi islem,string referans)
  {
   if(!File.Exists(islem.HedefDosya)||!DosyaAdiKurali.AyniYol(islem.HedefDosya,referans))throw new IOException("Yeni dosya veya montaj referansı doğrulanamadı; merkezi onay gönderilmedi.");
  }
  public void OnayiTekrarDene(KaydetmeIslemi islem,string dogrulanmisReferans)
  {
   var kalici=gunluk.Oku(islem.IslemId);
   if(kalici.KaydetmeBicimi!=KaydetmeBicimi.BagimsizBilesen||kalici.Asama!="Kaydedildi")throw new InvalidOperationException("Yalnızca doğrulanmış bağımsız kaydın onayı tekrar denenebilir.");
   Dogrula(kalici,dogrulanmisReferans);
   islem.Asama=new KayitSonucuToparlama(gunluk,yonetici).Tamamla(islem.IslemId).Asama;
  }
 }
}
