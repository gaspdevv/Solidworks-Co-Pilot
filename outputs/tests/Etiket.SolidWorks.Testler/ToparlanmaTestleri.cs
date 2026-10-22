using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Etiket.SolidWorks;
partial class Program
{
 sealed class KopanHttpTasiyici:HttpMessageHandler
 {
  public Guid Id;public string Ad;public bool Ozel,Kesinti=true,YanitKaybi;public int Commit,Cagri;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t)
  {
   Cagri++;
   if(r.Method!=HttpMethod.Post||r.RequestUri.AbsolutePath!="/api/rezervasyonlar/"+Id.ToString("D")+(Ozel?"/iptal":"/onay"))throw new Exception("Toparlama başka GUID/yol veya ad ayırma çağrısı yaptı.");
   if(Kesinti&&!YanitKaybi)return Task.FromException<HttpResponseMessage>(new HttpRequestException("Sunucuya ulaşılmadı"));
   Commit=1;
   if(Kesinti)return Task.FromException<HttpResponseMessage>(new HttpRequestException("Commit sonrası yanıt kaybı"));
   var json=Ozel?"{\"islemId\":\""+Id+"\",\"durum\":\"Iptal\"}":"{\"dosyaAdi\":\""+Ad+"\"}";
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});
  }
 }
 static KayitSonucuToparlama YenidenBaslat(StandartTestBaglami b)
 {
  var g=new KaliciIslemGunlugu(Path.Combine(Path.GetDirectoryName(b.I.GercekDosya),"gunluk"));
  return new KayitSonucuToparlama(g,new KaydetmeIslemYoneticisi(b.S,g));
 }
 static void ToparlanmaTestleri()
 {
  foreach(var ozel in new[]{false,true})foreach(var yanitKaybi in new[]{false,true})
  {
   var b=new StandartTestBaglami("P",ozel?"HTTP Ozel":null);b.DosyayiOlustur();
   var tel=new KopanHttpTasiyici{Id=b.I.IslemId,Ad=b.I.AyrilanDosyaAdi,Ozel=ozel,YanitKaybi=yanitKaybi};
   using(var http=new HttpAdServisi(new Uri("http://127.0.0.1:1/"),tel))
   {
    var y=new KaydetmeIslemYoneticisi(http,b.G);var n=new StandartKayitYoneticisi(y,b.G);n.Hazirla(b.I,b.Kaynak);
    Hata(()=>n.Kaydedildi(2,b.I.GercekDosya,b.I.GercekDosya,b.I.IslemId.ToString("D")),"Gerçek HTTP istemcisinin istek/yanıt kesintisi kayda yansımalı.");
    Kontrol(tel.Commit==(yanitKaybi?1:0)&&KayitSonucuToparlama.Uygun(b.G.Oku(b.I.IslemId)),"HTTP commit öncesi/sonrası kayıp aynı kalıcı sonuç niyetini korumalı.");
    tel.Kesinti=false;var yeniG=new KaliciIslemGunlugu(Path.Combine(Path.GetDirectoryName(b.I.GercekDosya),"gunluk"));
    var sonuc=new KayitSonucuToparlama(yeniG,new KaydetmeIslemYoneticisi(http,yeniG)).Tamamla(b.I.IslemId);
    Kontrol(tel.Commit==1&&tel.Cagri==2&&sonuc.Asama==(ozel?"Iptal":"Onaylandi"),"HTTP tekrarında aynı GUID'ye yalnızca doğru sonuç endpoint'i gönderilmeli.");
   }
  }
  foreach(var ozel in new[]{false,true})
  {
   var b=new StandartTestBaglami("P",ozel?"ServisKapaliOzel":null);b.DosyayiOlustur();b.S.OnayOncesiKaybi=!ozel;b.S.IptalOncesiKaybi=ozel;
   Hata(()=>b.N.Kaydedildi(2,b.I.GercekDosya,b.I.GercekDosya,b.I.IslemId.ToString("D")),"API işleme ulaşmadan kapalıysa dosya kanıtı korunmalı.");
   Kontrol(b.S.Onaylanan.Count==0&&b.S.IptalEdilen.Count==0,"Servis kapalı örneği merkezi commit öncesi kesintiyi temsil etmeli.");
   b.S.OnayOncesiKaybi=false;b.S.IptalOncesiKaybi=false;
   Kontrol(YenidenBaslat(b).Tamamla(b.I.IslemId).Asama==(ozel?"Iptal":"Onaylandi"),"Servis geri gelince ilk gerçek merkezi sonuç aynı kimlikle tamamlanmalı.");
  }
  foreach(var tur in new[]{"P","M"})foreach(var ozel in new[]{false,true})
  {
   var b=new StandartTestBaglami(tur,ozel?"Özel Ad":null);b.DosyayiOlustur();
   b.S.OnayKaybi=!ozel;b.S.IptalKaybi=ozel;
   Hata(()=>b.N.Kaydedildi(2,b.I.GercekDosya,b.I.GercekDosya,b.I.IslemId.ToString("D")),"Kaydedilen P/M otomatik/özel sonucun yanıt kaybı korunmalı.");
   var kalici=b.G.Oku(b.I.IslemId);
   Kontrol(KayitDosyaKaniti.Var(kalici)&&KayitSonucuToparlama.Uygun(kalici),"HTTP öncesinde başarılı kayıt niyeti ve SHA/boyut/zaman atomik saklanmalı.");
   Kontrol(kalici.Asama==(ozel?"OzelAdIptalBekliyor":"Kaydedildi"),"Yalnızca doğru merkezi sonuç tekrarına kalmalı.");
   var dosya=File.ReadAllBytes(b.I.GercekDosya);var adet=Directory.GetFiles(Path.GetDirectoryName(b.I.GercekDosya)).Length;var ayir=b.S.AyirmaCagrisi;
   b.S.OnayKaybi=false;b.S.IptalKaybi=false;
   var toparla=YenidenBaslat(b);var sonuc=toparla.Tamamla(b.I.IslemId);
   Kontrol(sonuc.IslemId==b.I.IslemId&&sonuc.Asama==(ozel?"Iptal":"Onaylandi"),"Yeni yönetici/journal aynı GUID ile toparlamalı.");
   Kontrol(b.S.AyirmaCagrisi==ayir&&b.S.Onaylanan.Count==(ozel?0:1)&&b.S.IptalEdilen.Count==(ozel?1:0),"Toparlama yeni numara veya ikinci merkezi kayıt üretmemeli.");
   Kontrol(File.ReadAllBytes(b.I.GercekDosya).SequenceEqual(dosya)&&Directory.GetFiles(Path.GetDirectoryName(b.I.GercekDosya)).Length==adet,"Toparlama CAD dosyası yazmamalı/çoğaltmamalı.");
   var cagrilar=b.S.OnayCagrisi+b.S.IptalCagrisi;toparla.Tamamla(b.I.IslemId);
   Kontrol(cagrilar==b.S.OnayCagrisi+b.S.IptalCagrisi,"Terminal yeniden çalıştırma HTTP tekrarı yapmamalı.");
   b.N.SonucuTekrarDene();b.D.KayitSonucunuAl();Kontrol(!b.N.Bekliyor&&!b.D.Aktif,"Başka nesnede tamamlanan sonuç mevcut oturumun beklemesini de temizlemeli.");
  }
  foreach(var durum in new[]{"dosya-yok","degisen-dosya","kanit-yok","zaman","boyut","asama","niyet","hedef","tur","kilit","bozuk-guid"})
  {
   var b=new StandartTestBaglami();b.DosyayiOlustur();b.S.OnayKaybi=true;
   Hata(()=>b.N.Kaydedildi(2,b.I.GercekDosya,b.I.GercekDosya,b.I.IslemId.ToString("D")),"Toparlanma ret örneğinin ilk onay kaybı kalmalı.");
   var i=b.G.Oku(b.I.IslemId);
   if(durum=="dosya-yok")File.Move(i.GercekDosya,i.GercekDosya+".tasindi");
   if(durum=="degisen-dosya")File.AppendAllText(i.GercekDosya,"değişti");
   if(durum=="kanit-yok")i.KayitSHA256="";
   if(durum=="zaman")i.KayitKanitiZamani="geçersiz";
   if(durum=="boyut")i.KayitBoyutu++;
   if(durum=="asama")i.Asama="NativeYazimBasladi";
   if(durum=="niyet")i.Asama="OzelAdIptalBekliyor";
   if(durum=="hedef")i.HedefDosya=Path.Combine(i.HedefKlasor,"P00999.SLDPRT");
   if(durum=="tur")i.KaydetmeBicimi=KaydetmeBicimi.KopyaVeDevam;
   b.G.Yaz(i);
   if(durum=="bozuk-guid"){
    var p=Path.Combine(Path.GetDirectoryName(i.GercekDosya),"gunluk",i.IslemId.ToString("D")+".json");
    File.WriteAllText(p,File.ReadAllText(p).Replace(i.IslemId.ToString("D"),Guid.NewGuid().ToString("D")));
   }
   b.S.OnayKaybi=false;var once=b.S.OnayCagrisi+b.S.IptalCagrisi;
   if(durum=="kilit")using(var f=new FileStream(i.GercekDosya,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Hata(()=>YenidenBaslat(b).Tamamla(i.IslemId),"Dosya yazım kilidindeyken sonuç gönderilmemeli.");
   else Hata(()=>YenidenBaslat(b).Tamamla(i.IslemId),"Eksik veya uyuşmayan kalıcı kanıt reddedilmeli: "+durum);
   Kontrol(once==b.S.OnayCagrisi+b.S.IptalCagrisi,"Reddedilen toparlama HTTP onay/iptal gönderemez: "+durum);
  }
  // Native başarı kanıtı olmayan yazım aşamalarının dosyası bulunsa bile otomatik onay yok.
  foreach(var asama in new[]{"AdBekleniyor","Ayrildi","BagimsizKuyrukta","NativeYazimBasladi","BagimsizYazimBasladi","IptalBekliyor"})
  {
   var b=new StandartTestBaglami();b.DosyayiOlustur();var i=b.G.Oku(b.I.IslemId);i.Asama=asama;b.G.Yaz(i);
   Hata(()=>YenidenBaslat(b).Tamamla(i.IslemId),"Belirsiz aşama yeniden CAD yazımına veya onaya dönüşemez: "+asama);
   Kontrol(b.S.OnayCagrisi==0&&b.S.IptalCagrisi==0,"Belirsiz dosyanın varlığı numara iptali/onay kanıtı değildir.");
  }
  foreach(var ozel in new[]{false,true})
  {
   var b=new StandartTestBaglami("M"); // Yalnızca izole klasör/günlük/fake servis için bağlam.
   var bagimsiz=new BagimsizKayitYoneticisi(b.Y,b.G);var i=bagimsiz.Baslat("M",Path.GetDirectoryName(b.I.GercekDosya));bagimsiz.AdiAl(i);
   var c=new SahteBilesen{KaynakYolu=b.Kaynak,YeniReferansYolu=b.Kaynak};b.S.OnayKaybi=!ozel;b.S.IptalKaybi=ozel;
   Hata(()=>bagimsiz.Kaydet(i,c,ozel?"OzelBagimsiz":null),"Bağımsız kayıt sonrası onay/iptal kaybı kalıcı olmalı.");
   Kontrol(c.Cagri==1&&KayitSonucuToparlama.Uygun(b.G.Oku(i.IslemId)),"Bağımsız native başarı/ref kanıtı toparlanma kaydına dönüşmeli.");
   var ayir=b.S.AyirmaCagrisi;b.S.OnayKaybi=false;b.S.IptalKaybi=false;
   var sonuc=YenidenBaslat(b).Tamamla(i.IslemId);
   Kontrol(sonuc.Asama==(ozel?"Iptal":"Onaylandi")&&c.Cagri==1&&b.S.AyirmaCagrisi==ayir,"Bağımsız yeniden başlatma native yazımı veya ad ayırmayı tekrarlamamalı.");
  }
 }
}
