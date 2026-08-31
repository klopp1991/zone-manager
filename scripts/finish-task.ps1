<#
.SYNOPSIS
Schliesst eine Aufgabe ab: auf origin/main rebasen, pruefen, main vorziehen, pushen, aufraeumen.

.DESCRIPTION
Das Skript ist der einzige vorgesehene Weg, Arbeit nach main zu bringen. Es laeuft sowohl im
primaeren Worktree auf main als auch in einem verknuepften Aufgaben-Worktree.
Es bricht ab, sobald etwas nicht sauber ist, und hinterlaesst in dem Fall den Aufgabenstand
unveraendert, damit die Ursache in derselben Aufgabe behoben werden kann.
#>
param(
    [string]$Path = (Get-Location).Path,

    [string]$Remote = 'origin',

    [string]$BaseBranch = 'main',

    # Umfang der Pruefung vor der Integration.
    #   Tests  Standard. Nur die Testsuite, ohne Publish und ohne Root-EXE.
    #   Full   Der vollstaendige Release-Lauf scripts/verify.ps1. Er erneuert die Root-EXE und
    #          gehoert deshalb nur in eine Release-Aufgabe.
    #   None   Keine Pruefung. Nur zulaessig, wenn sie in dieser Aufgabe nachweislich schon
    #          auf dem endgueltigen Stand gelaufen ist.
    [ValidateSet('Tests', 'Full', 'None')]
    [string]$Check = 'Tests',

    # Rebase, Pruefung und Fast-Forward laufen lokal, der Push unterbleibt.
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & git -C $WorkingDirectory @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($exitCode -ne 0) {
        $detail = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        throw "Git-Befehl fehlgeschlagen: git $($Arguments -join ' ')`n$detail"
    }

    return (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
}

function Test-GitSucceeds {
    param(
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & git -C $WorkingDirectory @Arguments *> $null
    return $LASTEXITCODE -eq 0
}

$taskPath = [System.IO.Path]::GetFullPath($Path)
if (-not (Test-Path -LiteralPath $taskPath -PathType Container)) {
    throw "Das Arbeitsverzeichnis fehlt: $taskPath"
}

$taskRoot = [System.IO.Path]::GetFullPath((Invoke-Git $taskPath @('rev-parse', '--show-toplevel')))
$commonGitDirectory = [System.IO.Path]::GetFullPath((Invoke-Git $taskPath @('rev-parse', '--path-format=absolute', '--git-common-dir')))
$repositoryRoot = [System.IO.Path]::GetDirectoryName($commonGitDirectory)
if ([System.IO.Path]::GetFileName($commonGitDirectory) -ne '.git' -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Das gemeinsame Git-Verzeichnis ist unerwartet: $commonGitDirectory"
}
$repositoryRoot = [System.IO.Path]::GetFullPath($repositoryRoot)

$isPrimaryWorktree = $taskRoot.Equals($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)
$taskBranch = Invoke-Git $taskPath @('rev-parse', '--abbrev-ref', 'HEAD')
if ($taskBranch -eq 'HEAD') {
    throw 'Die Aufgabe steht auf einem losgeloesten HEAD. Zuerst einen Aufgabenbranch anlegen.'
}

if ((Invoke-Git $taskPath @('status', '--porcelain')) -ne '') {
    throw "Der Arbeitsbaum ist nicht sauber: $taskRoot`nZuerst alle Aenderungen commiten."
}

if ($isPrimaryWorktree -and $taskBranch -ne $BaseBranch) {
    throw "Im primaeren Worktree wird nur '$BaseBranch' abgeschlossen, gefunden wurde '$taskBranch'."
}
if (-not $isPrimaryWorktree -and $taskBranch -eq $BaseBranch) {
    throw "Ein Aufgaben-Worktree darf nicht auf '$BaseBranch' stehen."
}

Write-Host "Aufgabe:    $taskBranch"
Write-Host "Worktree:   $taskRoot"
Write-Host "Repository: $repositoryRoot"

Invoke-Git $taskPath @('fetch', $Remote, $BaseBranch) | Out-Null
$upstream = "$Remote/$BaseBranch"
$upstreamCommit = Invoke-Git $taskPath @('rev-parse', "$upstream^{commit}")

# Rebase statt Merge, damit main linear bleibt und ein zweiter Agent sauber darauf aufsetzen kann.
if ((Invoke-Git $taskPath @('rev-parse', 'HEAD')) -ne $upstreamCommit -and
    -not (Test-GitSucceeds $taskPath @('merge-base', '--is-ancestor', $upstreamCommit, 'HEAD'))) {
    Write-Host "Rebase auf $upstream ..."
    try {
        Invoke-Git $taskPath @('rebase', $upstream) | Out-Null
    }
    catch {
        & git -C $taskPath rebase --abort *> $null
        throw "Der Rebase auf $upstream ist fehlgeschlagen. Konflikte in dieser Aufgabe aufloesen.`n$_"
    }
}

switch ($Check) {
    'Tests' {
        # Bewusst nur die Testsuite. Der volle Lauf publisht zusaetzlich eine 72-MB-EXE und
        # schreibt die Root-EXE, die laut AGENTS.md ohnehin der Release-Aufgabe vorbehalten ist.
        Write-Host 'Pruefung: dotnet test ...'
        & dotnet test (Join-Path $taskRoot 'ZoneManager.sln') -p:SkipRootExecutablePublish=true
        if ($LASTEXITCODE -ne 0) { throw 'Die Tests sind fehlgeschlagen.' }
    }
    'Full' {
        Write-Host 'Pruefung: scripts/verify.ps1 ...'
        & (Join-Path $taskRoot 'scripts\verify.ps1')
    }
    'None' {
        Write-Host 'Pruefung uebersprungen.'
    }
}

$taskCommit = Invoke-Git $taskPath @('rev-parse', 'HEAD')

if ($taskCommit -eq $upstreamCommit) {
    Write-Host "Nichts zu integrieren; $taskBranch entspricht $upstream."
}
elseif ($isPrimaryWorktree) {
    if (-not $NoPush) {
        Invoke-Git $taskPath @('push', $Remote, "${BaseBranch}:${BaseBranch}") | Out-Null
        Write-Host "$BaseBranch nach $upstream gepusht."
    }
}
else {
    # Der primaere Worktree haelt main. Er wird nur vorgezogen, wenn er selbst sauber ist.
    $primaryBranch = Invoke-Git $repositoryRoot @('rev-parse', '--abbrev-ref', 'HEAD')
    if ($primaryBranch -ne $BaseBranch) {
        throw "Der primaere Worktree steht auf '$primaryBranch' statt auf '$BaseBranch'."
    }
    if ((Invoke-Git $repositoryRoot @('status', '--porcelain')) -ne '') {
        throw "Der primaere Worktree ist nicht sauber: $repositoryRoot"
    }

    Invoke-Git $repositoryRoot @('merge', '--ff-only', $taskCommit) | Out-Null
    Write-Host "$BaseBranch auf $taskCommit vorgezogen."

    if (-not $NoPush) {
        Invoke-Git $repositoryRoot @('push', $Remote, "${BaseBranch}:${BaseBranch}") | Out-Null
        Write-Host "$BaseBranch nach $upstream gepusht."
    }
}

if (-not $isPrimaryWorktree) {
    # Erst aus dem Worktree heraustreten: Windows entfernt kein Verzeichnis,
    # das noch das Arbeitsverzeichnis des laufenden Prozesses ist.
    Set-Location -LiteralPath $repositoryRoot
    Invoke-Git $repositoryRoot @('worktree', 'remove', $taskRoot) | Out-Null
    Invoke-Git $repositoryRoot @('branch', '-D', $taskBranch) | Out-Null
    Write-Host "Worktree und Aufgabenbranch $taskBranch entfernt."
    Write-Host "Arbeitsverzeichnis ist jetzt $repositoryRoot."
}

[pscustomobject]@{
    Branch = $taskBranch
    Commit = $taskCommit
    BaseBranch = $BaseBranch
    Check = $Check
    Pushed = (-not $NoPush) -and ($taskCommit -ne $upstreamCommit)
    RepositoryRoot = $repositoryRoot
}
