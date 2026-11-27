using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

public sealed class SignPlayLog
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    readonly string clip;
    readonly long startedMs;
    readonly StringBuilder line = new StringBuilder(1024);
    StreamWriter writer;

    SignPlayLog(StreamWriter writer, string clip, long startedMs)
    {
        this.writer = writer;
        this.clip = clip;
        this.startedMs = startedMs;
    }

    public static bool TryOpen(string directory, DateTime localTime, long startedMs, out SignPlayLog log)
    {
        log = null;
        FileStream stream = null;
        try
        {
            Directory.CreateDirectory(directory);
            string clip;
            while (true)
            {
                clip = localTime.ToString("yyyyMMdd-HHmmss", Invariant);
                string path = Path.Combine(directory, clip + ".jsonl");
                try
                {
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    break;
                }
                catch (IOException)
                {
                    if (!File.Exists(path)) throw;
                    localTime = localTime.AddSeconds(1);
                }
            }
            log = new SignPlayLog(new StreamWriter(stream, new UTF8Encoding(false)), clip, startedMs);
            return true;
        }
        catch (Exception error)
        {
            if (!IsFileError(error)) throw;
            if (stream != null) stream.Dispose();
            return false;
        }
    }

    public bool TryWriteFrame(long timeMs, string expected, int width, int height, string handedness,
        float handednessScore, float[] landmarks, string stage, string mode, string rule, string confirmed)
    {
        if (writer == null) return false;
        line.Clear();
        AppendText("{\"clip\":", clip);
        AppendText(",\"expected\":", expected);
        line.Append(",\"held\":null,\"t\":").Append(((timeMs - startedMs) / 1000.0).ToString("F3", Invariant));
        line.Append(",\"width\":").Append(width.ToString(Invariant));
        line.Append(",\"height\":").Append(height.ToString(Invariant));
        AppendText(",\"handedness\":", landmarks == null ? null : handedness);
        line.Append(",\"handednessScore\":");
        if (landmarks == null) line.Append("null");
        else AppendNumber(handednessScore);
        line.Append(",\"landmarks\":");
        if (landmarks == null) line.Append("null");
        else
        {
            line.Append('[');
            for (int i = 0; i < landmarks.Length; i++)
            {
                if (i > 0) line.Append(',');
                AppendNumber(landmarks[i]);
            }
            line.Append(']');
        }
        AppendText(",\"stage\":", stage);
        AppendText(",\"mode\":", mode);
        AppendText(",\"rule\":", rule);
        AppendText(",\"confirmed\":", confirmed);
        line.Append('}');
        try
        {
            writer.WriteLine(line.ToString());
            return true;
        }
        catch (Exception error)
        {
            if (!IsFileError(error)) throw;
            TryClose();
            return false;
        }
    }

    public bool TryClose()
    {
        var open = writer;
        writer = null;
        try
        {
            if (open != null) open.Dispose();
            return true;
        }
        catch (Exception error)
        {
            if (!IsFileError(error)) throw;
            return false;
        }
    }

    static bool IsFileError(Exception error)
    {
        return error is IOException || error is UnauthorizedAccessException || error is SecurityException ||
               error is ArgumentException || error is NotSupportedException;
    }

    void AppendNumber(float value)
    {
        line.Append(float.IsNaN(value) || float.IsInfinity(value) ? "null" : value.ToString("F5", Invariant));
    }

    void AppendText(string key, string value)
    {
        line.Append(key);
        if (value == null)
        {
            line.Append("null");
            return;
        }
        line.Append('"');
        foreach (char c in value)
        {
            if (c == '"' || c == '\\') line.Append('\\').Append(c);
            else if (c < ' ') line.Append("\\u").Append(((int)c).ToString("x4", Invariant));
            else line.Append(c);
        }
        line.Append('"');
    }
}
