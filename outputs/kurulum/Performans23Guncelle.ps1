param([string]$EklentiKlasoru='C:\Pronom Eklenti')
$ErrorActionPreference='Stop'
if(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue){throw 'Önce açık belgelerinizi koruyarak SolidWorks uygulamasını tamamen kapatın. Dosyalar değiştirilmedi.'}
$eskiDll='92AFB675607548987A61F6F634A8AE2BB317A74683FA3B7BE2F5B0DE1B395E69'
$yeniDll='577227B6B52A5A94A2C354A5C25A359935FEA102E69FFC582DC394B48B053AD0'
$eskiExe='9CA38DFE8040E29D4825BC3D4EAEC378CB611664AE782D8D6907D750ED634185'
$yeniExe='5ED01111FB4C29297225BB0533291F15709DD986C6B1106547944DE5EF9C102F'
$dosyalar=@('Pronom.SolidWorks.dll','Araclar\Pronom.PencereTanisi.exe','Araclar\Pronom.PencereTanisi.exe.config')
foreach($ad in $dosyalar){
 if(!(Test-Path -LiteralPath (Join-Path $EklentiKlasoru $ad) -PathType Leaf)){throw ('Kurulu dosya bulunamadı: '+$ad+'. Güncelleme yapılmadı; eksik dosya incelenmeli.')}
 if(!(Test-Path -LiteralPath (Join-Path $PSScriptRoot ('Dosyalar\'+$ad)) -PathType Leaf)){throw ('Güncelleme dosyası eksik: '+$ad)}
}
$hash=(Get-FileHash -LiteralPath (Join-Path $EklentiKlasoru 'Pronom.SolidWorks.dll')).Hash
if($hash -notin @($eskiDll,$yeniDll)){throw 'Kurulu DLL beklenen sürüm21/23 değil; üzerine yazılmadı.'}
$hash=(Get-FileHash -LiteralPath (Join-Path $EklentiKlasoru 'Araclar\Pronom.PencereTanisi.exe')).Hash
if($hash -notin @($eskiExe,$yeniExe)){throw 'Kurulu yardımcı beklenen sürüm değil; üzerine yazılmadı.'}
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dosyalar.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($ad in $dosyalar){
 $girdi=@($manifest | Where-Object {$_.Dosya -eq $ad})
 if($girdi.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('Dosyalar\'+$ad))).Hash -ne $girdi[0].SHA256){throw ('Paket bütünlüğü uyuşmuyor: '+$ad)}
}
if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Dosyalar\Pronom.SolidWorks.dll')).Hash -ne $yeniDll -or (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Dosyalar\Araclar\Pronom.PencereTanisi.exe')).Hash -ne $yeniExe){throw 'Paket sürümü beklenen23 değil.'}
if(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue){throw 'SolidWorks açılmış; güncelleme yapılmadı.'}
$yedek=Join-Path $EklentiKlasoru ('Yedekler\Performans23-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path (Join-Path $yedek 'Araclar') -Force
foreach($ad in $dosyalar){Copy-Item -LiteralPath (Join-Path $EklentiKlasoru $ad) -Destination (Join-Path $yedek $ad)}
$degisen=@()
try{
 foreach($ad in $dosyalar){
  if(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue){throw 'Güncelleme sırasında SolidWorks açılmamalı.'}
  $degisen+=$ad
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Dosyalar\'+$ad)) -Destination (Join-Path $EklentiKlasoru $ad) -Force
 }
 foreach($ad in $dosyalar){
  $beklenen=@($manifest | Where-Object {$_.Dosya -eq $ad})[0].SHA256
  if((Get-FileHash -LiteralPath (Join-Path $EklentiKlasoru $ad)).Hash -ne $beklenen){throw ('Yazılan dosya doğrulanamadı: '+$ad)}
 }
}catch{
 $hata=$_.Exception.Message
 if(!(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue)){
  foreach($ad in $degisen){Copy-Item -LiteralPath (Join-Path $yedek $ad) -Destination (Join-Path $EklentiKlasoru $ad) -Force}
 }
 throw ('Güncelleme tamamlanamadı: '+$hata+'. Yedek: '+$yedek+'. SolidWorks açmadan önce dosyalar kontrol edilmeli.')
}
Write-Output 'DOGRULANDI: Performans23 DLL ve yardımcı araç birlikte güncellendi.'
Write-Output ('Önceki dosyalar: '+$yedek)
Write-Output 'COM kaydı/API/SQL/CAD/ApiAdresi/bayraklar/işlem günlükleri değiştirilmedi.'
