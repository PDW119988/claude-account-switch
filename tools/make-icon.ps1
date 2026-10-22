# Draws the app icon (two swap arrows on a rounded square) and writes a
# multi-size .ico with PNG-compressed images. No external tools needed.
param([Parameter(Mandatory = $true)][string]$Out)

Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $r = [single]($size * 0.22)
    $s = [single]($size - 1)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($s - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($s - $r * 2, $s - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $s - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xD9, 0x6C, 0x3F))
    $g.FillPath($bg, $path)

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single]($size * 0.085))
    $pen.StartCap = 'Round'
    $pen.EndCap = 'Round'
    $m = $size * 0.24; $e = $size * 0.76; $y1 = $size * 0.37; $y2 = $size * 0.63; $h = $size * 0.11
    # top arrow: left -> right
    $g.DrawLine($pen, [single]$m, [single]$y1, [single]$e, [single]$y1)
    $g.DrawLine($pen, [single]($e - $h), [single]($y1 - $h), [single]$e, [single]$y1)
    $g.DrawLine($pen, [single]($e - $h), [single]($y1 + $h), [single]$e, [single]$y1)
    # bottom arrow: right -> left
    $g.DrawLine($pen, [single]$e, [single]$y2, [single]$m, [single]$y2)
    $g.DrawLine($pen, [single]($m + $h), [single]($y2 - $h), [single]$m, [single]$y2)
    $g.DrawLine($pen, [single]($m + $h), [single]($y2 + $h), [single]$m, [single]$y2)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 256
$frames = @($sizes | ForEach-Object { , (New-Frame $_) })

$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $data = $frames[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($data in $frames) { $w.Write($data) }
$w.Close()
