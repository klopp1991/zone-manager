<#
.SYNOPSIS
    Prueft scripts/get-msi-version.ps1: die Abbildung der Anzeigeversion auf die MSI-Version.
#>
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionScript = Join-Path $scriptDirectory 'get-msi-version.ps1'
$passed = 0

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$Actual,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Erwartet: '$Expected'; erhalten: '$Actual'."
    }

    $script:passed++
}

function Assert-Rejected {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayVersion,
        [Parameter(Mandatory = $true)][string]$Message
    )

    try {
        & $versionScript -DisplayVersion $DisplayVersion | Out-Null
    }
    catch {
        $script:passed++
        return
    }

    throw $Message
}

Assert-Equal '26.9.902' (& $versionScript -DisplayVersion '2026.0909.02') 'Die Abbildung der Anzeigeversion ist unerwartet.'
Assert-Equal '26.9.901' (& $versionScript -DisplayVersion '2026.0909.01') 'Der erste Stand des Tages ist unerwartet.'
Assert-Equal '26.1.501' (& $versionScript -DisplayVersion '2026.0105.01') 'Fuehrende Nullen in Monat und Tag werden falsch gelesen.'
Assert-Equal '27.12.3101' (& $versionScript -DisplayVersion '2027.1231.01') 'Der letzte Tag des Jahres ist unerwartet.'

# Windows ersetzt eine vorhandene Installation nur, wenn die neue Version groesser ist. Die Abbildung
# muss deshalb dieselbe Reihenfolge tragen wie die Anzeigeversion — ueber Tag, Monat und Jahr hinweg.
$ordered = @('2026.0909.01', '2026.0909.02', '2026.0910.01', '2026.1001.01', '2027.0101.01')
$previous = $null
foreach ($display in $ordered) {
    $current = [version](& $versionScript -DisplayVersion $display)
    if ($null -ne $previous -and $current -le $previous) {
        throw "Die MSI-Version steigt bei '$display' nicht: $previous -> $current."
    }

    $previous = $current
    $passed++
}

Assert-Rejected -DisplayVersion '2026.9.1' -Message 'Eine Version ohne fuehrende Nullen wurde angenommen.'
Assert-Rejected -DisplayVersion '2026.0909.00' -Message 'Der Zaehler 00 wurde angenommen.'
Assert-Rejected -DisplayVersion '2026.0909.100' -Message 'Mehr als 99 Staende am Tag wurden angenommen.'
Assert-Rejected -DisplayVersion '2026.1309.01' -Message 'Der Monat 13 wurde angenommen.'
Assert-Rejected -DisplayVersion 'v2026.0909.01' -Message 'Ein Tagname wurde als Version angenommen.'

Write-Output "INSTALLER_VERSION_TESTS_PASSED=$passed"
