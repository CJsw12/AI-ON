<#
.SYNOPSIS
  Installs the developer tools AI-ON needs on this PC. Safe to rerun (installs only what is missing).

.DESCRIPTION
  Required: Git, Git LFS, PowerShell 7, ripgrep, uv, .NET SDK 10+, Unity CLI, Serena (uv tool).
  Optional (-IncludeOptional): GitHub CLI, Node.js LTS (AI proxy deploy), Unity Hub, RTK.
  Afterwards: git lfs install, and the Unity CLI skills/plugin for the agent tools on this PC
  (Claude Code, Codex, Antigravity) unless -SkipAgentSetup.
  winget may raise UAC prompts and accepts the winget source/package agreements for you.
  Exit code: 0 = every required tool present, 2 = something required is still missing.
  Background and decisions: docs/plans/Dev-Tooling.md. Usage: AGENTS.md section 7.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-dev.ps1 -CheckOnly
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-dev.ps1 -IncludeOptional
#>
param([switch] $CheckOnly, [switch] $IncludeOptional, [switch] $SkipAgentSetup)

$ErrorActionPreference = 'Continue'   # native tools write progress to stderr; check exit codes instead
$env:UNITY_NO_BANNER = '1'; $env:UNITY_NON_INTERACTIVE = '1'

function Update-SessionPath {
    $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}
function Test-Tool([string] $Name) { [bool](Get-Command $Name -ErrorAction SilentlyContinue) }
function Test-DotNet10 {
    if (-not (Test-Tool 'dotnet')) { return $false }
    return [bool](@(& dotnet --list-sdks 2>$null) -match '^([1-9][0-9])\.')
}

$tools = @(
    @{ Name = 'git';      Required = $true;  Winget = 'Git.Git';                 Test = { Test-Tool 'git' } }
    @{ Name = 'git-lfs';  Required = $true;  Winget = 'GitHub.GitLFS';           Test = { Test-Tool 'git-lfs' } }
    @{ Name = 'pwsh';     Required = $true;  Winget = 'Microsoft.PowerShell';    Test = { Test-Tool 'pwsh' } }
    @{ Name = 'rg';       Required = $true;  Winget = 'BurntSushi.ripgrep.MSVC'; Test = { Test-Tool 'rg' } }
    @{ Name = 'uv';       Required = $true;  Winget = 'astral-sh.uv';            Test = { Test-Tool 'uv' } }
    @{ Name = 'dotnet10'; Required = $true;  Winget = 'Microsoft.DotNet.SDK.10'; Test = { Test-DotNet10 } }
    @{ Name = 'unity';    Required = $true;  Winget = 'Unity.CLI';               Test = { Test-Tool 'unity' } }
    @{ Name = 'serena';   Required = $true;  Test = { Test-Tool 'serena' }
       Install = { if (Test-Tool 'uv') { & uv tool install -p 3.13 serena-agent; & uv tool update-shell } } }
    @{ Name = 'gh';       Required = $false; Winget = 'GitHub.cli';              Test = { Test-Tool 'gh' } }
    @{ Name = 'node';     Required = $false; Winget = 'OpenJS.NodeJS.LTS';       Test = { Test-Tool 'node' } }
    @{ Name = 'unityhub'; Required = $false; Winget = 'Unity.UnityHub';          Test = { Test-Path "$env:ProgramFiles\Unity Hub\Unity Hub.exe" } }
    @{ Name = 'rtk';      Required = $false; Winget = 'rtk-ai.rtk';              Test = { Test-Tool 'rtk' } }
)

Update-SessionPath
$hasWinget = Test-Tool 'winget'
$rows = @()
foreach ($t in $tools) {
    $state = if (& $t.Test) { 'ok' } else { 'missing' }
    $wanted = $t.Required -or $IncludeOptional
    if ($state -eq 'missing' -and $wanted -and -not $CheckOnly) {
        Write-Output "== installing $($t.Name)"
        if ($t.ContainsKey('Install')) { & $t.Install }
        elseif ($hasWinget) {
            & winget install --id $t.Winget -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
        } else { Write-Output "winget not found: install App Installer from Microsoft Store, then rerun." }
        Update-SessionPath
        $state = if (& $t.Test) { 'installed' } else { 'FAILED' }
    } elseif ($state -eq 'missing' -and -not $wanted) { $state = 'skipped (optional)' }
    $rows += [pscustomobject]@{ Tool = $t.Name; Required = $t.Required; State = $state }
}

if (-not $CheckOnly) {
    if (Test-Tool 'git-lfs') { & git lfs install --skip-repo | Out-Null }   # repo hooks come from setup-git-unity.ps1
    if (-not $SkipAgentSetup -and (Test-Tool 'unity')) {
        & unity setup all   # official Unity plugin + skills for agent CLIs found on PATH
        # Desktop apps have no CLI on PATH: install the skills into their folders directly.
        foreach ($c in @(@('claude-code', "$HOME\.claude"), @('codex', "$HOME\.codex"), @('antigravity', "$HOME\.gemini"))) {
            if (Test-Path -LiteralPath $c[1]) { & unity skill sync $c[0] | Out-Null; Write-Output "Unity skills synced: $($c[0])" }
        }
    }
}

$rows | Format-Table -AutoSize | Out-String -Width 120 | Write-Output
$editor = "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $editor)) { Write-Output "Unity 6000.3.23f1 not at the default path. Install: unity install 6000.3.23f1 (or Unity Hub)." }
Write-Output 'Sign in yourself where needed: gh auth login, unity auth login.'
$missing = @($rows | Where-Object { $_.Required -and $_.State -in @('missing', 'FAILED') })
if ($missing.Count -gt 0) { Write-Output ("Required tools missing: " + (($missing | ForEach-Object Tool) -join ', ')); exit 2 }
exit 0
