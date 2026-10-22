param(
 [Parameter(Mandatory=$true)][Guid]$DosyadaOkunanIslemId,
 [Parameter(Mandatory=$true)][string]$GercekDosya
)
$ErrorActionPreference='Stop'
# Yalnızca kullanıcıyla birlikte doğrulanan sürüm17 olayı; genel belirsiz yazım iptali değildir.
$id=[Guid]'632e419d-b974-472a-96c1-9af00b77dceb'
if($DosyadaOkunanIslemId -ne $id){throw 'Parça1 içinden okunan işlem kimliği bu olayla eşleşmiyor.'}
function SolidWorksKapaliMi {
 if(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue){throw 'Önce SolidWorks tamamen kapatılmalı. İptal gönderilmedi.'}
}
SolidWorksKapaliMi
$dll=Join-Path $PSScriptRoot 'Pronom.SolidWorks.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Betiği mevcut Eklenti klasörüne kopyalayıp oradan çalıştırın.'}
Add-Type -AssemblyName System.Runtime.Serialization
$null=[Reflection.Assembly]::LoadFrom($dll)
$yerel=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$gunluk=[Etiket.SolidWorks.KaliciIslemGunlugu]::new((Join-Path $yerel 'Islemler'))
$i=$gunluk.Oku($id)
if($i.KaydetmeBicimi -ne 4 -or $i.BelgeTuru -ne 'P' -or $i.AyrilanDosyaAdi -cne 'P00056.SLDPRT' -or $i.KaynakDosya){throw 'Yerel işlem bu ilk kayıt olayıyla eşleşmiyor.'}
if($i.Asama -eq 'Iptal'){Write-Output 'P00056 işlemi zaten iptal edilmiş; HTTP veya CAD işlemi yapılmadı.';return}
if($i.Asama -notin @('NativeYazimBasladi','NativeYanlisAdIptalBekliyor')){throw ('Bu aşama için kurtarma yapılamaz: '+$i.Asama)}
if(![IO.Path]::IsPathRooted($GercekDosya)){throw 'Gerçek dosyanın tam yolu gerekli.'}
$gercek=[IO.Path]::GetFullPath($GercekDosya)
$beklenen=Join-Path $i.HedefKlasor 'P00056.SLDPRT'
if(![Etiket.SolidWorks.DosyaAdiKurali]::AyniYol($i.HedefDosya,$beklenen) -or
 ![Etiket.SolidWorks.DosyaAdiKurali]::AyniYol($i.GercekDosya,$beklenen) -or
 ![Etiket.SolidWorks.DosyaAdiKurali]::AyniYol([IO.Path]::GetDirectoryName($gercek),$i.HedefKlasor) -or
 [IO.Path]::GetFileName($gercek) -cne 'Parça1.SLDPRT'){throw 'Bu yardımcı yalnızca aynı klasördeki Parça1/P00056 olayı içindir.'}
