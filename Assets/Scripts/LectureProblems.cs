using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public enum LectureTopic { DataStructures, Algorithms, ComputerBasics }

public sealed class LectureOption
{
    public int Value;
    public bool IsCorrect;
    public string Misconception;
}

public sealed class LectureProblem
{
    public LectureTopic Topic;
    public bool Experienced;
    public readonly List<int> Values = new List<int>();
    public readonly List<LectureOption> Choices = new List<LectureOption>();
    public int K, Target, DecimalValue, Answer, CorrectIndex;
    public bool UseStack;
    public string BitString, CorrectInput, Concept, Prompt, Explanation;
    public string[] RequiredInputs = new string[0];
}

// All answer-bearing data is generated and checked here, independently of Unity and the language model.
public static class LectureProblems
{
    static readonly Regex QuestionLabel = new Regex(@"^\s*문제\s*[:：]\s*(.*?)\s*$", RegexOptions.Compiled);
    static readonly Regex ExplanationLabel = new Regex(@"^\s*풀이\s*[:：]\s*(.*?)\s*$", RegexOptions.Compiled);

    public static LectureProblem Generate(LectureTopic topic, bool experienced, Random random)
    {
        if (random == null) random = new Random();
        switch (topic)
        {
            case LectureTopic.DataStructures: return DataStructure(experienced, random);
            case LectureTopic.Algorithms: return Algorithm(experienced, random);
            case LectureTopic.ComputerBasics: return ComputerBasics(experienced, random);
            default: throw new ArgumentOutOfRangeException("topic");
        }
    }

    static LectureProblem DataStructure(bool experienced, Random random)
    {
        var problem = new LectureProblem {
            Topic = LectureTopic.DataStructures,
            Experienced = experienced,
            Concept = "스택은 LIFO(나중에 넣은 값부터 꺼냄), 큐는 FIFO(먼저 넣은 값부터 꺼냄)입니다."
        };
        int length = random.Next(3, 6);
        var used = new HashSet<int>();
        while (problem.Values.Count < length)
        {
            int value = random.Next(1, 10);
            if (used.Add(value)) problem.Values.Add(value);
        }
        problem.UseStack = random.Next(0, 2) == 0;
        problem.K = random.Next(1, problem.Values.Count + 1);
        problem.Answer = problem.UseStack
            ? problem.Values[problem.Values.Count - problem.K]
            : problem.Values[problem.K - 1];
        problem.CorrectInput = problem.Answer.ToString();
        problem.RequiredInputs = problem.Values.Select(x => x.ToString()).Concat(new[] { problem.K.ToString() }).Distinct().ToArray();

        var mistakes = new List<KeyValuePair<int, string>>();
        int opposite = problem.UseStack ? problem.Values[problem.K - 1] : problem.Values[problem.Values.Count - problem.K];
        AddMistake(mistakes, opposite, "스택과 큐의 규칙을 서로 바꿔 생각했어요.");
        AddMistake(mistakes, problem.Values[0], "처음 넣은 값을 바로 꺼낸다고 착각했어요.");
        AddMistake(mistakes, problem.Values[problem.Values.Count - 1], "마지막에 넣은 값을 항상 고른 경우예요.");
        if (problem.K < problem.Values.Count)
        {
            int next = problem.UseStack
                ? problem.Values[problem.Values.Count - problem.K - 1]
                : problem.Values[problem.K];
            AddMistake(mistakes, next, "k번째가 아니라 그다음에 꺼낼 값을 골랐어요.");
        }
        AddMistake(mistakes, problem.K, "꺼낸 횟수 k를 값 자체로 착각했어요.");
        AddOptions(problem, mistakes, 1, 9, random, "목록 값은 맞아도 꺼내는 순서를 따라가야 해요.");

        string values = Join(problem.Values);
        string structure = problem.UseStack ? "스택" : "큐";
        string[] questions = {
            structure + "에 " + values + "를 차례로 넣어요. " + problem.K + "번째로 꺼내는 값은 무엇인가요?",
            "접시 쌓기(LIFO)와 줄 서기(FIFO) 중 " + structure + " 규칙을 씁니다. " + values + "를 넣고 " + problem.K + "번 꺼낼 때의 값은?",
            values + "를 순서대로 넣은 " + structure + "에서 " + problem.K + "번째로 나오는 값을 골라 보세요."
        };
        string order = problem.UseStack ? "마지막에 넣은 값부터 꺼내는 LIFO" : "먼저 넣은 값부터 꺼내는 FIFO";
        string[] explanations = {
            structure + "은 " + order + "예요. 넣은 순서를 따라가면 " + problem.K + "번째 값은 " + problem.Answer + "예요.",
            "넣은 순서 " + values + "에 " + (problem.UseStack ? "뒤에서부터" : "앞에서부터") + " 손을 대면 " + problem.K + "번째로 꺼내는 값은 " + problem.Answer + "예요."
        };
        problem.Prompt = questions[random.Next(questions.Length)];
        problem.Explanation = explanations[random.Next(explanations.Length)];
        return problem;
    }

