using System;
using System.IO;
namespace Etiket.SolidWorks
{
 public static class BagimsizDosyaAdiKurali
 {
  public static string Tamamla(string ad,string tur)
  {
   if(tur!="P"&&tur!="M")throw new ArgumentException("Belge türü geçersiz.");
   if(string.IsNullOrWhiteSpace(ad)||ad!=ad.Trim()||ad.EndsWith(".",StringComparison.Ordinal)
    ||ad!=Path.GetFileName(ad)||ad.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
    throw new IOException("Yalnızca geçerli bir dosya adı yazın; klasörü konum alanından seçin.");
   var uzanti=tur=="P"?".SLDPRT":".SLDASM";
   if(Path.GetExtension(ad)=="")ad+=uzanti;
   if(!string.Equals(Path.GetExtension(ad),uzanti,StringComparison.OrdinalIgnoreCase))throw new IOException("Bu işlemde yalnızca "+uzanti+" biçiminde kayıt yapılabilir.");
   var kok=Path.GetFileNameWithoutExtension(ad).Split('.')[0].TrimEnd(' ');
   if(string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(ad)))throw new IOException("Dosyanın uzantı dışında bir adı olmalı.");
   if(System.Text.RegularExpressions.Regex.IsMatch(kok,@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
    throw new IOException("Windows tarafından ayrılmış dosya adı kullanılamaz.");
   if(ad.Length>240)throw new IOException("Dosya adı çok uzun.");
   return ad;
  }
 }
}
