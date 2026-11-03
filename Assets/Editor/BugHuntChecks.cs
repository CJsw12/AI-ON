using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Run outside Play mode so the game's shared clock and random stream stay untouched.
public static class BugHuntChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Read(BugHunt game, string name) => typeof(BugHunt).GetField(name, Private).GetValue(game);
    static void Call(BugHunt game, string name, params object[] args) => typeof(BugHunt).GetMethod(name, Private).Invoke(game, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception("BugHunt: " + message); }
    static string State(BugHunt game) => Read(game, "state").ToString();

    [MenuItem("Tools/Checks/Bug Hunt Guidance")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running guidance checks.");
        float clock = MiniGame.Now;
        UnityEngine.Random.State random = UnityEngine.Random.state;
        Action<string> sound = NovelUI.Sound;
        NovelUI.Sound = null;
        try
        {
            var theme = new MiniGameTheme { Kind = MiniGameKind.BugHunt, Title = "반복문 버그 사냥" };
            var beginner = (BugHunt)MiniGames.Create(theme, 0, experienced: false);
            Check((bool)Read(beginner, "beginner"), "Factory must forward non-major selection.");
            Call(beginner, "Prepare");
            Check(State(beginner) == "Guide", "Non-majors need a guide before the first timer.");
            MiniGame.Tick(60);
            Call(beginner, "UpdateState");
            Check(State(beginner) == "Guide" && (int)Read(beginner, "hits") == 0, "Reading must not time out or award points.");
            int[] answers = { 1, 3, 3 };
            for (int round = 0; round < answers.Length; round++)
            {
                Check(State(beginner) == "Guide", "Each round needs a guide.");
                Call(beginner, "Begin");
                MiniGame.Tick(19);
                Call(beginner, "UpdateState");
                Check(State(beginner) == "Playing", "Timer must start after the guide.");
                Call(beginner, "Pick", answers[round]);
                MiniGame.Tick(10);
                Call(beginner, "UpdateState");
                Check(State(beginner) == "Feedback", "Non-major explanation must wait for confirmation.");
                Call(beginner, "Next");
            }
            Check(beginner.Done && beginner.Delta == 2, "Three correct answers must retain the original reward.");
            Check(beginner.Summary == "버그 3마리 중 3마리 잡음", "Completion summary must be preserved.");

            foreach (bool explicitMajor in new[] { false, true })
            {
                var major = (BugHunt)(explicitMajor ? MiniGames.Create(theme, 0, experienced: true) : MiniGames.Create(theme, 0, true));
                Check(!(bool)Read(major, "beginner"), "Major and existing practice paths must retain the original behavior.");
                Call(major, "Prepare");
                for (int round = 0; round < 3; round++)
                {
                    Check(State(major) == "Playing", "Major must skip guides.");
                    MiniGame.Tick(20);
                    Call(major, "UpdateState");
                    Check(State(major) == "Feedback" && (int)Read(major, "picked") == -1, "20-second timeout must still work.");
                    MiniGame.Tick(3);
                    Call(major, "UpdateState");
                }
                Check(major.Done && major.Delta == 0, "Major timeout route must finish without points.");
            }
            var mixed = new BugHunt(theme, false);
            Call(mixed, "Prepare");
            for (int round = 0; round < 3; round++)
            {
                Call(mixed, "Begin");
                if (round == 0) Call(mixed, "Pick", 0);
                else if (round == 1) { MiniGame.Tick(20); Call(mixed, "UpdateState"); }
                else Call(mixed, "Pick", answers[round]);
                Check(State(mixed) == "Feedback", "Wrong answers and timeout must reach the explanation.");
                Call(mixed, "Next");
            }
            Check(mixed.Done && mixed.Delta == 1, "One correct answer must retain the original partial reward.");
            Debug.Log("BUG_HUNT_GUIDANCE_CHECKS PASS: factory modes, untimed guides, fresh 20-second timer, manual feedback, 3-round rewards, major/practice timeout and auto-advance.");
        }
        finally
        {
            typeof(MiniGame).GetProperty("Now").SetValue(null, clock);
            UnityEngine.Random.state = random;
            NovelUI.Sound = sound;
        }
    }
}
