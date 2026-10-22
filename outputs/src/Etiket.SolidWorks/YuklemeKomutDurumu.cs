using System.Collections.Generic;
using System.Linq;
using System.IO;
namespace Etiket.SolidWorks
{
 public sealed class YuklemeKomutDurumu
 {
  readonly HashSet<int> acikKomutlar=new HashSet<int>();
  readonly List<int> komutSirasi=new List<int>();
  int? secimiBaslatanKomut;
  bool bilesenSecimi;
  bool secimdeDosyaYuklendi;
  public bool BilesenSecimiSuruyor=>bilesenSecimi;
  public int? SecimiBaslatanKomut=>secimiBaslatanKomut;
  public bool Guvenli=>acikKomutlar.Count==0&&!bilesenSecimi;
  // Bunlar kullanıcı cihazında gözlenen dosya/yazım komutlarıdır.
  // Çalışan komut sorgusu bu komutların veya bileşen seçiminin içinden yapılmaz.
  static bool YuklemeVeyaKayit(int komut)=>komut==0||komut==1||komut==2||komut==620||komut==3494;
  public bool KomutDogrulanabilir=>!bilesenSecimi&&acikKomutlar.Count>0&&!acikKomutlar.Any(YuklemeVeyaKayit);
  public string BekleyenKomutlar=>string.Join(",",acikKomutlar.OrderBy(x=>x));
  public bool CalisanKomutlaDogrula(bool arayuzKomutuAktif)
  {
   if(!KomutDogrulanabilir||arayuzKomutuAktif)return false;
   acikKomutlar.Clear();komutSirasi.Clear();return true;
  }
  public void KomutAcildi(int komut)
  {
   // Keep active IDs unique, but always record the latest opening order.
   // SW can reopen 1 after an older 1 never sent close; that new 1, not a
   // later stale sketch46, owns the following Insert Component selection.
   acikKomutlar.Add(komut);komutSirasi.Remove(komut);komutSirasi.Add(komut);
  }
  public void BilesenSecimiBasladi()
  {
   if(bilesenSecimi)return; // İç dosya seçici dış yerleştirme komutunun sahipliğini değiştirmez.
   bilesenSecimi=true;secimdeDosyaYuklendi=false;secimiBaslatanKomut=komutSirasi.Count==0?(int?)null:komutSirasi[komutSirasi.Count-1];
  }
  public void SecimdeBelgeYuklendi(string yol)
  {if(bilesenSecimi&&!string.IsNullOrWhiteSpace(yol)&&Path.IsPathRooted(yol))secimdeDosyaYuklendi=true;}
  public void BilesenSecimiBitti(){bilesenSecimi=false;secimiBaslatanKomut=null;secimdeDosyaYuklendi=false;}
  public void KomutKapandi(int komut,int neden=0)
  {
   // Yinelenen pre bildirimi aynı komutu iki kez saymaz; ilgisiz close yüklemeyi bitirmez.
   var vardi=acikKomutlar.Remove(komut);
   if(vardi)komutSirasi.Remove(komut);
   if(vardi&&secimiBaslatanKomut==komut)BilesenSecimiBitti();
   // Observed SW2023 automatic Insert Component: no CommandOpenPre, then a
   // real DocumentLoad, then CommandClose(-3,-2), with no FileOpenPost.
   // Only that completed, ownerless selection may be released here.
   else if(bilesenSecimi&&secimiBaslatanKomut==null&&secimdeDosyaYuklendi
    &&komut==-3&&neden==-2&&acikKomutlar.Count==0)BilesenSecimiBitti();
  }
  public void Temizle(){acikKomutlar.Clear();komutSirasi.Clear();BilesenSecimiBitti();}
 }
}
