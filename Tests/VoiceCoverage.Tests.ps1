param([string]$UnityManagedPath)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $UnityManagedPath) {
    $version = (Get-Content (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') -Encoding UTF8)[0].Split(' ')[1]
    $UnityManagedPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Data/Managed/UnityEngine"
}
if (-not (Test-Path (Join-Path $UnityManagedPath 'UnityEngine.CoreModule.dll'))) {
    throw 'Pass -UnityManagedPath with the installed Editor Managed/UnityEngine directory.'
}
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0) { throw 'An installed .NET SDK is required.' }
$target = 'net' + ($sdk.Trim().Split('.')[0]) + '.0'
$work = Join-Path ([IO.Path]::GetTempPath()) ('aion-voice-coverage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$repoEsc = [Security.SecurityElement]::Escape($repo)
$source = [Security.SecurityElement]::Escape((Join-Path $repo 'Assets/Scripts/**/*.cs'))
$exportLines = [Security.SecurityElement]::Escape((Join-Path $repo 'Tools/Voice/ExportLines.cs'))
$managed = [Security.SecurityElement]::Escape((Join-Path $UnityManagedPath '*.dll'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$target</TargetFramework>
    <StartupObject>VoiceCoverage</StartupObject>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
    <NoWarn>CS0162;CS0168;CS0219;CS0414;CS0649;CS8632</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs"/>
    <Compile Include="$exportLines"/>
    <Compile Include="$source"/>
    <Compile Remove="$repoEsc\Assets\Scripts\GameController*.cs"/>
    <Compile Remove="$repoEsc\Assets\Scripts\GameMenus.cs"/>
    <Compile Remove="$repoEsc\Assets\Scripts\GameEffects.cs"/>
    <Compile Remove="$repoEsc\Assets\Scripts\GameAiMode.cs"/>
    <Compile Remove="$repoEsc\Assets\Scripts\VTubeStudioUsbApiBridge.cs"/>
    <Compile Remove="$repoEsc\Assets\Scripts\IPhoneGazeUdpReceiver.cs"/>
    <Reference Include="$managed"/>
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $work 'VoiceCoverage.csproj') -Encoding UTF8
# No packages or network sources: compile only against the installed SDK and Editor.
'<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $work 'NuGet.Config') -Encoding UTF8
@'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

static class VoiceCoverage
{
    // 알려진 누락 허용 목록: 녹음을 요청해 둔 대사만 (Voice, 원문)으로 넣는다. 비어 있으면 모든 대사가 녹음과 연결돼야 한다.
    // 2026-11-27 심한뫼 강의 대사 13개는 797b356에서 해결되어 비웠다.
    static readonly (string Voice, string Line)[] KnownMissing = new (string Voice, string Line)[0];

    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("FAIL: Assets/Resources/Voice 경로 인자가 필요합니다.");
            return 1;
        }
        string root = args[0];
        var split = typeof(ProfessorVoice).GetMethod("Split", BindingFlags.NonPublic | BindingFlags.Static);
        if (split == null)
        {
            Console.WriteLine("FAIL: ProfessorVoice.Split 메서드를 찾을 수 없습니다.");
            return 1;
        }

        var allowlist = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in KnownMissing)
        {
            allowlist.Add(AllowKey(item.Voice, ProfessorVoice.Normalize(item.Line)));
        }

        var failures = new List<string>();
        var allowedWarnings = new List<string>();
        var resolvedAllowed = new List<string>();
        var perProfessor = new List<string>();
        var checkedVoiceDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var voiceIndexes = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var voicesNeedingFix = new SortedSet<string>(StringComparer.Ordinal);

        int totalLines = 0;
        int totalConnected = 0;
        int professorCount = 0;

        for (int f = 0; f < Stories.All.Length; f++)
        {
            var story = Stories.All[f];
            for (int p = 0; p < story.Professors.Length; p++)
            {
                professorCount++;
                var prof = story.Professors[p];
                string voice = prof.Voice;

                // 1. 모든 교수에 Voice 설정 확인
                if (string.IsNullOrWhiteSpace(voice))
                {
                    failures.Add($"[Voice 미설정] {story.Name} / {prof.Name}");
                    perProfessor.Add($"{story.Name} / {prof.Name} (없음): 0/0");
                    continue;
                }

                // 2~4. Voice 폴더, lines.txt(.meta), .ogg(.meta) & OggS 헤더, 고아 .ogg 검사 (폴더당 1회)
                if (checkedVoiceDirs.Add(voice))
                {
                    var index = ValidateVoiceDirectory(root, voice, failures, voicesNeedingFix);
                    if (index != null) voiceIndexes[voice] = index;
                }

                // 5. 교수별 실제 대사(ExportLines 규칙) 매칭 검사
                var raw = new List<string>();
                ExportLines.Collect(story, p, raw);
                var lines = raw.Select(ProfessorVoice.Normalize).Where(t => t.Length > 0).Distinct().ToList();
                totalLines += lines.Count;

                if (!voiceIndexes.TryGetValue(voice, out var voiceIndex))
                {
                    perProfessor.Add($"{story.Name} / {prof.Name} ({voice}): 0/{lines.Count} (인덱스 없음)");
                    continue;
                }

                int connected = 0;
                int profAllowed = 0;
                foreach (var line in lines)
                {
                    bool matched = Matches(split, voiceIndex, line);
                    string key = AllowKey(voice, line);
                    if (matched)
                    {
                        connected++;
                        if (allowlist.Contains(key))
                        {
                            resolvedAllowed.Add($"{voice} | {line}");
                        }
                    }
                    else if (allowlist.Contains(key))
                    {
                        profAllowed++;
                        allowedWarnings.Add($"{voice} | {line}");
                    }
                    else
                    {
                        failures.Add($"[미연결 대사] {voice} ({story.Name} / {prof.Name}) | {line}");
                        voicesNeedingFix.Add(voice);
                    }
                }

                totalConnected += connected;
                string suffix = profAllowed > 0 ? $" (허용 누락 {profAllowed}건)" : "";
                perProfessor.Add($"{story.Name} / {prof.Name} ({voice}): {connected}/{lines.Count}{suffix}");
            }
        }

        Console.WriteLine($"=== 교수 음성 연결 요약: {totalConnected}/{totalLines} 대사 연결 ({professorCount}명 교수) ===");
        foreach (var row in perProfessor)
        {
            Console.WriteLine("  " + row);
        }

        if (allowedWarnings.Count > 0)
        {
            Console.WriteLine($"[WARN] 알려진 누락 허용 목록 ({allowedWarnings.Count}건):");
            foreach (var w in allowedWarnings)
            {
                Console.WriteLine("  [WARN] " + w);
            }
        }

        if (resolvedAllowed.Count > 0)
        {
            Console.WriteLine($"[INFO] 해결됨 — 허용 목록에서 지워도 됨 ({resolvedAllowed.Count}건):");
            foreach (var r in resolvedAllowed)
            {
                Console.WriteLine("  [INFO] 해결됨 — 허용 목록에서 지워도 됨: " + r);
            }
        }

        if (failures.Count > 0)
        {
            Console.WriteLine($"FAIL: 교수 음성 검사 실패 ({failures.Count}건):");
            foreach (var err in failures)
            {
                Console.WriteLine("  - " + err);
            }
            if (voicesNeedingFix.Count > 0)
            {
                foreach (var v in voicesNeedingFix)
                {
                    Console.WriteLine($"고치는 법: .\\Tools\\Voice\\export_lines.ps1 -Voice {v} → generate.py (AGENTS.md \"교수 목소리\")");
                }
            }
            else
            {
                Console.WriteLine("고치는 법: .\\Tools\\Voice\\export_lines.ps1 -Voice <Voice> → generate.py (AGENTS.md \"교수 목소리\")");
            }
            return 1;
        }

        Console.WriteLine($"PASS: 교수 {professorCount}명, 전체 {totalConnected}/{totalLines} 대사 연결 확인 완료 (허용 누락 경고 {allowedWarnings.Count}건).");
        return 0;
    }

    static string AllowKey(string voice, string normalizedLine) => voice + "\0" + normalizedLine;

    static Dictionary<string, string> ValidateVoiceDirectory(
        string root,
        string voice,
        List<string> failures,
        SortedSet<string> voicesNeedingFix)
    {
        string dir = Path.Combine(root, voice);
        if (!Directory.Exists(dir))
        {
            failures.Add($"[폴더 없음] {voice}: {dir}");
            voicesNeedingFix.Add(voice);
            return null;
        }

        string linesPath = Path.Combine(dir, "lines.txt");
        string linesMetaPath = Path.Combine(dir, "lines.txt.meta");
        if (!File.Exists(linesMetaPath))
        {
            failures.Add($"[메타 없음] {voice}: lines.txt.meta 없음");
            voicesNeedingFix.Add(voice);
        }
        if (!File.Exists(linesPath))
        {
            failures.Add($"[파일 없음] {voice}: lines.txt 없음");
            voicesNeedingFix.Add(voice);
            return null;
        }

        var index = new Dictionary<string, string>();
        var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderedFiles = new List<string>();
        string content = File.ReadAllText(linesPath, Encoding.UTF8);
        string[] rows = content.Split('\n');
        for (int i = 0; i < rows.Length; i++)
        {
            string row = rows[i];
            if (string.IsNullOrWhiteSpace(row)) continue;
            int tab = row.IndexOf('\t');
            if (tab <= 0)
            {
                failures.Add($"[lines.txt 형식 오류] {voice} 행 {i + 1}: {row.Trim()}");
                voicesNeedingFix.Add(voice);
                continue;
            }
            string file = row.Substring(0, tab).Trim();
            if (string.IsNullOrEmpty(file))
            {
                failures.Add($"[lines.txt 파일명 누락] {voice} 행 {i + 1}");
                voicesNeedingFix.Add(voice);
                continue;
            }
            string normalized = ProfessorVoice.Normalize(row.Substring(tab + 1));
            index[normalized] = file;
            if (referencedFiles.Add(file))
            {
                orderedFiles.Add(file);
            }
        }

        foreach (var file in orderedFiles)
        {
            string oggPath = Path.Combine(dir, file + ".ogg");
            string oggMetaPath = Path.Combine(dir, file + ".ogg.meta");
            if (!File.Exists(oggPath))
            {
                failures.Add($"[OGG 없음] {voice}: {file}.ogg 없음");
                voicesNeedingFix.Add(voice);
            }
            else if (!HasOggHeader(oggPath))
            {
                failures.Add($"[OGG 헤더 이상] {voice}: {file}.ogg 앞 4바이트가 OggS가 아님");
                voicesNeedingFix.Add(voice);
            }

            if (!File.Exists(oggMetaPath))
            {
                failures.Add($"[메타 없음] {voice}: {file}.ogg.meta 없음");
                voicesNeedingFix.Add(voice);
            }
        }

        foreach (var oggFile in Directory.GetFiles(dir, "*.ogg"))
        {
            string stem = Path.GetFileNameWithoutExtension(oggFile);
            if (!referencedFiles.Contains(stem))
            {
                failures.Add($"[고아 OGG] {voice}: {Path.GetFileName(oggFile)} (lines.txt에 없음)");
                voicesNeedingFix.Add(voice);
            }
        }

        return index;
    }

    static bool HasOggHeader(string oggPath)
    {
        var head = new byte[4];
        using (var fs = File.OpenRead(oggPath))
        {
            int read = fs.Read(head, 0, 4);
            return read == 4 && head[0] == (byte)'O' && head[1] == (byte)'g' && head[2] == (byte)'g' && head[3] == (byte)'S';
        }
    }

    static bool Matches(MethodInfo split, Dictionary<string, string> index, string normalizedLine)
    {
        foreach (var row in normalizedLine.Split('\n'))
        {
            var files = new List<string>();
            if (!(bool)split.Invoke(null, new object[] { index, ProfessorVoice.Normalize(row), files }))
            {
                return false;
            }
        }
        return true;
    }
}
'@ | Set-Content (Join-Path $work 'Program.cs') -Encoding UTF8
$ErrorActionPreference = 'Continue'   # dotnet writes progress to stderr; exit codes decide.
try {
    & dotnet build (Join-Path $work 'VoiceCoverage.csproj') -nologo -v q -o (Join-Path $work 'bin') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'VoiceCoverage build failed.' }
    & dotnet (Join-Path $work 'bin/VoiceCoverage.dll') (Join-Path $repo 'Assets/Resources/Voice')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
