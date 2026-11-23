<#
.SYNOPSIS
PowerShell wrapper for Tools/agent-sync.sh on Windows.
Finds Git Bash automatically so Antigravity, Codex, and PowerShell users can run
`pwsh -NoProfile -File Tools/agent-sync.ps1 <command> [args...]` without `bash` in PATH.
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$syncSh = Join-Path $scriptDir 'agent-sync.sh'
if (-not (Test-Path $syncSh)) {
    Write-Error "agent-sync.sh not found at $syncSh"
    exit 2
}

# Git Bash first: a bare `bash` on PATH can be WSL (System32\bash.exe), which cannot run this checkout's git.
$bashCandidates = @()
$gitCmd = Get-Command git -ErrorAction SilentlyContinue
if ($gitCmd) {
    $gitRoot = Split-Path -Parent (Split-Path -Parent $gitCmd.Source)
    $bashCandidates += (Join-Path $gitRoot 'bin\bash.exe')
}
$bashCandidates += @(
    'C:\Program Files\Git\bin\bash.exe',
    'C:\Program Files (x86)\Git\bin\bash.exe'
)
$bashCmd = Get-Command bash -ErrorAction SilentlyContinue
if ($bashCmd -and $bashCmd.Source -notlike '*\System32\*') {
    $bashCandidates += $bashCmd.Source
}

$bashExe = $bashCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $bashExe) {
    Write-Error 'Git Bash (bash.exe) not found. Install Git for Windows or add bash.exe to PATH.'
    exit 2
}

$syncShPosix = $syncSh -replace '\\', '/'
& $bashExe $syncShPosix @RemainingArgs
exit $LASTEXITCODE
