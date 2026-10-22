using System;
using System.IO;
namespace Etiket.SolidWorks
{
 public sealed class KaydetmeIslemYoneticisi
 {
  readonly IAdServisi servis;readonly KaliciIslemGunlugu gunluk;
  public KaydetmeIslemYoneticisi(IAdServisi servis,KaliciIslemGunlugu gunluk){this.servis=servis;this.gunluk=gunluk;}
  public KaydetmeIslemi Baslat(string tur,string klasor,KaydetmeBicimi bicim=KaydetmeBicimi.FarkliKaydet)
  {
   if(tur!="P"&&tur!="M")throw new ArgumentException("Belge türü geçersiz.");
   if(!Directory.Exists(klasor))throw new IOException("Hedef klasör bulunamadı.");
   if(!Enum.IsDefined(typeof(KaydetmeBicimi),bicim))throw new ArgumentException("Kayıt biçimi geçersiz.");
   var islem=new KaydetmeIslemi{IslemId=Guid.NewGuid(),BelgeTuru=tur,HedefKlasor=Path.GetFullPath(klasor),KaydetmeBicimi=bicim};
   gunluk.Yaz(islem); // Kimlik ilk HTTP isteğinden önce kalıcıdır.
   return islem;
  }
  public string AdiAl(KaydetmeIslemi islem)
  {
   var kaliciAsama=gunluk.Oku(islem.IslemId).Asama;
   if(kaliciAsama!="AdBekleniyor"&&kaliciAsama!="Ayrildi")throw new InvalidOperationException("Kalıcı işlem yeni ad ayırma aşamasında değil.");
   if(islem.Asama!="AdBekleniyor"&&islem.Asama!="Ayrildi")throw new InvalidOperationException("Önce önceki işlemi tamamlayın.");
   var yanit=servis.Ayir(islem.IslemId,islem.BelgeTuru);
   if(yanit.IslemId!=islem.IslemId||!DosyaAdiKurali.Gecerli(yanit.DosyaAdi,islem.BelgeTuru)||yanit.Durum!="Ayrildi")
    throw new InvalidDataException("Merkezi işlem sonucu beklenen durumla uyuşmuyor; yeni kimlik oluşturulmadı.");
   var hedef=Path.Combine(islem.HedefKlasor,yanit.DosyaAdi);
   if(File.Exists(hedef)||Directory.Exists(hedef))throw new IOException("Hedef ad zaten mevcut. Üzerine yazılmayacak.");
   islem.HedefDosya=hedef;islem.AyrilanDosyaAdi=yanit.DosyaAdi;islem.Asama="Ayrildi";gunluk.Yaz(islem);return hedef;
  }
  public string HedefKlasoruBagla(KaydetmeIslemi islem,string klasor)
  {
   var kalici=gunluk.Oku(islem.IslemId);
   if(kalici.Asama!="Ayrildi"||islem.Asama!="Ayrildi"||!DosyaAdiKurali.AyniYol(kalici.HedefDosya,islem.HedefDosya))throw new InvalidOperationException("Bu rezervasyonun hedefi değiştirilemez.");
   var ad=Path.GetFileName(kalici.HedefDosya);
   if(!DosyaAdiKurali.Gecerli(ad,islem.BelgeTuru))throw new InvalidDataException("Rezervasyon adı geçersiz.");
   var yol=HedefAdresKurali.Coz("Adres: "+klasor);
   if(yol==null)throw new InvalidDataException("Tam hedef klasörü gerekli.");
   islem.HedefKlasor=yol;islem.HedefDosya=Path.Combine(yol,ad);gunluk.Yaz(islem);return islem.HedefDosya;
  }
  public void BagimsizKaydiSirayaAl(KaydetmeIslemi islem,string dosyaAdi)
  {
   if(islem.KaydetmeBicimi!=KaydetmeBicimi.BagimsizBilesen||islem.Asama!="Ayrildi"||gunluk.Oku(islem.IslemId).Asama!="Ayrildi")
    throw new InvalidOperationException("Bu işlem bağımsız kayıt sırasına alınamaz.");
   islem.GercekDosya=Path.Combine(islem.HedefKlasor,BagimsizDosyaAdiKurali.Tamamla(dosyaAdi,islem.BelgeTuru));
   islem.Asama="BagimsizKuyrukta";gunluk.Yaz(islem);
  }
  public void StandartKaydiHazirla(KaydetmeIslemi islem,string dosyaAdi)
  {
   if(islem.KaydetmeBicimi!=KaydetmeBicimi.StandartPencere||islem.Asama!="Ayrildi"||gunluk.Oku(islem.IslemId).Asama!="Ayrildi")
    throw new InvalidOperationException("Standart kayıt yeniden başlatılamaz; önceki sonuç kontrol edilmeli.");
   islem.GercekDosya=Path.Combine(islem.HedefKlasor,BagimsizDosyaAdiKurali.Tamamla(dosyaAdi,islem.BelgeTuru));
   islem.Asama="NativeYazimBasladi";gunluk.Yaz(islem);
  }
  public void OzelAdlaKaydetmeTamamlandi(KaydetmeIslemi islem,string gercekYol,bool nativeBasarili,string dogrulanmisReferans)
  {
   if(!nativeBasarili||!File.Exists(gercekYol)||!DosyaAdiKurali.AyniYol(gercekYol,dogrulanmisReferans))throw new IOException("Özel adla kayıt sonucu doğrulanamadı; numara geri verilmedi.");
   var kalici=gunluk.Oku(islem.IslemId);
   if(kalici.Asama!="Ayrildi"&&kalici.Asama!="BagimsizYazimBasladi"&&kalici.Asama!="NativeYazimBasladi")throw new InvalidOperationException("İşlem özel adla kayıt aşamasında değil.");
   var ayrilan=string.IsNullOrWhiteSpace(kalici.AyrilanDosyaAdi)?Path.GetFileName(kalici.HedefDosya):kalici.AyrilanDosyaAdi;
   if(KaydedilenAdKurali.MerkeziAdMi(ayrilan,Path.GetFileName(gercekYol)))throw new InvalidOperationException("Ayrılan otomatik ad için normal onay akışı kullanılmalı.");
   // Dosya ve referans doğrulanmıştır; ayrılan otomatik ad kullanılmamıştır.
   if(kalici.KaydetmeBicimi==KaydetmeBicimi.StandartPencere||kalici.KaydetmeBicimi==KaydetmeBicimi.BagimsizBilesen)KayitDosyaKaniti.Sakla(islem,gercekYol);
   islem.GercekDosya=Path.GetFullPath(gercekYol);islem.Asama="OzelAdIptalBekliyor";gunluk.Yaz(islem);
   if(KayitDosyaKaniti.Var(islem))KayitOlayTanisi.Yaz("KayitKanitiSaklandi","islem="+islem.IslemId+" asama="+islem.Asama+" boyut="+islem.KayitBoyutu+" sha256="+islem.KayitSHA256);
   OzelAdIptaliniTekrarDene(islem);
  }
  public void OzelAdIptaliniTekrarDene(KaydetmeIslemi islem)
  {
   var kalici=gunluk.Oku(islem.IslemId);
   if(kalici.Asama!="OzelAdIptalBekliyor")throw new InvalidOperationException("Özel ad iptali beklemiyor.");
   servis.IptalEt(islem.IslemId);islem.Asama="Iptal";islem.GercekDosya=kalici.GercekDosya;gunluk.Yaz(islem);
  }
  public void ApiKaydetmeTamamlandi(KaydetmeIslemi islem,string gercekYol,bool basarili,int hata)
  {
   if(!basarili||hata!=0)throw new IOException("SolidWorks kopya kaydetmeyi doğrulamadı (hata kodu "+hata+"). Merkezi onay gönderilmedi; işlem korundu.");
   KaydetmeTamamlandi(islem,gercekYol);
  }
  public void KaydetmeTamamlandi(KaydetmeIslemi islem,string gercekYol)
  {
   if(islem.Asama=="Onaylandi")return;
   if(islem.Asama!="Ayrildi"&&islem.Asama!="Kaydedildi")throw new InvalidOperationException("Bu işlem kaydetme onayı beklemiyor.");
   if(!DosyaAdiKurali.AyniYol(islem.HedefDosya,gercekYol)||!File.Exists(gercekYol))throw new IOException("Kaydedilen hedef doğrulanamadı; merkezi onay gönderilmedi.");
   islem.Asama="Kaydedildi";gunluk.Yaz(islem);
   OnayIsteginiGonder(islem);
  }
  void OnayIsteginiGonder(KaydetmeIslemi islem)
  {
   servis.Onayla(islem.IslemId,Path.GetFileName(islem.HedefDosya));
   islem.Asama="Onaylandi";gunluk.Yaz(islem);
  }
  public void DogrulanmisOtomatikKaydiTamamla(KaydetmeIslemi islem,string gercekYol)
  {
   var kalici=gunluk.Oku(islem.IslemId);
   if((kalici.KaydetmeBicimi!=KaydetmeBicimi.StandartPencere&&kalici.KaydetmeBicimi!=KaydetmeBicimi.BagimsizBilesen)
    ||(kalici.Asama!="NativeYazimBasladi"&&kalici.Asama!="BagimsizYazimBasladi")||islem.Asama!=kalici.Asama
    ||!DosyaAdiKurali.AyniYol(kalici.HedefDosya,gercekYol)||!DosyaAdiKurali.AyniYol(kalici.GercekDosya,gercekYol))throw new IOException("Doğrulanmış kayıt hedefi/aşaması uyuşmuyor.");
   KayitDosyaKaniti.Sakla(islem,gercekYol);
   islem.Asama="Kaydedildi";gunluk.Yaz(islem); // Kanıt ve onay niyeti tek atomik günlükte.
   KayitOlayTanisi.Yaz("KayitKanitiSaklandi","islem="+islem.IslemId+" asama="+islem.Asama+" boyut="+islem.KayitBoyutu+" sha256="+islem.KayitSHA256);
   if(!DosyaAdiKurali.AyniYol(islem.HedefDosya,gercekYol)||!File.Exists(gercekYol))throw new IOException("Kaydedilen hedef dogrulanamadi; merkezi onay gonderilmedi.");
   OnayIsteginiGonder(islem); // Kaydedildi+kanit zaten kalici; ayni asama ikinci kez yazilmaz.
  }
  public void IptalEt(KaydetmeIslemi islem)
  {
   var kaliciAsama=gunluk.Oku(islem.IslemId).Asama;
   if(kaliciAsama=="NativeYazimBasladi"||kaliciAsama=="BagimsizKuyrukta"||kaliciAsama=="BagimsizYazimBasladi"||kaliciAsama=="Kaydedildi"||kaliciAsama=="Onaylandi")throw new InvalidOperationException("Kayıt sırasındaki, yazılmış veya sonucu belirsiz işlem otomatik iptal edilemez.");
   if(islem.Asama=="BagimsizYazimBasladi"||islem.Asama=="Kaydedildi"||islem.Asama=="Onaylandi")throw new InvalidOperationException("Kaydedilmiş işlem otomatik iptal edilemez.");
   if(islem.Asama=="Iptal")return;
   islem.Asama="IptalBekliyor";gunluk.Yaz(islem);servis.IptalEt(islem.IslemId);islem.Asama="Iptal";gunluk.Yaz(islem);
  }
 }
}
