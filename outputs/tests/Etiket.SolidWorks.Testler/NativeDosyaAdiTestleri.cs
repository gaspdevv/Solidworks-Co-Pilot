using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Etiket.SolidWorks;

// Gerçek Windows Common Item Dialog: FileName dönüşünü sınar; hiçbir CAD/dosya yazmaz.
static class NativeDosyaAdiTestleri
{
 delegate bool Gezici(IntPtr h,IntPtr v);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint t,Gezici g,IntPtr v);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,Gezici g,IntPtr v);
 [DllImport("user32.dll")]static extern int GetDlgCtrlID(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string l);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 static string Sinif(IntPtr h){var s=new StringBuilder(128);GetClassName(h,s,s.Capacity);return s.ToString();}
 static string Metin(IntPtr h){var s=new StringBuilder(512);GetWindowText(h,s,s.Capacity);return s.ToString();}
 public static void Calistir()
 {
  int kontrol=0;
  foreach(var yontem in new[]{"WM_SETTEXT","otomatik-parca","otomatik-montaj","ozel-ad","ozelden-otomatik"})
  {
   var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
   var alan=Path.Combine(kok.FullName,"work","native-ad",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
   var montaj=yontem=="otomatik-montaj";var uzanti=montaj?"SLDASM":"SLDPRT";
   var ilkAd=montaj?"Montaj1.SLDASM":"Parça1.SLDPRT";
   var beklenen=yontem=="ozel-ad"?"Özel Mil":montaj?"M00056":"P00056";
   using(var diyalog=new SaveFileDialog{AutoUpgradeEnabled=true,Title="Farklı Kaydet",InitialDirectory=alan,FileName=ilkAd,Filter="SOLIDWORKS|*."+uzanti,AddExtension=true,DefaultExt=uzanti})
   using(var saat=new Timer{Interval=250})
   {
    var sure=DateTime.UtcNow.AddSeconds(12);IntPtr pencere=IntPtr.Zero,edit=IntPtr.Zero;int adim=0;Exception hata=null;
    saat.Tick+=(s,e)=>
    {
     try
     {
      if(pencere==IntPtr.Zero)EnumThreadWindows(GetCurrentThreadId(),(h,v)=>{if(Sinif(h)=="#32770"&&Metin(h)=="Farklı Kaydet")pencere=h;return true;},IntPtr.Zero);
      if(DateTime.UtcNow>sure)throw new TimeoutException("Native ad testi zaman aşımı.");
      if(pencere==IntPtr.Zero)return;
      if(adim==0)
      {
       EnumChildWindows(pencere,(h,v)=>{if(Sinif(h)=="Edit"&&GetDlgCtrlID(h)==1001&&Sinif(GetParent(h))=="ComboBox")edit=h;return true;},IntPtr.Zero);
       if(edit==IntPtr.Zero)return; // Native diyalog denetimleri ilk etkinleşme anında henüz hazır olmayabilir.
       if(yontem=="ozelden-otomatik")SendMessage(edit,0x000C,IntPtr.Zero,"Geçici Özel Ad");
       SendMessage(edit,0x000C,IntPtr.Zero,beklenen);
       if(yontem!="WM_SETTEXT")
       {
        var reddedildi=false;try{NativeDosyaAdiAktarimi.Esitle(pencere,edit,"Başka Ad");}catch(IOException){reddedildi=true;}
        if(!reddedildi)throw new Exception("Değişen son ad reddedilmedi.");kontrol++;
        reddedildi=false;try{NativeDosyaAdiAktarimi.Esitle(pencere,GetParent(edit),beklenen);}catch(IOException){reddedildi=true;}
        if(!reddedildi)throw new Exception("Edit olmayan denetim reddedilmedi.");kontrol++;
        NativeDosyaAdiAktarimi.Esitle(pencere,edit,beklenen);
       }
       Console.WriteLine(yontem+" görünüm="+Metin(edit));adim=1;
      }
      else if(adim==1){adim=2;PostMessage(pencere,0x0111,new IntPtr(1),IntPtr.Zero);}
     }
     catch(Exception ex){hata=ex;saat.Stop();if(pencere!=IntPtr.Zero)PostMessage(pencere,0x0010,IntPtr.Zero,IntPtr.Zero);}
    };
    saat.Start();var sonuc=diyalog.ShowDialog();saat.Stop();
    if(hata!=null)throw hata;
    Console.WriteLine(yontem+" sonuç="+sonuc+" gerçekAd="+diyalog.FileName);
    if(sonuc!=DialogResult.OK)throw new Exception("Native pencere testi tamamlanmadı.");
    var gercekBeklenen=yontem=="WM_SETTEXT"?ilkAd:beklenen+"."+uzanti;
    if(!string.Equals(Path.GetFileName(diyalog.FileName),gercekBeklenen,StringComparison.OrdinalIgnoreCase))throw new Exception(yontem+" iç dosya adı beklenen değil: "+gercekBeklenen);kontrol++;
    if(Directory.GetFiles(alan).Length!=0)throw new Exception("Pencere testi dosya yazmamalı.");kontrol++;
   }
  }
  var rapor="GEÇTİ: "+kontrol+" gerçek Windows kayıt penceresi kontrolü. Eski hata yeniden üretildi; üretim ad aktarımı P/M/özel/adı geri alma için doğrulandı; CAD/dosya yazılmadı.";
  Console.WriteLine(rapor);
  var sonucYolu=Environment.GetEnvironmentVariable("ETIKET_NATIVE_AD_TEST_RAPORU");if(!string.IsNullOrEmpty(sonucYolu))File.WriteAllText(sonucYolu,rapor+Environment.NewLine+DateTimeOffset.UtcNow.ToString("O"));
 }
}
