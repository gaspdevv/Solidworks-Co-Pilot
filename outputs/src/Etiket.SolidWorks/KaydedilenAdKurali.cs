using System;
using System.IO;
namespace Etiket.SolidWorks
{
 public static class KaydedilenAdKurali
 {
  public static bool MerkeziAdMi(string ayrilanAd,string gercekAd)
  {
   return !string.IsNullOrWhiteSpace(ayrilanAd)&&string.Equals(ayrilanAd,gercekAd,StringComparison.OrdinalIgnoreCase);
  }
 }
 public sealed class NativeAdAlaniDurumu
 {
  public bool KullaniciDegistirdi{get;private set;}
  public void KullaniciDuzenliyor(){KullaniciDegistirdi=true;}
  public string UygulanacakMetin(string gelen,string kaynakAd,string otomatikAd)
  {
   // Shell'in ilk kaynak adı yazımını düzelt; kullanıcı düzenlemeye başladıysa metni koru.
   if(!KullaniciDegistirdi&&string.Equals(gelen,kaynakAd,StringComparison.OrdinalIgnoreCase))return otomatikAd;
   if(!string.Equals(gelen,otomatikAd,StringComparison.OrdinalIgnoreCase))KullaniciDegistirdi=true;
   return gelen;
  }
 }
}
