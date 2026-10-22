using System;
namespace Etiket.SolidWorks
{
 public sealed class ErtelenmisBelgeTaramasi
 {
  readonly Action tara;bool bekliyor,calisiyor;
  public ErtelenmisBelgeTaramasi(Action tara){this.tara=tara??throw new ArgumentNullException(nameof(tara));}
  public bool Bekliyor=>bekliyor;
  // Açma/New event'inde COM belge sorgusu veya HTTP çalışmaz.
  public void Iste(){bekliyor=true;}
  public bool Calistir()
  {
   if(!bekliyor||calisiyor)return false;
   bekliyor=false;calisiyor=true;
   try{tara();return true;}finally{calisiyor=false;}
  }
  public void Temizle(){bekliyor=false;}
 }
}
