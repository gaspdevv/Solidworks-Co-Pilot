$ErrorActionPreference='Stop'
$base='http://127.0.0.1:5082'
$health=Invoke-RestMethod -Uri "$base/api/health" -TimeoutSec 5
if($health.veritabani -ne [regex]::Unescape('Ba\u011fl\u0131')){throw 'API/SQL hazir degil.'}
function TumunuOku {
    $all=[Collections.Generic.List[object]]::new()
    $offset=0
    do {
        $page=Invoke-RestMethod -Uri "$base/api/documents?baslangic=$offset&adet=500" -TimeoutSec 10
        if($null -eq $page.toplam){throw 'Yeni liste surumu calismiyor. APIyi guncel kaynakla yeniden baslatin.'}
        $items=@($page.belgeler)
        foreach($item in $items){$all.Add($item)}
        $offset+=$items.Count
    }while($items.Count -gt 0 -and $offset -lt $page.toplam)
    return $all.ToArray()
}
function Assert($ok,[string]$message){if(!$ok){throw $message}}
$before=@(TumunuOku)
Assert ($before.Count -gt 0) 'Kontrol icin en az bir onayli belge gerekli.'
$first=Invoke-RestMethod -Uri "$base/api/documents" -TimeoutSec 10
Assert ($first.toplam -eq $before.Count -and @($first.belgeler).Count -eq [Math]::Min(100,$before.Count)) 'Toplam/ilk sayfa eslesmiyor.'
$last=$before|Sort-Object id|Select-Object -Last 1
$term=[Uri]::EscapeDataString($last.dosyaAdi.ToLowerInvariant())
$found=Invoke-RestMethod -Uri "$base/api/documents?arama=$term" -TimeoutSec 10
Assert ($found.toplam -eq 1 -and @($found.belgeler).Count -eq 1 -and $found.belgeler[0].id -eq $last.id) 'Tum kayitlarda arama son belgeyi bulamadi.'
foreach($type in @('P','M')){
    $expected=@($before|Where-Object { $_.dosyaAdi.StartsWith($type) }|Sort-Object id)
    $page=Invoke-RestMethod -Uri "$base/api/documents?tur=$type&baslangic=0&adet=5" -TimeoutSec 10
    Assert ($page.toplam -eq $expected.Count -and @($page.belgeler).Count -eq [Math]::Min(5,$expected.Count)) "Tur filtresi sayisi hatali: $type"
    Assert (@($page.belgeler|Where-Object { !$_.dosyaAdi.StartsWith($type) }).Count -eq 0) "Yanlis turde kayit: $type"
}
$opposite=if($last.dosyaAdi.StartsWith('P')){'M'}else{'P'}
$combined=Invoke-RestMethod -Uri "$base/api/documents?arama=$term&tur=$opposite" -TimeoutSec 10
Assert ($combined.toplam -eq 0 -and @($combined.belgeler).Count -eq 0) 'Arama ve tur birlikte uygulanmiyor.'
$literal=[Uri]::EscapeDataString("%_['")
$empty=Invoke-RestMethod -Uri "$base/api/documents?arama=$literal" -TimeoutSec 10
Assert ($empty.toplam -eq 0 -and @($empty.belgeler).Count -eq 0) 'Arama ozel karakterleri kalip gibi yorumluyor.'
if($before.Count -gt 100){
    $second=Invoke-RestMethod -Uri "$base/api/documents?baslangic=100&adet=100" -TimeoutSec 10
    Assert (@($second.belgeler).Count -eq [Math]::Min(100,$before.Count-100)) 'Ikinci sayfa hatali.'
}
$after=@(TumunuOku)
$difference=@(Compare-Object $before $after -Property id,dosyaAdi,tarih)
Assert ($difference.Count -eq 0) 'Kontrol sirasinda belge listesi degisti; ayrica inceleyin.'
Write-Output "GECTI: Gercek SQL/HTTP uzerinde toplam $($before.Count), tum kayitlarda arama, P/M filtresi, birlesik filtre, ozel karakter ve sayfalama."
Write-Output 'Yalnizca GET istekleri yapildi; CAD/SQL yazimi veya numara ayirma yok.'
