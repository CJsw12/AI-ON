using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Observation-time comparison, not sensor ground truth: pursuit delay includes the participant's eye movement.
public enum GazeLabPath { Horizontal, Vertical, Ellipse, FigureEight, Fixations }

public static class GazeLabMotion
{
    public static Vector2 Target(GazeLabPath path, float seconds, float period)
    {
        float phase = Mathf.Max(0, seconds) / Mathf.Max(2, period) * Mathf.PI * 2;
        switch (path)
        {
            case GazeLabPath.Horizontal: return new Vector2(640 + 520 * Mathf.Sin(phase), 450);
            case GazeLabPath.Vertical: return new Vector2(640, 450 + 270 * Mathf.Sin(phase));
            case GazeLabPath.Ellipse: return new Vector2(640 + 520 * Mathf.Sin(phase), 450 - 270 * Mathf.Cos(phase));
            case GazeLabPath.FigureEight: return new Vector2(640 + 520 * Mathf.Sin(phase), 450 + 270 * Mathf.Sin(2 * phase));
            default:
                int point = Mathf.FloorToInt(Mathf.Max(0, seconds) / 3) % 9;
                return new Vector2(120 + point % 3 * 520, 180 + point / 3 * 270);
        }
    }

    public static bool Settling(GazeLabPath path, float seconds) => seconds < .7f ||
        (path == GazeLabPath.Fixations && seconds % 3 < .7f);
}

public struct GazeLabReading
{
    public long sequence;
    public bool face, fresh, marker, blink, calibrated;
    public float age, latencyMs;
    public Vector2 shown, raw, eye, head, look, position, drift;
    public bool hasRaw;
    public float blinkStrength;
    public float headDistance;
    public Vector2 networkRay;
}

