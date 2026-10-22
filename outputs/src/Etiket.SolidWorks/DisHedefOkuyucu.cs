using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
namespace Etiket.SolidWorks
{
 internal static class DisHedefOkuyucu
 {
  public static Task<HedefPencereYaniti[]> OkuCift(string exe,int pid,long pencere,Guid islem,Guid sorgu,Guid son)
  {
   return Task.Run(()=>
   {
    if(!File.Exists(exe))throw new FileNotFoundException("Araclar/Pronom.PencereTanisi.exe bulunamadı.",exe);
    using(var p=new Process{StartInfo=new ProcessStartInfo{FileName=exe,Arguments="--hedef-cift "+pid+" "+pencere+" "+islem.ToString("D")+" "+sorgu.ToString("D")+" "+son.ToString("D"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}})
    {
     var sure=Stopwatch.StartNew();
     KayitOlayTanisi.Yaz("DisHedefBaslatiliyor","islem="+islem+" sorgu="+sorgu);
     p.Start();
     KayitOlayTanisi.Yaz("DisHedefBaslatildi","islem="+islem+" sorgu="+sorgu+" ms="+sure.ElapsedMilliseconds);
     var cikti=p.StandardOutput.ReadToEndAsync();var hata=p.StandardError.ReadToEndAsync();
     if(!p.WaitForExit(15000)){try{p.Kill();}catch(InvalidOperationException){}throw new IOException("Yalnızca hedef okuma aracı zaman aşımında durduruldu.");}
     var metin=cikti.GetAwaiter().GetResult();var mesaj=hata.GetAwaiter().GetResult();
     KayitOlayTanisi.Yaz("DisHedefBitti","islem="+islem+" sorgu="+sorgu+" ms="+sure.ElapsedMilliseconds+" cikis="+p.ExitCode);
     if(p.ExitCode!=0)throw new IOException("Hedef okunamadı: "+mesaj);
     if(metin.Length>65536)throw new InvalidDataException("Hedef yanıtı sınırı aşıyor.");
     using(var s=new MemoryStream(Encoding.UTF8.GetBytes(metin)))return (HedefPencereYaniti[])new DataContractJsonSerializer(typeof(HedefPencereYaniti[])).ReadObject(s);
    }
   });
  }
 }
}
