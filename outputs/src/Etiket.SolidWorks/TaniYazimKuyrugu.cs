using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Etiket.SolidWorks
{
 // Yalniz tani. Kalici islem gunlugu bu kuyrugu kullanmaz.
 public sealed class TaniYazimKuyrugu
 {
  readonly object kilit=new object();
  readonly Queue<string> bekleyen=new Queue<string>();
  readonly Action<string> yaz;
  readonly int sinir;
  Task isci=Task.CompletedTask;
  bool calisiyor;
  long atlanan,hatalar;
  public TaniYazimKuyrugu(Action<string> yaz,int sinir=2048)
  {this.yaz=yaz??throw new ArgumentNullException(nameof(yaz));if(sinir<1)throw new ArgumentOutOfRangeException(nameof(sinir));this.sinir=sinir;}
  public long Atlanan {get{lock(kilit)return atlanan;}}
  public long YazmaHatasi {get{lock(kilit)return hatalar;}}
  public Task Tamamlanma {get{lock(kilit)return isci;}}
  public void Ekle(string satir)
  {
   lock(kilit)
   {
    if(bekleyen.Count>=sinir){atlanan++;return;}
    bekleyen.Enqueue(satir);
    if(calisiyor)return;
    calisiyor=true;
    isci=Task.Run(async()=>
    {
     await Task.Delay(50).ConfigureAwait(false);
     while(true)
     {
      string toplu;
      lock(kilit)
      {
       if(bekleyen.Count==0){calisiyor=false;return;}
       toplu=string.Concat(bekleyen);bekleyen.Clear();
      }
      // Dosya acma/yazma sirasinda uretici veya UI thread kilidi tutulmaz.
      try{yaz(toplu);}catch(Exception){lock(kilit)hatalar++;}
     }
    });
   }
  }
 }
}