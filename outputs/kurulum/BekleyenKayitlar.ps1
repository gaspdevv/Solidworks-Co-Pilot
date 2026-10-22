param([switch]$SadeceListele, [string]$OnizlemeDosyasi)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BekleyenKayitlarIslemleri.ps1')
$eklenti = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Pronom.SolidWorks.dll')) {
    $PSScriptRoot
} else {
    Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Eklenti'
}
if ($SadeceListele) { Get-PronomBekleyenKayit -EklentiKlasoru $eklenti; return }
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Pencereyi BekleyenKayitlar.cmd ile açın.' }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object Windows.Forms.Form
$form.Text = 'Pronom · Bekleyen kayıtlar'
$form.Size = New-Object Drawing.Size(960,680)
$form.MinimumSize = New-Object Drawing.Size(800,600)
$form.StartPosition = 'CenterScreen'
$form.BackColor = [Drawing.Color]::FromArgb(245,248,247)
$form.Font = New-Object Drawing.Font('Segoe UI',10)
$layout = New-Object Windows.Forms.TableLayoutPanel
$layout.Dock = 'Fill'; $layout.Padding = New-Object Windows.Forms.Padding(20)
$layout.ColumnCount = 1; $layout.RowCount = 6
foreach ($height in @(40,50,0,155,48,42)) {
    $style = New-Object Windows.Forms.RowStyle
    if ($height -eq 0) { $style.SizeType = 'Percent'; $style.Height = 100 }
    else { $style.SizeType = 'Absolute'; $style.Height = $height }
    $null = $layout.RowStyles.Add($style)
}
$form.Controls.Add($layout)
$title = New-Object Windows.Forms.Label
$title.Text = 'Bekleyen kayıtlar'; $title.Dock = 'Fill'
$title.Font = New-Object Drawing.Font('Segoe UI',18,[Drawing.FontStyle]::Bold)
$title.ForeColor = [Drawing.Color]::FromArgb(20,47,54)
$layout.Controls.Add($title,0,0)
$intro = New-Object Windows.Forms.Label
$intro.Text = 'Bu bilgisayardaki Windows kullanıcısının kayıtları. Toparlamak için SolidWorks kapalı olmalı.'
$intro.Dock = 'Fill'; $layout.Controls.Add($intro,0,1)
$list = New-Object Windows.Forms.ListView
$list.Dock = 'Fill'; $list.View = 'Details'; $list.FullRowSelect = $true
$list.MultiSelect = $false; $list.HideSelection = $false; $list.GridLines = $true
foreach ($column in @(@('Ayrılan dosya adı',200),@('Durum',280),@('Toparlama',260))) {
    $null = $list.Columns.Add([string]$column[0],[int]$column[1])
}
$layout.Controls.Add($list,0,2)
$detail = New-Object Windows.Forms.TextBox
$detail.Multiline = $true; $detail.ReadOnly = $true; $detail.ScrollBars = 'Vertical'
$detail.Dock = 'Fill'; $detail.BackColor = [Drawing.Color]::White
$detail.Text = 'Ayrıntıları görmek için bir kayıt seçin.'
$layout.Controls.Add($detail,0,3)
$buttons = New-Object Windows.Forms.FlowLayoutPanel
$buttons.Dock = 'Fill'
$refresh = New-Object Windows.Forms.Button
$refresh.Text = 'Listeyi yenile'; $refresh.Size = New-Object Drawing.Size(140,36)
$recover = New-Object Windows.Forms.Button
$recover.Text = 'Seçili sonucu toparla'; $recover.Size = New-Object Drawing.Size(210,36)
$recover.BackColor = [Drawing.Color]::FromArgb(20,121,109); $recover.ForeColor = [Drawing.Color]::White
$recover.FlatStyle = 'Flat'; $recover.Enabled = $false
$buttons.Controls.Add($refresh); $buttons.Controls.Add($recover)
$layout.Controls.Add($buttons,0,4)
$status = New-Object Windows.Forms.Label
$status.Dock = 'Fill'; $status.Text = 'Liste bekleniyor.'
$layout.Controls.Add($status,0,5)
function Update-PronomSelection {
    $recover.Enabled = $false
    if ($list.SelectedItems.Count -ne 1) { $detail.Text = 'Ayrıntıları görmek için bir kayıt seçin.'; return }
    $row = $list.SelectedItems[0].Tag
    $detail.Text = "İşlem kimliği: $($row.IslemId)`r`nDurum: $($row.Durum) ($($row.Asama))`r`nGerçek dosya: $($row.GercekDosya)`r`nHedef dosya: $($row.HedefDosya)`r`n`r`n$($row.Aciklama)"
    $recover.Enabled = $row.ToparlamayaUygun -and !(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue)
}
function Update-PronomList {
    $recover.Enabled = $false; $list.Items.Clear()
    try {
        $rows = @(Get-PronomBekleyenKayit -EklentiKlasoru $eklenti)
        foreach ($row in $rows) {
            $item = New-Object Windows.Forms.ListViewItem([string]$row.DosyaAdi)
            $null = $item.SubItems.Add($row.Durum)
            $null = $item.SubItems.Add($(if ($row.ToparlamayaUygun) {'Denenebilir'} else {'Elle inceleme'}))
            $item.Tag = $row; $null = $list.Items.Add($item)
        }
        $sw = [bool](Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue)
        $status.Text = if ($sw) { "$($rows.Count) bekleyen kayıt. SolidWorks açık; toparlama kapalı. Kapattıktan sonra yenileyin." }
        elseif ($rows.Count) { "$($rows.Count) bekleyen kayıt. Toparlamak için uygun bir kayıt seçin." }
        else { 'Bekleyen kayıt yok.' }
        Update-PronomSelection
    } catch {
        $list.Items.Clear(); $detail.Text = $_.Exception.Message
        $status.Text = 'Liste okunamadı. Günlükler korundu; hata ayrıntısını paylaşın.'
    }
}
$list.Add_SelectedIndexChanged({ Update-PronomSelection })
$refresh.Add_Click({ Update-PronomList })
$recover.Add_Click({
    if ($list.SelectedItems.Count -ne 1) { return }
    $id = [Guid]$list.SelectedItems[0].Tag.IslemId
    $refresh.Enabled = $false; $recover.Enabled = $false; $form.UseWaitCursor = $true
    try {
        $result = Invoke-PronomSeciliToparlama -EklentiKlasoru $eklenti -IslemId $id | Out-String
        Update-PronomList
        $detail.Text = "Son işlem: $id`r`n`r`n$result"
        $status.Text = 'Seçili işlem tamamlandı. Liste yenilendi.'
    } catch {
        $hata = $_.Exception.Message
        Update-PronomList
        $detail.Text = "İşlem: $id`r`n`r`n$hata"
        $status.Text = 'Toparlama tamamlanamadı. İşlem korundu; ayrıntıları kontrol edin.'
    } finally { $refresh.Enabled = $true; $form.UseWaitCursor = $false }
})
Update-PronomList
if ($OnizlemeDosyasi) {
    $form.ShowInTaskbar = $false; $form.Opacity = 0
    $form.Show(); $form.PerformLayout(); $form.Refresh()
    $bitmap = New-Object Drawing.Bitmap($form.Width,$form.Height)
    try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save($OnizlemeDosyasi,[Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose(); $form.Close(); $form.Dispose() }
} else {
    try { $null = $form.ShowDialog() } finally { $form.Dispose() }
}
