using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps iPhone gaze signals to a normalized screen point after calibration.
///
/// Inputs per frame: eye rotation, head rotation, and the ARKit "look" blendshapes (EyeLookUp/Down/In/Out), which
/// come from the eyelid and iris shape and keep responding when looking down, where the eyelid hides the iris and
/// eye rotation shrinks. Horizontal and vertical are fitted separately: each axis tries a small set of input
/// combinations (eye, head, eye shape, and curved eye terms for trackers that under-report large eye turns) and keeps
/// the one that best predicts calibration points it did not see (leave-one-out) plus single-frame scatter.
/// Inputs are standardized and lightly ridge-regularized, so one that barely moved can't blow up the fit.
/// After calibration, AddAnchor() shifts the result by known misses (online drift correction).
/// </summary>
public sealed class GazeMapper
{
    // Feature layout produced by Features().
    const int EyeX = 0, EyeY = 1, HeadX = 2, HeadY = 3, LookX = 4, LookY = 5,
        EyeX2 = 6, EyeY2 = 7, EyeXY = 8, EyeX3 = 9, EyeY3 = 10;
    const double Ridge = .005;
    const double MinSpread = 1e-6;
    const float ExtraInputPenalty = .08f;          // prefer simpler fits unless extra features clearly help
    const float DriftRate = .15f;                  // conservative correction from an inferred game target
    const float MaxDrift = .15f;                   // the offset never exceeds 15% of the screen
    const float MaxAnchorMiss = .2f;               // a bigger miss is more likely a wrong anchor than drift

    static readonly int[][] ModelsX =
    {
        new[] { EyeX }, new[] { HeadX }, new[] { LookX }, new[] { EyeX, HeadX }, new[] { EyeX, LookX }, new[] { HeadX, LookX },
        new[] { EyeX, HeadX, LookX }, new[] { EyeX, EyeX2, EyeXY }, new[] { EyeX, EyeX3 }, new[] { EyeX, HeadX, EyeX3 },
        // One side stretched more than the other (x² + x³), and an edge stretch helped by the eye-shape cue.
        new[] { EyeX, EyeX2, EyeX3 }, new[] { EyeX, LookX, EyeX3 },
    };
    static readonly int[][] ModelsY =
    {
        new[] { EyeY }, new[] { HeadY }, new[] { LookY }, new[] { EyeY, HeadY }, new[] { EyeY, LookY }, new[] { HeadY, LookY },
        new[] { EyeY, HeadY, LookY }, new[] { EyeY, EyeY2, EyeXY }, new[] { EyeY, EyeY3 }, new[] { EyeY, HeadY, EyeY3 },
        // Only the bottom squashed (y² + y³: a laptop camera sits above the screen), and edge stretch + eyelid cue.
        new[] { EyeY, EyeY2, EyeY3 }, new[] { EyeY, LookY, EyeY3 },
    };

    sealed class Axis
    {
        public int[] Used;
        public double[] Mean, Spread, Weight;
        public float Error;
        public double Predict(double[] f)
        {
            double v = Weight[Used.Length];
            for (int i = 0; i < Used.Length; i++) v += Weight[i] * (f[Used[i]] - Mean[i]) / Spread[i];
            return v;
        }
    }

    Axis axisX, axisY;

    public bool Ready => axisX != null && axisY != null;
    /// <summary>
    /// Expected pointer error in canvas pixels (1280×900): leave-one-out error on the calibration points combined
    /// with how far single frames scatter, since the live pointer sees single frames, not medians.
    /// </summary>
    public float ErrorPixels { get; private set; }
    public string ModelName { get; private set; } = "";
    /// <summary>Online drift correction in normalized screen units, added to every prediction.</summary>
    public Vector2 Drift { get; private set; }

    public static double[] Features(Vector2 eye, Vector2 head, Vector2 look = default) => new double[]
    {
        eye.x, eye.y, head.x, head.y, look.x, look.y,
        eye.x * eye.x, eye.y * eye.y, eye.x * eye.y, eye.x * eye.x * eye.x, eye.y * eye.y * eye.y,
    };

    public void Reset() { axisX = axisY = null; ErrorPixels = 0; ModelName = ""; Drift = Vector2.zero; }

