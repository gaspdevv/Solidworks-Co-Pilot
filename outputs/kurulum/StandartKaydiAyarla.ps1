param([switch]$Kapat)
$ErrorActionPreference='Stop'
$klasor=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$bayrak=Join-Path $klasor 'StandartKayit.acik'
if($Kapat){
 if(Test-Path -LiteralPath $bayrak){Remove-Item -LiteralPath $bayrak}
 Write-Output 'Standart Kaydet/Farklı Kaydet denemesi kapatıldı. SolidWorks tamamen kapatılıp yeniden açılmalı.'
 return
}
New-Item -ItemType Directory -Path $klasor -Force | Out-Null
foreach($ad in @('StandartKayit.acik','KayitOlayTanisi.acik')){
 [IO.File]::WriteAllText((Join-Path $klasor $ad),'1',[Text.UTF8Encoding]::new($false))
}
Write-Output 'Standart Kaydet/Farklı Kaydet denemesi açıldı. SolidWorks tamamen kapatılıp yeniden açılmalı.'
Write-Output 'Standart pencerede ad düzenlenebilir. Otomatik adla başarılı kayıt tabloya eklenir; özel adla kayıt tabloya eklenmez ve kullanılmayan numara serbest bırakılır.'
Write-Output 'Bu sürüm gerçek dosya kaydeder. İlk testte kopya seçeneklerini değiştirmeden yeni test parçasını kaydedin.'