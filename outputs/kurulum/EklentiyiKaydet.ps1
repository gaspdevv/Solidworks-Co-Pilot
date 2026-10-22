#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot 'Pronom.SolidWorks.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Bu scripti dağıtım paketindeki DLL ile aynı klasörden çalıştırın.' }
foreach ($ad in @('SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll','SolidWorks.Interop.swpublished.dll')) {
 $yol = Join-Path $PSScriptRoot $ad
 if (!(Test-Path -LiteralPath $yol) -or [Reflection.AssemblyName]::GetAssemblyName($yol).Version.ToString() -ne '31.1.0.106') { throw ('Beklenen SolidWorks 2023 SP01 referansı bulunamadı: '+$ad) }
}
$kaydedici = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'
if (!(Test-Path -LiteralPath $kaydedici)) { throw '.NET Framework 4.8 çalışma ortamını kontrol edin.' }
& $kaydedici $dll /codebase
if ($LASTEXITCODE -ne 0) { throw 'COM kaydı oluşturulamadı.' }
Write-Output 'Eklenti kaydedildi. SolidWorks → Tools/Araçlar → Add-Ins/Eklentiler listesinden Pronom Otomatik Dosya Adlandırma seçeneğini etkinleştirin.'
Write-Output 'Paket klasörünü taşımayın; COM kaydı bu konumu kullanır. Başlangıçta otomatik yükleme henüz etkinleştirilmedi.'
