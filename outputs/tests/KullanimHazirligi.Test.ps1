$ErrorActionPreference='Stop'
$kok=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$test=Join-Path $kok ('work\kullanim-hazirlik-test\'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path (Join-Path $test 'Api') -Force
$betik=Join-Path $test 'MerkezApiBaslat.ps1'
Copy-Item -LiteralPath (Join-Path $kok 'outputs\kurulum\MerkezApiBaslat.ps1') -Destination $betik
[IO.File]::WriteAllText((Join-Path $test 'Api\Etiket.Api.dll'),'izole test dosyasi; calistirilmaz')
$ayarYolu=Join-Path $test 'merkez-ayar.json'
$script:sayi=0; $script:calisti=$false; $script:runtime='10.0.13'
function dotnet {
    if ($args[0] -ne '--list-runtimes') { $script:calisti=$true; throw 'Test gerçek API başlatmamalı.' }
    $global:LASTEXITCODE=0
    'Microsoft.AspNetCore.App '+$script:runtime+' [izole]'
    'Microsoft.NETCore.App '+$script:runtime+' [izole]'
}
function Kontrol($ok,$mesaj) { if (!$ok) { throw ('TEST: '+$mesaj) }; $script:sayi++ }
function Ayar($ortam,$db,$ip='127.0.0.1') {
    @{Ortam=$ortam;Veritabani=$db;IpAdresi=$ip;SqlSunucusu='.\SQLEXPRESS'} | ConvertTo-Json | Set-Content -LiteralPath $ayarYolu -Encoding UTF8
}
$reddedildi=$false; try { & $betik -SadeceKontrol } catch { $reddedildi=$true }
Kontrol $reddedildi 'Eksik ayar reddedilir'
Ayar 'Kullanim' 'Pronom_Kullanim'
Kontrol ((& $betik -SadeceKontrol | Out-String) -match 'ORTAM: Kullanim') 'Kullanım ayarı doğrulanır'
Ayar 'Test' 'SolidWorksAutomation_ApiTest_Hazirlik'
Kontrol ((& $betik -SadeceKontrol | Out-String) -match 'ORTAM: Test') 'Test ayarı ayrı doğrulanır'
Ayar 'Kullanim' 'SolidWorksAutomation_ApiTest_Hazirlik'
$onay=Get-Content -LiteralPath $ayarYolu -Raw | ConvertFrom-Json
$onay | Add-Member -NotePropertyName KullanimVeritabaniOnayi -NotePropertyValue 'SolidWorksAutomation_ApiTest_Hazirlik'
$onay | ConvertTo-Json | Set-Content -LiteralPath $ayarYolu -Encoding UTF8
Kontrol ((& $betik -SadeceKontrol | Out-String) -match 'ORTAM: Kullanim') 'Açıkça onaylanan mevcut DB kullanımda kabul edilir'
$onay.KullanimVeritabaniOnayi='BaskaVeritabani'
$onay | ConvertTo-Json | Set-Content -LiteralPath $ayarYolu -Encoding UTF8
$reddedildi=$false;try { & $betik -SadeceKontrol | Out-Null } catch { $reddedildi=$true }
Kontrol $reddedildi 'Başka DB adıyla kullanım onayı reddedilir'
foreach ($senaryo in @(@('Test','Pronom_Kullanim','127.0.0.1'),@('Kullanim','SolidWorksAutomation_ApiTest_Hazirlik','127.0.0.1'),@('Belirsiz','Pronom_Kullanim','127.0.0.1'),@('Kullanim','Pronom_Kullanim','0.0.0.0'),@('Kullanim','Pronom_Kullanim','203.0.113.254'))) {
    Ayar $senaryo[0] $senaryo[1] $senaryo[2]
    $reddedildi=$false; try { & $betik -SadeceKontrol | Out-Null } catch { $reddedildi=$true }
    Kontrol $reddedildi 'Ortam/DB/IP karışıklığı reddedilir'
}
Ayar 'Kullanim' 'Pronom_Kullanim'
$script:runtime='10.0.9'
$reddedildi=$false; try { & $betik | Out-Null } catch { $reddedildi=$_.Exception.Message -match '10.0.13' }
Kontrol $reddedildi 'Eski runtime ile kullanım başlamaz'
$script:runtime='10.0.13'
$dinleyiciler=$null
try { $dinleyiciler=@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()) }
catch { Write-Output 'Bu test oturumunda port listesi okunamadı; gerçek dolu port kontrolü sınanmadı.' }
if ($null -ne $dinleyiciler -and @($dinleyiciler | Where-Object Port -eq 5082).Count) {
    $reddedildi=$false; try { & $betik | Out-Null } catch { $reddedildi=$_.Exception.Message -match '5082' }
    Kontrol $reddedildi 'Dolu portta ikinci API başlatılmaz'
}
Kontrol (!$script:calisti) 'Hiçbir test API/SQL başlatmadı'
$kontrol=Join-Path $test 'PaketButunlugunuDogrula.ps1'
Copy-Item -LiteralPath (Join-Path $kok 'outputs\kurulum\PaketButunlugunuDogrula.ps1') -Destination $kontrol
$dosya=Join-Path $test 'ornek.txt';[IO.File]::WriteAllText($dosya,'ornek')
@{Surum='test';Dosyalar=@(@{Dosya='ornek.txt';SHA256=(Get-FileHash -LiteralPath $dosya).Hash})} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $test 'paket-dosyalari.json') -Encoding UTF8
Kontrol ((& $kontrol | Out-String) -match 'doğrulandı') 'Doğru paket kabul edilir'
[IO.File]::WriteAllText($dosya,'degismis')
$reddedildi=$false;try { & $kontrol } catch { $reddedildi=$_.Exception.Message -match 'Değişmiş' }
Kontrol $reddedildi 'Değişmiş paket reddedilir'
Write-Output ('GEÇTİ: '+$script:sayi+' kullanım hazırlığı kontrolü. İzole ayarlar/sahte runtime; gerçek API/SQL yazımı yok.')
