Add-Type -AssemblyName System.Drawing

function Create-Studio-Bitmap([int]$size = 512) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $scale = $size / 512.0

    # Background rounded rect
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#14151C"))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 100 * $scale
    $d = $r * 2
    $w = $size
    $h = $size
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $path.AddArc(0, $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # OBS Propeller Center
    $cx = 256.0 * $scale
    $cy = 195.0 * $scale
    $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $grayBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#9CA3AF"))
    $accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#71717A"))

    # Draw 3 OBS blades around center
    for ($i = 0; $i -lt 3; $i++) {
        $state = $g.Save()
        $g.TranslateTransform($cx, $cy)
        $g.RotateTransform($i * 120.0)

        $bladePath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $bladePath.AddBezier(0, -90 * $scale, 40 * $scale, -90 * $scale, 70 * $scale, -60 * $scale, 70 * $scale, -25 * $scale)
        $bladePath.AddBezier(70 * $scale, -25 * $scale, 70 * $scale, 5 * $scale, 50 * $scale, 10 * $scale, 30 * $scale, 10 * $scale)
        $bladePath.AddBezier(30 * $scale, 10 * $scale, 10 * $scale, 10 * $scale, 0, -5 * $scale, 0, -25 * $scale)
        $bladePath.AddBezier(0, -25 * $scale, 0, -50 * $scale, -15 * $scale, -70 * $scale, -40 * $scale, -75 * $scale)
        $bladePath.AddLine(-40 * $scale, -75 * $scale, 0, -90 * $scale)
        $bladePath.CloseFigure()
        $g.FillPath($whiteBrush, $bladePath)

        $g.Restore($state)
    }

    # Center circles
    $g.FillEllipse($bgBrush, [float]($cx - 24 * $scale), [float]($cy - 24 * $scale), [float](48 * $scale), [float](48 * $scale))
    $g.FillEllipse($whiteBrush, [float]($cx - 10 * $scale), [float]($cy - 10 * $scale), [float](20 * $scale), [float](20 * $scale))

    # Text: EECC STUDIO
    $fontTitle = New-Object System.Drawing.Font ("Segoe UI", [float](34 * $scale), [System.Drawing.FontStyle]::Bold)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("EECC STUDIO", $fontTitle, $whiteBrush, $cx, [float](370 * $scale), $sf)

    # Accent line
    $g.FillRectangle($accentBrush, [float]($cx - 70 * $scale), [float](405 * $scale), [float](140 * $scale), [float](4 * $scale))

    # Subtitle: NATIVE BROADCAST
    $fontSub = New-Object System.Drawing.Font ("Segoe UI", [float](13 * $scale), [System.Drawing.FontStyle]::Bold)
    $g.DrawString("NATIVE BROADCAST", $fontSub, $grayBrush, $cx, [float](430 * $scale), $sf)

    $g.Dispose()
    return $bmp
}

