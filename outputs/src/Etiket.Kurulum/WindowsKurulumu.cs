using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Pronom.Kurulum
{
 static class WindowsKurulumu
 {
  public static string Hedef=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Pronom","SolidWorks");
  public static string Kullanici=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pronom","SolidWorksEtiketleme");
  const string Clsid=@"SOFTWARE\Classes\CLSID\"+KurulumCekirdegi.AddinGuid+@"\InprocServer32";
  const string Startup=@"Software\SOLIDWORKS\AddInsStartup\"+KurulumCekirdegi.AddinGuid;
  const string Uninstall=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PronomSolidWorks";
  static RegistryKey Hklm()=>RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
  public static bool Yonetici=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
  public static void KapaliMi(){if(Process.GetProcessesByName("SLDWORKS").Any())throw new InvalidOperationException("Önce açık belgelerinizi koruyarak SolidWorks uygulamasını tamamen kapatın.");}
  public static void OnKontrol()
  {
   if(!Environment.Is64BitOperatingSystem||IntPtr.Size!=8)throw new InvalidOperationException("64 bit Windows gerekli.");
   using(var root=Hklm())
   {
    using(var fx=root.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
     if(Convert.ToInt32(fx?.GetValue("Release",0))<528040)throw new InvalidOperationException(".NET Framework 4.8 gerekli.");
    using(var sw=root.OpenSubKey(@"SOFTWARE\SolidWorks\SOLIDWORKS 2023\Setup"))
    {
     var folder=sw?.GetValue("SolidWorks Folder") as string;
     if(string.IsNullOrWhiteSpace(folder)||!File.Exists(Path.Combine(folder,"SLDWORKS.exe")))throw new InvalidOperationException("SolidWorks 2023 kurulumu bulunamadı.");
    }
   }
   KapaliMi();
  }
  public static string KayitliDll()
  {
   using(var root=Hklm())using(var key=root.OpenSubKey(Clsid))
   {
    var url=key?.GetValue("CodeBase") as string;
    if(url==null&&key!=null)foreach(var sub in key.GetSubKeyNames())using(var k=key.OpenSubKey(sub)){url=k.GetValue("CodeBase") as string;if(url!=null)break;}
    if(string.IsNullOrEmpty(url))return null;
    if(!Uri.TryCreate(url,UriKind.Absolute,out var u)||!u.IsFile)throw new IOException("Mevcut COM kurulum yolu okunamadı.");
    return Path.GetFullPath(u.LocalPath);
   }
  }
  public static bool HazirMi()
  {
   var path=Path.Combine(Hedef,"Pronom.SolidWorks.dll");
   var self=Path.Combine(Hedef,"Pronom.Kurulum.exe");
   if(!File.Exists(self)||!string.Equals(KayitliDll(),path,StringComparison.OrdinalIgnoreCase))return false;
   if(KurulumCekirdegi.Hash(self)!=KurulumCekirdegi.Hash(Assembly.GetExecutingAssembly().Location))return false;
   return PaketSaglamMi();
  }
  static bool PaketSaglamMi()=>KurulumCekirdegi.Liste().All(f=>{var p=KurulumCekirdegi.AltYol(Hedef,f.Ad);return File.Exists(p)&&KurulumCekirdegi.Hash(p)==f.SHA256;});
  public static KurulumBilgisi KurulumBilgisiOku()
  {
   var kayit=KayitliDll();string hash=null;
   if(kayit!=null)
   {
    if(!File.Exists(kayit))throw new IOException("Kayıtlı Pronom DLL dosyası bulunamıyor: "+kayit);
    hash=KurulumCekirdegi.Hash(kayit);
   }
   var hedef=Path.Combine(Hedef,"Pronom.SolidWorks.dll");
   if(File.Exists(hedef)&&!KurulumCekirdegi.BilinenDllMi(KurulumCekirdegi.Hash(hedef)))throw new IOException("Hedef klasörde tanınmayan DLL bulundu.");
   return KurulumCekirdegi.KurulumDurumu(hash,File.Exists(hedef),HazirMi(),PaketSaglamMi());
  }
  public static async Task Saglik(Uri uri)
  {
   try
   {
    using(var h=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false}){Timeout=TimeSpan.FromSeconds(8)})
    using(var r=await h.GetAsync(new Uri(uri,"api/health")).ConfigureAwait(false))
    {
     if(!r.IsSuccessStatusCode)throw new IOException("Merkez servisi hazır değil (HTTP "+(int)r.StatusCode+"). Dell'deki API açık olmalı.");
     var s=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(await r.Content.ReadAsStringAsync().ConfigureAwait(false));
     if(s==null||!s.TryGetValue("durum",out var d)||!s.TryGetValue("veritabani",out var v)||(string)d!="Hazır"||(string)v!="Bağlı")throw new IOException("Merkez servisinin sağlık yanıtı doğrulanamadı.");
    }
   }
   catch(TaskCanceledException e){throw new IOException("Dell merkez API'si 8 saniye içinde yanıt vermedi. Adresi ve Dell'deki API penceresini kontrol edin.",e);}
   catch(HttpRequestException e){throw new IOException("Merkez bağlantısı kurulamadı. Dell'deki API açık ve iki bilgisayar aynı ağda olmalı.",e);}
  }
  static void RegAsm(string dll,bool kaldir=false)
  {
   var reg=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe");
   using(var p=new Process{StartInfo=new ProcessStartInfo(reg,"\""+dll+"\" "+(kaldir?"/unregister ":"")+"/codebase /nologo"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true}})
   {
    p.Start();var a=p.StandardOutput.ReadToEndAsync();var b=p.StandardError.ReadToEndAsync();
    if(!p.WaitForExit(60000)){try{p.Kill();p.WaitForExit(1000);}catch{}throw new IOException("Eklenti kaydı zamanında tamamlanmadı. SolidWorks açmadan kurulumu tekrar kontrol edin.");}
    Task.WaitAll(a,b);
    if(p.ExitCode!=0)throw new IOException("COM kaydı tamamlanamadı: "+a.Result+" "+b.Result);
   }
  }
  public static void MakineKur()
  {
   if(!Yonetici)throw new UnauthorizedAccessException("Makine kurulumu için Windows yönetici izni gerekli.");
   OnKontrol();
   var eski=KayitliDll();
   if(eski!=null)
   {
    if(!File.Exists(eski)||!KurulumCekirdegi.BilinenDllMi(KurulumCekirdegi.Hash(eski)))throw new IOException("Mevcut Pronom kaydı tanınmıyor veya dosyası eksik; otomatik değiştirilmedi.");
   }
   KurulumCekirdegi.BaglantisizDizin(Hedef);Directory.CreateDirectory(Hedef);
   var stage=Path.Combine(Hedef,"KurulumDosyalari",Guid.NewGuid().ToString("N"));
   KurulumCekirdegi.PaketiAc(stage);
   Action<string> geri=dll=>
   {
    // Kurulumdan onceki bilinen kayit ayni dosyalarla geri kurulur.
    if(eski!=null)
    {
     using(var f=new FileStream(eski,FileMode.Open,FileAccess.Read,FileShare.Read))
     using(var sha=System.Security.Cryptography.SHA256.Create())
     {
      var hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");
      if(!KurulumCekirdegi.BilinenDllMi(hash))throw new IOException("Önceki DLL değişmiş; COM geri yükleme yapılmadı.");
      RegAsm(eski);
     }
    }
    else RegAsm(Path.Combine(stage,"Pronom.SolidWorks.dll"),true);
   };
   KurulumCekirdegi.MakineDosyalari(stage,Hedef,dll=>
   {
    RegAsm(dll);
    if(!string.Equals(KayitliDll(),dll,StringComparison.OrdinalIgnoreCase))throw new IOException("COM dosya yolu doğrulanamadı.");
   },geri,KapaliMi);
   var self=Assembly.GetExecutingAssembly().Location;
   var installed=Path.Combine(Hedef,"Pronom.Kurulum.exe");
   if(!string.Equals(self,installed,StringComparison.OrdinalIgnoreCase))
   {
    if(File.Exists(installed))
    {
     var backup=Path.Combine(Hedef,"Yedekler",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
     File.Copy(installed,Path.Combine(backup,"Pronom.Kurulum.exe"),false);
    }
    File.Copy(self,installed,true);
   }
   using(var root=Hklm())using(var key=root.CreateSubKey(Uninstall))
   {
    key.SetValue("DisplayName","Pronom SolidWorks Co-Pilot");
    key.SetValue("DisplayVersion",Assembly.GetExecutingAssembly().GetName().Version.ToString());
    key.SetValue("Publisher","Pronom");
    key.SetValue("InstallLocation",Hedef);
    key.SetValue("DisplayIcon",installed);
    key.SetValue("UninstallString","\""+installed+"\" --kaldir");
    key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);
   }
  }
  public static object BaslangicOku(){using(var k=Registry.CurrentUser.OpenSubKey(Startup))return k?.GetValue("");}
  public static void BaslangicYaz(object value)
  {
   using(var k=Registry.CurrentUser.CreateSubKey(Startup))
    if(value==null)k.DeleteValue("",false);else k.SetValue("",Convert.ToInt32(value),RegistryValueKind.DWord);
  }
  public static string SonucYolu(Guid id)
  {
   var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Pronom","Kurulum");
   KurulumCekirdegi.BaglantisizDizin(folder);
   return Path.Combine(folder,id.ToString("N")+".txt");
  }
  public static void Kisayollar(Uri adres)
  {
   var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Pronom");
   KurulumCekirdegi.BaglantisizDizin(folder);Directory.CreateDirectory(folder);
   var type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);
   try
   {
    dynamic a=shell.CreateShortcut(Path.Combine(folder,"Pronom Bağlantı Ayarları.lnk"));
    try{a.TargetPath=Path.Combine(Hedef,"Pronom.Kurulum.exe");a.WorkingDirectory=Hedef;a.Save();}finally{Marshal.FinalReleaseComObject(a);}
    dynamic b=shell.CreateShortcut(Path.Combine(folder,"Bekleyen Kayıtlar.lnk"));
    try{b.TargetPath=Path.Combine(Hedef,"Pronom.Kurulum.exe");b.Arguments="--bekleyen";b.WorkingDirectory=Hedef;b.Save();}finally{Marshal.FinalReleaseComObject(b);}
    File.WriteAllText(Path.Combine(folder,"Pronom Web.url"),"[InternetShortcut]\r\nURL="+adres.AbsoluteUri+"\r\n");
   }
   finally{Marshal.FinalReleaseComObject(shell);}
  }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool MoveFileEx(string eski,string yeni,int flags);
  public static void Kaldir()
  {
   if(!Yonetici)throw new UnauthorizedAccessException("Windows yönetici izni gerekli.");
   KapaliMi();KurulumCekirdegi.BaglantisizDizin(Hedef);
   var dll=Path.Combine(Hedef,"Pronom.SolidWorks.dll");
   if(string.Equals(KayitliDll(),dll,StringComparison.OrdinalIgnoreCase)&&File.Exists(dll))RegAsm(dll,true);
   foreach(var f in KurulumCekirdegi.Liste())
   {
    var p=KurulumCekirdegi.AltYol(Hedef,f.Ad);
    if(File.Exists(p)&&KurulumCekirdegi.Hash(p)==f.SHA256)File.Delete(p);
   }
   using(var root=Hklm())root.DeleteSubKeyTree(Uninstall,false);
   var self=Path.Combine(Hedef,"Pronom.Kurulum.exe");
   if(File.Exists(self))MoveFileEx(self,null,4);
   // Yedekler, kurulum tanisi, kullanici gunlukleri ve CAD dosyalari korunur.
  }
 }
}
