using System.Collections.Generic;
using UnitEye;
using UnityEngine;

// Webcam "screen fit": 13 fixation targets plus 3 checks. The regular test layers this on UnitEye's calibration;
// mandatory preparation may skip that step and fit from whichever gaze, head and eye-angle inputs move.
public partial class GameController
{
    // 16 points × (.6 + .9) s ≈ 24 s, so the screen fit stays near 30 s even with a retry or two.
    const float WebcamSettleSeconds = .6f, WebcamCaptureSeconds = .9f;
    const float BlinkMaxSeconds = .45f;     // longer "closed" readings are lids lowered for a downward glance
    const float WebcamBlinkClosed = .85f;   // eyeBlink blendshape this high is a real closure however long it lasts

    readonly GazeMapper webcamMapper = new GazeMapper();
    // UnitEye already smooths its output, so this filter is lighter than the iPhone one.
    readonly GazeSmoother webcamSmoother = new GazeSmoother(.8f, .006f, 1f, 12f);   // UnitEye already filters; keep only a small fixation hold
    readonly float[] webcamShapes = new float[10];
    readonly List<double[]> webcamCapture = new List<double[]>();
    // Hybrid screen fit (Saxena et al., ETRA 2022: fixation + smooth pursuit together calibrated webcams best):
    // a 3x3 grid of fixations, a 12 s pursuit along a figure-eight, then 3 held-out check points. ~30 s total.
    static readonly Vector2[] WebcamFitTargets =
    {
        new Vector2(.08f, .12f), new Vector2(.50f, .12f), new Vector2(.92f, .12f),
        new Vector2(.08f, .50f), new Vector2(.50f, .50f), new Vector2(.92f, .50f),
        new Vector2(.08f, .88f), new Vector2(.50f, .88f), new Vector2(.92f, .88f),
        new Vector2(.29f, .31f), new Vector2(.71f, .69f), new Vector2(.71f, .31f)
    };
    const int WebcamFitFixations = 9;
    const float PursuitLeadIn = 1f, PursuitSeconds = 12f;
    const float PursuitLag = .05f;          // eye-only lag behind a smooth, predictable target (pipeline latency comes from CaptureTimestamp)
    const float PursuitSegment = .5f;       // pursuit frames are pooled into half-second points for the fit
    readonly double[][] webcamMeasured = new double[WebcamFitTargets.Length][];
    readonly List<double[]>[] webcamMeasuredSamples = new List<double[]>[WebcamFitTargets.Length];
    bool webcamPursuit;
    float webcamPursuitStart;
    readonly List<(float time, double[] features)> webcamPursuitFrames = new List<(float, double[])>();
    readonly List<double[]> webcamPursuitFeatures = new List<double[]>();
    readonly List<Vector2> webcamPursuitTargets = new List<Vector2>();

    // Figure-eight over most of the screen; t is seconds since the dot started moving.
    static Vector2 PursuitTarget(float t)
    {
        float phase = 2f * Mathf.PI * Mathf.Clamp(t, 0f, PursuitSeconds) / PursuitSeconds;
        return new Vector2(.5f + .38f * Mathf.Sin(phase), .5f + .34f * Mathf.Sin(2f * phase));
    }

    static float WebcamFitTotalSeconds => WebcamFitTargets.Length * (WebcamSettleSeconds + WebcamCaptureSeconds) + PursuitLeadIn + PursuitSeconds;

    static float WebcamFitProgress(int index, float phaseProgress)
    {
        float done = (index + phaseProgress) * (WebcamSettleSeconds + WebcamCaptureSeconds) + (index >= WebcamFitFixations ? PursuitLeadIn + PursuitSeconds : 0f);
        return Mathf.Clamp01(done / WebcamFitTotalSeconds);
    }

    // When the camera frame behind this sample was captured. Labels must use this, not the arrival time,
    // or the processing delay (often 100-200ms) turns into a systematic offset along the pursuit path.
    float WebcamFrameTime()
    {
        float now = Time.unscaledTime;
        double captured = gazeTracker != null ? gazeTracker.CaptureTimestamp : 0;
        if (captured > 0 && captured <= now && now - captured < 1.0) return (float)captured;
        return now - .12f;   // no usable timestamp: assume a typical pipeline delay
    }

