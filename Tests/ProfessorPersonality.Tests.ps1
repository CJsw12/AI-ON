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
$work = Join-Path ([IO.Path]::GetTempPath()) ('aion-personality-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$source = [Security.SecurityElement]::Escape((Join-Path $repo 'Assets/Scripts/**/*.cs'))
$managed = [Security.SecurityElement]::Escape((Join-Path $UnityManagedPath '*.dll'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>$target</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs"/>
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
"@ | Set-Content (Join-Path $work 'Check.csproj') -Encoding UTF8
# No packages or network sources: compile only against the installed SDK and Editor.
'<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $work 'NuGet.Config') -Encoding UTF8
@'
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
class Check {
    static int lines;
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Replies(string[][] rows, int count, string context) {
        Require(rows != null && rows.Length == 2, context + ": two professor reply rows required");
        foreach (var row in rows) Require(row != null && row.Length == count && row.All(x => !string.IsNullOrWhiteSpace(x)), context + ": incomplete reply row");
    }
    static void Main() {
        // Final manual-next buttons must complete each game with the same score thresholds.
        foreach (var type in new[] {typeof(ChordProgression),typeof(MixingBoard),typeof(BehaviorCoding)}) {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (int points in new[] {0,4,5,7,8,9}) {
                var game = (MiniGame)RuntimeHelpers.GetUninitializedObject(type);
                string progress = type == typeof(ChordProgression) ? "song" : type == typeof(MixingBoard) ? "round" : "item";
                type.GetField(progress,flags).SetValue(game,type == typeof(ChordProgression) ? 4 : type == typeof(MixingBoard) ? 2 : 7);
                if (type == typeof(ChordProgression)) type.GetField("blank",flags).SetValue(game,1);
                type.GetField(type == typeof(MixingBoard) ? "score" : "correct",flags).SetValue(game,points);
                type.GetMethod("Next",flags).Invoke(game,null);
                int high = type == typeof(MixingBoard) ? 8 : 7;
                int low = type == typeof(ChordProgression) ? 4 : 5;
                Require(game.Done && game.Delta == (points >= high ? 2 : points >= low ? 1 : 0), type.Name + ": manual final result");
            }
        }
        var key = ReadLog.ContentKey("0/0/1/0/0", "original");
        Require(key == ReadLog.ContentKey("0/0/1/0/0", "original"), "Read keys must be stable");
        Require(key != ReadLog.ContentKey("0/0/1/0/0", "rewritten"), "Rewritten dialogue must not inherit read status");
        Require(key != ReadLog.ContentKey("0/1/1/0/0", "original"), "Professors must keep separate read status");
        Require(key != "0/0/1/0/0" && ReadLog.ContentKey(null,"original") == null, "Legacy and unknown result keys must remain unread");
        for (int p = 0; p < 2; p++) {
            var speech = AiSwStory.Data.EndingLines[p][6];
            Require(AiSwStory.Data.Ending(p,6).Split('\n').Count(x => x == "교수님: " + speech) == 1, "Research ending must speak its reply only once");
        }
        Require(Stories.All.Length == 16, "Expected 16 faculties");
        Require(Stories.All.SelectMany(s => s.Professors).Select(p => p.Name).Distinct().Count() == 32, "Professor names must be unique");
        Require(Stories.All.SelectMany(s => s.Slots).Count(x => x.Game != null && x.Game.Kind == MiniGameKind.Lecture) == 1, "Only the computer-science pilot should expose a lecture minigame");
        foreach (var s in Stories.All) {
            Require(s.Professors.Length == 2 && s.Slots.Length == 12, s.Name + ": faculty shape");
            Require(s.Dialogue.Length == 7 && s.FollowUp.Length == 7, s.Name + ": chapter shape");
            Replies(s.EndingLines, 8, s.Name + " endings");
            Require(s.EndingNames.Length == 8 && s.EndingText.Length == 8, s.Name + ": eight endings");
            foreach (var tone in new[] {s.Fond, s.Wary}) Require(tone != null && tone.Length == 2 && tone.All(x => !string.IsNullOrWhiteSpace(x)), s.Name + ": custom affection tone");
            for (int p = 0; p < 2; p++) {
                var prof = s.Professors[p];
                Require(!string.IsNullOrWhiteSpace(prof.Description) && !string.IsNullOrWhiteSpace(prof.TagA) && !string.IsNullOrWhiteSpace(prof.TagB), s.Name + ": personality description");
                Require(Stories.Decorate(s,p,"REPLY",7) == "REPLY " + s.Fond[p], s.Name + ": fond routing");
                Require(Stories.Decorate(s,p,"REPLY",-1) == s.Wary[p] + " REPLY", s.Name + ": wary routing");
                Require(Stories.Decorate(s,p,"REPLY",0) == "REPLY", s.Name + ": neutral routing");
                var choices = s.Slots.Where(x => x.Options != null && x.Options.Length > 0).ToArray();
                Require(choices.Length == 6, s.Name + ": six choices");
                int min = 0, max = 0;
                foreach (var x in choices) {
                    Require(x.Scores.Length == 2 && x.Scores[p].Length == x.Options.Length && x.Stats.Length == x.Options.Length, s.Name + ": choice shape");
                    Replies(x.Replies, x.Options.Length, s.Name + " choices");
                    min += x.Scores[p].Min(); max += x.Scores[p].Max();
                }
                Require(min <= 0 && max >= 8, prof.Name + ": choice affection range " + min + ".." + max);
                for (int ending = 0; ending < 8; ending++) Require(!s.Ending(p,ending).Contains('|'), s.Name + ": unresolved ending tone");
                for (int c = 1; c <= 6; c++) {
                    if (s.ExperiencedDialogue != null) Require(s.ExperiencedDialogue[c].Length == s.Dialogue[c].Length, s.Name + ": experienced dialogue shape");
                    if (s.ExperiencedFollowUp != null) Require(s.ExperiencedFollowUp[c].Split('\n').Length == s.FollowUp[c].Split('\n').Length, s.Name + ": experienced followup shape");
                    foreach (bool experienced in new[] {false,true}) {
                        for (int i = 0; i < s.Lines(c,0); i++) { s.Line(p,c,i,out var speaker,out var text,experienced); Require(!string.IsNullOrWhiteSpace(text) && !text.Contains('|'),s.Name + ": dialogue tone"); lines++; }
                        for (int i = 0; i < s.Lines(c,1); i++) { var text=s.FollowUpLine(p,c,i,experienced); Require(!string.IsNullOrWhiteSpace(text) && !text.Contains('|'),s.Name + ": followup tone"); lines++; }
                    }
                }
            }
            int expectedGames = s.Name == "컴퓨터공학과" ? 4 : 3;
            int expectedQuizzes = s.Name == "컴퓨터공학과" ? 2 : 3;
            Require(s.Slots.Count(x => x.Game != null) == expectedGames && s.Slots.Count(x => x.Major != null && x.Minor != null) == expectedQuizzes, s.Name + ": game/quiz slots");
            if (s.Name == "컴퓨터공학과") Require(s.Slots[6].Game.Kind == MiniGameKind.Lecture && s.Slots[6].Stat == 'i' && s.Slots[5].Game != null && s.Slots[7].Game == null, "Computer Science slot 6 must be the interest-counting lecture game between, not beside, existing minigames");
            foreach (var x in s.Slots) {
                if (x.Game != null) Replies(x.Game.Replies,3,s.Name + " game outcomes");
                foreach (var q in new[] {x.Major,x.Minor}) if (q != null) {
                    Require(q.Accepted != null || q.Options != null, s.Name + ": quiz answers");
                    Replies(q.Replies,q.Options == null ? 2 : q.Options.Length,s.Name + " quiz outcomes");
                }
            }
        }
        Console.WriteLine("PASS: 16 faculties, 32 personalities, " + lines + " dialogue calls, reply shapes and affection ranges.");
    }
}
'@ | Set-Content (Join-Path $work 'Program.cs') -Encoding UTF8
& dotnet run --project (Join-Path $work 'Check.csproj') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Personality checks failed; diagnostics retained at $work" }
Write-Output "Validation project retained at $work"
