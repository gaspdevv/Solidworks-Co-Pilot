using System;
using System.IO;
using System.Text;
namespace Etiket.SolidWorks
{
 internal static class KayitOlayTanisi
 {
  static readonly TaniYazimKuyrugu kuyruk=new TaniYazimKuyrugu(s=>File.AppendAllText(Path.Combine(EklentiAyarlari.KullaniciKlasoru,"KayitOlayTanisi.log"),s,new UTF8Encoding(false)));
  public static bool Acik
  {
   get{try{return File.Exists(Path.Combine(EklentiAyarlari.KullaniciKlasoru,"KayitOlayTanisi.acik"));}catch(Exception){return false;}}
  }
  public static void Yaz(string olay,string ayrinti)
  {
   // İsteğe bağlı, yalnızca yerel metadata. Tanı hatası CAD işlemini etkilemez.
   try
   {
    if(!Acik)return;
    var satir=DateTimeOffset.UtcNow.ToString("O")+" | "+olay+" | "+(ayrinti??"").Replace('\r',' ').Replace('\n',' ')+Environment.NewLine;
    kuyruk.Ekle(satir);
   }
   catch(Exception){}
  }
 }
}
