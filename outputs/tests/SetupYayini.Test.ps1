$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$publish=Join-Path $root 'outputs\kurulum\SetupYayinla.ps1'
$setup=Join-Path $root 'outputs\dagitim\Pronom-SolidWorks2023-DXF24-Kurulum.exe'
$hash=(Get-FileHash -LiteralPath $setup).Hash
$test=Join-Path $root ('work\setup-yayin-test-'+[Guid]::NewGuid().ToString('N'))
$webs=@((Join-Path $test 'kaynak'),(Join-Path $test 'merkez'))
foreach($web in $webs){$null=New-Item -ItemType Directory -Path $web -Force}
$backups=Join-Path $test 'yedekler';$script:sayi=0
function Assert($ok,$message){if(!$ok){throw ('TEST: '+$message)};$script:sayi++}
function Ret($action){$failed=$false;try{& $action | Out-Null}catch{$failed=$true};Assert $failed 'Gecersiz yayin reddedilmeli'}
& $publish -SetupDosyasi $setup -BeklenenSHA256 $hash -WebKlasoru $webs -YedekKlasoru $backups | Out-Null
foreach($web in $webs){Assert ((Get-FileHash -LiteralPath (Join-Path $web 'indir\Pronom-Kurulum.exe')).Hash -eq $hash) 'Iki hedefte gercek setup hash ayni'}
$target=Join-Path $webs[0] 'indir\Pronom-Kurulum.exe'
[IO.File]::WriteAllText($target,'ONCEKI PAKET')
$once=(Get-FileHash -LiteralPath $target).Hash
& $publish -SetupDosyasi $setup -BeklenenSHA256 $hash -WebKlasoru $webs -YedekKlasoru $backups | Out-Null
Assert ((Get-FileHash -LiteralPath $target).Hash -eq $hash) 'Mevcut hedef atomik guncellenir'
Assert (@(Get-ChildItem -LiteralPath $backups -File | Where-Object {(Get-FileHash -LiteralPath $_.FullName).Hash -eq $once}).Count -eq 1) 'Eski paket web disina yedeklenir'
Ret { & $publish -SetupDosyasi $setup -BeklenenSHA256 ('0'*64) -WebKlasoru $webs -YedekKlasoru $backups }
Assert ((Get-FileHash -LiteralPath $target).Hash -eq $hash) 'Yanlis hash yayini bozmaz'
Ret { & $publish -SetupDosyasi (Join-Path $test 'yok.exe') -BeklenenSHA256 $hash -WebKlasoru $webs -YedekKlasoru $backups }
$fake=Join-Path $test 'yabanci.exe';[IO.File]::WriteAllText($fake,'setup degil')
Ret { & $publish -SetupDosyasi $fake -BeklenenSHA256 (Get-FileHash -LiteralPath $fake).Hash -WebKlasoru $webs -YedekKlasoru $backups }
Ret { & $publish -SetupDosyasi $setup -BeklenenSHA256 $hash -WebKlasoru @($webs[0],(Join-Path $test 'eksik')) -YedekKlasoru $backups }
Assert ((Get-FileHash -LiteralPath $target).Hash -eq $hash) 'Eksik ikinci hedefte aktif paket degismez'
Assert (@(Get-ChildItem -LiteralPath $test -Recurse -Filter '*.tmp').Count -eq 0) 'Yarim yayin dosyalari temizlenir'
Write-Output ('GECTI: '+$script:sayi+' setup yayin kontrolu; gercek EXE/izole web klasorleri. HTTP/SQL/CAD degismedi.')
