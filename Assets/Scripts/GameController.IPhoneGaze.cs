using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public partial class GameController
{
    const float IPhoneCalibrationSettleSeconds = .75f;
    const float IPhoneCalibrationCaptureSeconds = 1.2f;
    const float IPhoneRawPreviewScale = 450f;
    const float IPhoneBlinkThreshold = .7f;
    const string IPhoneIpPref = "GazeTest.IPhoneIP";
    const string IPhonePortPref = "GazeTest.IPhonePort";
    const string VtsPortPref = "GazeTest.VtsApiPort";
    static readonly Vector2[] IPhoneCalibrationTargets =
    {
        // A 3×3 grid plus four in-between points: five positions per axis let the fit tell a tracker that
        // under-reports large eye turns (the usual cause of weak edges) from a linear one, which 3 positions can't.
        new Vector2(.08f, .12f), new Vector2(.50f, .12f), new Vector2(.92f, .12f),
        new Vector2(.50f, .31f),
        new Vector2(.08f, .50f), new Vector2(.29f, .50f), new Vector2(.50f, .50f), new Vector2(.71f, .50f), new Vector2(.92f, .50f),
        new Vector2(.50f, .69f),
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
    int iphoneCalibrationPoint, iphoneLastCaptureSequence, iphoneLastLiveSequence;
    float iphoneTargetShownAt, iphoneMeasureAt;
    readonly List<double[]> iphoneCaptureAll = new List<double[]>(), iphoneCaptureOpen = new List<double[]>();
    readonly double[][] iphoneMeasuredFeatures = new double[IPhoneCalibrationTargets.Length][];
    readonly List<double[]>[] iphoneMeasuredSamples = new List<double[]>[IPhoneCalibrationTargets.Length];
    readonly GazeMapper iphoneMapper = new GazeMapper();
    readonly GazeSmoother iphoneSmoother = new GazeSmoother();
    Vector2 iphoneCanvasGaze;
    bool iphoneMarkerValid, iphoneCalibrated, iphoneUncalibrated, iphoneBlinking;
    float iphoneBlinkSince = -1f, iphoneLastLiveAt = -100f;

    void UpdateIPhoneGaze()
    {
        if (!gazeTestOpen || !gazeUseIphone || iphoneSource == null) return;
        iphoneSource.Poll();
        // Looking down lowers the eyelids and raises the blink value, so the bar rises with downward gaze;
        // otherwise every bottom-row sample would be thrown away as a blink.
        float lookDown = Mathf.Clamp01(-iphoneSource.LookFeature.y);
        bool lidsDown = iphoneSource.HasFreshSample && iphoneSource.Blink >= IPhoneBlinkThreshold + .2f * lookDown;
        // Calibration drops every lids-down frame; the pointer only pauses for real (short or fully closed) blinks.
        iphoneBlinking = ShortBlink(lidsDown, iphoneSource.Blink >= .92f, ref iphoneBlinkSince);
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
            iphoneStatus = iphoneCalibrated ? $"iPhone 추적 중 · 보정 완료 ({IPhoneAccuracyText()})" : "iPhone 얼굴·눈 추적 중 · 화면 보정이 필요해요.";
        Vector2 feature = iphoneSource.EyeFeature;
        double[] features = GazeMapper.Features(feature, iphoneSource.HeadFeature, iphoneSource.LookFeature);
        // Eye rotation jumps while the eyelids close, so blink frames are left out of calibration and don't move the pointer.
        bool blinking = iphoneBlinking;
        if (iphoneCalibrating && !iphoneMeasuring && Time.unscaledTime >= iphoneTargetShownAt + IPhoneCalibrationSettleSeconds)
            BeginIPhoneCalibrationCapture();

        if (iphoneMeasuring && iphoneSource.Sequence != iphoneLastCaptureSequence)
        {
            iphoneLastCaptureSequence = iphoneSource.Sequence;
            if (Time.unscaledTime <= iphoneMeasureAt + IPhoneCalibrationCaptureSeconds)
            {
                iphoneCaptureAll.Add(features);
                if (!lidsDown) iphoneCaptureOpen.Add(features);
            }
        }
        if (iphoneMeasuring && Time.unscaledTime > iphoneMeasureAt + IPhoneCalibrationCaptureSeconds)
            CompleteIPhoneCalibrationPoint();

        if (iphoneSource.Sequence == iphoneLastLiveSequence) return;   // no new sample this frame
        iphoneLastLiveSequence = iphoneSource.Sequence;
        iphoneLastLiveAt = Time.unscaledTime;
        if (blinking && iphoneMarkerValid) return;

        if (!iphoneCalibrated)
        {
            if (!iphoneUncalibrated) { iphoneMarkerValid = false; return; }
            Vector2 rawPoint = new Vector2(640f + feature.x * IPhoneRawPreviewScale, 450f - feature.y * IPhoneRawPreviewScale);
            rawPoint.x = Mathf.Clamp(rawPoint.x, 40f, 1240f);
            rawPoint.y = Mathf.Clamp(rawPoint.y, 40f, 860f);
            if (!iphoneMarkerValid) iphoneSmoother.Reset();
            iphoneCanvasGaze = iphoneSmoother.Filter(rawPoint, Time.unscaledTime);
            iphoneMarkerValid = true;
            iphoneStatus = "무보정 원시 시선 미리보기 · 화면 위치는 화면 보정이 필요해요.";
            return;
        }
        Vector2 projected = iphoneMapper.Predict(features);
        if (!iphoneMarkerValid) iphoneSmoother.Reset();
        iphoneCanvasGaze = iphoneSmoother.Filter(new Vector2(projected.x * 1280f, projected.y * 900f), Time.unscaledTime);
        iphoneMarkerValid = true;
    }

    string IPhoneAccuracyText()
    {
        float error = iphoneMapper.ErrorPixels;
        string grade = error < 90f ? "좋음" : error < 160f ? "보통" : "낮음 · 다시 보정 추천";
        return $"{iphoneMapper.ModelName} 기준 오차 약 {Mathf.RoundToInt(error)}px · {grade}";
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
        if (gazeUseIphone) StopStarGame();
        iphoneMarkerValid = false;
        if (iphoneSource != null)
        {
            if (iphoneSource is VTubeStudioUsbApiBridge bridge) vtsApiToken = bridge.ApiToken;   // null after a rejection
            iphoneSource.StopReceiving();
            if (iphoneSourceBehaviour != null) Destroy(iphoneSourceBehaviour.gameObject);
            iphoneSource = null;
            iphoneSourceBehaviour = null;
            iphoneReceiver = null;
        }
    }

    void BeginIPhoneCalibration()
    {
        StopStarGame();
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
        iphoneCaptureAll.Clear();
        iphoneCaptureOpen.Clear();
        iphoneLastCaptureSequence = iphoneSource.Sequence;
        iphoneMeasureAt = Time.unscaledTime;
        iphoneMeasuring = true;
        iphoneStatus = "시선을 고정해 주세요. 1.2초 동안 자동 측정합니다.";
    }

    void CompleteIPhoneCalibrationPoint()
    {
        iphoneMeasuring = false;
        if (iphoneCaptureAll.Count < 20)
        {
            iphoneTargetShownAt = Time.unscaledTime;
            iphoneStatus = "표본이 부족해요. 연결을 확인하고 같은 점을 다시 응시해 주세요.";
            return;
        }
        // Closed-eye frames are never used: if too few open-eye frames remain, measure this point again.
        if (iphoneCaptureOpen.Count < 10)
        {
            iphoneTargetShownAt = Time.unscaledTime;
            iphoneStatus = "눈을 감은 표본이 많았어요. 눈을 뜨고 같은 점을 다시 바라봐 주세요.";
            return;
        }
        // The median ignores quick glances that a plain average would drag along.
        var chosen = new List<double[]>(iphoneCaptureOpen);
        iphoneMeasuredFeatures[iphoneCalibrationPoint] = GazeMapper.Median(chosen);
        iphoneMeasuredSamples[iphoneCalibrationPoint] = chosen;
        iphoneCalibrationPoint++;
        NovelUI.Sound?.Invoke("Select");
        if (iphoneCalibrationPoint < IPhoneCalibrationTargets.Length)
        {
            iphoneTargetShownAt = Time.unscaledTime;
            iphoneStatus = $"다음 점을 바라봐 주세요. 자동 측정이 이어집니다. ({iphoneCalibrationPoint + 1}/{IPhoneCalibrationTargets.Length})";
            return;
        }
        if (!FitIPhoneCalibration())
        {
            iphoneCalibrationPoint = 0;
            iphoneTargetShownAt = Time.unscaledTime;
            iphoneStatus = "눈·고개 움직임이 점마다 충분히 달라지지 않았어요. 휴대폰을 고정하고 다시 보정해 주세요.";
            return;
        }
        iphoneCalibrating = false;
        iphoneCalibrated = true;
        iphoneMarkerValid = false;
        iphoneStatus = $"화면 보정 완료 · {IPhoneAccuracyText()}";
    }

    void DrawIPhoneCalibrationScreen()
    {
        if (DrawGazeTargetScreen(IPhoneCalibrationTargets, iphoneCalibrationPoint, iphoneMeasuring, iphoneTargetShownAt, iphoneMeasureAt,
                IPhoneCalibrationSettleSeconds, iphoneStatus, "분홍 점을 바라봐 주세요 · 평소처럼 고개를 조금 돌려도 괜찮아요."))
        {
            iphoneCalibrating = iphoneMeasuring = false;
            iphoneMarkerValid = false;
            iphoneStatus = "보정을 취소했어요.";
        }
    }

    // Full-screen target screen shared by the iPhone calibration and the webcam screen fit. True when cancelled.
    bool DrawGazeTargetScreen(Vector2[] targets, int index, bool measuring, float shownAt, float measureAt, float settleSeconds, string status, string hint)
    {
        GUI.matrix = Canvas;
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
        NovelUI.Label(new Rect(0, 20, 1280, 48), "시선 보정", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        // Text sits in the gaps between target rows (y 108 / 279 / 450 / 621 / 792) so no target lands on it.
        NovelUI.Label(new Rect(240, 156, 800, 34), $"보정 지점 {index + 1} / {targets.Length}", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);

        float phaseProgress = measuring
            ? .35f + Mathf.Clamp01((Time.unscaledTime - measureAt) / IPhoneCalibrationCaptureSeconds) * .65f
            : Mathf.Clamp01((Time.unscaledTime - shownAt) / settleSeconds) * .35f;
        float progress = (index + phaseProgress) / targets.Length;
        NovelUI.Fill(new Rect(240, 196, 800, 8), NovelUI.Alpha(NovelUI.Plum, .16f), 4);
        NovelUI.Fill(new Rect(240, 196, 800 * progress, 8), NovelUI.DeepPink, 4);

        Vector2 target = targets[Mathf.Min(index, targets.Length - 1)];
        Vector2 point = new Vector2(target.x * 1280f, target.y * 900f);
        NovelUI.Fill(new Rect(point.x - 31, point.y - 31, 62, 62), NovelUI.Alpha(NovelUI.DeepPink, .18f), 31);
        NovelUI.Fill(new Rect(point.x - 22, point.y - 22, 44, 44), NovelUI.DeepPink, 22);
        NovelUI.Fill(new Rect(point.x - 15, point.y - 15, 30, 30), NovelUI.Cream, 15);
        NovelUI.Fill(new Rect(point.x - 5, point.y - 5, 10, 10), NovelUI.DeepPink, 5);

        string instruction = measuring
            ? $"시선을 점에 고정해 주세요 · 자동 측정 중 {Mathf.CeilToInt(Mathf.Max(0f, IPhoneCalibrationCaptureSeconds - (Time.unscaledTime - measureAt)))}초"
            : hint;
        NovelUI.Label(new Rect(150, 668, 980, 42), instruction, NovelUI.Soft, 20, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
        NovelUI.Label(new Rect(150, 712, 980, 30), status, NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleCenter);
        return NovelUI.Button(new Rect(1040, 24, 190, 46), "보정 취소", false, 18);
    }

    bool FitIPhoneCalibration() => iphoneMapper.Fit(iphoneMeasuredFeatures, IPhoneCalibrationTargets, iphoneMeasuredSamples);

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
            NovelUI.Label(new Rect(120, 226, 1040, 42), status, NovelUI.Soft, 20, faceFound ? NovelUI.Mint : NovelUI.DeepPink, TextAnchor.MiddleCenter, FontStyle.Bold);

            if (!iphoneCalibrated)
            {
                if (iphoneUncalibrated && iphoneMarkerValid)
                    DrawIPhoneGazePointer(iphoneCanvasGaze);
                NovelUI.Label(new Rect(230, 310, 820, 72), iphoneUncalibrated
                    ? "무보정 원시 눈값을 점으로 표시합니다.\n방향 확인용이며 화면 위치 정확도는 보정이 필요해요."
                    : "화면 점 13개를 차례로 응시해 보정합니다.\n눈과 고개 방향을 함께 쓰니 평소처럼 바라보면 돼요.",NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter);
                GUI.enabled = fresh && iphoneSource.FaceFound;
                if (!iphoneUncalibrated)
                {
                    if (NovelUI.Button(new Rect(440, 410, 400, 62), "화면 보정 시작", true, 23)) BeginIPhoneCalibration();
                    if (NovelUI.Button(new Rect(440, 490, 400, 52), "무보정 원시값 보기", false, 20)) iphoneUncalibrated = true;
                }
                else
                {
                    Vector2 feature = iphoneSource.EyeFeature;
                    NovelUI.Label(new Rect(80, 742, 1120, 32), $"Raw Eye {feature.x:F2}, {feature.y:F2} · 화면 보정 후 화면 위치를 맞출 수 있어요.", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
                    if (NovelUI.Button(new Rect(500, 785, 280, 50), "화면 보정 시작", true, 19)) BeginIPhoneCalibration();
                }
                GUI.enabled = true;
            }
            else
            {
                if (iphoneMarkerValid) DrawIPhoneGazePointer(iphoneCanvasGaze);
                Vector2 feature = iphoneSource.EyeFeature;
                string transport = iphoneUseUsb ? "iPhone USB → VTube Studio API" : "iPhone Wi-Fi UDP";
                NovelUI.Label(new Rect(80, 694, 1120, 32), $"{transport} · {iphoneSource.Sequence} samples · Eye {feature.x:F2}, {feature.y:F2}", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
                if (NovelUI.Button(new Rect(270, 740, 280, 54), "시선 별잡기", true, 21)) StartStarGame();
                if (NovelUI.Button(new Rect(580, 740, 200, 54), "다시 보정", false, 19)) BeginIPhoneCalibration();
            }

            if (NovelUI.Button(iphoneCalibrated ? new Rect(810, 740, 200, 54) : new Rect(890, 785, 280, 50), "연결 끊기", false, 18)) StopIPhoneGaze();
        }
    }

    void DrawIPhoneGazePointer(Vector2 point, float alpha = 1f)
    {
        NovelUI.Fill(new Rect(point.x - 28, point.y - 28, 56, 56), NovelUI.Alpha(NovelUI.DeepPink, .22f * alpha), 28);
        NovelUI.Fill(new Rect(point.x - 17, point.y - 17, 34, 34), NovelUI.Alpha(NovelUI.DeepPink, alpha), 17);
        NovelUI.Fill(new Rect(point.x - 6, point.y - 6, 12, 12), NovelUI.Cream, 6);
    }
}
