#requires -Version 7.0
<#
.SYNOPSIS
    Veroeffentlicht ZoneManager.exe als GitHub-Release-Asset unter der Version YYYY.MMDD.NN.

.DESCRIPTION
    Der Ablauf ist bewusst linear und bricht bei jedem Fehler ab:

      1. Arbeitsbaum muss sauber sein und auf dem Release-Branch stehen.
      2. scripts/set-version.ps1 schreibt die naechste Version des Tages.
      3. scripts/verify.ps1 baut, testet und erneuert die Root-EXE.
      4. Directory.Build.props wird committet, der Tag v<Version> gesetzt und beides gepusht.
      5. Das Release wird mit ZoneManager.exe, ZoneManager.Helper.exe und je einer Pruefsummendatei
         als Anhaenge erstellt.

    Der Fensterhelfer haengt mit am Release, weil das Programm ihn beim Update mit ersetzt; laege nur
    die Programmdatei bei, liefe nach einem Update eine neue Anwendung gegen einen alten Helfer.

    Die EXE liegt bewusst nur am Release und nicht im Repository: sie ist ein Build-Artefakt von
    rund 66 MB, das sonst die Historie dauerhaft vergroessern wuerde.

    Fuer Schritt 5 braucht es entweder ein angemeldetes GitHub CLI (gh auth login) oder ein Token
    in GH_TOKEN bzw. GITHUB_TOKEN mit dem Scope "repo"; ohne beides endet das Skript nach Schritt 4
    mit einer Anleitung, wie das Release von Hand nachgeholt wird.

.PARAMETER Notes
    Beschreibungstext des Releases. Ohne Angabe entstehen die Notizen aus den Commits seit dem
    vorherigen Tag.
