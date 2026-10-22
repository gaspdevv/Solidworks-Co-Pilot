#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot 'Pronom.SolidWorks.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Paket DLL dosyası bulunamadı.' }
$kaydedici = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'
& $kaydedici $dll /unregister
if ($LASTEXITCODE -ne 0) { throw 'COM kaydı kaldırılamadı.' }
Write-Output 'Pronom COM ve SolidWorks eklenti kaydı kaldırıldı. Yerel işlem günlükleri korunuyor.'
