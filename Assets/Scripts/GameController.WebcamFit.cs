using System;
using System.Collections.Generic;
using UnitEye;
using UnityEngine;

// Webcam calibration ("시선 보정"): one pass that also teaches the fit how the signals shift when the player moves.
// 9 points in the player's usual posture, 5 points each after moving left, moving right and leaning in, then 3
// held-out check points back in the usual posture. No UnitEye base calibration is needed: the fit reads the raw
// signals (iris, eyeLook blendshapes, ResNet-34 gaze, head pose and position) directly.
//
// Why (measured 2026-11-06 on two multi-posture recordings, Tools/GazeAnalysis): a fit from one posture, whatever
// its inputs, ran 300-10 000 px off once the player moved, because nothing in its points showed what moving does.
// Fitting across postures with the models below kept the error at 60/20 px in a calibrated posture and 120/60 px
// between them (horizontal/vertical); every other input set blew up on at least one posture.
public partial class GameController
{
    const float WebcamSettleSeconds = .6f, WebcamCaptureSeconds = 1f;
    const float BlinkMaxSeconds = .45f;     // longer "closed" readings are lids lowered for a downward glance
    const float WebcamBlinkClosed = .85f;   // eyeBlink blendshape this high is a real closure however long it lasts

    readonly GazeMapper webcamMapper = new GazeMapper();
    readonly GazeSmoother webcamSmoother = new GazeSmoother(.8f, .006f, 1f, 12f);   // small fixation hold only
    readonly float[] webcamShapes = new float[10];
    readonly List<double[]> webcamCapture = new List<double[]>();

    // Horizontal: iris offset + eyeLook shape for precise eye movement, the ResNet ray's screen point for where the head is.
    // Vertical: iris + ResNet pitch, with head pitch, height and distance (the camera sits above the screen).
    static readonly int[] WebcamModelX = { GazeMapper.WebIrisX, GazeMapper.WebShapeX, GazeMapper.WebPlaneX };
    static readonly int[] WebcamModelY = { GazeMapper.WebIrisY, GazeMapper.WebResPitch, GazeMapper.WebHeadPitch, GazeMapper.WebHeadY, GazeMapper.WebDistance };

    static readonly Vector2[] WebcamGrid =
    {
        new Vector2(.08f, .12f), new Vector2(.50f, .12f), new Vector2(.92f, .12f),
        new Vector2(.08f, .50f), new Vector2(.50f, .50f), new Vector2(.92f, .50f),
        new Vector2(.08f, .88f), new Vector2(.50f, .88f), new Vector2(.92f, .88f),
    };
    static readonly Vector2[] WebcamCheckPoints = { new Vector2(.29f, .31f), new Vector2(.71f, .69f), new Vector2(.71f, .31f) };
    static readonly string[] WebcamPostures =
    {
        "평소 게임하는 자세로 앉아 주세요.",
        "몸을 왼쪽으로 10cm쯤 옮겨 주세요. 얼굴은 화면 쪽으로.",
        "이번엔 몸을 오른쪽으로 10cm쯤 옮겨 주세요.",
        "화면 쪽으로 10~15cm 다가가 주세요.",
        "다시 평소 자세로 돌아와 주세요. 확인용 점 3개예요.",
    };
    static readonly int[] FivePoints = { 0, 2, 4, 6, 8 };   // corners and centre of the grid

    static Vector2[] webcamFitTargets;
    static int[] webcamFitPostures;
    static void BuildWebcamSequence()
    {
        if (webcamFitTargets != null) return;
        var targets = new List<Vector2>(); var postures = new List<int>();
        foreach (Vector2 p in WebcamGrid) { targets.Add(p); postures.Add(0); }
        for (int posture = 1; posture <= 3; posture++)
            foreach (int i in FivePoints) { targets.Add(WebcamGrid[i]); postures.Add(posture); }
        foreach (Vector2 p in WebcamCheckPoints) { targets.Add(p); postures.Add(4); }
        webcamFitTargets = targets.ToArray(); webcamFitPostures = postures.ToArray();
    }
    static Vector2[] WebcamFitTargets { get { BuildWebcamSequence(); return webcamFitTargets; } }
    static int WebcamFitPoints => WebcamFitTargets.Length - WebcamCheckPoints.Length;
    // The accuracy check: the grid and the check points in whatever posture the player is in now; nothing is refit.
    static readonly Vector2[] WebcamCheckTargets =
    {
        new Vector2(.08f, .12f), new Vector2(.50f, .12f), new Vector2(.92f, .12f),
        new Vector2(.08f, .50f), new Vector2(.50f, .50f), new Vector2(.92f, .50f),
        new Vector2(.08f, .88f), new Vector2(.50f, .88f), new Vector2(.92f, .88f),
        new Vector2(.29f, .31f), new Vector2(.71f, .69f), new Vector2(.71f, .31f),
    };
    Vector2[] WebcamSequence => webcamValidating ? WebcamCheckTargets : WebcamFitTargets;

