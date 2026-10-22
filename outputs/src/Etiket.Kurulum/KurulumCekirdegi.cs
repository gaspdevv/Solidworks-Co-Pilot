using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Pronom.Kurulum
{
 public sealed class PaketDosyasi {public string Ad{get;set;} public string SHA256{get;set;}}
 public sealed class KurulumBilgisi
 {
  public string Dugme{get;set;}
  public string Baslik{get;set;}
  public string Aciklama{get;set;}
 }
 public static class KurulumCekirdegi
 {
  public const string AddinGuid="{ADC907D3-B1B5-4EAB-9C3A-0B1885FB416C}";
  public const string EskiDll="92AFB675607548987A61F6F634A8AE2BB317A74683FA3B7BE2F5B0DE1B395E69";
  public const string PerformansDll="577227B6B52A5A94A2C354A5C25A359935FEA102E69FFC582DC394B48B053AD0";
  public const string DxfDll="650EC98C440A4D338DDB98D0F141E5F01FDFC24A7DD4CA3F64DF2155C3957B6C";
  public const string Kaydet25Dll="32DC633E46AAAD428B3516EA8D559E4D47810B5A13B11B9A0E696409A44D6983";
  public const string Kaydet26Dll="B6E859FCE2A8F41FB241F49205E2498D754EBC051570AAE65628832D63CB42E9";
  public const string YeniDll="10335C585CA6B439099F3930259DBF0CC0A3FF0F0037591AC340C7E14BBA4D7B";
  public static bool BilinenDllMi(string hash)=>hash==EskiDll||hash==PerformansDll||hash==DxfDll||hash==Kaydet25Dll||hash==Kaydet26Dll||hash==YeniDll;
  public static KurulumBilgisi KurulumDurumu(string kayitHash,bool hedefVar,bool hazir,bool paketSaglam)
  {
   if(kayitHash!=null&&!BilinenDllMi(kayitHash))throw new IOException("Mevcut eklenti sürümü tanınmıyor; dosyalar değiştirilmedi.");
   if(kayitHash==null&&!hedefVar)return new KurulumBilgisi{Dugme="Kur",Baslik="Eklentiyi kullanıma hazır hale getirin",Aciklama="İlk kurulum yapılacak."};
   if(hazir)return new KurulumBilgisi{Dugme="Kontrol et",Baslik="Pronom güncel",Aciklama="Güncel sürüm kurulu. Bağlantı ayarlarını düzenleyebilirsiniz."};
   if(kayitHash==EskiDll||kayitHash==PerformansDll||kayitHash==DxfDll||kayitHash==Kaydet25Dll||kayitHash==Kaydet26Dll||kayitHash==YeniDll&&paketSaglam)return new KurulumBilgisi{Dugme="Güncelle",Baslik="Pronom'u güncelleyin",Aciklama=kayitHash!=YeniDll?"Kayıt penceresi ve bileşen ekleme kuyruğu düzeltmeleri kurulacak.":"Eklenti güncel. Kurulum ve bağlantı araçları güncellenecek."};
   return new KurulumBilgisi{Dugme="Onar",Baslik="Pronom kurulumunu tamamlayın",Aciklama="Eksik dosyalar veya eklenti kaydı onarılacak."};
  }
  public static string Hash(string yol){using(var s=File.OpenRead(yol))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","");}
  public static string AltYol(string kok,string ad)
  {
   if(string.IsNullOrWhiteSpace(ad)||Path.IsPathRooted(ad)||ad.IndexOf(':')>=0)throw new IOException("Paket dosya yolu geçersiz.");
   var root=Path.GetFullPath(kok).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   var result=Path.GetFullPath(Path.Combine(root,ad.Replace('/',Path.DirectorySeparatorChar)));
   if(!result.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Paket klasör dışına çıkamaz.");
   return result;
  }
  public static void BaglantisizDizin(string path)
  {
   for(var d=new DirectoryInfo(Path.GetFullPath(path));d!=null;d=d.Parent)
    if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Kurulum yolunda yönlendirilmiş klasör var: "+d.FullName);
  }
  public static Uri ApiAdresi(string metin)
  {
   if(!Uri.TryCreate((metin??"").Trim().TrimEnd('/')+"/",UriKind.Absolute,out var u)
    ||(u.Scheme!="http"&&u.Scheme!="https")||!string.IsNullOrEmpty(u.UserInfo)||!string.IsNullOrEmpty(u.Query)||!string.IsNullOrEmpty(u.Fragment)||u.AbsolutePath!="/")
    throw new ArgumentException("Merkez adresini http://192.0.2.10:5082 biçiminde yazın.");
   if(u.Host=="0.0.0.0")throw new ArgumentException("Merkezin belirli adresini yazın.");
   return u;
  }
  public static PaketDosyasi[] Liste()
  {
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Pronom.Paket.json"))
   using(var r=new StreamReader(s,Encoding.UTF8))return new JavaScriptSerializer().Deserialize<PaketDosyasi[]>(r.ReadToEnd());
  }
  public static void PaketiAc(string hedef)
  {
   BaglantisizDizin(hedef);
   Directory.CreateDirectory(hedef);
   var liste=Liste();var adlar=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   if(liste.Length<6)throw new IOException("Kurulum paketi eksik.");
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Pronom.Paket.zip"))
   using(var zip=new ZipArchive(s,ZipArchiveMode.Read))
   {
    long boyut=0;
    foreach(var e in zip.Entries)
    {
     var yol=AltYol(hedef,e.FullName);
     var kayit=liste.SingleOrDefault(x=>string.Equals(x.Ad,e.FullName,StringComparison.OrdinalIgnoreCase));
     if(kayit==null||!adlar.Add(e.FullName)||e.Length>40*1024*1024||(boyut+=e.Length)>100*1024*1024)throw new IOException("Paket içeriği uyuşmuyor.");
     BaglantisizDizin(Path.GetDirectoryName(yol));Directory.CreateDirectory(Path.GetDirectoryName(yol));
     using(var input=e.Open())using(var output=new FileStream(yol,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
     if(Hash(yol)!=kayit.SHA256)throw new IOException("Dosya özeti uyuşmuyor: "+e.FullName);
    }
   }
   if(adlar.Count!=liste.Length)throw new IOException("Paket dosyaları eksik.");
   if(Hash(AltYol(hedef,"Pronom.SolidWorks.dll"))!=YeniDll)throw new IOException("Kaydet27 DLL doğrulanamadı.");
  }
  public static void MakineDosyalari(string kaynak,string hedef,Action<string> kaydet,Action<string> geriKaydet,Action kapaliMi,Action<string,string> kopyala=null)
  {
   kapaliMi();BaglantisizDizin(hedef);Directory.CreateDirectory(hedef);
   var dll=Path.Combine(hedef,"Pronom.SolidWorks.dll");
   if(File.Exists(dll)&&!BilinenDllMi(Hash(dll)))throw new IOException("Bu klasörde tanınmayan Pronom DLL var; üzerine yazılmadı.");
   var backup=AltYol(hedef,"Yedekler/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
   var once=new Dictionary<string,bool>();var degisen=new List<string>();
   Action<string,string> copy=kopyala??((a,b)=>File.Copy(a,b,true));
   foreach(var f in Liste())
   {
    var src=AltYol(kaynak,f.Ad);if(Hash(src)!=f.SHA256)throw new IOException("Kurulum kaynağı değişmiş: "+f.Ad);
    var dst=AltYol(hedef,f.Ad);BaglantisizDizin(Path.GetDirectoryName(dst));
    if(File.Exists(dst)&&(File.GetAttributes(dst)&FileAttributes.ReparsePoint)!=0)throw new IOException("Yönlendirilmiş dosya üzerine kurulamaz.");
    once[f.Ad]=File.Exists(dst);
    if(once[f.Ad]){var old=AltYol(backup,f.Ad);Directory.CreateDirectory(Path.GetDirectoryName(old));File.Copy(dst,old,false);}
   }
   try
   {
    foreach(var f in Liste())
    {
     kapaliMi();var dst=AltYol(hedef,f.Ad);Directory.CreateDirectory(Path.GetDirectoryName(dst));degisen.Add(f.Ad);copy(AltYol(kaynak,f.Ad),dst);
     if(Hash(dst)!=f.SHA256)throw new IOException("Kopyalanan dosya uyuşmuyor: "+f.Ad);
    }
    kapaliMi();kaydet(dll);
   }
   catch(Exception hata)
   {
    kapaliMi();
    foreach(var ad in degisen)
    {
     var dst=AltYol(hedef,ad);
     if(once[ad])File.Copy(AltYol(backup,ad),dst,true);else if(File.Exists(dst))File.Delete(dst);
    }
    try{geriKaydet(dll);}catch(Exception geri){throw new IOException("Kurulum tamamlanamadı; COM geri dönüşü de kontrol edilmeli. Yedek: "+backup,new AggregateException(hata,geri));}
    throw new IOException("Kurulum tamamlanamadı; önceki dosyalar geri alındı. "+hata.Message,hata);
   }
  }
  public static void KullaniciAyarlari(string klasor,Uri adres,bool baslangic,Func<object> okuBaslangic,Action<object> yazBaslangic)
  {
   BaglantisizDizin(klasor);Directory.CreateDirectory(klasor);
   var dosyalar=new Dictionary<string,byte[]> {{"ApiAdresi.txt",new UTF8Encoding(false).GetBytes(adres.AbsoluteUri)}};
   foreach(var ad in new[]{"StandartKayit.acik","BagimsizKayit.acik","NativeAdDenemesi.acik","KayitOlayTanisi.acik"})dosyalar[ad]=new byte[]{49};
   var backup=AltYol(klasor,"AyarYedekleri/Setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
   var onceki=new Dictionary<string,byte[]>();var startup=okuBaslangic();
   foreach(var ad in dosyalar.Keys)
   {
    var p=AltYol(klasor,ad);
    if(File.Exists(p)&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Yönlendirilmiş kullanıcı ayarı değiştirilemez.");
    onceki[ad]=File.Exists(p)?File.ReadAllBytes(p):null;
   }
   try
   {
    foreach(var kv in dosyalar)
    {
     var p=AltYol(klasor,kv.Key);var temp=p+"."+Guid.NewGuid().ToString("N")+".tmp";
     try{File.WriteAllBytes(temp,kv.Value);if(File.Exists(p))File.Replace(temp,p,AltYol(backup,kv.Key));else File.Move(temp,p);}
     finally{if(File.Exists(temp))File.Delete(temp);}
    }
    yazBaslangic(baslangic?1:0);
   }
   catch
   {
    foreach(var kv in onceki){var p=AltYol(klasor,kv.Key);if(kv.Value==null){if(File.Exists(p))File.Delete(p);}else File.WriteAllBytes(p,kv.Value);}
    yazBaslangic(startup);throw;
   }
  }
 }
}
