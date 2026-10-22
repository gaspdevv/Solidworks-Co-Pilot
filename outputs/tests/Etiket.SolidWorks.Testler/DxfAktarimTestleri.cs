using System;
using Etiket.SolidWorks;

partial class Program
{
 static void DxfAktarimTestleri()
 {
  var kaynak=@"C:\Pronom-Test\P00080.SLDPRT";
  Kontrol(!DosyaAdiKurali.AyniYol(kaynak,"P00080.DXF"),"Gercek logdaki eski tam yol karsilastirmasi DXF'yi tanimiyordu.");
  Kontrol(DxfAktarimKurali.Eslesiyor("P",kaynak,"P00080.DXF"),"Gercek Prompt ve FileSaveAsNotify2 adiyla DXF ayri native aktarimdir.");
  Kontrol(DxfAktarimKurali.Eslesiyor("P",kaynak,@"D:\DXF\P00080.dxf"),"Farkli hedef klasorde ayni kaynak adi desteklenir.");
  Kontrol(DxfAktarimKurali.Eslesiyor("P",kaynak,"p00080.DxF"),"Windows buyuk/kucuk harf farki.");
  Kontrol(DxfAktarimKurali.Eslesiyor("P",@"\\Merkez\CAD\P00080.SLDPRT",@"\\Merkez\DXF\P00080.DXF"),"UNC kaynak/hedef tam yollari.");
  Kontrol(DxfAktarimKurali.Eslesiyor("P",@"C:\İmalat\Özel Parça.SLDPRT","Özel Parça.dxf"),"Ozel adli kaynagin adi korunur.");
  foreach(var ad in new[]{"P00081.DXF","P00080.DWG","P00080.SLDPRT","P00080.SLDASM","P00080.STEP","P00080.dxf.exe","P00080",@"alt\P00080.DXF",@"..\P00080.DXF",@"C:P00080.DXF",@"\P00080.DXF","P00080*.DXF","P00080.DXF:veri",null,""})
   Kontrol(!DxfAktarimKurali.Eslesiyor("P",kaynak,ad),"Yanlis ad/format/belirsiz yol DXF iznini kullanamaz: "+ad);
  foreach(var ad in new[]{null,"","P00080.SLDPRT",@"C:P00080.SLDPRT",@"\P00080.SLDPRT",@"C:\CAD\P00080.DXF",@"C:\CAD\P00080.SLDASM"})
   Kontrol(!DxfAktarimKurali.Eslesiyor("P",ad,"P00080.DXF"),"Kaydedilmemis veya parca olmayan kaynak yeni CAD korumasini atlayamaz.");
  Kontrol(!DxfAktarimKurali.Eslesiyor("M",kaynak,"P00080.DXF"),"Montaj ve ic ice belge korumasi genislemez.");
  Kontrol(!DxfAktarimKurali.Eslesiyor("D",kaynak,"P00080.DXF"),"Bu ekleme teknik resim numaralandirmasi degildir.");
  Kontrol(!DosyaAdiKurali.Gecerli("P00080.DXF","P"),"DXF merkezi CAD rezervasyonu olarak kabul edilmemeli.");
  Hata(()=>BagimsizDosyaAdiKurali.Tamamla("P00080.DXF","P"),"Bagimsiz Yap ve standart CAD uzanti korumasi degismemeli.");
 }
}
