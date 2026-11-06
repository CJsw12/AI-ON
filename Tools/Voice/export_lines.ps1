param([string]$Voice = 'ShimHanmoe', [string]$Out, [string]$UnityManagedPath)
# Writes every line spoken by the professor whose Professor.Voice is $Voice, one per row (UTF-8).
# Compiles the story scripts with the .NET SDK against the installed Unity Editor, like Tests/*.ps1.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $Out) { $Out = Join-Path $PSScriptRoot "$Voice.lines.txt" }
if (-not $UnityManagedPath) {
    $version = (Get-Content (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') -Encoding UTF8)[0].Split(' ')[1]
    $UnityManagedPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Data/Managed/UnityEngine"
}
if (-not (Test-Path (Join-Path $UnityManagedPath 'UnityEngine.CoreModule.dll'))) { throw 'Pass -UnityManagedPath with the Editor Managed/UnityEngine directory.' }
$sdk = & dotnet --version
$target = 'net' + ($sdk.Trim().Split('.')[0]) + '.0'
$work = Join-Path ([IO.Path]::GetTempPath()) ('aion-voice-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$source = [Security.SecurityElement]::Escape((Join-Path $repo 'Assets/Scripts/**/*.cs'))
$program = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'ExportLines.cs'))
$managed = [Security.SecurityElement]::Escape((Join-Path $UnityManagedPath '*.dll'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>$target</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><EnableNETAnalyzers>false</EnableNETAnalyzers><NoWarn>CS0162;CS0168;CS0219;CS0414;CS0649;CS8632</NoWarn></PropertyGroup>
  <ItemGroup>
    <Compile Include="$program"/>
    <Compile Include="$source"/>
    <Compile Remove="$repo\Assets\Scripts\GameController*.cs"/>
    <Compile Remove="$repo\Assets\Scripts\GameMenus.cs"/>
    <Compile Remove="$repo\Assets\Scripts\GameEffects.cs"/>
    <Compile Remove="$repo\Assets\Scripts\GameAiMode.cs"/>
    <Compile Remove="$repo\Assets\Scripts\VTubeStudioUsbApiBridge.cs"/>
    <Compile Remove="$repo\Assets\Scripts\IPhoneGazeUdpReceiver.cs"/>
    <Reference Include="$managed"/>
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $work 'Export.csproj') -Encoding UTF8
'<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $work 'NuGet.Config') -Encoding UTF8
$ErrorActionPreference = 'Continue'   # dotnet writes progress to stderr; exit codes decide.
try {
    & dotnet build (Join-Path $work 'Export.csproj') -nologo -v q -o (Join-Path $work 'bin') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & dotnet (Join-Path $work 'bin/Export.dll') $Voice $Out
    if ($LASTEXITCODE -ne 0) { throw 'Export failed.' }
} finally { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
