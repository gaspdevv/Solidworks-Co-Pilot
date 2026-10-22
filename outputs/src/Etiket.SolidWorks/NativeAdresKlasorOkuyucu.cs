using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Accessibility;
namespace Etiket.SolidWorks
{
 internal static class NativeAdresKlasorOkuyucu
 {
  delegate bool Gezici(IntPtr h,IntPtr veri);
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
  [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,Gezici g,IntPtr v);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder s,int boy);
  [DllImport("oleacc.dll")]static extern int AccessibleObjectFromWindow(IntPtr h,uint id,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IAccessible nesne);
  public static string[] ErisilebilirMetinler(IntPtr h)
  {
   if(h==IntPtr.Zero||GetWindowThreadProcessId(h,out _)!=GetCurrentThreadId())return new[]{"",""};
   var iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
   if(AccessibleObjectFromWindow(h,0xFFFFFFFC,ref iid,out var a)!=0||a==null)return new[]{"",""};
   string ad="",deger="";
   try{ad=a.get_accName(0)??"";}catch(COMException){}
   try{deger=a.get_accValue(0)??"";}catch(COMException){}
   return new[]{ad,deger};
  }
  public static string AdresMetnindenYol(string metin)
  {
   var s=(metin??"").Trim();
   if(s.StartsWith("Adres:",StringComparison.OrdinalIgnoreCase))s=s.Substring(6).Trim();
   else if(s.StartsWith("Address:",StringComparison.OrdinalIgnoreCase))s=s.Substring(8).Trim();
   if(s.IndexOfAny(new[]{'\r','\n','\0','"','<','>','|','*','?'})>=0||s.StartsWith(@"\\.\",StringComparison.Ordinal))return null;
   if(s.Length>=3&&((s[0]>='A'&&s[0]<='Z')||(s[0]>='a'&&s[0]<='z'))&&s[1]==':'&&s[2]=='\\'&&s.Substring(2).IndexOf(':')<0)return s;
   if(s.StartsWith(@"\\",StringComparison.Ordinal)&&s.IndexOf(':')<0)
   {
    var kisim=s.Substring(2).Split('\\');
    if(kisim.Length>=2&&!string.IsNullOrWhiteSpace(kisim[0])&&!string.IsNullOrWhiteSpace(kisim[1]))return s;
   }
   return null;
  }
  public static string Oku(IntPtr dialog,Action<string> tani)
  {
   if(dialog==IntPtr.Zero||GetWindowThreadProcessId(dialog,out _)!=GetCurrentThreadId())return null;
   var kokler=new List<IntPtr>();var adet=0;
   EnumChildWindows(dialog,(h,v)=>{if(++adet>192)return false;if(Sinif(h)=="Address Band Root"&&IsWindowVisible(h))kokler.Add(h);return true;},IntPtr.Zero);
   if(kokler.Count!=1||adet>192){tani("Adres kökü bulunamadı veya birden fazla.");return null;}
   var alanlar=new List<IntPtr>{kokler[0]};adet=0;
   EnumChildWindows(kokler[0],(h,v)=>{if(++adet>32)return false;if(Sinif(h)=="ToolbarWindow32"&&IsWindowVisible(h))alanlar.Add(h);return true;},IntPtr.Zero);
   if(adet>32)return null;
   var yollar=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var h in alanlar)
   {
    var m=ErisilebilirMetinler(h);
    tani("sinif="+Sinif(h)+" ad="+m[0]+" deger="+m[1]);
    foreach(var s in m){var yol=AdresMetnindenYol(s);if(yol!=null)yollar.Add(yol.TrimEnd('\\'));}
   }
   // Farklı adres değerleri varsa eski/güncel yolu tahmin ederek seçmeyin.
   if(yollar.Count!=1)return null;
   foreach(var yol in yollar)return yol.Length==2&&yol[1]==':'?yol+"\\":yol;
   return null;
  }
  static string Sinif(IntPtr h){var s=new StringBuilder(128);GetClassName(h,s,s.Capacity);return s.ToString();}
 }
}
