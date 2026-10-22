using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Etiket.Api;

public static class ApiKurulumu
{
    public static WebApplication Olustur(string[] args, Action<WebApplicationBuilder>? testKurulumu = null)
    {
        var kurucu = WebApplication.CreateBuilder(new WebApplicationOptions { Args=args, ContentRootPath=AppContext.BaseDirectory });
        // Yerel dosya Git dışında; environment variables ve user secrets daha öncelikli.
        kurucu.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        if (kurucu.Environment.IsDevelopment())
            kurucu.Configuration.AddUserSecrets(typeof(ApiKurulumu).Assembly, optional: true);
        kurucu.Configuration.AddEnvironmentVariables();
        kurucu.Configuration.AddCommandLine(args);

        var baglanti = kurucu.Configuration.GetConnectionString("MerkeziVeritabani");
        if (!string.IsNullOrWhiteSpace(baglanti))
        {
            // Yazma işlemlerinde otomatik yeniden deneme yapılmaz.
            var ayarlar = new SqlConnectionStringBuilder(baglanti) { ConnectRetryCount = 0 };
            kurucu.Services.AddSingleton<IBelgeDeposu>(new SqlBelgeDeposu(ayarlar.ConnectionString));
        }
        kurucu.Services.AddProblemDetails();
        kurucu.Services.Configure<RouteHandlerOptions>(ayarlar => ayarlar.ThrowOnBadRequest = true);
        testKurulumu?.Invoke(kurucu);
        if (!kurucu.Services.Any(tanim => tanim.ServiceType == typeof(IBelgeDeposu)))
            throw new InvalidOperationException("MerkeziVeritabani bağlantı ayarı eksik. Yerel ayar dosyası, user secrets veya environment variable kullanın.");

        var uygulama = kurucu.Build();
        uygulama.UseExceptionHandler(hataUygulamasi => hataUygulamasi.Run(async baglam =>
        {
            var hata = baglam.Features.Get<IExceptionHandlerFeature>()?.Error;
            var (durum, baslik, aciklama) = hata switch
            {
                BadHttpRequestException baglamaHatasi => (baglamaHatasi.StatusCode, "Geçersiz istek", "JSON gövdesini, içerik türünü ve sorgu parametrelerini kontrol edin."),
                BelgeIstegiHatasi => (400, "Geçersiz belge", "Belge türü veya dosya adı geçersiz."),
                IslemCakismasiHatasi => (409, "İşlem çakışması", "İşlem türü veya durumu istekle uyuşmuyor."),
                KayitBulunamadiHatasi => (404, "İşlem bulunamadı", "İşlem kimliğini kontrol edin."),
                VeritabaniErisimHatasi => (503, "Veritabanı kullanılamıyor", "İşlemin sonucunu doğrulayın; yeni belge isteğini otomatik tekrarlamayın."),
                _ => (500, "İşlem tamamlanamadı", "Sunucuda beklenmeyen bir hata oluştu.")
            };
            // İstek gövdesi, dosya adı ve connection string loglanmaz.
            uygulama.Logger.LogError("İstek başarısız. Durum: {Durum}, HataTuru: {HataTuru}, IzlemeNo: {IzlemeNo}",
                durum, hata?.GetType().Name, baglam.TraceIdentifier);
            await Results.Problem(statusCode: durum, title: baslik, detail: aciklama,
                extensions: new Dictionary<string, object?> { ["izlemeNo"] = baglam.TraceIdentifier })
                .ExecuteAsync(baglam);
        }));
        uygulama.UseStatusCodePages(async durum =>
        {
            await Results.Problem(statusCode: durum.HttpContext.Response.StatusCode,
                title: "İstek tamamlanamadı").ExecuteAsync(durum.HttpContext);
        });


        uygulama.UseDefaultFiles();
        uygulama.UseStaticFiles();
        uygulama.MapPost("/api/rezervasyonlar",async(RezervasyonIstegi istek,IBelgeDeposu depo,CancellationToken t)=>
        {
            if(istek.IslemId==Guid.Empty || istek.BelgeTuru is not ("P" or "M"))
                return Results.Problem(statusCode:400,title:"İşlem kimliği ve P/M belge türü gereklidir.");
            return Results.Ok(await depo.AyirAsync(istek,t));
        });
        uygulama.MapGet("/api/rezervasyonlar/{islemId:guid}",async(Guid islemId,IBelgeDeposu depo,CancellationToken t)=>
        {
            var kayit=await depo.RezervasyonBulAsync(islemId,t);
            return kayit is null ? Results.NotFound() : Results.Ok(kayit);
        });
        uygulama.MapPost("/api/rezervasyonlar/{islemId:guid}/onay",async(Guid islemId,IBelgeDeposu depo,CancellationToken t)=>
            Results.Ok(await depo.OnaylaAsync(islemId,t)));
        uygulama.MapPost("/api/rezervasyonlar/{islemId:guid}/iptal",async(Guid islemId,IBelgeDeposu depo,CancellationToken t)=>
            Results.Ok(await depo.IptalEtAsync(islemId,t)));
        uygulama.MapGet("/api/documents/{dosyaAdi}",async(string dosyaAdi,IBelgeDeposu depo,CancellationToken t)=>
        {
            if(!BelgeDogrulama.DosyaAdiGecerliMi(dosyaAdi))return Results.Problem(statusCode:400,title:"Geçersiz dosya adı");
            var belge=await depo.BulAsync(dosyaAdi,t);
            return belge is null ? Results.NotFound() : Results.Ok(belge);
        });
        uygulama.MapGet("/api/documents", async (int? baslangic, int? adet, string? arama, string? tur, IBelgeDeposu depo, CancellationToken iptal) =>
        {
            var ilk = baslangic ?? 0;
            var sayi = adet ?? 100;
            if (ilk < 0 || sayi is < 1 or > 500)
                return Results.Problem(statusCode: 400, title: "Geçersiz sayfalama",
                    detail: "başlangıç 0 veya üzeri, adet 1–500 arasında olmalıdır.");
            arama = string.IsNullOrWhiteSpace(arama) ? null : arama.Trim();
            tur = string.IsNullOrWhiteSpace(tur) ? null : tur.Trim().ToUpperInvariant();
            if (arama?.Length > 64 || tur is not (null or "P" or "M"))
                return Results.Problem(statusCode: 400, title: "Geçersiz filtre",
                    detail: "Arama en fazla 64 karakter, belge türü P veya M olmalıdır.");
            return Results.Ok(await depo.ListeleAsync(ilk, sayi, arama, tur, iptal));
        });

        uygulama.MapGet("/api/health", async (IBelgeDeposu depo, CancellationToken iptal) =>
        {
            await depo.SaglikKontroluAsync(iptal);
            return Results.Ok(new { durum = "Hazır", veritabani = "Bağlı" });
        });
        return uygulama;
    }
}
