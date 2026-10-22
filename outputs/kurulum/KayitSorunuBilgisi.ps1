param([string]$RaporKlasoru=[Environment]::GetFolderPath('Desktop'),[string]$EklentiKlasoru='C:\Pronom Eklenti')
$ErrorActionPreference='Stop'
$yerel=Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
$eklenti=$EklentiKlasoru
$rapor=Join-Path $RaporKlasoru ('Pronom-Kayit-Sorunu-'+[Guid]::NewGuid().ToString('N')+'.txt')
$satirlar=New-Object 'System.Collections.Generic.List[string]'
function Yaz([string]$Metin){$satirlar.Add($Metin)}
Yaz 'PRONOM KAYIT SORUNU - SALT OKUMA'
Yaz ('Bilgisayar: '+[Environment]::MachineName)
Yaz ('Zaman: '+[DateTimeOffset]::Now.ToString('O'))
Yaz 'CAD/gunluk/ayar/guvenlik politikasi degistirilmedi. Yardimci EXE calistirilmadi.'
Yaz ('SolidWorks surecleri: '+((@(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue) | ForEach-Object {$_.Id}) -join ','))
foreach($ad in @('StandartKayit.acik','BagimsizKayit.acik','NativeAdDenemesi.acik','KayitOlayTanisi.acik')){Yaz ($ad+': '+(Test-Path -LiteralPath (Join-Path $yerel $ad)))}
foreach($ad in @('Pronom.SolidWorks.dll','Araclar\Pronom.PencereTanisi.exe')){
 $yol=Join-Path $eklenti $ad
 try{
  if(Test-Path -LiteralPath $yol -PathType Leaf){
   $hash=(Get-FileHash -LiteralPath $yol -Algorithm SHA256).Hash
   Yaz ($yol+' SHA256='+$hash)
  }else{Yaz ('DOSYA YOK: '+$yol)}
 }catch{Yaz ('Dosya okunamadi: '+$yol+' - '+$_.Exception.Message)}
}
Yaz 'API SAGLIK / SALT GET'
try{
 $metin=[IO.File]::ReadAllText((Join-Path $yerel 'ApiAdresi.txt')).Trim().TrimEnd('/')+'/'
 $adres=$null
 if(![Uri]::TryCreate($metin,[UriKind]::Absolute,[ref]$adres) -or $adres.Scheme -notin @('http','https') -or $adres.UserInfo -or $adres.Query -or $adres.Fragment -or $adres.AbsolutePath -ne '/') {throw 'Kayitli API adresi gecersiz.'}
 Yaz ('Adres: '+$adres.AbsoluteUri)
 for($i=1;$i -le 3;$i++){
  $saat=[Diagnostics.Stopwatch]::StartNew()
  try{$saglik=Invoke-RestMethod -Uri ($adres.AbsoluteUri+'api/health') -TimeoutSec 5;Yaz ('Deneme'+$i+': '+$saat.ElapsedMilliseconds+'ms; '+$saglik.durum+'/'+$saglik.veritabani)}
  catch{Yaz ('Deneme'+$i+': '+$saat.ElapsedMilliseconds+'ms; '+$_.Exception.Message)}
 }
}catch{Yaz ('API bilgisi okunamadi: '+$_.Exception.Message)}
Yaz 'YEREL BEKLEYEN ISLEMLER'
$islemler=Join-Path $yerel 'Islemler'
if(Test-Path -LiteralPath $islemler -PathType Container){
 foreach($f in @(Get-ChildItem -LiteralPath $islemler -Filter '*.json' -File)){
  try{
   $i=Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
   if($i.Asama -notin @('Onaylandi','Iptal')){Yaz ('Islem='+$i.IslemId+' Asama='+$i.Asama+' Bicim='+$i.KaydetmeBicimi+' Ayrilan='+$i.AyrilanDosyaAdi+' Gercek='+$i.GercekDosya+' Hedef='+$i.HedefDosya)}
  }catch{Yaz ('GUNLUK OKUNAMADI: '+$f.Name+' - '+$_.Exception.Message)}
 }
}else{Yaz 'Islemler klasoru yok.'}
Yaz 'SON 250 TANI SATIRI'
$log=Join-Path $yerel 'KayitOlayTanisi.log'
if(Test-Path -LiteralPath $log -PathType Leaf){
 try{foreach($s in @(Get-Content -LiteralPath $log -Tail 250 -Encoding UTF8)){Yaz $s}}
 catch{Yaz ('Tani gunlugu okunamadi: '+$_.Exception.Message)}
}else{Yaz 'Tani gunlugu bulunamadi.'}
[IO.File]::WriteAllLines($rapor,$satirlar,[Text.UTF8Encoding]::new($true))
Write-Output ('RAPOR HAZIR: '+$rapor)
Write-Output 'Yeni kayit denemesi yapmadan bu TXT dosyasini paylasin. Guvenlik yazilimi degistirilmedi.'
