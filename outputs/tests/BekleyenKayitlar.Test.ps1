$ErrorActionPreference = 'Stop'
$kok = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$test = Join-Path $kok ('work\bekleyen-pencere-test\'+[Guid]::NewGuid().ToString('N'))
$eklenti = Join-Path $test 'Eklenti'
$null = New-Item -ItemType Directory -Path $eklenti -Force
foreach ($ad in @('BekleyenKayitlar.ps1','BekleyenKayitlarIslemleri.ps1')) {
    Copy-Item -LiteralPath (Join-Path $kok ('outputs\kurulum\'+$ad)) -Destination $eklenti
}
Copy-Item -LiteralPath (Join-Path $kok 'Eklenti\Pronom.SolidWorks.dll') -Destination $eklenti
. (Join-Path $eklenti 'BekleyenKayitlarIslemleri.ps1')
$onceki = $env:LOCALAPPDATA; $script:sayi = 0; $script:sw = $false
function Get-Process { param($Name,$ErrorAction) if ($script:sw) { [pscustomobject]@{ProcessName='SLDWORKS'} } }
function Kontrol($kosul,$metin) { if (!$kosul) { throw ('TEST: '+$metin) }; $script:sayi++ }
try {
    $env:LOCALAPPDATA = Join-Path $test 'yerel'
    Kontrol (@(Get-PronomBekleyenKayit $eklenti).Count -eq 0) 'Eksik günlükte boş liste'
    Kontrol (!(Test-Path -LiteralPath $env:LOCALAPPDATA)) 'Listeleme eksik günlük klasörü oluşturmaz'
    $yerel = Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme\Islemler'
    $gunluk = [Etiket.SolidWorks.KaliciIslemGunlugu]::new($yerel)
    foreach ($asama in @('Kaydedildi','OzelAdIptalBekliyor','NativeYazimBasladi','Ayrildi','Onaylandi','Iptal')) {
        $i = [Etiket.SolidWorks.KaydetmeIslemi]::new()
        $i.IslemId=[Guid]::NewGuid(); $i.Asama=$asama; $i.KaydetmeBicimi=4
        $i.AyrilanDosyaAdi='P00001.SLDPRT'; $i.BelgeTuru='P'
        $i.HedefKlasor=$test; $i.HedefDosya=Join-Path $test 'P00001.SLDPRT'; $i.GercekDosya=$i.HedefDosya
        if ($asama -in @('Kaydedildi','OzelAdIptalBekliyor')) {
            $i.KayitBoyutu=100; $i.KayitSHA256=('A'*64); $i.KayitKanitiZamani=[DateTimeOffset]::UtcNow.ToString('O')
        }
        $gunluk.Yaz($i)
    }
    $before = @(Get-ChildItem -LiteralPath $yerel -Filter '*.json' | Get-FileHash)
    $rows = @(Get-PronomBekleyenKayit $eklenti)
    Kontrol ($rows.Count -eq 4) 'Terminal işlemler listeden çıkar'
    Kontrol (@($rows | Where-Object ToparlamayaUygun).Count -eq 2) 'Yalnız kanıtlı iki sonuç uygun'
    Kontrol (($rows | Where-Object Asama -eq 'OzelAdIptalBekliyor').Durum -eq 'Özel ad sonucu bekliyor') 'Özel ad durumu anlaşılır'
    Kontrol (($rows | Where-Object Asama -eq 'NativeYazimBasladi').Aciklama -match 'inceleme') 'Belirsiz yazım inceleme gerektirir'
    $script:sw=$true
    Kontrol (@(Get-PronomBekleyenKayit $eklenti).Count -eq 4) 'SolidWorks açıkken salt listeleme çalışır'
    $id=$rows[0].IslemId
    $rejected=$false; try { Invoke-PronomSeciliToparlama $eklenti $id } catch { $rejected=$true }
    Kontrol $rejected 'SolidWorks açıkken toparlama reddedilir'
    $script:sw=$false
    $rejected=$false; try { Invoke-PronomSeciliToparlama $eklenti ([Guid]::Empty) } catch { $rejected=$true }
    Kontrol $rejected 'Boş kimlik reddedilir'
    $fake = 'param([Guid]$IslemId) $IslemId.ToString()'
    [IO.File]::WriteAllText((Join-Path $eklenti 'BekleyenKayitlariToparla.ps1'),$fake)
    Kontrol ((Invoke-PronomSeciliToparlama $eklenti $id) -eq $id.ToString()) 'Seçilen tek GUID mevcut yardımcıya aktarılır'
    foreach ($hash in $before) { Kontrol ((Get-FileHash -LiteralPath $hash.Path).Hash -eq $hash.Hash) 'Listeleme/ret/delegasyon günlük değiştirmez' }
    $preview = Join-Path $kok 'outputs\BEKLEYEN-KAYITLAR-ONIZLEME.png'
    & (Join-Path $eklenti 'BekleyenKayitlar.ps1') -OnizlemeDosyasi $preview
    Kontrol (Test-Path -LiteralPath $preview -PathType Leaf) 'Pencere görüntüsü üretildi'
    Copy-Item -LiteralPath (Join-Path $kok 'Eklenti\BekleyenKayitlariToparla.ps1') -Destination $eklenti -Force
    $terminal = @(Get-ChildItem -LiteralPath $yerel -Filter '*.json' | ForEach-Object {
        $gunluk.Oku([Guid]$_.BaseName)
    } | Where-Object Asama -eq 'Onaylandi')[0]
    Kontrol ((Invoke-PronomSeciliToparlama $eklenti $terminal.IslemId | Out-String) -match 'zaten tamamlanmış') 'Gerçek yardımcı terminal işlemi HTTP olmadan tanır'
    $kanitli = @($rows | Where-Object Asama -eq 'Kaydedildi')[0]
    [IO.File]::WriteAllText((Join-Path (Split-Path $yerel -Parent) 'ApiAdresi.txt'),'http://127.0.0.1:1/')
    [IO.File]::WriteAllText($kanitli.GercekDosya,'Kanıtla uyuşmayan izole test dosyası')
    $rejected=$false; try { Invoke-PronomSeciliToparlama $eklenti $kanitli.IslemId } catch { $rejected=$_.Exception.Message -match 'kanıt|eşleşmiyor' }
    Kontrol $rejected 'Gerçek yardımcı değiştirilmiş dosyayı HTTP öncesinde reddeder'
    [IO.File]::WriteAllText((Join-Path $yerel ($id.ToString()+'.json')),'bozuk')
    $rejected=$false; try { Get-PronomBekleyenKayit $eklenti } catch { $rejected=$true }
    Kontrol $rejected 'Bozuk günlük sessizce atlanmaz'
    Write-Output ('GEÇTİ: '+$script:sayi+' yardımcı pencere kontrolü. İzole günlükler ve sahte toparlama betiği; gerçek HTTP/CAD/SQL işlemi yok.')
} finally { $env:LOCALAPPDATA=$onceki; $script:sw=$false }
