using System;
using System.IO;

namespace Etiket.SolidWorks
{
 // Sag tik DXF/DWG'ye ver: PromptForFilename ve FileSaveAsNotify2,
 // kaynak yolundan farkli olarak yalniz P00080.DXF adini verebilir.
 // Bu eslesme yeni native CAD kaydi veya merkezi rezervasyon degildir.
 public static class DxfAktarimKurali
 {
  public static bool DxfAdiMi(string ad)
  {
   try{return !string.IsNullOrWhiteSpace(ad)&&string.Equals(Path.GetExtension(ad),".DXF",StringComparison.OrdinalIgnoreCase);}
   catch(ArgumentException){return false;}
  }
  public static bool Eslesiyor(string tur,string kaynak,string hedef)
  {
   if(tur!="P"||string.IsNullOrWhiteSpace(kaynak)||string.IsNullOrWhiteSpace(hedef))return false;
   try
   {
    if(!TamYol(kaynak)||!string.Equals(Path.GetExtension(kaynak),".SLDPRT",StringComparison.OrdinalIgnoreCase)
     ||!DxfAdiMi(hedef))return false;
    // Tam hedef yol veya SolidWorks'un verdigi yalniz dosya adi kabul edilir.
    if(!TamYol(hedef)&&(hedef.IndexOfAny(new[]{'\\','/',':'})>=0))return false;
    if(hedef.IndexOfAny(Path.GetInvalidPathChars())>=0||Path.GetFileName(hedef).IndexOfAny(Path.GetInvalidFileNameChars())>=0)return false;
    return string.Equals(Path.GetFileNameWithoutExtension(kaynak),Path.GetFileNameWithoutExtension(hedef),StringComparison.OrdinalIgnoreCase);
   }
   catch(ArgumentException){return false;}
   catch(NotSupportedException){return false;}
   catch(PathTooLongException){return false;}
  }
  static bool TamYol(string path)
  {
   var root=Path.GetPathRoot(path)??"";
   return root.Length==3&&char.IsLetter(root[0])&&root[1]==':'&&root[2]=='\\'
    ||path.StartsWith(@"\\",StringComparison.Ordinal)&&root.Trim('\\').Split('\\').Length>=2;
  }
 }
}
