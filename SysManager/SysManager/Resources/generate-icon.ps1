# Generates app.ico — a simple teal/purple gradient disk with a white "S".
# Run once: .\generate-icon.ps1  (outputs app.ico next to this script).
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$bitmaps = foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.TextRenderingHint = 'ClearTypeGridFit'

    $rect = New-Object System.Drawing.Rectangle 0, 0, $sz, $sz
    $rectF = New-Object System.Drawing.RectangleF 0, 0, $sz, $sz
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 91, 141, 239),
        [System.Drawing.Color]::FromArgb(255, 124, 58, 237),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $radius = [int]($sz * 0.22)
    $path.AddArc(0, 0, $radius*2, $radius*2, 180, 90)
    $path.AddArc($sz-$radius*2-1, 0, $radius*2, $radius*2, 270, 90)
    $path.AddArc($sz-$radius*2-1, $sz-$radius*2-1, $radius*2, $radius*2, 0, 90)
    $path.AddArc(0, $sz-$radius*2-1, $radius*2, $radius*2, 90, 90)
    $path.CloseFigure()

    $g.FillPath($brush, $path)

    # Draw the letter 'S'.
    $fontSize = [single]($sz * 0.55)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'
    $sf.LineAlignment = 'Center'
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.DrawString('S', $font, $white, $rectF, $sf)

    $g.Dispose()
    $bmp
}

# Assemble .ICO manually (each frame as PNG-compressed for sizes >= 64).
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([uint16]0)           # reserved
$w.Write([uint16]1)           # type 1 = .ico
$w.Write([uint16]$bitmaps.Count)

$imageStreams = @()
$offset = 6 + $bitmaps.Count * 16
for ($i = 0; $i -lt $bitmaps.Count; $i++) {
    $bmp = $bitmaps[$i]
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageStreams += ,$ms

    $sz = $bmp.Width
    $w.Write([byte]($sz -band 0xff))   # width  (0 = 256)
    $w.Write([byte]($sz -band 0xff))
    $w.Write([byte]0)                  # colors
    $w.Write([byte]0)                  # reserved
    $w.Write([uint16]1)                # planes
    $w.Write([uint16]32)               # bpp
    $w.Write([uint32]$ms.Length)
    $w.Write([uint32]$offset)
    $offset += [int]$ms.Length
}
foreach ($ms in $imageStreams) {
    $ms.Position = 0
    $ms.WriteTo($ico)
}
$outPath = Join-Path $PSScriptRoot 'app.ico'
[System.IO.File]::WriteAllBytes($outPath, $ico.ToArray())
"Saved $outPath"
