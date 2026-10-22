using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace Etiket.SolidWorks
{
 public static class KayitDosyaKaniti
 {
  static string Ozet(Stream dosya){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(dosya)).Replace("-","");}
  public static void Sakla(KaydetmeIslemi i,string yol)
  {
   // Bu çağrı yalnızca gerçek native başarı + GUID/yol/ref doğrulaması SONRASINDA yapılır.
   var once=new FileInfo(yol);var boy=once.Length;var tarih=once.LastWriteTimeUtc;
   using(var f=new FileStream(yol,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
   {
    if(boy<=0)throw new IOException("Kaydedilen dosya boş; sonuç kanıtı oluşturulmadı.");
    var ozet=Ozet(f);var sonra=new FileInfo(yol);
    if(f.Length!=boy||sonra.Length!=boy||sonra.LastWriteTimeUtc!=tarih)throw new IOException("Kayıt kanıtı okunurken dosya değişti.");
    i.GercekDosya=Path.GetFullPath(yol);i.KayitBoyutu=boy;i.KayitSHA256=ozet;i.KayitKanitiZamani=DateTimeOffset.UtcNow.ToString("O");
   }
  }
  public static bool Var(KaydetmeIslemi i)=>i.KayitBoyutu>0&&Regex.IsMatch(i.KayitSHA256??"",@"\A[A-F0-9]{64}\z")&&DateTimeOffset.TryParse(i.KayitKanitiZamani,out _);
  public static FileStream DogrulaVeKilitle(KaydetmeIslemi i)
  {
   if(!Var(i))throw new IOException("Bu işlemde kalıcı başarılı kayıt kanıtı yok; elle inceleme gerekli.");
   var f=new FileStream(i.GercekDosya,FileMode.Open,FileAccess.Read,FileShare.Read);
   try{if(f.Length!=i.KayitBoyutu||Ozet(f)!=i.KayitSHA256)throw new IOException("Dosya başarılı kayıt kanıtıyla eşleşmiyor; merkezi sonuç gönderilmedi.");return f;}
   catch{f.Dispose();throw;}
  }
 }
 public sealed class KayitSonucuToparlama
 {
  readonly KaliciIslemGunlugu gunluk;readonly KaydetmeIslemYoneticisi yonetici;
  public KayitSonucuToparlama(KaliciIslemGunlugu gunluk,KaydetmeIslemYoneticisi yonetici){this.gunluk=gunluk;this.yonetici=yonetici;}
  public static bool Uygun(KaydetmeIslemi i)=>
   (i.KaydetmeBicimi==KaydetmeBicimi.StandartPencere||i.KaydetmeBicimi==KaydetmeBicimi.BagimsizBilesen)
   &&(i.Asama=="Kaydedildi"||i.Asama=="OzelAdIptalBekliyor")&&KayitDosyaKaniti.Var(i);
  public KaydetmeIslemi Tamamla(Guid id)
  {
   var i=gunluk.Oku(id);
   if(i.Asama=="Onaylandi"||i.Asama=="Iptal")return i; // Eski iptal yeni numara sahibini etkilemez.
   if(!Uygun(i))throw new InvalidOperationException("Yalnızca kanıtlı kayıt sonucu tamamlanabilir. Belirsiz yazım/ayırma otomatik tekrarlanmaz.");
   if(!DosyaAdiKurali.Gecerli(i.AyrilanDosyaAdi,i.BelgeTuru)||!DosyaAdiKurali.AyniYol(i.HedefDosya,Path.Combine(i.HedefKlasor,i.AyrilanDosyaAdi))
    ||!DosyaAdiKurali.AyniYol(Path.GetDirectoryName(i.GercekDosya),i.HedefKlasor)
    ||!string.Equals(Path.GetFileName(i.GercekDosya),BagimsizDosyaAdiKurali.Tamamla(Path.GetFileName(i.GercekDosya),i.BelgeTuru),StringComparison.Ordinal))throw new IOException("İşlem hedefi veya belge türü kanıtla uyuşmuyor.");
   var otomatik=KaydedilenAdKurali.MerkeziAdMi(i.AyrilanDosyaAdi,Path.GetFileName(i.GercekDosya));
   if((i.Asama=="Kaydedildi")!=otomatik)throw new IOException("Onay/iptal niyeti son dosya adıyla uyuşmuyor.");
   using(KayitDosyaKaniti.DogrulaVeKilitle(i))
   {
    if(otomatik)yonetici.KaydetmeTamamlandi(i,i.GercekDosya);
    else yonetici.OzelAdIptaliniTekrarDene(i);
   }
   return i;
  }
 }
}
