$ErrorActionPreference='Stop'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'paket-dosyalari.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$root=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')+'\'
foreach ($kayit in $manifest.Dosyalar) {
    $yol=[IO.Path]::GetFullPath((Join-Path $root $kayit.Dosya))
    if (!$yol.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) { throw 'Geçersiz paket dosya yolu.' }
    if (!(Test-Path -LiteralPath $yol -PathType Leaf)) { throw ('Eksik dosya: '+$kayit.Dosya) }
    if ((Get-FileHash -LiteralPath $yol -Algorithm SHA256).Hash -ne $kayit.SHA256) { throw ('Değişmiş dosya: '+$kayit.Dosya) }
}
Write-Output ('Paket dosyaları doğrulandı: '+$manifest.Dosyalar.Count+' dosya; '+$manifest.Surum)
