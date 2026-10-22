using System;
using System.IO;
using Etiket.SolidWorks;
partial class Program
{
 static void Kaydet25Kurallari()
 {
  var d=new YuklemeKomutDurumu();d.BilesenSecimiBasladi();
  d.KomutKapandi(-3,-2);Kontrol(d.BilesenSecimiSuruyor,"Yukleme kaniti olmadan sahipsiz secim temizlenmemeli.");
  d.SecimdeBelgeYuklendi("");d.KomutKapandi(-3,-2);Kontrol(d.BilesenSecimiSuruyor,"Adsiz/virtual belge tek basina yukleme kaniti degil.");
  d.SecimdeBelgeYuklendi(@"C:\CAD\AS3201F_03_08SA.sldprt");
  d.KomutKapandi(1073,-3);Kontrol(d.BilesenSecimiSuruyor,"Ilgisiz kapanis yukleme kaniti olsa da secimi temizlememeli.");
  d.KomutKapandi(-3,-3);Kontrol(d.BilesenSecimiSuruyor,"Diger kapanis nedeni sahipsiz secimi temizlememeli.");
  d.KomutKapandi(-3,-2);Kontrol(d.Guvenli&&!d.BilesenSecimiSuruyor,"Gercek log: ownerless component + load + close(-3,-2) secimi bitirmeli.");
  d.KomutAcildi(3494);Kontrol(!d.Guvenli,"Bagimsiz komut surerken kuyruk calismamali.");d.KomutKapandi(3494,-3);
  Kontrol(d.Guvenli,"Gercek log: Bagimsiz kapaninca kuyruk idle icin serbest olmali.");
  d.BilesenSecimiBasladi();d.KomutKapandi(-3,-2);Kontrol(d.BilesenSecimiSuruyor,"Onceki yukleme kaniti yeni secime tasinmamali.");
  d.SecimdeBelgeYuklendi(@"C:\CAD\P.sldprt");d.KomutAcildi(3494);d.KomutKapandi(-3,-2);Kontrol(d.BilesenSecimiSuruyor,"Baska komut acikken sahipsiz secim temizlenmemeli.");d.Temizle();
  d.KomutAcildi(1);d.BilesenSecimiBasladi();d.SecimdeBelgeYuklendi(@"C:\CAD\P.sldprt");d.KomutKapandi(-3,-2);
  Kontrol(d.BilesenSecimiSuruyor&&d.SecimiBaslatanKomut==1,"Sahipli yerlesim erken serbest birakilmamali.");d.KomutKapandi(1);Kontrol(d.Guvenli,"Sahipli normal kapanis korunmali.");
  // Verify the actual controller callback wiring, not only the state class.
  var eklenti=new PronomEklentisi();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var tip=typeof(PronomEklentisi);
  tip.GetMethod("DosyaAdiIstendi",flags).Invoke(eklenti,new object[]{1,"",1,15});
  tip.GetMethod("BelgeYuklendi",flags).Invoke(eklenti,new object[]{"AS3201F_03_08SA",@"C:\CAD\AS3201F_03_08SA.sldprt"});
  tip.GetMethod("KomutKapandi",flags).Invoke(eklenti,new object[]{-3,-2});
  Kontrol(((YuklemeKomutDurumu)tip.GetField("yuklemeDurumu",flags).GetValue(eklenti)).Guvenli,"Gercek callback sirasi sahip/neden/yukleme kanitini aktarmali.");
  Kontrol(NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0202,0,true,false,false),"Kaydet fare birakma Shell'den once yakalanmali.");
  Kontrol(NativeKaydetGirdiKoruyucusu.Yakalanmali(0x00F5,0,true,false,false),"BM_CLICK Shell'den once yakalanmali.");
  Kontrol(NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0100,13,false,true,false),"Ad alaninda Enter yakalanmali.");
  Kontrol(NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0100,13,true,false,false),"Dugmede Enter yakalanmali.");
  Kontrol(NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0100,13,false,false,true),"Diyalog Enter yakalanmali.");
  Kontrol(NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0101,32,true,false,false),"Dugme Space yakalanmali.");
  Kontrol(!NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0100,27,false,true,false),"Escape iptali degismemeli.");
  Kontrol(!NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0100,13,false,false,false),"Adres/cancel/list alaninin Enter'i degismemeli.");
  Kontrol(!NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0202,0,false,true,false),"Ad alaninda fare tiklamasi degismemeli.");
  Kontrol(!NativeKaydetGirdiKoruyucusu.Yakalanmali(0x0111,1,false,false,true),"Dogrulanmis WM_COMMAND mevcut tek kullanimli korumadan gecmeli.");
  int okuma=0,bekleme=0;
  var sonuc=HedefOkumaTekrari.Oku(()=>{okuma++;if(okuma<3)throw new HedefAdresHazirDegilHatasi("hazir degil");return 7;},ms=>{Kontrol(ms==150,"Bekleme 150ms ile sinirli.");bekleme++;});
  Kontrol(sonuc==7&&okuma==3&&bekleme==2,"Yalniz bos adres en cok uc kez okunmali.");
  okuma=0;bekleme=0;Hata(()=>HedefOkumaTekrari.Oku<int>(()=>{okuma++;throw new HedefAdresHazirDegilHatasi("bos");},ms=>bekleme++),"Adres bulunamazsa fail closed.");
  Kontrol(okuma==3&&bekleme==2,"Tekrarlar sinirsiz olmamali.");
  okuma=0;bekleme=0;Hata(()=>HedefOkumaTekrari.Oku<int>(()=>{okuma++;throw new IOException("hedef degisti");},ms=>bekleme++),"Degisen/celiskili hedef reddedilmeli.");
  Kontrol(okuma==1&&bekleme==0,"Genel hata tekrar denenmemeli.");
  okuma=0;Kontrol(HedefOkumaTekrari.Oku(()=>++okuma,ms=>{throw new Exception("gereksiz bekleme");})==1,"Hazir adreste ek gecikme olmamali.");
 }
}
