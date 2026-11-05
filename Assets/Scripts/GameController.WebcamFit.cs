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
    readonly GazeSmoother webcamSmoother = new GazeSmoother(.8f, .006f, 1f, 30f);   // webcam frames scatter more: stronger smoothing and a wider hold
    readonly float[] webcamShapes = new float[10];
    readonly List<double[]> webcamCapture = new List<double[]>();
    readonly double[][] webcamMeasured = new double[IPhoneCalibrationTargets.Length][];
    readonly List<double[]>[] webcamMeasuredSamples = new List<double[]>[IPhoneCalibrationTargets.Length];
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
    Vector2 WebcamRawCanvasGaze => Canvas.inverse.MultiplyPoint3x4(gazeTracker.gazeLocation);

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
        // In the "정밀" model the ResNet34 face-gaze network reports gaze yaw/pitch, which already accounts for
        // head turn. It is a far steadier signal than webcam eyeLook blendshapes, so it takes the look slot.
        if (gazeTracker.GazeBackbone == UnitEye.GazeBackbone.EyeMUPlusResNet34)
        {
            float[] all = gazeTracker.Provider.GetFeatures();
            int at = UnitEye.HomulerEyeMURunner.FeatureCount;
            if (all != null && all.Length >= at + 2 && !float.IsNaN(all[at]) && !float.IsNaN(all[at + 1]))
                look = GazeOnScreenPlane(all[at], all[at + 1], all, HomulerEyeMURunner.ContextFeatureStart);
        }
        webcamLidsDown = gazeTracker.Blinking;
        webcamBlinking = ShortBlink(webcamLidsDown, hasShapes && lids >= WebcamBlinkClosed, ref webcamBlinkSince);

        double[] features = GazeMapper.Features(new Vector2(raw.x / 1280f, raw.y / 900f), new Vector2(pose.y, pose.x), look);
        // A sustained partial closure while looking down can be eyelid occlusion rather than a blink.
        // Only accept it when blendshapes confirm a downward look and the eye is not strongly closed.
        bool usableDownwardLook = hasShapes && shapeLookY < -.1f && lids < WebcamBlinkClosed && !webcamBlinking;
        bool usableForFit = !webcamLidsDown || usableDownwardLook;
        if (webcamFitMeasuring && usableForFit && Time.unscaledTime <= webcamMeasureAt + WebcamCaptureSeconds &&
            (!WebcamFitOnlyGaze || rawGazeFinite))
            webcamCapture.Add(features);

        if (!webcamFitted) return;
        if (WebcamFitOnlyGaze && !rawGazeFinite) { webcamMarkerValid = false; return; }
        if (webcamBlinking && webcamMarkerValid) return;   // hold the pointer through a real blink
        Vector2 p = webcamMapper.Predict(features);
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
        if (webcamFitPoint < IPhoneCalibrationTargets.Length)
        {
            webcamTargetShownAt = Time.unscaledTime;
            webcamFitStatus = $"다음 점을 바라봐 주세요. ({webcamFitPoint + 1}/{IPhoneCalibrationTargets.Length})";
            return;
        }
        webcamFitting = false;
        if (!FitAndCheck(webcamMapper, webcamMeasured, webcamMeasuredSamples, requireVerticalEye: false))
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
        webcamFitOnlySession = false;
        if (gazeTracker != null) gazeTracker.onlineDriftCorrection = true;
    }

    string WebcamFitAccuracyText() => GazeAccuracyText(webcamMapper, 110f, 190f);

    void DrawWebcamFitScreen()
    {
        if (DrawGazeTargetScreen(IPhoneCalibrationTargets, webcamFitPoint, webcamFitMeasuring, webcamTargetShownAt, webcamMeasureAt,
                WebcamSettleSeconds, webcamFitStatus, "분홍 점을 바라봐 주세요 · 노트북 화면 아래쪽 점도 눈으로 끝까지 봐 주세요."))
        {
            CancelWebcamFit();
            webcamFitStatus = "화면 맞춤을 취소했어요.";
            if (eyeContactPreparationMode) CancelEyeContactPreparation();
        }
    }
}
