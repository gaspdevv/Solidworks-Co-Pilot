using System.Net;
using System.Net.Http.Json;
using Etiket.Api;
using Etiket.TestIstemcisi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

if(args.Any(x=>x is not ("--sql" or "--sql-kurulum")))throw new ArgumentException("Desteklenen test seçenekleri: --sql veya --sql-kurulum.");
var sqlModu=args.Contains("--sql")||args.Contains("--sql-kurulum");
if(args.Contains("--sql-kurulum"))await Etiket.Api.Testler.KurulumSqlTesti.Calistir();
if(sqlModu)
{
 var ayar=Environment.GetEnvironmentVariable("ConnectionStrings__MerkeziVeritabani")??throw new InvalidOperationException("Test bağlantısını belirtin.");
 var sqlAyar=new SqlConnectionStringBuilder(ayar);
 if(!sqlAyar.InitialCatalog.StartsWith("SolidWorksAutomation_ApiTest_",StringComparison.Ordinal))throw new InvalidOperationException("Yalnızca ayrı test veritabanı kullanılabilir.");
 await using var c=new SqlConnection(ayar);await c.OpenAsync();
 await using var q=new SqlCommand("SELECT (SELECT COUNT_BIG(*) FROM dbo.Documents)+(SELECT COUNT_BIG(*) FROM dbo.Rezervasyonlar);",c);
 if(Convert.ToInt64(await q.ExecuteScalarAsync())!=0)throw new InvalidOperationException("Test veritabanı başlangıçta boş olmalıdır; hiçbir kayıt silinmez.");
}
var sahte=new BellekDeposu();
WebApplication Yeni()=>ApiKurulumu.Olustur([],b=>{
 b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
 if(!sqlModu)b.Services.AddSingleton<IBelgeDeposu>(sahte);
});
var sayac=0;

void Kontrol(bool dogru,string mesaj){if(!dogru)throw new InvalidOperationException("TEST BAŞARISIZ: "+mesaj);sayac++;}
await using var app=Yeni();
if(!sqlModu)app.MapGet("/api/test-modu",()=>Microsoft.AspNetCore.Http.Results.Ok(new{mod="BellekTesti"}));
await app.StartAsync();
using var http=new HttpClient{BaseAddress=new Uri(app.Urls.Single()),Timeout=TimeSpan.FromSeconds(30)};
var istemci=new EtiketApiIstemcisi(http);
async Task Hata(Func<Task> islem,int kod){try{await islem();throw new InvalidOperationException("Beklenen hata yok.");}catch(IstemciHatasi h){Kontrol(h.DurumKodu==kod,"HTTP hata kodu "+kod);}}
Kontrol((await istemci.SaglikAsync()).Veritabani=="Bağlı","Sağlık kontrolü.");
var web=await http.GetStringAsync("/");
Kontrol(web.Contains("Dosya kayıtları")&&!web.Contains("<th>ETİKET</th>"),"Web ekranı etiket sütunu olmadan sunulmalı.");
Kontrol(web.Contains("data-tur=\"P\"")&&web.Contains("data-tur=\"M\""),"Web ekranında iki tür filtresi bulunmalı.");
var id=Guid.NewGuid();var p=await istemci.AyirAsync(id,"P");
Kontrol(p.DosyaAdi=="P00001.SLDPRT"&&p.Durum=="Ayrildi","İlk parça adı merkezde üretilmeli.");
var m=await istemci.AyirAsync(Guid.NewGuid(),"M");Kontrol(m.DosyaAdi=="M00001.SLDASM","Montaj sayacı ayrı olmalı.");
Kontrol((await istemci.ListeleAsync()).Belgeler.Count==0,"Ayrılmış dosyalar tamamlanmış listede görünmemeli.");
var tekrarlar=await Task.WhenAll(Enumerable.Range(0,25).Select(_=>istemci.AyirAsync(id,"P")));
Kontrol(tekrarlar.All(x=>x.DosyaAdi==p.DosyaAdi),"Aynı işlemin eşzamanlı tekrarları aynı adı dönmeli.");
await Hata(()=>istemci.AyirAsync(id,"M"),409);
await Hata(()=>istemci.AyirAsync(Guid.Empty,"P"),400);
await Hata(()=>istemci.AyirAsync(Guid.NewGuid(),"X"),400);
var b=await istemci.OnaylaAsync(id);Kontrol(b.Id==1&&b.DosyaAdi==p.DosyaAdi,"Onay ilk global ID'yi oluşturmalı.");

