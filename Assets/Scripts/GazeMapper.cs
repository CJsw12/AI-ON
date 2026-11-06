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
        EyeX2 = 6, EyeY2 = 7, EyeXY = 8, EyeX3 = 9, EyeY3 = 10, PosX = 11, PosY = 12;
    const double Ridge = .005;
    const double MinSpread = 1e-6;
    const float ExtraInputPenalty = .08f;          // prefer simpler fits unless extra features clearly help
    // A model with three or more inputs, or no eye input, must beat the best simple eye model by this much.
    // Thirteen points are few, so without it each calibration picked a different complex model and tracking
    // was good once and odd the next three times.
    const float SwitchMargin = .3f;
    /// <summary>Head-position models are experimental: off until tested on a device (see the iPhone handoff doc).</summary>
    public static bool UsePosition = false;
    const float DriftRate = .1f;                   // conservative correction from an inferred game target
    const float MaxDrift = .1f;                    // the offset never exceeds 10% of the screen
    const float MaxAnchorMiss = .2f;               // a bigger miss is more likely a wrong anchor than drift

    static readonly int[][] ModelsX =
    {
        new[] { EyeX }, new[] { HeadX }, new[] { LookX }, new[] { EyeX, HeadX }, new[] { EyeX, LookX }, new[] { HeadX, LookX },
        new[] { EyeX, HeadX, LookX }, new[] { EyeX, EyeX2, EyeXY }, new[] { EyeX, EyeX3 }, new[] { EyeX, HeadX, EyeX3 },
        // One side stretched more than the other (x² + x³), and an edge stretch helped by the eye-shape cue.
        new[] { EyeX, EyeX2, EyeX3 }, new[] { EyeX, LookX, EyeX3 },
        // Head position: the same screen point is seen at another eye angle once the head slides sideways.
        new[] { EyeX, HeadX, PosX }, new[] { EyeX, LookX, PosX }, new[] { EyeX, HeadX, LookX, PosX },
    };
    static readonly int[][] ModelsY =
    {
        new[] { EyeY }, new[] { HeadY }, new[] { LookY }, new[] { EyeY, HeadY }, new[] { EyeY, LookY }, new[] { HeadY, LookY },
        new[] { EyeY, HeadY, LookY }, new[] { EyeY, EyeY2, EyeXY }, new[] { EyeY, EyeY3 }, new[] { EyeY, HeadY, EyeY3 },
        // Only the bottom squashed (y² + y³: a laptop camera sits above the screen), and edge stretch + eyelid cue.
        new[] { EyeY, EyeY2, EyeY3 }, new[] { EyeY, LookY, EyeY3 },
        // Head position (up/down slide); only chosen when the calibration actually saw the head move.
        new[] { EyeY, HeadY, PosY }, new[] { EyeY, LookY, PosY }, new[] { HeadY, LookY, PosY }, new[] { EyeY, HeadY, LookY, PosY },
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

    /// <summary>pos: head position (x = sideways, y = up/down); zero when the source doesn't send it, and then the
    /// position models are skipped because the input never moved.</summary>
    // Webcam extras appended after Features() (GameController.WebcamFit): ResNet-34 gaze yaw/pitch (radians,
    // running median of 3), head x/y/distance (metres, MediaPipe transform), iris offset in the eye (averaged
    // over both eyes), eyeLook blendshapes, head yaw/pitch (radians) and the ResNet gaze ray's screen-plane point.
    public const int WebResYaw = 13, WebResPitch = 14, WebHeadX = 15, WebHeadY = 16, WebDistance = 17, WebIrisX = 18,
        WebIrisY = 19, WebShapeX = 20, WebShapeY = 21, WebHeadYaw = 22, WebHeadPitch = 23, WebPlaneX = 24, WebPlaneY = 25,
        WebFeatureCount = 26;

    public static double[] Features(Vector2 eye, Vector2 head, Vector2 look = default, Vector2 pos = default) => new double[]
    {
        eye.x, eye.y, head.x, head.y, look.x, look.y,
        eye.x * eye.x, eye.y * eye.y, eye.x * eye.y, eye.x * eye.x * eye.x, eye.y * eye.y * eye.y,
        pos.x, pos.y,
    };

    public void Reset()
    {
        axisX = axisY = null; ErrorPixels = 0; ModelName = ""; Drift = Vector2.zero; CheckPixels = -1;
        fitFeatures.Clear(); fitTargets.Clear(); clickFeatures.Clear(); clickTargets.Clear(); checkFeatures.Clear(); checkTargets.Clear(); CheckP95 = -1; ClicksRejectedByGuard = 0; AnchorClicks = 0;
    }

    /// <summary>Mean error in canvas pixels on check points that were not used for the fit; -1 until measured.
    /// Unlike ErrorPixels this is a real measurement, not an estimate from the calibration points.</summary>
    public float CheckPixels { get; private set; } = -1;
    /// <summary>95th-percentile miss over the independent check points only (px); -1 before a check.</summary>
    public float CheckP95 { get; private set; } = -1;
    public int ClicksRejectedByGuard { get; private set; }
    readonly List<double[]> checkFeatures = new List<double[]>();
    readonly List<Vector2> checkTargets = new List<Vector2>();

    public float Check(IList<double[]> features, IList<Vector2> targets)
    {
        if (!Ready) return CheckPixels = -1;
        checkFeatures.Clear(); checkTargets.Clear();
        for (int i = 0; i < features.Count; i++) if (features[i] != null) { checkFeatures.Add(features[i]); checkTargets.Add(targets[i]); }
        CheckP95 = Percentile95(Sub(HeldOutMisses(), checkFeatures.Count));
        float sum = 0; int n = 0;
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] == null) continue;
            Vector2 miss = PredictUnclamped(features[i]) - targets[i];
            sum += new Vector2(miss.x * 1280f, miss.y * 900f).magnitude; n++;
        }
        return CheckPixels = n == 0 ? -1 : sum / n;
    }

    // Saved calibration: "x;y" axes, each "used|mean|spread|weight|error" with comma-separated numbers.
    public string Save()
    {
        if (!Ready) return "";
        string Join(double[] v) => string.Join(",", Array.ConvertAll(v, d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        string Axis(Axis a) => string.Join(",", a.Used) + "|" + Join(a.Mean) + "|" + Join(a.Spread) + "|" + Join(a.Weight) + "|" + a.Error.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return Axis(axisX) + ";" + Axis(axisY) + ";" + CheckPixels.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool Load(string text)
    {
        try
        {
            string[] parts = (text ?? "").Split(';');
            if (parts.Length != 3) return false;
            double[] Nums(string s) => s == "" ? new double[0] : Array.ConvertAll(s.Split(','), v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture));
            Axis Read(string s)
            {
                string[] f = s.Split('|');
                return new Axis { Used = Array.ConvertAll(f[0].Split(','), int.Parse), Mean = Nums(f[1]), Spread = Nums(f[2]), Weight = Nums(f[3]), Error = float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture) };
            }
            Axis x = Read(parts[0]), y = Read(parts[1]);
            // Feature layout changed since the save: drop it rather than predict nonsense.
            foreach (int u in x.Used) if (u >= Features(Vector2.zero, Vector2.zero).Length) return false;
            foreach (int u in y.Used) if (u >= Features(Vector2.zero, Vector2.zero).Length) return false;
            fitFeatures.Clear(); fitTargets.Clear(); clickFeatures.Clear(); clickTargets.Clear(); checkFeatures.Clear(); checkTargets.Clear(); CheckP95 = -1; ClicksRejectedByGuard = 0; AnchorClicks = 0;
            axisX = x; axisY = y; Drift = Vector2.zero;
            ErrorPixels = Mathf.Sqrt(x.Error * x.Error + y.Error * y.Error);
            ModelName = $"가로 {Describe(x.Used)} · 세로 {Describe(y.Used)}";
            CheckPixels = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception) { Reset(); return false; }
    }

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
    /// <param name="fixedX">Use exactly this model for the horizontal axis instead of choosing one from the points
    /// (the webcam does: it was chosen offline by how well it carries over to other postures, which a fit's own
    /// points cannot tell). Same for <paramref name="fixedY"/>.</param>
    public bool Fit(IList<double[]> features, IList<Vector2> targets, IList<List<double[]>> samples = null, bool requireVerticalEye = false,
        int[] fixedX = null, int[] fixedY = null)
    {
        Reset();
        var tx = new double[targets.Count];
        var ty = new double[targets.Count];
        for (int i = 0; i < targets.Count; i++) { tx[i] = targets[i].x; ty[i] = targets[i].y; }
        Axis x = FitAxis(fixedX != null ? new[] { fixedX } : ModelsX, features, tx, samples, 1280f);
        Axis y = FitAxis(fixedY != null ? new[] { fixedY } : ModelsY, features, ty, samples, 900f, fixedY == null && requireVerticalEye);
        if (x == null || y == null) return false;
        axisX = x;
        axisY = y;
        ErrorPixels = Mathf.Sqrt(x.Error * x.Error + y.Error * y.Error);
        ModelName = $"가로 {Describe(x.Used)} · 세로 {Describe(y.Used)}";
        for (int i = 0; i < features.Count; i++)
            if (features[i] != null) { fitFeatures.Add(features[i]); fitTargets.Add(targets[i]); }
        return true;
    }

    // ---- Learning from clicks (WebGazer / Tri-Cam idea) ----------------------------------------------
    // A player looks at what they click. Each accepted click becomes an extra, lightly weighted calibration
    // point and the chosen model is re-solved; the pointer itself is never moved toward the cursor.
    const int MaxClicks = 30;            // oldest clicks drop out, so a posture change is followed
    const double ClickWeight = .35;      // a click counts about a third of a calibration point
    const float MaxClickMissPixels = 250f;
    readonly List<double[]> fitFeatures = new List<double[]>();
    readonly List<Vector2> fitTargets = new List<Vector2>();
    readonly List<double[]> clickFeatures = new List<double[]>();
    readonly List<Vector2> clickTargets = new List<Vector2>();
    public int ClickCount => clickFeatures.Count + AnchorClicks;
    int AnchorClicks;
    const float MaxAnchorStepPixels = 120f;

    // Misses on the labelled points a click refit must not damage: check points first, then the fixed
    // calibration points. Used to refuse a click that makes the mapping worse.
    List<float> HeldOutMisses()
    {
        var misses = new List<float>();
        for (int i = 0; i < checkFeatures.Count; i++) misses.Add(Miss(checkFeatures[i], checkTargets[i]));
        for (int i = 0; i < fitFeatures.Count; i++) misses.Add(Miss(fitFeatures[i], fitTargets[i]));
        return misses;
    }
    float Miss(double[] f, Vector2 t) { Vector2 m = PredictUnclamped(f) - t; return new Vector2(m.x * 1280f, m.y * 900f).magnitude; }
    static List<float> Sub(List<float> v, int n) => v.GetRange(0, Mathf.Min(n, v.Count));
    static float Mean(List<float> v) { float s = 0; foreach (float x in v) s += x; return v.Count == 0 ? 0 : s / v.Count; }
    static float Percentile95(List<float> v)
    {
        if (v.Count == 0) return -1;
        var c = new List<float>(v); c.Sort();
        return c[Mathf.Clamp(Mathf.CeilToInt(.95f * c.Count) - 1, 0, c.Count - 1)];
    }

    /// <summary>Adds a click (normalized target) seen with these features; false when it was rejected.</summary>
    public bool AddClick(double[] features, Vector2 target)
    {
        if (!Ready || features == null) return false;
        foreach (double f in features) if (double.IsNaN(f) || double.IsInfinity(f)) return false;
        Vector2 miss = PredictUnclamped(features) - target;
        if (new Vector2(miss.x * 1280f, miss.y * 900f).magnitude > MaxClickMissPixels) { ClicksRejectedByGuard++; return false; }   // probably not looking there
        if (fitFeatures.Count == 0)
        {
            // Loaded calibration: no labelled points to refit or to guard with, so only nudge the offset,
            // and refuse a click that alone would move it more than MaxAnchorStepPixels.
            if (new Vector2(miss.x * 1280f, miss.y * 900f).magnitude > MaxAnchorStepPixels) { ClicksRejectedByGuard++; return false; }
            AddAnchor(-miss); AnchorClicks++; return true;
        }
        clickFeatures.Add(features); clickTargets.Add(target);
        double[] droppedFeatures = null; Vector2 droppedTarget = default;
        if (clickFeatures.Count > MaxClicks) { droppedFeatures = clickFeatures[0]; droppedTarget = clickTargets[0]; clickFeatures.RemoveAt(0); clickTargets.RemoveAt(0); }
        void Undo()
        {
            clickFeatures.RemoveAt(clickFeatures.Count - 1); clickTargets.RemoveAt(clickTargets.Count - 1);
            if (droppedFeatures != null) { clickFeatures.Insert(0, droppedFeatures); clickTargets.Insert(0, droppedTarget); }
        }

        var all = new List<double[]>(fitFeatures); all.AddRange(clickFeatures);
        int n = all.Count;
        var tx = new double[n]; var ty = new double[n]; var w = new double[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 t = i < fitTargets.Count ? fitTargets[i] : clickTargets[i - fitTargets.Count];
            tx[i] = t.x; ty[i] = t.y; w[i] = i < fitTargets.Count ? 1 : ClickWeight;
        }
        Axis x = Solve(axisX.Used, all, tx, -1, w), y = Solve(axisY.Used, all, ty, -1, w);
        if (x == null || y == null) { Undo(); return false; }
        x.Error = axisX.Error; y.Error = axisY.Error;
        float before = Mean(HeldOutMisses());
        Axis oldX = axisX, oldY = axisY; Vector2 oldDrift = Drift;
        axisX = x; axisY = y; Drift = Vector2.zero;   // the refit now carries the offset the drift used to patch
        // Rollback guard: a click must not make the held-out points clearly worse. The slack (20% / 8px) is wide on
        // purpose: those points were recorded in the calibration posture, so a real posture drift that clicks are
        // correcting will cost them a little.
        if (Mean(HeldOutMisses()) > Mathf.Max(before * 1.2f, before + 8f))
        {
            axisX = oldX; axisY = oldY; Drift = oldDrift;
            Undo();
            ClicksRejectedByGuard++;
            return false;
        }
        if (checkFeatures.Count > 0) { CheckPixels = Mean(Sub(HeldOutMisses(), checkFeatures.Count)); CheckP95 = Percentile95(Sub(HeldOutMisses(), checkFeatures.Count)); }
        return true;
    }

    static string Describe(int[] used)
    {
        var parts = new List<string>();
        foreach (int f in used)
        {
            string name = f == EyeX || f == EyeY ? "눈" : f == HeadX || f == HeadY || f == WebHeadYaw || f == WebHeadPitch ? "고개"
                : f == LookX || f == LookY || f == WebShapeX || f == WebShapeY ? "눈모양" : f == PosX || f == PosY || f == WebHeadX || f == WebHeadY || f == WebDistance ? "머리위치"
                : f == WebIrisX || f == WebIrisY ? "홍채" : f == WebResYaw || f == WebResPitch || f == WebPlaneX || f == WebPlaneY ? "얼굴방향" : "곡선";
            if (!parts.Contains(name)) parts.Add(name);
        }
        return string.Join("+", parts);
    }

    /// <summary>Normalized point (0..1, y down), clamped to the screen.</summary>
    public Vector2 Predict(double[] features)
    {
        var p = PredictUnclamped(features);
        return new Vector2(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y));
    }

    // Diagnostics need the value before screen clamping to distinguish saturation from missing input.
    public Vector2 PredictUnclamped(double[] features) => !Ready ? new Vector2(.5f, .5f) :
        new Vector2((float)axisX.Predict(features), (float)axisY.Predict(features)) + Drift;

    static Axis FitAxis(int[][] models, IList<double[]> features, double[] targets, IList<List<double[]>> samples, float pixels, bool requireEye = false)
    {
        Axis best = null, simple = null;
        float bestScore = float.MaxValue, simpleScore = float.MaxValue;
        foreach (int[] model in models)
        {
            if (requireEye && Array.IndexOf(model, EyeY) < 0 && Array.IndexOf(model, LookY) < 0) continue;
            if (!UsePosition && (Array.IndexOf(model, PosX) >= 0 || Array.IndexOf(model, PosY) >= 0)) continue;
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
            axis.Error = error;
            if (score < bestScore) { bestScore = score; best = axis; }
            if (IsSimple(model) && score < simpleScore) { simpleScore = score; simple = axis; }
        }
        // Stay with the simple eye model unless the complex one is clearly better.
        if (simple != null && best != simple && bestScore > simpleScore * (1f - SwitchMargin)) return simple;
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
    static Axis Solve(int[] model, IList<double[]> features, double[] targets, int skip, double[] weights = null)
    {
        int k = model.Length, count = 0;
        double n = 0;
        var mean = new double[k];
        var spread = new double[k];
        for (int p = 0; p < features.Count; p++)
        {
            if (p == skip) continue;
            double wp = weights == null ? 1 : weights[p];
            count++; n += wp;
            for (int i = 0; i < k; i++) mean[i] += wp * features[p][model[i]];
        }
        if (count < k + 3) return null;
        for (int i = 0; i < k; i++) mean[i] /= n;
        for (int p = 0; p < features.Count; p++)
        {
            if (p == skip) continue;
            double wp = weights == null ? 1 : weights[p];
            for (int i = 0; i < k; i++) { double d = features[p][model[i]] - mean[i]; spread[i] += wp * d * d; }
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
            double wp = weights == null ? 1 : weights[p];
            for (int i = 0; i < k; i++) row[i] = (features[p][model[i]] - mean[i]) / spread[i];
            row[k] = 1;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++) normal[r, c] += wp * row[r] * row[c];
                right[r] += wp * row[r] * targets[p];
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
    // One or two inputs, at least one of them an eye signal (rotation or eye shape).
    static bool IsSimple(int[] model)
    {
        if (model.Length > 2) return false;
        foreach (int f in model) if (f == EyeX || f == EyeY || f == LookX || f == LookY) return true;
        return false;
    }

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
    readonly float minCutoff, beta, derivativeCutoff, holdRadius;
    Vector2 value, velocity, held;
    float lastTime;
    bool started;

    /// <param name="holdRadius">Fixation hold in canvas pixels: while the gaze wobbles inside this circle the pointer
    /// stays still, and once it leaves the pointer is pulled along at the circle's edge. 0 turns it off.</param>
    public GazeSmoother(float minCutoff = .5f, float beta = .004f, float derivativeCutoff = 1f, float holdRadius = 0f)
    {
        this.holdRadius = holdRadius;
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
            held = value;
            return value;
        }
        float dt = Mathf.Max(time - lastTime, 1e-3f);
        lastTime = time;
        Vector2 rawVelocity = (point - value) / dt;
        velocity = Vector2.Lerp(velocity, rawVelocity, Alpha(derivativeCutoff, dt));
        float cutoff = minCutoff + beta * velocity.magnitude;
        value = Vector2.Lerp(value, point, Alpha(cutoff, dt));
        if (holdRadius <= 0) return value;
        // Small wobble while fixating is tracker noise, not eye movement: keep the pointer still.
        Vector2 off = value - held;
        float distance = off.magnitude;
        if (distance > holdRadius) held += off * (1f - holdRadius / distance);
        return held;
    }

    static float Alpha(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * cutoff);
        return 1f / (1f + tau / dt);
    }
}
