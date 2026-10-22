param([switch]$Kapat)
$ErrorActionPreference='Stop'
$klasor=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$bayrak=Join-Path $klasor 'BagimsizKayit.acik'
if($Kapat){
 if(Test-Path -LiteralPath $bayrak){Remove-Item -LiteralPath $bayrak}
 Write-Output 'Gerçek bağımsız kayıt kapatıldı. Native ad denemesinde Kaydet pasif kalır. SolidWorks tamamen kapatılıp yeniden açılmalı.'
 return
}
New-Item -ItemType Directory -Path $klasor -Force | Out-Null
foreach($ad in @('NativeAdDenemesi.acik','KayitOlayTanisi.acik','BagimsizKayit.acik')){
 [IO.File]::WriteAllText((Join-Path $klasor $ad),'1',[Text.UTF8Encoding]::new($false))
}
Write-Output 'Gerçek bağımsız kayıt açıldı. SolidWorks tamamen kapatılıp yeniden açılmalı.'
Write-Output 'Bağımsız Yap penceresinde Kaydet artık dosya oluşturur ve montaj referansını değiştirir. Yalnızca test montajı/klasörü kullanın.'
Write-Output 'Ayrılan otomatik adla başarılı kayıt merkezi tabloya eklenir; özel adla kayıt tabloya eklenmez ve kullanılmayan otomatik numara serbest bırakılır.'