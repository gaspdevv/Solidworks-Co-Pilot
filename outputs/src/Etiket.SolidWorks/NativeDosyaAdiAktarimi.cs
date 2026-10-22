using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace Etiket.SolidWorks
{
 // WM_SETTEXT görünümü değiştirip Common Item Dialog'un iç FileName değerini eski bırakabilir.
 public static class NativeDosyaAdiAktarimi
 {
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
  [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
  [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr h,int id);
  [DllImport("user32.dll")]static extern int GetDlgCtrlID(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  static string Sinif(IntPtr h){var s=new StringBuilder(128);GetClassName(h,s,s.Capacity);return s.ToString();}
  static string Metin(IntPtr h){var s=new StringBuilder(2048);var n=GetWindowText(h,s,s.Capacity);if(n>=s.Capacity-1)throw new IOException("Native dosya adı çok uzun.");return s.ToString();}
  public static void Esitle(IntPtr dialog,IntPtr edit,string beklenen)
  {
   var thread=GetCurrentThreadId();var pid=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
   var combo=GetParent(edit);var sink=GetParent(combo);var dugme=GetDlgItem(dialog,1);
   if(GetWindowThreadProcessId(dialog,out var dp)!=thread||dp!=pid||GetWindowThreadProcessId(edit,out var ep)!=thread||ep!=pid
    ||!IsChild(dialog,edit)||Sinif(dialog)!="#32770"||Sinif(edit)!="Edit"||GetDlgCtrlID(edit)!=1001
    ||Sinif(combo)!="ComboBox"||GetDlgCtrlID(combo)!=0||Sinif(sink)!="FloatNotifySink"||Sinif(dugme)!="Button"
    ||!NativeKayitPenceresiKurali.Eslesiyor(Metin(dialog),Metin(dugme)))throw new IOException("Native ad aktarımının pencere/alan kimliği doğrulanamadı.");
   if(string.IsNullOrWhiteSpace(beklenen)||Metin(edit)!=beklenen)throw new IOException("Native ad aktarımından önce son ad değişti.");
   // Yalnızca kendi UI thread'imizdeki dosya adı ComboBox'ı. CAD/SQL/COM ve küresel tuş yok.
   SendMessage(sink,0x0111,new IntPtr(5<<16),combo); // CBN_EDITCHANGE=5, kontrol kimliği=0.
   if(Metin(edit)!=beklenen)throw new IOException("Native ad aktarımı sırasında ad değişti; kayıt durduruldu.");
  }
 }
}
