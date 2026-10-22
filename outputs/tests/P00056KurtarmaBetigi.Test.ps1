$ErrorActionPreference='Stop'
$kok=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testKoku=Join-Path $kok ('work\p56-test\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testKoku -Force | Out-Null
$betik=Join-Path $testKoku 'P00056YanlisAdKaydiniSonuclandir.ps1'
Copy-Item -LiteralPath (Join-Path $kok 'outputs\kurulum\P00056YanlisAdKaydiniSonuclandir.ps1') -Destination $betik
Copy-Item -LiteralPath (Join-Path $kok 'work\geri-kullanim-build\bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll') -Destination $testKoku
$null=[Reflection.Assembly]::LoadFrom((Join-Path $testKoku 'Pronom.SolidWorks.dll'))
$eski=$env:LOCALAPPDATA;$script:sayi=0
function Kontrol($kosul,$mesaj){if(!$kosul){throw ('TEST BAŞARISIZ: '+$mesaj)};$script:sayi++}
function Get-Process {param($Name,$ErrorAction) if($global:p56.sw){[pscustomobject]@{ProcessName='SLDWORKS'}}}
function Invoke-RestMethod {
 param($Method,$Uri,$TimeoutSec,$MaximumRedirection)
 $p=$global:p56;$p.cagri++
 if($Uri -notlike ('*'+$p.id+'*')){throw 'Başka GUID ile HTTP isteği gönderildi'}
 if($Method -eq 'Get'){
  if($p.mod -eq 'get-hata'){throw 'Bağlantı yok'}
  return [pscustomobject]@{islemId=$(if($p.mod -eq 'get-kimlik'){[Guid]::NewGuid()}else{$p.id});dosyaAdi=$(if($p.mod -eq 'get-ad'){'P00057.SLDPRT'}else{'P00056.SLDPRT'});durum=$(if($p.mod -eq 'onayli'){'Onaylandi'}elseif($p.mod -in @('merkez-iptal','iptal-yeni-sahip')){'Iptal'}else{'Ayrildi'})}
 }
 $p.post++
 if($p.g.Oku($p.id).Asama -ne 'NativeYanlisAdIptalBekliyor'){throw 'POST öncesi kalıcı niyet yok'}
 $kanit=Join-Path $p.yerel ('KurtarmaKanitlari\'+$p.id+'.json')
 if(!(Test-Path -LiteralPath $kanit)){throw 'POST öncesi kanıt yok'}
 if($p.mod -eq 'post-hata'){throw 'POST yanıt kaybı'}
 return [pscustomobject]@{islemId=$(if($p.mod -eq 'post-kimlik'){[Guid]::NewGuid()}else{$p.id});durum=$(if($p.mod -eq 'post-durum'){'Ayrildi'}else{'Iptal'})}
}
try {
 foreach($mod in @('sw','kimlik','tur','asama','hedef','dosya-yok','bos','yanlis-ad','log-yok','log-tur','log-guid','get-hata','get-kimlik','get-ad','onayli','merkez-iptal','post-hata','post-kimlik','post-durum','basarili')){
  $alan=Join-Path $testKoku $mod;New-Item -ItemType Directory -Path $alan | Out-Null
  $env:LOCALAPPDATA=Join-Path $alan 'local';$yerel=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
  $g=[Etiket.SolidWorks.KaliciIslemGunlugu]::new((Join-Path $yerel 'Islemler'))
  [IO.File]::WriteAllText((Join-Path $yerel 'ApiAdresi.txt'),'http://127.0.0.1:1/')
  $id=[Guid]'632e419d-b974-472a-96c1-9af00b77dceb'
  $i=[Etiket.SolidWorks.KaydetmeIslemi]::new();$i.IslemId=$id;$i.BelgeTuru='P';$i.KaydetmeBicimi=4;$i.Asama='NativeYazimBasladi';$i.HedefKlasor=$alan;$i.AyrilanDosyaAdi='P00056.SLDPRT';$i.HedefDosya=Join-Path $alan 'P00056.SLDPRT';$i.GercekDosya=$i.HedefDosya
  if($mod -eq 'tur'){$i.KaydetmeBicimi=3};if($mod -eq 'asama'){$i.Asama='Kaydedildi'}
  $g.Yaz($i);$ilk=$i.Asama
  $gercek=Join-Path $alan 'Parça1.SLDPRT';[IO.File]::WriteAllText($gercek,'Gerçek CAD olmayan korunacak test verisi')
  if($mod -eq 'hedef'){[IO.File]::WriteAllText($i.HedefDosya,'korunacak hedef')}
  if($mod -eq 'dosya-yok'){$gercek=Join-Path $alan 'eksik\Parça1.SLDPRT'}
  if($mod -eq 'bos'){[IO.File]::WriteAllText($gercek,'')}
  if($mod -eq 'yanlis-ad'){$gercek=Join-Path $alan 'Mil.SLDPRT';[IO.File]::WriteAllText($gercek,'koru')}
  $log=@("2026-10-15T09:08:07Z | StandartNativeKaydet | islem=$id hedef=$($i.HedefDosya)","2026-10-15T09:08:08Z | FileSavePostNotify | tur=P kaydetmeTuru=2 hedef=$gercek","2026-10-15T09:08:08Z | StandartKayitHatasi | islem=$id asama=NativeYazimBasladi System.IO.IOException: Hedef uyuşmuyor.","2026-10-15T09:08:17Z | CommandCloseNotify | komut=2 neden=-3")
  if($mod -eq 'log-yok'){$log=@('Eksik log')};if($mod -eq 'log-tur'){$log[1]=$log[1].Replace('kaydetmeTuru=2','kaydetmeTuru=1')};if($mod -eq 'log-guid'){$log[0]=$log[0].Replace($id.ToString(),[Guid]::NewGuid().ToString())}
  [IO.File]::WriteAllLines((Join-Path $yerel 'KayitOlayTanisi.log'),$log,[Text.UTF8Encoding]::new($false))
  $global:p56=@{mod=$mod;sw=($mod -eq 'sw');id=$id;g=$g;yerel=$yerel;cagri=0;post=0}
  $kimlik=if($mod -eq 'kimlik'){[Guid]::NewGuid()}else{$id}
  $ozet=if(Test-Path -LiteralPath $gercek){(Get-FileHash -LiteralPath $gercek).Hash}else{''}
  $hata=$false;try{& $betik -DosyadaOkunanIslemId $kimlik -GercekDosya $gercek | Out-Null}catch{$hata=$true}
  if($mod -eq 'basarili'){
   Kontrol (!$hata -and $g.Oku($id).Asama -eq 'Iptal' -and $global:p56.post -eq 1) 'Doğrulanmış olay tek iptal ile tamamlanmalı'
   Kontrol ($g.Oku($id).GercekDosya -eq $gercek) 'Gerçek yanlış adlı dosya son günlükte korunmalı'
   $global:p56.cagri=0
   [IO.File]::WriteAllText($i.HedefDosya,'Numaranın yeni sahibinin dosyası')
   & $betik -DosyadaOkunanIslemId $id -GercekDosya $gercek | Out-Null
   Kontrol ($global:p56.cagri -eq 0 -and [IO.File]::ReadAllText($i.HedefDosya) -eq 'Numaranın yeni sahibinin dosyası') 'Terminal tekrar yeni sahibin dosyasına/numarasına dokunmamalı'
  }else{Kontrol $hata ('Geçersiz durum veya kayıp yanıt korunmalı: '+$mod)}
  if($mod -like 'post-*'){
   Kontrol ($g.Oku($id).Asama -eq 'NativeYanlisAdIptalBekliyor') 'Kayıp yanıtta GUID/kalıcı iptal niyeti kalmalı'
   $global:p56.mod='basarili';$global:p56.post=0
   $oncekiIcerik=[IO.File]::ReadAllText($gercek);[IO.File]::AppendAllText($gercek,'sonradan değişti')
   $ret=$false;try{& $betik -DosyadaOkunanIslemId $id -GercekDosya $gercek | Out-Null}catch{$ret=$true}
   Kontrol ($ret -and $global:p56.post -eq 0 -and $g.Oku($id).Asama -eq 'NativeYanlisAdIptalBekliyor') 'Kanıt alındıktan sonra değişen CAD yeni POST gönderememeli'
   [IO.File]::WriteAllText($gercek,$oncekiIcerik)
   $kanitYolu=Join-Path $yerel ('KurtarmaKanitlari\'+$id+'.json');$oncekiKanit=[IO.File]::ReadAllText($kanitYolu)
   [IO.File]::WriteAllText($kanitYolu,$oncekiKanit.Replace($id.ToString(),[Guid]::NewGuid().ToString()))
   $ret=$false;try{& $betik -DosyadaOkunanIslemId $id -GercekDosya $gercek | Out-Null}catch{$ret=$true}
   Kontrol ($ret -and $global:p56.post -eq 0) 'Başka GUID ile değiştirilen kanıt iptal gönderememeli'
   [IO.File]::WriteAllText($kanitYolu,$oncekiKanit)
   $global:p56.mod='iptal-yeni-sahip';$global:p56.post=0
   [IO.File]::WriteAllText($i.HedefDosya,'Yeni sahibin korunacak dosyası')
   & $betik -DosyadaOkunanIslemId $id -GercekDosya $gercek | Out-Null
   Kontrol ($g.Oku($id).Asama -eq 'Iptal' -and $global:p56.post -eq 0) 'Merkez iptal ack tekrarı ikinci POST yapmamalı'
  }elseif($mod -ne 'basarili'){Kontrol ($g.Oku($id).Asama -eq $ilk -and $global:p56.post -eq 0) 'Reddedilen durum kalıcı aşamayı değiştirmemeli veya POST göndermemeli'}
  if($ozet){Kontrol ((Get-FileHash -LiteralPath $gercek).Hash -eq $ozet) 'Gerçek dosya içeriği hiçbir yolda değişmemeli'}
 }
 $rapor='GEÇTİ: '+$script:sayi+' P00056 kurtarma kontrolü. PowerShell '+$PSVersionTable.PSVersion+'. Sahte HTTP/izole dosyalar; gerçek SQL, CAD veya rezervasyon değiştirilmedi.'
 Write-Output $rapor
 [IO.File]::WriteAllText((Join-Path $kok 'outputs\P00056-KURTARMA-TEST-SONUCU.txt'),$rapor+"`r`n"+[DateTimeOffset]::UtcNow.ToString('O'),[Text.UTF8Encoding]::new($false))
}finally{$env:LOCALAPPDATA=$eski}
