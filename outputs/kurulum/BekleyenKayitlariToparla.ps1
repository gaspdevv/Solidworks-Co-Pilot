param([Guid]$IslemId=[Guid]::Empty)
$ErrorActionPreference='Stop'
if(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue){throw 'Önce SolidWorks tamamen kapatılmalı. Günlük/merkezi sonuç değiştirilmedi.'}
$dll=Join-Path $PSScriptRoot 'Pronom.SolidWorks.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Bu betiği sürüm20 Eklenti klasöründen çalıştırın.'}
Add-Type -AssemblyName System.Runtime.Serialization
$null=[Reflection.Assembly]::LoadFrom($dll)
$yerel=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$gunluk=[Etiket.SolidWorks.KaliciIslemGunlugu]::new((Join-Path $yerel 'Islemler'))
if($IslemId -eq [Guid]::Empty){
 $liste=@($gunluk.BekleyenleriOku() | Where-Object {$_.KaydetmeBicimi -in @(3,4)} | ForEach-Object {
  [pscustomobject]@{IslemId=$_.IslemId;Asama=$_.Asama;Dosya=$_.GercekDosya;ToparlamayaUygun=[Etiket.SolidWorks.KayitSonucuToparlama]::Uygun($_)}
 })
 if($liste.Count -eq 0){Write-Output 'Bekleyen standart/bağımsız kayıt işlemi yok.'}else{$liste | Format-List}
 Write-Output 'Bu listeleme dosya kaydetmez, HTTP isteği göndermez ve numara ayırmaz.'
 return
}
$i=$gunluk.Oku($IslemId)
if($i.Asama -in @('Onaylandi','Iptal')){Write-Output ('İşlem zaten tamamlanmış: '+$i.Asama+'. HTTP/CAD işlemi yapılmadı.');return}
if(![Etiket.SolidWorks.KayitSonucuToparlama]::Uygun($i)){throw ('Kalıcı başarılı kayıt kanıtı yok veya aşama uygun değil: '+$i.Asama+'. İşlem korundu; elle inceleme gerekli.')}
$metin=[IO.File]::ReadAllText((Join-Path $yerel 'ApiAdresi.txt')).Trim().TrimEnd('/')+'/'
$adres=$null
if(![Uri]::TryCreate($metin,[UriKind]::Absolute,[ref]$adres) -or $adres.Scheme -notin @('http','https') -or $adres.UserInfo -or $adres.Query -or $adres.Fragment){throw 'API adresi geçersiz; işlem korundu.'}
$servis=[Etiket.SolidWorks.HttpAdServisi]::new($adres)
try{
 if(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue){throw 'SolidWorks açılmış; önce kapatılmalı.'}
 $yonetici=[Etiket.SolidWorks.KaydetmeIslemYoneticisi]::new($servis,$gunluk)
 $toparlama=[Etiket.SolidWorks.KayitSonucuToparlama]::new($gunluk,$yonetici)
 $sonuc=$toparlama.Tamamla($IslemId)
 $sonuc | Select-Object IslemId,Asama,AyrilanDosyaAdi,GercekDosya | Format-List
 Write-Output 'Aynı işlem kimliğinin merkezi sonucu tamamlandı. CAD yeniden yazılmadı; yeni numara ayrılmadı.'
}finally{$servis.Dispose()}
