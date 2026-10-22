param([Parameter(Mandatory=$true)][string]$IpAdresi,[string]$TestVeritabani='SolidWorksAutomation_ApiTest_20261012_01')
$ErrorActionPreference = 'Stop'
if (!$TestVeritabani.StartsWith('SolidWorksAutomation_ApiTest_') -or $TestVeritabani -notmatch '^SolidWorksAutomation_ApiTest_[A-Za-z0-9_]+$') { throw 'Yalnızca ayrı test veritabanı adı kabul edilir.' }
$ip = $null
if (![Net.IPAddress]::TryParse($IpAdresi,[ref]$ip) -or $ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) { throw 'Geçerli yerel IPv4 adresi belirtin.' }
$yerelAdresler = [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | ForEach-Object { $_.GetIPProperties().UnicastAddresses } | ForEach-Object { $_.Address.ToString() }
if ($yerelAdresler -notcontains $ip.ToString()) { throw 'Bu IP geliştirme bilgisayarının bir adresi değil.' }
$projeKoku = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$yerelAyar = Join-Path $projeKoku 'outputs\src\Etiket.Api\appsettings.Local.json'
if (!(Test-Path -LiteralPath $yerelAyar)) { throw 'Yerel API bağlantı ayarı bulunamadı.' }
$ayar = Get-Content -LiteralPath $yerelAyar -Raw | ConvertFrom-Json
# PowerShell sözlük uyarlaması yerine gerçek .NET erişim metotlarını kullan.
$baglanti = [System.Data.Common.DbConnectionStringBuilder]::new()
$baglanti.set_ConnectionString($ayar.ConnectionStrings.MerkeziVeritabani)
$null=$baglanti.Remove('Database');$null=$baglanti.Remove('Initial Catalog');$baglanti.set_Item('Initial Catalog',$TestVeritabani)
$oncekiBaglanti=$env:ConnectionStrings__MerkeziVeritabani
$oncekiHosts=$env:AllowedHosts
$env:ConnectionStrings__MerkeziVeritabani=$baglanti.get_ConnectionString()
$env:AllowedHosts="localhost;127.0.0.1;$IpAdresi"
try {
 Write-Output "Test servisi: http://${IpAdresi}:5082/ — Veritabanı: $TestVeritabani"
 $proje = Join-Path $projeKoku 'outputs\src\Etiket.Api\Etiket.Api.csproj'
 $derleme = Join-Path $projeKoku 'work\tek-bilgisayar-build'
 Write-Output 'Güncel test API derleniyor...'
 & dotnet build $proje -c Release --artifacts-path $derleme --ignore-failed-sources -m:1 -nr:false -p:UseSharedCompilation=false
 if ($LASTEXITCODE -ne 0) { throw 'Güncel API derlenemedi; servis başlatılmadı.' }
 $uygulama = Join-Path $derleme 'bin\Etiket.Api\release\Etiket.Api.dll'
 & dotnet $uygulama --urls "http://${IpAdresi}:5082"
 if ($LASTEXITCODE -ne 0) { throw 'Test API başlatılamadı.' }
} finally {
 $env:ConnectionStrings__MerkeziVeritabani=$oncekiBaglanti
 $env:AllowedHosts=$oncekiHosts
}