    static LectureProblem Algorithm(bool experienced, Random random)
    {
        var problem = new LectureProblem {
            Topic = LectureTopic.Algorithms,
            Experienced = experienced,
            Concept = experienced
                ? "정렬된 배열의 이진 탐색은 lo와 hi를 포함하고 mid=(lo+hi)/2를 정수 내림으로 계산합니다. 비교 횟수를 셉니다."
                : "순차 탐색은 정렬된 목록의 왼쪽부터 값을 하나씩 비교하며, 비교 횟수를 셉니다."
        };
        int length = random.Next(7, 16);
        var used = new HashSet<int>();
        while (used.Count < length) used.Add(random.Next(1, 100));
        problem.Values.AddRange(used.OrderBy(x => x));
        int targetIndex = random.Next(problem.Values.Count);
        problem.Target = problem.Values[targetIndex];
        problem.Answer = experienced
            ? BinarySearchCount(problem.Values, problem.Target)
            : targetIndex + 1;
        problem.CorrectInput = problem.Answer.ToString();
        problem.RequiredInputs = problem.Values.Select(x => x.ToString()).Concat(new[] { problem.Target.ToString() }).Distinct().ToArray();

        int otherSearch = experienced ? targetIndex + 1 : BinarySearchCount(problem.Values, problem.Target);
        var mistakes = new List<KeyValuePair<int, string>>();
        AddMistake(mistakes, otherSearch, experienced
            ? "순차 탐색으로 끝까지 훑은 비교 횟수를 적었어요."
            : "가운데부터 범위를 줄이는 이진 탐색의 횟수를 적었어요.");
        AddMistake(mistakes, problem.Answer - 1, "비교 횟수 대신 0부터 세는 위치를 답했어요.");
        AddMistake(mistakes, problem.Answer + 1, "마지막 비교 뒤에 한 번을 더 세었어요.");
        AddMistake(mistakes, problem.Values.Count, "목록 길이를 실제 비교 횟수로 착각했어요.");
        AddMistake(mistakes, targetIndex, "인덱스와 비교 횟수를 혼동했어요.");
        AddOptions(problem, mistakes, 0, 20, random, "비교한 횟수와 목록의 위치·길이를 구분해 보세요.");

        string list = "[" + Join(problem.Values) + "]";
        string[] questions = experienced
            ? new[] {
                "정렬된 " + list + "에서 target=" + problem.Target + "을 이진 탐색해요. lo=0, hi=" + (length - 1) + "은 양끝 포함, mid=(lo+hi)/2는 내림일 때 몇 번 비교하나요?",
                "target=" + problem.Target + "을 " + list + "에서 찾습니다. 이진 탐색 규칙은 lo=0, hi=" + (length - 1) + ", mid=(lo+hi)/2 내림이에요. 비교 횟수는?",
                list + "에서 " + problem.Target + "을 이진 탐색합니다. [lo,hi] 양끝을 포함하고 mid=(lo+hi)/2를 내림할 때 비교 횟수는?"
            }
            : new[] {
                "정렬된 " + list + "을 왼쪽부터 순차 탐색해 " + problem.Target + "을 찾아요. 몇 번 비교하나요?",
                "목록 " + list + "을 순서대로 확인합니다. " + problem.Target + "을 찾을 때까지 비교하는 횟수는?",
                "순차 탐색으로 " + list + "에서 target=" + problem.Target + "을 찾으면 비교는 몇 번 필요한가요?"
            };
        var trace = new List<string>();
        if (experienced) BinarySearchTrace(problem.Values, problem.Target, trace);
        string[] explanations = experienced
            ? new[] {
                "mid는 (lo+hi)/2를 내림해요. 비교한 mid는 " + string.Join(", ", trace) + "이므로 " + problem.Answer + "번입니다.",
                "lo와 hi를 포함해 가운데부터 비교하면 " + string.Join(" → ", trace) + " 순서예요. target을 만날 때까지 " + problem.Answer + "번 비교해요."
            }
            : new[] {
                "순차 탐색은 왼쪽부터 확인해요. " + problem.Target + " 앞의 값까지 세면 " + problem.Answer + "번 비교합니다.",
                "목록의 앞에서부터 하나씩 비교해 target 위치에 닿을 때까지 " + problem.Answer + "번 확인해요."
            };
        problem.Prompt = questions[random.Next(questions.Length)];
        problem.Explanation = explanations[random.Next(explanations.Length)];
        return problem;
    }

