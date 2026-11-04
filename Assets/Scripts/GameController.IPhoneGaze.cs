using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public partial class GameController
{
    const float IPhoneCalibrationSettleSeconds = .9f;
    const float IPhoneCalibrationCaptureSeconds = 1.5f;
    const float IPhoneRawPreviewScale = 450f;
    const float IPhoneStarGameDuration = 30f;
    const float IPhoneStarDwell = .65f;
    const float IPhoneStarHitRadius = 165f;
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
    float iphoneTargetShownAt, iphoneMeasureAt;
    double iphoneFeatureXSum, iphoneFeatureYSum;
    readonly Vector2[] iphoneMeasuredFeatures = new Vector2[IPhoneCalibrationTargets.Length];
    readonly Vector3[] iphoneFitX = new Vector3[2];
    Vector2 iphoneCanvasGaze;
    bool iphoneMarkerValid, iphoneCalibrated, iphoneUncalibrated;
    bool iphoneStarGameActive, iphoneStarGameFinished;
    float iphoneStarGameStartedAt, iphoneStarDwellStartedAt = -1f;
    int iphoneStarScore;
    Vector2 iphoneStarTarget;

    void UpdateIPhoneGaze()
    {
        if (!gazeTestOpen || !gazeUseIphone || iphoneSource == null) return;
        iphoneSource.Poll();
        TickIPhoneStarGame();
        bool fresh = iphoneSource.HasFreshSample;
        if (!fresh)
        {
            iphoneMarkerValid = false;
            iphoneStatus = iphoneSource.Status ?? "아이폰 데이터를 기다리는 중이에요. 연결 설정을 확인해 주세요.";
            if (iphoneMeasuring && Time.unscaledTime > iphoneMeasureAt + IPhoneCalibrationCaptureSeconds)
                CompleteIPhoneCalibrationPoint();
            return;
        }
        if (!iphoneSource.FaceFound)
        {
            iphoneMarkerValid = false;
            iphoneStatus = "아이폰에서 얼굴이 잡히지 않아요. 휴대폰을 바라봐 주세요.";
            if (iphoneMeasuring && Time.unscaledTime > iphoneMeasureAt + IPhoneCalibrationCaptureSeconds)
                CompleteIPhoneCalibrationPoint();
            return;
        }

        if (!iphoneCalibrating)
            iphoneStatus = iphoneCalibrated ? "iPhone 추적 중 · 9지점 보정 완료" : "iPhone 얼굴·눈 추적 중 · 화면 보정이 필요해요.";
        Vector2 feature = iphoneSource.EyeFeature;
        if (iphoneCalibrating && !iphoneMeasuring && Time.unscaledTime >= iphoneTargetShownAt + IPhoneCalibrationSettleSeconds)
            BeginIPhoneCalibrationCapture();

        if (iphoneMeasuring && iphoneSource.Sequence != iphoneLastCaptureSequence)
        {
            iphoneLastCaptureSequence = iphoneSource.Sequence;
            if (Time.unscaledTime <= iphoneMeasureAt + IPhoneCalibrationCaptureSeconds)
            {
                iphoneFeatureXSum += feature.x;
                iphoneFeatureYSum += feature.y;
                iphoneCaptureCount++;
            }
        }
        if (iphoneMeasuring && Time.unscaledTime > iphoneMeasureAt + IPhoneCalibrationCaptureSeconds)
            CompleteIPhoneCalibrationPoint();

        if (!iphoneCalibrated)
        {
            if (!iphoneUncalibrated) { iphoneMarkerValid = false; return; }
            Vector2 rawPoint = new Vector2(640f + feature.x * IPhoneRawPreviewScale, 450f - feature.y * IPhoneRawPreviewScale);
            rawPoint.x = Mathf.Clamp(rawPoint.x, 40f, 1240f);
            rawPoint.y = Mathf.Clamp(rawPoint.y, 40f, 860f);
            iphoneCanvasGaze = iphoneMarkerValid ? Vector2.Lerp(iphoneCanvasGaze, rawPoint, .35f) : rawPoint;
            iphoneMarkerValid = true;
            iphoneStatus = "무보정 원시 시선 미리보기 · 화면 위치는 9지점 보정이 필요해요.";
            return;
        }
        Vector2 projected = new Vector2(
            Vector3.Dot(iphoneFitX[0], new Vector3(feature.x, feature.y, 1f)),
            Vector3.Dot(iphoneFitX[1], new Vector3(feature.x, feature.y, 1f)));
        projected.x = Mathf.Clamp01(projected.x);
        projected.y = Mathf.Clamp01(projected.y);
        Vector2 canvasPoint = new Vector2(projected.x * 1280f, projected.y * 900f);
        iphoneCanvasGaze = iphoneMarkerValid ? Vector2.Lerp(iphoneCanvasGaze, canvasPoint, .35f) : canvasPoint;
        iphoneMarkerValid = true;
    }

    void TickIPhoneStarGame()
    {
        if (!iphoneStarGameActive) return;
        if (Time.unscaledTime - iphoneStarGameStartedAt >= IPhoneStarGameDuration)
        {
            iphoneStarGameActive = false;
            iphoneStarGameFinished = true;
            iphoneStarDwellStartedAt = -1f;
            iphoneStatus = $"시선 별잡기 완료 · {iphoneStarScore}개";
            return;
        }
        if (!iphoneMarkerValid || (iphoneCanvasGaze - iphoneStarTarget).sqrMagnitude > IPhoneStarHitRadius * IPhoneStarHitRadius)
        {
            iphoneStarDwellStartedAt = -1f;
            return;
        }
        if (iphoneStarDwellStartedAt < 0f) iphoneStarDwellStartedAt = Time.unscaledTime;
        if (Time.unscaledTime - iphoneStarDwellStartedAt < IPhoneStarDwell) return;
        iphoneStarScore++;
        iphoneStarTarget = new Vector2(UnityEngine.Random.Range(200f, 1080f), UnityEngine.Random.Range(300f, 650f));
        iphoneStarDwellStartedAt = -1f;
        NovelUI.Sound?.Invoke("Game/Correct|Up");
    }

    void StartIPhoneStarGame()
    {
        iphoneStarGameActive = true;
        iphoneStarGameFinished = false;
        iphoneStarGameStartedAt = Time.unscaledTime;
        iphoneStarDwellStartedAt = -1f;
        iphoneStarScore = 0;
        iphoneStarTarget = new Vector2(UnityEngine.Random.Range(200f, 1080f), UnityEngine.Random.Range(300f, 650f));
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
        iphoneUncalibrated = false;
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
        iphoneUncalibrated = false;
        iphoneCalibrationPoint = 0;
        iphoneMarkerValid = false;
        iphoneStatus = "VTube Studio PC API에 연결 중이에요.";
    }

    void StopIPhoneGaze()
    {
        iphoneMeasuring = iphoneCalibrating = false;
        iphoneStarGameActive = iphoneStarGameFinished = false;
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
        iphoneStarGameActive = iphoneStarGameFinished = false;
        iphoneUncalibrated = false;
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
        iphoneTargetShownAt = Time.unscaledTime;
        iphoneStatus = "화면 점을 바라봐 주세요. 자동으로 표본을 모읍니다.";
    }

    void BeginIPhoneCalibrationCapture()
    {
        iphoneFeatureXSum = iphoneFeatureYSum = 0;
        iphoneCaptureCount = 0;
        iphoneLastCaptureSequence = iphoneSource.Sequence;
        iphoneMeasureAt = Time.unscaledTime;
        iphoneMeasuring = true;
        iphoneStatus = "시선을 고정해 주세요. 1.5초 동안 자동 측정합니다.";
    }

    void CompleteIPhoneCalibrationPoint()
    {
        iphoneMeasuring = false;
        if (iphoneCaptureCount < 20)
        {
            iphoneTargetShownAt = Time.unscaledTime;
            iphoneStatus = "표본이 부족해요. 연결을 확인하고 같은 점을 다시 응시해 주세요.";
            return;
        }
        iphoneMeasuredFeatures[iphoneCalibrationPoint] = new Vector2(
            (float)(iphoneFeatureXSum / iphoneCaptureCount), (float)(iphoneFeatureYSum / iphoneCaptureCount));
        iphoneCalibrationPoint++;
        if (iphoneCalibrationPoint < IPhoneCalibrationTargets.Length)
        {
            iphoneTargetShownAt = Time.unscaledTime;
            iphoneStatus = $"다음 점을 바라봐 주세요. 자동 측정이 이어집니다. ({iphoneCalibrationPoint + 1}/9)";
            return;
        }
        if (!FitIPhoneCalibration())
        {
            iphoneCalibrationPoint = 0;
            iphoneTargetShownAt = Time.unscaledTime;
            iphoneStatus = "눈 움직임이 충분히 달라지지 않았어요. 휴대폰을 고정하고 다시 보정해 주세요.";
            return;
        }
        iphoneCalibrating = false;
        iphoneCalibrated = true;
        iphoneStatus = "9지점 보정 완료. 시선 위치를 확인해 보세요.";
    }

    void DrawIPhoneCalibrationScreen()
    {
        GUI.matrix = Canvas;
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
        NovelUI.Label(new Rect(0, 20, 1280, 48), "시선 보정", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(80, 86, 420, 34), $"보정 지점 {iphoneCalibrationPoint + 1} / {IPhoneCalibrationTargets.Length}", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);

        float phaseProgress = iphoneMeasuring
            ? .35f + Mathf.Clamp01((Time.unscaledTime - iphoneMeasureAt) / IPhoneCalibrationCaptureSeconds) * .65f
            : Mathf.Clamp01((Time.unscaledTime - iphoneTargetShownAt) / IPhoneCalibrationSettleSeconds) * .35f;
        float progress = (iphoneCalibrationPoint + phaseProgress) / IPhoneCalibrationTargets.Length;
        NovelUI.Fill(new Rect(80, 126, 1120, 8), NovelUI.Alpha(NovelUI.Plum, .16f), 4);
        NovelUI.Fill(new Rect(80, 126, 1120 * progress, 8), NovelUI.DeepPink, 4);

        Vector2 target = IPhoneCalibrationTargets[Mathf.Min(iphoneCalibrationPoint, IPhoneCalibrationTargets.Length - 1)];
        Vector2 point = new Vector2(target.x * 1280f, target.y * 900f);
        NovelUI.Fill(new Rect(point.x - 31, point.y - 31, 62, 62), NovelUI.Alpha(NovelUI.DeepPink, .18f), 31);
        NovelUI.Fill(new Rect(point.x - 22, point.y - 22, 44, 44), NovelUI.DeepPink, 22);
        NovelUI.Fill(new Rect(point.x - 15, point.y - 15, 30, 30), NovelUI.Cream, 15);
        NovelUI.Fill(new Rect(point.x - 5, point.y - 5, 10, 10), NovelUI.DeepPink, 5);

        string instruction = iphoneMeasuring
            ? $"시선을 점에 고정해 주세요 · 자동 측정 중 {Mathf.CeilToInt(Mathf.Max(0f, IPhoneCalibrationCaptureSeconds - (Time.unscaledTime - iphoneMeasureAt)))}초"
            : "분홍 점을 바라봐 주세요 · 잠시 후 자동 측정합니다.";
        NovelUI.Label(new Rect(150, 790, 980, 42), instruction, NovelUI.Soft, 20, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
        NovelUI.Label(new Rect(150, 836, 980, 30), iphoneStatus, NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (NovelUI.Button(new Rect(1040, 24, 190, 46), "보정 취소", false, 18))
        {
            iphoneCalibrating = iphoneMeasuring = false;
            iphoneMarkerValid = false;
            iphoneStatus = "보정을 취소했어요.";
        }
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

        if (iphoneStarGameActive || iphoneStarGameFinished)
        {
            DrawIPhoneStarGame();
            return;
        }

        {
            bool fresh = iphoneSource.HasFreshSample;
            bool faceFound = fresh && iphoneSource.FaceFound;
            string status = faceFound ? iphoneStatus : iphoneSource.Status ?? iphoneStatus;
            NovelUI.Label(new Rect(120, 205, 1040, 42), status, NovelUI.Soft, 20, faceFound ? NovelUI.Mint : NovelUI.DeepPink, TextAnchor.MiddleCenter, FontStyle.Bold);

            if (!iphoneCalibrated)
            {
                if (iphoneUncalibrated && iphoneMarkerValid)
                    DrawIPhoneGazePointer(iphoneCanvasGaze);
                NovelUI.Label(new Rect(230, 310, 820, 72), iphoneUncalibrated
                    ? "무보정 원시 눈값을 점으로 표시합니다.\n방향 확인용이며 화면 위치 정확도는 보정이 필요해요."
                    : "화면 점 9개를 차례로 응시해 보정합니다.\n보정 화면에서 점을 바라보면 자동으로 측정합니다.", NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter);
                GUI.enabled = fresh && iphoneSource.FaceFound;
                if (!iphoneUncalibrated)
                {
                    if (NovelUI.Button(new Rect(440, 410, 400, 62), "9지점 보정 시작", true, 23)) BeginIPhoneCalibration();
                    if (NovelUI.Button(new Rect(440, 490, 400, 52), "무보정 원시값 보기", false, 20)) iphoneUncalibrated = true;
                }
                else
                {
                    Vector2 feature = iphoneSource.EyeFeature;
                    NovelUI.Label(new Rect(80, 742, 1120, 32), $"Raw Eye {feature.x:F2}, {feature.y:F2} · 9지점 보정 후 화면 위치를 맞출 수 있어요.", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
                    if (NovelUI.Button(new Rect(500, 785, 280, 50), "9지점 보정 시작", true, 19)) BeginIPhoneCalibration();
                }
                GUI.enabled = true;
            }
            else
            {
                if (iphoneMarkerValid) DrawIPhoneGazePointer(iphoneCanvasGaze);
                Vector2 feature = iphoneSource.EyeFeature;
                string transport = iphoneUseUsb ? "iPhone USB → VTube Studio API" : "iPhone Wi-Fi UDP";
                NovelUI.Label(new Rect(80, 742, 1120, 32), $"{transport} · {iphoneSource.Sequence} samples · Eye {feature.x:F2}, {feature.y:F2}", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
                if (NovelUI.Button(new Rect(110, 785, 280, 50), "시선 별잡기", true, 19)) StartIPhoneStarGame();
                if (NovelUI.Button(new Rect(500, 785, 280, 50), "다시 보정", false, 19)) BeginIPhoneCalibration();
            }

            if (NovelUI.Button(new Rect(890, 785, 280, 50), "연결 끊기", false, 18)) StopIPhoneGaze();
        }
    }

    void DrawIPhoneGazePointer(Vector2 point)
    {
        NovelUI.Fill(new Rect(point.x - 28, point.y - 28, 56, 56), NovelUI.Alpha(NovelUI.DeepPink, .22f), 28);
        NovelUI.Fill(new Rect(point.x - 17, point.y - 17, 34, 34), NovelUI.DeepPink, 17);
        NovelUI.Fill(new Rect(point.x - 6, point.y - 6, 12, 12), NovelUI.Cream, 6);
    }

    void DrawIPhoneStarGame()
    {
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
        NovelUI.Label(new Rect(0, 42, 1280, 56), "시선 별잡기", NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (iphoneStarGameFinished)
        {
            NovelUI.Label(new Rect(260, 290, 760, 92), $"별 {iphoneStarScore}개를 잡았어요!", NovelUI.Round, 42, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (NovelUI.Button(new Rect(330, 430, 280, 62), "한 판 더", true, 23)) StartIPhoneStarGame();
            if (NovelUI.Button(new Rect(670, 430, 280, 62), "돌아가기", false, 23)) iphoneStarGameFinished = false;
            return;
        }

        float remaining = Mathf.Max(0f, IPhoneStarGameDuration - (Time.unscaledTime - iphoneStarGameStartedAt));
        NovelUI.Label(new Rect(80, 115, 330, 54), $"점수  {iphoneStarScore}", NovelUI.Round, 26, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
        NovelUI.Label(new Rect(870, 115, 330, 54), $"남은 시간  {Mathf.CeilToInt(remaining)}초", NovelUI.Round, 26, NovelUI.Plum, TextAnchor.MiddleRight, FontStyle.Bold);
        NovelUI.Label(new Rect(180, 168, 920, 40), "별 주위에 커진 포인터를 0.65초 머물게 하면 잡아요. 넉넉하게 판정합니다!", NovelUI.Soft, 18, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Fill(new Rect(iphoneStarTarget.x - 98, iphoneStarTarget.y - 98, 196, 196), NovelUI.Alpha(NovelUI.Lemon, .65f), 98);
        NovelUI.Label(new Rect(iphoneStarTarget.x - 82, iphoneStarTarget.y - 94, 164, 170), "★", NovelUI.Round, 124, NovelUI.DeepPink, TextAnchor.MiddleCenter);
        float dwell = iphoneStarDwellStartedAt < 0f ? 0f : Mathf.Clamp01((Time.unscaledTime - iphoneStarDwellStartedAt) / IPhoneStarDwell);
        NovelUI.Fill(new Rect(iphoneStarTarget.x - 76, iphoneStarTarget.y + 108, 152, 12), NovelUI.Alpha(NovelUI.Plum, .18f), 6);
        NovelUI.Fill(new Rect(iphoneStarTarget.x - 76, iphoneStarTarget.y + 108, 152 * dwell, 12), NovelUI.DeepPink, 6);
        if (iphoneMarkerValid) DrawIPhoneGazePointer(iphoneCanvasGaze);
        if (NovelUI.Button(new Rect(1030, 820, 200, 48), "게임 끝내기", false, 18)) iphoneStarGameActive = iphoneStarGameFinished = false;
    }
}
