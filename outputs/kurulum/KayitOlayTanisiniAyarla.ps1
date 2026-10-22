param([switch]$Kapat)
$ErrorActionPreference='Stop'
$klasor=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$bayrak=Join-Path $klasor 'KayitOlayTanisi.acik'
if($Kapat){if(Test-Path -LiteralPath $bayrak){Remove-Item -LiteralPath $bayrak};Write-Output 'Kayıt olay tanısı kapatıldı; günlük ve rezervasyonlar korundu.';return}
New-Item -ItemType Directory -Path $klasor -Force | Out-Null
[IO.File]::WriteAllText($bayrak,'1',[Text.UTF8Encoding]::new($false))
Write-Output 'Kayıt olay tanısı açıldı. Eklenti dosya adlandırma davranışını değiştirmez.'
Write-Output ('Tanı dosyası: '+(Join-Path $klasor 'KayitOlayTanisi.log'))