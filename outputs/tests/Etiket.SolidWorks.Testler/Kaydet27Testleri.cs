using System;
using System.Reflection;
using Etiket.SolidWorks;
partial class Program
{
 static void Kaydet27Kurallari()
 {
  // Client 22 Oct, 08:36-08:38: old 1 + sketch46, new assembly opens 1
  // again; no second active-set entry, but the latest owner must become 1.
  var d=new YuklemeKomutDurumu();
  d.KomutAcildi(1);d.KomutAcildi(45);d.KomutKapandi(45,-3);d.KomutAcildi(46);
  d.KomutAcildi(8);d.KomutKapandi(8,-2);d.KomutKapandi(45,-2);
  d.KomutAcildi(1);d.KomutAcildi(45);d.KomutKapandi(45,-3);d.KomutAcildi(46);
  d.KomutAcildi(1);d.BilesenSecimiBasladi();
  Kontrol(d.SecimiBaslatanKomut==1,"22Ekim log: yinelenen1 en son acilis olmali; eski46 secime sahip olmamali.");
  Kontrol(d.BekleyenKomutlar=="1,46","Yinelenen komut sayaci artmamali.");
  d.SecimdeBelgeYuklendi(@"C:\CAD\P00106.SLDPRT");d.KomutKapandi(1,-2);
  Kontrol(!d.BilesenSecimiSuruyor&&d.SecimiBaslatanKomut==null,"Gercek1 kapanisi secimi bitirmeli.");
  Kontrol(!d.Guvenli&&d.BekleyenKomutlar=="46"&&d.KomutDogrulanabilir,"Eski46 korumasi atlanmamali; resmi UI durumu sorulabilmeli.");
  d.KomutAcildi(3494);Kontrol(!d.KomutDogrulanabilir,"Bagimsiz penceresi acikken UI sorgusu yapilmamali.");d.KomutKapandi(3494,-3);
  Kontrol(!d.CalisanKomutlaDogrula(true)&&!d.Guvenli,"Gercekten aktif cizim korunmali.");
  Kontrol(d.CalisanKomutlaDogrula(false)&&d.Guvenli,"Pencere kapali+UI komutu bitti ise kuyruk calisabilmeli.");
  Kontrol(!d.CalisanKomutlaDogrula(false),"Ayni kapanis iki kez temizlenmemeli.");
  foreach(var stale in new[]{46,76,124})
  {
   d.Temizle();d.KomutAcildi(1);d.KomutAcildi(stale);d.KomutAcildi(1);d.BilesenSecimiBasladi();
   Kontrol(d.SecimiBaslatanKomut==1,"Tum eski cizimlerden sonra yinelenen1 sahip olmali.");
   d.KomutAcildi(620);d.BilesenSecimiBasladi();d.KomutAcildi(620);d.KomutKapandi(620);
   Kontrol(d.BilesenSecimiSuruyor&&d.SecimiBaslatanKomut==1,"Ic tekrar mevcut dis secimin sahibini degistiremez.");
   d.KomutKapandi(1);Kontrol(!d.BilesenSecimiSuruyor&&d.KomutDogrulanabilir,"Yinelenen1 tek kapanisla bitmeli.");
   Kontrol(d.CalisanKomutlaDogrula(false)&&d.Guvenli,"Dengeli kapanis kalici kuyruk beklemesi yaratmamali.");
  }
  // Exercise controller callbacks from the same failed session, with no COM/CAD.
  var plugin=new PronomEklentisi();var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(PronomEklentisi);
  Action<int> open=c=>type.GetMethod("KomutAciliyor",flags).Invoke(plugin,new object[]{c,0});
  Action<int,int> close=(c,r)=>type.GetMethod("KomutKapandi",flags).Invoke(plugin,new object[]{c,r});
  open(1);open(46);open(1);
  type.GetMethod("DosyaAdiIstendi",flags).Invoke(plugin,new object[]{1,"",1,15});
  var state=(YuklemeKomutDurumu)type.GetField("yuklemeDurumu",flags).GetValue(plugin);
  Kontrol(state.SecimiBaslatanKomut==1,"Callback sirasi yeni acilisi korumali.");
  type.GetMethod("BelgeYuklendi",flags).Invoke(plugin,new object[]{"P00106",@"C:\CAD\P00106.SLDPRT"});close(1,-2);
  Kontrol(!state.BilesenSecimiSuruyor&&state.KomutDogrulanabilir,"Callback kapanisi resmi sorguya izin vermeli.");
  Kontrol(!state.CalisanKomutlaDogrula(true)&&state.CalisanKomutlaDogrula(false),"Callback akisi UI aktiflik kontrolunu korumali.");
 }
}
