$headers = @{'Referer'='https://igoutu.cn/'}
$base = 'https://img.icons8.com/plasticine/100'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

$icons = @(
    'undo', 'redo',
    'marker', 'search', 'combo-chart', 'edit',
    'table', 'settings', 'warning-shield', 'layers',
    'mountain', 'slope', 'compass', 'pulse', '3d-select-face',
    'soil', 'leaf', 'building', 'water',
    'print', 'book', 'document'
)
foreach ($i in $icons) {
    $url = "$base/$i.png"
    $out = Join-Path $dir "$i.png"
    Write-Host "Downloading $url"
    try { Invoke-WebRequest -Uri $url -OutFile $out -Headers $headers -ErrorAction Stop } catch { Write-Host "FAILED: $i" }
}
Get-ChildItem $dir | Select Name, Length