// Sunucu işlemi tamamlar, fakat yanıt istemciye ulaşmaz: yeni GUID üretmeden toparlan.
using(var tasiyici=new YanitiKaybedenTasiyici())
using(var kayipHttp=new HttpClient(tasiyici){BaseAddress=http.BaseAddress})
{
 var kayipIstemci=new EtiketApiIstemcisi(kayipHttp);
 try{await kayipIstemci.OnaylaAsync(id);Kontrol(false,"Onay yanıt kaybı yakalanmalı.");}
 catch(IstemciHatasi h){Kontrol(h.SonucBelirsiz,"Onay kaybında sonuç belirsiz olmalı.");}
 Kontrol(tasiyici.YazmaSayisi==1,"İstemci otomatik yazma tekrarı yapmamalı.");
 Kontrol((await istemci.RezervasyonBulAsync(id)).Durum=="Onaylandi","Yanıt kaybında işlem kimliğiyle sonuç bulunmalı.");
}
var onaylar=await Task.WhenAll(Enumerable.Range(0,25).Select(_=>istemci.OnaylaAsync(id)));
Kontrol(onaylar.All(x=>x==b),"Tekrarlanan onay aynı kayıt ve tarihi dönmeli.");
Kontrol((await istemci.ListeleAsync()).Belgeler.Count==1,"Tekrarlanan onay yeni belge eklememeli.");
Kontrol((await istemci.RezervasyonBulAsync(id)).Durum=="Onaylandi","Onaylı işlem sorgulanmalı.");
Kontrol((await istemci.BulAsync(p.DosyaAdi))==b,"Dosya adıyla sorgu.");
await Hata(()=>istemci.IptalEtAsync(id),409);
Kontrol((await istemci.IptalEtAsync(m.IslemId)).Durum=="Iptal","Kaydetme iptali.");
Kontrol((await istemci.IptalEtAsync(m.IslemId)).Durum=="Iptal","İptal tekrarı güvenli olmalı.");
await Hata(()=>istemci.OnaylaAsync(m.IslemId),409);
await Hata(()=>istemci.OnaylaAsync(Guid.NewGuid()),404);
await Hata(()=>istemci.BulAsync("P00001"),400);
await Hata(()=>istemci.BulAsync("P99999.SLDPRT"),404);
await Hata(()=>istemci.ListeleAsync(-1),400);
Kontrol(!(await http.GetStringAsync("api/documents")).Contains("\"etiket\""),"Yanıtta ayrı etiket alanı olmamalı.");
using(var eski=await http.PostAsJsonAsync("api/documents",new{belgeTuru="P",dosyaAdi="Mil.SLDPRT"}))Kontrol(eski.StatusCode==HttpStatusCode.MethodNotAllowed,"Eski doğrudan kayıt endpoint'i kapalı olmalı.");
var karisik=await Task.WhenAll(Enumerable.Range(0,100).Select(i=>istemci.AyirAsync(Guid.NewGuid(),i%2==0?"P":"M")));
Kontrol(karisik.Select(x=>x.DosyaAdi).Distinct().Count()==100,"Eşzamanlı farklı işlemlerde adlar benzersiz olmalı.");
var kayitlar=await Task.WhenAll(karisik.Select(x=>istemci.OnaylaAsync(x.IslemId)));
Kontrol(kayitlar.Select(x=>x.Id).Distinct().Count()==100,"Global ID benzersiz olmalı.");
Kontrol((await istemci.ListeleAsync(adet:500)).Belgeler.Count==101,"Yalnızca onaylı kayıtlar görünmeli.");
if(!sqlModu)
{
 var tumBelgeler=(await istemci.ListeleAsync(adet:500)).Belgeler.OrderBy(x=>x.Id).ToArray();
 var ilkSayfa=await http.GetFromJsonAsync<BelgeListesi>("api/documents");
 Kontrol(ilkSayfa!.Toplam==101&&ilkSayfa.Belgeler.Count==100,"İlk sayfa tüm kayıt sayısını vermeli.");
 var sonBelge=tumBelgeler.Last();
 var aranan=await http.GetFromJsonAsync<BelgeListesi>("api/documents?arama="+Uri.EscapeDataString(sonBelge.DosyaAdi.ToLowerInvariant()));
 Kontrol(aranan!.Toplam==1&&aranan.Belgeler.Single().Id==sonBelge.Id,"Arama ilk 100 dışındaki kaydı bulmalı.");
 var parcalar=await http.GetFromJsonAsync<BelgeListesi>("api/documents?tur=p&adet=10");
 Kontrol(parcalar!.Toplam==tumBelgeler.Count(x=>x.DosyaAdi[0]=='P')&&parcalar.Belgeler.Count==10&&parcalar.Belgeler.All(x=>x.DosyaAdi[0]=='P'),"Tür filtresi tüm kayıtları sayıp sayfalamalı.");
 var montajlar=await http.GetFromJsonAsync<BelgeListesi>("api/documents?tur=M&baslangic=10&adet=10");
 Kontrol(montajlar!.Toplam==tumBelgeler.Count(x=>x.DosyaAdi[0]=='M')&&montajlar.Belgeler.Select(x=>x.Id).SequenceEqual(tumBelgeler.Where(x=>x.DosyaAdi[0]=='M').Skip(10).Take(10).Select(x=>x.Id)),"Filtreli sonraki sayfa sabit ID sırasında olmalı.");
 var zitTur=sonBelge.DosyaAdi[0]=='P'?"M":"P";
 var birlikte=await http.GetFromJsonAsync<BelgeListesi>("api/documents?arama="+sonBelge.DosyaAdi+"&tur="+zitTur);
 Kontrol(birlikte!.Toplam==0&&birlikte.Belgeler.Count==0,"Arama ve tür birlikte uygulanmalı.");
 var literal=await http.GetFromJsonAsync<BelgeListesi>("api/documents?arama="+Uri.EscapeDataString("%_['"));
 Kontrol(literal!.Toplam==0&&literal.Belgeler.Count==0,"Arama SQL jokerlerini kalıp olarak yorumlamamalı.");
 var bosSayfa=await http.GetFromJsonAsync<BelgeListesi>("api/documents?baslangic=500");
 Kontrol(bosSayfa!.Toplam==101&&bosSayfa.Belgeler.Count==0,"Son sayfa dışındaki istek toplamı korumalı.");
 var kirpilmis=await http.GetFromJsonAsync<BelgeListesi>("api/documents?arama="+Uri.EscapeDataString("  "+sonBelge.DosyaAdi+"  ")+"&tur=%20%20");
 Kontrol(kirpilmis!.Toplam==1,"Boşluklar filtre doğrulamasından önce temizlenmeli.");
 using(var kotuTur=await http.GetAsync("api/documents?tur=X"))Kontrol(kotuTur.StatusCode==HttpStatusCode.BadRequest,"Geçersiz tür 400 olmalı.");
 using(var uzun=await http.GetAsync("api/documents?arama="+new string('a',65)))Kontrol(uzun.StatusCode==HttpStatusCode.BadRequest,"Uzun arama 400 olmalı.");
 Kontrol((await istemci.ListeleAsync(adet:500)).Belgeler.Count==101,"Salt filtre sorguları kayıt üretmemeli.");
}
var yarisan=await istemci.AyirAsync(Guid.NewGuid(),"P");
async Task Yaristir(Func<Task> islem){try{await islem();}catch(IstemciHatasi h) when(h.DurumKodu==409){}}
await Task.WhenAll(Yaristir(()=>istemci.OnaylaAsync(yarisan.IslemId)),Yaristir(()=>istemci.IptalEtAsync(yarisan.IslemId)));
var son=await istemci.RezervasyonBulAsync(yarisan.IslemId);
var tum=(await istemci.ListeleAsync(adet:500)).Belgeler;
Kontrol((son.Durum=="Onaylandi"&&tum.Count(x=>x.DosyaAdi==son.DosyaAdi)==1)||(son.Durum=="Iptal"&&tum.All(x=>x.DosyaAdi!=son.DosyaAdi)),"Onay/iptal yarışı tek ve tutarlı sonuç üretmeli.");

