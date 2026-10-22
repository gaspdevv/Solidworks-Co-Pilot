param([Parameter(Mandatory=$true)][string]$SolidWorksApiKlasoru)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$refs=(Resolve-Path -LiteralPath $SolidWorksApiKlasoru).Path
$names=@('SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll','SolidWorks.Interop.swpublished.dll')
foreach($name in $names){if(!(Test-Path -LiteralPath (Join-Path $refs $name) -PathType Leaf)){throw ('Referans eksik: '+$name)}}
$build=Join-Path $root 'work\github-build'
foreach($project in @('Etiket.SolidWorks','Etiket.PencereTanisi')){
 & dotnet build (Join-Path $root ('outputs\src\'+$project+'\'+$project+'.csproj')) -c Release --artifacts-path $build -m:1 -nr:false -p:UseSharedCompilation=false ('-p:SolidWorksApiKlasoru='+$refs)
 if($LASTEXITCODE -ne 0){throw ($project+' derlenemedi.')}
}
$temp=Join-Path $root ('work\g'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$payload=Join-Path $temp 'payload'
$setup=Join-Path $temp 'setup'
New-Item -ItemType Directory -Path (Join-Path $payload 'Araclar'),$setup -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll') -Destination $payload
foreach($name in $names){Copy-Item -LiteralPath (Join-Path $refs $name) -Destination $payload}
foreach($name in @('Pronom.PencereTanisi.exe','Pronom.PencereTanisi.exe.config')){Copy-Item -LiteralPath (Join-Path $build ('bin\Etiket.PencereTanisi\release\'+$name)) -Destination (Join-Path $payload 'Araclar')}
foreach($name in @('BekleyenKayitlar.ps1','BekleyenKayitlarIslemleri.ps1','BekleyenKayitlariToparla.ps1','KayitSorunuBilgisi.ps1','YazilmamisKuyruguIptalEt.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $payload}
$collector=Join-Path $payload 'KayitSorunuBilgisi.ps1'
$text=[IO.File]::ReadAllText($collector).Replace("[string]"+'$EklentiKlasoru='+"'C:\Pronom Eklenti'","[string]"+'$EklentiKlasoru=$PSScriptRoot')
[IO.File]::WriteAllText($collector,$text,[Text.UTF8Encoding]::new($true))
foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'outputs\src\Etiket.Kurulum') -File){if($file.Extension -in @('.cs','.csproj','.manifest','.config')){Copy-Item -LiteralPath $file.FullName -Destination $setup}}
$hash=(Get-FileHash -LiteralPath (Join-Path $payload 'Pronom.SolidWorks.dll') -Algorithm SHA256).Hash
$core=Join-Path $setup 'KurulumCekirdegi.cs'
$text=[IO.File]::ReadAllText($core)
if(([regex]::Matches($text,'public const string YeniDll="[A-F0-9]{64}"')).Count -ne 1){throw 'Setup hash sabiti kesin degil.'}
$text=[regex]::Replace($text,'public const string YeniDll="[A-F0-9]{64}"',('public const string YeniDll="'+$hash+'"'))
[IO.File]::WriteAllText($core,$text,[Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zip=[IO.Compression.ZipFile]::Open((Join-Path $setup 'paket.zip'),[IO.Compression.ZipArchiveMode]::Create)
$rows=@()
try{foreach($file in Get-ChildItem -LiteralPath $payload -Recurse -File){
 $relative=$file.FullName.Substring($payload.Length+1).Replace('\','/')
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
 $rows+=[pscustomobject]@{Ad=$relative;SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash}
}}finally{$zip.Dispose()}
[IO.File]::WriteAllText((Join-Path $setup 'paket.json'),($rows | ConvertTo-Json),[Text.UTF8Encoding]::new($true))
& dotnet build (Join-Path $setup 'Etiket.Kurulum.csproj') -c Release --artifacts-path $build -m:1 -nr:false -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Setup derlenemedi.'}
$exe=Join-Path $build 'bin\Etiket.Kurulum\release\Pronom.Kurulum.exe'
& $exe --kendi-testleri $temp
if($LASTEXITCODE -ne 0){throw 'Setup kendi kontrolleri gecmedi.'}
Write-Output ('YEREL SETUP HAZIR: '+$exe)
Write-Output 'Bilgisayara kurulum/COM kaydi/SQL islemi yapilmadi. Eski surumden guncelleme ve gercek SolidWorks kabulunu ayrica yapin.'
