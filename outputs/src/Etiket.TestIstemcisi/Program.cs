using Etiket.TestIstemcisi;
Console.OutputEncoding=System.Text.Encoding.UTF8;
try
{
    var ayar=Path.Combine(AppContext.BaseDirectory,"ApiAdresi.txt");
    var metin=Environment.GetEnvironmentVariable("ETIKET_API_ADRESI")??(File.Exists(ayar)?File.ReadAllText(ayar).Trim():"http://localhost:5080/");
    if(!Uri.TryCreate(metin.TrimEnd('/')+"/",UriKind.Absolute,out var adres)||adres.Scheme is not("http" or "https")||!string.IsNullOrEmpty(adres.UserInfo)||!string.IsNullOrEmpty(adres.Query)||!string.IsNullOrEmpty(adres.Fragment))
    {Console.Error.WriteLine("Geçerli bir HTTP/HTTPS API adresi girin.");return 2;}
    using var http=new HttpClient(new SocketsHttpHandler{AllowAutoRedirect=false,PooledConnectionLifetime=TimeSpan.FromMinutes(2)}){BaseAddress=adres,Timeout=TimeSpan.FromSeconds(15)};
    var istemci=new EtiketApiIstemcisi(http);
    void Yaz(BelgeKaydi b)=>Console.WriteLine($"ID: {b.Id} | Dosya: {b.DosyaAdi} | Tarih (UTC): {b.Tarih:O}");
    switch(args)
    {
        case ["--saglik"]:var s=await istemci.SaglikAsync();Console.WriteLine($"Servis: {s.Durum}, Veritabanı: {s.Veritabani}");break;
        case ["--liste"]:foreach(var b in (await istemci.ListeleAsync()).Belgeler)Yaz(b);break;
        case ["--bul",var ad]:Yaz(await istemci.BulAsync(ad));break;
        default:Console.WriteLine("Web arayüzünü API adresinde açın. Tanılama komutları: --saglik | --liste | --bul P00001.SLDPRT");return args.Length==0?0:2;
    }
    return 0;
}
catch(IstemciHatasi h){Console.Error.WriteLine(h.Message);return 1;}
catch(Exception h) when(h is IOException or UnauthorizedAccessException){Console.Error.WriteLine("Yerel ayar dosyası okunamadı.");return 2;}
