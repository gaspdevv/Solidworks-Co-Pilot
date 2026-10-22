param([switch]$SadeceKontrol)
$ErrorActionPreference = 'Stop'
$ayarYolu = Join-Path $PSScriptRoot 'merkez-ayar.json'
if (!(Test-Path -LiteralPath $ayarYolu -PathType Leaf)) {
    throw 'merkez-ayar.ornek.json dosyasını merkez-ayar.json adıyla kopyalayıp merkez bilgisayarın SQL ve IP bilgilerini yazın.'
}
$ayar = Get-Content -LiteralPath $ayarYolu -Raw -Encoding UTF8 | ConvertFrom-Json
if ($ayar.Ortam -ceq 'Kullanim') {
    if ($ayar.Veritabani -cne 'Pronom_Kullanim' -and
        ([string]::IsNullOrWhiteSpace($ayar.Veritabani) -or $ayar.KullanimVeritabaniOnayi -cne $ayar.Veritabani)) {
        throw 'Mevcut veritabanıyla Kullanim için KullanimVeritabaniOnayi, Veritabani adıyla birebir eşleşmeli.'
    }
} elseif ($ayar.Ortam -ceq 'Test') {
    if ($ayar.Veritabani -cnotmatch '^SolidWorksAutomation_ApiTest_[A-Za-z0-9_]+$') { throw 'Test ortamında ayrı SolidWorksAutomation_ApiTest_ veritabanı kullanılmalı.' }
} else { throw 'Ortam açıkça Test veya Kullanim seçilmeli.' }
if ([string]::IsNullOrWhiteSpace($ayar.SqlSunucusu)) { throw 'SQL sunucusu yazılmalı.' }
$ip = $null
if (![Net.IPAddress]::TryParse($ayar.IpAdresi,[ref]$ip) -or $ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $ip.Equals([Net.IPAddress]::Any)) { throw 'Merkez bilgisayarın belirli bir yerel IPv4 adresini yazın.' }
$adresler = @([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | ForEach-Object { $_.GetIPProperties().UnicastAddresses } | ForEach-Object { $_.Address.ToString() })
if ($adresler -notcontains $ip.ToString()) { throw 'Seçilen IP bu bilgisayara ait değil.' }
$baglanti = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$baglanti.set_DataSource([string]$ayar.SqlSunucusu)
$baglanti.set_InitialCatalog([string]$ayar.Veritabani)
$baglanti.set_IntegratedSecurity($true)
$baglanti.set_Encrypt($true)
$baglanti.set_TrustServerCertificate($true)
$baglanti.set_ConnectTimeout(10)
$api = Join-Path $PSScriptRoot 'Api\Etiket.Api.dll'
if (!(Test-Path -LiteralPath $api -PathType Leaf)) { throw 'API dosyaları eksik; kullanım paketinin Merkez klasöründen çalıştırın.' }
Write-Output ('Veritabanı: '+$ayar.Veritabani+' — Windows oturumu ile SQL bağlantısı')
Write-Output ('ORTAM: '+$ayar.Ortam)
Write-Output 'SQL veritabanı bu komutla oluşturulmaz veya sıfırlanmaz.'
if ($SadeceKontrol) { Write-Output 'Yapılandırma geçerli. SQL/ağ bağlantısı sınanmadı; API başlatılmadı.'; return }
$runtimes = & dotnet --list-runtimes
if ($LASTEXITCODE -ne 0) { throw '.NET çalışma ortamı okunamadı.' }
foreach ($framework in @('Microsoft.AspNetCore.App','Microsoft.NETCore.App')) {
    $surumler = @($runtimes | ForEach-Object {
        if ($_ -match ('^'+[regex]::Escape($framework)+' (10\.0\.\d+) ')) { [version]$Matches[1] }
    })
    if (!($surumler | Where-Object { $_ -ge [version]'10.0.13' })) { throw '.NET 10.0.13 veya daha yeni 10.0.x ASP.NET Core Runtime (x64) gerekli.' }
}
try { $dinleyiciler=@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()) }
catch { throw 'Bu Windows oturumunda port bilgisi okunamadı; API başlatılmadı. Hata sonucunu paylaşın.' }
if (@($dinleyiciler | Where-Object Port -eq 5082).Count) { throw '5082 kullanılıyor. İkinci API başlatılmadı; mevcut servis penceresini kontrol edin.' }
$urls = @('http://127.0.0.1:5082')
if ($ip.ToString() -ne '127.0.0.1') { $urls += ('http://'+$ip+':5082') }
$oncekiBaglanti=$env:ConnectionStrings__MerkeziVeritabani
$oncekiHosts=$env:AllowedHosts
$oncekiOrtam=$env:ASPNETCORE_ENVIRONMENT
try {
    $env:ConnectionStrings__MerkeziVeritabani=$baglanti.get_ConnectionString()
    $env:AllowedHosts=(@('localhost','127.0.0.1',$ip.ToString(),[Environment]::MachineName) | Select-Object -Unique) -join ';'
    $env:ASPNETCORE_ENVIRONMENT='Production'
    Write-Output ('Web: '+($urls -join ' ; '))
    Write-Output 'Bu pencere açık kalmalı. Durdurmak için Ctrl+C.'
    & dotnet $api --urls ($urls -join ';')
    if ($LASTEXITCODE -ne 0) { throw 'API hata ile kapandı.' }
} finally {
    $env:ConnectionStrings__MerkeziVeritabani=$oncekiBaglanti
    $env:AllowedHosts=$oncekiHosts
    $env:ASPNETCORE_ENVIRONMENT=$oncekiOrtam
}
