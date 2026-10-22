using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;
namespace Etiket.SolidWorks
{
 public sealed class HttpAdServisi:IAdServisi,IDisposable
 {
  readonly HttpClient http;readonly JavaScriptSerializer json=new JavaScriptSerializer();
  public HttpAdServisi(Uri adres):this(adres,new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false}){}
  public HttpAdServisi(Uri adres,HttpMessageHandler tasiyici){http=new HttpClient(tasiyici){BaseAddress=adres,Timeout=TimeSpan.FromSeconds(10)};}
  Dictionary<string,object> Gonder(string yol,object govde)
  {
   using(var istek=new HttpRequestMessage(HttpMethod.Post,yol))
   {
    if(govde!=null)istek.Content=new StringContent(json.Serialize(govde),Encoding.UTF8,"application/json");
    try
    {
     using(var yanit=http.SendAsync(istek).GetAwaiter().GetResult())
     {
      if(!yanit.IsSuccessStatusCode)throw new InvalidOperationException("Merkezi servis işlemi tamamlayamadı (HTTP "+(int)yanit.StatusCode+"). Aynı işlem kimliği korunuyor.");
      return json.Deserialize<Dictionary<string,object>>(yanit.Content.ReadAsStringAsync().GetAwaiter().GetResult());
     }
    }
    catch(Exception h) when(h is HttpRequestException||h is System.Threading.Tasks.TaskCanceledException)
    {
     var ayrinti=h is System.Threading.Tasks.TaskCanceledException?"10 saniye içinde yanıt alınamadı.":h.GetBaseException().Message;
     throw new IOException("Merkezi servise erişilemiyor: "+http.BaseAddress+". "+ayrinti+" Sonuç belirsiz olabilir; aynı işlem kimliği korunuyor.",h);
    }
   }
  }
  public AyrilanAd Ayir(Guid id,string tur)
  {var r=Gonder("api/rezervasyonlar",new{islemId=id,belgeTuru=tur});return new AyrilanAd{IslemId=Guid.Parse((string)r["islemId"]),DosyaAdi=(string)r["dosyaAdi"],Durum=(string)r["durum"]};}
  public void Onayla(Guid id,string beklenenAd)
  {var r=Gonder("api/rezervasyonlar/"+id+"/onay",null);if((string)r["dosyaAdi"]!=beklenenAd)throw new InvalidDataException("Onay sonucu beklenen dosyayla uyuşmuyor.");}
  public void IptalEt(Guid id)
  {var r=Gonder("api/rezervasyonlar/"+id+"/iptal",null);if((string)r["durum"]!="Iptal"||Guid.Parse((string)r["islemId"])!=id)throw new InvalidDataException("İptal sonucu uyuşmuyor.");}
  public void Dispose()=>http.Dispose();
 }
}
