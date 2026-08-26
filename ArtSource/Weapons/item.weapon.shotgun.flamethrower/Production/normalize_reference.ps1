param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath,

    [ValidateRange(0.1, 1.0)]
    [double]$MaxScale = 1.0
)

Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourcePath))
try {
    $output = New-Object System.Drawing.Bitmap 2048, 1024, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($output)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $scale = [Math]::Min($MaxScale, [Math]::Min(2048.0 / $source.Width, 1024.0 / $source.Height))
            $targetWidth = [Math]::Round($source.Width * $scale)
            $targetHeight = [Math]::Round($source.Height * $scale)
            $offsetX = [Math]::Floor((2048 - $targetWidth) / 2)
            $offsetY = [Math]::Floor((1024 - $targetHeight) / 2)
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImage($source, $offsetX, $offsetY, $targetWidth, $targetHeight)
        }
        finally {
            $graphics.Dispose()
        }

        $output.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $output.Dispose()
    }
}
finally {
    $source.Dispose()
}
