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
$worktreeScript = Join-Path $scriptDirectory 'new-task-worktree.ps1'
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testRoot = Join-Path $temporaryRoot ("ZoneManager-WorktreeTest-$([guid]::NewGuid().ToString('N'))")

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    git -C $testRoot init --initial-branch=main | Out-Null
    git -C $testRoot config user.name 'Worktree Test' | Out-Null
    git -C $testRoot config user.email 'worktree-test@example.invalid' | Out-Null
    Set-Content -LiteralPath (Join-Path $testRoot 'README.md') -Value 'Testrepository' -Encoding UTF8
    git -C $testRoot add README.md
    git -C $testRoot commit -m 'Initialer Teststand' | Out-Null

    $ignoreFailure = $null
    try {
        & $worktreeScript `
            -TaskName 'Fenster Regeln' `
            -RepositoryPath $testRoot `
            -Timestamp ([datetimeoffset]'2026-08-31T01:30:00Z') | Out-Null
    }
    catch {
        $ignoreFailure = $_.Exception.Message
    }

    Assert-Equal $true ($ignoreFailure -like '*nicht ignoriert*') 'Eine ungeschützte .worktrees-Ablage wurde nicht abgewiesen.'

    Set-Content -LiteralPath (Join-Path $testRoot '.gitignore') -Value ".worktrees/`n" -Encoding UTF8
    git -C $testRoot add .gitignore
    git -C $testRoot commit -m 'Ignoriere Aufgaben-Worktrees' | Out-Null

    $first = & $worktreeScript `
        -TaskName 'Fenster Regeln' `
        -RepositoryPath $testRoot `
        -Timestamp ([datetimeoffset]'2026-08-31T01:30:00Z')
    $second = & $worktreeScript `
        -TaskName 'Fenster Regeln' `
        -RepositoryPath $testRoot `
        -Timestamp ([datetimeoffset]'2026-08-31T01:30:01Z')

    Assert-Equal 'task/fenster-regeln-20260831-013000' $first.Branch 'Der erste Aufgabenbranch ist nicht deterministisch.'
    Assert-Equal 'task/fenster-regeln-20260831-013001' $second.Branch 'Der zweite Aufgabenbranch ist nicht eindeutig.'
    Assert-Equal 'task/fenster-regeln-20260831-013000' (git -C $first.WorktreePath branch --show-current) 'Der erste Worktree verwendet den falschen Branch.'
    Assert-Equal 'task/fenster-regeln-20260831-013001' (git -C $second.WorktreePath branch --show-current) 'Der zweite Worktree verwendet den falschen Branch.'
    Assert-Equal 'main' (git -C $testRoot branch --show-current) 'Der Haupt-Worktree wurde umgeschaltet.'
    Assert-Equal $true (Test-Path -LiteralPath $first.WorktreePath -PathType Container) 'Der erste Worktree fehlt.'
    Assert-Equal $true (Test-Path -LiteralPath $second.WorktreePath -PathType Container) 'Der zweite Worktree fehlt.'

    Write-Output 'WORKTREE_TEST_OK tasks=2 isolated=true mainUnchanged=true'
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Path]::GetFileName($resolvedTestRoot).StartsWith('ZoneManager-WorktreeTest-', [System.StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
