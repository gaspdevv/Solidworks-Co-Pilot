param([string]$NugetPaketKlasoru)
$ErrorActionPreference='Stop'
$kok=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$surum='2026.10.16-rc1'
$dagitim=Join-Path $kok 'outputs\dagitim'
$paket=Join-Path $dagitim ('Pronom-Kullanim-Adayi-'+$surum+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
if (Test-Path -LiteralPath $paket) { throw 'Paket hedefi zaten var.' }
$null=New-Item -ItemType Directory -Path $paket
$merkez=Join-Path $paket 'Merkez'; $eklenti=Join-Path $paket 'Eklenti'
$null=New-Item -ItemType Directory -Path $merkez,$eklenti
$onceki=$env:NUGET_PACKAGES
try {
    if ($NugetPaketKlasoru) { $env:NUGET_PACKAGES=$NugetPaketKlasoru }
    & dotnet publish (Join-Path $kok 'outputs\src\Etiket.Api\Etiket.Api.csproj') -c Release --self-contained false --artifacts-path (Join-Path $kok 'work\kullanim-hazirlik-build') -o (Join-Path $merkez 'Api') -m:1 -nr:false -p:UseSharedCompilation=false -p:NuGetAudit=true -p:NuGetAuditMode=all
    if ($LASTEXITCODE -ne 0) { throw 'API yayımlama dosyaları hazırlanamadı. Eksik klasör paketlenmedi.' }
} finally { $env:NUGET_PACKAGES=$onceki }
if (Get-ChildItem -LiteralPath $paket -Recurse -File -Filter 'appsettings.Local.json') { throw 'Yerel SQL ayarı pakete girmiş; arşiv hazırlanmadı.' }
foreach ($ad in @('MerkezApiBaslat.ps1','MerkezApiBaslat.cmd','merkez-ayar.ornek.json','merkez-ayar.test-ornek.json')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $ad) -Destination $merkez
}
foreach ($ad in @('Pronom.SolidWorks.dll','SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll','SolidWorks.Interop.swpublished.dll','EklentiyiKaydet.ps1','ApiAdresiniAyarla.ps1','BagimsizKaydiAyarla.ps1','StandartKaydiAyarla.ps1','NativeAdDenemesiniAyarla.ps1','KayitOlayTanisiniAyarla.ps1','BekleyenKayitlariToparla.ps1','YazilmamisKuyruguIptalEt.ps1')) {
    Copy-Item -LiteralPath (Join-Path $kok ('Eklenti\'+$ad)) -Destination $eklenti
}
$beklenen='92AFB675607548987A61F6F634A8AE2BB317A74683FA3B7BE2F5B0DE1B395E69'
if ((Get-FileHash -LiteralPath (Join-Path $eklenti 'Pronom.SolidWorks.dll')).Hash -ne $beklenen) { throw 'Fiziksel kabulü yapılmış sürüm21 DLL uyuşmuyor. Paket durduruldu.' }
Copy-Item -LiteralPath (Join-Path $kok 'Eklenti\Araclar') -Destination $eklenti -Recurse
foreach ($ad in @('BekleyenKayitlar.ps1','BekleyenKayitlarIslemleri.ps1','BekleyenKayitlar.cmd','KullaniciBaglantisiniAyarla.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $ad) -Destination $eklenti
}
$sql=Join-Path $merkez 'Sql'; $null=New-Item -ItemType Directory -Path $sql
Copy-Item -LiteralPath (Join-Path $kok 'outputs\sql\006-temiz-kullanim-kur.sql') -Destination $sql
Copy-Item -LiteralPath (Join-Path $kok 'outputs\sql\007-kullanim-yedegi.sql') -Destination $sql
Copy-Item -LiteralPath (Join-Path $kok 'outputs\KULLANIMA-HAZIRLIK.md') -Destination (Join-Path $paket 'ONCE-OKU.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PaketButunlugunuDogrula.ps1') -Destination $paket
$dosyalar=@(Get-ChildItem -LiteralPath $paket -Recurse -File | ForEach-Object {
    [pscustomobject]@{Dosya=$_.FullName.Substring($paket.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$manifest=[pscustomobject]@{Surum=$surum;Tur='Kullanim adayi; coklu bilgisayar kabulu bekliyor';Dosyalar=$dosyalar}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $paket 'paket-dosyalari.json') -Encoding UTF8
& (Join-Path $paket 'PaketButunlugunuDogrula.ps1')
$zip=$paket+'.zip'
Compress-Archive -LiteralPath $paket -DestinationPath $zip
Write-Output ('PAKET: '+$zip)
