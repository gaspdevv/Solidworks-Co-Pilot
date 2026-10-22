using System;
using System.Runtime.InteropServices;
using System.Text;
namespace Etiket.SolidWorks
{
 internal static class NativeHedefKlasorOkuyucu
 {
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint process);
  [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,StringBuilder l);
  public static string Oku(IntPtr dialog)
  {
   // Özel mesajın buffer'ı yalnızca aynı thread'deki doğrulanmış native pencereye gider.
   if(dialog==IntPtr.Zero||GetWindowThreadProcessId(dialog,out _)!=GetCurrentThreadId())return null;
   var metin=new StringBuilder(32768);
   var boy=SendMessage(dialog,0x0466,new IntPtr(metin.Capacity),metin).ToInt64();
   if(boy<=0||boy>metin.Capacity||boy!=metin.Length+1)return null;
   var yol=metin.ToString();
   // Kaynak klasörü, çalışma dizini veya kısmi breadcrumb metni yedek yol olarak kullanılmaz.
   // Ağ klasörüne Directory.Exists çağrısı UI timer'ında yapılmaz.
   if(yol.Length>=3&&char.IsLetter(yol[0])&&yol[1]==':'&&yol[2]=='\\')return yol;
   if(yol.StartsWith(@"\\",StringComparison.Ordinal)&&yol.Substring(2).Split('\\').Length>=2
    &&!string.IsNullOrWhiteSpace(yol.Substring(2).Split('\\')[0])&&!string.IsNullOrWhiteSpace(yol.Substring(2).Split('\\')[1]))return yol;
   return null;
  }
 }
}
