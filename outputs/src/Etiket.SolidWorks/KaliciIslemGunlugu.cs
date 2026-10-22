using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
namespace Etiket.SolidWorks
{
 public sealed class KaliciIslemGunlugu
 {
  readonly string klasor;
  public KaliciIslemGunlugu(string klasor){this.klasor=klasor;Directory.CreateDirectory(klasor);}
  string Yol(Guid id)=>Path.Combine(klasor,id.ToString("D")+".json");
  public void Yaz(KaydetmeIslemi islem)
  {
   var yol=Yol(islem.IslemId);var gecici=yol+"."+Guid.NewGuid().ToString("N")+".tmp";
   try
   {
    using(var dosya=new FileStream(gecici,FileMode.CreateNew,FileAccess.Write,FileShare.None))
    {new DataContractJsonSerializer(typeof(KaydetmeIslemi)).WriteObject(dosya,islem);dosya.Flush(true);}
    if(File.Exists(yol))File.Replace(gecici,yol,null);else File.Move(gecici,yol);
   }
   finally{if(File.Exists(gecici))File.Delete(gecici);}
  }
  public KaydetmeIslemi Oku(Guid id)
  {
   using(var dosya=File.OpenRead(Yol(id)))
   {
    var sonuc=(KaydetmeIslemi)new DataContractJsonSerializer(typeof(KaydetmeIslemi)).ReadObject(dosya);
    if(sonuc.IslemId!=id)throw new InvalidDataException("İşlem günlüğü kimliği uyuşmuyor.");
    return sonuc;
   }
  }
  public IEnumerable<KaydetmeIslemi> BekleyenleriOku()
  {
   foreach(var ad in Directory.GetFiles(klasor,"*.json"))
   {
    if(!Guid.TryParse(Path.GetFileNameWithoutExtension(ad),out var id))continue;
    var islem=Oku(id); // Bozuk günlük sessizce atlanmaz.
    if(islem.Asama!="Onaylandi"&&islem.Asama!="Iptal")yield return islem;
   }
  }
 }
}
