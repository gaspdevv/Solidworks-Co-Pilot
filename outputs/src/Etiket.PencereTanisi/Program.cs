using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

internal static class Program
{
 delegate bool Gezici(IntPtr h,IntPtr v);
 [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint thread,Gezici g,IntPtr v);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,Gezici g,IntPtr v);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr h,int id);
 static readonly object kilit=new object();
 static string rapor;static int cikis;
 static int Main(string[] args)
 {
  Console.OutputEncoding=Encoding.UTF8;
  if(args.Length==5&&args[0]=="--hedef")return HedefiOku(args);
  if(args.Length==6&&args[0]=="--hedef-cift")return HedefiOku(args,true);
  if(args.Length==1&&args[0]=="--kendi-testleri")return KendiTestleri();
  if(args.Length!=2||args[0]!="--ad"||string.IsNullOrWhiteSpace(args[1])||args[1].Length>60)
  {Console.Error.WriteLine("Kullanım: Pronom.PencereTanisi.exe --ad AnaKlasor");return 1;}
  rapor=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PencereTanisi-sonuc.txt");
  try
  {
   Yaz("OTURUM",args[1]+" | "+DateTimeOffset.UtcNow.ToString("O")+" | sürüm=1 / salt-okuma");
   var t=new Thread(()=>{try{Oku();}catch(Exception h){cikis=1;Yaz("HATA",h.GetType().Name+": "+h.Message);}}){IsBackground=true};
   t.SetApartmentState(ApartmentState.MTA);t.Start();
   if(!t.Join(12000))
   {Console.Error.WriteLine("Tanı 12 saniyede tamamlanmadı. Bu yardımcı uygulama sonlandırılıyor; SolidWorks sürecine sonlandırma komutu gönderilmedi.");return 2;}
   Yaz("RAPOR",rapor);return cikis;
  }
  catch(Exception h){Console.Error.WriteLine(h.Message);return 1;}
 }
 static int HedefiOku(string[] args,bool cift=false)
 {
  try
  {
   var pid=int.Parse(args[1]);var hwnd=new IntPtr(long.Parse(args[2]));var islem=Guid.Parse(args[3]);var sorgu=Guid.Parse(args[4]);
   var son=cift?Guid.Parse(args[5]):Guid.Empty;
   if(islem==Guid.Empty||sorgu==Guid.Empty||(cift&&(son==Guid.Empty||son==sorgu)))throw new InvalidOperationException("Guncel ve ayri sorgu kimlikleri gerekli.");
   Etiket.SolidWorks.HedefPencereYaniti[] yanit=null;Exception hata=null;
   var t=new Thread(()=>
   {
    try
    {
     yanit=Etiket.SolidWorks.HedefOkumaTekrari.Oku(()=>
     {
      var ilk=TekHedefOku(pid,hwnd,islem,sorgu);
      var sonuc=cift?new[]{ilk,TekHedefOku(pid,hwnd,islem,son)}:new[]{ilk};
      if(cift)Etiket.SolidWorks.HedefAdresKurali.CiftiDogrula(sonuc,islem,sorgu,son,pid,hwnd.ToInt64());
      return sonuc;
     },ms=>Thread.Sleep(ms));
    }
    catch(Exception h){hata=h;}
   }){IsBackground=true};
   t.SetApartmentState(ApartmentState.MTA);t.Start();
   if(!t.Join(12000))return 2;
   if(hata!=null){Console.Error.WriteLine(hata.Message);return 1;}
   var tur=cift?typeof(Etiket.SolidWorks.HedefPencereYaniti[]):typeof(Etiket.SolidWorks.HedefPencereYaniti);
   new System.Runtime.Serialization.Json.DataContractJsonSerializer(tur).WriteObject(Console.OpenStandardOutput(),cift?(object)yanit:yanit[0]);return 0;
  }
  catch(Exception h){Console.Error.WriteLine(h.Message);return 1;}
 }
 static Etiket.SolidWorks.HedefPencereYaniti TekHedefOku(int pid,IntPtr hwnd,Guid islem,Guid sorgu)
 {
    using(var p=Process.GetProcessById(pid))if(p.ProcessName!="SLDWORKS")throw new InvalidOperationException("Hedef süreç SolidWorks değil.");
    GetWindowThreadProcessId(hwnd,out var sahip);
    if(sahip!=(uint)pid||!IsWindowVisible(hwnd)||Sinif(hwnd)!="#32770"||!KayitPenceresiMi(Metin(hwnd),Metin(GetDlgItem(hwnd,1))))throw new InvalidOperationException("İstenen kayıt penceresi açık değil.");
    var adresKokleri=new List<IntPtr>();var n=0;
    EnumChildWindows(hwnd,(h,v)=>{if(++n>192)return false;if(IsWindowVisible(h)&&Sinif(h)=="Address Band Root")adresKokleri.Add(h);return true;},IntPtr.Zero);
    if(adresKokleri.Count==0&&n<=192)throw new Etiket.SolidWorks.HedefAdresHazirDegilHatasi("Adres alanı henüz hazır değil.");
    if(adresKokleri.Count!=1||n>192)throw new InvalidOperationException("Adres alanı kesin değil.");
    var q=new Queue<KeyValuePair<AutomationElement,int>>();q.Enqueue(new KeyValuePair<AutomationElement,int>(AutomationElement.FromHandle(adresKokleri[0]),0));
    var walker=TreeWalker.RawViewWalker;var yollar=new HashSet<string>(StringComparer.OrdinalIgnoreCase);n=0;
    while(q.Count>0&&n++<40)
    {
     var a=q.Dequeue();var b=a.Key.Current;
     if(b.ProcessId!=pid)throw new InvalidOperationException("Adres sağlayıcısı farklı süreçte.");
     // Kullanıcı cihazında doğrulanan tek güncel adres toolbar'ı; breadcrumb parçaları kullanılmaz.
     if(!b.IsOffscreen&&b.ControlType==ControlType.ToolBar&&b.ClassName=="ToolbarWindow32"&&b.AutomationId=="1001")
     {var yol=Etiket.SolidWorks.HedefAdresKurali.Coz(b.Name);if(yol!=null)yollar.Add(yol);}
     if(a.Value>=4)continue;
     var c=walker.GetFirstChild(a.Key);var kardes=0;
     while(c!=null&&kardes++<12&&q.Count<40){q.Enqueue(new KeyValuePair<AutomationElement,int>(c,a.Value+1));c=walker.GetNextSibling(c);}
     if(c!=null)throw new InvalidOperationException("Adres ağacı tam okunamadı.");
    }
    if(q.Count>0)throw new InvalidOperationException("Adres ağacı sınırı aşıldı.");
    if(yollar.Count==0)throw new Etiket.SolidWorks.HedefAdresHazirDegilHatasi("Tek ve tam hedef klasör yolu henüz bulunamadı.");
    if(yollar.Count!=1)throw new InvalidOperationException("Birden fazla hedef klasör yolu bulundu.");
    GetWindowThreadProcessId(hwnd,out sahip);if(sahip!=(uint)pid||!IsWindowVisible(hwnd))throw new InvalidOperationException("Kayıt penceresi okuma sırasında kapandı.");
    foreach(var yol in yollar)return new Etiket.SolidWorks.HedefPencereYaniti{IslemId=islem,SorguId=sorgu,SurecId=pid,Pencere=hwnd.ToInt64(),Klasor=yol};
    throw new InvalidOperationException("Hedef bulunamadi.");
 }
 static void Oku()
 {
  var adaylar=new HashSet<IntPtr>();
  foreach(var p in Process.GetProcessesByName("SLDWORKS"))
  using(p)
  {
   var pid=(uint)p.Id;
   foreach(ProcessThread t in p.Threads)
   using(t)EnumThreadWindows((uint)t.Id,(h,v)=>
   {
    GetWindowThreadProcessId(h,out var sahip);
    if(sahip==pid&&IsWindowVisible(h)&&Sinif(h)=="#32770"&&KayitPenceresiMi(Metin(h),Metin(GetDlgItem(h,1))))adaylar.Add(h);
    return true;
   },IntPtr.Zero);
  }
  if(adaylar.Count!=1){cikis=3;Yaz("SONUC","Tek bir SolidWorks Farklı Kaydet penceresi gerekli. Bulunan="+adaylar.Count);return;}
  foreach(var dialog in adaylar)
  {
   var kokler=new List<IntPtr>();var sayi=0;
   EnumChildWindows(dialog,(h,v)=>{if(++sayi>192)return false;if(IsWindowVisible(h)&&Sinif(h)=="Address Band Root")kokler.Add(h);return true;},IntPtr.Zero);
   if(kokler.Count!=1||sayi>192){cikis=4;Yaz("SONUC","Tek adres alanı bulunamadı. Adet="+kokler.Count);return;}
   GetWindowThreadProcessId(dialog,out var pid);
   Yaz("PENCERE","pid="+pid+" baslik="+Metin(dialog));
   // Yalnızca adres alanı: dosya listesi, arama alanı veya masaüstü kökü taranmaz.
   var kok=AutomationElement.FromHandle(kokler[0]);
   var q=new Queue<KeyValuePair<AutomationElement,int>>();q.Enqueue(new KeyValuePair<AutomationElement,int>(kok,0));
   var walker=TreeWalker.RawViewWalker;var adet=0;
   while(q.Count>0&&adet<40)
   {
    var a=q.Dequeue();var bilgi=a.Key.Current;
    if(bilgi.ProcessId!=(int)pid){Yaz("SONUC","Adres sağlayıcısının süreç kimliği uyuşmuyor.");cikis=5;return;}
    var deger="";
    if(a.Key.TryGetCurrentPattern(ValuePattern.Pattern,out var v))deger=((ValuePattern)v).Current.Value??"";
    Yaz("ADRES","sira="+(++adet)+" derinlik="+a.Value+" tur="+bilgi.ControlType.ProgrammaticName+" sinif="+bilgi.ClassName+" id="+bilgi.AutomationId+" ad="+bilgi.Name+" deger="+deger+" ekranDisi="+bilgi.IsOffscreen);
    if(a.Value>=4)continue;
    var c=walker.GetFirstChild(a.Key);var kardes=0;
    while(c!=null&&kardes++<12&&q.Count<40){q.Enqueue(new KeyValuePair<AutomationElement,int>(c,a.Value+1));c=walker.GetNextSibling(c);}
   }
   Yaz("SONUC","Adres bilgileri okundu. Bu rapor hedef yol veya kayıt onayı değildir; CAD dosyası oluşturulmadı.");
  }
 }
 static int KendiTestleri()
 {
  var adet=0;Action<bool,string> kontrol=(ok,m)=>{if(!ok)throw new Exception(m);adet++;};
  try
  {
   kontrol(KayitPenceresiMi("Farklı Kaydet","&Kaydet"),"Türkçe kayıt penceresi tanınmalı.");
   kontrol(KayitPenceresiMi("Save As","&Save"),"İngilizce kayıt penceresi tanınmalı.");
   kontrol(!KayitPenceresiMi("Aç","&Aç"),"Açma penceresi incelenmemeli.");
   kontrol(!KayitPenceresiMi("Farklı Kaydet","&Aç"),"Düğme ve başlık birlikte doğrulanmalı.");
   kontrol(!KayitPenceresiMi("Başka pencere","&Kaydet"),"Başka uygulama penceresi başlıkla eşleşmemeli.");
   IntPtr hwnd=IntPtr.Zero;System.Windows.Forms.TextBox alan=null;Exception uiHatasi=null;
   using(var hazir=new ManualResetEvent(false))
   {
    var ui=new Thread(()=>
    {
     try
     {
      using(var f=new System.Windows.Forms.Form())
      using(var a=new System.Windows.Forms.TextBox{Text=@"C:\Pronom Kayıt Testi\YeniHedef",AccessibleName="Adres"})
      {
       f.Controls.Add(a);var fh=f.Handle;alan=a;hwnd=a.Handle;hazir.Set();
       System.Windows.Forms.Application.Run();
      }
     }
     catch(Exception h){uiHatasi=h;hazir.Set();}
    }){IsBackground=true};ui.SetApartmentState(ApartmentState.STA);ui.Start();
    if(!hazir.WaitOne(3000)||uiHatasi!=null)throw new Exception("Test HWND hazırlanamadı.",uiHatasi);
    try
    {
     var a=AutomationElement.FromHandle(hwnd);kontrol(a.Current.ProcessId==Process.GetCurrentProcess().Id,"Test kendi sürecinin kontrolünü okumalı.");
     kontrol(a.TryGetCurrentPattern(ValuePattern.Pattern,out var v),"Windows adres test kontrolü ValuePattern sunmalı.");
     kontrol(((ValuePattern)v).Current.Value==@"C:\Pronom Kayıt Testi\YeniHedef","UIA Unicode tam yol değerini okumalı.");
     kontrol(a.Current.Name=="Adres","UIA adres etiketini okumalı.");
     kontrol(((ValuePattern)v).Current.Value==@"C:\Pronom Kayıt Testi\YeniHedef","Okuma test metnini değiştirmemeli.");
    }
    finally
    {
     alan.BeginInvoke((Action)(()=>System.Windows.Forms.Application.ExitThread()));
     if(!ui.Join(3000))throw new Exception("Test UI thread'i sonlanmadı.");
    }
   }
   Console.WriteLine("GEÇTİ: "+adet+" kontrol. Mod: ayrı süreç UIA tanı / kendi Windows test kontrolü.");return 0;
  }
  catch(Exception h){Console.Error.WriteLine(h);return 1;}
 }
 static bool KayitPenceresiMi(string baslik,string dugme)
 {
  return (baslik=="Farklı Kaydet"||baslik=="Save As")&&(dugme.Replace("&","").Trim()=="Kaydet"||dugme.Replace("&","").Trim()=="Save");
 }
 static string Sinif(IntPtr h){var s=new StringBuilder(128);GetClassName(h,s,s.Capacity);return s.ToString();}
 static string Metin(IntPtr h){var s=new StringBuilder(512);GetWindowText(h,s,s.Capacity);return s.ToString();}
 static void Yaz(string tur,string metin)
 {
  var s=tur+" | "+(metin??"").Replace("\r"," ").Replace("\n"," ");
  lock(kilit){File.AppendAllText(rapor,s+Environment.NewLine,new UTF8Encoding(false));Console.WriteLine(s);}
 }
}
