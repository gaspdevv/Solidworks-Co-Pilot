param([switch]$Kapat)
$ErrorActionPreference='Stop'
$klasor=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$bayrak=Join-Path $klasor 'NativeAdDenemesi.acik'
if($Kapat){if(Test-Path -LiteralPath $bayrak){Remove-Item -LiteralPath $bayrak};Write-Output 'Native ad deneme modu kapatıldı. SolidWorks tamamen kapatılıp yeniden açılmalı.';return}
New-Item -ItemType Directory -Path $klasor -Force | Out-Null
[IO.File]::WriteAllText($bayrak,'1',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $klasor 'KayitOlayTanisi.acik'),'1',[Text.UTF8Encoding]::new($false))
Write-Output 'Native ad pencere denemesi açıldı. Yalnızca Bağımsız Yap için; Kaydet kapalı, İptal seçilmeli.'
Write-Output 'API test servisi açık olmalı. Ad merkezden ayrılır; iptal numarası 005 SQL kurulumu ile tekrar kullanılabilir. Dosya kaydetmeyin.'