    static LectureProblem ComputerBasics(bool experienced, Random random)
    {
        var problem = new LectureProblem {
            Topic = LectureTopic.ComputerBasics,
            Experienced = experienced,
            Concept = "2진수의 오른쪽 자리부터 값은 1, 2, 4, 8, 16, 32…이며 켜진 자리값을 더합니다."
        };
        var mistakes = new List<KeyValuePair<int, string>>();
        if (!experienced)
        {
            int width = random.Next(4, 6);
            problem.DecimalValue = random.Next(1, (1 << width));
            problem.BitString = ToBinary(problem.DecimalValue).PadLeft(width, '0');
            problem.Answer = BitsToInt(problem.BitString);
            problem.CorrectInput = problem.Answer.ToString();
            problem.RequiredInputs = new[] { problem.BitString };
            AddMistake(mistakes, BitsToInt(Reverse(problem.BitString)), "비트 순서를 거꾸로 읽었어요.");
            int lowestSet = LowestSetPower(problem.DecimalValue, width);
            AddMistake(mistakes, problem.DecimalValue - lowestSet, "켜진 자리값 하나를 더하지 않았어요.");
            int lowestOff = LowestOffPower(problem.DecimalValue, width);
            AddMistake(mistakes, problem.DecimalValue + lowestOff, "꺼진 자리에 자리값을 잘못 더했어요.");
            AddMistake(mistakes, CountOnes(problem.BitString), "켜진 비트의 개수를 값으로 착각했어요.");
            AddOptions(problem, mistakes, 0, (1 << width) - 1, random, "켜진 자리와 비트 순서를 확인해 보세요.");

            string[] questions = {
                "2진수 " + problem.BitString + "에서 켜진 자리값을 더하면 10진수는 얼마인가요?",
                "자리값 1, 2, 4, 8, 16 순서로 읽어요. 2진수 " + problem.BitString + "은 10진수 몇인가요?",
                "컴퓨터가 저장한 " + problem.BitString + "을 10진수로 바꿔 보세요."
            };
            string sum = BinarySum(problem.BitString);
            string[] explanations = {
                "오른쪽부터 자리값은 1, 2, 4, 8, 16이에요. 켜진 자리의 합 " + sum + "은 " + problem.Answer + "예요.",
                problem.BitString + "에서 1인 자리값만 골라 더하면 " + sum + "이므로 " + problem.Answer + "예요."
            };
            problem.Prompt = questions[random.Next(questions.Length)];
            problem.Explanation = explanations[random.Next(explanations.Length)];
        }
        else
        {
            problem.DecimalValue = random.Next(10, 64);
            problem.Answer = problem.DecimalValue;
            problem.BitString = ToBinary(problem.DecimalValue);
            problem.CorrectInput = problem.BitString;
            problem.RequiredInputs = new[] { problem.DecimalValue.ToString() };
            AddMistake(mistakes, BitsToInt(Reverse(problem.BitString)), "비트 순서를 거꾸로 바꿨어요.");
            int lowestSet = LowestSetPower(problem.DecimalValue, 6);
            AddMistake(mistakes, problem.DecimalValue - lowestSet, "켜진 자리값 하나를 빠뜨렸어요.");
            int lowestOff = LowestOffPower(problem.DecimalValue, 6);
            AddMistake(mistakes, problem.DecimalValue + lowestOff, "꺼진 자리에 자리값을 더했어요.");
            AddMistake(mistakes, CountOnes(problem.BitString), "켜진 비트 수를 10진수 값으로 착각했어요.");
            AddOptions(problem, mistakes, 0, 63, random, "각 1의 자리에 대응하는 자리값을 더해 보세요.");

            string[] questions = {
                "10진수 " + problem.DecimalValue + "을 2진수로 쓰면 무엇인가요? 앞자리 0은 생략해요.",
                "자리값 1, 2, 4, 8, 16, 32를 사용해 " + problem.DecimalValue + "을 2진수로 적어 보세요.",
                problem.DecimalValue + "을 2진수로 바꿔 입력하세요. 앞자리 0은 쓰지 않아도 돼요."
            };
            string[] explanations = {
                "" + problem.DecimalValue + " = " + BinarySum(problem.BitString) + "이므로 2진수는 " + problem.BitString + "이에요.",
                "켜진 자리값을 더해 " + problem.DecimalValue + "을 만들면 " + problem.BitString + "이 됩니다."
            };
            problem.Prompt = questions[random.Next(questions.Length)];
            problem.Explanation = explanations[random.Next(explanations.Length)];
        }
        return problem;
    }

