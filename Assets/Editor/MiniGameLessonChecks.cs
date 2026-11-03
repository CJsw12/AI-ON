using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MiniGameLessonChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static FieldInfo Field(object x, string name)
    {
        for (Type t = x.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, All); if (f != null) return f; }
        throw new MissingFieldException(name);
    }
    public static object Read(object x, string name) => Field(x, name).GetValue(x);
    static void Set(object x, string name, object value) => Field(x, name).SetValue(x, value);
    static object Call(object x, string name, params object[] args)
    {
        for (Type t = x.GetType(); t != null; t = t.BaseType) { var m = t.GetMethod(name, All); if (m != null) return m.Invoke(x, args); }
        throw new MissingMethodException(name);
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception("Lesson check: " + message); }
    static MiniGameTheme Theme(MiniGameKind kind) => new MiniGameTheme { Kind = kind, Title = kind.ToString() };
    static void Lesson(MiniGame game)
    {
        Check(game.Reviewing, game.Title + " needs a review.");
        Check(!string.IsNullOrWhiteSpace((string)Read(game, "failureReason")), "Missing reason.");
        Check(!string.IsNullOrWhiteSpace((string)Read(game, "failureCorrection")), "Missing correction.");
    }

    // Also used by a temporary Play-mode probe to inspect the real review screens.
    public static List<MiniGame> FailureExamples()
    {
        var examples = new List<MiniGame>();
        for (int round = 0; round < 3; round++)
        {
            var bug = new BugHunt(Theme(MiniGameKind.BugHunt), false);
            Set(bug, "round", round); Call(bug, "Begin"); Call(bug, "Pick", -1); examples.Add(bug);
            var coffee = new CoffeeOrder(0, Theme(MiniGameKind.Coffee), true);
            Set(coffee, "round", round); Call(coffee, "Add", 3); Call(coffee, "Serve"); examples.Add(coffee);
            var tray = new PlateBalance(Theme(MiniGameKind.Tray));
            Set(tray, "meal", round); var plate = (List<int>)Read(tray, "plate"); plate.AddRange(new[] { 0, 1, 6 });
            Call(tray, "Serve"); examples.Add(tray);
            var route = new RobotRoute(Theme(MiniGameKind.Route));
            Set(route, "map", round); Call(route, "Begin"); Call(route, "End", false, "로봇이 벽에 부딪혔어요."); examples.Add(route);
        }
        var cards = new CardMatch(Theme(MiniGameKind.Cards));
        Call(cards, "Flip", 0); Call(cards, "Flip", 2); Call(cards, "Finish"); examples.Add(cards);
        var probe = new CircuitProbe(Theme(MiniGameKind.CircuitProbe), false);
        Call(probe, "Begin"); Set(probe, "broken", 7); Call(probe, "Replace", 0); examples.Add(probe);
        var overBudget = new CircuitProbe(Theme(MiniGameKind.CircuitProbe));
        Call(overBudget, "Begin"); Set(overBudget, "broken", 0); Set(overBudget, "probes", 4); Call(overBudget, "Replace", 0); examples.Add(overBudget);
        for (int customer = 0; customer < 5; customer++)
        {
            var cashier = new ChangeMaker(Theme(MiniGameKind.Cashier)); Set(cashier, "customer", customer);
            Call(cashier, "Begin"); Call(cashier, "Hand"); examples.Add(cashier);
        }
        for (int day = 0; day < 3; day++)
        {
            var price = new PriceTag(Theme(MiniGameKind.PriceTag)); Set(price, "day", day); Call(price, "Choose", 0); examples.Add(price);
        }
        var rhythm = new StretchRhythm(Theme(MiniGameKind.Rhythm));
        var judged = (int[])Read(rhythm, "judged"); for (int i = 0; i < judged.Length; i++) judged[i] = 1;
        Set(rhythm, "misses", 5); Call(rhythm, "Finish"); examples.Add(rhythm);
        var timing = new LoopTiming(Theme(MiniGameKind.Timing)); Call(timing, "Stop", 12); examples.Add(timing);
        var timeout = new CoffeeOrder(0, Theme(MiniGameKind.Coffee), true);
        MiniGame.Tick(61); Call(timeout, "Finish"); examples.Add(timeout);
        return examples;
    }

    [MenuItem("Tools/Checks/Minigame Failure Lessons")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
        float clock = MiniGame.Now;
        var random = UnityEngine.Random.state;
        var sound = NovelUI.Sound;
        NovelUI.Sound = null;
        try
        {
            BugHuntChecks.Run();
            var failures = FailureExamples();
            foreach (var game in failures)
            {
                Lesson(game);
                int delta = game.Delta; bool done = game.Done;
                Call(game, "CloseReview");
                Check(!game.Reviewing && game.Delta == delta && game.Done == done, "Review must not alter score/completion.");
            }
            foreach (bool experienced in new[] { false, true })
            {
                var probe = MiniGames.Create(Theme(MiniGameKind.CircuitProbe), 0, experienced: experienced);
                Check((bool)Read(probe, "beginner") == !experienced, "Experience mode was not forwarded.");
                for (int broken = 0; broken < 8; broken++)
                {
                    Call(probe, "Begin"); Set(probe, "broken", broken);
                    int low = 0, high = 7;
                    while (low < high)
                    {
                        int middle = (low + high) / 2; Call(probe, "Probe", middle);
                        if (broken > middle) low = middle + 1; else high = middle;
                    }
                    Check((int)Read(probe, "probes") == 3, "Every fault must be solvable within the original three probes.");
                    Call(probe, "Replace", low);
                    Check(!probe.Reviewing, "Successful repair must not show a failure lesson.");
                }
            }
            for (int map = 0; map < 3; map++)
            {
                var route = new RobotRoute(Theme(MiniGameKind.Route)); Set(route, "map", map); Call(route, "Begin");
                var maps = (Array)Read(route, "Maps"); string text = (string)Read(maps.GetValue(map), "Solution");
                var program = (List<int>)Read(route, "program");
                foreach (string token in text.Split('→'))
                {
                    string instruction = token.Trim();
                    if (instruction.StartsWith("앞으로")) { int n = int.Parse(Regex.Match(instruction, @"\d+").Value); for (int i = 0; i < n; i++) program.Add(0); }
                    else program.Add(instruction == "왼쪽" ? 1 : 2);
                }
                Check(program.Count <= 14, "Example route exceeds the command limit.");
                for (int i = 0; i < program.Count && !(bool)Read(route, "arrived"); i++) Call(route, "Tick");
                Check((bool)Read(route, "arrived") && !route.Reviewing, "Displayed route must actually reach its goal.");
            }
            for (int round = 0; round < 3; round++)
            {
                var coffee = new CoffeeOrder(0, Theme(MiniGameKind.Coffee), true); Set(coffee, "round", round);
                var recipes = (int[][])Read(coffee, "recipes"); var cup = (List<int>)Read(coffee, "cup");
                cup.AddRange(recipes[round]); if (round == 0) cup.Reverse(); Call(coffee, "Serve");
                Check(!coffee.Reviewing && (int)Read(coffee, "correct") == 1, "Correct coffee must retain the original count/order rule.");
            }
            var wrongOrder = new CoffeeOrder(0, Theme(MiniGameKind.Coffee), true);
            Set(wrongOrder, "round", 1);
            ((int[][])Read(wrongOrder, "recipes"))[1] = new[] { 0, 1, 2, 3 };
            ((List<int>)Read(wrongOrder, "cup")).AddRange(new[] { 1, 0, 2, 3 }); Call(wrongOrder, "Serve");
            Lesson(wrongOrder); Check(((string)Read(wrongOrder, "failureReason")).Contains("1번째"), "Ordered coffee must identify the first wrong slot even with matching counts.");
            int[] bestPrices = { 2, 3, 1 };
            for (int day = 0; day < 3; day++)
                for (int choice = 0; choice < 5; choice++)
                {
                    var price = new PriceTag(Theme(MiniGameKind.PriceTag)); Set(price, "day", day); Call(price, "Choose", choice);
                    Check(price.Reviewing == (choice != bestPrices[day]), "Price review must agree with the original best-profit rule.");
                }
            int[] change = { 4200, 1500, 14000, 39000, 2500 };
            for (int customer = 0; customer < change.Length; customer++)
            {
                var cashier = new ChangeMaker(Theme(MiniGameKind.Cashier)); Set(cashier, "customer", customer); Call(cashier, "Begin");
                Set(cashier, "given", change[customer]); Call(cashier, "Hand");
                Check(!cashier.Reviewing && (int)Read(cashier, "correct") == 1, "Correct change must not trigger a failure review.");
            }
            for (int meal = 0; meal < 3; meal++)
            {
                var tray = new PlateBalance(Theme(MiniGameKind.Tray)); Set(tray, "meal", meal);
                string example = (string)Call(tray, "Solution"); var dishes = (Array)Read(tray, "Dishes");
                var plate = (List<int>)Read(tray, "plate");
                for (int i = 0; i < dishes.Length; i++) if (example.Contains((string)Read(dishes.GetValue(i), "Name"))) plate.Add(i);
                Check(plate.Count == 3, "Food example needs exactly three distinct dishes."); Call(tray, "Serve");
                Check(!tray.Reviewing && (bool)Read(tray, "lastOk"), "Displayed food example must satisfy all targets.");
            }
            Debug.Log("MINIGAME_LESSONS_CHECKS PASS: " + failures.Count + " failure examples; 16 circuit faults; 3 real routes; coffee count/order; 15 price choices; 5 cashier successes; 3 valid meal solutions; review dismissal preserves rewards.");
        }
        finally
        {
            typeof(MiniGame).GetProperty("Now").SetValue(null, clock);
            UnityEngine.Random.state = random; NovelUI.Sound = sound;
        }
    }
}
