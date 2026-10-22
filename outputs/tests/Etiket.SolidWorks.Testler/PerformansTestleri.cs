using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Etiket.SolidWorks;
partial class Program
{
 static void PerformansDogrulamaTestleri()
 {
  var islem=Guid.NewGuid();var ilk=Guid.NewGuid();var son=Guid.NewGuid();
  Func<Guid,string,HedefPencereYaniti> cevap=(id,yol)=>new HedefPencereYaniti{IslemId=islem,SorguId=id,SurecId=123,Pencere=456,Klasor=yol};
  Func<HedefPencereYaniti[]> cift=()=>new[]{cevap(ilk,@"C:\Test"),cevap(son,@"C:\Test")};
  Func<HedefPencereYaniti[],string> kontrol=x=>HedefAdresKurali.CiftiDogrula(x,islem,ilk,son,123,456);
  Kontrol(kontrol(cift())==@"C:\Test","Tek surecteki iki taze hedef kabul edilmeli.");
  Hata(()=>kontrol(new[]{cevap(ilk,@"C:\Test")}),"Tek okuma reddedilmeli.");
  Hata(()=>kontrol(null),"Eksik cift reddedilmeli.");
  Hata(()=>kontrol(new[]{cevap(ilk,@"C:\Test"),cevap(son,@"C:\Baska")}),"Degisen klasor reddedilmeli.");
  Hata(()=>kontrol(new[]{cevap(son,@"C:\Test"),cevap(ilk,@"C:\Test")}),"Yer degistirmis nonce reddedilmeli.");
  Hata(()=>HedefAdresKurali.CiftiDogrula(cift(),islem,ilk,ilk,123,456),"Ayni nonce iki okuma sayilmamali.");
  foreach(var n in new[]{0,1})
  {
   var a=cift();a[n].SurecId=999;Hata(()=>kontrol(a),"Her okumada PID dogrulanmali.");
   a=cift();a[n].Pencere=999;Hata(()=>kontrol(a),"Her okumada HWND dogrulanmali.");
   a=cift();a[n].IslemId=Guid.NewGuid();Hata(()=>kontrol(a),"Her okumada islem dogrulanmali.");
   a=cift();a[n].SorguId=Guid.NewGuid();Hata(()=>kontrol(a),"Her okumada nonce dogrulanmali.");
  }
  using(var girdi=new ManualResetEventSlim())
  using(var devam=new ManualResetEventSlim())
  {
   var metin=new StringBuilder();int adet=0;
   var q=new TaniYazimKuyrugu(s=>{if(Interlocked.Increment(ref adet)==1){girdi.Set();if(!devam.Wait(3000))throw new TimeoutException();}metin.Append(s);},2);
   q.Ekle("a\n");Kontrol(girdi.Wait(3000),"Arka plan yazicisi baslamali.");
   var uretici=Task.Run(()=>{q.Ekle("b\n");q.Ekle("c\n");q.Ekle("d\n");});
   var serbest=uretici.Wait(1000);devam.Set();
   Kontrol(serbest,"Disk yazicisi beklerken UI ureticisi kilitlenmemeli.");
   Kontrol(q.Tamamlanma.Wait(3000),"Tani kuyrugu bitmeli.");
   Kontrol(metin.ToString()=="a\nb\nc\n"&&q.Atlanan==1,"Kuyruk sinirli ve satir sirasi korunmali.");
  }
  var hataQ=new TaniYazimKuyrugu(s=>{throw new IOException("izole");});
  hataQ.Ekle("satir");Kontrol(hataQ.Tamamlanma.Wait(3000)&&hataQ.YazmaHatasi==1,"Tani disk hatasi CAD ureticisine tasmamali.");
  var yazilan=new StringBuilder();var paralel=new TaniYazimKuyrugu(s=>yazilan.Append(s));
  Task.WaitAll(Enumerable.Range(0,8).Select(n=>Task.Run(()=>{for(int k=0;k<100;k++)paralel.Ekle(n+":"+k+"\n");})).ToArray());
  Kontrol(paralel.Tamamlanma.Wait(3000)&&yazilan.ToString().Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Distinct().Count()==800&&paralel.Atlanan==0,"Paralel tani ureticileri satir kaybetmemeli.");
 }
 static int PerformansOlc()
 {
  var kok=new DirectoryInfo(AppContext.BaseDirectory);while(kok!=null&&!File.Exists(Path.Combine(kok.FullName,"outputs","SolidWorksEtiketleme.slnx")))kok=kok.Parent;
  var alan=Path.Combine(kok.FullName,"work","performans23-olcum",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(alan);
  var rapor=new StringBuilder();
  const int n=1000;var satir="2026-10-16T00:00:00Z | Performans | Yerel tani satiri\n";
  var senkron=Path.Combine(alan,"senkron.log");var toplu=Path.Combine(alan,"toplu.log");
  var saat=Stopwatch.StartNew();for(int k=0;k<n;k++)File.AppendAllText(senkron,satir,new UTF8Encoding(false));var eski=saat.Elapsed.TotalMilliseconds;
  var q=new TaniYazimKuyrugu(s=>File.AppendAllText(toplu,s,new UTF8Encoding(false)));
  saat.Restart();for(int k=0;k<n;k++)q.Ekle(satir);var ui=saat.Elapsed.TotalMilliseconds;
  if(!q.Tamamlanma.Wait(10000))throw new TimeoutException();
  if(File.ReadAllText(senkron)!=File.ReadAllText(toplu)||q.Atlanan!=0)throw new Exception("Olcum loglarinda satir kaybi.");
  rapor.AppendLine("1000 gercek AppendAllText satiri, ayni icerik ve yerel disk:");
  rapor.AppendLine("Eski uretici/disk senkron: "+eski.ToString("F2")+" ms");
  rapor.AppendLine("Yeni uretici kuyruga ekleme: "+ui.ToString("F2")+" ms (disk arka planda, 50ms toplama gecikmesi).");
  var eskiExe=Environment.GetEnvironmentVariable("PRONOM_ESKI_HEDEF_ARACI");var yeniExe=Environment.GetEnvironmentVariable("PRONOM_YENI_HEDEF_ARACI");
  if(!string.IsNullOrEmpty(eskiExe)&&!string.IsNullOrEmpty(yeniExe))
  {
   Func<string,bool,double> dene=(exe,cift)=>
   {
    var args=(cift?"--hedef-cift ":"--hedef ")+Process.GetCurrentProcess().Id+" 1 "+Guid.NewGuid()+" "+Guid.NewGuid()+(cift?" "+Guid.NewGuid():"");
    using(var p=new Process{StartInfo=new ProcessStartInfo{FileName=exe,Arguments=args,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}})
    {
     var sw=Stopwatch.StartNew();p.Start();var o=p.StandardOutput.ReadToEndAsync();var e=p.StandardError.ReadToEndAsync();
     if(!p.WaitForExit(15000)){p.Kill();throw new TimeoutException();}
     Task.WaitAll(o,e);if(p.ExitCode!=1)throw new Exception("Gercek SolidWorks olmayan hedef reddedilmeliydi.");
     return sw.Elapsed.TotalMilliseconds;
    }
   };
   dene(eskiExe,false);dene(yeniExe,true);
   var a=new double[7];var b=new double[7];
   for(int k=0;k<7;k++){a[k]=dene(eskiExe,false)+dene(eskiExe,false);b[k]=dene(yeniExe,true);}
   Array.Sort(a);Array.Sort(b);
   rapor.AppendLine("Yardimci acilis/guvenli ret maliyeti medyani (7tur): eski 2surec "+a[3].ToString("F2")+"ms; yeni 1surec "+b[3].ToString("F2")+"ms.");
   rapor.AppendLine("Bu olcum SLDWORKS olmayan kendi test surecini reddeder; gercek UIA/hedef/CAD kayit hizi olcumu degildir.");
  }
  rapor.AppendLine("Gercek CAD/SQL/yerel ag/Symantec performans kabulunun yerine gecmez.");
  File.WriteAllText(Path.Combine(alan,"sonuc.txt"),rapor.ToString(),new UTF8Encoding(false));
  Console.Write(rapor);Console.WriteLine("Rapor: "+Path.Combine(alan,"sonuc.txt"));return 0;
 }
}