param([Parameter(Mandatory=$true)][string]$ApiAdresi)
$ErrorActionPreference = 'Stop'
$adres = $null
if (![Uri]::TryCreate($ApiAdresi.TrimEnd('/')+'/',[UriKind]::Absolute,[ref]$adres) -or $adres.Scheme -notin @('http','https') -or $adres.UserInfo -or $adres.Query -or $adres.Fragment) { throw 'Geçerli bir HTTP/HTTPS servis adresi yazın.' }
$ayarKlasoru = Join-Path $env:LOCALAPPDATA 'Pronom\SolidWorksEtiketleme'
New-Item -ItemType Directory -Path $ayarKlasoru -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $ayarKlasoru 'ApiAdresi.txt'),$adres.AbsoluteUri,[Text.UTF8Encoding]::new($false))
Write-Output 'API adresi mevcut SolidWorks kullanıcısı için kaydedildi.'
