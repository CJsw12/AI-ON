using System;

public sealed class FrameTimingStats
{
    readonly double[] values;
    int next, count;
    double sum;
    public long FrameCount { get; private set; }
    public int Count => count;
    public double Average => count == 0 ? 0 : sum / count;
    public double Maximum { get { if (count == 0) return 0; double max = values[0]; for (int i = 1; i < count; i++) max = Math.Max(max, values[i]); return max; } }
    public double Median { get { if (count == 0) return 0; var copy = new double[count]; Array.Copy(values, copy, count); Array.Sort(copy); return count % 2 == 1 ? copy[count / 2] : (copy[count / 2 - 1] + copy[count / 2]) / 2; } }
    public double Percentile95 { get { if (count == 0) return 0; var copy = new double[count]; Array.Copy(values, copy, count); Array.Sort(copy); return copy[(int)Math.Ceiling(0.95 * count) - 1]; } }
    public FrameTimingStats(int capacity) { if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity)); values = new double[capacity]; }
    public void Add(double milliseconds) { if (count == values.Length) sum -= values[next]; else count++; values[next] = milliseconds; sum += milliseconds; next = (next + 1) % values.Length; FrameCount++; }
}
