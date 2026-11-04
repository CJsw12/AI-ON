using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public partial class GameController
{
    const string IPhoneIpPref = "GazeTest.IPhoneIP";
    const string IPhonePortPref = "GazeTest.IPhonePort";
    const string VtsPortPref = "GazeTest.VtsApiPort";
    static readonly Vector2[] IPhoneCalibrationTargets =
    {
        new Vector2(.08f, .12f), new Vector2(.50f, .12f), new Vector2(.92f, .12f),
        new Vector2(.08f, .50f), new Vector2(.50f, .50f), new Vector2(.92f, .50f),
        new Vector2(.08f, .88f), new Vector2(.50f, .88f), new Vector2(.92f, .88f)
    };

    IPhoneGazeUdpReceiver iphoneReceiver;
    IIPhoneGazeSource iphoneSource;
    MonoBehaviour iphoneSourceBehaviour;
    bool iphoneUseUsb;
    string iphoneIpDraft = "";
    string iphonePortDraft = "21412";
    string vtsPortDraft = "8001";
    string vtsApiToken;
    string iphoneStatus = "VTube Studio 설정에서 3rd Party PC Clients를 켜 주세요.";
    bool iphoneFieldsLoaded;
    bool iphoneCalibrating, iphoneMeasuring;
    int iphoneCalibrationPoint, iphoneCaptureCount, iphoneLastCaptureSequence;
    float iphoneMeasureAt;
    double iphoneFeatureXSum, iphoneFeatureYSum;
    readonly Vector2[] iphoneMeasuredFeatures = new Vector2[IPhoneCalibrationTargets.Length];
    readonly Vector3[] iphoneFitX = new Vector3[2];
    Vector2 iphoneCanvasGaze;
    bool iphoneMarkerValid, iphoneCalibrated;

    void UpdateIPhoneGaze()
    {
        if (!gazeTestOpen || !gazeUseIphone || iphoneSource == null) return;
        iphoneSource.Poll();
        bool fresh = iphoneSource.HasFreshSample;
        if (!fresh)
        {
            iphoneMarkerValid = false;
            iphoneStatus = iphoneSource.Status ?? "아이폰 데이터를 기다리는 중이에요. 연결 설정을 확인해 주세요.";
            return;
        }
        if (!iphoneSource.FaceFound)
        {
            iphoneMarkerValid = false;
            iphoneStatus = "아이폰에서 얼굴이 잡히지 않아요. 휴대폰을 바라봐 주세요.";
            return;
        }

        iphoneStatus = iphoneCalibrated ? "iPhone 추적 중 · 9지점 보정 완료" : "iPhone 얼굴·눈 추적 중 · 화면 보정이 필요해요.";
        Vector2 feature = iphoneSource.EyeFeature;
        if (iphoneMeasuring && iphoneSource.Sequence != iphoneLastCaptureSequence)
        {
            iphoneLastCaptureSequence = iphoneSource.Sequence;
            if (Time.unscaledTime >= iphoneMeasureAt && Time.unscaledTime <= iphoneMeasureAt + 1f)
            {
                iphoneFeatureXSum += feature.x;
                iphoneFeatureYSum += feature.y;
                iphoneCaptureCount++;
            }
        }
        if (iphoneMeasuring && Time.unscaledTime > iphoneMeasureAt + 1f)
            CompleteIPhoneCalibrationPoint();

        if (!iphoneCalibrated) { iphoneMarkerValid = false; return; }
        Vector2 projected = new Vector2(
            Vector3.Dot(iphoneFitX[0], new Vector3(feature.x, feature.y, 1f)),
            Vector3.Dot(iphoneFitX[1], new Vector3(feature.x, feature.y, 1f)));
        projected.x = Mathf.Clamp01(projected.x);
        projected.y = Mathf.Clamp01(projected.y);
        Vector2 canvasPoint = new Vector2(projected.x * 1280f, projected.y * 900f);
        iphoneCanvasGaze = iphoneMarkerValid ? Vector2.Lerp(iphoneCanvasGaze, canvasPoint, .35f) : canvasPoint;
        iphoneMarkerValid = true;
    }

    void StartIPhoneGaze()
    {
        string ipText = iphoneIpDraft.Trim();
        if (!IPAddress.TryParse(ipText, out IPAddress address) || address.AddressFamily != AddressFamily.InterNetwork)
        {
            iphoneStatus = "아이폰의 IPv4 주소를 입력해 주세요. (예: 192.168.0.15)";
            return;
        }
        if (!int.TryParse(iphonePortDraft, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port < 1 || port > 65535)
        {
            iphoneStatus = "VTube Studio 앱에 표시된 UDP 포트를 확인해 주세요.";
            return;
        }

        StopIPhoneGaze();
        GameObject receiverObject = new GameObject("iPhone Gaze UDP Receiver");
        receiverObject.transform.SetParent(transform, false);
        iphoneReceiver = receiverObject.AddComponent<IPhoneGazeUdpReceiver>();
        iphoneSource = iphoneReceiver;
        iphoneSourceBehaviour = iphoneReceiver;
        if (!iphoneReceiver.StartReceiving(address, port))
        {
            iphoneStatus = iphoneReceiver.Error;
            StopIPhoneGaze();
            return;
        }

        PlayerPrefs.SetString(IPhoneIpPref, ipText);
        PlayerPrefs.SetString(IPhonePortPref, port.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        iphoneCalibrating = iphoneMeasuring = iphoneCalibrated = false;
        iphoneCalibrationPoint = 0;
        iphoneMarkerValid = false;
        iphoneStatus = "연결 요청을 보냈어요. 얼굴 데이터를 기다리는 중이에요.";
    }

    void StartIPhoneUsbGaze()
    {
        if (!int.TryParse(vtsPortDraft, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port < 1 || port > 65535)
        {
            iphoneStatus = "VTube Studio API 포트가 올바르지 않아요. 기본값은 8001이에요.";
            return;
        }
        StopIPhoneGaze();
        GameObject bridgeObject = new GameObject("iPhone Gaze VTube Studio USB Bridge");
        bridgeObject.transform.SetParent(transform, false);
        VTubeStudioUsbApiBridge bridge = bridgeObject.AddComponent<VTubeStudioUsbApiBridge>();
        iphoneSource = bridge;
        iphoneSourceBehaviour = bridge;
        if (!bridge.StartConnecting(port, vtsApiToken))
        {
            iphoneStatus = bridge.Status;
            StopIPhoneGaze();
            return;
        }
        PlayerPrefs.SetString(VtsPortPref, port.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        iphoneCalibrating = iphoneMeasuring = iphoneCalibrated = false;
        iphoneCalibrationPoint = 0;
        iphoneMarkerValid = false;
        iphoneStatus = "VTube Studio PC API에 연결 중이에요.";
    }

    void StopIPhoneGaze()
    {
        iphoneMeasuring = iphoneCalibrating = false;
        iphoneMarkerValid = false;
        if (iphoneSource != null)
        {
            if (iphoneSource is VTubeStudioUsbApiBridge bridge && !string.IsNullOrEmpty(bridge.ApiToken)) vtsApiToken = bridge.ApiToken;
            iphoneSource.StopReceiving();
            if (iphoneSourceBehaviour != null) Destroy(iphoneSourceBehaviour.gameObject);
            iphoneSource = null;
            iphoneSourceBehaviour = null;
            iphoneReceiver = null;
        }
    }

    void BeginIPhoneCalibration()
    {
        if (iphoneSource == null || !iphoneSource.HasFreshSample || !iphoneSource.FaceFound)
        {
            iphoneStatus = "먼저 아이폰 얼굴 추적 데이터가 들어오는지 확인해 주세요.";
            return;
        }
        iphoneCalibrating = true;
        iphoneMeasuring = false;
        iphoneCalibrated = false;
        iphoneMarkerValid = false;
        iphoneCalibrationPoint = 0;
        iphoneStatus = "화면의 분홍 점을 바라본 뒤 측정 버튼을 눌러 주세요.";
    }

    void BeginIPhoneCalibrationCapture()
    {
        iphoneFeatureXSum = iphoneFeatureYSum = 0;
        iphoneCaptureCount = 0;
        iphoneLastCaptureSequence = iphoneSource.Sequence;
        iphoneMeasureAt = Time.unscaledTime + .5f;
        iphoneMeasuring = true;
        iphoneStatus = "분홍 점을 바라보세요. 잠시 뒤 1초간 자동 측정합니다.";
    }

    void CompleteIPhoneCalibrationPoint()
    {
        iphoneMeasuring = false;
        if (iphoneCaptureCount < 20)
        {
            iphoneStatus = "측정 표본이 부족해요. 얼굴과 Wi-Fi 연결을 확인하고 다시 측정해 주세요.";
            return;
        }
        iphoneMeasuredFeatures[iphoneCalibrationPoint] = new Vector2(
            (float)(iphoneFeatureXSum / iphoneCaptureCount), (float)(iphoneFeatureYSum / iphoneCaptureCount));
        iphoneCalibrationPoint++;
        if (iphoneCalibrationPoint < IPhoneCalibrationTargets.Length)
        {
            iphoneStatus = "다음 분홍 점을 바라본 뒤 측정 버튼을 눌러 주세요.";
            return;
        }
        if (!FitIPhoneCalibration())
        {
            iphoneCalibrationPoint = 0;
            iphoneStatus = "눈 움직임이 충분히 달라지지 않았어요. 휴대폰을 고정하고 다시 보정해 주세요.";
            return;
        }
        iphoneCalibrating = false;
        iphoneCalibrated = true;
        iphoneStatus = "9지점 보정 완료. 시선 위치를 확인해 보세요.";
    }

    bool FitIPhoneCalibration()
    {
        double[,] normal = new double[3, 3];
        double[] xRight = new double[3], yRight = new double[3];
        for (int i = 0; i < IPhoneCalibrationTargets.Length; i++)
        {
            Vector2 feature = iphoneMeasuredFeatures[i];
            double[] row = { feature.x, feature.y, 1d };
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++) normal[r, c] += row[r] * row[c];
                xRight[r] += row[r] * IPhoneCalibrationTargets[i].x;
                yRight[r] += row[r] * IPhoneCalibrationTargets[i].y;
            }
        }
        if (!SolveIPhone3x3(normal, xRight, out double[] x) || !SolveIPhone3x3(normal, yRight, out double[] y)) return false;
        iphoneFitX[0] = new Vector3((float)x[0], (float)x[1], (float)x[2]);
        iphoneFitX[1] = new Vector3((float)y[0], (float)y[1], (float)y[2]);
        return true;
    }

    static bool SolveIPhone3x3(double[,] source, double[] sourceRight, out double[] solution)
    {
        double[,] matrix = new double[3, 4];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++) matrix[r, c] = source[r, c];
            matrix[r, 3] = sourceRight[r];
        }
        for (int column = 0; column < 3; column++)
        {
            int pivot = column;
            for (int row = column + 1; row < 3; row++)
                if (Math.Abs(matrix[row, column]) > Math.Abs(matrix[pivot, column])) pivot = row;
            if (Math.Abs(matrix[pivot, column]) < 1e-9) { solution = null; return false; }
            if (pivot != column)
                for (int c = column; c < 4; c++) { double value = matrix[column, c]; matrix[column, c] = matrix[pivot, c]; matrix[pivot, c] = value; }
            double divisor = matrix[column, column];
            for (int c = column; c < 4; c++) matrix[column, c] /= divisor;
            for (int row = 0; row < 3; row++)
            {
                if (row == column) continue;
                double factor = matrix[row, column];
                for (int c = column; c < 4; c++) matrix[row, c] -= factor * matrix[column, c];
            }
        }
        solution = new[] { matrix[0, 3], matrix[1, 3], matrix[2, 3] };
        return true;
    }

    void DrawIPhoneGazePanel()
    {
        if (!iphoneFieldsLoaded)
        {
            iphoneIpDraft = PlayerPrefs.GetString(IPhoneIpPref, "");
            iphonePortDraft = PlayerPrefs.GetString(IPhonePortPref, "21412");
            vtsPortDraft = PlayerPrefs.GetString(VtsPortPref, "8001");
            iphoneFieldsLoaded = true;
        }
        bool chooseUsb = iphoneUseUsb;
        if (NovelUI.Button(new Rect(390, 170, 230, 46), "Wi-Fi UDP", !iphoneUseUsb, 18)) chooseUsb = false;
        if (NovelUI.Button(new Rect(660, 170, 230, 46), "USB · VTube Studio", iphoneUseUsb, 18)) chooseUsb = true;
        if (chooseUsb != iphoneUseUsb)
        {
            if (iphoneSource != null) StopIPhoneGaze();
            iphoneUseUsb = chooseUsb;
        }

        if (iphoneSource == null)
        {
            if (!iphoneUseUsb)
            {
                NovelUI.Label(new Rect(150, 225, 980, 34), "iPhone과 노트북을 같은 Wi-Fi에 연결하세요.", NovelUI.Soft, 19, NovelUI.Berry, TextAnchor.MiddleCenter);
                NovelUI.Label(new Rect(210, 280, 220, 46), "iPhone IP 주소", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleRight, FontStyle.Bold);
                iphoneIpDraft = GUI.TextField(new Rect(450, 280, 360, 46), iphoneIpDraft, 48, NovelUI.Input);
                NovelUI.Label(new Rect(210, 338, 220, 46), "iPhone UDP 포트", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleRight, FontStyle.Bold);
                iphonePortDraft = GUI.TextField(new Rect(450, 338, 160, 46), iphonePortDraft, 5, NovelUI.Input);
                NovelUI.Label(new Rect(620, 338, 390, 46), "기본값 21412 · 앱 표시 포트를 사용하세요.", NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleLeft);
                if (NovelUI.Button(new Rect(440, 410, 400, 58), "iPhone Wi-Fi 연결", true, 23)) StartIPhoneGaze();
                NovelUI.Label(new Rect(170, 490, 940, 88), "VTube Studio iOS 설정에서 ‘3rd Party PC Clients’를 켜고 IP와 포트를 입력하세요.\nWindows 방화벽이 묻는 경우 개인 네트워크 접근을 허용하세요.", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleCenter);
            }
            else
            {
                NovelUI.Label(new Rect(150, 220, 980, 42), "iPhone은 USB로 PC의 VTube Studio에 연결하고, Unity가 PC API를 읽습니다.", NovelUI.Soft, 18, NovelUI.Berry, TextAnchor.MiddleCenter);
                NovelUI.Label(new Rect(250, 285, 180, 46), "API 포트", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleRight, FontStyle.Bold);
                vtsPortDraft = GUI.TextField(new Rect(455, 285, 150, 46), vtsPortDraft, 5, NovelUI.Input);
                NovelUI.Label(new Rect(625, 285, 380, 46), "기본값 8001 · VTube Studio 설정 확인", NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleLeft);
                if (NovelUI.Button(new Rect(440, 355, 400, 58), "VTube Studio USB 연결", true, 22)) StartIPhoneUsbGaze();
                NovelUI.Label(new Rect(130, 445, 1020, 142), "준비: iPhone과 PC에 VTube Studio 설치 · iPhone USB 연결 및 잠금 해제 · Windows에 Apple Devices 또는 iTunes 설치 후 기기 페어링\nPC VTube Studio의 API 접근을 켜고, 연결 후 ‘AI-ON Gaze Test’ 접근 요청을 승인하세요.\n※ iPhone 무료 PC 스트리밍은 VTube Studio 정책에 따라 5분 후 끊길 수 있습니다.", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
            }
            NovelUI.Label(new Rect(190, 620, 900, 40), iphoneStatus, NovelUI.Soft, 17, NovelUI.DeepPink, TextAnchor.MiddleCenter);
            return;
        }

        {
            bool fresh = iphoneSource.HasFreshSample;
            bool faceFound = fresh && iphoneSource.FaceFound;
            string status = faceFound ? iphoneStatus : iphoneSource.Status ?? iphoneStatus;
            NovelUI.Label(new Rect(120, 205, 1040, 42), status, NovelUI.Soft, 20, faceFound ? NovelUI.Mint : NovelUI.DeepPink, TextAnchor.MiddleCenter, FontStyle.Bold);

            if (iphoneCalibrating)
            {
                Vector2 target = IPhoneCalibrationTargets[Mathf.Min(iphoneCalibrationPoint, IPhoneCalibrationTargets.Length - 1)];
                Vector2 canvas = new Vector2(target.x * 1280f, target.y * 900f);
                NovelUI.Fill(new Rect(canvas.x - 19, canvas.y - 19, 38, 38), NovelUI.DeepPink, 19);
                NovelUI.Label(new Rect(340, 728, 600, 34), iphoneMeasuring ? "측정 중... 눈을 점에 고정해 주세요." : $"보정 지점 {iphoneCalibrationPoint + 1}/9 · 분홍 점을 바라보세요.", NovelUI.Soft, 18, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
                GUI.enabled = fresh && !iphoneMeasuring;
                if (NovelUI.Button(new Rect(500, 772, 280, 50), "이 지점 측정", true, 20)) BeginIPhoneCalibrationCapture();
                GUI.enabled = true;
                if (NovelUI.Button(new Rect(1030, 708, 200, 48), "보정 취소", false, 18)) { iphoneCalibrating = iphoneMeasuring = false; }
            }
            else if (!iphoneCalibrated)
            {
                NovelUI.Label(new Rect(230, 310, 820, 72), "화면 점 9개를 차례로 응시해 보정합니다.\n각 점에서 눈을 고정한 뒤 ‘이 지점 측정’을 눌러 주세요.", NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter);
                GUI.enabled = fresh && iphoneSource.FaceFound;
                if (NovelUI.Button(new Rect(440, 410, 400, 62), "9지점 보정 시작", true, 23)) BeginIPhoneCalibration();
                GUI.enabled = true;
            }
            else
            {
                if (iphoneMarkerValid) NovelUI.Fill(new Rect(iphoneCanvasGaze.x - 14, iphoneCanvasGaze.y - 14, 28, 28), NovelUI.DeepPink, 14);
                Vector2 feature = iphoneSource.EyeFeature;
                string transport = iphoneUseUsb ? "iPhone USB → VTube Studio API" : "iPhone Wi-Fi UDP";
                NovelUI.Label(new Rect(80, 742, 1120, 32), $"{transport} · {iphoneSource.Sequence} samples · Eye {feature.x:F2}, {feature.y:F2}", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
                if (NovelUI.Button(new Rect(480, 785, 320, 50), "다시 보정", false, 19)) BeginIPhoneCalibration();
            }

            if (NovelUI.Button(new Rect(1030, 820, 200, 48), "연결 끊기", false, 18)) StopIPhoneGaze();
        }
    }
}
