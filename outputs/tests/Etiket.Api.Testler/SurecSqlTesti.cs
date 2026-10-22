// Tarihsel etiket-kolonu modeli: yeni rezervasyon akışında kullanılmaz.
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Etiket.TestIstemcisi;
using Microsoft.Data.SqlClient;

namespace Etiket.Api.Testler;

internal static class SurecSqlTesti
{
    public static async Task CalistirAsync()
    {
        var baglantiMetni = Environment.GetEnvironmentVariable("ConnectionStrings__MerkeziVeritabani");
        if (string.IsNullOrWhiteSpace(baglantiMetni)) throw new InvalidOperationException("Test SQL bağlantı ayarı eksik.");
        var ayarlar = new SqlConnectionStringBuilder(baglantiMetni);
        if (!ayarlar.InitialCatalog.StartsWith("SolidWorksAutomation_ApiTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Süreç testi yalnızca ayrı SolidWorksAutomation_ApiTest_ veritabanında çalışır.");
        var gunluk = new List<string>();
        var kontrolSayisi = 0;
        Process? api = null;
        void Kontrol(bool sonuc, string mesaj)
        {
            if (!sonuc) throw new InvalidOperationException("TEST BAŞARISIZ: " + mesaj);
            kontrolSayisi++;
        }
        void Bilgi(string metin) { gunluk.Add(metin); Console.WriteLine(metin); }
        async Task Kaydet(string sonuc)
        {
            var rapor = Environment.GetEnvironmentVariable("ETIKET_TEST_RAPORU");
            if (!string.IsNullOrWhiteSpace(rapor))
                await File.WriteAllLinesAsync(rapor, new[] { sonuc, DateTimeOffset.UtcNow.ToString("O") }.Concat(gunluk));
        }
        using var sure = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var iptal = sure.Token;
        await using var denetim = new SqlConnection(baglantiMetni);
        await denetim.OpenAsync(iptal);
        async Task<long> Sayi(string sql)
        {
            await using var komut = new SqlCommand(sql, denetim) { CommandTimeout = 5 };
            return Convert.ToInt64(await komut.ExecuteScalarAsync(iptal));
        }
        Kontrol(await Sayi("SELECT COUNT_BIG(*) FROM dbo.Documents;") == 0, "Test başlangıcında veritabanı boş olmalı.");
        Kontrol(await Sayi("SELECT CASE WHEN IS_SRVROLEMEMBER('sysadmin')=1 THEN 1 ELSE 0 END;") == 1,
            "Bağlantı kesme testi yerel geliştirme yöneticisi yetkisi gerektirir.");
        // Bu yetki yalnızca test aracına aittir; uygulama/istemci gereksinimi değildir.
        using var portSecimi = new TcpListener(IPAddress.Loopback, 0);
        portSecimi.Start();
        var port = ((IPEndPoint)portSecimi.LocalEndpoint).Port;
        portSecimi.Stop();
        var adres = new Uri($"http://127.0.0.1:{port}/");
        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        { BaseAddress = adres, Timeout = TimeSpan.FromSeconds(15) };
        var istemci = new EtiketApiIstemcisi(http);
        var uygulamaAdi = "Pronom_SurecTest_" + Guid.NewGuid().ToString("N");
        ayarlar.ApplicationName = uygulamaAdi;
        ayarlar.ConnectRetryCount = 0;
        var apiDosyasi = typeof(ApiKurulumu).Assembly.Location;
        Kontrol(File.Exists(Path.ChangeExtension(apiDosyasi, ".runtimeconfig.json")), "API çalışma dosyası bulunmalı.");
        Process Baslat()
        {
            var baslatma = new ProcessStartInfo("dotnet")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(apiDosyasi)! };
            baslatma.ArgumentList.Add(apiDosyasi);
            baslatma.ArgumentList.Add("--urls");
            baslatma.ArgumentList.Add(adres.ToString());
            baslatma.Environment["ConnectionStrings__MerkeziVeritabani"] = ayarlar.ConnectionString;
            baslatma.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            var baslatilan = Process.Start(baslatma) ?? throw new InvalidOperationException("Test API süreci başlatılamadı.");
            // Çıktıları tüket: dolu çıktı tamponu API'nin çalışmasını engellemesin.
            baslatilan.OutputDataReceived += (_, _) => { };
            baslatilan.ErrorDataReceived += (_, _) => { };
            baslatilan.BeginOutputReadLine();
            baslatilan.BeginErrorReadLine();
            return baslatilan;
        }
        async Task HazirBekle(Process surec)
        {
            var zaman = Stopwatch.StartNew();
            while (zaman.Elapsed < TimeSpan.FromSeconds(20))
            {
                if (surec.HasExited) throw new InvalidOperationException("Test API süreci başlatılırken kapandı.");
                try
                {
                    if ((await istemci.SaglikAsync(iptal)).Durum == "Hazır") return;
                }
                catch (IstemciHatasi) when (!iptal.IsCancellationRequested) { }
                await Task.Delay(100, iptal); // Yalnızca salt okunur sağlık sorgusu tekrar edilir.
            }
            throw new InvalidOperationException("Test API süreci hazır duruma gelmedi.");
        }
        static async Task Durdur(Process surec)
        {
            if (!surec.HasExited) surec.Kill(entireProcessTree: true);
            using var zaman = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await surec.WaitForExitAsync(zaman.Token);
        }
        try
        {
            api = Baslat();
            await HazirBekle(api);
            var ilkPid = api.Id;
            var ilkParca = await istemci.OlusturAsync("P", "Surec_Mil.SLDPRT", iptal);
            var ilkMontaj = await istemci.OlusturAsync("M", "Surec_Montaj.SLDASM", iptal);
            Kontrol(ilkParca.Id == 1 && ilkParca.Etiket == "P00001", "İlk parça P00001/ID1 olmalı.");
            Kontrol(ilkMontaj.Id == 2 && ilkMontaj.Etiket == "M00001", "İlk montaj M00001/ID2 olmalı.");
            await Durdur(api);
            Kontrol(api.HasExited, "İlk API işletim sistemi süreci gerçekten kapanmalı.");
            try { await istemci.SaglikAsync(iptal); Kontrol(false, "API kapalıyken sağlık başarı göstermemeli."); }
            catch (IstemciHatasi hata) { Kontrol(!hata.SonucBelirsiz, "Kapalı API okuma hatası kontrollü olmalı."); }
            api.Dispose();
            api = null;
            api = Baslat();
            await HazirBekle(api);
            Kontrol(api.Id != ilkPid, "İkinci API farklı bir işletim sistemi süreci olmalı.");
            Kontrol(await istemci.BulAsync(ilkParca.Etiket, iptal) == ilkParca, "Önceki parça kaydı korunmalı.");
            Kontrol(await istemci.BulAsync(ilkMontaj.Etiket, iptal) == ilkMontaj, "Önceki montaj kaydı korunmalı.");
            var ikinciParca = await istemci.OlusturAsync("P", "Surec_Mil.SLDPRT", iptal);
            var ikinciMontaj = await istemci.OlusturAsync("M", "Surec_Montaj.SLDASM", iptal);
            Kontrol(ikinciParca.Id == 3 && ikinciParca.Etiket == "P00002", "Yeniden başlatma sonrası parça sayacı devam etmeli.");
            Kontrol(ikinciMontaj.Id == 4 && ikinciMontaj.Etiket == "M00002", "Yeniden başlatma sonrası montaj sayacı devam etmeli.");
            Bilgi($"API tam süreç yeniden başlatma geçti. İlk PID: {ilkPid}, ikinci PID: {api.Id}; P00002/M00002, son ID4.");

            // Sadece bu test DB'sinde INSERT'i beklet: yazma transaction'ı başladıktan sonra
            // GUID uygulama adı + test DB + bu API'nin PID'si ile doğrulanan tek SQL oturumunu kes.
            await using var kilitBaglantisi = new SqlConnection(baglantiMetni);
            await kilitBaglantisi.OpenAsync(iptal);
            await using var kilit = (SqlTransaction)await kilitBaglantisi.BeginTransactionAsync(iptal);
            await using (var kilitle = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.Documents WITH (TABLOCKX, HOLDLOCK);", kilitBaglantisi, kilit))
                await kilitle.ExecuteScalarAsync(iptal);
            int kilitSpid;
            await using (var spid = new SqlCommand("SELECT @@SPID;", kilitBaglantisi, kilit))
                kilitSpid = Convert.ToInt32(await spid.ExecuteScalarAsync(iptal));
            var bekleyenYazma = istemci.OlusturAsync("P", "Kesilen_Yazma.SLDPRT", iptal);
            try
            {
                int? hedefSpid = null;
                var zaman = Stopwatch.StartNew();
                while (zaman.Elapsed < TimeSpan.FromSeconds(8) && !bekleyenYazma.IsCompleted)
                {
                    await using var sorgu = new SqlCommand("""
                        SELECT r.session_id FROM sys.dm_exec_requests r
                        JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id
                        WHERE s.program_name = @Uygulama AND s.host_process_id = @Surec
                          AND r.database_id = DB_ID() AND r.blocking_session_id = @Kilit
                          AND r.transaction_id <> 0 AND s.is_user_process = 1;
                        """, denetim) { CommandTimeout = 5 };
                    sorgu.Parameters.AddWithValue("@Uygulama", uygulamaAdi);
                    sorgu.Parameters.AddWithValue("@Surec", api.Id);
                    sorgu.Parameters.AddWithValue("@Kilit", kilitSpid);
                    await using var satir = await sorgu.ExecuteReaderAsync(iptal);
                    if (await satir.ReadAsync(iptal))
                    {
                        hedefSpid = satir.GetInt16(0);
                        Kontrol(!await satir.ReadAsync(iptal), "Kesilecek tek test oturumu bulunmalı.");
                        break;
                    }
                    await Task.Delay(100, iptal);
                }
                Kontrol(hedefSpid is > 0, "Yazma transaction'ının SQL'de gerçekten beklediği doğrulanmalı.");
                // KILL deyimi parametre kabul etmez. SPID SQL'den int olarak alınır ve sayısal yazılır.
                await using var kes = new SqlCommand("KILL " + hedefSpid!.Value.ToString(CultureInfo.InvariantCulture) + ";", denetim);
                await kes.ExecuteNonQueryAsync(iptal);
                try { await bekleyenYazma; Kontrol(false, "Kesilen SQL yazması başarı olarak gösterilmemeli."); }
                catch (IstemciHatasi hata)
                {
                    Kontrol(hata.DurumKodu == 503 && hata.SonucBelirsiz, "SQL yazma kesintisi 503 ve belirsiz sonuç olarak gösterilmeli.");
                }
                Bilgi($"İşlem sırasında SQL bağlantısı kesme geçti. Test API oturumu SPID {hedefSpid}; istemci kontrollü 503 aldı.");
            }
            finally { await kilit.RollbackAsync(CancellationToken.None); }
            Kontrol(await Sayi("SELECT COUNT_BIG(*) FROM dbo.Documents;") == 4, "Kesilen yazma kalıcı kayıt oluşturmamalı; otomatik tekrar yapılmamalı.");
            Kontrol(await Sayi("SELECT COUNT_BIG(*) FROM dbo.Documents WHERE DosyaAdi=N'Kesilen_Yazma.SLDPRT';") == 0, "Yarım kalan belge kaydedilmemeli.");
            Kontrol((await istemci.SaglikAsync(iptal)).Durum == "Hazır", "Kesinti sonrası API/SQL bağlantısı toparlanmalı.");
            var sonParca = await istemci.OlusturAsync("P", "Kesinti_Sonrasi.SLDPRT", iptal);
            Kontrol(sonParca.Id > ikinciMontaj.Id && long.Parse(sonParca.Etiket[1..], CultureInfo.InvariantCulture) > 2,
                "Kesinti sonrası daha büyük, benzersiz ID ve parça etiketi verilmeli.");
            var sonMontaj = await istemci.OlusturAsync("M", "Kesinti_Sonrasi.SLDASM", iptal);
            Kontrol(sonMontaj.Etiket == "M00003", "Parça kesintisi montaj sayacını etkilememeli.");
            var tumu = await istemci.ListeleAsync(iptal: iptal);
            Kontrol(tumu.Belgeler.Count == 6 && tumu.Belgeler.Select(b => b.Etiket).Distinct().Count() == 6
                && tumu.Belgeler.Select(b => b.Id).Distinct().Count() == 6, "Son 6 kayıt benzersiz olmalı.");
            Bilgi($"Kesinti sonrası toparlanma geçti. Yeni etiketler: {sonParca.Etiket}/{sonMontaj.Etiket}; son ID: {sonMontaj.Id}; toplam 6 benzersiz kayıt.");
            var sonuc = $"GEÇTİ: {kontrolSayisi} kontrol. Gerçek API süreç yeniden başlatma + işlem sırasında gerçek SQL bağlantı kesintisi.";
            Bilgi(sonuc);
            await Kaydet(sonuc);
        }
        catch (Exception hata)
        {
            await Kaydet("BAŞARISIZ: " + hata.GetType().Name + (hata is InvalidOperationException ? ": " + hata.Message : ""));
            throw;
        }
        finally
        {
            if (api is not null) { await Durdur(api); api.Dispose(); }
        }
    }
}