    void FinishWebcamPursuit()
    {
        webcamPursuit = false;
        webcamPursuitFeatures.Clear(); webcamPursuitTargets.Clear();
        for (float from = PursuitLeadIn + PursuitLag; from < PursuitLeadIn + PursuitSeconds; from += PursuitSegment)
        {
            var chunk = new List<double[]>(); Vector2 target = Vector2.zero;
            foreach (var f in webcamPursuitFrames)
            {
                float t = f.time - webcamPursuitStart;
                if (t < from || t >= from + PursuitSegment) continue;
                chunk.Add(f.features); target += PursuitTarget(t - PursuitLeadIn - PursuitLag);
            }
            if (chunk.Count < 3) continue;
            webcamPursuitFeatures.Add(GazeMapper.Median(chunk));
            webcamPursuitTargets.Add(target / chunk.Count);
        }
        webcamPursuitFrames.Clear();
        webcamTargetShownAt = Time.unscaledTime;
        webcamFitStatus = $"좋아요. 확인용 점 {WebcamFitTargets.Length - WebcamFitFixations}개만 더 봐 주세요.";
    }

    void DrawWebcamPursuitScreen()
    {
        GUI.matrix = Canvas;
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
        NovelUI.Label(new Rect(0, 20, 1280, 48), "시선 보정", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        float t = Time.unscaledTime - webcamPursuitStart;
        float before = WebcamFitFixations * (WebcamSettleSeconds + WebcamCaptureSeconds);
        float progress = (before + Mathf.Min(t, PursuitLeadIn + PursuitSeconds)) / WebcamFitTotalSeconds;
        NovelUI.Fill(new Rect(240, 196, 800, 8), NovelUI.Alpha(NovelUI.Plum, .16f), 4);
        NovelUI.Fill(new Rect(240, 196, 800 * Mathf.Clamp01(progress), 8), NovelUI.DeepPink, 4);
        Vector2 target = PursuitTarget(t - PursuitLeadIn);
        Vector2 point = new Vector2(target.x * 1280f, target.y * 900f);
        NovelUI.Fill(new Rect(point.x - 22, point.y - 22, 44, 44), NovelUI.DeepPink, 22);
        NovelUI.Fill(new Rect(point.x - 15, point.y - 15, 30, 30), NovelUI.Cream, 15);
        NovelUI.Fill(new Rect(point.x - 5, point.y - 5, 10, 10), NovelUI.DeepPink, 5);
        NovelUI.Label(new Rect(150, 156, 980, 34), t < PursuitLeadIn ? "점이 곧 움직여요 · 고개는 그대로, 눈으로만 따라가 주세요" : "움직이는 점을 눈으로 따라가 주세요",
            NovelUI.Soft, 20, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (NovelUI.Button(new Rect(1040, 24, 190, 46), "보정 취소", false, 18))
        {
            CancelWebcamFit();
            webcamFitStatus = "화면 맞춤을 취소했어요.";
            if (eyeContactPreparationMode) CancelEyeContactPreparation();
        }
    }
    bool webcamFitting, webcamFitMeasuring, webcamFitted, webcamFitOnlySession, webcamMarkerValid, webcamBlinking, webcamLidsDown;
    int webcamFitPoint;
    float webcamTargetShownAt, webcamMeasureAt, webcamBlinkSince = -1f;
    Vector2 webcamCanvasGaze;
    string webcamFitStatus = "";
    bool WebcamFitOnlyGaze => eyeContactPreparationMode || webcamFitOnlySession;

    /// <summary>True while the eyes are really shut: closed briefly, or closed hard. A long half-closure is a downward look.</summary>
    static bool ShortBlink(bool lidsDown, bool certainlyClosed, ref float since)
    {
        if (!lidsDown) { since = -1f; return false; }
        if (since < 0f) since = Time.unscaledTime;
        return certainlyClosed || Time.unscaledTime - since < BlinkMaxSeconds;
    }

    // gazeLocation is already GUI space (y down): HomulerGaze draws its own dot at it with GUI.DrawTexture.
    // Unfiltered: UnitEye's One-Euro lag would otherwise pair a delayed gaze with the pursuit target at capture time.
    Vector2 WebcamRawCanvasGaze => Canvas.inverse.MultiplyPoint3x4(gazeTracker.UnfilteredGazeLocation);

    static bool IsFiniteWebcamPoint(Vector2 point) =>
        !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y);

    /// <summary>The pointer the game uses: the screen-fitted point when available, UnitEye's otherwise.</summary>
    // Before the screen fit, the UnitEye point gets only the fixation hold (UnitEye already smooths it).
    readonly GazeSmoother webcamRawHold = new GazeSmoother(4f, .006f, 1f, 30f);
    Vector2 webcamRawShown;
    Vector2 WebcamCanvasGaze => webcamFitted ? webcamCanvasGaze : webcamRawShown;

    // Called once per fresh UnitEye sample.
    void OnWebcamSample()
    {
        // Without a face the head pose and blendshapes are stale; never fit or point with them.
        if (!gazeTracker.IsUserPresent) { webcamMarkerValid = false; return; }
        Vector2 raw = WebcamRawCanvasGaze;
        bool rawGazeFinite = IsFiniteWebcamPoint(raw);
        if (WebcamFitOnlyGaze && !rawGazeFinite)
            raw = Vector2.zero;   // keep the feature finite; the invalid frame is excluded from fit samples below
        Vector3 pose = gazeTracker.Provider.HeadPoseEuler;   // pitch, yaw, roll
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
        float shapeLookY = look.y;   // blendshape up/down, kept for the eyelid check below
        gazeLabWebcamHeadPosition = Vector3.zero;
        gazeLabWebcamRay = Vector2.zero;
        // In the "정밀" model the ResNet34 face-gaze network reports gaze yaw/pitch, which already accounts for
        // head turn. It is a far steadier signal than webcam eyeLook blendshapes, so it takes the look slot.
        if (gazeTracker.GazeBackbone == UnitEye.GazeBackbone.EyeMUPlusResNet34)
        {
            float[] all = gazeTracker.Provider.GetFeatures();
            int at = UnitEye.HomulerEyeMURunner.FeatureCount;
            if (all != null && all.Length >= at + 2 && !float.IsNaN(all[at]) && !float.IsNaN(all[at + 1]))
            {
                gazeLabWebcamRay = new Vector2(all[at], all[at + 1]);
                int context = HomulerEyeMURunner.ContextFeatureStart;
                if (all.Length >= context + 3)
                    gazeLabWebcamHeadPosition = new Vector3(all[context], all[context + 1], all[context + 2]);
                look = GazeOnScreenPlane(all[at], all[at + 1], all, context);
            }
        }
        webcamLidsDown = gazeTracker.Blinking;
        webcamBlinking = ShortBlink(webcamLidsDown, hasShapes && lids >= WebcamBlinkClosed, ref webcamBlinkSince);

        double[] features = GazeMapper.Features(new Vector2(raw.x / 1280f, raw.y / 900f), new Vector2(pose.y, pose.x), look);
        gazeLabWebcamFeatures = features;
        // A sustained partial closure while looking down can be eyelid occlusion rather than a blink.
        // Only accept it when blendshapes confirm a downward look and the eye is not strongly closed.
        bool usableDownwardLook = hasShapes && shapeLookY < -.1f && lids < WebcamBlinkClosed && !webcamBlinking;
        bool usableForFit = !webcamLidsDown || usableDownwardLook;
        if (webcamFitMeasuring && usableForFit && Time.unscaledTime <= webcamMeasureAt + WebcamCaptureSeconds &&
            (!WebcamFitOnlyGaze || rawGazeFinite))
            webcamCapture.Add(features);
        if (webcamPursuit && usableForFit && !webcamBlinking && (!WebcamFitOnlyGaze || rawGazeFinite))
            webcamPursuitFrames.Add((WebcamFrameTime(), features));

        if (!webcamFitted) return;
        if (WebcamFitOnlyGaze && !rawGazeFinite) { webcamMarkerValid = false; return; }
        if (webcamBlinking && webcamMarkerValid) return;   // hold the pointer through a real blink
        Vector2 p = webcamMapper.Predict(features);
        RecordClickGaze(features, new Vector2(p.x * 1280f, p.y * 900f));
        if (!webcamMarkerValid) webcamSmoother.Reset();
        webcamCanvasGaze = webcamSmoother.Filter(new Vector2(p.x * 1280f, p.y * 900f), Time.unscaledTime);
        webcamMarkerValid = true;
    }

    /// <summary>
    /// Where the gaze ray meets the screen plane, in metres relative to the camera (Falch & Lohan 2024;
    /// WebEyeTrack 2025): head position + distance · tan(gaze angle). The angles alone change when the head
    /// slides sideways or leans in while the eyes stay on the same spot; this point does not, so the fit
    /// only has to learn a near-linear camera-to-screen map. Falls back to the raw angles without depth.
    /// </summary>
    static Vector2 GazeOnScreenPlane(float yaw, float pitch, float[] features, int context)
    {
        if (features.Length < context + 3) return new Vector2(yaw, pitch);
        float tx = features[context], ty = features[context + 1], distance = features[context + 2];
        if (!(distance > .15f && distance < 2f)) return new Vector2(yaw, pitch);   // no metric head pose this frame
        const float MaxAngle = 1.2f;   // ~70°, keeps tan finite on a bad frame
        return new Vector2(tx + distance * Mathf.Tan(Mathf.Clamp(yaw, -MaxAngle, MaxAngle)),
            ty + distance * Mathf.Tan(Mathf.Clamp(pitch, -MaxAngle, MaxAngle)));
    }

    void TickWebcamFit()
    {
        if (!webcamFitting) return;
        if (webcamPursuit)
        {
            if (Time.unscaledTime - webcamPursuitStart >= PursuitLeadIn + PursuitSeconds) FinishWebcamPursuit();
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

    void BeginWebcamFit()
    {
        StopStarGame();
        webcamFitOnlySession = eyeContactPreparationMode;
        // UnitEye's drift layer would shift its output under the new fit, so the fit's own anchors take over.
        gazeTracker.onlineDriftCorrection = false;
        webcamFitting = true;
        webcamFitMeasuring = webcamFitted = webcamMarkerValid = false;
        webcamFitPoint = 0;
        webcamPursuit = false;
        webcamPursuitFrames.Clear(); webcamPursuitFeatures.Clear(); webcamPursuitTargets.Clear();
        webcamTargetShownAt = Time.unscaledTime;
        webcamFitStatus = eyeContactPreparationMode
            ? "화면의 점을 차례로 바라봐 주세요. 각 점은 자동으로 측정돼요."
            : "화면 점을 바라봐 주세요. 자동으로 표본을 모읍니다.";
    }

    void CompleteWebcamFitPoint()
    {
        webcamFitMeasuring = false;
        // A webcam gives 10–30 samples a second, so .9 s holds far fewer than the iPhone's 60.
        if (webcamCapture.Count < 5)
        {
            webcamTargetShownAt = Time.unscaledTime;
            webcamFitStatus = !gazeTracker.IsUserPresent ? "얼굴이 잡히지 않았어요. 웹캠을 바라보고 같은 점을 다시 응시해 주세요."
                : eyeContactPreparationMode ? "측정 신호가 부족했어요. 얼굴이 잘 보이도록 하고 같은 점을 다시 바라봐 주세요."
                : "표본이 부족했어요(눈 감김 포함). 같은 점을 다시 바라봐 주세요.";
            return;
        }
        var chosen = new List<double[]>(webcamCapture);
        webcamMeasured[webcamFitPoint] = GazeMapper.Median(chosen);
        webcamMeasuredSamples[webcamFitPoint] = chosen;
        webcamFitPoint++;
        NovelUI.Sound?.Invoke("Select");
        if (webcamFitPoint == WebcamFitFixations)
        {
            webcamPursuit = true;
            webcamPursuitStart = Time.unscaledTime;
            webcamPursuitFrames.Clear();
            webcamFitStatus = "움직이는 점을 눈으로 따라가 주세요.";
            return;
        }
        if (webcamFitPoint < WebcamFitTargets.Length)
        {
            webcamTargetShownAt = Time.unscaledTime;
            webcamFitStatus = $"다음 점을 바라봐 주세요. ({webcamFitPoint + 1}/{WebcamFitTargets.Length})";
            return;
        }
        webcamFitting = false;
        if (!FitAndCheck(webcamMapper, WebcamFitTargets, WebcamFitFixations, webcamMeasured, webcamMeasuredSamples, false,
                webcamPursuitFeatures, webcamPursuitTargets))
        {
            CancelWebcamFit();
            webcamFitStatus = eyeContactPreparationMode
                ? "점마다 시선 값이 충분히 달라지지 않았어요. 얼굴이 잘 보이게 하고 다시 해 주세요."
                : "점마다 시선 값이 충분히 달라지지 않았어요. 먼저 ‘1. 시선 보정’을 하고 다시 해 주세요.";
            return;
        }
        webcamFitted = true;
        webcamMarkerValid = false;
        webcamFitStatus = $"화면 맞춤 완료 · {WebcamFitAccuracyText()}";
        TryCompleteEyeContactPreparation();
    }

    void CancelWebcamFit()
    {
        webcamFitting = webcamFitMeasuring = webcamFitted = webcamMarkerValid = false;
        webcamPursuit = false;
        webcamFitOnlySession = false;
        if (gazeTracker != null) gazeTracker.onlineDriftCorrection = true;
    }

    string WebcamFitAccuracyText() => GazeAccuracyText(webcamMapper, 110f, 190f);

    void DrawWebcamFitScreen()
    {
        if (webcamPursuit) { DrawWebcamPursuitScreen(); return; }
        if (DrawGazeTargetScreen(WebcamFitTargets, webcamFitPoint, webcamFitMeasuring, webcamTargetShownAt, webcamMeasureAt,
                WebcamSettleSeconds, webcamFitStatus, "분홍 점을 바라봐 주세요 · 노트북 화면 아래쪽 점도 눈으로 끝까지 봐 주세요."))
        {
            CancelWebcamFit();
            webcamFitStatus = "화면 맞춤을 취소했어요.";
            if (eyeContactPreparationMode) CancelEyeContactPreparation();
        }
    }
}
