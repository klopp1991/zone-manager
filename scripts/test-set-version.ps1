#requires -Version 7.0
<#
.SYNOPSIS
    Prueft scripts/set-version.ps1 in einem temporaeren Repository.
#>
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Expected,

        [Parameter(Mandatory = $true)]
        [object]$Actual,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Erwartet: '$Expected'; erhalten: '$Actual'."
    }
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionScript = Join-Path $scriptDirectory 'set-version.ps1'
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testRoot = Join-Path $temporaryRoot ("ZoneManager-VersionTest-$([guid]::NewGuid().ToString('N'))")
$august = [datetimeoffset]'2026-08-31T10:00:00+02:00'
$january = [datetimeoffset]'2027-01-05T10:00:00+01:00'

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    git -C $testRoot init --initial-branch=main | Out-Null
    git -C $testRoot config user.name 'Version Test' | Out-Null
    git -C $testRoot config user.email 'version-test@example.invalid' | Out-Null
    Set-Content -LiteralPath (Join-Path $testRoot 'README.md') -Value 'Testrepository' -Encoding UTF8
    git -C $testRoot add README.md
    git -C $testRoot commit -m 'Initialer Teststand' | Out-Null

    $first = & $versionScript -RepositoryPath $testRoot -Date $august
    Assert-Equal '2026.0831.01' $first.DisplayVersion 'Der erste Lauf eines Tages beginnt nicht bei 01.'
    Assert-Equal '2026.831.1' $first.AssemblyVersion 'Die numerische Version ist unerwartet.'
    Assert-Equal 'v2026.0831.01' $first.Tag 'Der Tagname ist unerwartet.'

    $second = & $versionScript -RepositoryPath $testRoot -Date $august
    Assert-Equal '2026.0831.02' $second.DisplayVersion 'Die bereits geschriebene Version wurde nicht hochgezaehlt.'

    git -C $testRoot add Directory.Build.props | Out-Null
    git -C $testRoot commit -m 'Version 2026.0831.02' | Out-Null
    git -C $testRoot tag 'v2026.0831.07' | Out-Null

    $afterTag = & $versionScript -RepositoryPath $testRoot -Date $august
    Assert-Equal '2026.0831.08' $afterTag.DisplayVersion 'Ein vorhandener Tag wurde nicht beruecksichtigt.'

    $explicit = & $versionScript -RepositoryPath $testRoot -Date $august -Increment 42
    Assert-Equal '2026.0831.42' $explicit.DisplayVersion 'Ein ausdruecklicher Increment wurde nicht uebernommen.'

    $nextDay = & $versionScript -RepositoryPath $testRoot -Date $january
    Assert-Equal '2027.0105.01' $nextDay.DisplayVersion 'Ein neuer Tag beginnt nicht wieder bei 01.'
    Assert-Equal '2027.105.1' $nextDay.AssemblyVersion 'Die numerische Version verliert die fuehrende Null nicht.'

    $document = [xml](Get-Content -LiteralPath (Join-Path $testRoot 'Directory.Build.props') -Raw)
    Assert-Equal '2027.0105.01' $document.Project.PropertyGroup.InformationalVersion 'Die InformationalVersion traegt nicht die Anzeigeform.'
    Assert-Equal '2027.105.1' $document.Project.PropertyGroup.FileVersion 'Die FileVersion traegt nicht die numerische Form.'

    $preview = & $versionScript -RepositoryPath $testRoot -Date $january -WhatIfOnly
    Assert-Equal '2027.0105.02' $preview.DisplayVersion 'Die Vorschau meldet die falsche naechste Version.'
    Assert-Equal $false $preview.Written 'Die Vorschau meldet einen Schreibvorgang.'
    $unchanged = [xml](Get-Content -LiteralPath (Join-Path $testRoot 'Directory.Build.props') -Raw)
    Assert-Equal '2027.0105.01' $unchanged.Project.PropertyGroup.ZoneManagerVersion 'Die Vorschau hat die Datei veraendert.'

    Write-Output 'SET_VERSION_TEST_OK'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
