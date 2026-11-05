using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Five save slots in PlayerPrefs. Each slot is one '|'-joined line (the project has no JSON module);
// the professor's reply is free text, so it is stored as Base64.
public static class GameSave
{
    public const int Slots = 5;
    const string Version = "1";

    public class Data
    {
        public int Faculty, Scene, Stage, Line, Professor, Affection, Questions, Teamwork, Interest, Solo;
        public bool Experienced, ShowResult;
        public string ResultLine = "";
        public string ResultKey;
        public int WrongPick = -1;   // Beginner quiz option already ruled out; absent in older saves.   // Which reply resultLine is, for skip mode; absent in older saves.
        public bool AiMode;          // Played in AI mode; absent (false) in older saves.
        public string Hint;          // The AI hint on screen; absent in older saves.
        public int HintsUsed;        // AI questions already asked at this quiz; absent (0) in older saves.
        public int EyeContactCount;
        public float EyeContactSeconds;
        public bool EyeContactTrackingEnabled;
        public bool LookAwayTriggered;
        public DateTime SavedAt;
    }

    static string Key(int slot) => "AION.Save." + slot;

    public static void Write(int slot, Data d)
    {
        string[] fields = {
            Version, DateTime.Now.Ticks.ToString(), d.Faculty.ToString(), d.Scene.ToString(), d.Stage.ToString(), d.Line.ToString(),
            d.Professor.ToString(), d.Affection.ToString(), d.Questions.ToString(), d.Teamwork.ToString(), d.Interest.ToString(), d.Solo.ToString(),
            d.Experienced ? "1" : "0", d.ShowResult ? "1" : "0", Convert.ToBase64String(Encoding.UTF8.GetBytes(d.ResultLine ?? "")),
            d.ResultKey ?? "", d.WrongPick.ToString(), d.AiMode ? "1" : "0", d.HintsUsed.ToString(), Convert.ToBase64String(Encoding.UTF8.GetBytes(d.Hint ?? "")),
            Math.Max(0, d.EyeContactCount).ToString(), Mathf.Max(0f, d.EyeContactSeconds).ToString("R", CultureInfo.InvariantCulture), d.EyeContactTrackingEnabled ? "1" : "0",
            d.LookAwayTriggered ? "1" : "0"
        };
        PlayerPrefs.SetString(Key(slot), string.Join("|", fields));
        PlayerPrefs.Save();
        any = null;
    }

    // Null for an empty slot or one written by an incompatible version.
    public static Data Read(int slot)
    {
        string[] f = PlayerPrefs.GetString(Key(slot), "").Split('|');
        // Optional fields added over time keep older saves loadable.
        if (f.Length < 15 || f.Length > 24 || f[0] != Version) return null;
        try
        {
            int eyeContactCount = f.Length > 20 ? Math.Max(0, int.Parse(f[20])) : 0;
            float eyeContactSeconds = 0f;
            if (f.Length > 21 && float.TryParse(f[21], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSeconds) &&
                !float.IsNaN(parsedSeconds) && !float.IsInfinity(parsedSeconds))
                eyeContactSeconds = Math.Max(0f, parsedSeconds);
            Data d = new Data
            {
                SavedAt = new DateTime(long.Parse(f[1])),
                Faculty = int.Parse(f[2]), Scene = int.Parse(f[3]), Stage = int.Parse(f[4]), Line = int.Parse(f[5]),
                Professor = int.Parse(f[6]), Affection = int.Parse(f[7]), Questions = int.Parse(f[8]), Teamwork = int.Parse(f[9]),
                Interest = int.Parse(f[10]), Solo = int.Parse(f[11]), Experienced = f[12] == "1", ShowResult = f[13] == "1",
                ResultLine = Encoding.UTF8.GetString(Convert.FromBase64String(f[14])),
                ResultKey = f.Length > 15 && f[15] != "" ? f[15] : null,
                WrongPick = f.Length > 16 ? int.Parse(f[16]) : -1,
                AiMode = f.Length > 17 && f[17] == "1",
                HintsUsed = f.Length > 18 ? Math.Max(0, int.Parse(f[18])) : 0,
                Hint = f.Length > 19 && f[19] != "" ? Encoding.UTF8.GetString(Convert.FromBase64String(f[19])) : null,
                EyeContactCount = eyeContactCount, EyeContactSeconds = eyeContactSeconds,
                EyeContactTrackingEnabled = f.Length > 22 && f[22] == "1",
                LookAwayTriggered = f.Length > 23 && f[23] == "1"
            };
            bool valid = d.Faculty >= 0 && d.Faculty < Stories.All.Length && d.Scene >= 1 && d.Scene <= 6 && (d.Stage == 0 || d.Stage == 1) && d.Line >= 0 && (d.Professor == 0 || d.Professor == 1);
            return valid ? d : null;
        }
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
        catch (OverflowException) { return null; }
    }

    // Cached: the title screen asks every GUI event. Write() clears it.
    static bool? any;
    public static bool Any()
    {
        if (any == null)
        {
            any = false;
            for (int i = 0; i < Slots; i++) if (Read(i) != null) { any = true; break; }
        }
        return any.Value;
    }
}
