param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $projectRoot 'src\SnapZones.App\Assets\SaschaWindowZones.ico'
}
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$expectedDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'src\SnapZones.App\Assets'))
if (-not $resolvedOutput.StartsWith($expectedDirectory + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Icon-Pfad liegt ausserhalb des vorgesehenen Asset-Ordners.'
}

Add-Type -AssemblyName PresentationCore, WindowsBase

function New-IconPng {
    param([int]$Size)

    $visual = [System.Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    try {
        $scale = $Size / 256.0
        $drawing.PushTransform([System.Windows.Media.ScaleTransform]::new($scale, $scale))
        $blue = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(47, 111, 237))
        $navy = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(18, 32, 58))
        $white = [System.Windows.Media.Brushes]::White
        $soft = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(143, 178, 255))
        $drawing.DrawRoundedRectangle($blue, $null, [System.Windows.Rect]::new(8, 8, 240, 240), 54, 54)
        $drawing.DrawRoundedRectangle($navy, [System.Windows.Media.Pen]::new($white, 10), [System.Windows.Rect]::new(34, 44, 188, 142), 20, 20)
        $drawing.DrawRoundedRectangle($white, $null, [System.Windows.Rect]::new(48, 58, 67, 51), 8, 8)
        $drawing.DrawRoundedRectangle($soft, $null, [System.Windows.Rect]::new(123, 58, 85, 51), 8, 8)
        $drawing.DrawRoundedRectangle($soft, $null, [System.Windows.Rect]::new(48, 117, 86, 55), 8, 8)
        $drawing.DrawRoundedRectangle($soft, $null, [System.Windows.Rect]::new(142, 117, 66, 55), 8, 8)
        $drawing.DrawRoundedRectangle($white, $null, [System.Windows.Rect]::new(122, 184, 12, 28), 6, 6)
        $drawing.DrawRoundedRectangle($white, $null, [System.Windows.Rect]::new(84, 207, 88, 13), 6.5, 6.5)
        $drawing.Pop()
    }
    finally {
        $drawing.Close()
    }

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = [System.IO.MemoryStream]::new()
    try {
        $encoder.Save($memory)
        return $memory.ToArray()
    }
    finally {
        $memory.Dispose()
    }
}

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$images = foreach ($size in $sizes) {
    [pscustomobject]@{ Size = $size; Bytes = (New-IconPng -Size $size) }
}

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
$file = [System.IO.FileStream]::new($resolvedOutput, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)
    $offset = 6 + (16 * $images.Count)
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }
    foreach ($image in $images) {
        $writer.Write([byte[]]$image.Bytes)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Output "ICON_OK path=$resolvedOutput sizes=$($sizes -join ',')"
