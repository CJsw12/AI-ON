using System;

// 좌우 판정이 뒤집혀도(x 반전) 값이 바뀌지 않는 각도, 거리, 방향 구간만 쓴다.
public static class SignRuleClassifier
{
    public const float ExtendedDegrees = 470f;
    public const float BentDegrees = 420f;
    public const float RMaxIndexMiddleDegrees = 18f;
    public const float SMaxThumbSpreadDegrees = 70f;
    public static readonly string[] Letters = { "ㅂ", "ㅇ", "ㅎ", "ㄹ", "ㅅ" };

    private const double RadiansToDegrees = 180.0 / Math.PI;

    private enum FingerState { Extended, Bent, InBetween }
    private enum HandDirection { Up, Down, Side }

    public static string Classify(float[] features)
    {
        if (features == null || features.Length != SignFeatures.FeatureCount)
            return null;

        foreach (float feature in features)
        {
            if (float.IsNaN(feature) || float.IsInfinity(feature))
                return null;
        }

        FingerState thumb = State(features, 0);
        FingerState index = State(features, 1);
        FingerState middle = State(features, 2);
        FingerState ring = State(features, 3);
        FingerState pinky = State(features, 4);
        HandDirection direction = Direction(features);
        string letter = null;
        int matches = 0;

        // 엄지를 제외한 네 손가락을 펴서 위로 향한다.
        if (index == FingerState.Extended && middle == FingerState.Extended &&
            ring == FingerState.Extended && pinky == FingerState.Extended &&
            thumb != FingerState.Extended && direction == HandDirection.Up)
        {
            letter = "ㅂ";
            matches++;
        }

        // 엄지와 검지 끝을 가까이 모으고 나머지 세 손가락을 위로 편다.
        if (middle == FingerState.Extended && ring == FingerState.Extended &&
            pinky == FingerState.Extended && index != FingerState.Extended &&
            direction == HandDirection.Up && TipGap(features, 4, 8) < 0.35)
        {
            letter = "ㅇ";
            matches++;
        }

        // 네 손가락을 접고 엄지 끝을 엄지 시작 관절보다 높이 세운다.
        if (thumb == FingerState.Extended && index == FingerState.Bent &&
            middle == FingerState.Bent && ring == FingerState.Bent &&
            pinky == FingerState.Bent && features[9] < features[5])
        {
            letter = "ㅎ";
            matches++;
        }

        // 검지, 중지, 약지를 펴고 소지를 접은 손이 옆을 향한다.
        if (index == FingerState.Extended && middle == FingerState.Extended &&
            ring == FingerState.Extended && pinky == FingerState.Bent &&
            direction == HandDirection.Side)
        {
            letter = "ㄹ";
            matches++;
        }

        // 검지와 중지를 펴고 약지와 소지를 접은 손이 아래를 향한다.
        if (index == FingerState.Extended && middle == FingerState.Extended &&
            ring == FingerState.Bent && pinky == FingerState.Bent &&
            direction == HandDirection.Down)
        {
            letter = "ㅅ";
            matches++;
        }

        if (matches != 1)
            return null;
        if (letter == "ㄹ" && IndexMiddleGapDegrees(features) >= RMaxIndexMiddleDegrees)
            return null;
        if (letter == "ㅅ" && ThumbSpreadDegrees(features) >= SMaxThumbSpreadDegrees)
            return null;
        return letter;
    }

    public static double IndexMiddleGapDegrees(float[] f) { return AngleBetween(f, 5, 8, 9, 12); }

    public static double ThumbSpreadDegrees(float[] f) { return AngleBetween(f, 5, 4, 5, 8); }

    private static double AngleBetween(float[] f, int aStart, int aEnd, int bStart, int bEnd)
    {
        double ax = (double)f[2 * aEnd] - f[2 * aStart];
        double ay = (double)f[2 * aEnd + 1] - f[2 * aStart + 1];
        double bx = (double)f[2 * bEnd] - f[2 * bStart];
        double by = (double)f[2 * bEnd + 1] - f[2 * bStart + 1];
        double angle = Math.Atan2(ay, ax) - Math.Atan2(by, bx);
        return Math.Abs(Math.Atan2(Math.Sin(angle), Math.Cos(angle))) * RadiansToDegrees;
    }

    private static FingerState State(float[] features, int finger)
    {
        int start = 42 + 3 * finger;
        double degrees = features[start] * RadiansToDegrees +
                         features[start + 1] * RadiansToDegrees +
                         features[start + 2] * RadiansToDegrees;
        if (degrees >= ExtendedDegrees)
            return FingerState.Extended;
        if (degrees <= BentDegrees)
            return FingerState.Bent;
        return FingerState.InBetween;
    }

    private static HandDirection Direction(float[] features)
    {
        double degrees = Math.Atan2(-features[19], features[18]) * RadiansToDegrees;
        if (degrees >= 30 && degrees <= 150)
            return HandDirection.Up;
        if (degrees >= -150 && degrees <= -30)
            return HandDirection.Down;
        return HandDirection.Side;
    }

    private static double TipGap(float[] features, int first, int second)
    {
        double x = (double)features[2 * first] - features[2 * second];
        double y = (double)features[2 * first + 1] - features[2 * second + 1];
        return Math.Sqrt(x * x + y * y);
    }
}