    /// <summary>
    /// The player was certainly looking at a known spot and the pointer missed it by <paramref name="miss"/>
    /// (target − pointer, normalized). Moves the drift offset part of the way; a miss too large to be drift is ignored.
    /// </summary>
    public void AddAnchor(Vector2 miss)
    {
        if (!Ready || miss.magnitude > MaxAnchorMiss) return;
        Vector2 d = Drift + miss * DriftRate;
        Drift = new Vector2(Mathf.Clamp(d.x, -MaxDrift, MaxDrift), Mathf.Clamp(d.y, -MaxDrift, MaxDrift));
    }

    /// <param name="features">One median feature vector per calibration point.</param>
    /// <param name="samples">Optional single-frame samples per point; they make the error estimate and model choice reflect noise.</param>
    public bool Fit(IList<double[]> features, IList<Vector2> targets, IList<List<double[]>> samples = null)
    {
        Reset();
        var tx = new double[targets.Count];
        var ty = new double[targets.Count];
        for (int i = 0; i < targets.Count; i++) { tx[i] = targets[i].x; ty[i] = targets[i].y; }
        Axis x = FitAxis(ModelsX, features, tx, samples, 1280f);
        Axis y = FitAxis(ModelsY, features, ty, samples, 900f);
        if (x == null || y == null) return false;
        axisX = x;
        axisY = y;
        ErrorPixels = Mathf.Sqrt(x.Error * x.Error + y.Error * y.Error);
        ModelName = $"가로 {Describe(x.Used)} · 세로 {Describe(y.Used)}";
        return true;
    }

    static string Describe(int[] used)
    {
        var parts = new List<string>();
        foreach (int f in used)
        {
            string name = f == EyeX || f == EyeY ? "눈" : f == HeadX || f == HeadY ? "고개" : f == LookX || f == LookY ? "눈모양" : "곡선";
            if (!parts.Contains(name)) parts.Add(name);
        }
        return string.Join("+", parts);
    }