    static void AddMistake(List<KeyValuePair<int, string>> mistakes, int value, string explanation)
    {
        mistakes.Add(new KeyValuePair<int, string>(value, explanation));
    }

    static void AddOptions(LectureProblem problem, List<KeyValuePair<int, string>> mistakes, int minValue, int maxValue, Random random, string fillExplanation)
    {
        var options = new List<LectureOption> {
            new LectureOption { Value = problem.Answer, IsCorrect = true, Misconception = "" }
        };
        foreach (var mistake in mistakes)
        {
            if (options.Count >= 4) break;
            AddOption(options, problem.Answer, mistake.Key, mistake.Value);
        }
        for (int value = minValue; options.Count < 4 && value <= maxValue; value++)
            AddOption(options, problem.Answer, value, fillExplanation);
        for (int radius = 1; options.Count < 4; radius++)
        {
            AddOption(options, problem.Answer, problem.Answer - radius, fillExplanation);
            AddOption(options, problem.Answer, problem.Answer + radius, fillExplanation);
        }
        for (int i = options.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            LectureOption tmp = options[i]; options[i] = options[j]; options[j] = tmp;
        }
        problem.Choices.AddRange(options);
        problem.CorrectIndex = problem.Choices.FindIndex(x => x.IsCorrect);
    }

    static void AddOption(List<LectureOption> options, int answer, int value, string explanation)
    {
        if (value == answer || options.Any(x => x.Value == value)) return;
        options.Add(new LectureOption { Value = value, IsCorrect = false, Misconception = explanation });
    }

    public static string NormalizeInput(LectureTopic topic, bool experienced, string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var compact = new StringBuilder(input.Length);
        foreach (char c in input) if (!char.IsWhiteSpace(c)) compact.Append(c);
        string value = compact.ToString();
        if (value.Length == 0) return "";
        if (topic == LectureTopic.ComputerBasics && experienced)
        {
            for (int i = 0; i < value.Length; i++) if (value[i] != '0' && value[i] != '1') return "";
            int first = 0;
            while (first < value.Length - 1 && value[first] == '0') first++;
            return value.Substring(first);
        }
        for (int i = 0; i < value.Length; i++) if (value[i] < '0' || value[i] > '9') return "";
        int digit = 0;
        while (digit < value.Length - 1 && value[digit] == '0') digit++;
        return value.Substring(digit);
    }

    public static string MisconceptionFor(LectureProblem problem, string input)
    {
        if (problem == null) return "계산 과정을 다시 확인해 보세요.";
        string normalized = NormalizeInput(problem.Topic, problem.Experienced, input);
        int value;
        bool hasValue = normalized != "";
        if (hasValue && problem.Topic == LectureTopic.ComputerBasics && problem.Experienced)
        {
            try { value = BitsToInt(normalized); }
            catch { hasValue = false; value = 0; }
        }
        else if (hasValue) hasValue = int.TryParse(normalized, out value);
        else value = 0;
        if (hasValue)
            foreach (var choice in problem.Choices)
                if (!choice.IsCorrect && choice.Value == value) return choice.Misconception;
        if (!hasValue) return "숫자 형식을 확인해요. 공백은 빼고 답을 입력해 주세요.";
        switch (problem.Topic)
        {
            case LectureTopic.DataStructures: return "값을 찾기만 하지 말고, 스택이나 큐에서 꺼내는 순서를 따라가야 해요.";
            case LectureTopic.Algorithms: return problem.Experienced
                ? "이진 탐색은 비교한 mid의 개수를 세야 해요. lo, hi, mid를 차례로 따라가 보세요."
                : "순차 탐색은 target까지 실제로 비교한 횟수를 세야 해요. 위치를 0부터 세는 것과 구분해요.";
            default: return problem.Experienced
                ? "각 비트가 나타내는 자리값을 더해 이진수로 바꿔 보세요."
                : "켜진 비트의 개수가 아니라 각 자리의 값 1, 2, 4, 8, 16…을 더해요.";
        }
    }

