using System;
using System.Collections.Generic;

/// <summary>Quality gates for target exposure, fixation samples and held-out screen checks.</summary>
public sealed class IPhoneCalibrationGate
{
    double renderedAt = double.NaN;
    double lastRenderedAt = double.NaN;

    public void Reset() { renderedAt = lastRenderedAt = double.NaN; }

    public void MarkRendered(double now)
    {
        if (double.IsNaN(renderedAt) && Finite(now)) renderedAt = now;
        if (Finite(now)) lastRenderedAt = now;
    }

    public bool CanCapture(double now, double settleSeconds)
    {
        return Finite(renderedAt) && Finite(now) && settleSeconds >= 0 &&
            now - renderedAt >= settleSeconds && now - lastRenderedAt >= 0 && now - lastRenderedAt <= 2;
    }

    // Direct UDP eye rotation is in degrees. The USB API uses different units and must not use this gate.
    // A median tolerates brief spikes,
    // but it must not conceal a saccade spanning the collection window.
    public static bool Stable(IList<double[]> samples)
    {
        if (samples == null || samples.Count < 20) return false;
        for (int axis = 0; axis < 2; axis++)
        {
            var all = new List<double>();
            var first = new List<double>();
            var last = new List<double>();
            for (int i = 0; i < samples.Count; i++)
            {
                double[] sample = samples[i];
                if (sample == null || sample.Length < 2) return false;
                foreach (double value in sample) if (!Finite(value)) return false;
                double v = sample[axis];
                all.Add(v);
                (i < samples.Count / 2 ? first : last).Add(v);
            }
            all.Sort(); first.Sort(); last.Sort();
            if (all[(all.Count - 1) * 9 / 10] - all[(all.Count - 1) / 10] > 3.0) return false;
            if (Math.Abs(first[first.Count / 2] - last[last.Count / 2]) > 1.5) return false;
        }
        return true;
    }

    // Canvas pixels (1280x900). Keep validation points independent of training.
    public static bool Accurate(double mean, double p95)
    {
        return Finite(mean) && Finite(p95) && mean >= 0 && p95 >= 0 && mean <= 160 && p95 <= 240;
    }

    static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
}
