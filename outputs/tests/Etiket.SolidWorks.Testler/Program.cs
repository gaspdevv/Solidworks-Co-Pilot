using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using Etiket.SolidWorks;

partial class Program
{
 static int sayi;
 static void Kontrol(bool sonuc,string mesaj){if(!sonuc)throw new Exception("TEST BAŞARISIZ: "+mesaj);sayi++;}
 static void Hata(Action islem,string mesaj){try{islem();}catch(Exception){Kontrol(true,mesaj);return;}Kontrol(false,mesaj);}
 [STAThread]
 static int Main(string[] args)
 {
  Console.OutputEncoding=System.Text.Encoding.UTF8;
  try
  {
   if(args.Length==1&&args[0]=="--performans")return PerformansOlc();
   if(args.Length==0){Kaydet27Kurallari();Kaydet25Kurallari();DxfAktarimTestleri();PerformansDogrulamaTestleri();ToparlanmaTestleri();KayitOncesiBaglamaTestleri();}
   if(args.Length==1&&args[0]=="--native-girdi"){NativeKaydetGirdiTestleri.Calistir();return 0;}
   if(args.Length==1&&args[0]=="--native-ad"){NativeDosyaAdiTestleri.Calistir();return 0;}
   if(args.Length==2&&args[0]=="--http")HttpTesti(new Uri(args[1]));
   else if(args.Length==0){StandartKayitTestleri();BagimsizPencereKayitTestleri();OzelAdTestleri();HedefBaglamaTestleri();YuklemeKomutTestleri();BagimsizKayitTestleri();NativeAdAlaniTesti();ErtelenmisTaramaTesti();SurumTestleri();CallbackComTesti();GunlukTestleri();NativeAdDenemeTesti();}else throw new ArgumentException("Yalnızca --http test adresi desteklenir.");
   var sonuc="GEÇTİ: "+sayi+" kontrol. Mod: "+(args.Length==0?"Eklenti COM callback ve işlem günlüğü / .NET Framework 4.8":"Eklenti HTTP / .NET Framework 4.8 / merkez test deposu");
   Console.WriteLine(sonuc);
   var rapor=Environment.GetEnvironmentVariable("ETIKET_EKLENTI_TEST_RAPORU");if(!string.IsNullOrEmpty(rapor))File.WriteAllText(rapor,sonuc+Environment.NewLine+DateTimeOffset.UtcNow.ToString("O"));
   return 0;
  }
  catch(Exception h){Console.Error.WriteLine(h);return 1;}
 }
 static void KayitOncesiBaglamaTestleri()
 {
  var b=new KayitOncesiBelgeBaglama();int okuma=0,bag=0;
  Func<string> etkin=()=>{okuma++;return @"C:\Test\P00061.SLDPRT";};Action bagla=()=>bag++;
  Kontrol(!b.Dene(2,false,etkin,bagla)&&okuma==0,"Bekleyen açılmış belge yokken Save öncesi COM sorgusu olmamalı.");
  b.BelgeAcildi(null);b.BelgeAcildi("");Kontrol(b.BekleyenSayisi==0,"Boş yeni belge adı açılmış kopya sayılmamalı.");
  b.BelgeAcildi(@"C:\Test\P00061.SLDPRT");b.BelgeAcildi(@"C:\TEST\p00061.sldprt");
  Kontrol(b.BekleyenSayisi==1&&okuma==0,"FileOpenPost yalnızca tekilleştirilmiş yolu kaydetmeli; COM çağırmamalı.");
  foreach(var komut in new[]{0,1,620,3494,43})Kontrol(!b.Dene(komut,false,etkin,bagla)&&okuma==0,"Açma/yerleştirme/FarklıKaydet callback'i Save bağlantısı yapmamalı.");
  Kontrol(!b.Dene(2,true,etkin,bagla)&&okuma==0,"Bileşen seçimi/programatik yazım/reentrancy koruması sorguyu tamamen engellemeli.");
  Kontrol(!b.Dene(2,false,()=>@"C:\Test\P00059.SLDPRT",bagla)&&bag==0&&b.BekleyenSayisi==1,"Kaynak CtrlS kopyanın bekleyen bağlantısını tüketmemeli.");
  var d=new YuklemeKomutDurumu();d.KomutAcildi(1);d.KomutAcildi(43);d.KomutAcildi(620);d.KomutKapandi(620);
  Kontrol(!d.Guvenli&&!d.KomutDogrulanabilir,"Kapanışsız New/çizim genel idle taramasını bekletebilir.");
  Kontrol(b.Dene(2,false,etkin,bagla)&&bag==1&&b.BekleyenSayisi==0,"Gerçek sonraki CtrlS yalnızca doğru etkin kopyanın olaylarını bağlamalı; genel tarama gerekmez.");
  Kontrol(!b.Dene(2,false,etkin,bagla)&&bag==1,"Aynı belgeye tekrar olay sağlayıcısı eklenmemeli.");
  b.BelgeAcildi(@"C:\Test\P00061.SLDPRT");
  Hata(()=>b.Dene(2,false,etkin,()=>{throw new IOException("bağlantı başarısız");}),"Bağlantı hatası çağırana bildirilmeli.");
  Kontrol(b.BekleyenSayisi==1,"Başarısız bağlantının yolu tekrar için korunmalı.");
  Kontrol(b.Dene(2,false,etkin,()=>{Kontrol(!b.Dene(2,false,etkin,bagla),"İç içe Save aynı bağlantıyı başlatmamalı.");bag++;}),"Hata sonrası bağlama kilidi açık kalmamalı.");
  b.BelgeAcildi(@"C:\Test\P00061.SLDPRT");b.Baglandi(@"C:\test\p00061.sldprt");Kontrol(b.BekleyenSayisi==0,"Normal idle bağlaması Save öncesi beklemeyi kaldırmalı.");
  b.BelgeAcildi(@"C:\Test\P00061.SLDPRT");b.Temizle();Kontrol(b.BekleyenSayisi==0,"Disconnect bekleyen belge yollarını temizlemeli.");
 }
 sealed class StandartTestBaglami
 {
  public SahteServis S=new SahteServis();public KaliciIslemGunlugu G;public KaydetmeIslemYoneticisi Y;public NativeAdDenemeIslemi D;public StandartKayitYoneticisi N;public KaydetmeIslemi I;public string Kaynak;
  public StandartTestBaglami(string tur="P",string ozelAd=null,bool ilkKayit=false)
  {
   var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
   var alan=Path.Combine(kok.FullName,"work","std-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
   Kaynak=ilkKayit?"":Path.Combine(alan,"Kaynak"+(tur=="P"?".SLDPRT":".SLDASM"));if(Kaynak!="")File.WriteAllText(Kaynak,"Kaynak korunmalı");
   G=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));Y=new KaydetmeIslemYoneticisi(S,G);D=new NativeAdDenemeIslemi(Y);N=new StandartKayitYoneticisi(Y,G);
   var ad=D.Hazirla(tur,alan,true,true);I=D.KaydaAktar(alan,ozelAd??ad);N.Hazirla(I,Kaynak);
  }
  public void DosyayiOlustur()=>File.WriteAllText(I.GercekDosya,"Gerçek CAD olmayan native başarı örneği");
 }
 static void StandartKayitTestleri()
 {
  Hata(()=>NativeDosyaAdiAktarimi.Esitle(IntPtr.Zero,IntPtr.Zero,"P00056"),"Pencere olmayan adreslere native ad bildirimi gönderilmemeli.");
  foreach(var tur in new[]{"P","M"})foreach(var saveType in new[]{1,2,3,4})
  {
   var b=new StandartTestBaglami(tur,null,saveType==1||saveType==2);
   Kontrol(b.G.Oku(b.I.IslemId).KaydetmeBicimi==KaydetmeBicimi.StandartPencere&&b.G.Oku(b.I.IslemId).Asama=="NativeYazimBasladi","Native komut öncesi ayrı yerel biçim ve yazım sınırı kalıcı olmalı.");
   Kontrol(!File.Exists(b.I.GercekDosya)&&b.S.OnayCagrisi==0,"Hazırlama CAD yazmamalı veya merkezi onay vermemeli.");
   b.DosyayiOlustur();var model=saveType==3?b.Kaynak:b.I.GercekDosya;
   b.N.Kaydedildi(saveType,b.I.GercekDosya,model,b.I.IslemId.ToString("D"));b.D.KayitSonucunuAl();
   Kontrol(b.I.Asama=="Onaylandi"&&!b.N.Bekliyor&&!b.D.Aktif&&b.S.OnayCagrisi==1&&b.S.IptalCagrisi==0,"P/M native normal/kopya kaydı tek onayla tamamlanmalı.");
   b.N.Kaydedildi(saveType,b.I.GercekDosya,model,b.I.IslemId.ToString("D"));b.N.SonucuTekrarDene();Kontrol(b.S.OnayCagrisi==1,"Yinelenen post/normal CtrlS yeni merkezi kayıt üretmemeli.");
   if(b.Kaynak!="")Kontrol(File.ReadAllText(b.Kaynak)=="Kaynak korunmalı","Native kopya çekirdeği kaynağı değiştirmemeli.");
  }
  foreach(var saveType in new[]{2,3,4})
  {
   var b=new StandartTestBaglami("P","Mil");b.DosyayiOlustur();b.N.Kaydedildi(saveType,b.I.GercekDosya,saveType==3?b.Kaynak:b.I.GercekDosya,b.I.IslemId.ToString("D"));
   Kontrol(b.I.Asama=="Iptal"&&b.S.IptalCagrisi==1&&b.S.OnayCagrisi==0&&File.Exists(b.I.GercekDosya),"Native özel ad normal/kopya kaydında Documents değil kullanılmayan ad iptali olmalı.");
  }
  foreach(var hata in new[]{"kimlik","hedef","dosya","model","tur","kopya-bos","normal-save"})
  {
   var b=new StandartTestBaglami();if(hata!="dosya")b.DosyayiOlustur();
   var tip=hata=="tur"?99:hata=="kopya-bos"?3:hata=="normal-save"?1:2;
   var hedef=hata=="hedef"?b.Kaynak:hata=="kopya-bos"?"":b.I.GercekDosya;
   var model=hata=="model"||tip==3?b.Kaynak:b.I.GercekDosya;
   Hata(()=>b.N.Kaydedildi(tip,hedef,model,hata=="kimlik"?Guid.NewGuid().ToString("D"):b.I.IslemId.ToString("D")),"Eksik/uyuşmayan native kayıt kanıtı reddedilmeli: "+hata);
   Kontrol(b.S.OnayCagrisi==0&&b.S.IptalCagrisi==0&&b.G.Oku(b.I.IslemId).Asama=="NativeYazimBasladi","Belirsiz native sonuç onay veya numara iptali gönderemez.");
   b.D.IptalEt();Hata(()=>b.Y.IptalEt(b.I),"Native yazım başlamış işlem genel iptalle serbest bırakılamaz.");
   Kontrol(b.S.IptalCagrisi==0,"Pencere kapanışı belirsiz native yazımı iptal edemez.");
  }
  var kayip=new StandartTestBaglami();kayip.DosyayiOlustur();kayip.S.OnayKaybi=true;
  Hata(()=>kayip.N.Kaydedildi(2,kayip.I.GercekDosya,kayip.I.GercekDosya,kayip.I.IslemId.ToString("D")),"Native onay yanıt kaybı kalıcı olmalı.");
  Kontrol(kayip.G.Oku(kayip.I.IslemId).Asama=="Kaydedildi"&&kayip.N.Bekliyor,"Doğrulanmış yazım ayrı onay tekrarına kalmalı.");
  kayip.S.OnayKaybi=false;kayip.N.SonucuTekrarDene();
  Kontrol(kayip.S.Onaylanan.Count==1&&kayip.I.Asama=="Onaylandi"&&!kayip.N.Bekliyor,"Onay tekrarı CAD yazmadan aynı GUID ile tamamlanmalı.");
  var iptal=new StandartTestBaglami("M","OzelMontaj");iptal.DosyayiOlustur();iptal.S.IptalKaybi=true;
  Hata(()=>iptal.N.Kaydedildi(2,iptal.I.GercekDosya,iptal.I.GercekDosya,iptal.I.IslemId.ToString("D")),"Özel native ad iptal yanıt kaybı tutulmalı.");
  Kontrol(iptal.G.Oku(iptal.I.IslemId).Asama=="OzelAdIptalBekliyor","Kayıp iptal yanıtı yazım tekrarına dönüşmemeli.");
  iptal.S.IptalKaybi=false;iptal.N.SonucuTekrarDene();Kontrol(iptal.I.Asama=="Iptal"&&iptal.S.OnayCagrisi==0,"Özel native iptal aynı kimlikle tamamlanmalı.");
  var bos=new StandartTestBaglami();Hata(()=>bos.N.SonucuTekrarDene(),"Sadece dosyanın varlığıyla belirsiz native sonuç onaylanamaz.");
  Kontrol(bos.N.KaydetmeOncesiYolEslesiyor(bos.Kaynak)&&bos.N.KaydetmeOncesiYolEslesiyor(bos.I.GercekDosya),"Native ön bildirim kaynak veya ayrılan hedef yolunu bildirebilir.");
  Kontrol(!bos.N.KaydetmeOncesiYolEslesiyor(Path.Combine(Path.GetDirectoryName(bos.Kaynak),"Baska.SLDPRT"))&&bos.S.OnayCagrisi==0,"Başka yol ön bildirimde reddedilmeli; kaynak eşleşmesi onay sayılmaz.");
  var kopyaAc=new StandartTestBaglami();kopyaAc.DosyayiOlustur();kopyaAc.N.Kaydedildi(4,kopyaAc.I.GercekDosya,kopyaAc.Kaynak,kopyaAc.I.IslemId.ToString("D"));
  Kontrol(kopyaAc.I.Asama=="Onaylandi","Kopya ve aç bildiriminde kaynak RCW'si korunmuş olabilir; gerçek post hedefi doğrulanmalı.");
  Kontrol(kopyaAc.G.Oku(kopyaAc.I.IslemId).KaynakDosya==kopyaAc.Kaynak,"Native kaydın kaynak yolu yeniden başlatma için yerel günlükte korunmalı.");
  var elleEtiket=new StandartTestBaglami("P","P00999.SLDPRT");elleEtiket.DosyayiOlustur();elleEtiket.N.Kaydedildi(2,elleEtiket.I.GercekDosya,elleEtiket.I.GercekDosya,elleEtiket.I.IslemId.ToString("D"));
  Kontrol(elleEtiket.S.OnayCagrisi==0&&elleEtiket.I.Asama=="Iptal","Elle yazılan başka P etiketi bu işlem için otomatik tahsis sayılmamalı.");
  var alan=Path.GetDirectoryName(elleEtiket.I.GercekDosya);var g=new KaliciIslemGunlugu(Path.Combine(alan,"iptal-gunluk"));var s=new SahteServis();var y=new KaydetmeIslemYoneticisi(s,g);var d=new NativeAdDenemeIslemi(y);
  var ilk=d.Hazirla("M",alan,true,true);var id=d.IslemId;d.IptalEt();
  Kontrol(g.Oku(id).Asama=="Iptal"&&s.OnayCagrisi==0&&!File.Exists(Path.Combine(alan,ilk)),"Native pencerede Kaydet öncesi İptal dosya yaratmadan numarayı bırakmalı.");
  Kontrol(d.Hazirla("M",alan,true,true)==ilk&&d.IslemId!=id,"Native pencere iptal numarası farklı GUID ile tekrar verilmeli.");d.IptalEt();
 }
 static void BagimsizPencereKayitTestleri()
 {
  Kontrol(BagimsizDosyaAdiKurali.Tamamla("Mil","P")=="Mil.SLDPRT","Uzantısız özel parça adına CAD uzantısı eklenmeli.");
  Kontrol(BagimsizDosyaAdiKurali.Tamamla("Govde","M")=="Govde.SLDASM","Montaj uzantısı ayrı olmalı.");
  Kontrol(BagimsizDosyaAdiKurali.Tamamla("Mil.sldprt","P")=="Mil.sldprt","Yazılan geçerli uzantı korunmalı.");
  foreach(var ad in new[]{"", " Mil", "Mil.","..\\Mil","C:\\Mil","CON","COM1.SLDPRT","AUX.x.SLDPRT","LPT²","Mil.SLDASM","Mil:deneme","Mil*",".SLDPRT"})
   Hata(()=>BagimsizDosyaAdiKurali.Tamamla(ad,"P"),"Geçersiz veya farklı türde son ad reddedilmeli: "+ad);
  var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
  var alan=Path.Combine(kok.FullName,"work","eklenti-testleri",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
  var kaynak=Path.Combine(alan,"Kaynak.SLDPRT");File.WriteAllText(kaynak,"Kaynak");
  var g=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));var s=new SahteServis();var y=new KaydetmeIslemYoneticisi(s,g);var b=new BagimsizKayitYoneticisi(y,g);
  var d=new NativeAdDenemeIslemi(y);var adOto=d.Hazirla("P",alan,true);var id=d.IslemId;
  Hata(()=>d.KaydaAktar(alan,"Mil.STL"),"Dışa aktarma yazım kuyruğuna alınmamalı.");
  Kontrol(g.Oku(id).Asama=="Ayrildi"&&!d.KaydaDevredildi,"Ad doğrulama hatasında kayıt niyeti yazılmamalı.");
  var i=d.KaydaAktar(alan,adOto);
  Kontrol(i.IslemId==id&&i.KaydetmeBicimi==KaydetmeBicimi.BagimsizBilesen&&g.Oku(id).Asama=="BagimsizKuyrukta","Pencere kapanmadan aynı bağımsız işlem kuyruğa kalıcı yazılmalı.");
  Kontrol(!File.Exists(i.GercekDosya)&&s.OnayCagrisi==0,"Kuyruğa alma CAD veya Documents oluşturmamalı.");
  d.IptalEt();Kontrol(s.IptalCagrisi==0&&d.KaydaDevredildi,"Programatik pencere iptali kayıt rezervasyonunu serbest bırakamaz.");
  Hata(()=>d.KaydaAktar(alan,adOto),"İkinci Kaydet aynı işlemi tekrar devredememeli.");
  Hata(()=>d.Hazirla("P",alan,true),"Bekleyen kayıt sırasında yeni numara ayrılamamalı.");
  Hata(()=>y.IptalEt(i),"Kuyruktaki kayıt genel iptal yoluyla serbest bırakılamamalı.");
  var c=new SahteBilesen{KaynakYolu=kaynak,YeniReferansYolu=kaynak};
  Hata(()=>b.Kaydet(i,c,"Degisen.SLDPRT"),"Kuyruktaki son ad sonradan değiştirilememeli.");
  Kontrol(c.Cagri==0&&g.Oku(id).Asama=="BagimsizKuyrukta","Değişen hedef hiçbir native yazım yapmamalı.");
  b.Kaydet(i,c,adOto);d.KayitSonucunuAl();
  Kontrol(!d.Aktif&&s.OnayCagrisi==1&&s.IptalCagrisi==0&&File.Exists(i.GercekDosya),"Otomatik adla başarılı kuyruk kaydı tek onay vermeli.");
  Hata(()=>b.Kaydet(i,c,adOto),"Başarılı kuyruk kaydı ikinci CAD yazımı yapamamalı.");
  d.Hazirla("P",alan,true);i=d.KaydaAktar(alan,"OzelMil");var ayirma=s.YeniAdSayisi;
  c=new SahteBilesen{KaynakYolu=kaynak,YeniReferansYolu=kaynak};b.Kaydet(i,c,"OzelMil");d.KayitSonucunuAl();
  Kontrol(!d.Aktif&&i.Asama=="Iptal"&&s.OnayCagrisi==1&&s.IptalCagrisi==1&&File.Exists(i.GercekDosya),"Özel adla kuyruk kaydı dosyayı yazmalı ve onay yerine iptal göndermeli.");
  var tekrar=d.Hazirla("P",alan,true);Kontrol(tekrar==i.AyrilanDosyaAdi&&s.YeniAdSayisi==ayirma+1,"Özel adın kullanmadığı numara yeni GUID'e verilebilmeli.");
  i=d.KaydaAktar(alan,"Belirsiz");c=new SahteBilesen{KaynakYolu=kaynak,YeniReferansYolu=kaynak,Sonuc=false};
  Hata(()=>b.Kaydet(i,c,"Belirsiz"),"Native false kuyruk işlemini belirsiz bırakmalı.");d.KayitSonucunuAl();d.IptalEt();
  Kontrol(d.Aktif&&g.Oku(i.IslemId).Asama=="BagimsizYazimBasladi"&&s.IptalCagrisi==1,"Belirsiz yazımda numara tutulmalı ve kapanış iptali gönderilmemeli.");
  Kontrol(File.ReadAllText(kaynak)=="Kaynak","Kaynak dosya değiştirilmemeli.");
  // Gerçek Windows test penceresi: CommandClose gelmiş olsa bile açık diyalogda yazım yok.
  var nt=typeof(PronomEklentisi).Assembly.GetType("Etiket.SolidWorks.NativeAdDenemePenceresi",true);
  var np=Activator.CreateInstance(nt,new object[]{y});var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  var yazim=0;
  nt.GetField("kayit",f).SetValue(np,new Action<KaydetmeIslemi,string>((x,a)=>yazim++));
  nt.GetField("devredilen",f).SetValue(np,i);nt.GetField("sonAd",f).SetValue(np,"Belirsiz.SLDPRT");
  using(var form=new System.Windows.Forms.Form())
  {
   nt.GetField("kayitDialog",f).SetValue(np,form.Handle);nt.GetField("devretHazir",f).SetValue(np,true);
   var eylem=(Action)nt.GetMethod("KayitKapanisiniAl").Invoke(np,null);
   Kontrol(yazim==0,"CommandClose bildirimi dosya yazımını hemen başlatmamalı.");
   Kontrol(nt.GetMethod("KayitKapanisiniAl").Invoke(np,null)==null,"Tekrarlanan CommandClose ikinci kayıt kuyruğu üretmemeli.");
   Hata(()=>eylem(),"Pencere kapanmadan OnIdle gelse bile kayıt reddedilmeli.");
  }
  nt.GetField("kayitDialog",f).SetValue(np,IntPtr.Zero);nt.GetField("devretHazir",f).SetValue(np,true);
  ((Action)nt.GetMethod("KayitKapanisiniAl").Invoke(np,null))();
  Kontrol(yazim==1,"Kapanan pencerenin kaydı yalnızca ertelenmiş eylemden başlamalı.");
  ((IDisposable)np).Dispose();
 }
 static void OzelAdTestleri()
 {
  Kontrol(KaydedilenAdKurali.MerkeziAdMi("P00018.SLDPRT","P00018.SLDPRT"),"Son ad otomatikse merkezi kayıt gerekir.");
  Kontrol(!KaydedilenAdKurali.MerkeziAdMi("P00018.SLDPRT","Mil.SLDPRT"),"Özel ad merkezi kayıt oluşturmamalı.");
  Kontrol(!KaydedilenAdKurali.MerkeziAdMi("P00018.SLDPRT","P00099.SLDPRT"),"Başka P görünümündeki ad otomatik tahsis sayılmamalı.");
  var ad=new NativeAdAlaniDurumu();Kontrol(ad.UygulanacakMetin("P00002","P00002","P00018")=="P00018","Shell ilk kaynak adını otomatik ada çevirmeli.");
  ad.KullaniciDuzenliyor();Kontrol(ad.UygulanacakMetin("P00002","P00002","P00018")=="P00002","Kullanıcının eski kaynak adına dönüşü engellenmemeli.");
  Kontrol(ad.UygulanacakMetin("P00018","P00002","P00018")=="P00018","Otomatik ada geri dönüş korunmalı.");
  var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
  var alan=Path.Combine(kok.FullName,"work","eklenti-testleri",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
  var kaynak=Path.Combine(alan,"P00999.SLDPRT");File.WriteAllText(kaynak,"Kaynak");
  var g=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));var s=new SahteServis();var y=new KaydetmeIslemYoneticisi(s,g);var b=new BagimsizKayitYoneticisi(y,g);
  var i=b.Baslat("P",alan);b.AdiAl(i);var c=new SahteBilesen{KaynakYolu=kaynak,YeniReferansYolu=kaynak};
  b.Kaydet(i,c,"Mil.SLDPRT");
  Kontrol(File.Exists(Path.Combine(alan,"Mil.SLDPRT"))&&s.OnayCagrisi==0&&s.IptalCagrisi==1,"Özel ad kaydedilirken otomatik numara iptal edilmeli, Onay çağrılmamalı.");
  Kontrol(g.Oku(i.IslemId).GercekDosya==Path.Combine(alan,"Mil.SLDPRT")&&i.Asama=="Iptal","Özel dosya ve iptal sonucu günlüğe yazılmalı.");
  i=b.Baslat("P",alan);b.AdiAl(i);c=new SahteBilesen{KaynakYolu=kaynak,YeniReferansYolu=kaynak,Sonuc=false};var iptal=s.IptalCagrisi;
  Hata(()=>b.Kaydet(i,c,"Belirsiz.SLDPRT"),"Native false sonucu numara geri veremez.");Kontrol(s.IptalCagrisi==iptal,"Belirsiz özel kayıt iptal göndermemeli.");
  i=b.Baslat("P",alan);b.AdiAl(i);c=new SahteBilesen{KaynakYolu=kaynak,YeniReferansYolu=kaynak};s.IptalKaybi=true;
  Hata(()=>b.Kaydet(i,c,"Mil2.SLDPRT"),"Özel kayıt sonrası iptal yanıt kaybı tutulmalı.");
  Kontrol(g.Oku(i.IslemId).Asama=="OzelAdIptalBekliyor"&&c.Cagri==1,"Özel dosya tekrar yazılmadan iptal kimliği korunmalı.");
  s.IptalKaybi=false;y.OzelAdIptaliniTekrarDene(i);Kontrol(c.Cagri==1&&i.Asama=="Iptal"&&s.OnayCagrisi==0,"Özel iptal yalnızca aynı GUID ile tekrar edilmeli.");
 }
 static void HedefBaglamaTestleri()
 {
  Kontrol(HedefAdresKurali.Coz(@"Adres: C:\Pronom Kayıt Testi")==@"C:\Pronom Kayıt Testi","Cihazda gözlenen Türkçe tam adres korunmalı.");
  Kontrol(HedefAdresKurali.Coz(@"Address: C:\Pronom\YeniHedef")==@"C:\Pronom\YeniHedef","İngilizce tam adres korunmalı.");
  Kontrol(HedefAdresKurali.Coz(@"Adres: \\sunucu\paylasim\Test")==@"\\sunucu\paylasim\Test","UNC tam adres korunmalı.");
  Kontrol(HedefAdresKurali.Coz("Adres: Masaüstü")==null,"Sanal klasör adından yol uydurulmamalı.");
  Kontrol(HedefAdresKurali.Coz(@"Adres: C:Goreli")==null,"Göreli hedef reddedilmeli.");
  Kontrol(HedefAdresKurali.Coz(@"Adres: C:\Test\..\Baska")==null,"Göreli yol bileşeni reddedilmeli.");
  var id=Guid.NewGuid();var sorgu=Guid.NewGuid();var y=new HedefPencereYaniti{IslemId=id,SorguId=sorgu,SurecId=42,Pencere=999,Klasor=@"C:\Pronom Kayıt Testi"};
  Kontrol(HedefAdresKurali.Dogrula(y,id,sorgu,42,999)==y.Klasor,"Tam eşleşen yanıt kabul edilmeli.");
  Hata(()=>HedefAdresKurali.Dogrula(y,Guid.NewGuid(),sorgu,42,999),"Başka rezervasyon reddedilmeli.");
  Hata(()=>HedefAdresKurali.Dogrula(y,id,Guid.NewGuid(),42,999),"Önceki sorgu yanıtı reddedilmeli.");
  Hata(()=>HedefAdresKurali.Dogrula(y,id,sorgu,43,999),"Başka SolidWorks süreci reddedilmeli.");
  Hata(()=>HedefAdresKurali.Dogrula(y,id,sorgu,42,1000),"Başka kayıt penceresi reddedilmeli.");
  y.Surum=2;Hata(()=>HedefAdresKurali.Dogrula(y,id,sorgu,42,999),"Bilinmeyen protokol reddedilmeli.");y.Surum=1;
  using(var m=new MemoryStream())
  {
   var ser=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(HedefPencereYaniti));ser.WriteObject(m,y);m.Position=0;
   var geri=(HedefPencereYaniti)ser.ReadObject(m);Kontrol(HedefAdresKurali.Dogrula(geri,id,sorgu,42,999)==y.Klasor,"JSON Unicode yol ve kimlikleri korumalı.");
  }
  var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
  var yardimci=Environment.GetEnvironmentVariable("PRONOM_YENI_HEDEF_ARACI")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","Etiket.PencereTanisi","release","Pronom.PencereTanisi.exe"));
  Kontrol(File.Exists(yardimci),"Gercek hedef okuma test araci bulunmali.");
  var istemci=typeof(PronomEklentisi).Assembly.GetType("Etiket.SolidWorks.DisHedefOkuyucu",true).GetMethod("OkuCift");
  var t=(System.Threading.Tasks.Task)istemci.Invoke(null,new object[]{yardimci,System.Diagnostics.Process.GetCurrentProcess().Id,999L,id,sorgu,Guid.NewGuid()});
  Hata(()=>t.GetAwaiter().GetResult(),"Gerçek yardımcı süreç, SolidWorks olmayan test sürecini hedef saymamalı.");
  var alan=Path.Combine(kok.FullName,"work","eklenti-testleri",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
  var hedef=Path.Combine(alan,"YeniHedef");Directory.CreateDirectory(hedef);
  var g=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));var s=new SahteServis();var manager=new KaydetmeIslemYoneticisi(s,g);var d=new NativeAdDenemeIslemi(manager);
  var ad=d.Hazirla("P",alan);id=d.IslemId;var cagri=s.AyirmaCagrisi;
  var yol=d.HedefKlasoruBagla(hedef);Kontrol(Path.GetFileName(yol)==ad&&d.IslemId==id,"Klasör değişimi ad/GUID değiştirmemeli.");
  Kontrol(g.Oku(id).HedefDosya==yol&&g.Oku(id).Asama=="Ayrildi","Hedef aynı işlem günlüğüne kalıcı yazılmalı.");
  d.HedefKlasoruBagla(alan);Kontrol(s.AyirmaCagrisi==cagri&&s.OnayCagrisi==0&&!File.Exists(yol),"Hedef bağlama API numarası/onay/CAD yazımı yapmamalı.");
  Hata(()=>d.HedefKlasoruBagla("YeniHedef"),"Eksik yol rezervasyona yazılmamalı.");
  d.IptalEt();Hata(()=>d.HedefKlasoruBagla(hedef),"İptal edilmiş oturuma hedef bağlanmamalı.");
  Kontrol(!typeof(PronomEklentisi).Assembly.GetReferencedAssemblies().Any(x=>x.Name.StartsWith("UIAutomation")),"Pencere okuma kütüphanesi eklentiye yüklenmemeli.");
 }
 static void YuklemeKomutTestleri()
 {
  var d=new YuklemeKomutDurumu();var cagri=0;var tarama=new ErtelenmisBelgeTaramasi(()=>cagri++);
  Kontrol(d.Guvenli,"Başlangıçta komut koruması açık olmamalı.");
  d.KomutAcildi(1);d.KomutAcildi(1);d.BilesenSecimiBasladi();tarama.Iste();
  if(d.Guvenli)tarama.Calistir();
  Kontrol(cagri==0&&tarama.Bekliyor,"Bileşen ekleme sürerken idle taraması başlamamalı.");
  d.KomutKapandi(1073);Kontrol(!d.Guvenli,"İlgisiz komut kapanışı yükleme korumasını kaldırmamalı.");
  d.BilesenSecimiBitti();Kontrol(!d.Guvenli,"Dosya açma bitse bile yerleştirme komutu hâlâ beklenmeli.");
  d.KomutKapandi(1);Kontrol(d.Guvenli,"Yinelenen pre tek close ile kapanmalı.");
  if(d.Guvenli)tarama.Calistir();Kontrol(cagri==1&&!tarama.Bekliyor,"Kapanış sonrası bekleyen tarama bir kez çalışmalı.");
  d.KomutAcildi(1);d.KomutAcildi(620);d.KomutKapandi(620);Kontrol(!d.Guvenli,"İç komut kapanışı dış komutu kapatmamalı.");
  d.KomutKapandi(1);Kontrol(d.Guvenli,"Son komut kapanınca tarama serbest kalmalı.");
  d.BilesenSecimiBasladi();Kontrol(!d.Guvenli,"Komut bildirimi yokken bile bileşen seçimi korunmalı.");
  d.BilesenSecimiBitti();Kontrol(d.Guvenli,"İptal edilen seçim kalıcı bekleme oluşturmamalı.");
  d.KomutAcildi(1);d.BilesenSecimiBasladi();d.Temizle();Kontrol(d.Guvenli,"Disconnect yükleme durumunu temizlemeli.");
  // Kullanıcının log akışı: kapanışı gelmeyen çizim komutundan sonra başka dosya açılıyor.
  d.KomutAcildi(124);d.KomutAcildi(43);d.KomutAcildi(0);d.KomutKapandi(0);tarama.Iste();
  Kontrol(!d.Guvenli&&d.KomutDogrulanabilir,"Eski çizim bildirimi doğrudan tarama başlatmamalı; resmi durum sorgulanabilmeli.");
  Kontrol(!d.CalisanKomutlaDogrula(true)&&!d.Guvenli,"Etkin çizim/PropertyManager komutu korumayı kaldırmamalı.");
  var once=cagri;if(d.Guvenli)tarama.Calistir();Kontrol(cagri==once,"Etkin komut sürerken yeni dosyanın olay bağlantısı ertelenmeli.");
  Kontrol(d.CalisanKomutlaDogrula(false)&&d.Guvenli,"UI komutu bittiğinde eksik close bildirimleri süresiz bekletmemeli.");
  if(d.Guvenli)tarama.Calistir();Kontrol(cagri==once+1&&!tarama.Bekliyor,"Doğrulanmış kapanış sonrası bekleyen yeni dosya taraması bir kez yapılmalı.");
  Kontrol(!d.CalisanKomutlaDogrula(false),"Doğrulanmış komutlar her idle turunda yeniden temizlenmemeli.");
  foreach(var kritik in new[]{0,1,2,620,3494})
  {
   d.KomutAcildi(124);d.KomutAcildi(kritik);
   Kontrol(!d.KomutDogrulanabilir&&!d.CalisanKomutlaDogrula(false)&&!d.Guvenli,"Açma/yerleştirme/yazım komutu içinden UI sorgusu veya erken serbest bırakma olmamalı: "+kritik);
   d.KomutKapandi(kritik);Kontrol(d.CalisanKomutlaDogrula(false)&&d.Guvenli,"Gerçek komut kapanışından sonra eski çizim bildirimi çözülebilmeli.");
  }
  d.KomutAcildi(124);d.BilesenSecimiBasladi();
  Kontrol(!d.KomutDogrulanabilir&&!d.CalisanKomutlaDogrula(false),"Bileşen seçimi varken resmi UI sorgusu bile başlatılmamalı.");
  d.BilesenSecimiBitti();Kontrol(d.CalisanKomutlaDogrula(false),"Seçim bittikten ve UI boş olduğu doğrulandıktan sonra bekleme çözülebilmeli.");
  d.KomutAcildi(46);d.KomutAcildi(76);d.KomutAcildi(76);d.KomutKapandi(76);d.KomutKapandi(76);
  Kontrol(d.BekleyenKomutlar=="46"&&d.CalisanKomutlaDogrula(false),"Yinelenen çizim ve kapanış bildirimleri negatif sayaç veya kalıcı bekleme oluşturmamalı.");
  // Aynı oturumda önceden yüklenmiş bileşen eklenirse FileOpenPost gelmeyebilir.
  // Eski bir çizim komutunun close'u eksik olsa da seçimi başlatan komutun close'u seçimi bitirmeli.
  d.KomutAcildi(46);d.KomutAcildi(1);d.KomutAcildi(1);d.BilesenSecimiBasladi();d.KomutKapandi(1);
  Kontrol(d.KomutDogrulanabilir,"Eski çizim + tekrar bileşen ekleme kapanışı seçim kilidini kaldırmalı (FileOpenPost yok).");
  d.KomutAcildi(3494);d.KomutKapandi(3494);
  Kontrol(d.CalisanKomutlaDogrula(false)&&d.Guvenli,"Bağımsız kayıt kuyruğu eski çizimden kalan seçim kilidinde beklememeli.");
  for(var tur=0;tur<3;tur++)
  {
   d.KomutAcildi(46);d.KomutAcildi(1);d.BilesenSecimiBasladi();
   d.KomutAcildi(620);d.BilesenSecimiBasladi();d.KomutKapandi(620);
   Kontrol(d.BilesenSecimiSuruyor&&d.SecimiBaslatanKomut==1&&!d.KomutDogrulanabilir,"İç pencere dış bileşen yerleştirmesinin sahipliğini almamalı/bitirmemeli.");
   d.KomutKapandi(1073);Kontrol(d.BilesenSecimiSuruyor,"İlgisiz kapanış bileşen seçimini bitiremez.");
   d.KomutKapandi(1);
   Kontrol(!d.BilesenSecimiSuruyor&&d.SecimiBaslatanKomut==null&&d.KomutDogrulanabilir,"Tekrarlanan aynı bileşenin her yeni montajında sahip komut kapanışı seçimi bitirmeli.");
   Kontrol(!d.CalisanKomutlaDogrula(true)&&d.CalisanKomutlaDogrula(false),"Seçim kapanışı eski çizim doğrulamasını atlamamalı.");
  }
  d.BilesenSecimiBasladi();d.KomutAcildi(620);d.KomutKapandi(620);
  Kontrol(d.BilesenSecimiSuruyor,"Sahibi bilinmeyen seçim, ilgisiz tek komutun kapanışıyla bitmemeli.");
  d.BilesenSecimiBitti();
  var eklenti=new PronomEklentisi();var tip=typeof(PronomEklentisi);var bayrak=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  tip.GetMethod("KomutAciliyor",bayrak).Invoke(eklenti,new object[]{1,0});
  var sonuc=tip.GetMethod("DosyaAdiIstendi",bayrak).Invoke(eklenti,new object[]{1,"",1,15});
  Kontrol((int)sonuc==0,"Bileşen açma callback'i normal native açma sonucunu korumalı.");
  Kontrol(tip.GetField("nativeAd",bayrak).GetValue(eklenti)==null&&tip.GetField("nativeTani",bayrak).GetValue(eklenti)==null,"Bileşen ekleme callback'i native pencere/timer aracı oluşturmamalı.");
  Kontrol(tip.Assembly.GetType("Etiket.SolidWorks.NativeAdresKlasorOkuyucu")==null&&tip.Assembly.GetType("Etiket.SolidWorks.NativeHedefKlasorOkuyucu")==null,"Dönüş DLL'i klasör okuyucu deneylerini içermemeli.");
  Kontrol(!tip.Assembly.GetReferencedAssemblies().Any(x=>x.Name=="Accessibility"),"Dönüş DLL'i deneyin Accessibility bağımlılığını içermemeli.");
 }
 sealed class SahteBilesen:IBagimsizBilesenKaydi
 {
  public string KaynakYolu{get;set;} public string YeniReferansYolu{get;set;}
  public bool Gecerli=true,Sonuc=true,DosyaYaz=true,ReferansDegistir=true,HataVer;
  public int Cagri;public Action YazimOncesi;
  public bool SecimGecerliMi()=>Gecerli;
  public bool Kaydet(string hedef)
  {
   Cagri++;YazimOncesi?.Invoke();if(DosyaYaz)File.WriteAllText(hedef,"Yeni bağımsız test modeli");
   if(ReferansDegistir)YeniReferansYolu=hedef;
   if(HataVer)throw new IOException("Native yazım sonrası belirsiz hata");return Sonuc;
  }
 }
 static void BagimsizKayitTestleri()
 {
  var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
  var alan=Path.Combine(kok.FullName,"work","eklenti-testleri",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
  var kaynak=Path.Combine(alan,"P00999.SLDPRT");File.WriteAllText(kaynak,"Kaynak korunmalı");
  var g=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));var s=new SahteServis();var y=new KaydetmeIslemYoneticisi(s,g);var b=new BagimsizKayitYoneticisi(y,g);
  Func<SahteBilesen> secim=()=>new SahteBilesen{KaynakYolu=kaynak,YeniReferansYolu=kaynak};
  Func<string,KaydetmeIslemi> hazirla=tur=>{var x=b.Baslat(tur,alan);b.AdiAl(x);return x;};
  var i=hazirla("P");var c=secim();
  c.YazimOncesi=()=>Kontrol(g.Oku(i.IslemId).Asama=="BagimsizYazimBasladi","Native çağrıdan önce belirsizlik sınırı kalıcı olmalı.");
  b.Kaydet(i,c);
  Kontrol(c.Cagri==1&&i.Asama=="Onaylandi"&&s.OnayCagrisi==1,"Dosya ve yeni montaj referansı doğrulanınca tek onay gönderilmeli.");
  Kontrol(File.ReadAllText(kaynak)=="Kaynak korunmalı","Bağımsız kayıt kaynak dosyayı değiştirmemeli.");
  Hata(()=>b.Kaydet(i,c),"Başarılı bağımsız işlem tekrar yazılamaz.");
  i=hazirla("P");c=secim();c.Gecerli=false;var once=s.OnayCagrisi;
  Hata(()=>b.Kaydet(i,c),"Değişen bileşen seçimi yazımı engellemeli.");Kontrol(c.Cagri==0&&s.OnayCagrisi==once,"Yanlış seçim native/API çağrısı yapmamalı.");
  c.Gecerli=true;File.WriteAllText(i.HedefDosya,"Mevcut hedef");
  Hata(()=>b.Kaydet(i,c),"Mevcut hedefe yazım engellenmeli.");Kontrol(File.ReadAllText(i.HedefDosya)=="Mevcut hedef"&&c.Cagri==0,"Mevcut hedef korunmalı.");
  i=hazirla("P");c=secim();c.ReferansDegistir=false;
  Hata(()=>b.Kaydet(i,c),"Dosya oluşsa bile eski montaj referansı onaylanmamalı.");
  Kontrol(g.Oku(i.IslemId).Asama=="BagimsizYazimBasladi"&&s.OnayCagrisi==once,"Eksik referans sonucunda işlem belirsiz kalmalı.");
  Hata(()=>b.Kaydet(i,c),"Belirsiz bağımsız yazım yeniden başlatılmamalı.");Hata(()=>y.IptalEt(i),"Dosya yazılmış olabilirken rezervasyon iptal edilmemeli.");
  i=hazirla("M");c=secim();c.DosyaYaz=false;
  Hata(()=>b.Kaydet(i,c),"Referans değişse bile eksik dosya onaylanmamalı.");Kontrol(s.OnayCagrisi==once,"Eksik dosya merkezi kayıt oluşturmamalı.");
  i=hazirla("P");c=secim();c.Sonuc=false;
  Hata(()=>b.Kaydet(i,c),"Native false sonucu dosya mevcut olsa da onaylanmamalı.");
  i=hazirla("P");var eski=g.Oku(i.IslemId);c=secim();c.HataVer=true;
  Hata(()=>b.Kaydet(i,c),"Yazım sonrası istisna belirsiz bırakılmalı.");Hata(()=>y.IptalEt(eski),"Eski bellek nesnesi kalıcı belirsizliği iptal edememeli.");Hata(()=>y.AdiAl(eski),"Eski bellek nesnesi belirsiz yazımı ad ayırma aşamasına geri alamamalı.");
  i=hazirla("P");c=secim();s.OnayKaybi=true;
  Hata(()=>b.Kaydet(i,c),"Başarılı dosyanın onay yanıt kaybı korunmalı.");
  Kontrol(g.Oku(i.IslemId).Asama=="Kaydedildi"&&c.Cagri==1,"Onay kaybı CAD yazımını tekrarlamamalı.");
  s.OnayKaybi=false;Hata(()=>b.OnayiTekrarDene(i,kaynak),"Tekrar onayda yeni referans doğrulanmalı.");
  b.OnayiTekrarDene(i,c.YeniReferansYolu);Kontrol(c.Cagri==1&&i.Asama=="Onaylandi","Aynı GUID ile yalnızca onay tekrar edilmeli.");
 }
 static void NativeAdAlaniTesti()
 {
  // Testin kendi HWND'sinde Shell'in geç ad yazımını taklit eder.
  var tur=typeof(PronomEklentisi).Assembly.GetType("Etiket.SolidWorks.NativeAdDenemePenceresi",true);
  var pencere=Activator.CreateInstance(tur,new object[]{null});
  var bayrak=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  using(var edit=new System.Windows.Forms.TextBox())
  {
   edit.Text="P00002";var h=edit.Handle;
   tur.GetField("otomatikAd",bayrak).SetValue(pencere,"P00099");
   try
   {
    tur.GetMethod("AdAlaniniBagla",bayrak).Invoke(pencere,new object[]{h});
    tur.GetField("bekleyenKaynak",bayrak).SetValue(pencere,"P00002.SLDPRT");edit.ReadOnly=false;edit.Text="P00099";
    Kontrol(edit.Text=="P00099","Merkezi ad ilk yazımda gösterilmeli.");
    edit.Text="P00002";
    Kontrol(edit.Text=="P00099","Salt okunur edit'e geç kaynak adı yazımı merkezi adı bozmamalı.");
    edit.Text="Mil";
    Kontrol(edit.Text=="Mil","Kullanıcının farklı adı korunmalı.");
   }
   finally{((IDisposable)pencere).Dispose();}
   edit.Text="P00002";
   Kontrol(edit.Text=="P00002","Dispose sonrası ad koruması kontrol üzerinde kalmamalı.");
  }
 }
 static void ErtelenmisTaramaTesti()
 {
  var cagri=0;var tarama=new ErtelenmisBelgeTaramasi(()=>cagri++);
  tarama.Iste();tarama.Iste();tarama.Iste();
  Kontrol(cagri==0&&tarama.Bekliyor,"Açılış event'i belge/HTTP sorgusunu çalıştırmamalı.");
  Kontrol(tarama.Calistir()&&cagri==1,"Birden fazla açılış bildirimi tek idle taramasında birleştirilmeli.");
  Kontrol(!tarama.Calistir()&&cagri==1,"Boş idle her turda yeniden COM sorgusu çalıştırmamalı.");
  tarama.Iste();tarama.Temizle();Kontrol(!tarama.Calistir(),"Disconnect bekleyen taramayı iptal etmeli.");
  ErtelenmisBelgeTaramasi icIce=null;var icCagri=0;
  icIce=new ErtelenmisBelgeTaramasi(()=>{icCagri++;if(icCagri==1){icIce.Iste();Kontrol(!icIce.Calistir(),"İç içe idle taraması engellenmeli.");}});
  icIce.Iste();icIce.Calistir();Kontrol(icIce.Bekliyor&&icCagri==1,"Tarama sırasında gelen yeni istek kaybolmamalı.");
  icIce.Calistir();Kontrol(icCagri==2&&!icIce.Bekliyor,"Yeni istek sonraki idle turunda tamamlanmalı.");
  var hata=true;var toparlanan=new ErtelenmisBelgeTaramasi(()=>{if(hata)throw new IOException("Tanı test hatası");});
  toparlanan.Iste();Hata(()=>toparlanan.Calistir(),"Tarama hatası üst seviyeye bildirilmeli.");
  hata=false;toparlanan.Iste();Kontrol(toparlanan.Calistir(),"Hata sonrası kilitli çalışan bayrağı kalmamalı.");
 }
 static void NativeAdDenemeTesti()
 {
  Kontrol(NativeKayitPenceresiKurali.Eslesiyor("Farklı Kaydet","&Kaydet"),"Doğrulanmış Türkçe Save As tanınmalı.");
  Kontrol(NativeKayitPenceresiKurali.Eslesiyor("Save As","&Save"),"İngilizce Save As tanınmalı.");
  Kontrol(!NativeKayitPenceresiKurali.Eslesiyor("Aç","&Aç"),"Bileşen açma penceresine müdahale edilmemeli.");
  Kontrol(!NativeKayitPenceresiKurali.Eslesiyor("Open","&Open"),"İngilizce açma penceresine müdahale edilmemeli.");
  Kontrol(!NativeKayitPenceresiKurali.Eslesiyor("Farklı Kaydet","&Aç"),"Kaydet düğmesi olmayan pencereye müdahale edilmemeli.");
  Kontrol(!NativeKayitPenceresiKurali.Eslesiyor("Başka Pencere","&Kaydet"),"Sadece düğme kimliği eşleşmesi yeterli olmamalı.");
  Kontrol(!NativeKayitPenceresiKurali.Eslesiyor(null,null),"Eksik pencere bilgisi reddedilmeli.");
  var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
  if(kok==null)throw new IOException("Proje kökü bulunamadı.");
  var alan=Path.Combine(kok.FullName,"work","eklenti-testleri",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
  var g=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));var s=new SahteServis();
  var d=new NativeAdDenemeIslemi(new KaydetmeIslemYoneticisi(s,g));
  s.AyirmaKaybi=true;Hata(()=>d.Hazirla("P",alan),"Native deneme rezervasyon yanıt kaybını bildirmeli.");
  var id=d.IslemId;Kontrol(id!=Guid.Empty&&g.Oku(id).Asama=="AdBekleniyor","Native deneme kimliği HTTP öncesinde kalıcı olmalı.");
  s.AyirmaKaybi=false;var ad=d.Hazirla("P",alan);
  Kontrol(d.IslemId==id&&s.YeniAdSayisi==1,"Native deneme yanıt kaybından sonra aynı GUID/adı korumalı.");
  Kontrol(DosyaAdiKurali.Gecerli(ad,"P")&&!File.Exists(Path.Combine(alan,ad))&&s.OnayCagrisi==0,"Pencere denemesi merkezi belge veya CAD oluşturmamalı.");
  Kontrol(d.Hazirla("P",alan)==ad&&d.IslemId==id,"Aynı pencere tekrarı yeni ad almamalı.");
  s.IptalKaybi=true;Hata(()=>d.IptalEt(),"Native deneme iptal yanıt kaybını bildirmeli.");
  Kontrol(d.Aktif&&d.IslemId==id&&g.Oku(id).Asama=="IptalBekliyor","Belirsiz iptal yeni kimlikle gizlenmemeli.");
  s.IptalKaybi=false;d.IptalEt();
  Kontrol(!d.Aktif&&g.Oku(id).Asama=="Iptal"&&s.OnayCagrisi==0,"İptal sonrası belge onayı gönderilmemeli.");
  var yeni=d.Hazirla("P",alan);Kontrol(yeni==ad&&d.IslemId!=id,"İptal numarası yeni GUID ile tekrar kullanılmalı.");d.IptalEt();
 }
 static void SurumTestleri()
 {
  Kontrol(SolidWorksSurumKurali.TestHedefiMi("31.0.1"),"2023 SP0.1 revizyonu yıl kontrolünden geçmeli.");
  Kontrol(SolidWorksSurumKurali.TestHedefiMi("31.1.0"),"Önceki 2023 SP1 hedefi korunmalı.");
  Kontrol(!SolidWorksSurumKurali.TestHedefiMi("30.1.0"),"2022 bu test paketi tarafından kabul edilmemeli.");
  Kontrol(!SolidWorksSurumKurali.TestHedefiMi("32.0.1"),"2024 bu test paketi tarafından kabul edilmemeli.");
  Kontrol(!SolidWorksSurumKurali.TestHedefiMi("31.-1.0"),"Ön sürüm kabul edilmemeli.");
  Kontrol(!SolidWorksSurumKurali.TestHedefiMi("31.abc"),"Bozuk revizyon kabul edilmemeli.");
  Kontrol(!SolidWorksSurumKurali.TestHedefiMi(null),"Eksik revizyon kabul edilmemeli.");
 }
 static void CallbackComTesti()
 {
  // Gerçek .NET Framework CCW: SolidWorks callback parametresi IDispatch ister.
  var eklenti=new PronomEklentisi();IntPtr dispatch=IntPtr.Zero,addin=IntPtr.Zero;
  try
  {
   dispatch=Marshal.GetIDispatchForObject(eklenti);
   Kontrol(dispatch!=IntPtr.Zero,"Callback nesnesi IDispatch sağlamalı.");
   var iid=typeof(PronomEklentisi).GetInterfaces().Single(x=>x.FullName=="SolidWorks.Interop.swpublished.ISwAddin").GUID;
   var sonuc=Marshal.QueryInterface(dispatch,ref iid,out addin);
   Kontrol(sonuc==0&&addin!=IntPtr.Zero,"Callback nesnesi ISwAddin arayüzünü de korumalı.");
  }
  finally{if(addin!=IntPtr.Zero)Marshal.Release(addin);if(dispatch!=IntPtr.Zero)Marshal.Release(dispatch);}
 }
 static void GunlukTestleri()
 {
  var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
  if(kok==null)throw new IOException("Proje kökü bulunamadı.");
  var alan=Path.Combine(kok.FullName,"work","eklenti-testleri",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
  var gunluk=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));var servis=new SahteServis();var yonetici=new KaydetmeIslemYoneticisi(servis,gunluk);
  var i=yonetici.Baslat("P",alan);
  Kontrol(i.IslemId!=Guid.Empty&&gunluk.Oku(i.IslemId).Asama=="AdBekleniyor","Kimlik HTTP'den önce kalıcı olmalı.");
  Kontrol(servis.AyirmaCagrisi==0,"İşlemi başlatmak numara istememeli.");
  servis.AyirmaKaybi=true;Hata(()=>yonetici.AdiAl(i),"Rezervasyon yanıt kaybı yakalanmalı.");
  Kontrol(servis.AyirmaCagrisi==1&&servis.YeniAdSayisi==1,"Yanıt kaybında otomatik tekrar yok.");
  var ikinciGunluk=new KaliciIslemGunlugu(Path.Combine(alan,"gunluk"));i=ikinciGunluk.Oku(i.IslemId);servis.AyirmaKaybi=false;
  var hedef=yonetici.AdiAl(i);
  Kontrol(servis.YeniAdSayisi==1&&servis.AyirmaCagrisi==2,"Yeni süreç aynı işlem kimliğiyle aynı adı almalı.");
  Kontrol(ikinciGunluk.Oku(i.IslemId).HedefDosya==hedef&&i.Asama=="Ayrildi","Hedef Save'den önce kalıcı olmalı.");
  Kontrol(!File.Exists(hedef),"Ad ayırma CAD dosyası üretmemeli.");
  Hata(()=>yonetici.KaydetmeTamamlandi(i,hedef),"Dosya yokken onay olmaz.");Kontrol(servis.OnayCagrisi==0,"Erken onay API'ye ulaşmamalı.");
  File.WriteAllText(hedef,"Test dosyası; CAD değildir.");
  var yanlis=Path.Combine(alan,"Baska.SLDPRT");File.WriteAllText(yanlis,"Test");
  Hata(()=>yonetici.KaydetmeTamamlandi(i,yanlis),"Yanlış Save hedefi onaylanmamalı.");
  servis.OnayKaybi=true;Hata(()=>yonetici.KaydetmeTamamlandi(i,hedef),"Kaydetmeden sonra onay kaybı yakalanmalı.");
  Kontrol(ikinciGunluk.Oku(i.IslemId).Asama=="Kaydedildi","Onay öncesi başarılı Save kalıcı olmalı.");
  Kontrol(servis.Onaylanan.Contains(i.IslemId)&&servis.OnayCagrisi==1,"Onay yanıtı kaybolsa da tek işlem korunmalı.");
  var tekrar=ikinciGunluk.Oku(i.IslemId);servis.OnayKaybi=false;yonetici.KaydetmeTamamlandi(tekrar,hedef);
  Kontrol(tekrar.Asama=="Onaylandi"&&servis.Onaylanan.Count==1,"Yeniden başlatma sonrası onay tekrarı tek kayıt olmalı.");
  Kontrol(servis.YeniAdSayisi==1,"Onay toparlanması yeni numara istememeli.");
  Hata(()=>yonetici.IptalEt(tekrar),"Başarılı Save iptal edilemez.");Kontrol(servis.IptalCagrisi==0,"Başarılı Save için iptal isteği gönderilmez.");
  var c=yonetici.Baslat("M",alan);yonetici.AdiAl(c);servis.IptalKaybi=true;
  Hata(()=>yonetici.IptalEt(c),"İptal kaybı yakalanmalı.");
  Kontrol(ikinciGunluk.Oku(c.IslemId).Asama=="IptalBekliyor","İptal niyeti önce kalıcı olmalı.");
  Hata(()=>yonetici.AdiAl(c),"İptal belirsizliğinde yeniden kaydetme başlamamalı.");
  servis.IptalKaybi=false;c=ikinciGunluk.Oku(c.IslemId);yonetici.IptalEt(c);
  Kontrol(c.Asama=="Iptal"&&servis.IptalEdilen.Count==1,"İptal tekrarı aynı kimlikle toparlanmalı.");
  Kontrol(!ikinciGunluk.BekleyenleriOku().Any(),"Bitmiş işlemler bekleyen listesine girmemeli.");
  var cakisan=yonetici.Baslat("P",alan);servis.ZorlananAd="P99999.SLDPRT";var ad=Path.Combine(alan,servis.ZorlananAd);File.WriteAllText(ad,"ORIJINAL");
  Hata(()=>yonetici.AdiAl(cakisan),"Mevcut dosya hedefi reddedilmeli.");Kontrol(File.ReadAllText(ad)=="ORIJINAL","Var olan dosya değişmemeli.");
  servis.ZorlananAd="..\\Disari.SLDPRT";Hata(()=>yonetici.AdiAl(cakisan),"API yol kaçışı reddedilmeli.");
  servis.ZorlananAd="M99999.SLDASM";Hata(()=>yonetici.AdiAl(cakisan),"Yanlış tür yanıtı reddedilmeli.");
  Kontrol(!DosyaAdiKurali.Gecerli("P00000.SLDPRT","P"),"Sıfır etiket geçersiz.");
  Kontrol(!DosyaAdiKurali.Gecerli("P00001.SLDPRT\n","P"),"Satır sonu içeren dosya adı reddedilmeli.");
  Kontrol(DosyaAdiKurali.Gecerli("P100000.SLDPRT","P"),"Beş hane üstü ad desteklenmeli.");
  Kontrol(!DosyaAdiKurali.Gecerli("P１２３４５.SLDPRT","P"),"ASCII dışı numara reddedilmeli.");
  Hata(()=>yonetici.Baslat("X",alan),"Desteklenmeyen tür engellenmeli.");
  Hata(()=>yonetici.Baslat("P",Path.Combine(alan,"yok")),"Olmayan klasör engellenmeli.");
  foreach(var bicim in new[]{KaydetmeBicimi.KopyaVeDevam,KaydetmeBicimi.KopyaVeAc})
  {
   var kopya=yonetici.Baslat("P",alan,bicim);
   var okunan=ikinciGunluk.Oku(kopya.IslemId);
   Kontrol(okunan.KaydetmeBicimi==bicim,"Kopya seçimi aynı işlem kimliğiyle kalıcı olmalı.");
   servis.ZorlananAd=null;var kopyaYolu=yonetici.AdiAl(kopya);File.WriteAllText(kopyaYolu,"CAD değildir.");
   var onayOnce=servis.OnayCagrisi;
   Hata(()=>yonetici.ApiKaydetmeTamamlandi(kopya,kopyaYolu,false,1),"Başarısız native kopya, dosya var olsa bile onaylanmamalı.");
   Hata(()=>yonetici.ApiKaydetmeTamamlandi(kopya,kopyaYolu,true,1),"Hata kodu varsa kopya onaylanmamalı.");
   Kontrol(servis.OnayCagrisi==onayOnce&&ikinciGunluk.Oku(kopya.IslemId).Asama=="Ayrildi","Belirsiz kopya kimliği korunmalı; merkezi kayıt yazılmamalı.");
   yonetici.ApiKaydetmeTamamlandi(kopya,kopyaYolu,true,0);
   Kontrol(kopya.Asama=="Onaylandi","Başarılı kopya hedefi onaylanmalı.");
  }
  Hata(()=>yonetici.Baslat("P",alan,(KaydetmeBicimi)99),"Geçersiz kayıt biçimi engellenmeli.");
 }
 static void HttpTesti(Uri adres)
 {
  if(!adres.IsLoopback)throw new ArgumentException("HTTP testi yalnızca test aracının loopback servisini kullanabilir.");
  using(var http=new HttpClient{BaseAddress=adres})
  {var bilgi=http.GetStringAsync("api/test-modu").GetAwaiter().GetResult();if(!bilgi.Contains("BellekTesti"))throw new InvalidOperationException("Gerçek veritabanına test yazılmayacak.");}
  using(var s=new HttpAdServisi(adres))
  {
   var id=Guid.NewGuid();var a=s.Ayir(id,"P");Kontrol(a.IslemId==id&&a.Durum=="Ayrildi"&&DosyaAdiKurali.Gecerli(a.DosyaAdi,"P"),"Gerçek net48 HTTP/JSON rezervasyonu.");
   Kontrol(s.Ayir(id,"P").DosyaAdi==a.DosyaAdi,"Net48 tekrarı aynı adı döndürmeli.");
   s.Onayla(id,a.DosyaAdi);s.Onayla(id,a.DosyaAdi);Kontrol(s.Ayir(id,"P").Durum=="Onaylandi","Net48 onay tekrarı güvenli.");
   var m=s.Ayir(Guid.NewGuid(),"M");s.IptalEt(m.IslemId);s.IptalEt(m.IslemId);Kontrol(s.Ayir(m.IslemId,"M").Durum=="Iptal","Net48 iptal tekrarı güvenli.");
  }
 }
 sealed class SahteServis:IAdServisi
 {
  public int AyirmaCagrisi,YeniAdSayisi,OnayCagrisi,IptalCagrisi;public bool AyirmaKaybi,OnayKaybi,IptalKaybi,OnayOncesiKaybi,IptalOncesiKaybi;public string ZorlananAd;
  readonly System.Collections.Generic.Dictionary<Guid,string> adlar=new System.Collections.Generic.Dictionary<Guid,string>();
  public readonly System.Collections.Generic.HashSet<Guid> Onaylanan=new System.Collections.Generic.HashSet<Guid>(),IptalEdilen=new System.Collections.Generic.HashSet<Guid>();
  public AyrilanAd Ayir(Guid id,string tur){AyirmaCagrisi++;if(!adlar.ContainsKey(id)){YeniAdSayisi++;adlar[id]=adlar.Where(x=>IptalEdilen.Contains(x.Key)&&x.Value.StartsWith(tur,StringComparison.Ordinal)&&!adlar.Any(a=>a.Value==x.Value&&!IptalEdilen.Contains(a.Key))).Select(x=>x.Value).OrderBy(x=>x).FirstOrDefault()??(tur+YeniAdSayisi.ToString("D5")+(tur=="P"?".SLDPRT":".SLDASM"));}if(AyirmaKaybi)throw new IOException("Yanıt kaybı.");return new AyrilanAd{IslemId=id,DosyaAdi=ZorlananAd??adlar[id],Durum=IptalEdilen.Contains(id)?"Iptal":Onaylanan.Contains(id)?"Onaylandi":"Ayrildi"};}
  public void Onayla(Guid id,string ad){OnayCagrisi++;if(OnayOncesiKaybi)throw new IOException("Servis kapalı.");Onaylanan.Add(id);if(OnayKaybi)throw new IOException("Onay kaybı.");}
  public void IptalEt(Guid id){IptalCagrisi++;if(IptalOncesiKaybi)throw new IOException("Servis kapalı.");IptalEdilen.Add(id);if(IptalKaybi)throw new IOException("İptal kaybı.");}
 }
}