    public static bool TryParseAiResponse(LectureProblem problem, string response, out string question, out string explanation)
    {
        question = explanation = "";
        if (problem == null || string.IsNullOrWhiteSpace(response)) return false;
        string[] lines = response.Replace("\r", "").Split('\n');
        int questionCount = 0, explanationCount = 0, nonEmpty = 0;
        foreach (string raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            nonEmpty++;
            Match q = QuestionLabel.Match(raw);
            Match e = ExplanationLabel.Match(raw);
            if (q.Success) { question = Unquote(q.Groups[1].Value); questionCount++; }
            else if (e.Success) { explanation = Unquote(e.Groups[1].Value); explanationCount++; }
            else return false;
        }
        if (questionCount != 1 || explanationCount != 1 || nonEmpty != 2) return false;
        if (question.Length == 0 || question.Length > 140 || explanation.Length == 0 || explanation.Length > 160 || !IsAtMostTwoSentences(explanation)) return false;
        foreach (string required in problem.RequiredInputs ?? new string[0])
        {
            if (problem.Topic == LectureTopic.ComputerBasics && !problem.Experienced && required == problem.BitString)
            {
                if (question.IndexOf(required, StringComparison.Ordinal) < 0) return false;
            }
            else if (!HasNumber(question, required)) return false;
        }
        if (!HasNumber(explanation, problem.CorrectInput)) return false;
        if (ClaimsDistractorAsCorrect(problem, question) || ClaimsDistractorAsCorrect(problem, explanation)) return false;
        return true;
    }

    public static bool ClaimsDistractorAsCorrect(LectureProblem problem, string text)
    {
        if (problem == null || string.IsNullOrEmpty(text)) return false;
        foreach (var option in problem.Choices)
        {
            if (option.IsCorrect) continue;
            if (ClaimsValueAsCorrect(text, option.Value.ToString())) return true;
            if (problem.Topic == LectureTopic.ComputerBasics && problem.Experienced && ClaimsValueAsCorrect(text, ToBinary(option.Value))) return true;
        }
        return false;
    }

    static bool ClaimsValueAsCorrect(string text, string value) =>
        Regex.IsMatch(text, @"(?:정답|답)\s*(?:은|이|:|：)\s*" + Regex.Escape(value) + @"(?!\d)");

    static string Unquote(string value) => value.Trim().Trim('"', '“', '”', '\'', '‘', '’');
    static bool HasNumber(string text, string number) => Regex.IsMatch(text, @"(?<!\d)" + Regex.Escape(number) + @"(?!\d)");

    public static bool IsAtMostTwoSentences(string text)
    {
        int endings = 0;
        foreach (char c in text ?? "") if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！' || c == '？') endings++;
        return endings <= 2;
    }

    static int BinarySearchCount(IList<int> values, int target)
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
        return count;
    }

    static void BinarySearchTrace(IList<int> values, int target, List<string> trace)
    {
        int lo = 0, hi = values.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            trace.Add(values[mid].ToString());
            if (values[mid] == target) return;
            if (values[mid] < target) lo = mid + 1;
            else hi = mid - 1;
        }
    }

    static string Join(IEnumerable<int> values) => string.Join(", ", values.Select(x => x.ToString()).ToArray());
    static string ToBinary(int value) => Convert.ToString(value, 2);
    static int BitsToInt(string bits) => Convert.ToInt32(bits, 2);
    static string Reverse(string value) => new string(value.Reverse().ToArray());
    static int CountOnes(string bits) => bits.Count(x => x == '1');
    static int LowestSetPower(int value, int width)
    {
        for (int i = 0; i < width; i++) if ((value & (1 << i)) != 0) return 1 << i;
        return 0;
    }
    static int LowestOffPower(int value, int width)
    {
        for (int i = 0; i < width; i++) if ((value & (1 << i)) == 0) return 1 << i;
        return 0;
    }
    static string BinarySum(string bits)
    {
        var parts = new List<string>();
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] != '1') continue;
            int value = 1 << (bits.Length - i - 1);
            parts.Add(value.ToString());
        }
        return parts.Count == 0 ? "0" : string.Join("+", parts.ToArray());
    }
}
