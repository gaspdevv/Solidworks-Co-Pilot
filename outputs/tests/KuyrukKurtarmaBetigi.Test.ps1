$ErrorActionPreference='Stop'
$kok=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testKoku=Join-Path $kok ('work\kkt\'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $testKoku -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $kok 'outputs\kurulum\YazilmamisKuyruguIptalEt.ps1') -Destination $testKoku
Copy-Item -LiteralPath (Join-Path $kok 'work\geri-kullanim-build\bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll') -Destination $testKoku
$null=[Reflection.Assembly]::LoadFrom((Join-Path $testKoku 'Pronom.SolidWorks.dll'))
$eski=$env:LOCALAPPDATA
$global:pronomKurtarma = @{}
$script:sayi=0
function Kontrol($kosul,$mesaj){if(!$kosul){throw ('TEST BAŞARISIZ: '+$mesaj)};$script:sayi++}
function Get-Process {param($Name,$ErrorAction) if($global:pronomKurtarma.sw){[pscustomobject]@{ProcessName='SLDWORKS'}}}
function Invoke-RestMethod {
 param($Method,$Uri,$TimeoutSec,$MaximumRedirection)
 $p=$global:pronomKurtarma;$p.cagri++
 if($Uri -notlike ('*'+$p.id+'*')){throw 'Başka GUID ile istek gönderildi'}
 if($Method -eq 'Get'){
  if($p.mod -eq 'get-hata'){throw 'GET bağlantı hatası'}
  return [pscustomobject]@{islemId=$(if($p.mod -eq 'get-kimlik'){[Guid]::NewGuid()}else{$p.id});dosyaAdi=$(if($p.mod -eq 'get-ad'){'P00055.SLDPRT'}else{'P00054.SLDPRT'});durum=$(if($p.mod -eq 'onayli'){'Onaylandi'}elseif($p.mod -eq 'zaten-merkez-iptal'){'Iptal'}else{'Ayrildi'})}
 }
 if($p.g.Oku($p.id).Asama -ne 'KuyrukIptalBekliyor'){throw 'İptal niyeti POST öncesi kalıcı değil'}
 $p.post++
 if($p.mod -eq 'post-hata'){throw 'Yanıt kaybı'}
 return [pscustomobject]@{islemId=$(if($p.mod -eq 'post-kimlik'){[Guid]::NewGuid()}else{$p.id});durum=$(if($p.mod -eq 'post-durum'){'Ayrildi'}else{'Iptal'})}
}
try {
 foreach($mod in @('sw','yazim','kaydedildi','onayli-yerel','dosya','dizin','yanlis-ad','get-kimlik','get-ad','onayli','get-hata','post-hata','post-kimlik','post-durum','basarili','zaten-yerel-iptal')){
  $alan=Join-Path $testKoku ([Guid]::NewGuid().ToString('N').Substring(0,6));New-Item -ItemType Directory -Path $alan | Out-Null
  $env:LOCALAPPDATA=$alan;$yerel=Join-Path $alan 'Pronom\SolidworksEtiketleme';$g=[Etiket.SolidWorks.KaliciIslemGunlugu]::new((Join-Path $yerel 'Islemler'))
  [IO.File]::WriteAllText((Join-Path $yerel 'ApiAdresi.txt'),'http://127.0.0.1:1/')
  $i=[Etiket.SolidWorks.KaydetmeIslemi]::new();$i.IslemId=[Guid]::NewGuid();$i.BelgeTuru='P';$i.KaydetmeBicimi=3;$i.Asama='BagimsizKuyrukta';$i.HedefKlasor=$alan;$i.AyrilanDosyaAdi='P00054.SLDPRT';$i.HedefDosya=Join-Path $alan $i.AyrilanDosyaAdi;$i.GercekDosya=$i.HedefDosya
  switch($mod){'yazim'{$i.Asama='BagimsizYazimBasladi'}'kaydedildi'{$i.Asama='Kaydedildi'}'onayli-yerel'{$i.Asama='Onaylandi'}'dosya'{[IO.File]::WriteAllText($i.GercekDosya,'korunacak')}'dizin'{New-Item -ItemType Directory -Path $i.GercekDosya|Out-Null}'zaten-yerel-iptal'{$i.Asama='Iptal';[IO.File]::WriteAllText($i.GercekDosya,'yeni sahibin dosyası')}}
  $g.Yaz($i);$ilk=$i.Asama
  $global:pronomKurtarma=@{mod=$mod;sw=($mod -eq 'sw');id=$i.IslemId;g=$g;cagri=0;post=0}
  $ad=if($mod -eq 'yanlis-ad'){'P00055.SLDPRT'}else{'P00054.SLDPRT'}
  $hata=$false;try{& (Join-Path $testKoku 'YazilmamisKuyruguIptalEt.ps1') -IslemId $i.IslemId -BeklenenDosyaAdi $ad|Out-Null}catch{$hata=$true}
  if($mod -in @('basarili','zaten-yerel-iptal')){Kontrol (!$hata -and $g.Oku($i.IslemId).Asama -eq 'Iptal') ('Başarılı veya idempotent iptal: '+$mod)}else{Kontrol $hata ('Güvenlik veya yanıt kaybı durmalı: '+$mod)}
  if($mod -like 'post-*'){
   Kontrol ($g.Oku($i.IslemId).Asama -eq 'KuyrukIptalBekliyor') 'Yanıt kaybında GUID ve iptal niyeti tutulmalı'
   $global:pronomKurtarma.mod='zaten-merkez-iptal'
   & (Join-Path $testKoku 'YazilmamisKuyruguIptalEt.ps1') -IslemId $i.IslemId -BeklenenDosyaAdi 'P00054.SLDPRT'|Out-Null
   Kontrol ($g.Oku($i.IslemId).Asama -eq 'Iptal') 'Aynı GUID ile kayıp yanıt toparlanmalı'
  }elseif($mod -notin @('basarili','zaten-yerel-iptal')){Kontrol ($g.Oku($i.IslemId).Asama -eq $ilk -and $global:pronomKurtarma.post -eq 0) 'Ret, günlüğü ve merkezi numarayı değiştirmemeli'}
  if($mod -eq 'zaten-yerel-iptal'){Kontrol ($global:pronomKurtarma.cagri -eq 0 -and [IO.File]::ReadAllText($i.GercekDosya) -eq 'yeni sahibin dosyası') 'Eski iptal tekrarı yeni sahibin dosyasına veya rezervasyonuna dokunmamalı'}
 }
 Write-Output ('GEÇTİ: '+$script:sayi+' kurtarma kontrolü; PowerShell '+$PSVersionTable.PSVersion+'; sahte HTTP, gerçek SQL/SolidWorks çağrısı yok.')
}finally{$env:LOCALAPPDATA=$eski}