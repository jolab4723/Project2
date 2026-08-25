param(
    [Parameter(Mandatory = $true)]
    [string]$ReferenceDirectory,

    [Parameter(Mandatory = $true)]
    [hashtable]$SourceByView,

    [hashtable]$ScaleByView = @{},

    [switch]$MirrorLeftForRight
)

Add-Type -AssemblyName System.Drawing

function Convert-ToTransparentBitmap {
    param([System.Drawing.Bitmap]$Source)

    $result = [System.Drawing.Bitmap]::new(
        $Source.Width,
        $Source.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $hasAlpha = $Source.PixelFormat.ToString().Contains('Argb')
    for ($y = 0; $y -lt $Source.Height; $y++) {
        for ($x = 0; $x -lt $Source.Width; $x++) {
            $pixel = $Source.GetPixel($x, $y)
            if ($hasAlpha) {
                $result.SetPixel($x, $y, $pixel)
                continue
            }

            $minimum = [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
            $maximum = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B))
            $chroma = $maximum - $minimum
            $isNeutralLightBackground = $minimum -ge 215 -and $chroma -le 10
            if ($isNeutralLightBackground) {
                $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            }
            else {
                $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.G, $pixel.B))
            }
        }
    }

    return $result
}

function Resize-ToFinalCanvas {
    param(
        [System.Drawing.Bitmap]$Source,
        [double]$Scale = 1.0
    )

    $result = [System.Drawing.Bitmap]::new(
        2048,
        1024,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $width = [int][Math]::Round(2048 * $Scale)
        $height = [int][Math]::Round(1024 * $Scale)
        $left = [int][Math]::Round((2048 - $width) / 2.0)
        $top = [int][Math]::Round((1024 - $height) / 2.0)
        $graphics.DrawImage($Source, $left, $top, $width, $height)
    }
    finally {
        $graphics.Dispose()
    }

    return $result
}

foreach ($view in @('front', 'left', 'back', 'right')) {
    $sourceView = if ($view -eq 'right' -and $MirrorLeftForRight) { 'left' } else { $view }
    $sourcePath = Join-Path $ReferenceDirectory $SourceByView[$sourceView]
    $destinationPath = Join-Path $ReferenceDirectory "$view.png"
    $source = [System.Drawing.Bitmap]::new($sourcePath)
    try {
        $transparent = Convert-ToTransparentBitmap -Source $source
        try {
            if ($view -eq 'right' -and $MirrorLeftForRight) {
                $transparent.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
            }
            $viewScale = if ($ScaleByView.ContainsKey($view)) { [double]$ScaleByView[$view] } else { 1.0 }
            $final = Resize-ToFinalCanvas -Source $transparent -Scale $viewScale
            try {
                $final.Save($destinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally {
                $final.Dispose()
            }
        }
        finally {
            $transparent.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}
