$ErrorActionPreference = 'Stop'
$handSource = Get-Content (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Assets\Scripts\Sign\HandLandmarkSource.cs') -Raw -Encoding UTF8
foreach ($state in @('Starting','Running','NoModel','NoCamera','PermissionDenied','Failed','UnsupportedRotation')) { if ($handSource -notmatch "HandSourceState\.$state") { throw "Missing hand source state: $state" } }
$source = [Security.SecurityElement]::Escape((Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Assets\Scripts\Sign\FrameTimingStats.cs'))
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0) { throw 'An installed .NET SDK is required.' }
$target = 'net' + $sdk.Trim().Split('.')[0] + '.0'
$work = Join-Path ([IO.Path]::GetTempPath()) ('aion-hand-probe-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>' + $target + '</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Program.cs"/><Compile Include="' + $source + '"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $work 'Check.csproj'), $project)
[IO.File]::WriteAllText((Join-Path $work 'NuGet.Config'), '<configuration><packageSources><clear /></packageSources></configuration>')
$program = @'
using System;
class Check {
 static void Require(bool value) { if (!value) throw new Exception("Timing stats failed"); }
 static void Main() {
  var s = new FrameTimingStats(3);
  Require(s.Count == 0 && s.FrameCount == 0 && s.Average == 0 && s.Maximum == 0 && s.Median == 0);
  s.Add(1); s.Add(5); Require(s.Average == 3 && s.Maximum == 5 && s.Median == 3);
  s.Add(3); Require(s.Average == 3 && s.Maximum == 5 && s.Median == 3);
  s.Add(7); Require(s.Count == 3 && s.FrameCount == 4 && s.Average == 5 && s.Maximum == 7 && s.Median == 5);
  Console.WriteLine("PASS: empty, average, maximum, median, eviction");
 }
}
'@
[IO.File]::WriteAllText((Join-Path $work 'Program.cs'), $program)
& dotnet run --project (Join-Path $work 'Check.csproj') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Hand probe checks failed at $work" }
