<#
.SYNOPSIS
  Code-mode wrappers around the official Unity CLI (`unity command ...`) for agents.

.DESCRIPTION
  Agents import this module in a short script, chain calls, and print only the summary they need,
  instead of loading ~190 Unity MCP tool schemas into context. Works in Windows PowerShell 5.1 and 7.
  Requires: the `unity` CLI on PATH and an open Editor with the com.unity.pipeline package (AGENTS.md section 7).

    Import-Module ./Tools/codemode/Unity.psm1
    Wait-UnityReady
    $r = Invoke-UnityRecompile; if ($r.failed) { $r.errors | Select-Object -First 5 }

  Discover: Get-Command -Module Unity; Get-Help <name>. Any other Editor command: Invoke-UnityCommand
  ('unity command' lists them all; 'unity command <group> <name> --help' shows its parameters).
#>
Set-StrictMode -Version 2
$script:ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Invoke-UnityCommand {
    <#
    .SYNOPSIS Run one Editor command and return its result object (parsed JSON).
    .EXAMPLE Invoke-UnityCommand 'console get' @{ level = 'error'; tail = 20 }
    .EXAMPLE Invoke-UnityCommand 'editor.playmode start'
    #>
    param(
        [Parameter(Mandatory)] [string] $Command,
        [hashtable] $Params = @{},
        [int] $TimeoutSec = 0
    )
    if (-not (Get-Command unity -ErrorAction SilentlyContinue)) {
        throw 'unity CLI not found. Run Tools/setup-dev.ps1 (AGENTS.md section 7).'
    }
    $env:UNITY_NO_BANNER = '1'; $env:UNITY_NO_PAGER = '1'; $env:UNITY_NON_INTERACTIVE = '1'
    $argv = @('command') + ($Command -split '\s+') + @('--project-path', $script:ProjectRoot, '--result-only', '--format', 'json')
    if ($TimeoutSec -gt 0) { $argv += @('--timeout', "$TimeoutSec") }
    foreach ($key in $Params.Keys) {
        $value = $Params[$key]
        if ($value -is [bool]) { $value = $value.ToString().ToLowerInvariant() }
        $argv += @("--$key", "$value")
    }
    $out = & unity @argv 2>&1
    $code = $LASTEXITCODE
    $text = (($out | ForEach-Object { "$_" }) -join "`n").Trim()
    if ($code -ne 0) {
        if ($text.Length -gt 2000) { $text = $text.Substring(0, 2000) + ' ...' }
        throw "unity command $Command failed (exit $code): $text"
    }
    if ([string]::IsNullOrEmpty($text)) { return $null }
    try { return $text | ConvertFrom-Json } catch { return $text }
}

function Get-UnityEditorState {
    <# .SYNOPSIS Editor status: status, compiling, domainReloadInProgress, playMode. #>
    Invoke-UnityCommand 'editor status'
}

function Wait-UnityReady {
    <# .SYNOPSIS Block until the Editor is connected, idle (not compiling or reloading). #>
    param([int] $TimeoutSec = 120)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        try {
            $s = Get-UnityEditorState
            if ($s -and $s.status -in @('ready', 'playing') -and -not $s.compiling -and -not $s.domainReloadInProgress) { return $s }
        } catch { }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Unity Editor not ready within $TimeoutSec s (is the project open in Unity 6000.3.23f1?)."
}

function Invoke-UnityRecompile {
    <# .SYNOPSIS Recompile scripts and wait. Returns { failed, errors[] }. #>
    param([int] $TimeoutSec = 300)
    Wait-UnityReady -TimeoutSec $TimeoutSec | Out-Null
    Invoke-UnityCommand 'script recompile' | Out-Null
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        Start-Sleep -Milliseconds 700
        try { $r = Invoke-UnityCommand 'script recompile_status' } catch { $r = $null }   # domain reload drops the link briefly
        if ($r -and $r.status -in @('completed', 'up_to_date')) {
            Wait-UnityReady -TimeoutSec 120 | Out-Null
            return [pscustomobject]@{ failed = [bool]$r.failed; errors = @($r.errors) }
        }
    } while ((Get-Date) -lt $deadline)
    throw "Recompile did not finish within $TimeoutSec s."
}