    readonly double[][] webcamMeasured = new double[32][];
    readonly List<double[]>[] webcamMeasuredSamples = new List<double[]>[32];
    bool webcamFitting, webcamFitMeasuring, webcamFitted, webcamFitOnlySession, webcamMarkerValid, webcamBlinking, webcamLidsDown;
    bool webcamPostureWaiting;   // between postures: the player moves, then presses 준비됐어요 / Space
    bool webcamValidating;
    readonly double[][] webcamCheckMeasured = new double[WebcamCheckTargets.Length][];
    readonly List<double[]>[] webcamCheckSamples = new List<double[]>[WebcamCheckTargets.Length];
    int webcamFitPoint;
    float webcamTargetShownAt, webcamMeasureAt, webcamBlinkSince = -1f;
    Vector2 webcamCanvasGaze;
    string webcamFitStatus = "";
    bool WebcamFitOnlyGaze => eyeContactPreparationMode || webcamFitOnlySession;

    // ResNet-34 angles jump on single frames; a running median of three keeps those out (p95 683 -> 507 px).
    readonly float[] webcamYawHistory = new float[3], webcamPitchHistory = new float[3];
    int webcamAngleCount;

    /// <summary>True while the eyes are really shut: closed briefly, or closed hard. A long half-closure is a downward look.</summary>
    static bool ShortBlink(bool lidsDown, bool certainlyClosed, ref float since)
    {
        if (!lidsDown) { since = -1f; return false; }
        if (since < 0f) since = Time.unscaledTime;
        return certainlyClosed || Time.unscaledTime - since < BlinkMaxSeconds;
    }

    // gazeLocation is already GUI space (y down): HomulerGaze draws its own dot at it with GUI.DrawTexture.
    Vector2 WebcamRawCanvasGaze => Canvas.inverse.MultiplyPoint3x4(gazeTracker.UnfilteredGazeLocation);