// İptal sonrası geri kullanım, eski GUID tekrarları ve geri dönen adlara eşzamanlı talep.
var geri1=await istemci.AyirAsync(Guid.NewGuid(),"M");await istemci.IptalEtAsync(geri1.IslemId);
var geri2=await istemci.AyirAsync(Guid.NewGuid(),"M");
Kontrol(geri1.DosyaAdi==geri2.DosyaAdi&&geri1.IslemId!=geri2.IslemId,"İptal numarası yeni işlemde tekrar kullanılmalı.");
await istemci.IptalEtAsync(geri1.IslemId);
Kontrol((await istemci.RezervasyonBulAsync(geri2.IslemId)).Durum=="Ayrildi","Eski iptal tekrarı yeni sahibini iptal etmemeli.");
Kontrol((await istemci.AyirAsync(geri1.IslemId,"M")).Durum=="Iptal","Eski GUID yeniden sahiplik kazanamaz.");
await Hata(()=>istemci.OnaylaAsync(geri1.IslemId),409);
using(var tasiyici=new YanitiKaybedenTasiyici())
using(var kayipHttp=new HttpClient(tasiyici){BaseAddress=http.BaseAddress})
{
 var kayip=new EtiketApiIstemcisi(kayipHttp);
 try{await kayip.IptalEtAsync(geri2.IslemId);Kontrol(false,"İptal yanıt kaybı yakalanmalı.");}
 catch(IstemciHatasi h){Kontrol(h.SonucBelirsiz,"İptal yanıt kaybı aynı GUID ile sorgulanmalı.");}
 Kontrol((await istemci.RezervasyonBulAsync(geri2.IslemId)).Durum=="Iptal","Sunucuda tamamlanmış iptal sorgulanabilmeli.");
}
var geri3=await istemci.AyirAsync(Guid.NewGuid(),"M");Kontrol(geri3.DosyaAdi==geri1.DosyaAdi,"İptal yanıtı kaybolsa da sunucu tek yeni sahip atamalı.");
await istemci.OnaylaAsync(geri3.IslemId);
var sonraki=await istemci.AyirAsync(Guid.NewGuid(),"M");Kontrol(sonraki.DosyaAdi!=geri3.DosyaAdi,"Onaylı numara tekrar dağıtılamaz.");
var tohum=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>istemci.AyirAsync(Guid.NewGuid(),"P")));
await Task.WhenAll(tohum.Select(r=>istemci.IptalEtAsync(r.IslemId)));
var yenidenAlinan=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>istemci.AyirAsync(Guid.NewGuid(),"P")));
Kontrol(yenidenAlinan.Select(r=>r.DosyaAdi).Distinct().Count()==12,"Geri dönüş havuzunda eşzamanlı talepler benzersiz olmalı.");
Kontrol(tohum.Select(r=>r.DosyaAdi).Order().SequenceEqual(yenidenAlinan.Select(r=>r.DosyaAdi).Order()),"İptal edilmiş 12 numara yeni sayaca geçmeden kullanılmalı.");
await Task.WhenAll(yenidenAlinan.Select(r=>istemci.OnaylaAsync(r.IslemId)));
var yenidenAyrilanOnaylar=await Task.WhenAll(yenidenAlinan.Select(r=>istemci.OnaylaAsync(r.IslemId)));
Kontrol(yenidenAyrilanOnaylar.Select(r=>r.Id).Distinct().Count()==12,"Geri kullanılan adların onay tekrarları tek belge üretmeli.");
var bekleyen=await istemci.AyirAsync(Guid.NewGuid(),"P");var farkli=await istemci.AyirAsync(Guid.NewGuid(),"P");
Kontrol(bekleyen.DosyaAdi!=farkli.DosyaAdi,"İptal edilmemiş belirsiz/bekleyen rezervasyon dağıtılamaz.");
// API restart sırasında boş numaranın kullanılabilirliği de kalıcı olmalı.
await istemci.IptalEtAsync(sonraki.IslemId);
var yenidenKullanilacak=sonraki.DosyaAdi;
if(!sqlModu)
{
 var profil=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
 var exe=Environment.GetEnvironmentVariable("ETIKET_NET48_HTTP_TEST_EXE")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Etiket.SolidWorks.Testler/bin",profil,"net48","Etiket.SolidWorks.Testler.exe"));
 if(File.Exists(exe))
 {
  var baslat=new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  baslat.ArgumentList.Add("--http");baslat.ArgumentList.Add(http.BaseAddress!.ToString());
  baslat.Environment.Remove("ETIKET_EKLENTI_TEST_RAPORU");
  using var surec=System.Diagnostics.Process.Start(baslat)!;
  var cikti=surec.StandardOutput.ReadToEndAsync();var hata=surec.StandardError.ReadToEndAsync();
  using var sureSiniri=new CancellationTokenSource(TimeSpan.FromSeconds(60));
  try{await surec.WaitForExitAsync(sureSiniri.Token);}catch{if(!surec.HasExited)surec.Kill();throw;}
  var metin=await cikti;var hataMetni=await hata;
  Kontrol(surec.ExitCode==0,"Net48 eklenti HTTP testi: "+hataMetni);Console.Write(metin);
 }
 else Console.WriteLine("Net48 eklenti HTTP kontrolü bu derlemede yok; tam çözümü derleyin.");
}
// Yeni API örneği: SQL modunda kalıcı SQL, varsayılanda paylaşılan test deposu.
await app.StopAsync();await using var yeniden=Yeni();await yeniden.StartAsync();
using var http2=new HttpClient{BaseAddress=new Uri(yeniden.Urls.Single())};var istemci2=new EtiketApiIstemcisi(http2);
Kontrol((await istemci2.AyirAsync(id,"P")).DosyaAdi==p.DosyaAdi,"API yeniden kurulunca aynı işlem korunmalı.");
Kontrol((await istemci2.OnaylaAsync(id))==b,"API yeniden kurulunca onay tekrarı korunmalı.");
var yeni=await istemci2.AyirAsync(Guid.NewGuid(),"M");Kontrol(yeni.DosyaAdi==yenidenKullanilacak,"API restart sonrası kullanılmayan numara tekrar dağıtılmalı.");
await yeniden.StopAsync();
if(!sqlModu)
{
 await using var kapali=ApiKurulumu.Olustur([],kurucu=>{kurucu.Logging.ClearProviders();kurucu.WebHost.UseUrls("http://127.0.0.1:0");kurucu.Services.AddSingleton<IBelgeDeposu>(new SqlBelgeDeposu("Server=tcp:127.0.0.1,1;Initial Catalog=ErisilemeyenTest;Integrated Security=True;Encrypt=True;Connect Timeout=1;ConnectRetryCount=0"));});
 await kapali.StartAsync();using var hc=new HttpClient{BaseAddress=new Uri(kapali.Urls.Single())};
 using var yanit=await hc.PostAsJsonAsync("api/rezervasyonlar",new{islemId=Guid.NewGuid(),belgeTuru="P"});
 Kontrol(yanit.StatusCode==HttpStatusCode.ServiceUnavailable,"Gerçek SqlClient bağlantı hatası 503 olmalı.");
 Kontrol(!(await yanit.Content.ReadAsStringAsync()).Contains("ErisilemeyenTest"),"Bağlantı ayarı sızmamalı.");await kapali.StopAsync();
}

