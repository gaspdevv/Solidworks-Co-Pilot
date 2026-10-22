param(
 [Parameter(Mandatory=$true)][Guid]$IslemId,
 [Parameter(Mandatory=$true)][string]$BeklenenDosyaAdi
)
$ErrorActionPreference='Stop'
if($IslemId -eq [Guid]::Empty){throw 'Geçerli işlem kimliği gerekli.'}
function SolidWorksKapaliMi {
 if(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue){throw 'Önce SolidWorks tamamen kapatılmalı. Günlük değiştirilmedi.'}
}
SolidWorksKapaliMi
$dll=Join-Path $PSScriptRoot 'Pronom.SolidWorks.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Bu betiği güncel Eklenti paketinin içinden çalıştırın.'}
Add-Type -AssemblyName System.Runtime.Serialization
$null=[Reflection.Assembly]::LoadFrom($dll)
$klasor=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$gunluk=[Etiket.SolidWorks.KaliciIslemGunlugu]::new((Join-Path $klasor 'Islemler'))
$islem=$gunluk.Oku($IslemId)
if($islem.KaydetmeBicimi -ne [Etiket.SolidWorks.KaydetmeBicimi]::BagimsizBilesen -or
 ![Etiket.SolidWorks.DosyaAdiKurali]::Gecerli($BeklenenDosyaAdi,$islem.BelgeTuru) -or
 ![string]::Equals($islem.AyrilanDosyaAdi,$BeklenenDosyaAdi,[StringComparison]::OrdinalIgnoreCase)){
 throw 'İşlem türü veya beklenen otomatik ad uyuşmuyor; değişiklik yapılmadı.'
}
if($islem.Asama -eq 'Iptal'){Write-Output ('İşlem zaten iptal edilmiş: '+$IslemId);return}
if($islem.Asama -notin @('BagimsizKuyrukta','KuyrukIptalBekliyor')){
 throw ('Yalnızca yazımı başlamamış kuyruk iptal edilebilir. Mevcut aşama: '+$islem.Asama)
}
if([string]::IsNullOrWhiteSpace($islem.GercekDosya) -or ![IO.Path]::IsPathRooted($islem.GercekDosya) -or
 ![Etiket.SolidWorks.DosyaAdiKurali]::AyniYol([IO.Path]::GetDirectoryName($islem.GercekDosya),$islem.HedefKlasor)){
 throw 'Günlükte gerçek hedef yolu doğrulanamadı; işlem korundu.'
}
$surucu=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($islem.GercekDosya))
if(!$surucu.IsReady -or $surucu.DriveType -ne [IO.DriveType]::Fixed){throw 'Bu yardımcı yalnızca erişilebilir yerel diskteki hedefler içindir; numara korundu.'}
$hedefKlasor=Get-Item -LiteralPath ([IO.Path]::GetDirectoryName($islem.GercekDosya)) -ErrorAction Stop
if(!$hedefKlasor.PSIsContainer){throw 'Hedef klasör doğrulanamadı; numara korundu.'}
function HedefYokMu {
 # Erişim hatası da iptali durdurur; ulaşılamayan hedef yok sayılmaz.
 $var=Test-Path -LiteralPath $islem.GercekDosya -ErrorAction Stop
 if($var){throw 'Hedef dosya/klasör mevcut. Yazılmadığı doğrulanamadığı için işlem iptal edilmedi.'}
}
HedefYokMu
$adresDosyasi=Join-Path $klasor 'ApiAdresi.txt'
$metin=[IO.File]::ReadAllText($adresDosyasi).Trim().TrimEnd('/')+'/'
$adres=$null
if(![Uri]::TryCreate($metin,[UriKind]::Absolute,[ref]$adres) -or $adres.Scheme -notin @('http','https') -or
 $adres.UserInfo -or $adres.Query -or $adres.Fragment){throw 'Yerel API adresi geçersiz; işlem korundu.'}
$uri=[Uri]::new($adres,('api/rezervasyonlar/'+$IslemId.ToString('D')))
$merkez=Invoke-RestMethod -Method Get -Uri $uri.AbsoluteUri -TimeoutSec 10 -MaximumRedirection 0
if([string]$merkez.islemId -ne $IslemId.ToString('D') -or
 ![string]::Equals([string]$merkez.dosyaAdi,$BeklenenDosyaAdi,[StringComparison]::OrdinalIgnoreCase) -or
 [string]$merkez.durum -notin @('Ayrildi','Iptal')){
 throw 'Merkezi işlem kimliği, adı veya durumu uyuşmuyor. İptal gönderilmedi.'
}
SolidWorksKapaliMi
HedefYokMu
$islem.Asama='KuyrukIptalBekliyor';$gunluk.Yaz($islem)
# Niyet kalıcıdır; HTTP yanıtı kaybolursa aynı kimlikle yeniden çalıştırılabilir.
$yanit=Invoke-RestMethod -Method Post -Uri ($uri.AbsoluteUri+'/iptal') -TimeoutSec 10 -MaximumRedirection 0
if([string]$yanit.islemId -ne $IslemId.ToString('D') -or [string]$yanit.durum -ne 'Iptal'){
 throw 'İptal yanıtı doğrulanamadı; aynı işlem kimliği beklemede tutuldu.'
}
$islem.Asama='Iptal';$gunluk.Yaz($islem)
Write-Output ('Yazımı başlamamış kuyruk iptal edildi: '+$IslemId)
Write-Output ('Serbest bırakılan otomatik ad: '+$BeklenenDosyaAdi)
Write-Output 'Günlük silinmedi. CAD dosyası oluşturulmadı; aynı kimlikle iptal sonucu kaydedildi.'