$kanitKlasoru=Join-Path $yerel 'KurtarmaKanitlari'
$kanitYolu=Join-Path $kanitKlasoru ($id.ToString('D')+'.json')
$kanit=$null
if(Test-Path -LiteralPath $kanitYolu){$kanit=Get-Content -LiteralPath $kanitYolu -Raw -Encoding UTF8 | ConvertFrom-Json}
function KanitiDogrula {
 if(!$kanit -or [string]$kanit.IslemId -ne $id.ToString('D') -or
 [string]$kanit.DosyadaOkunanIslemId -ne $id.ToString('D') -or
 ![Etiket.SolidWorks.DosyaAdiKurali]::AyniYol([string]$kanit.GercekDosya,$gercek) -or
 ![Etiket.SolidWorks.DosyaAdiKurali]::AyniYol([string]$kanit.BeklenenDosya,$beklenen) -or
 [string]$kanit.SHA256 -notmatch '^[A-F0-9]{64}$'){throw 'Kurtarma kanıtı eksik veya uyuşmuyor; işlem korundu.'}
}
if($i.Asama -eq 'NativeYanlisAdIptalBekliyor'){KanitiDogrula}
$metin=[IO.File]::ReadAllText((Join-Path $yerel 'ApiAdresi.txt')).Trim().TrimEnd('/')+'/'
$adres=$null
if(![Uri]::TryCreate($metin,[UriKind]::Absolute,[ref]$adres) -or $adres.Scheme -notin @('http','https') -or $adres.UserInfo -or $adres.Query -or $adres.Fragment){throw 'API adresi geçersiz.'}
$uri=[Uri]::new($adres,('api/rezervasyonlar/'+$id.ToString('D')))
$merkez=Invoke-RestMethod -Method Get -Uri $uri.AbsoluteUri -TimeoutSec 10 -MaximumRedirection 0
if([string]$merkez.islemId -ne $id.ToString('D') -or [string]$merkez.dosyaAdi -cne 'P00056.SLDPRT' -or [string]$merkez.durum -notin @('Ayrildi','Iptal')){throw 'Merkezi kimlik/ad/durum uyuşmuyor; iptal gönderilmedi.'}
if($merkez.durum -eq 'Iptal'){
 if($i.Asama -ne 'NativeYanlisAdIptalBekliyor'){throw 'Merkez zaten iptal; yerel doğrulanmış kurtarma niyeti yok. İnceleme gerekli.'}
 # Yanıt kaybı sonrası yeni sahibi P00056 oluşturmuş olabilir. Yalnızca eski GUID sonucu alınır.
 SolidWorksKapaliMi
 $i.Asama='Iptal';$i.GercekDosya=$gercek;$gunluk.Yaz($i)
 Write-Output 'P00056 iptal sonucu aynı işlem kimliğiyle tamamlandı; yeniden iptal isteği gönderilmedi.'
 return
}
$surucu=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($gercek))
if(!$surucu.IsReady -or $surucu.DriveType -ne [IO.DriveType]::Fixed){throw 'Yalnızca erişilebilir yerel sabit disk desteklenir.'}
# Yeniden yönlendirilmiş dosya/üst klasör kabul edilmez.
$oge=Get-Item -LiteralPath $gercek -Force -ErrorAction Stop
if($oge.PSIsContainer -or $oge.Length -le 0){throw 'Gerçek CAD dosyası eksik/boş.'}
while($oge){
 if($oge.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Yeniden yönlendirilmiş yol desteklenmiyor.'}
 $ust=[IO.Directory]::GetParent($oge.FullName);if(!$ust){break};$oge=Get-Item -LiteralPath $ust.FullName -Force -ErrorAction Stop
}
function HedefYokMu {if(Test-Path -LiteralPath $beklenen -ErrorAction Stop){throw 'P00056 hedefi mevcut; otomatik iptal yapılmadı.'}}
HedefYokMu
# Dosya salt okunur açılır; işlem boyunca başka süreçten yazma/silme engellenir.
$kilit=[IO.File]::Open($gercek,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try {
 $sha=[Security.Cryptography.SHA256]::Create()
 try{$ozet=[BitConverter]::ToString($sha.ComputeHash($kilit)).Replace('-','')}finally{$sha.Dispose()}
 if($kanit){KanitiDogrula;if($kanit.SHA256 -ne $ozet){throw 'Gerçek dosya önceki kurtarma kanıtından sonra değişti.'}}
 # Özgün native komut -> gerçek SaveAs post -> aynı GUID'nin hedef hatası birlikte aranır.
 $basladi=$false;$post=$null;$dogrulandi=$false
 foreach($satir in Get-Content -LiteralPath (Join-Path $yerel 'KayitOlayTanisi.log') -Encoding UTF8){
  $parcalar=$satir.Split([string[]]@(' | '),3,[StringSplitOptions]::None)
  if($parcalar.Count -ne 3){continue}
  $olay=$parcalar[1];$veri=$parcalar[2]
  if($olay -eq 'StandartNativeKaydet' -and $veri -eq ('islem='+$id.ToString('D')+' hedef='+$beklenen)){$basladi=$true;$post=$null;continue}
  if(!$basladi){continue}
  if($olay -eq 'CommandCloseNotify' -or $olay -eq 'NativeAdAyrildi'){$basladi=$false;continue}
  if($olay -eq 'FileSavePostNotify'){
   if($veri -ne ('tur=P kaydetmeTuru=2 hedef='+$gercek)){throw 'İlk kayıt sonrası hedef/tür bu olayla uyuşmuyor.'}
   $post=$satir
  }
  if($post -and $olay -eq 'StandartKayitHatasi' -and $veri.StartsWith(('islem='+$id.ToString('D')+' asama=NativeYazimBasladi '),[StringComparison]::Ordinal)){$dogrulandi=$true;break}
 }
 if(!$dogrulandi){throw 'Özgün kayıt sonrası kanıt bulunamadı; iptal gönderilmedi.'}
 SolidWorksKapaliMi;HedefYokMu
 if(!$kanit){
  New-Item -ItemType Directory -Path $kanitKlasoru -Force | Out-Null
  $kanit=[pscustomobject]@{IslemId=$id.ToString('D');DosyadaOkunanIslemId=$DosyadaOkunanIslemId.ToString('D');KimlikKaniti='Kullanıcının SolidWorks Özel özellikler ekranından okuduğu değer';GercekDosya=$gercek;BeklenenDosya=$beklenen;SHA256=$ozet;NativePost=$post;Api=$uri.AbsoluteUri;Tarih=[DateTimeOffset]::UtcNow.ToString('O')}
  $bayt=[Text.UTF8Encoding]::new($false).GetBytes(($kanit | ConvertTo-Json -Depth 5))
  $akis=[IO.File]::Open($kanitYolu,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
  try{$akis.Write($bayt,0,$bayt.Length);$akis.Flush($true)}finally{$akis.Dispose()}
 }
 $yedek=Join-Path $kanitKlasoru ($id.ToString('D')+'-once.json')
 if(!(Test-Path -LiteralPath $yedek)){[IO.File]::Copy((Join-Path $yerel ('Islemler\'+$id.ToString('D')+'.json')),$yedek,$false)}
 # İptal niyeti POST'tan önce atomik günlük yazımıyla saklanır.
 $i.Asama='NativeYanlisAdIptalBekliyor';$gunluk.Yaz($i)
 SolidWorksKapaliMi;HedefYokMu
 $yanit=Invoke-RestMethod -Method Post -Uri ($uri.AbsoluteUri+'/iptal') -TimeoutSec 10 -MaximumRedirection 0
 if([string]$yanit.islemId -ne $id.ToString('D') -or [string]$yanit.durum -ne 'Iptal'){throw 'İptal yanıtı doğrulanamadı; aynı kimlik beklemede tutuldu.'}
 $i.GercekDosya=$gercek;$i.Asama='Iptal';$gunluk.Yaz($i)
 Write-Output 'P00056 rezervasyonu iptal edildi; kullanılmayan numara yeniden kullanılabilir.'
 Write-Output ('Parça1.SLDPRT korundu. SHA256: '+$ozet)
 Write-Output 'Web belgesi oluşturulmadı. Günlük ve kurtarma kanıtı korundu; CAD dosyası yazılmadı/silinmedi.'
}finally{$kilit.Dispose()}