function Create-Tv-Bitmap([int]$size = 512) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $scale = $size / 512.0

    # Background rounded rect - White
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml("#E2E8F0"), [float](4 * $scale))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 100 * $scale
    $d = $r * 2
    $w = $size
    $h = $size
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $path.AddArc(0, $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)
    $g.DrawPath($borderPen, $path)

    # Dark blue brush for eecc
    $blueBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#0B1B3D"))
    $redBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#DC2626"))
    $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $subBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#64748B"))

    # eecc Text
    $fontEecc = New-Object System.Drawing.Font ("Segoe UI", [float](85 * $scale), [System.Drawing.FontStyle]::Bold)
    $sfLeft = New-Object System.Drawing.StringFormat
    $sfLeft.Alignment = [System.Drawing.StringAlignment]::Near
    $sfLeft.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("eecc", $fontEecc, $blueBrush, [float](48 * $scale), [float](225 * $scale), $sfLeft)

    # tv Badge (Red rounded rect)
    $tvPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bx = 318 * $scale
    $by = 175 * $scale
    $bw = 142 * $scale
    $bh = 100 * $scale
    $br = 24 * $scale
    $bd = $br * 2
    $tvPath.AddArc($bx, $by, $bd, $bd, 180, 90)
    $tvPath.AddArc($bx + $bw - $bd, $by, $bd, $bd, 270, 90)
    $tvPath.AddArc($bx + $bw - $bd, $by + $bh - $bd, $bd, $bd, 0, 90)
    $tvPath.AddArc($bx, $by + $bh - $bd, $bd, $bd, 90, 90)
    $tvPath.CloseFigure()
    $g.FillPath($redBrush, $tvPath)

    # tv text inside badge
    $fontTv = New-Object System.Drawing.Font ("Segoe UI", [float](54 * $scale), [System.Drawing.FontStyle]::Bold)
    $sfCenter = New-Object System.Drawing.StringFormat
    $sfCenter.Alignment = [System.Drawing.StringAlignment]::Center
    $sfCenter.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("tv", $fontTv, $whiteBrush, [float]($bx + $bw/2.0), [float]($by + $bh/2.0 - 4*$scale), $sfCenter)

    # Dynamic line
    $g.FillRectangle($redBrush, [float](48 * $scale), [float](312 * $scale), [float](398 * $scale), [float](8 * $scale))
    $g.FillEllipse($blueBrush, [float](450 * $scale), [float](310 * $scale), [float](12 * $scale), [float](12 * $scale))

    # Subtitle: LE CANAL DE L'ÉGLISE
    $fontSub = New-Object System.Drawing.Font ("Segoe UI", [float](15 * $scale), [System.Drawing.FontStyle]::Bold)
    $g.DrawString("LE CANAL DE L'ÉGLISE", $fontSub, $subBrush, [float](256 * $scale), [float](360 * $scale), $sfCenter)

    $g.Dispose()
    return $bmp
}

function Create-Camera-Bitmap([int]$size = 512) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $scale = $size / 512.0

    # Background rounded rect - Blue EECC #1E3A8A
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#1E3A8A"))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 100 * $scale
    $d = $r * 2
    $w = $size
    $h = $size
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $path.AddArc(0, $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $subBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#93C5FD"))
    $handleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90, 255, 255, 255))

    # Camera top handle
    $p1 = New-Object System.Drawing.PointF -ArgumentList @([float](126 * $scale), [float](150 * $scale))
    $p2 = New-Object System.Drawing.PointF -ArgumentList @([float](246 * $scale), [float](150 * $scale))
    $p3 = New-Object System.Drawing.PointF -ArgumentList @([float](256 * $scale), [float](174 * $scale))
    $p4 = New-Object System.Drawing.PointF -ArgumentList @([float](116 * $scale), [float](174 * $scale))
    [System.Drawing.PointF[]]$handlePts = @($p1, $p2, $p3, $p4)
    $handlePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $handlePath.AddPolygon($handlePts)
    $g.FillPath($handleBrush, $handlePath)

    # Camera body (white rounded rect)
    $camPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cx = 96 * $scale
    $cy = 174 * $scale
    $cw = 220 * $scale
    $ch = 170 * $scale
    $cr = 28 * $scale
    $cd = $cr * 2
    $camPath.AddArc($cx, $cy, $cd, $cd, 180, 90)
    $camPath.AddArc($cx + $cw - $cd, $cy, $cd, $cd, 270, 90)
    $camPath.AddArc($cx + $cw - $cd, $cy + $ch - $cd, $cd, $cd, 0, 90)
    $camPath.AddArc($cx, $cy + $ch - $cd, $cd, $cd, 90, 90)
    $camPath.CloseFigure()
    $g.FillPath($whiteBrush, $camPath)

    # Camera lens trapezoid (right)
    $lp1 = New-Object System.Drawing.PointF -ArgumentList @([float](330 * $scale), [float](204 * $scale))
    $lp2 = New-Object System.Drawing.PointF -ArgumentList @([float](416 * $scale), [float](164 * $scale))
    $lp3 = New-Object System.Drawing.PointF -ArgumentList @([float](416 * $scale), [float](354 * $scale))
    $lp4 = New-Object System.Drawing.PointF -ArgumentList @([float](330 * $scale), [float](314 * $scale))
    [System.Drawing.PointF[]]$lensPts = @($lp1, $lp2, $lp3, $lp4)
    $lensPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $lensPath.AddPolygon($lensPts)
    $g.FillPath($whiteBrush, $lensPath)

    # EECC Cross inside camera body (Blue EECC #1E3A8A)
    # Vertical beam
    $g.FillRectangle($bgBrush, [float](192 * $scale), [float](204 * $scale), [float](28 * $scale), [float](110 * $scale))
    # Horizontal beam
    $g.FillRectangle($bgBrush, [float](156 * $scale), [float](232 * $scale), [float](100 * $scale), [float](28 * $scale))
    # Cross center circle
    $g.FillEllipse($whiteBrush, [float](200 * $scale), [float](240 * $scale), [float](12 * $scale), [float](12 * $scale))

    # Text: EECC CAMÉRA
    $sfCenter = New-Object System.Drawing.StringFormat
    $sfCenter.Alignment = [System.Drawing.StringAlignment]::Center
    $sfCenter.LineAlignment = [System.Drawing.StringAlignment]::Center
    $fontCam = New-Object System.Drawing.Font ("Segoe UI", [float](28 * $scale), [System.Drawing.FontStyle]::Bold)
    $g.DrawString("EECC CAMÉRA", $fontCam, $whiteBrush, [float](256 * $scale), [float](395 * $scale), $sfCenter)

    # Subtitle
    $fontSub = New-Object System.Drawing.Font ("Segoe UI", [float](14 * $scale), [System.Drawing.FontStyle]::Bold)
    $g.DrawString("FLUX VIDÉO SANS FIL", $fontSub, $subBrush, [float](256 * $scale), [float](426 * $scale), $sfCenter)

    $g.Dispose()
    return $bmp
}