#>
param(
    [string]$RepositoryPath = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),
    [string]$Branch = 'main',
    [string]$Notes,
    [ValidateRange(1, 99999)]
    [int]$Increment,
    [switch]$SkipVerify,
    [switch]$SkipDpiCheck,
    [switch]$SkipPush
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath($RepositoryPath)
$executablePath = Join-Path $repositoryRoot 'ZoneManager.exe'
$helperPath = Join-Path $repositoryRoot 'ZoneManager.Helper.exe'

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & git -C $repositoryRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Git-Befehl fehlgeschlagen: git $($Arguments -join ' ')`n$(($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine)"
    }

    return (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
}

$currentBranch = Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD')
if ($currentBranch -ne $Branch) {
    throw "Ein Release entsteht nur auf '$Branch'; aktuell ausgecheckt ist '$currentBranch'."
}

if ((Invoke-Git @('status', '--porcelain')).Length -gt 0) {
    throw 'Der Arbeitsbaum enthaelt uncommitete Aenderungen. Ein Release braucht einen sauberen Stand.'
}

$versionArguments = @{ RepositoryPath = $repositoryRoot }
if ($PSBoundParameters.ContainsKey('Increment')) {
    $versionArguments['Increment'] = $Increment
}

$version = & (Join-Path $scriptDirectory 'set-version.ps1') @versionArguments
Write-Host "RELEASE_VERSION $($version.DisplayVersion) tag=$($version.Tag)"

if ((Invoke-Git @('tag', '--list', $version.Tag)).Length -gt 0) {
    throw "Der Tag $($version.Tag) existiert bereits."
}

if ($SkipVerify) {
    Write-Warning 'Der Prueflauf wurde uebersprungen; die vorhandene ZoneManager.exe wird veroeffentlicht.'
}
else {
    # Die DPI-Pruefung startet die Oberflaeche und braucht eine bestaetigte UAC-Abfrage; ohne
    # interaktive Sitzung bleibt der Lauf daran stehen.
    & (Join-Path $scriptDirectory 'verify.ps1') -SkipDpiCheck:$SkipDpiCheck
}

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "ZoneManager.exe fehlt: $executablePath"
}

if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
    throw "ZoneManager.Helper.exe fehlt: $helperPath"
}

Invoke-Git @('add', '--', 'Directory.Build.props') | Out-Null
if ((Invoke-Git @('status', '--porcelain', '--', 'Directory.Build.props')).Length -gt 0) {
    Invoke-Git @('commit', '-m', "chore: Version $($version.DisplayVersion)") | Out-Null
}

Invoke-Git @('tag', '-a', $version.Tag, '-m', "Zone Manager $($version.DisplayVersion)") | Out-Null

if ($SkipPush) {
    Write-Host 'RELEASE_LOCAL_ONLY Commit und Tag liegen lokal; Push und Release wurden uebersprungen.'
    return
}

Invoke-Git @('push', 'origin', $Branch) | Out-Null
Invoke-Git @('push', 'origin', $version.Tag) | Out-Null

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $previousTag = & git -C $repositoryRoot describe --tags --abbrev=0 "$($version.Tag)^" 2>$null
    $range = if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($previousTag)) { "$previousTag..$($version.Tag)" } else { $version.Tag }
    $log = Invoke-Git @('log', '--no-merges', '--pretty=format:- %s', $range)
    $Notes = if ([string]::IsNullOrWhiteSpace($log)) { "Zone Manager $($version.DisplayVersion)" } else { $log }
}

# Die Pruefsumme ist Pflicht: das Programm laedt eine Datei nur, wenn die Veroeffentlichung die
# zugehoerige .sha256 traegt und deren Inhalt zur geladenen Datei passt. Beide Pruefsummen entstehen
# vor der Anmeldepruefung, damit die Anleitung zum Nachholen auf vorhandene Dateien verweist.
function Write-Checksum {
    param([Parameter(Mandatory = $true)][string]$Path)

    $checksumPath = "$Path.sha256"
    $name = [System.IO.Path]::GetFileName($Path)
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksumPath -Value "$hash *$name" -Encoding ascii
    Write-Host "CHECKSUM sha256=$hash -> $checksumPath"
    return $checksumPath
}

$checksumPath = Write-Checksum -Path $executablePath
$helperChecksumPath = Write-Checksum -Path $helperPath
$assets = @($executablePath, $checksumPath, $helperPath, $helperChecksumPath)
$assetQuoted = ($assets | ForEach-Object { """$_""" }) -join ' '

$ghCommand = Get-Command gh -ErrorAction SilentlyContinue
if ($null -eq $ghCommand) {
    Write-Warning @"
GitHub CLI (gh) ist nicht installiert. Commit und Tag sind gepusht; das Release fehlt noch.
Nachholen: gh release create $($version.Tag) $assetQuoted --title "Zone Manager $($version.DisplayVersion)"
oder auf github.com unter Releases den Tag $($version.Tag) waehlen und alle vier Dateien anhaengen.
"@
    return
}

& gh auth status 2>&1 | Out-Null
$authenticated = $LASTEXITCODE -eq 0
if (-not $authenticated -and [string]::IsNullOrWhiteSpace($env:GH_TOKEN) -and [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    Write-Warning @"
GitHub CLI ist nicht angemeldet. Commit und Tag sind gepusht; das Release fehlt noch.
Nachholen: gh auth login   danach
gh release create $($version.Tag) $assetQuoted --title "Zone Manager $($version.DisplayVersion)"
"@
    return
}

$notesFile = New-TemporaryFile
try {
    Set-Content -LiteralPath $notesFile -Value $Notes -Encoding utf8NoBOM
    & gh release create $version.Tag @assets `
        --repo (Invoke-Git @('config', '--get', 'remote.origin.url')) `
        --title "Zone Manager $($version.DisplayVersion)" `
        --notes-file $notesFile
    if ($LASTEXITCODE -ne 0) {
        throw 'Das GitHub-Release konnte nicht erstellt werden.'
    }
}
finally {
    Remove-Item -LiteralPath $notesFile -Force -ErrorAction SilentlyContinue
}

Write-Host "RELEASE_OK version=$($version.DisplayVersion) tag=$($version.Tag) assets=$(($assets | ForEach-Object { [System.IO.Path]::GetFileName($_) }) -join ',')"
