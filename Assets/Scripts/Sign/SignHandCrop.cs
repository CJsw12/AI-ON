using System;
using System.Globalization;
using System.IO;

// 손 관절 좌표로 손 부분만 잘라 학습용 이미지를 만드는 순수 계산(Unity 의존 없음, Tests/SignHandCrop.Tests.ps1).
// 좌표는 HandLandmarkSource.Landmarks와 같은 기준이다: 0~1, 좌상단 원점, FrameFlipH/V를 적용한 영상 기준.
// Windows PowerShell 5.1의 Add-Type(C# 5)으로도 컴파일되도록 옛 문법만 쓴다.
public static class SignHandCrop
{
    public const int ImageSize = 224, MaxImagesPerTrial = 10, CaptureIntervalMs = 150, StartDelayMs = 300, MinSide = 48;
    public const float DefaultMargin = .25f;

    // 손 전체를 감싸는 정사각형(좌상단 x, y, 한 변 size, 영상 픽셀). 영상 밖으로 나가면 안쪽으로 밀고, 영상보다 크면 짧은 변에 맞춘다.
    public static bool TryRect(float[] landmarks, int width, int height, float margin, out int x, out int y, out int size)
    {
        x = 0; y = 0; size = 0;
        if (landmarks == null || landmarks.Length != 63 || width <= 0 || height <= 0 || float.IsNaN(margin) || float.IsInfinity(margin) || margin < 0) return false;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (int i = 0; i < 21; i++)
        {
            double px = (double)landmarks[3 * i] * width, py = (double)landmarks[3 * i + 1] * height;
            if (double.IsNaN(px) || double.IsInfinity(px) || double.IsNaN(py) || double.IsInfinity(py)) return false;
            minX = Math.Min(minX, px); maxX = Math.Max(maxX, px); minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
        }
        double side = Math.Max(maxX - minX, maxY - minY) * (1 + 2 * margin);
        if (side < MinSide) return false;
        side = Math.Min(side, Math.Min(width, height));
        double left = Math.Max(0, Math.Min((minX + maxX) / 2 - side / 2, width - side));
        double top = Math.Max(0, Math.Min((minY + maxY) / 2 - side / 2, height - side));
        size = (int)Math.Round(side);
        x = Math.Max(0, Math.Min((int)Math.Round(left), width - size));
        y = Math.Max(0, Math.Min((int)Math.Round(top), height - size));
        return size >= MinSide;
    }

    // 정사각형 안의 점 (u, v)(좌상단 원점)가 WebCamTexture 픽셀 배열(좌하단 원점)의 어느 칸인가. flip은 HandLandmarkSource.FrameFlipH/V.
    public static void ToSource(int u, int v, int width, int height, bool flipH, bool flipV, out int sx, out int sy)
    {
        sx = flipH ? width - 1 - u : u;
        sy = flipV ? height - 1 - v : v;
    }

    // rgba(좌하단 원점, 픽셀당 4바이트) 영상에서 정사각형을 잘라 outSize×outSize RGB(좌상단 원점, 위에서 아래로)로 만든다. 쌍선형 보간.
    public static byte[] CropRgb(byte[] rgba, int width, int height, bool flipH, bool flipV, int x, int y, int size, int outSize)
    {
        if (rgba == null || rgba.Length < width * height * 4 || size < 1 || outSize < 1 || x < 0 || y < 0 || x + size > width || y + size > height)
            throw new ArgumentException("잘못된 자르기 입력");
        byte[] rgb = new byte[outSize * outSize * 3];
        double scale = (double)size / outSize;
        for (int oy = 0; oy < outSize; oy++)
        {
            double fv = Math.Max(0, Math.Min((oy + .5) * scale - .5, size - 1));
            int v0 = (int)fv, v1 = Math.Min(v0 + 1, size - 1);
            double wv = fv - v0;
            for (int ox = 0; ox < outSize; ox++)
            {
                double fu = Math.Max(0, Math.Min((ox + .5) * scale - .5, size - 1));
                int u0 = (int)fu, u1 = Math.Min(u0 + 1, size - 1);
                double wu = fu - u0;
                int o = (oy * outSize + ox) * 3;
                for (int c = 0; c < 3; c++)
                {
                    double a = Pixel(rgba, width, height, flipH, flipV, x + u0, y + v0, c), b = Pixel(rgba, width, height, flipH, flipV, x + u1, y + v0, c);
                    double d = Pixel(rgba, width, height, flipH, flipV, x + u0, y + v1, c), e = Pixel(rgba, width, height, flipH, flipV, x + u1, y + v1, c);
                    rgb[o + c] = (byte)Math.Round((a * (1 - wu) + b * wu) * (1 - wv) + (d * (1 - wu) + e * wu) * wv);
                }
            }
        }
        return rgb;
    }

    static double Pixel(byte[] rgba, int width, int height, bool flipH, bool flipV, int u, int v, int channel)
    {
        int sx, sy;
        ToSource(u, v, width, height, flipH, flipV, out sx, out sy);
        return rgba[(sy * width + sx) * 4 + channel];
    }

    // Unity Texture2D.LoadRawTextureData는 아래 줄부터 읽는다. 위에서 아래로 만든 RGB의 줄 순서를 뒤집어 준다.
    public static byte[] FlipRows(byte[] rgb, int size)
    {
        byte[] flipped = new byte[rgb.Length];
        int row = size * 3;
        for (int r = 0; r < size; r++) Buffer.BlockCopy(rgb, r * row, flipped, (size - 1 - r) * row, row);
        return flipped;
    }

    public static bool ShouldCapture(long elapsedMs, long lastCaptureMs, int captured)
    {
        // 시작 직후(손을 자세로 옮기는 구간)는 건너뛰고, 그 뒤 150ms 간격으로 최대 10장.
        return captured < MaxImagesPerTrial && elapsedMs >= StartDelayMs && (lastCaptureMs < 0 || elapsedMs - lastCaptureMs >= CaptureIntervalMs);
    }

    // 파일 이름: 시도번호-순번-손-글자.jpg (예 012-03-R-ㅂ.jpg). 글자는 마지막에 두어 `_other`처럼 밑줄이 있어도 해석된다.
    public static bool IsSafeLabel(string label)
    {
        return !string.IsNullOrEmpty(label) && label.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && label.IndexOf('-') < 0 && label.Trim() == label;
    }

    public static string FileName(int trial, int index, bool left, string label)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:000}-{1:00}-{2}-{3}.jpg", trial, index, left ? 'L' : 'R', label);
    }

    public static bool TryParseFileName(string name, out int trial, out int index, out bool left, out string label)
    {
        trial = 0; index = 0; left = false; label = null;
        if (string.IsNullOrEmpty(name) || !name.EndsWith(".jpg", StringComparison.Ordinal)) return false;
        string[] parts = name.Substring(0, name.Length - 4).Split(new char[] { '-' }, 4);
        if (parts.Length != 4 || (parts[2] != "L" && parts[2] != "R") || parts[3].Length == 0) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out trial) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out index)) return false;
        left = parts[2] == "L"; label = parts[3];
        return true;
    }
}