function Save-Ico-File([System.Drawing.Bitmap]$sourceBmp, [string]$icoPath) {
    # Generate multi-size ICO file
    $sizes = @(16, 32, 48, 64, 128, 256)
    $images = @()
    foreach ($sz in $sizes) {
        $resized = New-Object System.Drawing.Bitmap $sz, $sz
        $g = [System.Drawing.Graphics]::FromImage($resized)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.DrawImage($sourceBmp, 0, 0, $sz, $sz)
        $g.Dispose()
        $images += $resized
    }

    $fs = [System.IO.File]::Create($icoPath)
    $bw = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR
    $bw.Write([uint16]0) # Reserved
    $bw.Write([uint16]1) # Type (1 = ICO)
    $bw.Write([uint16]$images.Count)

    # Convert each image to PNG stream
    $streams = @()
    foreach ($img in $images) {
        $ms = New-Object System.IO.MemoryStream
        $img.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $streams += $ms
    }

    $offset = 6 + (16 * $images.Count)
    for ($i = 0; $i -lt $images.Count; $i++) {
        $img = $images[$i]
        $w = if ($img.Width -ge 256) { 0 } else { [byte]$img.Width }
        $h = if ($img.Height -ge 256) { 0 } else { [byte]$img.Height }
        $len = [uint32]$streams[$i].Length

        $bw.Write($w)
        $bw.Write($h)
        $bw.Write([byte]0) # Colors in palette
        $bw.Write([byte]0) # Reserved
        $bw.Write([uint16]1) # Color planes
        $bw.Write([uint16]32) # Bits per pixel
        $bw.Write($len)
        $bw.Write([uint32]$offset)
        $offset += $len
    }

    for ($i = 0; $i -lt $streams.Count; $i++) {
        $bw.Write($streams[$i].ToArray())
        $streams[$i].Dispose()
        $images[$i].Dispose()
    }

    $bw.Flush()
    $bw.Close()
    $fs.Close()
}

Write-Host "--- Génération des Logos Officiels EECC ---"

