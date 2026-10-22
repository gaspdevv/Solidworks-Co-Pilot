using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
namespace Etiket.SolidWorks
{
 internal sealed class NativePencereTanisi:IDisposable
 {
  readonly Timer zamanlayici=new Timer{Interval=250};
  readonly HashSet<string> okunan=new HashSet<string>();
  uint isParcacigi;DateTime son;
  delegate bool PencereGezici(IntPtr pencere,IntPtr veri);
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint threadId,PencereGezici gezici,IntPtr veri);
  [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr parent,PencereGezici gezici,IntPtr veri);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr pencere,StringBuilder metin,int uzunluk);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr pencere,StringBuilder metin,int uzunluk);
  [DllImport("user32.dll")]static extern int GetDlgCtrlID(IntPtr pencere);
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr pencere);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr pencere);
  public NativePencereTanisi(){zamanlayici.Tick+=Oku;}
  public void Baslat()
  {
   if(!KayitOlayTanisi.Acik)return;
   isParcacigi=GetCurrentThreadId();son=DateTime.UtcNow.AddMinutes(2);
   if(!zamanlayici.Enabled)okunan.Clear();zamanlayici.Start();
  }
  void Oku(object sender,EventArgs e)
  {
   try
   {
    if(!KayitOlayTanisi.Acik||DateTime.UtcNow>son){Durdur();return;}
    // Yalnızca eklentiyi çağıran SolidWorks UI thread'indeki görünür standart dialoglar.
    EnumThreadWindows(isParcacigi,(p,v)=>
    {
     if(!IsWindowVisible(p)||Sinif(p)!="#32770")return true;
     var baslik=Metin(p);var kimlik=p.ToInt64()+":"+baslik;
     if(!okunan.Add(kimlik))return true;
     KayitOlayTanisi.Yaz("NativeDialog","sinif=#32770 baslik="+baslik);
     var adet=0;
     EnumChildWindows(p,(c,d)=>
     {
      if(++adet>120)return false;
      var sinif=Sinif(c);var metin="";
      if(sinif=="Edit"||sinif=="ComboBox"||sinif=="ComboBoxEx32"||sinif=="Static"||sinif=="Button")metin=Metin(c);
      KayitOlayTanisi.Yaz("NativeKontrol","id="+GetDlgCtrlID(c)+" sinif="+sinif+" ustId="+GetDlgCtrlID(GetParent(c))+" ustSinif="+Sinif(GetParent(c))+" gorunur="+IsWindowVisible(c)+" metin="+metin);
      return true;
     },IntPtr.Zero);
     return true;
    },IntPtr.Zero);
   }
   catch(Exception h){KayitOlayTanisi.Yaz("NativeTaniHatasi",h.Message);Durdur();}
  }
  static string Sinif(IntPtr p){var s=new StringBuilder(128);GetClassName(p,s,s.Capacity);return s.ToString();}
  static string Metin(IntPtr p){var s=new StringBuilder(512);GetWindowText(p,s,s.Capacity);return s.ToString();}
  public void Durdur(){zamanlayici.Stop();okunan.Clear();}
  public void Dispose(){zamanlayici.Stop();zamanlayici.Tick-=Oku;zamanlayici.Dispose();}
 }
}
