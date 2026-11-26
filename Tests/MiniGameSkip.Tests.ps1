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
$work = Join-Path ([IO.Path]::GetTempPath()) ('aion-minigame-skip-' + [guid]::NewGuid().ToString('N'))
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
'<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $work 'NuGet.Config') -Encoding UTF8
@'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

class Check
{
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

    static MiniGameTheme SampleTheme() => new MiniGameTheme
    {
        Title = "테스트 미니게임",
        Place = "LectureHall",
        Note = "테스트",
        ImpactVariant = 0
    };

    static MiniGame TryConstruct(Type type, out string reason)
    {
        reason = null;
        var theme = SampleTheme();
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (ctors.Length == 0) { reason = "No public constructor"; return null; }
        var ctor = ctors.OrderBy(c => c.GetParameters().Length).First();
        var parameters = ctor.GetParameters();
        var args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var pt = parameters[i].ParameterType;
            if (pt == typeof(MiniGameTheme)) args[i] = theme;
            else if (pt == typeof(int)) args[i] = 0;
            else if (pt == typeof(bool)) args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : false;
            else { reason = "Unsupported parameter type: " + pt.Name; return null; }
        }
        try
        {
            return (MiniGame)ctor.Invoke(args);
        }
        catch (TargetInvocationException ex)
        {
            var inner = ex.InnerException ?? ex;
            reason = inner.GetType().Name + ": " + inner.Message;
            return null;
        }
        catch (Exception ex)
        {
            reason = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }

