using System;
using System.IO;
namespace Etiket.SolidWorks
{
 public static class SolidWorksSurumKurali
 {
  // 2023 = major revision 31. SP/hotfix kısmı yıl kontrolünden ayrı tutulur.
  // Bu kontrol hedef yılı doğrular; gerçek CAD davranışının testinin yerini almaz.
  public static bool TestHedefiMi(string revizyon)
   =>Version.TryParse(revizyon,out var surum)&&surum.Major==31;
 }
 internal static class EklentiAyarlari
 {
  public static string KullaniciKlasoru=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pronom","SolidWorksEtiketleme");
  public static Uri ApiAdresi()
  {
   var dosya=Path.Combine(KullaniciKlasoru,"ApiAdresi.txt");
   if(!File.Exists(dosya))throw new IOException("API adresini "+dosya+" dosyasına yazın.");
   var metin=File.ReadAllText(dosya).Trim().TrimEnd('/')+"/";
   if(!Uri.TryCreate(metin,UriKind.Absolute,out var adres)||(adres.Scheme!="http"&&adres.Scheme!="https")||!string.IsNullOrEmpty(adres.UserInfo)||!string.IsNullOrEmpty(adres.Query)||!string.IsNullOrEmpty(adres.Fragment))throw new IOException("API adresi geçerli HTTP/HTTPS adresi olmalıdır.");
   return adres;
  }
 }
}