if(sqlModu)
{
 await using var c=new SqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__MerkeziVeritabani"));await c.OpenAsync();
 var kok=new DirectoryInfo(AppContext.BaseDirectory);
 while(kok is not null&&!File.Exists(Path.Combine(kok.FullName,"outputs","sql","004-dosya-adi-rezervasyon.sql")))kok=kok.Parent;
 await using var q=new SqlCommand(await File.ReadAllTextAsync(Path.Combine(kok!.FullName,"outputs","sql","005-numara-geri-kullanim.sql")),c){CommandTimeout=60};await q.ExecuteNonQueryAsync();
 var depo=new SqlBelgeDeposu(c.ConnectionString);
 Kontrol((await depo.OnaylaAsync(id,default)).Id==b.Id,"Yeniden kurulum mevcut belgeyi korumalı.");
 Kontrol((await depo.AyirAsync(new RezervasyonIstegi(Guid.NewGuid(),"M"),default)).DosyaAdi!=yeni.DosyaAdi,"Yeniden kurulum aktif numarayı yeniden dağıtmamalı.");
}
var rapor=$"GEÇTİ: {sayac} kontrol. Mod: {(sqlModu?"Gerçek SQL + rezervasyon HTTP":"Bellek deposu + rezervasyon HTTP + gerçek SQL bağlantı hatası")}";
Console.WriteLine(rapor);
if(Environment.GetEnvironmentVariable("ETIKET_TEST_RAPORU") is {Length:>0} yol)await File.WriteAllTextAsync(yol,rapor+Environment.NewLine+DateTimeOffset.UtcNow.ToString("O"));

