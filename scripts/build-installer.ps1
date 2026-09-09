#requires -Version 7.0
<#
.SYNOPSIS
    Baut das Installationspaket ZoneManager-Setup-<Version>.msi aus den fertigen Programmdateien.

.DESCRIPTION
    Das Paket enthaelt Programmdatei und Fensterhelfer und legt beide nach «Programme\ZoneManager» —
    dieselbe Stelle, an die sich das Programm ueber «--install» auch selbst installiert. Gebaut wird
    mit dem WiX-Toolset, das als MSBuild-SDK aus NuGet kommt; ein eigens installiertes Werkzeug
    braucht es nicht.

    Die Eingabedateien werden nicht gebaut: das erledigt scripts/verify.ps1, und ohne dessen Lauf
    stimmten Paketinhalt und Release-Anhaenge nicht ueberein.

.PARAMETER Version
    Die Anzeigeversion (YYYY.MMDD.NN). Ohne Angabe wird sie aus Directory.Build.props gelesen.
#>
param(
    [string]$RepositoryPath = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),
    [string]$ExecutablePath,
    [string]$HelperPath,
    [string]$OutputDirectory,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath($RepositoryPath)
$projectPath = Join-Path $repositoryRoot 'setup\ZoneManager.Setup.wixproj'
$iconPath = Join-Path $repositoryRoot 'src\SnapZones.App\Assets\ZoneManager.ico'
$licensePath = Join-Path $repositoryRoot 'LICENSE'
if (-not $ExecutablePath) { $ExecutablePath = Join-Path $repositoryRoot 'ZoneManager.exe' }
if (-not $HelperPath) { $HelperPath = Join-Path $repositoryRoot 'ZoneManager.Helper.exe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'outputs' }

<#
.SYNOPSIS
    Schreibt den Lizenztext als RTF, weil die Lizenzseite des Installationsprogramms nur RTF anzeigt.
#>
function Write-LicenseRtf {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    $lines = Get-Content -LiteralPath $SourcePath
    $builder = [System.Text.StringBuilder]::new()
    # Kopfzeile eines RTF-Dokuments: Zeichensatz, Schriftart, Schriftgroesse 9 pt (halbe Punkte).
    [void]$builder.Append('{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}\fs18' + "`r`n")
    foreach ($line in $lines) {
        # In RTF leiten \, { und } Befehle ein; Zeichen ausserhalb von ASCII werden als Unicode notiert.
        $escaped = $line -replace '([\\{}])', '\$1'
        $encoded = [System.Text.StringBuilder]::new()
        foreach ($character in $escaped.ToCharArray()) {
            if ([int]$character -gt 127) {
                [void]$encoded.Append('\u' + [int]$character + '?')
            }
            else {
                [void]$encoded.Append($character)
            }
        }

        [void]$builder.Append($encoded.ToString() + '\par' + "`r`n")
    }

    [void]$builder.Append('}')
    Set-Content -LiteralPath $DestinationPath -Value $builder.ToString() -Encoding ascii
}

if (-not $Version) {
    $document = [xml](Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw)
    $node = $document.SelectSingleNode('/Project/PropertyGroup/ZoneManagerVersion')
    if ($null -eq $node) { throw 'Directory.Build.props enthaelt keine ZoneManagerVersion.' }
    $Version = $node.InnerText.Trim()
}

foreach ($required in @($ExecutablePath, $HelperPath, $iconPath, $licensePath, $projectPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Fuer das Installationspaket fehlt: $required"
    }
}

$msiVersion = & (Join-Path $scriptDirectory 'get-msi-version.ps1') -DisplayVersion $Version
$buildDirectory = Join-Path $repositoryRoot 'obj\installer'
if (Test-Path -LiteralPath $buildDirectory) {
    Remove-Item -LiteralPath $buildDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $buildDirectory | Out-Null
$licenseRtfPath = Join-Path $buildDirectory 'Lizenz.rtf'
Write-LicenseRtf -SourcePath $licensePath -DestinationPath $licenseRtfPath

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$msiPath = Join-Path $OutputDirectory "ZoneManager-Setup-$Version.msi"
if (Test-Path -LiteralPath $msiPath) {
    Remove-Item -LiteralPath $msiPath -Force
}

dotnet build $projectPath -c Release `
    -p:ZoneManagerExecutablePath=$([System.IO.Path]::GetFullPath($ExecutablePath)) `
    -p:ZoneManagerHelperPath=$([System.IO.Path]::GetFullPath($HelperPath)) `
    -p:ZoneManagerIconPath=$iconPath `
    -p:ZoneManagerLicenseRtfPath=$licenseRtfPath `
    -p:ZoneManagerMsiVersion=$msiVersion `
    -p:ZoneManagerDisplayVersion=$Version `
    -p:OutputPath=$buildDirectory
if ($LASTEXITCODE -ne 0) { throw 'Der Bau des Installationspakets ist fehlgeschlagen.' }

$built = Join-Path $buildDirectory 'ZoneManager-Setup.msi'
if (-not (Test-Path -LiteralPath $built -PathType Leaf)) {
    throw "Das Installationspaket fehlt: $built"
}

Move-Item -LiteralPath $built -Destination $msiPath -Force
$bytes = (Get-Item -LiteralPath $msiPath).Length
Write-Output "INSTALLER_OK path=$msiPath version=$Version msiVersion=$msiVersion bytes=$bytes"
