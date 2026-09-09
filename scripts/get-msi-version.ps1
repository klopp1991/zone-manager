<#
.SYNOPSIS
    Rechnet die Anzeigeversion YYYY.MMDD.NN in die dreiteilige Version um, die ein MSI tragen kann.

.DESCRIPTION
    Ein MSI erlaubt nur «Major.Minor.Build» mit Major und Minor bis 255 und Build bis 65535; weder das
    Jahr 2026 noch die Zahl 0909 passen dort hinein. Windows vergleicht ausserdem nur diese drei Felder,
    wenn es entscheidet, ob eine vorhandene Installation aelter ist und ersetzt werden darf — die
    Abbildung muss also in derselben Reihenfolge steigen wie die Anzeigeversion.

        Jahr - 2000     ->  Major   (26)
        Monat           ->  Minor   (9)
        Tag * 100 + NN  ->  Build   (902)

    2026.0909.02 wird damit zu 26.9.902. Innerhalb eines Monats steigt der Build mit Tag und Zaehler,
    zum Monatswechsel steigt der Minor, zum Jahreswechsel der Major. Mehr als 99 Veroeffentlichungen an
    einem Tag liefen in den naechsten Tag hinein und werden deshalb abgelehnt.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$DisplayVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($DisplayVersion -notmatch '^(\d{4})\.(\d{2})(\d{2})\.(\d{1,5})$') {
    throw "Die Version '$DisplayVersion' folgt nicht dem Schema YYYY.MMDD.NN."
}

$year = [int]$Matches[1]
$month = [int]$Matches[2]
$day = [int]$Matches[3]
$increment = [int]$Matches[4]

if ($year -lt 2000 -or $year -gt 2255) { throw "Das Jahr $year laesst sich nicht in eine MSI-Version abbilden." }
if ($month -lt 1 -or $month -gt 12) { throw "Der Monat $month ist unmoeglich." }
if ($day -lt 1 -or $day -gt 31) { throw "Der Tag $day ist unmoeglich." }
if ($increment -lt 1 -or $increment -gt 99) { throw 'Mehr als 99 Veroeffentlichungen an einem Tag sind nicht abbildbar.' }

'{0}.{1}.{2}' -f ($year - 2000), $month, ($day * 100 + $increment)
