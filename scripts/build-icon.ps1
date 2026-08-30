param(
    [string]$OutputPath,
    [string]$HeaderOutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $projectRoot 'src\SnapZones.App\Assets\SaschaWindowZones.ico'
}
if ([string]::IsNullOrWhiteSpace($HeaderOutputPath)) {
    $HeaderOutputPath = Join-Path $projectRoot 'src\SnapZones.App\Assets\SaschaWindowZones.Header.png'
}
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$resolvedHeaderOutput = [System.IO.Path]::GetFullPath($HeaderOutputPath)
$expectedDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'src\SnapZones.App\Assets'))
if (-not $resolvedOutput.StartsWith($expectedDirectory + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Icon-Pfad liegt ausserhalb des vorgesehenen Asset-Ordners.'
}
if (-not $resolvedHeaderOutput.StartsWith($expectedDirectory + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Header-Icon-Pfad liegt ausserhalb des vorgesehenen Asset-Ordners.'
}

Add-Type -AssemblyName PresentationCore, WindowsBase

function New-IconPng {
    param([int]$Size)

    $visual = [System.Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    try {
        # Die Geometrie wird je Zielgroesse auf ganze Pixel gesetzt, damit kleine Taskbar-Icons scharf bleiben.
        $margin = [Math]::Max(1, [int][Math]::Round($Size * 8.0 / 256.0, [MidpointRounding]::AwayFromZero))
        $iconSize = $Size - (2 * $margin)
        $gap = [Math]::Max(1, [int][Math]::Round($Size * 7.0 / 256.0, [MidpointRounding]::AwayFromZero))
        $zoneArea = $iconSize - $gap
        $leftWidth = [int][Math]::Round($zoneArea * 132.0 / 233.0, [MidpointRounding]::AwayFromZero)
        $rightWidth = $zoneArea - $leftWidth
        $topHeight = [int][Math]::Round($zoneArea * 107.0 / 233.0, [MidpointRounding]::AwayFromZero)
        $bottomHeight = $zoneArea - $topHeight
        $rightX = $margin + $leftWidth + $gap
        $bottomY = $margin + $topHeight + $gap
        $cornerRadius = [Math]::Max(2, [int][Math]::Round($iconSize * 46.0 / 240.0, [MidpointRounding]::AwayFromZero))

        $graphite = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(41, 45, 51))
        $orange = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(255, 90, 31))
        $lightGrey = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(144, 149, 157))
        $slate = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(81, 87, 96))
        $iconBounds = [System.Windows.Rect]::new($margin, $margin, $iconSize, $iconSize)
        $drawing.PushClip([System.Windows.Media.RectangleGeometry]::new($iconBounds, $cornerRadius, $cornerRadius))
        $drawing.DrawRectangle($graphite, $null, [System.Windows.Rect]::new($margin, $margin, $leftWidth, $topHeight))
        $drawing.DrawRectangle($orange, $null, [System.Windows.Rect]::new($rightX, $margin, $rightWidth, $topHeight))
        $drawing.DrawRectangle($lightGrey, $null, [System.Windows.Rect]::new($margin, $bottomY, $leftWidth, $bottomHeight))
        $drawing.DrawRectangle($slate, $null, [System.Windows.Rect]::new($rightX, $bottomY, $rightWidth, $bottomHeight))
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

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
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

$headerImage = $images | Where-Object Size -eq 256 | Select-Object -First 1
[System.IO.File]::WriteAllBytes($resolvedHeaderOutput, $headerImage.Bytes)

Write-Output "ICON_OK path=$resolvedOutput header=$resolvedHeaderOutput sizes=$($sizes -join ',')"
