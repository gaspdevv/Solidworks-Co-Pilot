using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Etiket.SolidWorks;

static class NativeKaydetGirdiTestleri
{
 delegate bool Gezici(IntPtr h,IntPtr v);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint t,Gezici g,IntPtr v);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,Gezici g,IntPtr v);
 [DllImport("user32.dll")]static extern int GetDlgCtrlID(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr h,int id);
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
  foreach(var yontem in new[]{"BM_CLICK","fare","Enter","Space","ozel-ad","standart-devam","tekrar","cift-tik"})
  {
   var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
   var alan=Path.Combine(kok.FullName,"work","native-girdi25",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
   var kaynak=Path.Combine(alan,"P00097.SLDPRT");File.WriteAllText(kaynak,"CAD degil; uzerine yazma kontrolu.");
   var beklenen=yontem=="ozel-ad"?"Ozel Mil":"P00098";
   using(var d=new SaveFileDialog{Title="Farklı Kaydet",InitialDirectory=alan,FileName="P00097.SLDPRT",Filter="SOLIDWORKS|*.SLDPRT",DefaultExt="SLDPRT",OverwritePrompt=true})
   using(var timer=new Timer{Interval=100})
   {
    IntPtr pencere=IntPtr.Zero,edit=IntPtr.Zero;Exception hata=null;int adim=0,istek=0,fileOk=0;bool onay=false;var son=DateTime.UtcNow.AddSeconds(8);
    using(var koruma=new NativeKaydetGirdiKoruyucusu(h=>{
     istek++;if(Metin(edit)!=beklenen)hata=new Exception("Gorunen ad degisti.");
     if(yontem=="tekrar"&&istek==1)return; // Failed validation leaves the same dialog open.
     if(yontem=="standart-devam"){NativeDosyaAdiAktarimi.Esitle(h,edit,beklenen);PostMessage(h,0x0111,new IntPtr(1),IntPtr.Zero);}
     else PostMessage(h,0x0010,IntPtr.Zero,IntPtr.Zero);
    }))
    {
     d.FileOk+=(s,e)=>{fileOk++;e.Cancel=true;if(yontem=="standart-devam")PostMessage(pencere,0x0010,IntPtr.Zero,IntPtr.Zero);};
     timer.Tick+=(s,e)=>
     {
      try
      {
       EnumThreadWindows(GetCurrentThreadId(),(h,v)=>{if(Sinif(h)=="#32770"&&Metin(h).Contains("Onayla")){onay=true;PostMessage(h,0x0010,IntPtr.Zero,IntPtr.Zero);}if(Sinif(h)=="#32770"&&Metin(h)=="Farklı Kaydet")pencere=h;return true;},IntPtr.Zero);
       if(DateTime.UtcNow>son||onay)throw new Exception("Native Save dogrulamayi atladi veya girdi zaman asimi.");
       if(pencere==IntPtr.Zero)return;
       if(yontem=="tekrar"&&adim==1&&istek==1){adim=2;SendMessage(GetDlgItem(pencere,1),0x00F5,IntPtr.Zero,IntPtr.Zero);return;}
       if(adim!=0)return;
       EnumChildWindows(pencere,(h,v)=>{if(Sinif(h)=="Edit"&&GetDlgCtrlID(h)==1001&&Sinif(GetParent(h))=="ComboBox")edit=h;return true;},IntPtr.Zero);
       if(edit==IntPtr.Zero)return;
       var button=GetDlgItem(pencere,1);koruma.Bagla(pencere,button,edit);SendMessage(edit,0x000C,IntPtr.Zero,beklenen);adim=1;
       if(yontem=="Enter")PostMessage(edit,0x0100,new IntPtr(13),IntPtr.Zero);
       else if(yontem=="fare")PostMessage(button,0x0202,IntPtr.Zero,IntPtr.Zero);
       else if(yontem=="Space")SendMessage(button,0x0101,new IntPtr(32),IntPtr.Zero);
       else SendMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero);
       if(yontem=="cift-tik")SendMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero);
      }
      catch(Exception ex){hata=ex;timer.Stop();if(pencere!=IntPtr.Zero)PostMessage(pencere,0x0010,IntPtr.Zero,IntPtr.Zero);}
     };
     timer.Start();var sonuc=d.ShowDialog();timer.Stop();if(hata!=null)throw hata;
     var beklenenFileOk=yontem=="standart-devam"?1:0;
     var beklenenIstek=yontem=="tekrar"?2:1;
     if(sonuc!=DialogResult.Cancel||istek!=beklenenIstek||fileOk!=beklenenFileOk||onay)throw new Exception("Koruma native kaydi/onayi engellemedi: "+yontem);kontrol++;
     if(File.ReadAllText(kaynak)!="CAD degil; uzerine yazma kontrolu."||Directory.GetFiles(alan).Length!=1)throw new Exception("Test kaynagi degisti.");kontrol++;
     Console.WriteLine(yontem+": dogrulama istegi="+istek+", FileOk="+fileOk+", uzerine yazma onayi ve dosya yazimi yok.");
    }
   }
  }
  Console.WriteLine("GECTI: "+kontrol+" gercek Windows kayit penceresi girdi kontrolu.");
 }
}
