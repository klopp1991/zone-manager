#requires -Version 7.0
<#
.SYNOPSIS
    Schreibt die naechste Produktversion nach dem Schema YYYY.MMDD.NN in Directory.Build.props.

.DESCRIPTION
    NN beginnt an jedem Tag bei 01 und zaehlt je Release des Tages um eins hoch. Als bereits
    vergeben gelten sowohl vorhandene Git-Tags des Tages als auch die Version, die aktuell in
    Directory.Build.props steht. Ohne -Increment ermittelt das Skript daraus den naechsten freien
    Wert.

    Assemblys koennen keine fuehrenden Nullen speichern; Version, AssemblyVersion und FileVersion
    erhalten deshalb die numerische Schreibweise (2026.831.1), waehrend InformationalVersion die
    Anzeigeform (2026.0831.01) traegt. Die Anwendung zeigt ausschliesslich die Anzeigeform.
#>
param(
    [datetimeoffset]$Date = [datetimeoffset]::Now,

    [ValidateRange(1, 99999)]
    [int]$Increment,

    [string]$RepositoryPath = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),

    [switch]$WhatIfOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath($RepositoryPath)
$propsPath = Join-Path $repositoryRoot 'Directory.Build.props'
$year = $Date.ToString('yyyy')
$monthDay = $Date.ToString('MMdd')
$dayPrefix = "$year.$monthDay."

function Get-IncrementFromVersion {
    param([string]$Version)

    if ([string]::IsNullOrWhiteSpace($Version) -or -not $Version.StartsWith($dayPrefix, [System.StringComparison]::Ordinal)) {
        return 0
    }

    $tail = $Version.Substring($dayPrefix.Length)
    $parsed = 0
    if ([int]::TryParse($tail, [ref]$parsed)) {
        return $parsed
    }

    return 0
}

function Get-CurrentDisplayVersion {
    if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
        return ''
    }

    $document = [xml](Get-Content -LiteralPath $propsPath -Raw)
    $node = $document.SelectSingleNode('/Project/PropertyGroup/ZoneManagerVersion')
    if ($null -eq $node) {
        return ''
    }

    return $node.InnerText.Trim()
}

function Get-HighestTaggedIncrement {
    & git -C $repositoryRoot rev-parse --git-dir 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        return 0
    }

    $tags = & git -C $repositoryRoot tag --list "v$dayPrefix*"
    if ($LASTEXITCODE -ne 0 -or $null -eq $tags) {
        return 0
    }

    $highest = 0
    foreach ($tag in @($tags)) {
        $candidate = Get-IncrementFromVersion -Version $tag.Trim().TrimStart('v')
        if ($candidate -gt $highest) {
            $highest = $candidate
        }
    }

    return $highest
}

if ($PSBoundParameters.ContainsKey('Increment')) {
    $nextIncrement = $Increment
}
else {
    $used = @(
        (Get-HighestTaggedIncrement),
        (Get-IncrementFromVersion -Version (Get-CurrentDisplayVersion))
    ) | Measure-Object -Maximum
    $nextIncrement = [int]$used.Maximum + 1
}

$displayVersion = '{0}{1:D2}' -f $dayPrefix, $nextIncrement
$assemblyVersion = '{0}.{1}.{2}' -f $year, [int]$monthDay, $nextIncrement

$content = @"
<Project>
  <!--
    Produktversion nach dem Schema YYYY.MMDD.NN. NN startet taeglich bei 01.
    Diese Datei wird von scripts/set-version.ps1 erzeugt; nicht von Hand bearbeiten.
    ZoneManagerVersion ist die Anzeigeform, die numerischen Felder tragen dieselbe
    Version ohne fuehrende Nullen, weil Assemblys keine speichern koennen.
  -->
  <PropertyGroup>
    <ZoneManagerVersion>$displayVersion</ZoneManagerVersion>
    <Version>$assemblyVersion</Version>
    <AssemblyVersion>$assemblyVersion</AssemblyVersion>
    <FileVersion>$assemblyVersion</FileVersion>
    <InformationalVersion>$displayVersion</InformationalVersion>
    <!-- Ohne diesen Schalter haengt das SDK die Commit-Id an die InformationalVersion. -->
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
  </PropertyGroup>
</Project>
"@

if (-not $WhatIfOnly) {
    Set-Content -LiteralPath $propsPath -Value $content -Encoding utf8NoBOM
}

[pscustomobject]@{
    DisplayVersion  = $displayVersion
    AssemblyVersion = $assemblyVersion
    Tag             = "v$displayVersion"
    PropsPath       = $propsPath
    Written         = -not $WhatIfOnly
}
