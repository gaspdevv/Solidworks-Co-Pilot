// Tarihsel etiket-kolonu modeli: yeni rezervasyon akışında kullanılmaz.
using System.Net;
using System.Net.Http.Json;
using Etiket.TestIstemcisi;

namespace Etiket.Api.Testler;

internal static class IstemciTestleri
{
    public static async Task CalistirAsync(HttpClient http, Action<bool, string> kontrol)
    {
        var istemci = new EtiketApiIstemcisi(http);
        var saglik = await istemci.SaglikAsync();
        kontrol(saglik.Durum == "Hazır", "C# istemcisi sağlık yanıtını okumalı.");
        var eski = await istemci.BulAsync("P00001");
        kontrol(eski.Etiket == "P00001" && eski.Tarih.Kind == DateTimeKind.Utc, "C# istemcisi etiket ve UTC tarihini okumalı.");
        var liste = await istemci.ListeleAsync(1, 1);
        kontrol(liste.Belgeler.Count == 1 && liste.Belgeler[0].Id == 2, "C# istemcisi sayfalama göndermeli.");
        foreach (var (gorev, kod) in new (Func<Task>, int)[]
        {
            (() => istemci.BulAsync("P99999"), 404),
            (() => istemci.OlusturAsync("X", "Hatali.SLDPRT"), 400),
            (() => istemci.ListeleAsync(-1), 400)
        })
        {
            try { await gorev(); kontrol(false, "İstemci hata durumunda başarı göstermemeli."); }
            catch (IstemciHatasi hata) { kontrol(hata.DurumKodu == kod && !hata.SonucBelirsiz, "C# istemcisi kontrollü HTTP hatasını göstermeli."); }
        }
        // Yanıt kaybında kaydı tekrarlamama: bir POST'tan sonra başarısızlık simüle edilir.
        foreach (var mod in new[] { "baglanti", "zaman", "json", "503" })
        {
            using var tasiyici = new HataTasiyicisi(mod);
            using var kopan = new HttpClient(tasiyici) { BaseAddress = new Uri("http://test.local/") };
            try { await new EtiketApiIstemcisi(kopan).OlusturAsync("P", "Mil.SLDPRT"); kontrol(false, "Hata yakalanmalı."); }
            catch (IstemciHatasi hata)
            {
                kontrol(hata.SonucBelirsiz, "Yazma yanıtı alınamazsa sonuç belirsiz olmalı.");
                kontrol(tasiyici.IstekSayisi == 1, "Belirsiz POST otomatik tekrarlanmamalı.");
                kontrol(!hata.Message.Contains("GIZLI"), "Taşıyıcı ayrıntısı kullanıcıya sızmamalı.");
            }
        }
        using var okumaTasiyicisi = new HataTasiyicisi("baglanti");
        using var okuma = new HttpClient(okumaTasiyicisi) { BaseAddress = new Uri("http://test.local/") };
        try { await new EtiketApiIstemcisi(okuma).SaglikAsync(); kontrol(false, "Bağlantı hatası yakalanmalı."); }
        catch (IstemciHatasi hata) { kontrol(!hata.SonucBelirsiz, "Okuma isteğinde kayıt oluştu uyarısı verilmemeli."); }
    }

    public static async Task MenuTestiAsync(HttpClient http, Action<bool, string> kontrol)
    {
        var ayarlar = new System.Diagnostics.ProcessStartInfo("dotnet")
        { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        ayarlar.ArgumentList.Add(typeof(EtiketApiIstemcisi).Assembly.Location);
        ayarlar.Environment["ETIKET_API_ADRESI"] = http.BaseAddress!.ToString();
        using var surec = System.Diagnostics.Process.Start(ayarlar) ?? throw new InvalidOperationException("İstemci başlatılamadı.");
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        await surec.StandardInput.WriteAsync("5\n1\nİstemci_Mil.SLDPRT\n2\nİstemci_Montaj.SLDASM\n3\nP00003\n4\n0\n");
        surec.StandardInput.Close();
        using var zaman = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await surec.WaitForExitAsync(zaman.Token); }
        catch (OperationCanceledException) { surec.Kill(entireProcessTree: true); throw; }
        var metin = await cikti;
        kontrol(surec.ExitCode == 0 && (await hata).Length == 0, "Konsol menüsü hatasız kapanmalı.");
        kontrol(metin.Contains("Servis: Hazır") && metin.Contains("Etiket: P00003") && metin.Contains("Etiket: M00002"), "Konsol sağlık, parça ve montaj isteklerini tamamlamalı.");
        var liste = await new EtiketApiIstemcisi(http).ListeleAsync();
        kontrol(liste.Belgeler.Count == 5, "Sorgu ve listeleme yeni kayıt oluşturmamalı.");
        kontrol(liste.Belgeler.Single(b => b.Etiket == "P00003").DosyaAdi == "İstemci_Mil.SLDPRT", "Türkçe dosya adı uçtan uca korunmalı.");
    }
    private sealed class HataTasiyicisi(string mod) : HttpMessageHandler
    {
        public int IstekSayisi { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken iptal)
        {
            IstekSayisi++;
            return mod switch
            {
                "baglanti" => throw new HttpRequestException("GIZLI"),
                "zaman" => throw new TaskCanceledException("GIZLI"),
                "503" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { detail = "GIZLI" }) }),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json") })
            };
        }
    }
}
