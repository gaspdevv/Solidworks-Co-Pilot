function Get-PronomBekleyenKayit {
    param([Parameter(Mandatory=$true)][string]$EklentiKlasoru)
    $dll = Join-Path $EklentiKlasoru 'Pronom.SolidWorks.dll'
    if (!(Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Mevcut Pronom Eklenti klasörü bulunamadı.' }
    Add-Type -AssemblyName System.Runtime.Serialization
    $null = [Reflection.Assembly]::LoadFrom($dll)
    $klasor = Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme\Islemler'
    if (!(Test-Path -LiteralPath $klasor -PathType Container)) { return }
    $gunluk = [Etiket.SolidWorks.KaliciIslemGunlugu]::new($klasor)
    foreach ($islem in $gunluk.BekleyenleriOku()) {
        $uygun = [Etiket.SolidWorks.KayitSonucuToparlama]::Uygun($islem)
        $durum = switch ($islem.Asama) {
            'Kaydedildi' { 'Merkezi onay bekliyor' }
            'OzelAdIptalBekliyor' { 'Özel ad sonucu bekliyor' }
            'BagimsizKuyrukta' { 'Kaydetme kuyruğunda' }
            'Ayrildi' { 'Ad ayrılmış' }
            default { 'İnceleme gerekiyor' }
        }
        $aciklama = if ($uygun) {
            'Kayıt kanıtı mevcut. Toparlama sırasında dosya yeniden doğrulanır.'
        } else {
            'Bu işlem otomatik toparlamaya uygun değil. Kaydı koruyun; işlem kimliğiyle inceleme gerekir.'
        }
        [pscustomobject]@{
            IslemId = $islem.IslemId
            DosyaAdi = $islem.AyrilanDosyaAdi
            Durum = $durum
            Asama = $islem.Asama
            GercekDosya = $islem.GercekDosya
            HedefDosya = $islem.HedefDosya
            ToparlamayaUygun = $uygun
            Aciklama = $aciklama
        }
    }
}

function Invoke-PronomSeciliToparlama {
    param([Parameter(Mandatory=$true)][string]$EklentiKlasoru,
          [Parameter(Mandatory=$true)][Guid]$IslemId)
    if ($IslemId -eq [Guid]::Empty) { throw 'Önce bir işlem seçin.' }
    if (Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue) {
        throw 'Önce açık belgelerinizi kontrol ederek SolidWorksü tamamen kapatın, ardından listeyi yenileyin.'
    }
    $betik = Join-Path $EklentiKlasoru 'BekleyenKayitlariToparla.ps1'
    if (!(Test-Path -LiteralPath $betik -PathType Leaf)) { throw 'Mevcut güvenli toparlama betiği bulunamadı.' }
    # Aynı GUID; kanıt/yol/hash ve terminal tekrar kontrolleri mevcut yardımcıda kalır.
    & $betik -IslemId $IslemId
}
