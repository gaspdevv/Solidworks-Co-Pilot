param([switch]$SadeceKontrol)
$ErrorActionPreference='Stop'
$kok=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sqlKlasoru=Join-Path $kok 'Merkez\Sql'
$kurulum=Join-Path $sqlKlasoru '006-temiz-kullanim-kur.sql'
if (!(Test-Path -LiteralPath $kurulum -PathType Leaf)) { throw 'Dell Merkez/Sql kurulum dosyası bulunamadı.' }
$merkezAyari=Get-Content -LiteralPath (Join-Path $kok 'Merkez\merkez-ayar.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($merkezAyari.Ortam -cne 'Test' -or $merkezAyari.Veritabani -cnotmatch '^SolidWorksAutomation_ApiTest_[A-Za-z0-9_]+$') {
    throw 'Bu hazırlık betiği yalnız merkez hâlâ Test ortamına ayarlıyken çalışır. Canlı kullanımda veritabanı hazırlığı yapılmadı.'
}
$b=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$b.set_DataSource('localhost'); $b.set_InitialCatalog('master')
$b.set_IntegratedSecurity($true); $b.set_Encrypt($true); $b.set_TrustServerCertificate($true)
$b.set_ConnectTimeout(10)
$baglanti=[System.Data.SqlClient.SqlConnection]::new($b.get_ConnectionString())
function Invoke-DellSql([string]$Metin,[hashtable]$Degerler=@{},[switch]$Skaler) {
    $cmd=$baglanti.CreateCommand(); $cmd.CommandText=$Metin; $cmd.CommandTimeout=180
    try {
        foreach ($ad in $Degerler.Keys) { $null=$cmd.Parameters.AddWithValue($ad,$Degerler[$ad]) }
        if ($Skaler) { return $cmd.ExecuteScalar() }
        $null=$cmd.ExecuteNonQuery()
    } finally { $cmd.Dispose() }
}
function Assert-DellBosVeritabani([string]$Ad) {
    if ($Ad -notmatch '^Pronom_(Kullanim|GeriYuklemeKontrol_[a-f0-9]{8})$') { throw 'Beklenmeyen veritabanı adı.' }
    $kontrol=@"
USE [$Ad];
IF OBJECT_ID(N'dbo.Documents',N'U') IS NULL OR OBJECT_ID(N'dbo.Rezervasyonlar',N'U') IS NULL
 THROW 51210,N'Kullanım şeması eksik; otomatik değiştirilmedi.',1;
IF EXISTS(SELECT 1 FROM dbo.Documents) OR EXISTS(SELECT 1 FROM dbo.Rezervasyonlar)
 THROW 51211,N'Kullanım veritabanı boş değil. Kayıtlar/sayaçlar değiştirilmedi.',1;
IF (SELECT COUNT(*) FROM sys.sequences WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN(N'ParcaSayaci',N'MontajSayaci') AND CONVERT(bigint,start_value)=1 AND CONVERT(bigint,current_value)=1 AND last_used_value IS NULL)<>2
 THROW 51212,N'P/M sayaçları hiç kullanılmamış başlangıç1durumunda değil. Sayaçlar sıfırlanmadı.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Rezervasyonlar') AND name=N'UX_Rezervasyonlar_AktifDosyaAdi')
 THROW 51213,N'005 şeması doğrulanamadı.',1;
IF OBJECT_ID(N'dbo.DosyaAdiAyir',N'P') IS NULL OR OBJECT_ID(N'dbo.RezervasyonOnayla',N'P') IS NULL OR OBJECT_ID(N'dbo.RezervasyonIptalEt',N'P') IS NULL
 THROW 51214,N'Rezervasyon yordamları eksik.',1;
