param(
 [Parameter(Mandatory=$true)][string]$SetupDosyasi,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$BeklenenSHA256,
 [string[]]$WebKlasoru,
 [string]$YedekKlasoru
)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(!$WebKlasoru){$WebKlasoru=@((Join-Path $root 'outputs\src\Etiket.Api\wwwroot'),(Join-Path $root 'Merkez\Api\wwwroot'))}
if(!$YedekKlasoru){$YedekKlasoru=Join-Path $root 'work\setup-yayin-yedekleri'}
if(!(Test-Path -LiteralPath $SetupDosyasi -PathType Leaf)){throw 'Yayınlanacak setup dosyası bulunamadı.'}
$SetupDosyasi=[IO.Path]::GetFullPath($SetupDosyasi)
if([IO.Path]::GetExtension($SetupDosyasi) -ine '.exe'){throw 'Yalnızca setup EXE dosyası yayınlanabilir.'}
if((Get-FileHash -LiteralPath $SetupDosyasi -Algorithm SHA256).Hash -ine $BeklenenSHA256){throw 'Setup SHA256 uyuşmuyor; yayın değiştirilmedi.'}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($SetupDosyasi)
if($version.ProductName -ne 'Pronom SolidWorks Co-Pilot Kurulumu'){throw 'Pronom setup ürünü doğrulanamadı; yayın değiştirilmedi.'}
function DizinDogrula([string]$path){
 for($d=[IO.DirectoryInfo]::new([IO.Path]::GetFullPath($path));$null -ne $d;$d=$d.Parent){
  if($d.Exists -and ($d.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Yönlendirilmiş yayın klasörü kullanılamaz: '+$d.FullName)}
 }
}
DizinDogrula $YedekKlasoru
$id=[Guid]::NewGuid().ToString('N')
$stages=@()
try{
 foreach($web in ($WebKlasoru | Select-Object -Unique)){
  if(!(Test-Path -LiteralPath $web -PathType Container)){throw ('Web klasörü bulunamadı: '+$web)}
  $folder=Join-Path ([IO.Path]::GetFullPath($web)) 'indir';DizinDogrula $folder
  $target=Join-Path $folder 'Pronom-Kurulum.exe'
  if((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Yönlendirilmiş setup dosyası değiştirilemez.'}
  $null=New-Item -ItemType Directory -Path $folder -Force
  $temp=Join-Path $folder ('Pronom-Kurulum.'+$id+'.tmp')
  $stages+= [pscustomobject]@{Target=$target;Temp=$temp}
  [IO.File]::Copy($SetupDosyasi,$temp,$false)
  if((Get-FileHash -LiteralPath $temp).Hash -ine $BeklenenSHA256){throw 'Yayın kopyası doğrulanamadı.'}
 }
 foreach($stage in $stages){
  if(Test-Path -LiteralPath $stage.Target){
   $backup=Join-Path $YedekKlasoru ($id+'-'+[Guid]::NewGuid().ToString('N')+'.exe')
   $null=New-Item -ItemType Directory -Path $YedekKlasoru -Force
   [IO.File]::Replace($stage.Temp,$stage.Target,$backup)
  }else{[IO.File]::Move($stage.Temp,$stage.Target)}
  [pscustomobject]@{Surum=$version.FileVersion;SHA256=$BeklenenSHA256.ToUpperInvariant();Dosya=$stage.Target;IndirmeYolu='/indir/Pronom-Kurulum.exe'}
 }
}finally{
 foreach($stage in $stages){if(Test-Path -LiteralPath $stage.Temp){Remove-Item -LiteralPath $stage.Temp}}
}
