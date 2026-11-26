<#
.SYNOPSIS
  Per-PC git setup for AI-ON (run once on every PC, from any directory).

.DESCRIPTION
  - Installs Tools/git-hooks/pre-commit and pre-push into this checkout's hooks directory.
    pre-push replaces the stock Git LFS hook and still runs 'git lfs pre-push'.
  - Stores this clone's agent id (git config aion.agentId) used by Tools/agent-sync.sh.
  - Saves -EditorPath (git config aion.unityEditor) for the pre-push compile and test checks.
  - Registers Unity Smart Merge as the manual 'unityyamlmerge' mergetool for scene/prefab
    conflicts: git mergetool --tool=unityyamlmerge <file>.
  - Reports whether the paid Modern UI Pack (Assets/Resources/UI, not in git) is present.
  - Sets pull.rebase=true and rebase.autoStash=true locally for the main-only workflow.
  Only local (.git/config) settings are changed; global git config is not touched.
  Exit code 0 = configured, 2 = a required tool was not found.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-git-unity.ps1 -AgentId pc1-claude-opus55
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-git-unity.ps1 -AgentId pc1-codex-sol -EditorPath "D:\Unity\6000.3.23f1\Editor\Unity.exe"
#>
param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')] [string] $AgentId,
    [string] $EditorPath
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Output 'git not found. Install: winget install --id Git.Git -e'
    exit 2
}

# 1. git hooks (pre-commit, pre-push)
# A core.hooksPath (often global) can point at a folder shared by every repository; never overwrite hooks
# there. Give this repository its own hooks folder instead (local setting, this clone only).
$configuredHooks = (& git config --get core.hooksPath) 2>$null
if (-not [string]::IsNullOrEmpty($configuredHooks)) {
    $commonDir = (& git rev-parse --git-common-dir).Trim()
    if (-not [System.IO.Path]::IsPathRooted($commonDir)) { $commonDir = Join-Path $repo $commonDir }
    $ownHooks = ([System.IO.Path]::GetFullPath((Join-Path $commonDir 'hooks'))) -replace '\\', '/'
    if ($configuredHooks -ne $ownHooks) {
        & git config --local core.hooksPath $ownHooks
        Write-Output "core.hooksPath was '$configuredHooks'; this repository now uses its own hooks folder: $ownHooks"
    }
}
$hooksDir = (& git rev-parse --git-path hooks).Trim()
if (-not [System.IO.Path]::IsPathRooted($hooksDir)) { $hooksDir = Join-Path $repo $hooksDir }
if (-not [string]::IsNullOrEmpty($configuredHooks)) {
    # A worktree- or command-level hooksPath can still win over the local setting; never write to a shared folder.
    $effective = ([System.IO.Path]::GetFullPath($hooksDir)) -replace '\\', '/'
    if ($effective -ne $ownHooks) {
        Write-Output "Hooks NOT installed: git still uses '$effective' (a worktree or command-level core.hooksPath overrides this repository). Remove that setting and rerun."
        exit 2
    }
}
[System.IO.Directory]::CreateDirectory($hooksDir) | Out-Null
foreach ($hookName in 'pre-commit', 'pre-push') {
    $hookTarget = Join-Path $hooksDir $hookName
    $hookSource = Join-Path $repo "Tools/git-hooks/$hookName"
    if ((Test-Path -LiteralPath $hookTarget) -and
        -not ((Get-Content -Raw -LiteralPath $hookTarget) -match "AI-ON $hookName guard")) {
        $backup = "$hookTarget.backup-$(Get-Date -Format 'yyyyMMddHHmmss')"
        Copy-Item -LiteralPath $hookTarget -Destination $backup
        Write-Output "Existing $hookName hook backed up to $backup"
    }
    # Write with LF line endings so Git for Windows' sh can run it.
    $hookText = (Get-Content -Raw -LiteralPath $hookSource) -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($hookTarget, $hookText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output "$hookName hook installed: $hookTarget"
}

# 1b. agent id for Tools/agent-sync.sh
if (-not [string]::IsNullOrEmpty($AgentId)) {
    & git config --local aion.agentId $AgentId
    Write-Output "Agent id: $AgentId (git config aion.agentId)"
} else {
    $current = (& git config --local --get aion.agentId) 2>$null
    if ([string]::IsNullOrEmpty($current)) {
        Write-Output 'Agent id NOT set. Rerun with -AgentId <pc>-<tool>-<model>, e.g. pc1-claude-opus55'
    } else {
        Write-Output "Agent id: $current (unchanged)"
    }
}

# 2. Unity Editor location (used by the pre-push compile/test gates) and Smart Merge
if (-not [string]::IsNullOrEmpty($EditorPath)) {
    & git config --local aion.unityEditor $EditorPath
    Write-Output "Unity Editor: $EditorPath (git config aion.unityEditor)"
} else {
    $versionLine = Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') |
        Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
    $version = ($versionLine -split ':', 2)[1].Trim()
    $EditorPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
$yamlMerge = Join-Path (Split-Path -Parent $EditorPath) 'Data\Tools\UnityYAMLMerge.exe'
$status = 0
if (Test-Path -LiteralPath $yamlMerge) {
    $mergePath = $yamlMerge -replace '\\', '/'
    $mergeCmd = '"' + $mergePath + '" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
    # Windows PowerShell 5.1 (and 7.0-7.2) drop embedded quotes when calling native programs.
    if ($PSVersionTable.PSVersion -lt [version]'7.3') { $mergeCmd = $mergeCmd -replace '"', '\"' }
    & git config --local mergetool.unityyamlmerge.cmd $mergeCmd
    & git config --local mergetool.unityyamlmerge.trustExitCode false
    # AI-ON's .gitattributes has no merge=unityyamlmerge rules, so Smart Merge is a manual tool only.
    Write-Output "Smart Merge registered (manual): git mergetool --tool=unityyamlmerge <scene or prefab>"
} else {
    Write-Output "UnityYAMLMerge not found at $yamlMerge. Install Editor via Unity Hub or pass -EditorPath."
    $status = 2
}

# 3. main-only workflow defaults
& git config --local pull.rebase true
& git config --local rebase.autoStash true
Write-Output 'Local git: pull.rebase=true, rebase.autoStash=true'

# 4. Paid Modern UI Pack: kept out of the public repo, shared privately among the team.
$uiPack = Join-Path $repo 'Assets/Resources/UI'
if ((Test-Path -LiteralPath $uiPack) -and (Get-ChildItem -LiteralPath $uiPack -Filter *.png -ErrorAction SilentlyContinue)) {
    Write-Output 'UI pack  ok (Assets/Resources/UI)'
} else {
    Write-Output 'UI pack  missing: copy the privately shared files into Assets/Resources/UI (the game still runs with simple fallbacks)'
}

# 5. Tool presence report (informational)
foreach ($tool in 'git-lfs', 'gh', 'pwsh', 'node') {
    $found = [bool](Get-Command $tool -ErrorAction SilentlyContinue)
    Write-Output ("{0,-8} {1}" -f $tool, $(if ($found) { 'ok' } else { 'missing (see AGENTS.md section 5)' }))
}
if (Get-Command gh -ErrorAction SilentlyContinue) {
    $ErrorActionPreference = 'Continue'
    & cmd /c 'gh auth status >nul 2>&1'
    if ($LASTEXITCODE -ne 0) { Write-Output 'gh       not logged in: run gh auth login' }
}
exit $status
