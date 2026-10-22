using System;
using System.IO;
namespace Etiket.SolidWorks
{
 public sealed class HedefAdresHazirDegilHatasi:IOException
 {public HedefAdresHazirDegilHatasi(string mesaj):base(mesaj){}}
 public static class HedefOkumaTekrari
 {
  // Only an absent address can settle. Ambiguity, changed paths, process/window
  // mismatches and security/process errors must remain immediate failures.
  public static T Oku<T>(Func<T> oku,Action<int> bekle)
  {
   for(var deneme=0;;deneme++)
   {
    try{return oku();}
    catch(HedefAdresHazirDegilHatasi){if(deneme>=2)throw;bekle(150);}
   }
  }
 }
}
