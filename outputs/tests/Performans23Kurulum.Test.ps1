param([string]$Paket,[string]$Root,[string]$Work)
$ErrorActionPreference='Stop'
$base=Join-Path $Work ('performans23-kurulum-test-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $base
$global:pronomPerfSw=$false;$global:pronomPerfFail=$false
function Get-Process {param($Name,$ErrorAction) if($global:pronomPerfSw){[pscustomobject]@{ProcessName='SLDWORKS'}}}
function Copy-Item {
 param($LiteralPath,$Destination,[switch]$Recurse,[switch]$Force)
 if($global:pronomPerfFail -and $LiteralPath -like '*Dosyalar*Pronom.PencereTanisi.exe.config'){throw 'Izole kopyalama hatasi'}
 Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
}
function Fixture([string]$Name){
 $path=Join-Path $base $Name
 $null=New-Item -ItemType Directory -Path (Join-Path $path 'Araclar')
 foreach($ad in @('Pronom.SolidWorks.dll','Araclar\Pronom.PencereTanisi.exe','Araclar\Pronom.PencereTanisi.exe.config')){
  Copy-Item -LiteralPath (Join-Path $Root ('Eklenti\'+$ad)) -Destination (Join-Path $path $ad)
 }
 [IO.File]::WriteAllText((Join-Path $path 'korunan-ayar.txt'),'dokunma')
 return $path
}
function ExpectFail([string]$Path){
 $caught=$false;try{& (Join-Path $Paket 'Performans23Guncelle.ps1') -EklentiKlasoru $Path | Out-Null}catch{$caught=$true}
 if(!$caught){throw 'Beklenen ret gelmedi'}
}
$open=Fixture 'acik';$global:pronomPerfSw=$true;ExpectFail $open;$global:pronomPerfSw=$false
if((Get-FileHash -LiteralPath (Join-Path $open 'Pronom.SolidWorks.dll')).Hash -ne '92AFB675607548987A61F6F634A8AE2BB317A74683FA3B7BE2F5B0DE1B395E69'){throw 'AcikSWdeyazilmamali'}
$bad=Fixture 'yabanci';[IO.File]::WriteAllText((Join-Path $bad 'Pronom.SolidWorks.dll'),'yabanci');ExpectFail $bad
$broken=Fixture 'rollback';$global:pronomPerfFail=$true;ExpectFail $broken;$global:pronomPerfFail=$false
foreach($ad in @('Pronom.SolidWorks.dll','Araclar\Pronom.PencereTanisi.exe','Araclar\Pronom.PencereTanisi.exe.config')){
 if((Get-FileHash -LiteralPath (Join-Path $broken $ad)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $Root ('Eklenti\'+$ad))).Hash){throw 'Rollback orijinali korumali'}
}
$valid=Fixture 'normal'
& (Join-Path $Paket 'Performans23Guncelle.ps1') -EklentiKlasoru $valid
if((Get-FileHash -LiteralPath (Join-Path $valid 'Pronom.SolidWorks.dll')).Hash -ne '577227B6B52A5A94A2C354A5C25A359935FEA102E69FFC582DC394B48B053AD0'){throw 'YeniDLLdogrulanmadi'}
if([IO.File]::ReadAllText((Join-Path $valid 'korunan-ayar.txt')) -ne 'dokunma'){throw 'Ayardegismemeli'}
if(@(Get-ChildItem -LiteralPath (Join-Path $valid 'Yedekler') -Directory).Count -ne 1){throw 'Yedekolmali'}
Write-Output 'GECTI: acikSW, yabanciDLL, kopyalama hatasinda3dosya rollback, normalguncelleme/yedek ve ayar koruma. Gercek kurulu Eklenti degismedi.'