    /// <summary>Normalized point (0..1, y down), clamped to the screen.</summary>
    public Vector2 Predict(double[] features)
    {
        if (!Ready) return new Vector2(.5f, .5f);
        var p = new Vector2((float)axisX.Predict(features), (float)axisY.Predict(features)) + Drift;
        return new Vector2(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y));
    }

    static Axis FitAxis(int[][] models, IList<double[]> features, double[] targets, IList<List<double[]>> samples, float pixels)
    {
        Axis best = null;
        float bestScore = float.MaxValue;
        foreach (int[] model in models)
        {
            float loo = LeaveOneOutError(model, features, targets, pixels);
            if (float.IsNaN(loo)) continue;
            Axis axis = Solve(model, features, targets, -1);
            if (axis == null) continue;
            float error = loo;
            if (samples != null)
            {
                float scatter = SampleScatter(axis, samples, pixels);
                error = Mathf.Sqrt(loo * loo + scatter * scatter);
            }
            float score = error * (1f + ExtraInputPenalty * (model.Length - 1));
            if (score < bestScore)
            {
                bestScore = score;
                axis.Error = error;
                best = axis;
            }
        }
        return best;
    }

    // RMS distance of single frames from their point's mean prediction, along one axis (the noise part, not the bias).
    static float SampleScatter(Axis axis, IList<List<double[]>> samples, float pixels)
    {
        double sum = 0;
        int n = 0;
        foreach (List<double[]> point in samples)
        {
            if (point == null || point.Count == 0) continue;
            var values = new double[point.Count];
            double centre = 0;
            for (int i = 0; i < point.Count; i++) { values[i] = axis.Predict(point[i]); centre += values[i]; }
            centre /= point.Count;
            foreach (double v in values) { double d = (v - centre) * pixels; sum += d * d; n++; }
        }
        return n == 0 ? 0f : (float)Math.Sqrt(sum / n);
    }

    static float LeaveOneOutError(int[] model, IList<double[]> features, double[] targets, float pixels)
    {
        double sum = 0;
        for (int skip = 0; skip < features.Count; skip++)
        {
            Axis axis = Solve(model, features, targets, skip);
            if (axis == null) return float.NaN;
            double d = (axis.Predict(features[skip]) - targets[skip]) * pixels;
            sum += d * d;
        }
        return (float)Math.Sqrt(sum / features.Count);
    }

    // Ridge least squares on standardized features; the bias term is not penalized. skip = -1 uses every point.
    static Axis Solve(int[] model, IList<double[]> features, double[] targets, int skip)
    {
        int k = model.Length, n = 0;
        var mean = new double[k];
        var spread = new double[k];
        for (int p = 0; p < features.Count; p++)
        {
            if (p == skip) continue;
            n++;
            for (int i = 0; i < k; i++) mean[i] += features[p][model[i]];
        }
        if (n < k + 3) return null;
        for (int i = 0; i < k; i++) mean[i] /= n;
        for (int p = 0; p < features.Count; p++)
        {
            if (p == skip) continue;
            for (int i = 0; i < k; i++) { double d = features[p][model[i]] - mean[i]; spread[i] += d * d; }
        }
        for (int i = 0; i < k; i++)
        {
            spread[i] = Math.Sqrt(spread[i] / n);
            if (spread[i] < MinSpread) return null;   // this input never moved (or the source doesn't send it)
        }

        int size = k + 1;
        var normal = new double[size, size];
        var right = new double[size];
        var row = new double[size];
        for (int p = 0; p < features.Count; p++)
        {
            if (p == skip) continue;
            for (int i = 0; i < k; i++) row[i] = (features[p][model[i]] - mean[i]) / spread[i];
            row[k] = 1;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++) normal[r, c] += row[r] * row[c];
                right[r] += row[r] * targets[p];
            }
        }
        for (int i = 0; i < k; i++) normal[i, i] += Ridge * n;
        if (!SolveLinear(normal, right, out double[] weight)) return null;
        return new Axis { Used = model, Mean = mean, Spread = spread, Weight = weight };
    }

    static bool SolveLinear(double[,] a, double[] b, out double[] x)
    {
        int n = b.Length;
        var m = new double[n, n + 1];
        for (int r = 0; r < n; r++) { for (int c = 0; c < n; c++) m[r, c] = a[r, c]; m[r, n] = b[r]; }
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int r = col + 1; r < n; r++) if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
            if (Math.Abs(m[pivot, col]) < 1e-12) { x = null; return false; }
            if (pivot != col) for (int c = col; c <= n; c++) { double t = m[col, c]; m[col, c] = m[pivot, c]; m[pivot, c] = t; }
            double div = m[col, col];
            for (int c = col; c <= n; c++) m[col, c] /= div;
            for (int r = 0; r < n; r++)
            {
                if (r == col) continue;
                double f = m[r, col];
                if (f != 0) for (int c = col; c <= n; c++) m[r, c] -= f * m[col, c];
            }
        }
        x = new double[n];
        for (int r = 0; r < n; r++) x[r] = m[r, n];
        return true;
    }

    /// <summary>Per-dimension median of the samples taken at one calibration point (ignores blinks and glances).</summary>
    public static double[] Median(List<double[]> samples)
    {
        int count = samples[0].Length;
        var result = new double[count];
        var column = new double[samples.Count];
        for (int f = 0; f < count; f++)
        {
            for (int i = 0; i < samples.Count; i++) column[i] = samples[i][f];
            Array.Sort(column);
            int mid = column.Length / 2;
            result[f] = column.Length % 2 == 1 ? column[mid] : (column[mid - 1] + column[mid]) * .5;
        }
        return result;
    }
}

/// <summary>
/// One Euro filter (Casiez et al. 2012): heavy smoothing while the gaze rests, little lag while it moves.
/// Values are canvas pixels; time is seconds.
/// </summary>
public sealed class GazeSmoother
{
    readonly float minCutoff, beta, derivativeCutoff;
    Vector2 value, velocity;
    float lastTime;
    bool started;

    public GazeSmoother(float minCutoff = .5f, float beta = .004f, float derivativeCutoff = 1f)
    {
        this.minCutoff = minCutoff;
        this.beta = beta;
        this.derivativeCutoff = derivativeCutoff;
    }

    public void Reset() => started = false;

    public Vector2 Filter(Vector2 point, float time)
    {
        if (!started || time - lastTime > .5f)
        {
            started = true;
            value = point;
            velocity = Vector2.zero;
            lastTime = time;
            return value;
        }
        float dt = Mathf.Max(time - lastTime, 1e-3f);
        lastTime = time;
        Vector2 rawVelocity = (point - value) / dt;
        velocity = Vector2.Lerp(velocity, rawVelocity, Alpha(derivativeCutoff, dt));
        float cutoff = minCutoff + beta * velocity.magnitude;
        value = Vector2.Lerp(value, point, Alpha(cutoff, dt));
        return value;
    }

    static float Alpha(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * cutoff);
        return 1f / (1f + tau / dt);
    }
}
