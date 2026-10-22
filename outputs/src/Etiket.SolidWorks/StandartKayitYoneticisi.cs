using System;
using System.IO;
namespace Etiket.SolidWorks
{
 // Yalnızca native Save öncesi kayda geçirilen hedefi ve gerçek post bildirimini eşleştirir.
 // Dosya yazmaz; kopya/renk/konfigürasyon gibi seçimleri SolidWorks uygular.
 public sealed class StandartKayitYoneticisi
 {
  readonly KaydetmeIslemYoneticisi yonetici;readonly KaliciIslemGunlugu gunluk;
  public KaydetmeIslemi Bekleyen {get;private set;}
  public bool Bekliyor=>Bekleyen!=null;
  public StandartKayitYoneticisi(KaydetmeIslemYoneticisi yonetici,KaliciIslemGunlugu gunluk){this.yonetici=yonetici;this.gunluk=gunluk;}
  public void Hazirla(KaydetmeIslemi islem,string kaynak)
  {
   if(Bekleyen!=null)throw new InvalidOperationException("Bu belgenin önceki standart kayıt sonucu bekleniyor.");
   var kalici=gunluk.Oku(islem.IslemId);
   if(kalici.KaydetmeBicimi!=KaydetmeBicimi.StandartPencere||kalici.Asama!="NativeYazimBasladi"||islem.Asama!=kalici.Asama
    ||!DosyaAdiKurali.AyniYol(kalici.GercekDosya,islem.GercekDosya))throw new InvalidOperationException("Native kayıt niyeti doğrulanamadı.");
   islem.KaynakDosya=kaynak??"";gunluk.Yaz(islem);Bekleyen=islem;
  }
  public bool HedefEslesiyor(string yol)=>Bekleyen!=null&&DosyaAdiKurali.AyniYol(Bekleyen.GercekDosya,yol);
  // FileSaveNotify kaynak belgenin adını da bildirebilir; bu bir başarı kanıtı değildir.
  public bool KaydetmeOncesiYolEslesiyor(string yol)=>HedefEslesiyor(yol)||(Bekleyen!=null&&!string.IsNullOrWhiteSpace(Bekleyen.KaynakDosya)&&DosyaAdiKurali.AyniYol(Bekleyen.KaynakDosya,yol));
  public void Kaydedildi(int tur,string olayYolu,string modelYolu,string modelKimligi)
  {
   if(Bekleyen==null)return;
   var i=Bekleyen;var kalici=gunluk.Oku(i.IslemId);
   if(i.Asama!="NativeYazimBasladi"||kalici.Asama!=i.Asama)throw new InvalidOperationException("Bu olay yeni native kayıt onayı veremez.");
   if(modelKimligi!=i.IslemId.ToString("D"))throw new IOException("Native kayıt model kimliği uyuşmuyor; numara korundu.");
   // Kopyada eksik hedef, kaynak yoluyla doldurulmaz; kaynak dosyanın varlığı kopya kanıtı değildir.
   var yol=olayYolu;
   if(string.IsNullOrWhiteSpace(yol)&&tur!=3&&tur!=4)yol=modelYolu;
   if(!HedefEslesiyor(yol)||!File.Exists(i.GercekDosya))throw new IOException("Native kayıt hedefi veya dosyası doğrulanamadı.");
   if(tur==1||tur==2)
   {if(!DosyaAdiKurali.AyniYol(modelYolu,i.GercekDosya)||(tur==1&&!string.IsNullOrEmpty(i.KaynakDosya)))throw new IOException("Yeni belge yolu native kayıtla uyuşmuyor.");}
   else if(tur==3)
   {if(!string.Equals(modelYolu??"",i.KaynakDosya,StringComparison.OrdinalIgnoreCase))throw new IOException("Kopya ve devam kaynağı beklenen belge değil.");}
   else if(tur==4)
   {if(!DosyaAdiKurali.AyniYol(modelYolu,i.GercekDosya)&&!string.Equals(modelYolu??"",i.KaynakDosya,StringComparison.OrdinalIgnoreCase))throw new IOException("Kopya ve aç kaynak/hedef yolu uyuşmuyor.");}
   else throw new IOException("Bilinmeyen native kayıt türü; onay gönderilmedi.");
   if(KaydedilenAdKurali.MerkeziAdMi(i.AyrilanDosyaAdi,Path.GetFileName(i.GercekDosya)))
   {yonetici.DogrulanmisOtomatikKaydiTamamla(i,i.GercekDosya);}
   else yonetici.OzelAdlaKaydetmeTamamlandi(i,i.GercekDosya,true,i.GercekDosya);
   SonucuAl();
  }
  public void SonucuTekrarDene()
  {
   if(Bekleyen==null)return;
   var i=Bekleyen;var kalici=gunluk.Oku(i.IslemId);
   var sonuc=new KayitSonucuToparlama(gunluk,yonetici).Tamamla(i.IslemId);i.Asama=sonuc.Asama;
   SonucuAl();
  }
  void SonucuAl(){if(Bekleyen?.Asama=="Onaylandi"||Bekleyen?.Asama=="Iptal")Bekleyen=null;}
 }
}
