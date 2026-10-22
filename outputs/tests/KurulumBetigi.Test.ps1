# Sentetik bağlantılar ve sahte dotnet ile betik regresyonu; SQL sunucusuna bağlanmaz.
$ErrorActionPreference='Stop'
$kok=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$hedefBetik=Join-Path $kok 'outputs\kurulum\NumaraGeriKullanimSqlTesti.ps1'
$onceki=$env:ConnectionStrings__MerkeziVeritabani
$oncekiRapor=$env:ETIKET_TEST_RAPORU
$global:pronomTestAdet=0
$global:pronomTestHata=''
function dotnet {
 if($args[0] -eq 'build') { if($global:pronomTestHata -eq 'build'){$global:LASTEXITCODE=1}else{$global:LASTEXITCODE=0}; return }
 $b=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($env:ConnectionStrings__MerkeziVeritabani)
 if($b.get_InitialCatalog() -cne 'SolidWorksAutomation_ApiTest_BetikDogrulama'){throw 'Yanlış test hedefi!'}
 if($b.get_DataSource() -ne 'localhost'){throw 'Sunucu değişti!'}
 $global:pronomTestAdet++
 if($global:pronomTestHata -eq 'test'){$global:LASTEXITCODE=1}else{$global:LASTEXITCODE=0}
}
try {
 foreach($anahtar in @('Database','Initial Catalog')) {
  foreach($global:pronomTestHata in @('','build','test')) {
   $sentetik='Server=localhost;'+$anahtar+'=SolidWorksAutomation;Integrated Security=True'
   $env:ConnectionStrings__MerkeziVeritabani=$sentetik
   $env:ETIKET_TEST_RAPORU='OncekiRapor'
   $yakalanan=$false
   try { & $hedefBetik -VeritabaniAdi 'SolidWorksAutomation_ApiTest_BetikDogrulama' | Out-Null } catch { $yakalanan=$true }
   if($yakalanan -ne ($global:pronomTestHata -ne '')){throw 'Beklenmeyen hata davranışı!'}
   if($env:ConnectionStrings__MerkeziVeritabani -cne $sentetik -or $env:ETIKET_TEST_RAPORU -cne 'OncekiRapor'){throw 'Ortam ayarları geri yüklenmedi!'}
  }
 }
 $reddedildi=$false
 try { & $hedefBetik -VeritabaniAdi 'SolidWorksAutomation' | Out-Null } catch { $reddedildi=$true }
 if(!$reddedildi -or $global:pronomTestAdet -ne 4){throw 'Ana veritabanı koruması veya test çağrısı sayısı yanlış!'}
 Write-Output ('GEÇTİ: 7 betik senaryosu; PowerShell '+$PSVersionTable.PSVersion+'; gerçek SQL/derleme çalıştırılmadı.')
}finally{
 $env:ConnectionStrings__MerkeziVeritabani=$onceki
 $env:ETIKET_TEST_RAPORU=$oncekiRapor
}