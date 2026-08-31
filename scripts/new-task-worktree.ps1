param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$TaskName,

    [string]$RepositoryPath = (Get-Location).Path,

    [string]$BaseBranch = 'main',

    [datetimeoffset]$Timestamp = [datetimeoffset]::Now
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & git -C $WorkingDirectory @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        $detail = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        throw "Git-Befehl fehlgeschlagen: git $($Arguments -join ' ')`n$detail"
    }

    return (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
}

$resolvedRepositoryPath = [System.IO.Path]::GetFullPath($RepositoryPath)
if (-not (Test-Path -LiteralPath $resolvedRepositoryPath -PathType Container)) {
    throw "Das Repository-Verzeichnis fehlt: $resolvedRepositoryPath"
}

$currentRoot = Invoke-Git $resolvedRepositoryPath @('rev-parse', '--show-toplevel')
$commonGitDirectory = Invoke-Git $resolvedRepositoryPath @('rev-parse', '--path-format=absolute', '--git-common-dir')
$commonGitDirectory = [System.IO.Path]::GetFullPath($commonGitDirectory)
$repositoryRoot = [System.IO.Path]::GetDirectoryName($commonGitDirectory)
if ([System.IO.Path]::GetFileName($commonGitDirectory) -ne '.git' -or
    [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Das gemeinsame Git-Verzeichnis ist unerwartet: $commonGitDirectory"
}

if (-not [System.IO.Path]::GetFullPath($currentRoot).Equals(
        [System.IO.Path]::GetFullPath($repositoryRoot),
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Die Aufgabe befindet sich bereits in einem verknüpften Worktree: $currentRoot"
}

& git -C $repositoryRoot check-ignore --quiet -- '.worktrees/'
if ($LASTEXITCODE -ne 0) {
    throw "Die Worktree-Ablage '$repositoryRoot\.worktrees' ist nicht ignoriert. Ergänze zuerst '.worktrees/' in .gitignore."
}

$slug = $TaskName.Trim().ToLowerInvariant()
$slug = [System.Text.RegularExpressions.Regex]::Replace($slug, '[^a-z0-9]+', '-')
$slug = $slug.Trim('-')
if ([string]::IsNullOrWhiteSpace($slug)) {
    throw 'Der Aufgabenname enthält keine verwendbaren Zeichen.'
}

$stamp = $Timestamp.ToUniversalTime().ToString('yyyyMMdd-HHmmss')
$branch = "task/$slug-$stamp"
$worktreeName = "$slug-$stamp"
$worktreeRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.worktrees'))
$worktreePath = [System.IO.Path]::GetFullPath((Join-Path $worktreeRoot $worktreeName))

Invoke-Git $repositoryRoot @('rev-parse', '--verify', "$BaseBranch^{commit}") | Out-Null

& git -C $repositoryRoot show-ref --verify --quiet "refs/heads/$branch"
if ($LASTEXITCODE -eq 0) {
    throw "Der Aufgabenbranch existiert bereits: $branch"
}

if (Test-Path -LiteralPath $worktreePath) {
    throw "Das Worktree-Verzeichnis existiert bereits: $worktreePath"
}

Invoke-Git $repositoryRoot @(
    'worktree',
    'add',
    '--no-track',
    '-b',
    $branch,
    $worktreePath,
    $BaseBranch
) | Out-Null

[pscustomobject]@{
    Branch = $branch
    WorktreePath = $worktreePath
    BaseBranch = $BaseBranch
    RepositoryRoot = [System.IO.Path]::GetFullPath($repositoryRoot)
}