    static bool IsFiniteWebcamPoint(Vector2 point) =>
        !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y);

    // When the camera frame behind this sample was captured (click learning lines clicks up with it).
    float WebcamFrameTime()
    {
        float now = Time.unscaledTime;
        double captured = gazeTracker != null ? gazeTracker.CaptureTimestamp : 0;
        if (captured > 0 && captured <= now && now - captured < 1.0) return (float)captured;
        return now - .12f;   // no usable timestamp: assume a typical pipeline delay
    }

    /// <summary>The pointer the game uses: the fitted point when available, UnitEye's (uncalibrated) otherwise.</summary>
    readonly GazeSmoother webcamRawHold = new GazeSmoother(4f, .006f, 1f, 30f);
    Vector2 webcamRawShown;
    Vector2 WebcamCanvasGaze => webcamFitted ? webcamCanvasGaze : webcamRawShown;

    static float Median3(float[] v, int count)
    {
        if (count < 3) return v[(count - 1 + 3) % 3];
        float a = v[0], b = v[1], c = v[2];
        return Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
    }

    // Called once per fresh UnitEye sample.
    void OnWebcamSample()
    {
        // Without a face the head pose and blendshapes are stale; never fit or point with them.
        if (!gazeTracker.IsUserPresent) { webcamMarkerValid = false; return; }
        Vector2 raw = WebcamRawCanvasGaze;
        if (!IsFiniteWebcamPoint(raw)) raw = Vector2.zero;   // UnitEye's own point is only a fallback pointer
        Vector3 pose = gazeTracker.Provider.HeadPoseEuler;   // pitch, yaw, roll (radians)
        Vector2 look = Vector2.zero;
        float lids = gazeTracker.Blinking ? 1f : 0f;
        bool hasShapes = gazeTracker.Provider is IGazeRecordingSource source && source.TryCopyEyeBlendshapes(webcamShapes);
        if (hasShapes)
        {
            // [inL, outL, upL, downL, inR, outR, upR, downR, blinkL, blinkR], same meaning as ARKit's.
            look = new Vector2((webcamShapes[1] - webcamShapes[0] + webcamShapes[4] - webcamShapes[5]) * .5f,
                (webcamShapes[2] + webcamShapes[6] - webcamShapes[3] - webcamShapes[7]) * .5f);
            lids = Mathf.Max(webcamShapes[8], webcamShapes[9]);
        }
        webcamLidsDown = gazeTracker.Blinking;
        webcamBlinking = ShortBlink(webcamLidsDown, hasShapes && lids >= WebcamBlinkClosed, ref webcamBlinkSince);

        double[] features = WebcamFeatures(raw, pose, look, out bool complete);
        // A sustained partial closure while looking down can be eyelid occlusion rather than a blink.
        // Only accept it when blendshapes confirm a downward look and the eye is not strongly closed.
        bool usableDownwardLook = hasShapes && look.y < -.1f && lids < WebcamBlinkClosed && !webcamBlinking;
        bool usableForFit = complete && (!webcamLidsDown || usableDownwardLook);
        if (webcamFitMeasuring && usableForFit && Time.unscaledTime <= webcamMeasureAt + WebcamCaptureSeconds)
            webcamCapture.Add(features);

        if (!webcamFitted) return;
        if (!complete) { webcamMarkerValid = false; return; }
        if (webcamBlinking && webcamMarkerValid) return;   // hold the pointer through a real blink
        Vector2 p = webcamMapper.Predict(features);
        RecordClickGaze(features, new Vector2(p.x * 1280f, p.y * 900f));
        if (!webcamMarkerValid) webcamSmoother.Reset();
        webcamCanvasGaze = webcamSmoother.Filter(new Vector2(p.x * 1280f, p.y * 900f), Time.unscaledTime);
        webcamMarkerValid = true;
    }

    /// <summary>GazeMapper.Features plus the webcam extras (GazeMapper.Web*). complete is false while the ResNet
    /// gaze or the metric head pose is missing this frame; such a frame is neither fitted nor pointed with.</summary>
    double[] WebcamFeatures(Vector2 raw, Vector3 pose, Vector2 look, out bool complete)
    {
        var f = new double[GazeMapper.WebFeatureCount];
        double[] basic = GazeMapper.Features(new Vector2(raw.x / 1280f, raw.y / 900f), new Vector2(pose.y, pose.x), look);
        Array.Copy(basic, f, basic.Length);
        f[GazeMapper.WebShapeX] = look.x; f[GazeMapper.WebShapeY] = look.y;
        f[GazeMapper.WebHeadYaw] = pose.y; f[GazeMapper.WebHeadPitch] = pose.x;
        complete = false;
        float[] all = gazeTracker.Provider.GetFeatures();
        int at = HomulerEyeMURunner.FeatureCount, c = HomulerEyeMURunner.ContextFeatureStart, iris = HomulerEyeMURunner.IrisFeatureStart;
        if (gazeTracker.GazeBackbone != GazeBackbone.EyeMUPlusResNet34 || all == null || all.Length < at + 2) return f;
        if (float.IsNaN(all[at]) || float.IsNaN(all[at + 1])) return f;
        webcamYawHistory[webcamAngleCount % 3] = all[at];
        webcamPitchHistory[webcamAngleCount % 3] = all[at + 1];
        webcamAngleCount++;
        float yaw = Median3(webcamYawHistory, webcamAngleCount), pitch = Median3(webcamPitchHistory, webcamAngleCount);
        float tx = all[c], ty = all[c + 1], distance = all[c + 2];
        Vector2 plane = GazeOnScreenPlane(yaw, pitch, tx, ty, distance);
        f[GazeMapper.WebResYaw] = yaw; f[GazeMapper.WebResPitch] = pitch;
        f[GazeMapper.WebHeadX] = tx; f[GazeMapper.WebHeadY] = ty; f[GazeMapper.WebDistance] = distance;
        f[GazeMapper.WebIrisX] = (all[iris] + all[iris + 2]) * .5f; f[GazeMapper.WebIrisY] = (all[iris + 1] + all[iris + 3]) * .5f;
        f[GazeMapper.WebPlaneX] = plane.x; f[GazeMapper.WebPlaneY] = plane.y;
        complete = distance > .15f && distance < 2f;
        foreach (double v in f) if (double.IsNaN(v) || double.IsInfinity(v)) complete = false;
        return f;
    }

    /// <summary>
    /// Where the ResNet gaze ray meets the screen plane, in metres from the camera (Falch & Lohan 2024): head
    /// position + distance · tan(angle / ResNetAngleGain).
    /// </summary>
    static Vector2 GazeOnScreenPlane(float yaw, float pitch, float tx, float ty, float distance)
    {
        const float MaxAngle = 1.2f;   // ~70°, keeps tan finite on a bad frame
        return new Vector2(tx + distance * Mathf.Tan(Mathf.Clamp(yaw / ResNetAngleGain, -MaxAngle, MaxAngle)),
            ty + distance * Mathf.Tan(Mathf.Clamp(pitch / ResNetAngleGain, -MaxAngle, MaxAngle)));
    }

    /// <summary>
    /// How much of a real gaze turn the ResNet-34 (Gaze360) angles report on a laptop webcam: the same screen
    /// point seen from 15 cm further right moved the reported yaw by about a third of the true change (2026-11-06).
    /// </summary>
    const float ResNetAngleGain = .37f;

    void TickWebcamFit()
    {
        if (!webcamFitting) return;
        if (webcamPostureWaiting)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) EndWebcamPostureWait();
            return;
        }
        if (!webcamFitMeasuring && Time.unscaledTime >= webcamTargetShownAt + WebcamSettleSeconds)
        {
            webcamCapture.Clear();
            webcamMeasureAt = Time.unscaledTime;
            webcamFitMeasuring = true;
            webcamFitStatus = "시선을 고정해 주세요. 잠깐 자동 측정합니다.";
        }
        if (webcamFitMeasuring && Time.unscaledTime > webcamMeasureAt + WebcamCaptureSeconds)
            CompleteWebcamFitPoint();
    }

    void EndWebcamPostureWait()
    {
        webcamPostureWaiting = false;
        webcamTargetShownAt = Time.unscaledTime;
        NovelUI.Sound?.Invoke("Select");
    }

    void BeginWebcamFit()
    {
        StopStarGame();
        BuildWebcamSequence();
        webcamFitOnlySession = true;   // the fit reads raw signals; UnitEye's own calibration is not used
        gazeTracker.onlineDriftCorrection = false;
        webcamFitting = true;
        webcamValidating = false;
        webcamFitMeasuring = webcamFitted = webcamMarkerValid = false;
        webcamFitPoint = 0;
        Array.Clear(webcamMeasured, 0, webcamMeasured.Length);
        Array.Clear(webcamMeasuredSamples, 0, webcamMeasuredSamples.Length);
        webcamPostureWaiting = true;   // first screen: sit as usual, then start
        webcamFitStatus = "";
    }

    void CompleteWebcamFitPoint()
    {
        webcamFitMeasuring = false;
        // About 9 samples a second at 720p, so a 1 s capture holds ~9; fewer than 5 means the face was lost or eyes shut.
        if (webcamCapture.Count < 5)
        {
            webcamTargetShownAt = Time.unscaledTime;
            webcamFitStatus = !gazeTracker.IsUserPresent ? "얼굴이 잡히지 않았어요. 웹캠을 바라보고 같은 점을 다시 응시해 주세요."
                : "측정 신호가 부족했어요(눈 감김 포함). 얼굴이 잘 보이도록 하고 같은 점을 다시 바라봐 주세요.";
            return;
        }
        var chosen = new List<double[]>(webcamCapture);
        (webcamValidating ? webcamCheckMeasured : webcamMeasured)[webcamFitPoint] = GazeMapper.Median(chosen);
        (webcamValidating ? webcamCheckSamples : webcamMeasuredSamples)[webcamFitPoint] = chosen;
        webcamFitPoint++;
        NovelUI.Sound?.Invoke("Select");
        Vector2[] sequence = WebcamSequence;
        if (webcamFitPoint < sequence.Length)
        {
            webcamTargetShownAt = Time.unscaledTime;
            webcamFitStatus = $"다음 점을 바라봐 주세요. ({webcamFitPoint + 1}/{sequence.Length})";
            if (!webcamValidating && webcamFitPostures[webcamFitPoint] != webcamFitPostures[webcamFitPoint - 1]) webcamPostureWaiting = true;
            return;
        }
        webcamFitting = false;
        if (webcamValidating)
        {
            webcamValidating = false;
            float error = LogGazeFit("webcam-check", webcamMapper, WebcamCheckTargets, webcamCheckMeasured, webcamCheckSamples, 0);
            webcamFitStatus = $"정확도 검사 · {WebcamCheckTargets.Length}점 평균 오차 약 {Mathf.RoundToInt(error)}px (로그와 파일에 저장)";
            return;
        }
        bool fitted = gazeTracker.GazeBackbone == GazeBackbone.EyeMUPlusResNet34 &&
            FitAndCheck(webcamMapper, WebcamFitTargets, WebcamFitPoints, webcamMeasured, webcamMeasuredSamples, false, null, null,
                WebcamModelX, WebcamModelY);
        if (!fitted)
        {
            CancelWebcamFit();
            webcamFitStatus = "점마다 시선 값이 충분히 달라지지 않았어요. 얼굴이 잘 보이게 하고 다시 해 주세요.";
            return;
        }
        webcamFitted = true;
        webcamMarkerValid = false;
        webcamFitStatus = $"시선 보정 완료 · {WebcamFitAccuracyText()}";
        LogGazeFit("webcam", webcamMapper, WebcamFitTargets, webcamMeasured, webcamMeasuredSamples, WebcamFitPoints);
        TryCompleteEyeContactPreparation();
    }

    void BeginWebcamCheck()
    {
        StopStarGame();
        webcamFitting = webcamValidating = true;
        webcamFitMeasuring = webcamPostureWaiting = false;
        webcamFitPoint = 0;
        Array.Clear(webcamCheckMeasured, 0, webcamCheckMeasured.Length);
        Array.Clear(webcamCheckSamples, 0, webcamCheckSamples.Length);
        webcamTargetShownAt = Time.unscaledTime;
        webcamFitStatus = "정확도 검사: 원하는 자세(옆으로 이동, 앞뒤로 기울이기)로 점을 바라봐 주세요.";
    }

    void CancelWebcamFit()
    {
        webcamPostureWaiting = false;
        if (webcamValidating)
        {
            // Stopping a check keeps the fit it was measuring.
            webcamValidating = webcamFitting = webcamFitMeasuring = false;
            return;
        }
        webcamFitting = webcamFitMeasuring = webcamFitted = webcamMarkerValid = false;
        webcamFitOnlySession = false;
        if (gazeTracker != null) gazeTracker.onlineDriftCorrection = true;
    }

    string WebcamFitAccuracyText() => GazeAccuracyText(webcamMapper, 110f, 190f);

    void DrawWebcamFitScreen()
    {
        if (webcamPostureWaiting)
        {
            DrawWebcamPostureScreen();
            return;
        }
        if (DrawGazeTargetScreen(WebcamSequence, webcamFitPoint, webcamFitMeasuring, webcamTargetShownAt, webcamMeasureAt,
                WebcamSettleSeconds, webcamFitStatus, "분홍 점을 눈으로 바라봐 주세요 · 자세는 그대로 유지해 주세요."))
            CancelWebcamFitByButton();
    }

    void CancelWebcamFitByButton()
    {
        CancelWebcamFit();
        webcamFitStatus = "시선 보정을 취소했어요.";
        if (eyeContactPreparationMode) CancelEyeContactPreparation();
    }

    void DrawWebcamPostureScreen()
    {
        GUI.matrix = Canvas;
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
        NovelUI.Label(new Rect(0, 20, 1280, 48), "시선 보정", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        int posture = webcamFitPostures[Mathf.Min(webcamFitPoint, webcamFitPostures.Length - 1)];
        int step = posture + 1, steps = WebcamPostures.Length;
        NovelUI.Label(new Rect(0, 230, 1280, 50), $"자세 {step} / {steps}", NovelUI.Round, 30, NovelUI.Plum, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(140, 300, 1000, 60), WebcamPostures[posture], NovelUI.Soft, 27, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
        string why = posture == 0
            ? "자세를 몇 번 바꿔 가며 점을 봐요(약 1분). 그래야 게임 중에 몸을 움직여도 시선이 맞아요."
            : "자세를 잡았으면 그대로 두고, 눈으로만 점을 따라가 주세요.";
        NovelUI.Label(new Rect(140, 370, 1000, 40), why, NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (NovelUI.Button(new Rect(490, 450, 300, 62), "준비됐어요 (Space)", true, 22)) EndWebcamPostureWait();
        if (NovelUI.Button(new Rect(1040, 24, 190, 46), "보정 취소", false, 18)) CancelWebcamFitByButton();
    }
}
