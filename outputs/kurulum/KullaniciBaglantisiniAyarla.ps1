param(
    [Parameter(Mandatory=$true)][string]$ApiAdresi,
    [switch]$SadeceKontrol
)
$ErrorActionPreference = 'Stop'
$adres = $null
if (![Uri]::TryCreate($ApiAdresi.TrimEnd('/')+'/',[UriKind]::Absolute,[ref]$adres) -or $adres.Scheme -notin @('http','https') -or $adres.UserInfo -or $adres.Query -or $adres.Fragment -or $adres.AbsolutePath -ne '/') { throw 'Sunucunun kok HTTP/HTTPS adresini yazin; ornek http://127.0.0.1:5082 . Kullanici/parola veya ek yol eklemeyin.' }
if (!$SadeceKontrol -and (Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue)) { throw 'Adres ayarlamak icin once SolidWorks tamamen kapali olmali. Mevcut ayar degistirilmedi.' }
$saglik = Invoke-RestMethod -Uri ($adres.AbsoluteUri+'api/health') -TimeoutSec 5
if ($saglik.durum -ne [regex]::Unescape('Haz\u0131r') -or $saglik.veritabani -ne [regex]::Unescape('Ba\u011fl\u0131')) { throw 'Pronom API saglik sonucu beklenen durumda degil. Mevcut ayar degistirilmedi.' }
Write-Output ('API saglik kontrolu gecti: ' + $adres.AbsoluteUri)
$ayarKlasoru = Join-Path $env:LOCALAPPDATA 'Pronom\SolidworksEtiketleme'
$ayarDosyasi = Join-Path $ayarKlasoru 'ApiAdresi.txt'
if ($SadeceKontrol) {
    Write-Output 'Yalnizca baglanti kontrol edildi. Kullanici ayari ve islem gunlugu degistirilmedi.'
    return
}
if (Test-Path -LiteralPath $ayarDosyasi) {
    if ([IO.File]::ReadAllText($ayarDosyasi).Trim().TrimEnd('/') -eq $adres.AbsoluteUri.TrimEnd('/')) {
        Write-Output 'Bu adres zaten kayitli. Dosya ve islem gunlugu degistirilmedi.'
        return
    }
}
if (Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue) { throw 'SolidWorks acilmis. Mevcut ayar degistirilmedi.' }
[IO.Directory]::CreateDirectory($ayarKlasoru) | Out-Null
$gecici = Join-Path $ayarKlasoru ('ApiAdresi-'+[guid]::NewGuid().ToString('N')+'.tmp')
$bayt = [Text.UTF8Encoding]::new($false).GetBytes($adres.AbsoluteUri)
$akim = [IO.File]::Open($gecici,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try { $akim.Write($bayt,0,$bayt.Length) } finally { $akim.Dispose() }
if (Test-Path -LiteralPath $ayarDosyasi) {
    $yedekKlasoru = Join-Path $ayarKlasoru 'AyarYedekleri'
    [IO.Directory]::CreateDirectory($yedekKlasoru) | Out-Null
    $yedek = Join-Path $yedekKlasoru ('ApiAdresi-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N')+'.txt')
    [IO.File]::Replace($gecici,$ayarDosyasi,$yedek)
    Write-Output ('Onceki adres yedeklendi: ' + $yedek)
} else {
    [IO.File]::Move($gecici,$ayarDosyasi)
}
Write-Output 'Kullanici API adresi kaydedildi. CAD, Eklenti DLL ve Islemler degistirilmedi.'
Write-Output ('Web adresi: ' + $adres.AbsoluteUri)
