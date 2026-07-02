$headers = @{'Referer'='https://igoutu.cn/'}
$base = 'https://img.icons8.com/plasticine/100'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

$icons = @(
    'mountain', 'gradient', '3d', 'layers'
)
foreach ($i in $icons) {
    $url = "$base/$i.png"
    $out = Join-Path $dir "$i.png"
    Write-Host "Downloading $url"
    try { Invoke-WebRequest -Uri $url -OutFile $out -Headers $headers -ErrorAction Stop } catch { Write-Host "FAILED: $i" }
}
Get-ChildItem $dir | Select Name, Length
