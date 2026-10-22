param(
    [Parameter(Mandatory=$true)][string]$IpAdresi,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_]+$')][string]$Veritabani,
    [switch]$SadeceKontrol
)
$ErrorActionPreference = 'Stop'
$projeKoku = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$proje = Join-Path $projeKoku 'outputs\src\Etiket.Api\Etiket.Api.csproj'
$yerelAyar = Join-Path $projeKoku 'outputs\src\Etiket.Api\appsettings.Local.json'
if (!(Test-Path -LiteralPath $proje) -or !(Test-Path -LiteralPath $yerelAyar)) { throw 'Betik proje outputs\kurulum klasorunde olmali; kaynak ve yerel SQL ayari gerekli.' }
$ip = $null
if (![Net.IPAddress]::TryParse($IpAdresi,[ref]$ip) -or $ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $ip.Equals([Net.IPAddress]::Any)) { throw 'Merkez bilgisayarin belirli bir yerel IPv4 adresini yazin.' }
$yerelAdresler = @([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | ForEach-Object { $_.GetIPProperties().UnicastAddresses } | ForEach-Object { $_.Address.ToString() })
if ($yerelAdresler -notcontains $ip.ToString()) { throw 'Bu IP bu bilgisayara ait degil. Baska bilgisayarin IP adresiyle servis baslatilamaz.' }
$ayar = Get-Content -LiteralPath $yerelAyar -Raw -Encoding UTF8 | ConvertFrom-Json
$baglanti = [System.Data.Common.DbConnectionStringBuilder]::new()
$baglanti.set_ConnectionString($ayar.ConnectionStrings.MerkeziVeritabani)
if ($baglanti.Count -eq 0) { throw 'Yerel SQL baglanti ayari bos.' }
$null = $baglanti.Remove('Database')
$null = $baglanti.Remove('Initial Catalog')
$baglanti.set_Item('Initial Catalog',$Veritabani)
$adresler = @('http://127.0.0.1:5082')
if ($ip.ToString() -ne '127.0.0.1') { $adresler += "http://$($ip.ToString()):5082" }
$hosts = @('localhost','127.0.0.1',$ip.ToString(),[Environment]::MachineName) | Select-Object -Unique
Write-Output "Merkez: $([Environment]::MachineName)"
Write-Output "Veritabani: $Veritabani (mevcut veritabani; bu betik kurmaz veya sifirlamaz)"
Write-Output ('API: ' + ($adresler -join ' ; '))
if ($SadeceKontrol) {
    Write-Output 'Yapilandirma kontrolu gecti. SQL/HTTP erisimi ve guvenlik duvari bu adimda sinanmadi.'
    Write-Output 'API baslatilmadi; dosya veya veritabani degistirilmedi.'
    return
}
$dinleyici = @(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object { $_.LocalPort -eq 5082 })
if ($dinleyici.Count -gt 0) { throw '5082 zaten kullaniliyor. Mevcut API penceresini kontrol edin; ikinci servis baslatilmadi.' }
$derleme = Join-Path $projeKoku 'work\kullanim-build'
Write-Output 'API derleniyor. SolidWorks Eklenti DLL dosyasina dokunulmaz.'
& dotnet build $proje -c Release --artifacts-path $derleme --ignore-failed-sources -m:1 -nr:false -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'API derlenemedi; baslatilmadi.' }
$uygulama = Join-Path $derleme 'bin\Etiket.Api\release\Etiket.Api.dll'
$oncekiBaglanti = $env:ConnectionStrings__MerkeziVeritabani
$oncekiHosts = $env:AllowedHosts
$oncekiOrtam = $env:ASPNETCORE_ENVIRONMENT
try {
    $env:ConnectionStrings__MerkeziVeritabani = $baglanti.get_ConnectionString()
    $env:AllowedHosts = $hosts -join ';'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    Write-Output 'Durdurmak icin bu pencerede Ctrl+C. Pencere acik kalmali.'
    & dotnet $uygulama --urls ($adresler -join ';')
    if ($LASTEXITCODE -ne 0) { throw 'API hata ile kapandi.' }
} finally {
    $env:ConnectionStrings__MerkeziVeritabani = $oncekiBaglanti
    $env:AllowedHosts = $oncekiHosts
    $env:ASPNETCORE_ENVIRONMENT = $oncekiOrtam
}