public sealed class GazeLabSession : IDisposable
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    readonly StreamWriter csv;
    readonly List<float> distances = new List<float>(8192);
    readonly StringBuilder row = new StringBuilder(640);
    readonly string root, summaryPath;
    readonly GazeLabSummary summary;
    long lastSequence = long.MinValue;
    double distanceSum, squareSum, sumX, sumY, rawSquareSum;
    int rawCount;
    bool disposed;

    [Serializable]
    public sealed class GazeLabSummary
    {
        public int schema = 1;
        public string session, csv_path, source, pattern, revision, calibration, unity, started_utc, updated_utc, state;
        public string coordinate_system = "1280x900 canvas, origin top-left, x right/y down; error=gaze-target";
        public string timing = "Unity Repaint observation time; not camera exposure time; includes human pursuit lag";
        public float period_s, duration_s, elapsed_s;
        public int rows, unique_samples, valid_samples, invalid_samples;
        public float mean_px, rmse_px, p95_px, bias_x_px, bias_y_px, raw_rmse_px, valid_percent;
        public bool metrics_available, raw_metrics_available, recording, focused;
    }

    public string CsvPath => summary.csv_path;
    public int ValidCount => summary.valid_samples;
    public float Mean => distances.Count == 0 ? 0 : (float)(distanceSum / distances.Count);
    public float Rmse => distances.Count == 0 ? 0 : (float)Math.Sqrt(squareSum / distances.Count);
    public Vector2 Bias => distances.Count == 0 ? Vector2.zero : new Vector2((float)(sumX / distances.Count), (float)(sumY / distances.Count));

    public GazeLabSession(string directory, string source, GazeLabPath pattern, float period, float duration, string revision, string calibration = "")
    {
        root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", Inv) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        summaryPath = Path.Combine(root, id + ".json");
        summary = new GazeLabSummary { session = id, csv_path = Path.Combine(root, id + ".csv"), source = source,
            pattern = pattern.ToString(), period_s = period, duration_s = duration, revision = revision, calibration = calibration,
            unity = Application.unityVersion, started_utc = DateTime.UtcNow.ToString("O"), state = "recording", recording = true };
        csv = new StreamWriter(new FileStream(CsvPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false), 32768);
        csv.WriteLine("elapsed_s,realtime_s,frame,sequence,new_sample,included,phase,focused,face,fresh,marker,blink,calibrated,age_s,latency_ms,target_x,target_y,gaze_x,gaze_y,raw_x,raw_y,error_x,error_y,error_px,eye_x,eye_y,head_x,head_y,look_x,look_y,position_x,position_y,drift_x,drift_y,screen_width,screen_height,canvas_scale,has_raw,blink_strength,head_distance_m,network_yaw_rad,network_pitch_rad");
        try { Publish(0, "countdown", true); }
        catch { csv.Dispose(); throw; }
    }

    public bool Record(float elapsed, double realtime, int frame, Vector2 target, GazeLabReading r,
        string phase, bool focused, int width, int height, float scale)
    {
        if (disposed) return false;
        bool freshSequence = r.sequence > 0 && r.sequence != lastSequence;
        lastSequence = r.sequence;
        bool valid = freshSequence && phase == "tracking" && focused && r.face && r.fresh && r.marker && !r.blink && r.calibrated
            && Finite(r.shown) && Finite(target);
        Vector2 error = r.shown - target;
        if (freshSequence && phase == "tracking")
        {
            summary.unique_samples++;
            if (!valid) summary.invalid_samples++;
        }
        if (valid)
        {
            distances.Add(error.magnitude);
            distanceSum += error.magnitude; squareSum += error.sqrMagnitude;
            sumX += error.x; sumY += error.y;
            if (r.hasRaw && Finite(r.raw)) { rawSquareSum += (r.raw - target).sqrMagnitude; rawCount++; }
            summary.valid_samples++;
        }
        row.Clear();
        Add(elapsed); Add(realtime); Add(frame); Add(r.sequence); Add(freshSequence); Add(valid); Add(phase); Add(focused);
        Add(r.face); Add(r.fresh); Add(r.marker); Add(r.blink); Add(r.calibrated); Add(r.age); Add(r.latencyMs);
        Add(target.x); Add(target.y); Add(r.marker ? r.shown.x : float.NaN); Add(r.marker ? r.shown.y : float.NaN);
        Add(r.hasRaw ? r.raw.x : float.NaN); Add(r.hasRaw ? r.raw.y : float.NaN);
        Add(valid ? error.x : float.NaN); Add(valid ? error.y : float.NaN); Add(valid ? error.magnitude : float.NaN);
        Add(r.eye.x); Add(r.eye.y); Add(r.head.x); Add(r.head.y); Add(r.look.x); Add(r.look.y);
        Add(r.position.x); Add(r.position.y); Add(r.drift.x); Add(r.drift.y); Add(width); Add(height); Add(scale); Add(r.hasRaw); Add(r.blinkStrength);
        Add(r.headDistance > 0 ? r.headDistance : float.NaN); Add(r.networkRay.x); Add(r.networkRay.y);
        csv.WriteLine(row.ToString());
        summary.rows++;
        return valid;
    }

    void Add(bool v) => Add(v ? "1" : "0");
    void Add(double v) => Add(double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString("0.######", Inv));
    void Add(long v) => Add(v.ToString(Inv));
    void Add(string v) { if (row.Length > 0) row.Append(','); row.Append(v); }
    public static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

    public void Publish(float elapsed, string state, bool focused)
    {
        if (disposed) return;
        csv.Flush();
        summary.updated_utc = DateTime.UtcNow.ToString("O"); summary.elapsed_s = elapsed;
        summary.state = state; summary.focused = focused;
        summary.metrics_available = distances.Count > 0;
        summary.raw_metrics_available = rawCount > 0;
        summary.mean_px = Mean; summary.rmse_px = Rmse;
        summary.bias_x_px = Bias.x; summary.bias_y_px = Bias.y;
        summary.valid_percent = summary.unique_samples == 0 ? 0 : 100f * summary.valid_samples / summary.unique_samples;
        summary.raw_rmse_px = rawCount == 0 ? 0 : (float)Math.Sqrt(rawSquareSum / rawCount);
        if (distances.Count > 0)
        {
            var sorted = distances.ToArray(); Array.Sort(sorted);
            summary.p95_px = sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length * .95f) - 1, 0, sorted.Length - 1)];
        }
        string json = JsonUtility.ToJson(summary, true);
        AtomicWrite(summaryPath, json);
        AtomicWrite(Path.Combine(root, "latest.json"), json);
    }

    static void AtomicWrite(string path, string json)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }

    public void Finish(float elapsed, string state, bool focused)
    {
        if (disposed) return;
        summary.recording = false;
        try { Publish(elapsed, state, focused); }
        finally { disposed = true; csv.Dispose(); }
    }
    public void Dispose() => Finish(summary.elapsed_s, "interrupted", summary.focused);
}
