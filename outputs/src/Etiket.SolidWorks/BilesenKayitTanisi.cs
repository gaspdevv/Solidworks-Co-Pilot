using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
namespace Etiket.SolidWorks
{
 internal sealed class BilesenKayitTanisi:IDisposable
 {
  PartDoc parca;AssemblyDoc montaj;
  readonly string kaynak;
  readonly Func<bool> denemeAktif;
  public BilesenKayitTanisi(IModelDoc2 model,Func<bool> denemeAktif)
  {
   this.denemeAktif=denemeAktif;
   kaynak=model.GetPathName();
   try
   {
    if(model.GetType()==(int)swDocumentTypes_e.swDocPART)
    {parca=(PartDoc)model;parca.FileSaveAsNotify2+=AdIstendi;parca.FileSaveNotify+=Kaydetme;parca.FileSavePostNotify+=Kaydedildi;parca.FileSavePostCancelNotify+=Iptal;}
    else if(model.GetType()==(int)swDocumentTypes_e.swDocASSEMBLY)
    {montaj=(AssemblyDoc)model;montaj.FileSaveAsNotify2+=AdIstendi;montaj.FileSaveNotify+=Kaydetme;montaj.FileSavePostNotify+=Kaydedildi;montaj.FileSavePostCancelNotify+=Iptal;}
    KayitOlayTanisi.Yaz("BilesenTaniBaglandi",kaynak);
   }
   catch{Dispose();throw;}
  }
  // Normalde pasif gözlem; özel pencere denemesinde Save ayrıca engellenir. Ad/özellik yazmaz.
  int AdIstendi(string ad){KayitOlayTanisi.Yaz("BilesenFileSaveAsNotify2","kaynak="+kaynak+" hedef="+ad);return 0;}
  int Kaydetme(string ad){KayitOlayTanisi.Yaz("BilesenFileSaveNotify","kaynak="+kaynak+" hedef="+ad);return denemeAktif()?1:0;}
  int Kaydedildi(int tur,string ad){KayitOlayTanisi.Yaz("BilesenFileSavePostNotify","kaynak="+kaynak+" tur="+tur+" hedef="+ad);return 0;}
  int Iptal(){KayitOlayTanisi.Yaz("BilesenFileSavePostCancelNotify",kaynak);return 0;}
  public void Dispose()
  {
   if(parca!=null){try{parca.FileSaveAsNotify2-=AdIstendi;parca.FileSaveNotify-=Kaydetme;parca.FileSavePostNotify-=Kaydedildi;parca.FileSavePostCancelNotify-=Iptal;}finally{parca=null;}}
   if(montaj!=null){try{montaj.FileSaveAsNotify2-=AdIstendi;montaj.FileSaveNotify-=Kaydetme;montaj.FileSavePostNotify-=Kaydedildi;montaj.FileSavePostCancelNotify-=Iptal;}finally{montaj=null;}}
  }
 }
}
