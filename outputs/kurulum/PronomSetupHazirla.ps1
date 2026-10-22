param([string]$NugetPaketKlasoru)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$src=Join-Path $root 'outputs\src\Etiket.Kurulum'
$build=Join-Path $root 'work\kaydet27-build'
$payload=Join-Path $root ('work\setup-payload-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path (Join-Path $payload 'Araclar') -Force
foreach($name in @('Pronom.SolidWorks.dll','SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll','SolidWorks.Interop.swpublished.dll')){
 $source=if($name -eq 'Pronom.SolidWorks.dll'){Join-Path $build 'bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll'}else{Join-Path $root ('Eklenti\'+$name)}
 Copy-Item -LiteralPath $source -Destination $payload
}
foreach($name in @('Pronom.PencereTanisi.exe','Pronom.PencereTanisi.exe.config')){
 Copy-Item -LiteralPath (Join-Path $build ('bin\Etiket.PencereTanisi\release\'+$name)) -Destination (Join-Path $payload 'Araclar')
}
foreach($name in @('BekleyenKayitlar.ps1','BekleyenKayitlarIslemleri.ps1','BekleyenKayitlariToparla.ps1','KayitSorunuBilgisi.ps1','YazilmamisKuyruguIptalEt.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $payload}
$collector=Join-Path $payload 'KayitSorunuBilgisi.ps1'
$text=[IO.File]::ReadAllText($collector).Replace("[string]"+'$EklentiKlasoru='+"'C:\Pronom Eklenti'","[string]"+'$EklentiKlasoru=$PSScriptRoot')
[IO.File]::WriteAllText($collector,$text,[Text.UTF8Encoding]::new($true))
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zipPath=Join-Path $src 'paket.zip'
# Yalnız bu proje tarafından üretilen gömülü paket yeniden oluşturulur.
if(Test-Path -LiteralPath $zipPath){Remove-Item -LiteralPath $zipPath}
$zip=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
$rows=@()
try{
 foreach($file in @(Get-ChildItem -LiteralPath $payload -Recurse -File)){
  $relative=$file.FullName.Substring($payload.Length+1).Replace('\','/')
  $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
  $rows+=[pscustomobject]@{Ad=$relative;SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash}
 }
}finally{$zip.Dispose()}
$rows | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $src 'paket.json') -Encoding UTF8
$oldHome=$env:DOTNET_CLI_HOME;$oldPackages=$env:NUGET_PACKAGES
try{
 $env:DOTNET_CLI_HOME=Join-Path $root 'work\dotnet-home'
 if($NugetPaketKlasoru){$env:NUGET_PACKAGES=$NugetPaketKlasoru}
 & dotnet build (Join-Path $src 'Etiket.Kurulum.csproj') -c Release --artifacts-path (Join-Path $root 'work\setup23-build') -m:1 -nr:false -p:UseSharedCompilation=false
 if($LASTEXITCODE -ne 0){throw 'Setup derlenemedi.'}
}finally{$env:DOTNET_CLI_HOME=$oldHome;$env:NUGET_PACKAGES=$oldPackages}
$exe=Join-Path $root 'work\setup23-build\bin\Etiket.Kurulum\release\Pronom.Kurulum.exe'
if(!(Test-Path -LiteralPath $exe)){throw ('Derleme bitti ancak kurulum EXE dosyası bulunamadı: '+$exe+'. Güvenlik geçmişini kontrol edin; dağıtım oluşturulmadı.')}
$process=Start-Process -FilePath $exe -ArgumentList @('--kendi-testleri',('"'+(Join-Path $root 'work')+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Setup çekirdeği kontrolleri geçmedi; dağıtım oluşturulmadı.'}
$eskiDllTestYolu=Join-Path $root 'work\setup-test-verisi\21\Pronom.SolidWorks.dll'
$process=Start-Process -FilePath $exe -ArgumentList @('--guncelleme-testleri',('"'+(Join-Path $root 'work')+'"'),('"'+$eskiDllTestYolu+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Eski sürümden güncelleme kontrolleri geçmedi; dağıtım oluşturulmadı.'}
$performansDllTestYolu=Join-Path $root 'work\performans23-build\bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll'
$process=Start-Process -FilePath $exe -ArgumentList @('--guncelleme-testleri',('"'+(Join-Path $root 'work')+'"'),('"'+$performansDllTestYolu+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Sürüm23ten güncelleme kontrolleri geçmedi; dağıtım oluşturulmadı.'}
$dxfDllTestYolu=Join-Path $root 'work\dxf24-build\bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll'
$process=Start-Process -FilePath $exe -ArgumentList @('--guncelleme-testleri',('"'+(Join-Path $root 'work')+'"'),('"'+$dxfDllTestYolu+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Sürüm24ten güncelleme kontrolleri geçmedi; dağıtım oluşturulmadı.'}
$kaydet25DllTestYolu=Join-Path $root 'work\kaydet25-build\bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll'
$process=Start-Process -FilePath $exe -ArgumentList @('--guncelleme-testleri',('"'+(Join-Path $root 'work')+'"'),('"'+$kaydet25DllTestYolu+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Sürüm25ten güncelleme kontrolleri geçmedi; dağıtım oluşturulmadı.'}
$kaydet26DllTestYolu=Join-Path $root 'work\kaydet26-build\bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll'
$process=Start-Process -FilePath $exe -ArgumentList @('--guncelleme-testleri',('"'+(Join-Path $root 'work')+'"'),('"'+$kaydet26DllTestYolu+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Sürüm26dan güncelleme kontrolleri geçmedi; dağıtım oluşturulmadı.'}
$process=Start-Process -FilePath $exe -ArgumentList @('--arayuz-testleri',('"'+(Join-Path $root 'work\setup-arayuz-kontrol')+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'DPI/arayüz kontrolleri geçmedi; dağıtım oluşturulmadı.'}
$out=Join-Path $root 'outputs\dagitim\Pronom-SolidWorks2023-Kaydet27-Kurulum.exe'
Copy-Item -LiteralPath $exe -Destination $out -Force
& (Join-Path $PSScriptRoot 'SetupYayinla.ps1') -SetupDosyasi $out -BeklenenSHA256 (Get-FileHash -LiteralPath $out).Hash
Write-Output ('SETUP HAZIR: '+$out)