    static void Main()
    {
        // 1. GameSettings default must be false.
        Require(!GameSettings.MiniGameSkip, "GameSettings.MiniGameSkip default must be false");

        // 2. Discover and test all MiniGame subclasses.
        var explain = typeof(MiniGame).GetMethod("ExplainFailure", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(explain != null, "MiniGame.ExplainFailure method must exist");

        var types = typeof(MiniGame).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(MiniGame).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();
        Require(types.Length >= 21, "Expected at least 21 MiniGame subclasses, found " + types.Length);

        var constructed = new List<string>();
        var unconstructible = new List<string>();

        foreach (var type in types)
        {
            var game = TryConstruct(type, out string reason);
            if (game == null)
            {
                unconstructible.Add(type.Name + " (" + reason + ")");
                Console.WriteLine("[UNCONSTRUCTIBLE] " + type.Name + ": " + reason);
                continue;
            }
            constructed.Add(type.Name);
            Require(!game.Done, type.Name + ": must not start Done");
            explain.Invoke(game, new object[] { "테스트 오답 원인", "테스트 해결 방법" });
            Require(game.Reviewing, type.Name + ": ExplainFailure must set Reviewing=true");

            game.SkipAsCorrect();

            Require(game.Done, type.Name + ": SkipAsCorrect must set Done=true");
            Require(game.Delta == 2, type.Name + ": SkipAsCorrect must set Delta=2, got " + game.Delta);
            Require(!game.Reviewing, type.Name + ": SkipAsCorrect must clear Reviewing");
            Require(!string.IsNullOrWhiteSpace(game.Summary), type.Name + ": SkipAsCorrect must set Summary");
            Require(game.CountsForStat, type.Name + ": SkipAsCorrect must count for stat");
            var replies = game.Replies;
            Require(replies != null && replies.Length == 2 && replies[0].Length == 3 && replies[1].Length == 3,
                type.Name + ": Replies must have 2x3 rows for Delta=2");
            Require(!string.IsNullOrWhiteSpace(replies[0][2]) && !string.IsNullOrWhiteSpace(replies[1][2]),
                type.Name + ": Replies[p][2] must be non-empty");
        }
        Require(constructed.Count > 0, "Expected at least one constructible MiniGame subclass");

        // 3. Verify cleanup overrides for async / custom-stat games.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        // Lecture: pending AI tasks must be cleared and question window closed.
        var lecture = new Lecture(SampleTheme(), 0, false);
        typeof(Lecture).GetField("questionTask", flags).SetValue(lecture, Task.FromResult(new AiProfessor.Result()));
        typeof(Lecture).GetField("explanationTask", flags).SetValue(lecture, Task.FromResult(new AiProfessor.Result()));
        lecture.SkipAsCorrect();
        Require(typeof(Lecture).GetField("questionTask", flags).GetValue(lecture) == null, "Lecture.OnSkip must clear questionTask");
        Require(typeof(Lecture).GetField("explanationTask", flags).GetValue(lecture) == null, "Lecture.OnSkip must clear explanationTask");
        Require((bool)typeof(Lecture).GetField("questionWindowClosed", flags).GetValue(lecture), "Lecture.OnSkip must close questionWindowClosed");

        // CoffeeOrder: CountsForStat overrides to professorCorrect, which OnSkip must set true.
        var coffee = (CoffeeOrder)RuntimeHelpers.GetUninitializedObject(typeof(CoffeeOrder));
        coffee.SkipAsCorrect();
        Require(coffee.Done && coffee.Delta == 2 && !coffee.Reviewing && coffee.CountsForStat,
            "CoffeeOrder.SkipAsCorrect must set Done=true, Delta=2, Reviewing=false, CountsForStat=true");

        // BondDesigner: pending AI comment token must be cancelled and BondPuzzle ended.
        var bond = (BondDesigner)RuntimeHelpers.GetUninitializedObject(typeof(BondDesigner));
        var puzzle = new BondPuzzle(42);
        var token = puzzle.BeginRequest(BondStage.Bond);
        Require(token != null && puzzle.IsCommentPending(token), "BondPuzzle.BeginRequest should create pending token");
        typeof(BondDesigner).GetField("puzzle", flags).SetValue(bond, puzzle);
        typeof(BondDesigner).GetField("commentToken", flags).SetValue(bond, token);
        typeof(BondDesigner).GetField("commentTask", flags).SetValue(bond, Task.FromResult(new AiProfessor.Result()));
        bond.SkipAsCorrect();
        Require(bond.Done && bond.Delta == 2 && !bond.Reviewing, "BondDesigner.SkipAsCorrect must succeed");
        Require(!puzzle.IsCommentPending(token), "BondDesigner.OnSkip must cancel pending comment token");
        Require(puzzle.Stage == BondStage.Finished, "BondDesigner.OnSkip must finish BondPuzzle");
        Require(typeof(BondDesigner).GetField("commentTask", flags).GetValue(bond) == null, "BondDesigner.OnSkip must clear commentTask");

        // 4. Verify SignGameSession.SkipAsCorrect from Intro, Practice, and mid-Challenge.
        var labels = new SignLabels(new[] { "A", "B", "C", "_other" });
        var sIntro = new SignGameSession(labels, 8000, 17);
        sIntro.SkipAsCorrect();
        Require(sIntro.CurrentStage == SignGameSession.Stage.Result && sIntro.Answers.Count == 3 && sIntro.CorrectCount == 3,
            "SignGameSession.SkipAsCorrect from Intro");

        var sPractice = new SignGameSession(labels, 8000, 17);
        sPractice.BeginPractice();
        sPractice.SkipAsCorrect();
        Require(sPractice.CurrentStage == SignGameSession.Stage.Result && sPractice.Answers.Count == 3 && sPractice.CorrectCount == 3,
            "SignGameSession.SkipAsCorrect from Practice");

        var sMid = new SignGameSession(labels, 8000, 17);
        sMid.BeginPractice();
        for (int i = 0; i < 3; i++) { sMid.Update(i * 2, sMid.CurrentLabel); sMid.Update(i * 2 + 1, null); }
        Require(sMid.CurrentStage == SignGameSession.Stage.Challenge, "SignGameSession should reach Challenge");
        sMid.Update(100, sMid.CurrentLabel);
        Require(sMid.Answers.Count == 1 && sMid.CorrectCount == 1, "SignGameSession should record first answer");
        sMid.SkipAsCorrect();
        Require(sMid.CurrentStage == SignGameSession.Stage.Result && sMid.Answers.Count == 3 && sMid.CorrectCount == 3,
            "SignGameSession.SkipAsCorrect mid-Challenge");

        Console.WriteLine("PASS: GameSettings.MiniGameSkip=false, " + constructed.Count + "/" + types.Length +
            " MiniGame subclasses constructed & skipped, " + unconstructible.Count +
            " unconstructible without Unity runtime, async/stat cleanup & SignGameSession.SkipAsCorrect verified.");
    }
}
'@ | Set-Content (Join-Path $work 'Program.cs') -Encoding UTF8
& dotnet run --project (Join-Path $work 'Check.csproj') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "MiniGameSkip checks failed; diagnostics retained at $work" }
Remove-Item -LiteralPath $work -Recurse -Force
