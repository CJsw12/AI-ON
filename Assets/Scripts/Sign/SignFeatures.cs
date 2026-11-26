using System;
public static class SignHandedness
{
    public const bool DefaultLabelsAreMirrored = false;
    public static bool TryIsLeft(string raw, bool mirrored, out bool isLeft) { isLeft = false; if (raw != "Left" && raw != "Right") return false; isLeft = (raw == "Left") != mirrored; return true; }
    public static bool IsLeft(string rawLabel, bool labelsAreMirrored = DefaultLabelsAreMirrored)
    {
        if (rawLabel != "Left" && rawLabel != "Right") throw new ArgumentException("잘못된 손 라벨");
        return (rawLabel == "Left") != labelsAreMirrored;
    }
}
public static class SignFeatures
{
    public const int FeatureCount = 57;
    public const float MinimumPalmSize = 10f;
    public static bool TryCompute(float[] landmarks63, int width, int height, bool isLeft, out float[] features, float minimumPalmSize = MinimumPalmSize)
    {
        features = null;
        if (float.IsNaN(minimumPalmSize) || float.IsInfinity(minimumPalmSize) || minimumPalmSize <= 0 || landmarks63 == null || landmarks63.Length != 63 || width <= 0 || height <= 0) return false;
        foreach (float v in landmarks63) if (float.IsNaN(v) || float.IsInfinity(v)) return false;
        double[] x = new double[21], y = new double[21];
        for (int i = 0; i < 21; i++) { x[i] = ((double)landmarks63[3*i] - landmarks63[0]) * width * (isLeft ? -1 : 1); y[i] = ((double)landmarks63[3*i+1] - landmarks63[1]) * height; }
        double scale = Math.Sqrt(x[9]*x[9] + y[9]*y[9]);
        if (scale < minimumPalmSize || (double.IsInfinity(scale) || double.IsNaN(scale))) return false;
        float[] result = new float[FeatureCount];
        for (int i = 0; i < 21; i++) { x[i] /= scale; y[i] /= scale; result[2*i] = (float)x[i]; result[2*i+1] = (float)y[i]; }
        int n = 42;
        for (int finger = 0; finger < 5; finger++) { int start = 1 + finger*4; for (int j = 0; j < 3; j++) { int a = j == 0 ? 0 : start+j-1, b = start+j, c = start+j+1; double ax=x[a]-x[b], ay=y[a]-y[b], bx=x[c]-x[b], by=y[c]-y[b]; double la=Math.Sqrt(ax*ax+ay*ay), lb=Math.Sqrt(bx*bx+by*by); double angle = la < 1e-6 || lb < 1e-6 ? Math.PI : Math.Acos(Math.Max(-1,Math.Min(1,(ax*bx+ay*by)/(la*lb)))); result[n++]=(float)angle; } }
        features = result; return true;
    }
}
