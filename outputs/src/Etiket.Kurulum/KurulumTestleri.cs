using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
namespace Pronom.Kurulum
{
 static class KurulumTestleri
 {
  static int sayi;
  static void Ok(bool value,string why){if(!value)throw new Exception("TEST: "+why);sayi++;}
  static void Ret(Action a,string why){try{a();}catch{Ok(true,why);return;}throw new Exception("TEST: Ret yok: "+why);}
  public static int Calistir(string work)
  {
   Ok(KurulumCekirdegi.KurulumDurumu(null,false,false,false).Dugme=="Kur","Temiz bilgisayarda Kur.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.EskiDll,false,false,false).Dugme=="Güncelle","Eski klasorde21 guncellenmeli.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.EskiDll,true,false,false).Dugme=="Güncelle","Hedefte21 guncellenmeli.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.PerformansDll,true,false,false).Dugme=="Güncelle","Kurulu23 surumu DXF24e guncellenmeli.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.DxfDll,true,false,false).Dugme=="Güncelle","Kurulu24 surumu Kaydet25e guncellenmeli.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.Kaydet25Dll,true,false,false).Dugme=="Güncelle","Kurulu25 surumu Kaydet26ya guncellenmeli.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.Kaydet26Dll,true,false,false).Dugme=="Güncelle","Kurulu26 surumu Kaydet27ye guncellenmeli.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.YeniDll,true,false,true).Dugme=="Güncelle","Onceki23setup guncellenmeli.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.YeniDll,true,true,true).Dugme=="Kontrol et","Guncel kurulum tekrar dosya degistirmeden kontrol.");
   Ok(KurulumCekirdegi.KurulumDurumu(null,true,false,true).Dugme=="Onar","COM kaydi eksik kurulum onarilir.");
   Ok(KurulumCekirdegi.KurulumDurumu(KurulumCekirdegi.YeniDll,true,false,false).Dugme=="Onar","Eksik paket onarilir.");
   Ret(()=>KurulumCekirdegi.KurulumDurumu("BILINMIYOR",true,false,false),"Taninmayan kayit guncellenmez.");
   Directory.CreateDirectory(work);var root=Path.Combine(work,"kurulum-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   var kaynak=Path.Combine(root,"paket");KurulumCekirdegi.PaketiAc(kaynak);
   var liste=KurulumCekirdegi.Liste();Ok(liste.Length>=6,"Gomulu tum paket acilmali.");
   Ok(File.Exists(Path.Combine(kaynak,"BekleyenKayitlariToparla.ps1")),"Bekleyen kayitlar penceresinin toparlama betigi pakette olmali.");
   foreach(var f in liste)Ok(KurulumCekirdegi.Hash(KurulumCekirdegi.AltYol(kaynak,f.Ad))==f.SHA256,"Her dosya hash uyumlu.");
   foreach(var p in new[]{"../disari","C:\\disari","x:disari"})Ret(()=>KurulumCekirdegi.AltYol(root,p),"Klasor disina cikilamaz.");
   foreach(var u in new[]{"file:///C:/test","http://u:p@localhost:5082","http://localhost:5082/api/health","http://localhost:5082/?x=1","http://0.0.0.0:5082"})
    Ret(()=>KurulumCekirdegi.ApiAdresi(u),"Yanlis API reddi.");
   Ok(KurulumCekirdegi.ApiAdresi("http://192.0.2.10:5082").AbsoluteUri=="http://192.0.2.10:5082/","API normallesmeli.");
   var hedef=Path.Combine(root,"temiz");string kayit=null;int geri=0;
   KurulumCekirdegi.MakineDosyalari(kaynak,hedef,p=>kayit=p,p=>geri++,()=>{});
   Ok(kayit==Path.Combine(hedef,"Pronom.SolidWorks.dll")&&geri==0,"Temiz kurulumu kayit izlemeli.");
   foreach(var f in liste)Ok(KurulumCekirdegi.Hash(KurulumCekirdegi.AltYol(hedef,f.Ad))==f.SHA256,"Kurulan paket dogru.");
   var backupBefore=Directory.GetDirectories(Path.Combine(hedef,"Yedekler")).Length;
   KurulumCekirdegi.MakineDosyalari(kaynak,hedef,p=>{},p=>{},()=>{});
   Ok(Directory.GetDirectories(Path.Combine(hedef,"Yedekler")).Length==backupBefore+1,"Tekrar kurulum eski dosyalari yedeklemeli.");
   var eski=liste.ToDictionary(f=>f.Ad,f=>KurulumCekirdegi.Hash(KurulumCekirdegi.AltYol(hedef,f.Ad)));
   Ret(()=>KurulumCekirdegi.MakineDosyalari(kaynak,hedef,p=>{throw new IOException("RegAsm fake");},p=>geri++,()=>{}),"COM hata geri donmeli.");
   Ok(geri==1,"COM geri donus cagrilmali.");
   foreach(var f in liste)Ok(KurulumCekirdegi.Hash(KurulumCekirdegi.AltYol(hedef,f.Ad))==eski[f.Ad],"COM hatasinda dosyalar korunmali.");
   var freshFail=Path.Combine(root,"hata-temiz");
   Ret(()=>KurulumCekirdegi.MakineDosyalari(kaynak,freshFail,p=>{},p=>geri++,()=>{},(a,b)=>{if(b.EndsWith(".config",StringComparison.OrdinalIgnoreCase))throw new IOException("copy fake");File.Copy(a,b,true);}),"Kopyalama hatasi geri donmeli.");
   Ok(!liste.Any(f=>File.Exists(KurulumCekirdegi.AltYol(freshFail,f.Ad))),"Yarim yeni dosya kalmamali.");
   var open=Path.Combine(root,"acik");Ret(()=>KurulumCekirdegi.MakineDosyalari(kaynak,open,p=>{},p=>{},()=>{throw new IOException("SWacik");}),"AcikSWreddi");
   Ok(!Directory.Exists(open),"AcikSWde dosya degismemeli.");
   var yabanci=Path.Combine(root,"yabanci");Directory.CreateDirectory(yabanci);File.WriteAllText(Path.Combine(yabanci,"Pronom.SolidWorks.dll"),"yabanci");
   Ret(()=>KurulumCekirdegi.MakineDosyalari(kaynak,yabanci,p=>{},p=>{},()=>{}),"Yabanci DLL reddi.");
   var local=Path.Combine(root,"kullanici");Directory.CreateDirectory(Path.Combine(local,"Islemler"));
   var journal=Path.Combine(local,"Islemler","korunan.json");File.WriteAllText(journal,"KORU");object startup=null;
   var uri=KurulumCekirdegi.ApiAdresi("http://192.0.2.10:5082");
   KurulumCekirdegi.KullaniciAyarlari(local,uri,true,()=>startup,x=>startup=x);
   Ok((int)startup==1,"Kendi kullanicisinda baslangic acilmali.");
   Ok(File.ReadAllText(Path.Combine(local,"ApiAdresi.txt"))==uri.AbsoluteUri,"APIyazilmali.");
   foreach(var flag in new[]{"StandartKayit.acik","BagimsizKayit.acik","NativeAdDenemesi.acik","KayitOlayTanisi.acik"})Ok(File.ReadAllText(Path.Combine(local,flag))=="1","Kayit modu hazir olmali.");
   Ok(File.ReadAllText(journal)=="KORU","Islem gunlugune dokunulmamali.");
   var eskiAdres=File.ReadAllText(Path.Combine(local,"ApiAdresi.txt"));
   Ret(()=>KurulumCekirdegi.KullaniciAyarlari(local,KurulumCekirdegi.ApiAdresi("http://127.0.0.1:5082"),false,()=>startup,x=>{if(Convert.ToInt32(x)==0)throw new IOException("registry fake");startup=x;}),"HKCUhatasirollback");
   Ok(File.ReadAllText(Path.Combine(local,"ApiAdresi.txt"))==eskiAdres&&(int)startup==1,"Kullanici ayarlari atomik geri donmeli.");
   Ok(Directory.GetFiles(Path.Combine(local,"AyarYedekleri"),"ApiAdresi.txt",SearchOption.AllDirectories).Length>0,"EskiAPIyedekolmali.");
   var sonuc="GECTI: "+sayi+" setup cekirdegi kontrolu. Gomulu gercek dosyalar/izole klasor; COM/HKCU/UAC ve gercek SolidWorks yuklemesi taklit edildi.";
   File.WriteAllText(Path.Combine(root,"sonuc.txt"),sonuc,Encoding.UTF8);Console.WriteLine(sonuc);return 0;
  }
  public static int Guncelleme(string work,string eskiDll)
  {
   var eskiHash=KurulumCekirdegi.Hash(eskiDll);Ok(eskiHash==KurulumCekirdegi.EskiDll||eskiHash==KurulumCekirdegi.PerformansDll||eskiHash==KurulumCekirdegi.DxfDll||eskiHash==KurulumCekirdegi.Kaydet25Dll||eskiHash==KurulumCekirdegi.Kaydet26Dll,"Gercek21/23/24/25/26 guncelleme fixture'i gerekir.");
   var root=Path.Combine(work,"guncelleme-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(root);
   var kaynak=Path.Combine(root,"paket");KurulumCekirdegi.PaketiAc(kaynak);
   var hedef=Path.Combine(root,"hedef");Directory.CreateDirectory(hedef);
   var dll=Path.Combine(hedef,"Pronom.SolidWorks.dll");File.Copy(eskiDll,dll);
   var cad=Path.Combine(hedef,"ornek.SLDPRT");File.WriteAllText(cad,"KORUNAN-CAD");
   var journal=Path.Combine(hedef,"islem.json");File.WriteAllText(journal,"KORUNAN-GUNLUK");
   var geri=0;
   Ret(()=>KurulumCekirdegi.MakineDosyalari(kaynak,hedef,p=>{throw new IOException("COM hata");},p=>{geri++;Ok(KurulumCekirdegi.Hash(p)==eskiHash,"COM geri donerken onceki DLL gelmeli.");},()=>{}),"Guncelleme hata geri donmeli");
   Ok(geri==1&&KurulumCekirdegi.Hash(dll)==eskiHash,"Basarisiz guncelleme onceki DLL korur.");
   KurulumCekirdegi.MakineDosyalari(kaynak,hedef,p=>Ok(KurulumCekirdegi.Hash(p)==KurulumCekirdegi.YeniDll,"COM yeni24dosyaya yonlenir."),p=>{},()=>{});
   Ok(KurulumCekirdegi.Hash(dll)==KurulumCekirdegi.YeniDll,"Eski gercek dosya24olur.");
   Ok(Directory.GetFiles(Path.Combine(hedef,"Yedekler"),"Pronom.SolidWorks.dll",SearchOption.AllDirectories).Any(p=>KurulumCekirdegi.Hash(p)==eskiHash),"Eski dosya yedegi korunur.");
   Ok(File.ReadAllText(cad)=="KORUNAN-CAD"&&File.ReadAllText(journal)=="KORUNAN-GUNLUK","Ilgisiz CAD/journal korunur.");
   var eskiKlasor=Path.Combine(root,"eski-klasor");Directory.CreateDirectory(eskiKlasor);
   var eskiYol=Path.Combine(eskiKlasor,"Pronom.SolidWorks.dll");File.Copy(eskiDll,eskiYol);
   string codebase=eskiYol;var yeniHedef=Path.Combine(root,"yeni-klasor");
   KurulumCekirdegi.MakineDosyalari(kaynak,yeniHedef,p=>codebase=p,p=>codebase=eskiYol,()=>{});
   Ok(codebase==Path.Combine(yeniHedef,"Pronom.SolidWorks.dll"),"Eski klasorden yeni klasore COM gecisi.");
   Ok(KurulumCekirdegi.Hash(eskiYol)==eskiHash,"Eski klasor dosyalari silinmez.");
   File.WriteAllText(Path.Combine(root,"sonuc.txt"),"GECTI: "+sayi+" gercek eski DLL ile izole guncelleme kontrolu. Onceki hash: "+eskiHash+". Gercek COM/UAC cagrilmadi.");
   return 0;
  }
 }
}
