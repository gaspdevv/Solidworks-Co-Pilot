$ErrorActionPreference='Stop'
$kok=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$test=Join-Path $kok ('work\toparla-ps\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $test -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $kok 'outputs\kurulum\BekleyenKayitlariToparla.ps1') -Destination $test
Copy-Item -LiteralPath (Join-Path $kok 'work\geri-kullanim-build\bin\Etiket.SolidWorks\release\Pronom.SolidWorks.dll') -Destination $test
$null=[Reflection.Assembly]::LoadFrom((Join-Path $test 'Pronom.SolidWorks.dll'))
$eski=$env:LOCALAPPDATA;$script:sayi=0;$global:toparlaSw=$false
function Get-Process {param($Name,$ErrorAction) if($global:toparlaSw){[pscustomobject]@{ProcessName='SLDWORKS'}}}
function Kontrol($durum,$metin){if(!$durum){throw ('TEST BAŞARISIZ: '+$metin)};$script:sayi++}
try{
 foreach($mod in @('sw','bos','liste-belirsiz','liste-kanitli','terminal','kanitsiz','bozuk','degisik')){
  $env:LOCALAPPDATA=Join-Path $test $mod;$yerel=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
  $g=[Etiket.SolidWorks.KaliciIslemGunlugu]::new((Join-Path $yerel 'Islemler'))
  [IO.File]::WriteAllText((Join-Path $yerel 'ApiAdresi.txt'),'http://127.0.0.1:1/')
  $i=[Etiket.SolidWorks.KaydetmeIslemi]::new();$i.IslemId=[Guid]::NewGuid();$i.KaydetmeBicimi=4;$i.BelgeTuru='P';$i.AyrilanDosyaAdi='P00001.SLDPRT';$i.Asama='NativeYazimBasladi';$i.HedefKlasor=$yerel;$i.HedefDosya=Join-Path $yerel $i.AyrilanDosyaAdi;$i.GercekDosya=$i.HedefDosya
  [IO.File]::WriteAllText($i.GercekDosya,'Gerçek CAD olmayan test dosyası')
  if($mod -in @('liste-kanitli','degisik')){[Etiket.SolidWorks.KayitDosyaKaniti]::Sakla($i,$i.GercekDosya);$i.Asama='Kaydedildi'}
  if($mod -eq 'terminal'){$i.Asama='Onaylandi'}
  if($mod -eq 'kanitsiz'){$i.Asama='Kaydedildi'}
  if($mod -ne 'bos'){$g.Yaz($i)}
  if($mod -eq 'degisik'){[IO.File]::AppendAllText($i.GercekDosya,' değiştirilmiş')}
  $yol=Join-Path $yerel ('Islemler\'+$i.IslemId+'.json')
  if($mod -eq 'bozuk'){[IO.File]::WriteAllText($yol,'geçersiz JSON')}
  $once=if(Test-Path -LiteralPath $yol){[IO.File]::ReadAllText($yol)}else{''}
  $global:toparlaSw=$mod -eq 'sw';$hata=$false;$cikti=''
  try{
   if($mod -in @('terminal','kanitsiz','degisik')){$cikti=& (Join-Path $test 'BekleyenKayitlariToparla.ps1') -IslemId $i.IslemId | Out-String}
   else{$cikti=& (Join-Path $test 'BekleyenKayitlariToparla.ps1') | Out-String}
  }catch{$hata=$true}
  Kontrol ($hata -eq ($mod -in @('sw','kanitsiz','bozuk','degisik'))) ('Betik doğru yerde durmalı: '+$mod)
  if($mod -eq 'liste-kanitli'){Kontrol ($cikti -match 'ToparlamayaUygun\s*:\s*True') 'Kanıtlı bekleyen sonuç salt okunur listelenmeli'}
  if($mod -eq 'liste-belirsiz'){Kontrol ($cikti -match 'ToparlamayaUygun\s*:\s*False') 'Belirsiz native yazım uygun sayılmamalı'}
  if($once){Kontrol ([IO.File]::ReadAllText($yol) -ceq $once) ('Listeleme/ret/terminal günlük yazmamalı: '+$mod)}
 }
 $rapor='GEÇTİ: '+$script:sayi+' toparlanma betiği kontrolü; Windows PowerShell '+$PSVersionTable.PSVersion+'. İzole dosyalar; gerçek SQL/HTTP/SolidWorks çağrısı yok.'
 Write-Output $rapor
 [IO.File]::WriteAllText((Join-Path $kok 'outputs\TOPARLANMA-BETIK-TEST-SONUCU.txt'),$rapor+"`r`n"+[DateTimeOffset]::UtcNow.ToString('O'),[Text.UTF8Encoding]::new($false))
}finally{$env:LOCALAPPDATA=$eski;$global:toparlaSw=$false}
