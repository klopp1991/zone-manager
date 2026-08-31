param(
    [Parameter(Mandatory = $true)]
    [string]$PublishedExecutablePath,

    [Parameter(Mandatory = $true)]
    [string]$RootExecutablePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256Hash([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            return [System.BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '')
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$workRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'work'))
$sourcePath = [System.IO.Path]::GetFullPath($PublishedExecutablePath)
$destinationPath = [System.IO.Path]::GetFullPath($RootExecutablePath)
$destinationDirectory = [System.IO.Path]::GetDirectoryName($destinationPath)
$defaultDestination = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'ZoneManager.exe'))
$isTestDestination = $destinationPath.StartsWith(
    $workRoot + [System.IO.Path]::DirectorySeparatorChar,
    [System.StringComparison]::OrdinalIgnoreCase)

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Die veröffentlichte EXE fehlt: $sourcePath"
}

if ([System.IO.Path]::GetFileName($destinationPath) -ne 'ZoneManager.exe') {
    throw 'Der Name der Root-EXE ist unerwartet.'
}

if (-not $destinationPath.Equals($defaultDestination, [System.StringComparison]::OrdinalIgnoreCase) -and -not $isTestDestination) {
    throw 'Der Zielpfad liegt weder im Root- noch im Testverzeichnis.'
}

if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
}

$temporaryPath = "$destinationPath.new"
$backupPath = "$destinationPath.previous.$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()).$PID"
$destinationWasMoved = $false

try {
    Copy-Item -LiteralPath $sourcePath -Destination $temporaryPath -Force
    if ((Get-Sha256Hash $sourcePath) -ne (Get-Sha256Hash $temporaryPath)) {
        throw 'Die vorbereitete Root-EXE stimmt nicht mit dem Publish-Artefakt überein.'
    }

    if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
        Move-Item -LiteralPath $destinationPath -Destination $backupPath
        $destinationWasMoved = $true
    }

    try {
        Move-Item -LiteralPath $temporaryPath -Destination $destinationPath
    }
    catch {
        if ($destinationWasMoved -and -not (Test-Path -LiteralPath $destinationPath)) {
            Move-Item -LiteralPath $backupPath -Destination $destinationPath
            $destinationWasMoved = $false
        }

        throw
    }

    if ((Get-Sha256Hash $sourcePath) -ne (Get-Sha256Hash $destinationPath)) {
        throw 'Die Root-EXE stimmt nach dem Austausch nicht mit dem Publish-Artefakt überein.'
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

if ($destinationWasMoved -and (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    try {
        Remove-Item -LiteralPath $backupPath -Force
    }
    catch {
        Write-Warning "Die noch laufende Vorgängerversion bleibt bis zum nächsten Build erhalten: $backupPath"
    }
}

$bytes = (Get-Item -LiteralPath $destinationPath).Length
$hash = Get-Sha256Hash $destinationPath
Write-Output "ROOT_EXE_UPDATED path=$destinationPath bytes=$bytes sha256=$hash"