# 1. STUDIO
$studioBmp = Create-Studio-Bitmap 512
$studioBmp.Save("assets\logos\eecc_studio_logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$studioBmp.Save("apps\studio_native\Assets\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
Save-Ico-File $studioBmp "apps\studio_native\Assets\icon.ico"
$studioBmp.Save("apps\studio\public\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$studioBmp.Save("apps\studio\public\icon-512.png", [System.Drawing.Imaging.ImageFormat]::Png)

$studio192 = New-Object System.Drawing.Bitmap 192, 192
$g192 = [System.Drawing.Graphics]::FromImage($studio192)
$g192.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g192.DrawImage($studioBmp, 0, 0, 192, 192)
$g192.Dispose()
$studio192.Save("apps\studio\public\icon-192.png", [System.Drawing.Imaging.ImageFormat]::Png)
$studio192.Dispose()

$studio64 = New-Object System.Drawing.Bitmap 64, 64
$g64 = [System.Drawing.Graphics]::FromImage($studio64)
$g64.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g64.DrawImage($studioBmp, 0, 0, 64, 64)
$g64.Dispose()
$studio64.Save("apps\studio\public\icon-64.png", [System.Drawing.Imaging.ImageFormat]::Png)
$studio64.Dispose()

$studio32 = New-Object System.Drawing.Bitmap 32, 32
$g32 = [System.Drawing.Graphics]::FromImage($studio32)
$g32.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g32.DrawImage($studioBmp, 0, 0, 32, 32)
$g32.Dispose()
$studio32.Save("apps\studio\public\icon-32.png", [System.Drawing.Imaging.ImageFormat]::Png)
$studio32.Dispose()

Save-Ico-File $studioBmp "apps\studio\public\favicon.ico"
$studioBmp.Dispose()
Write-Host "EECC Studio logos générés avec succès !"

# 2. TV
$tvBmp = Create-Tv-Bitmap 512
$tvBmp.Save("assets\logos\eecc_tv_logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$tvBmp.Save("apps\tv_mobile\assets\images\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$tvBmp.Save("apps\tv_mobile\assets\images\logo.jpeg", [System.Drawing.Imaging.ImageFormat]::Jpeg)

# Launcher icons TV
$tvSizes = @{
    "apps\tv_mobile\android\app\src\main\res\mipmap-mdpi\ic_launcher.png" = 48
    "apps\tv_mobile\android\app\src\main\res\mipmap-hdpi\ic_launcher.png" = 72
    "apps\tv_mobile\android\app\src\main\res\mipmap-xhdpi\ic_launcher.png" = 96
    "apps\tv_mobile\android\app\src\main\res\mipmap-xxhdpi\ic_launcher.png" = 144
    "apps\tv_mobile\android\app\src\main\res\mipmap-xxxhdpi\ic_launcher.png" = 192
}
foreach ($kv in $tvSizes.GetEnumerator()) {
    $dir = Split-Path $kv.Key
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $rs = New-Object System.Drawing.Bitmap $kv.Value, $kv.Value
    $gr = [System.Drawing.Graphics]::FromImage($rs)
    $gr.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gr.DrawImage($tvBmp, 0, 0, $kv.Value, $kv.Value)
    $gr.Dispose()
    $rs.Save($kv.Key, [System.Drawing.Imaging.ImageFormat]::Png)
    $rs.Dispose()
}
$tvBmp.Dispose()
Write-Host "EECC TV logos & mipmaps générés avec succès !"

# 3. CAMÉRA
$camBmp = Create-Camera-Bitmap 512
$camBmp.Save("assets\logos\eecc_camera_logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$camBmp.Save("apps\camera_mobile\assets\images\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$camBmp.Save("apps\camera_mobile\assets\images\logo.jpeg", [System.Drawing.Imaging.ImageFormat]::Jpeg)

# Launcher icons Caméra
$camSizes = @{
    "apps\camera_mobile\android\app\src\main\res\mipmap-mdpi\ic_launcher.png" = 48
    "apps\camera_mobile\android\app\src\main\res\mipmap-hdpi\ic_launcher.png" = 72
    "apps\camera_mobile\android\app\src\main\res\mipmap-xhdpi\ic_launcher.png" = 96
    "apps\camera_mobile\android\app\src\main\res\mipmap-xxhdpi\ic_launcher.png" = 144
    "apps\camera_mobile\android\app\src\main\res\mipmap-xxxhdpi\ic_launcher.png" = 192
}
foreach ($kv in $camSizes.GetEnumerator()) {
    $dir = Split-Path $kv.Key
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $rs = New-Object System.Drawing.Bitmap $kv.Value, $kv.Value
    $gr = [System.Drawing.Graphics]::FromImage($rs)
    $gr.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gr.DrawImage($camBmp, 0, 0, $kv.Value, $kv.Value)
    $gr.Dispose()
    $rs.Save($kv.Key, [System.Drawing.Imaging.ImageFormat]::Png)
    $rs.Dispose()
}
$camBmp.Dispose()
function Create-Global-Bitmap([int]$size = 512) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $scale = $size / 512.0

    # Background rounded rect - Blue EECC #1E3A8A
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#1E3A8A"))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 96 * $scale
    $d = $r * 2
    $w = $size
    $h = $size
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $path.AddArc(0, $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # Octagon dark slate
    $octBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#0F172A"))
    $goldPen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml("#D4AF37"), [float](8 * $scale))
    $goldBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#D4AF37"))
    $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

    [System.Drawing.PointF[]]$octPts = @(
        (New-Object System.Drawing.PointF -ArgumentList @([float](256 * $scale), [float](48 * $scale))),
        (New-Object System.Drawing.PointF -ArgumentList @([float](400 * $scale), [float](108 * $scale))),
        (New-Object System.Drawing.PointF -ArgumentList @([float](460 * $scale), [float](252 * $scale))),
        (New-Object System.Drawing.PointF -ArgumentList @([float](400 * $scale), [float](396 * $scale))),
        (New-Object System.Drawing.PointF -ArgumentList @([float](256 * $scale), [float](456 * $scale))),
        (New-Object System.Drawing.PointF -ArgumentList @([float](112 * $scale), [float](396 * $scale))),
        (New-Object System.Drawing.PointF -ArgumentList @([float](52 * $scale), [float](252 * $scale))),
        (New-Object System.Drawing.PointF -ArgumentList @([float](112 * $scale), [float](108 * $scale)))
    )
    $g.FillPolygon($octBrush, $octPts)
    $g.DrawPolygon($goldPen, $octPts)

    # Gold Cross
    $g.FillRectangle($goldBrush, [float](232 * $scale), [float](112 * $scale), [float](48 * $scale), [float](260 * $scale))
    $g.FillRectangle($goldBrush, [float](156 * $scale), [float](180 * $scale), [float](200 * $scale), [float](48 * $scale))
    $g.FillRectangle($whiteBrush, [float](236 * $scale), [float](184 * $scale), [float](40 * $scale), [float](40 * $scale))

    # EECC text
    $fontGold = New-Object System.Drawing.Font ("Segoe UI", [float](34 * $scale), [System.Drawing.FontStyle]::Bold)
    $sfCenter = New-Object System.Drawing.StringFormat
    $sfCenter.Alignment = [System.Drawing.StringAlignment]::Center
    $sfCenter.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("EECC", $fontGold, $goldBrush, [float](256 * $scale), [float](424 * $scale), $sfCenter)

    $g.Dispose()
    return $bmp
}

# 4. GLOBAL / WEB PORTAL
$globalBmp = Create-Global-Bitmap 512
$globalBmp.Save("assets\logos\eecc_global_logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$globalBmp.Save("apps\web_portal\public\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$globalBmp.Save("apps\web_portal\public\logo.jpeg", [System.Drawing.Imaging.ImageFormat]::Jpeg)
$globalBmp.Save("apps\web_portal\public\icon-512.png", [System.Drawing.Imaging.ImageFormat]::Png)
$globalBmp.Save("apps\web_portal\public\apple-touch-icon.png", [System.Drawing.Imaging.ImageFormat]::Png)

$global192 = New-Object System.Drawing.Bitmap 192, 192
$gWeb = [System.Drawing.Graphics]::FromImage($global192)
$gWeb.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gWeb.DrawImage($globalBmp, 0, 0, 192, 192)
$gWeb.Dispose()
$global192.Save("apps\web_portal\public\icon-192.png", [System.Drawing.Imaging.ImageFormat]::Png)
$global192.Dispose()

Save-Ico-File $globalBmp "apps\web_portal\public\favicon.ico"
$globalBmp.Dispose()
Write-Host "EECC Global & Web Portal logos générés avec succès !"