function Get-UnityConsole {
    <# .SYNOPSIS Recent console entries at or above a level (default: errors only). #>
    param([ValidateSet('log', 'warn', 'error')] [string] $Level = 'error', [int] $Tail = 50)
    $r = Invoke-UnityCommand 'console get' @{ level = $Level; tail = $Tail }
    if ($r -and $r.PSObject.Properties['entries']) { return @($r.entries) }
    return $r
}

function Get-UnityCompileState {
    <# .SYNOPSIS One-line truth: compiling, compilationFailed, console error/warning counts. #>
    $g = (Invoke-UnityCommand 'console status').groundTruth
    [pscustomobject]@{ compiling = $g.compiling; failed = $g.compilationFailed; errors = $g.consoleErrors; warnings = $g.consoleWarnings }
}

function Invoke-UnityTests {
    <# .SYNOPSIS Run Unity Test Framework tests (the project's main regression suite is Tests/*.ps1, AGENTS.md section 4). #>
    param([ValidateSet('all', 'editor', 'playmode')] [string] $Mode = 'editor', [string] $Filter = '', [int] $TimeoutSec = 600)
    Invoke-UnityCommand 'test run' @{ mode = $Mode; filter = $Filter; timeout = $TimeoutSec } -TimeoutSec ($TimeoutSec + 30)
}

function Invoke-UnityEval {
    <#
    .SYNOPSIS Evaluate C# in the Editor (Roslyn script). Code is written to Temp/codemode to avoid shell quoting.
    .EXAMPLE Invoke-UnityEval 'return UnityEditor.EditorBuildSettings.scenes.Length;'
    #>
    param([string] $Code, [string] $File, [int] $TimeoutSec = 30, [switch] $DryRun)
    if (-not $File) {
        if (-not $Code) { throw 'Give -Code or -File.' }
        $dir = Join-Path $script:ProjectRoot 'Temp/codemode'
        [System.IO.Directory]::CreateDirectory($dir) | Out-Null
        $File = Join-Path $dir ("eval-{0}.cs" -f [guid]::NewGuid().ToString('N').Substring(0, 8))
        [System.IO.File]::WriteAllText($File, $Code, (New-Object System.Text.UTF8Encoding($false)))
    }
    Invoke-UnityCommand 'script eval' @{ file = $File; timeout = $TimeoutSec; 'dry-run' = [bool]$DryRun; 'auto-import' = $true } -TimeoutSec ($TimeoutSec + 15)
}

function Start-UnityPlayMode { Invoke-UnityCommand 'editor.playmode start' | Out-Null; Wait-UnityReady | Out-Null }
function Stop-UnityPlayMode { Invoke-UnityCommand 'editor.playmode stop' | Out-Null; Wait-UnityReady | Out-Null }

function Save-UnityGameView {
    <# .SYNOPSIS Save the Game view as PNG (default Logs/codemode/game.png, outside Assets so no .meta). Returns the path. #>
    param([string] $Path = 'Logs/codemode/game.png', [int] $Width = 1280, [int] $Height = 900)
    $full = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $script:ProjectRoot $Path }
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $full)) | Out-Null
    Invoke-UnityCommand 'capture game_view' @{ 'output-path' = $full; width = $Width; height = $Height } | Out-Null
    return $full
}

function Sync-UnityProjectFiles {
    <# .SYNOPSIS Regenerate AI-ON.sln / *.csproj (git-ignored) so C# language servers (Serena, csharp-ls) can load the project. Needs com.unity.ide.visualstudio. #>
    Invoke-UnityEval 'Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll(); return System.IO.Directory.GetFiles(".", "*.csproj").Length;'
}

Export-ModuleMember -Function Invoke-UnityCommand, Get-UnityEditorState, Wait-UnityReady, Invoke-UnityRecompile,
    Get-UnityConsole, Get-UnityCompileState, Invoke-UnityTests, Invoke-UnityEval, Start-UnityPlayMode,
    Stop-UnityPlayMode, Save-UnityGameView, Sync-UnityProjectFiles
