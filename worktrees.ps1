# Delegate to the project-owned shell entry point using Git for Windows.
$git = Get-Command git.exe -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $git) {
    Write-Error 'Git for Windows was not found. Install it and make git.exe available on PATH.' -ErrorAction Continue
    exit 2
}

# Preserve native exit codes even when the caller enables PowerShell's native error handling.
$PSNativeCommandUseErrorActionPreference = $false
$gitExecPath = & $git.Source --exec-path
if ($LASTEXITCODE -ne 0 -or -not $gitExecPath) {
    Write-Error 'Could not locate Git Bash. Check your Git for Windows installation.' -ErrorAction Continue
    exit 2
}

# Git for Windows reports <install>/<architecture>/libexec/git-core.
$gitRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $gitExecPath))
$bash = Join-Path $gitRoot 'bin\bash.exe'
if (-not (Test-Path -LiteralPath $bash -PathType Leaf)) {
    Write-Error "Git Bash was not found at '$bash'. Check your Git for Windows installation." -ErrorAction Continue
    exit 2
}

$wrapper = Join-Path $PSScriptRoot 'worktrees.sh'
if (-not (Test-Path -LiteralPath $wrapper -PathType Leaf)) {
    Write-Error "The companion script was not found: '$wrapper'." -ErrorAction Continue
    exit 2
}

Push-Location -LiteralPath $PSScriptRoot -ErrorAction Stop
try {
    & $bash './worktrees.sh' @args
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $code