USE [master];
"@
    Invoke-DellSql $kontrol
}
try {
    $baglanti.Open()
    $var=Invoke-DellSql "SELECT COUNT(*) FROM sys.databases WHERE name=N'Pronom_Kullanim'" -Skaler
    if ($var) { Assert-DellBosVeritabani 'Pronom_Kullanim' }
    if ($SadeceKontrol) {
        Write-Output ('SQL bağlantısı başarılı. Pronom_Kullanim mevcut: '+[bool]$var)
        Write-Output 'Veritabanı/numara/SQL dosyası oluşturulmadı veya değiştirilmedi.'
        return
    }
    if (!$var) { Invoke-DellSql ([IO.File]::ReadAllText($kurulum)); Write-Output '1. Boş Pronom_Kullanim veritabanı kuruldu.' }
    else { Write-Output '1. Mevcut boş kullanım veritabanı korundu.' }
    Assert-DellBosVeritabani 'Pronom_Kullanim'
    $yedekKlasoru=[string](Invoke-DellSql "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))" -Skaler)
    $veriKlasoru=[string](Invoke-DellSql "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))" -Skaler)
    $logKlasoru=[string](Invoke-DellSql "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))" -Skaler)
    if (!$yedekKlasoru -or !$veriKlasoru -or !$logKlasoru) { throw 'SQL varsayılan dosya klasörleri okunamadı; elle inceleme gerekli.' }
    $yedek=$yedekKlasoru.TrimEnd('\')+'\Pronom_Kullanim_'+[Guid]::NewGuid().ToString('N')+'.bak'
    Invoke-DellSql 'BACKUP DATABASE [Pronom_Kullanim] TO DISK=@yol WITH COPY_ONLY,CHECKSUM,NOINIT; RESTORE VERIFYONLY FROM DISK=@yol WITH CHECKSUM;' @{'@yol'=$yedek}
    Write-Output '2. Yeni SQL yedeği alındı ve doğrulandı.'
    $cmd=$baglanti.CreateCommand(); $cmd.CommandText='RESTORE FILELISTONLY FROM DISK=@yol'; $cmd.CommandTimeout=180
    $null=$cmd.Parameters.AddWithValue('@yol',$yedek)
    $dosyalar=@()
    try {
        $r=$cmd.ExecuteReader()
        try { while ($r.Read()) { $dosyalar+=[pscustomobject]@{Mantiksal=[string]$r['LogicalName'];Tur=[string]$r['Type']} } }
        finally { $r.Dispose() }
    } finally { $cmd.Dispose() }
    if (!$dosyalar.Count -or @($dosyalar | Where-Object {$_.Tur -notin @('D','L')}).Count) { throw 'Beklenmeyen yedek dosya türü; geri yükleme yapılmadı.' }
    $geriAd='Pronom_GeriYuklemeKontrol_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
    if (Invoke-DellSql 'SELECT COUNT(*) FROM sys.databases WHERE name=@ad' @{'@ad'=$geriAd} -Skaler) { throw 'Geri yükleme hedefi zaten var; üzerine yazılmadı.' }
    $moves=@(); $sira=0
    foreach ($dosya in $dosyalar) {
        $klasor=if($dosya.Tur -eq 'L'){$logKlasoru}else{$veriKlasoru}
        $uzanti=if($dosya.Tur -eq 'L'){'.ldf'}else{'.mdf'}
        $yol=$klasor.TrimEnd('\')+'\'+$geriAd+'_'+$sira+$uzanti
        $moves+="MOVE N'"+$dosya.Mantiksal.Replace("'","''")+"' TO N'"+$yol.Replace("'","''")+"'"
        $sira++
    }
    Invoke-DellSql ("RESTORE DATABASE [$geriAd] FROM DISK=@yol WITH "+($moves -join ',')+', CHECKSUM,RECOVERY;') @{'@yol'=$yedek}
    Assert-DellBosVeritabani $geriAd
    Invoke-DellSql "DBCC CHECKDB ([$geriAd]) WITH NO_INFOMSGS;"
    Write-Output '3. Yedek ayrı veritabanına geri yüklendi; şema/sayaçlar ve CHECKDB doğrulandı.'
    $raporKlasoru=Join-Path $kok 'work\dell-merkez-kurulum'
    $null=New-Item -ItemType Directory -Path $raporKlasoru -Force
    $rapor=Join-Path $raporKlasoru ('sonuc-'+[Guid]::NewGuid().ToString('N')+'.json')
    [pscustomobject]@{Bilgisayar=[Environment]::MachineName;ZamanTSI=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'Turkey Standard Time').ToString('O');Veritabani='Pronom_Kullanim';BelgeSayisi=0;RezervasyonSayisi=0;IlkParca='P00001';IlkMontaj='M00001';YedekDosyasi=$yedek;GeriYuklemeVeritabani=$geriAd;CheckDb='Gecti'} | ConvertTo-Json | Set-Content -LiteralPath $rapor -Encoding UTF8
    Write-Output ('DOGRULANDI: Temiz kullanım veritabanı, yedek ve ayrı geri yükleme. Rapor: '+$rapor)
    Write-Output ('Yedek: '+$yedek)
    Write-Output ('Korunan geri yükleme denemesi: '+$geriAd)
    Write-Output 'API/eklenti adresi değiştirilmedi. Mevcut test DB/CAD/günlükler korundu. Yeni numara ayrılmadı.'
} finally { $baglanti.Dispose() }