sealed class BellekDeposu:IBelgeDeposu
{
 readonly object kilit=new();readonly Dictionary<Guid,Rezervasyon> rezervasyonlar=new();readonly List<Belge> belgeler=[];
 long parca,montaj;
 public Task<Rezervasyon> AyirAsync(RezervasyonIstegi i,CancellationToken t){lock(kilit){if(rezervasyonlar.TryGetValue(i.IslemId,out var eski)){if(eski.DosyaAdi[0].ToString()!=i.BelgeTuru)throw new IslemCakismasiHatasi("Farklı tür.");return Task.FromResult(eski);}var ad=rezervasyonlar.Values.Where(r=>r.Durum=="Iptal"&&r.DosyaAdi[0].ToString()==i.BelgeTuru&&!rezervasyonlar.Values.Any(a=>a.DosyaAdi==r.DosyaAdi&&a.Durum!="Iptal")&&!belgeler.Any(b=>b.DosyaAdi==r.DosyaAdi)).OrderBy(r=>long.Parse(r.DosyaAdi.Substring(1,r.DosyaAdi.Length-8))).Select(r=>r.DosyaAdi).FirstOrDefault()??(i.BelgeTuru+(i.BelgeTuru=="P"?++parca:++montaj).ToString("D5")+(i.BelgeTuru=="P"?".SLDPRT":".SLDASM"));var yeni=new Rezervasyon(i.IslemId,ad,"Ayrildi",DateTime.UtcNow);rezervasyonlar.Add(i.IslemId,yeni);return Task.FromResult(yeni);}}
 public Task<Rezervasyon?> RezervasyonBulAsync(Guid id,CancellationToken t){lock(kilit)return Task.FromResult(rezervasyonlar.GetValueOrDefault(id));}
 Rezervasyon Bul(Guid id)=>rezervasyonlar.GetValueOrDefault(id)??throw new KayitBulunamadiHatasi();
 public Task<Belge> OnaylaAsync(Guid id,CancellationToken t){lock(kilit){var r=Bul(id);if(r.Durum=="Iptal")throw new IslemCakismasiHatasi("İptal.");if(r.Durum=="Onaylandi")return Task.FromResult(belgeler.Single(b=>b.DosyaAdi==r.DosyaAdi));var b=new Belge(belgeler.Count+1,r.DosyaAdi,DateTime.UtcNow);belgeler.Add(b);rezervasyonlar[id]=r with{Durum="Onaylandi"};return Task.FromResult(b);}}
 public Task<Rezervasyon> IptalEtAsync(Guid id,CancellationToken t){lock(kilit){var r=Bul(id);if(r.Durum=="Onaylandi")throw new IslemCakismasiHatasi("Onaylı.");r=r with{Durum="Iptal"};rezervasyonlar[id]=r;return Task.FromResult(r);}}
 public Task<Belge?> BulAsync(string ad,CancellationToken t){lock(kilit)return Task.FromResult(belgeler.SingleOrDefault(b=>b.DosyaAdi==ad));}
 public Task<BelgeListesi> ListeleAsync(int b,int a,string? arama,string? tur,CancellationToken t){lock(kilit){var filtreli=belgeler.Where(x=>(arama is null||x.DosyaAdi.Contains(arama,StringComparison.OrdinalIgnoreCase))&&(tur is null||x.DosyaAdi.StartsWith(tur,StringComparison.Ordinal))).ToArray();return Task.FromResult(new BelgeListesi(filtreli.Skip(b).Take(a).ToArray(),b,a,filtreli.LongLength));}}
 public Task SaglikKontroluAsync(CancellationToken t)=>Task.CompletedTask;
}

sealed class YanitiKaybedenTasiyici:DelegatingHandler
{
 public int YazmaSayisi {get;private set;}
 public YanitiKaybedenTasiyici():base(new HttpClientHandler()){ }
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek,CancellationToken t)
 {
  var yanit=await base.SendAsync(istek,t);
  if(istek.Method==HttpMethod.Post){YazmaSayisi++;yanit.Dispose();throw new HttpRequestException("Test yanıt kaybı.");}
  return yanit;
 }
}
