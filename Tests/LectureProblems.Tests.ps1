$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$lecturePath = Join-Path $repo 'Assets\Scripts\LectureProblems.cs'
if (-not (Test-Path -LiteralPath $lecturePath)) { throw 'LectureProblems.cs is not defined yet.' }
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0) { throw 'An installed .NET SDK is required.' }
$target = 'net' + $sdk.Trim().Split('.')[0] + '.0'
$work = Join-Path ([IO.Path]::GetTempPath()) ('aion-lecture-problems-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$source = [Security.SecurityElement]::Escape($lecturePath)
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>$target</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup>
  <ItemGroup><Compile Include="Program.cs"/><Compile Include="$source"/></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $work 'Check.csproj') -Encoding UTF8
'<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $work 'NuGet.Config') -Encoding UTF8
@'
using System;
using System.Collections.Generic;
using System.Linq;

static class Check
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static int BinarySearchComparisons(IList<int> values, int target)
    {
        int lo = 0, hi = values.Count - 1, count = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            count++;
            if (values[mid] == target) return count;
            if (values[mid] < target) lo = mid + 1;
            else hi = mid - 1;
        }
        throw new Exception("Independent binary search did not find target.");
    }

    static string Bits(int value) { return Convert.ToString(value, 2); }
    static int FromBits(string bits) { return Convert.ToInt32(bits, 2); }

    static string ValidQuestion(LectureProblem problem)
    {
        return "문제 입력: [" + string.Join(", ", problem.RequiredInputs) + "]에서 주어진 조건을 계산하세요.";
    }

    static void CheckAiValidation()
    {
        var problem = LectureProblems.Generate(LectureTopic.Algorithms, false, new Random(613));
        string question = ValidQuestion(problem);
        string answer = problem.CorrectInput;
        string response = "문제: " + question + "\n풀이: 정답은 " + answer + "입니다.";
        Require(LectureProblems.TryParseAiResponse(problem, response, out string parsedQuestion, out string parsedExplanation), "Valid labeled AI response should pass.");
        Require(parsedQuestion == question && parsedExplanation.Contains(answer), "AI parser should return both cleaned sections.");

        string missingNumbers = "문제: 입력 조건만 확인하세요.\n풀이: 정답은 " + answer + "입니다.";
        Require(!LectureProblems.TryParseAiResponse(problem, missingNumbers, out _, out _), "AI question missing generated input values should fail.");
        Require(!LectureProblems.TryParseAiResponse(problem, "Question: " + question + "\nSolution: 정답 " + answer, out _, out _), "Missing Korean labels should fail.");
        string tooLong = "문제: " + question + new string('가', 141) + "\n풀이: 정답은 " + answer;
        Require(!LectureProblems.TryParseAiResponse(problem, tooLong, out _, out _), "Question over the character limit should fail.");
        string longExplanation = "문제: " + question + "\n풀이: 정답은 " + answer + new string('가', 161);
        Require(!LectureProblems.TryParseAiResponse(problem, longExplanation, out _, out _), "Explanation over the character limit should fail.");
        Require(!LectureProblems.TryParseAiResponse(problem, "문제: " + question + "\n풀이: 정답은 " + answer + "입니다. 그 이유예요. 마지막 문장입니다.", out _, out _), "Explanation over two sentences should fail.");
        Require(!LectureProblems.TryParseAiResponse(problem, "문제: " + question + "\n풀이: 정답은 다른 값입니다.", out _, out _), "Explanation missing the generated answer should fail.");
        int wrongChoice = problem.Choices.First(x => !x.IsCorrect).Value;
        string wrongAsserted = "문제: " + question + "\n풀이: 정답은 " + answer + "입니다. 정답은 " + wrongChoice + "입니다.";
        Require(!LectureProblems.TryParseAiResponse(problem, wrongAsserted, out _, out _), "Explanation asserting a distractor as correct should fail.");
        string wrongInQuestion = "문제: " + question + " 정답은 " + wrongChoice + "입니다.\n풀이: 정답은 " + answer + "입니다.";
        Require(!LectureProblems.TryParseAiResponse(problem, wrongInQuestion, out _, out _), "Question asserting a distractor as correct should fail.");
        Console.WriteLine("AI response validation: 1 valid and 8 invalid examples checked (missing values, labels, question/explanation length/sentence count, answer, distractor asserted as correct).");
    }

    static void Main()
    {
        int generated = 0;
        foreach (LectureTopic topic in Enum.GetValues(typeof(LectureTopic)))
        foreach (bool experienced in new[] { false, true })
        for (int i = 0; i < 1000; i++)
        {
            var problem = LectureProblems.Generate(topic, experienced, new Random(17000 + generated));
            Require(problem.Prompt.Length <= 140, topic + ": offline question template exceeds 140 characters (" + problem.Prompt.Length + ").");
            Require(problem.Explanation.Length <= 160, topic + ": offline explanation template exceeds 160 characters (" + problem.Explanation.Length + ").");
            Require(problem.Choices != null && problem.Choices.Count == 4, topic + ": expected four choices, got " + (problem.Choices == null ? -1 : problem.Choices.Count) + " (answer " + problem.Answer + ").");
            Require(problem.Choices.Select(x => x.Value).Distinct().Count() == 4, topic + ": choices must be distinct.");
            Require(problem.CorrectIndex >= 0 && problem.CorrectIndex < 4, topic + ": correct choice index is invalid.");
            Require(problem.Choices.Count(x => x.IsCorrect) == 1 && problem.Choices[problem.CorrectIndex].IsCorrect, topic + ": exactly one choice must be correct.");
            Require(problem.Choices[problem.CorrectIndex].Value == problem.Answer, topic + ": correct choice differs from answer.");
            Require(problem.Choices.Where((x, index) => index != problem.CorrectIndex).All(x => !string.IsNullOrWhiteSpace(x.Misconception)), topic + ": every distractor needs a misconception explanation.");

            int expected;
            if (topic == LectureTopic.DataStructures)
            {
                Require(problem.Values.Count >= 3 && problem.Values.Count <= 5, "Data structure input size must be 3-5.");
                Require(problem.Values.All(x => x >= 1 && x <= 9) && problem.Values.Distinct().Count() == problem.Values.Count, "Data structure inputs must be distinct values from 1-9.");
                Require(problem.K >= 1 && problem.K <= problem.Values.Count, "Data structure dequeue index is outside the input.");
                expected = problem.UseStack ? problem.Values[problem.Values.Count - problem.K] : problem.Values[problem.K - 1];
            }
            else if (topic == LectureTopic.Algorithms)
            {
                Require(problem.Values.Count >= 7 && problem.Values.Count <= 15, "Algorithm input size must be 7-15.");
                Require(problem.Values.SequenceEqual(problem.Values.OrderBy(x => x)) && problem.Values.Distinct().Count() == problem.Values.Count, "Algorithm values must be sorted and distinct.");
                int targetIndex = problem.Values.IndexOf(problem.Target);
                Require(targetIndex >= 0, "Algorithm target must be in the list.");
                expected = experienced ? BinarySearchComparisons(problem.Values, problem.Target) : targetIndex + 1;
            }
            else if (!experienced)
            {
                Require(problem.BitString.Length >= 4 && problem.BitString.Length <= 5 && problem.BitString.All(c => c == '0' || c == '1'), "Beginner computer-basics problem must use a 4-5 bit string.");
                expected = FromBits(problem.BitString);
                Require(expected == problem.DecimalValue, "Binary-to-decimal input does not match its generated decimal value.");
            }
            else
            {
                Require(problem.DecimalValue >= 10 && problem.DecimalValue <= 63, "Experienced computer-basics input must be 10-63.");
                expected = problem.DecimalValue;
                Require(LectureProblems.NormalizeInput(topic, experienced, " 0 0 " + problem.CorrectInput + " ") == Bits(expected), "Binary normalization must ignore whitespace and leading zeroes.");
            }
            Require(problem.Answer == expected, topic + ": generated answer differs from an independent calculation.");
            Require(LectureProblems.NormalizeInput(topic, experienced, " 0 " + problem.CorrectInput + " ") == (topic == LectureTopic.ComputerBasics && experienced ? Bits(expected) : expected.ToString()), topic + ": typed answer normalization differs from the expected result.");
            generated++;
        }

        CheckAiValidation();
        Console.WriteLine("PASS: " + generated + " generated problems (3 topics × 2 levels × 1,000), independent answer checks, distinct choices, and typed normalization.");
    }
}
'@ | Set-Content (Join-Path $work 'Program.cs') -Encoding UTF8
& dotnet run --project (Join-Path $work 'Check.csproj') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Lecture problem checks failed; diagnostics retained at $work" }
Write-Output "Validation project retained at $work"
