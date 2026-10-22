using System;
using System.Collections.Generic;
namespace Etiket.SolidWorks
{
 // FileOpenPost yalnızca yol kaydeder. Sonraki Kaydet komutu, etkin belgenin olaylarını
 // bağlamak için ikinci fırsattır; GetDocuments ve toparlanma HTTP'si gerekmez.
 public sealed class KayitOncesiBelgeBaglama
 {
  readonly HashSet<string> yollar=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool calisiyor;
  public int BekleyenSayisi=>yollar.Count;
  public void BelgeAcildi(string yol){if(!string.IsNullOrWhiteSpace(yol))yollar.Add(yol);}
  public void Baglandi(string yol){if(yol!=null)yollar.Remove(yol);}
  public bool Dene(int komut,bool koruma,Func<string> etkinYol,Action bagla)
  {
   if(komut!=2||koruma||calisiyor||yollar.Count==0)return false;
   calisiyor=true;
   try
   {
    var yol=etkinYol();if(yol==null||!yollar.Contains(yol))return false;
    bagla();yollar.Remove(yol);return true;
   }
   finally{calisiyor=false;}
  }
  public void Temizle(){yollar.Clear();}
 }
}
