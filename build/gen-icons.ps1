# Generates the Object1688 icon matrix into resources/icons (architecture 8.1 / F-70 / AC-14/62/77/79).
# Pure ASCII (avoids PowerShell 5.1 UTF-8 no-BOM parsing issues).
# Style: geometric "O" ring on a dark rounded square (exe) / ring glyph (tray, themed + state variants).

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "resources\icons"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$script:white = [System.Drawing.Color]::White
$script:dark = [System.Drawing.Color]::FromArgb(255, 32, 32, 32)
$script:bg = [System.Drawing.Color]::FromArgb(255, 30, 30, 30)
$script:red = [System.Drawing.Color]::FromArgb(255, 220, 48, 48)

function New-Canvas([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    return @($bmp, $g)
}

function Save-Png([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return $bytes
}

function Write-Ico([System.Collections.IEnumerable]$frames, [string]$outPath) {
    $list = @($frames)
    $count = $list.Count
    $ms = New-Object System.IO.MemoryStream
    $header = New-Object byte[] 6
    $header[2] = 1
    $header[4] = [byte]($count -band 0xFF)
    $header[5] = [byte](($count -shr 8) -band 0xFF)
    $ms.Write($header, 0, 6)
    $offset = 6 + 16 * $count
    foreach ($f in $list) {
        $e = New-Object byte[] 16
        $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
        $e[0] = [byte]$dim
        $e[1] = [byte]$dim
        $e[6] = 32
        $len = $f.Bytes.Length
        $e[8] = [byte]($len -band 0xFF)
        $e[9] = [byte](($len -shr 8) -band 0xFF)
        $e[10] = [byte](($len -shr 16) -band 0xFF)
        $e[11] = [byte](($len -shr 24) -band 0xFF)
        $e[12] = [byte]($offset -band 0xFF)
        $e[13] = [byte](($offset -shr 8) -band 0xFF)
        $e[14] = [byte](($offset -shr 16) -band 0xFF)
        $e[15] = [byte](($offset -shr 24) -band 0xFF)
        $ms.Write($e, 0, 16)
        $offset += $len
    }
    foreach ($f in $list) { $ms.Write($f.Bytes, 0, $f.Bytes.Length) }
    [System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $ms.Dispose()
}

# App icon: dark rounded square + white ring
function New-AppFrame([int]$size) {
    $pair = New-Canvas $size
    $bmp = $pair[0]; $g = $pair[1]
    $bgBrush = New-Object System.Drawing.SolidBrush($script:bg)
    $inset = [float]($size * 0.06)
    $g.FillEllipse($bgBrush, $inset, $inset, $size - 2 * $inset, $size - 2 * $inset)
    $pen = New-Object System.Drawing.Pen($script:white, [float][Math]::Max(1.0, $size * 0.12))
    $m = [float]($size * 0.30)
    $g.DrawEllipse($pen, $m, $m, $size - 2 * $m, $size - 2 * $m)
    $g.Dispose()
    $bytes = Save-Png $bmp
    $bmp.Dispose()
    return @{ Size = $size; Bytes = $bytes }
}

# Tray glyph: ring + state decoration; theme light => white glyph, dark => dark glyph
function New-TrayFrame([int]$size, [string]$theme, [string]$state) {
    $pair = New-Canvas $size
    $bmp = $pair[0]; $g = $pair[1]
    $color = if ($theme -eq 'dark') { $script:dark } else { $script:white }
    $pen = New-Object System.Drawing.Pen($color, [float][Math]::Max(1.0, $size * 0.13))
    $m = [float]($size * 0.18)
    $g.DrawEllipse($pen, $m, $m, $size - 2 * $m, $size - 2 * $m)
    $brush = New-Object System.Drawing.SolidBrush($color)
    if ($state -eq 'paused') {
        $g.FillRectangle($brush, [float]($size * 0.40), [float]($size * 0.34), [float][Math]::Max(1, $size * 0.08), [float]($size * 0.32))
        $g.FillRectangle($brush, [float]($size * 0.52), [float]($size * 0.34), [float][Math]::Max(1, $size * 0.08), [float]($size * 0.32))
    } elseif ($state -eq 'dnd') {
        $pen2 = New-Object System.Drawing.Pen($color, [float][Math]::Max(1.0, $size * 0.10))
        $g.DrawLine($pen2, [float]($size * 0.32), [float]($size * 0.68), [float]($size * 0.68), [float]($size * 0.32))
        $pen2.Dispose()
    } elseif ($state -eq 'error') {
        $redBrush = New-Object System.Drawing.SolidBrush($script:red)
        $g.FillEllipse($redBrush, [float]($size * 0.58), [float]($size * 0.58), [float]($size * 0.34), [float]($size * 0.34))
        $redBrush.Dispose()
    }
    $brush.Dispose(); $pen.Dispose(); $g.Dispose()
    $bytes = Save-Png $bmp
    $bmp.Dispose()
    return @{ Size = $size; Bytes = $bytes }
}

# --- exe / window icon ---
$appSizes = @(16, 24, 32, 48, 64, 256)
$appFrames = $appSizes | ForEach-Object { New-AppFrame $_ }
Write-Ico $appFrames (Join-Path $outDir "app.ico")
Write-Ico (@($appFrames | Where-Object { $_.Size -le 48 })) (Join-Path $outDir "window.ico")

# --- tray matrix: 4 states x 2 themes ---
$traySizes = @(16, 24, 32)
foreach ($state in @("normal", "paused", "dnd", "error")) {
    foreach ($theme in @("light", "dark")) {
        $frames = $traySizes | ForEach-Object { New-TrayFrame $_ $theme $state }
        Write-Ico $frames (Join-Path $outDir ("tray_{0}_{1}.ico" -f $state, $theme))
    }
}

# --- about png ---
foreach ($size in @(64, 128)) {
    $pair = New-Canvas $size
    $bmp = $pair[0]; $g = $pair[1]
    $bgBrush = New-Object System.Drawing.SolidBrush($script:bg)
    $g.FillEllipse($bgBrush, 0, 0, $size, $size)
    $pen = New-Object System.Drawing.Pen($script:white, [float]($size * 0.12))
    $m = [float]($size * 0.30)
    $g.DrawEllipse($pen, $m, $m, $size - 2 * $m, $size - 2 * $m)
    $g.Dispose()
    $bmp.Save((Join-Path $outDir ("about_{0}.png" -f $size)), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Write-Output ("Icons written to " + $outDir)
Get-ChildItem $outDir | ForEach-Object { Write-Output ("  " + $_.Name + " (" + $_.Length + " bytes)") }
