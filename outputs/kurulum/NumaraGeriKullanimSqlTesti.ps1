param([string]$VeritabaniAdi=('SolidWorksAutomation_ApiTest_GeriNumara_'+(Get-Date -Format 'yyyyMMdd_HHmmss')))
$ErrorActionPreference='Stop'
if($VeritabaniAdi -notmatch '^SolidWorksAutomation_ApiTest_[A-Za-z0-9_]+$'){throw 'Yalnızca ayrı test veritabanı adı kullanılabilir.'}
$kok=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$derleme=Join-Path $kok 'work\geri-kullanim-build'
$eskiBaglanti=$env:ConnectionStrings__MerkeziVeritabani
$eskiRapor=$env:ETIKET_TEST_RAPORU
try{
 Write-Output ('Ayrı SQL test veritabanı: '+$VeritabaniAdi)
 & dotnet build (Join-Path $kok 'outputs\SolidWorksEtiketleme.slnx') -c Release --artifacts-path $derleme --ignore-failed-sources -m:1 -nr:false -p:UseSharedCompilation=false
 if($LASTEXITCODE -ne 0){throw 'Derleme başarısız; SQL testi başlatılmadı.'}
 $yerelAyar=Join-Path $kok 'outputs\src\Etiket.Api\appsettings.Local.json'
 $temelBaglanti=$eskiBaglanti
 if([string]::IsNullOrWhiteSpace($temelBaglanti)){
  if(!(Test-Path -LiteralPath $yerelAyar)){throw 'Yerel API bağlantı ayarı bulunamadı.'}
  $ayar=Get-Content -LiteralPath $yerelAyar -Raw | ConvertFrom-Json
  $temelBaglanti=$ayar.ConnectionStrings.MerkeziVeritabani
 }
 $baglanti=[System.Data.Common.DbConnectionStringBuilder]::new();$baglanti.set_ConnectionString($temelBaglanti)
 $null=$baglanti.Remove('Database');$null=$baglanti.Remove('Initial Catalog');$baglanti.set_Item('Initial Catalog',$VeritabaniAdi)
 $env:ConnectionStrings__MerkeziVeritabani=$baglanti.get_ConnectionString()
 $dogrulama=[System.Data.Common.DbConnectionStringBuilder]::new();$dogrulama.set_ConnectionString($env:ConnectionStrings__MerkeziVeritabani)
 if($dogrulama.get_Item('Initial Catalog') -cne $VeritabaniAdi){throw 'Test bağlantısının veritabanı hedefi doğrulanamadı; SQL testi başlatılmadı.'}
 Write-Output ('Doğrulanan SQL test hedefi: '+$VeritabaniAdi)
 $env:ETIKET_TEST_RAPORU=Join-Path $kok 'outputs\NUMARA-GERI-KULLANIM-SQL-TEST-SONUCU.txt'
 & dotnet (Join-Path $derleme 'bin\Etiket.Api.Testler\release\Etiket.Api.Testler.dll') --sql-kurulum
 if($LASTEXITCODE -ne 0){throw 'Gerçek SQL testi başarısız; yukarıdaki hata çıktısını paylaşın.'}
 Write-Output 'Gerçek SQL geri kullanım testi tamamlandı. Ana veritabanı değiştirilmedi.'
}finally{
 $env:ConnectionStrings__MerkeziVeritabani=$eskiBaglanti
 $env:ETIKET_TEST_RAPORU=$eskiRapor
}
