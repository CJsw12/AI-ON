using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GazeLabChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception("GazeLab: " + message); }

    [MenuItem("Tools/Checks/Gaze Lab")]
    public static void Run()
    {
        foreach (GazeLabPath path in Enum.GetValues(typeof(GazeLabPath)))
            for (int i = 0; i < 1800; i++)
            {
                Vector2 p = GazeLabMotion.Target(path, i * .1f, 10);
                Check(p.x >= 119.9f && p.x <= 1160.1f && p.y >= 179.9f && p.y <= 720.1f, "Target must stay in the measurement area.");
            }
        Check(Vector2.Distance(GazeLabMotion.Target(GazeLabPath.FigureEight, 2, 10),
            GazeLabMotion.Target(GazeLabPath.FigureEight, 12, 10)) < .01f, "Trajectory must repeat deterministically.");
        Check(GazeLabMotion.Settling(GazeLabPath.Fixations, 3.2f) && !GazeLabMotion.Settling(GazeLabPath.Fixations, 4), "Exclude refixation transitions.");
        var oldCulture = CultureInfo.CurrentCulture;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/GazeLabChecks/" + Guid.NewGuid().ToString("N")));
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using (var session = new GazeLabSession(directory, "synthetic-test", GazeLabPath.Horizontal, 10, 30, "checks"))
            {
                var r = new GazeLabReading { sequence = 1, fresh = true, face = true, marker = true, calibrated = true,
                    shown = new Vector2(3, 4), raw = new Vector2(6, 8), hasRaw = true };
                Check(session.Record(1.5f, 5, 1, Vector2.zero, r, "tracking", true, 1280, 900, 1), "Accept a live calibrated sample.");
                r.shown = new Vector2(300, 400);
                Check(!session.Record(1.6f, 6, 2, Vector2.zero, r, "tracking", true, 1280, 900, 1), "Repeated sequences must not affect metrics.");
                r.sequence++; r.blink = true;
                Check(!session.Record(2, 7, 3, Vector2.zero, r, "tracking", true, 1280, 900, 1), "Blink must be excluded.");
                r.sequence++; r.blink = false;
                Check(!session.Record(3, 8, 4, Vector2.zero, r, "tracking", false, 1280, 900, 1), "Unfocused samples must be excluded.");
                r.sequence++; r.calibrated = false;
                Check(!session.Record(4, 9, 5, Vector2.zero, r, "tracking", true, 1280, 900, 1), "Uncalibrated samples must be excluded.");
                r.sequence++; r.calibrated = true; r.shown = new Vector2(float.NaN, 0);
                Check(!session.Record(5, 10, 6, Vector2.zero, r, "tracking", true, 1280, 900, 1), "Nonfinite samples must be excluded.");
                r.sequence++; r.shown = Vector2.zero;
                Check(!session.Record(6, 11, 7, Vector2.zero, r, "settling", true, 1280, 900, 1), "Settling samples must be excluded.");
                session.Publish(6, "tracking", true);
                Check(session.ValidCount == 1 && Mathf.Abs(session.Mean - 5) < .001f && Mathf.Abs(session.Rmse - 5) < .001f, "Known 3/4/5 error must be exact.");
                using (var reader = new StreamReader(new FileStream(session.CsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    string header = reader.ReadLine(), first = reader.ReadLine();
                    Check(header.Split(',').Length == first.Split(',').Length, "CSV column count must match under comma-decimal locale.");
                    Check(first.StartsWith("1.5,"), "CSV decimals must be invariant.");
                }
                session.Finish(6, "completed", true);
            }
            string latest = File.ReadAllText(Path.Combine(directory, "latest.json"));
            var summary = JsonUtility.FromJson<GazeLabSession.GazeLabSummary>(latest);
            Check(!summary.recording && summary.state == "completed" && summary.valid_samples == 1 && summary.raw_rmse_px == 10, "Final summary must be flushed and accurate.");
            Debug.Log("[GazeLab checks] PASS: 9000 trajectory bounds, deterministic motion, settling, duplicate/blink/focus/calibration/NaN exclusion, live CSV reads, invariant locale and final summary.");
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }
}
