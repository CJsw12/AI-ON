using System.Collections.Generic;
using UnitEye;
using UnityEngine;

// Webcam "screen fit": 13 fixation targets measured on top of UnitEye's own calibration.
// UnitEye's ridge calibration pulls estimates toward the screen centre and a laptop camera above the screen sees
// the eyes worst when they look down, so the far sides and the bottom fall short. Fitting each axis again on
// fixations at those edges, with head pose and MediaPipe's eye-look blendshapes as extra inputs, stretches them back.
public partial class GameController
{
    const float BlinkMaxSeconds = .45f;     // longer "closed" readings are lids lowered for a downward glance
    const float WebcamBlinkClosed = .85f;   // eyeBlink blendshape this high is a real closure however long it lasts

    readonly GazeMapper webcamMapper = new GazeMapper();
    // UnitEye already smooths its output, so this filter is lighter than the iPhone one.
    readonly GazeSmoother webcamSmoother = new GazeSmoother(1.2f, .006f);
    readonly float[] webcamShapes = new float[10];
    readonly List<double[]> webcamCapture = new List<double[]>();
    readonly double[][] webcamMeasured = new double[IPhoneCalibrationTargets.Length][];
    readonly List<double[]>[] webcamMeasuredSamples = new List<double[]>[IPhoneCalibrationTargets.Length];
    bool webcamFitting, webcamFitMeasuring, webcamFitted, webcamMarkerValid, webcamBlinking, webcamLidsDown;
    int webcamFitPoint;
    float webcamTargetShownAt, webcamMeasureAt, webcamBlinkSince = -1f;
    Vector2 webcamCanvasGaze;
    string webcamFitStatus = "";

    /// <summary>True while the eyes are really shut: closed briefly, or closed hard. A long half-closure is a downward look.</summary>
    static bool ShortBlink(bool lidsDown, bool certainlyClosed, ref float since)
    {
        if (!lidsDown) { since = -1f; return false; }
        if (since < 0f) since = Time.unscaledTime;
        return certainlyClosed || Time.unscaledTime - since < BlinkMaxSeconds;
    }

    Vector2 WebcamRawCanvasGaze => Canvas.inverse.MultiplyPoint3x4(gazeTracker.gazeLocation);

    /// <summary>The pointer the game uses: the screen-fitted point when available, UnitEye's otherwise.</summary>
    Vector2 WebcamCanvasGaze => webcamFitted ? webcamCanvasGaze : WebcamRawCanvasGaze;

    // Called once per fresh UnitEye sample.
    void OnWebcamSample()
    {
        // Without a face the head pose and blendshapes are stale; never fit or point with them.
        if (!gazeTracker.IsUserPresent) { webcamMarkerValid = false; return; }
        Vector2 raw = WebcamRawCanvasGaze;
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
        webcamLidsDown = gazeTracker.Blinking;
        webcamBlinking = ShortBlink(webcamLidsDown, hasShapes && lids >= WebcamBlinkClosed, ref webcamBlinkSince);

        double[] features = GazeMapper.Features(new Vector2(raw.x / 1280f, raw.y / 900f), new Vector2(pose.y, pose.x), look);
        if (webcamFitMeasuring && !webcamLidsDown && Time.unscaledTime <= webcamMeasureAt + IPhoneCalibrationCaptureSeconds)
            webcamCapture.Add(features);

        if (!webcamFitted) return;
        if (webcamBlinking && webcamMarkerValid) return;   // hold the pointer through a real blink
        Vector2 p = webcamMapper.Predict(features);
        if (!webcamMarkerValid) webcamSmoother.Reset();
        webcamCanvasGaze = webcamSmoother.Filter(new Vector2(p.x * 1280f, p.y * 900f), Time.unscaledTime);
        webcamMarkerValid = true;
    }

    void TickWebcamFit()
    {
        if (!webcamFitting) return;
        if (!webcamFitMeasuring && Time.unscaledTime >= webcamTargetShownAt + IPhoneCalibrationSettleSeconds)
        {
            webcamCapture.Clear();
            webcamMeasureAt = Time.unscaledTime;
            webcamFitMeasuring = true;
            webcamFitStatus = "시선을 고정해 주세요. 1.2초 동안 자동 측정합니다.";
        }
        if (webcamFitMeasuring && Time.unscaledTime > webcamMeasureAt + IPhoneCalibrationCaptureSeconds)
            CompleteWebcamFitPoint();
    }

    void BeginWebcamFit()
    {
        StopStarGame();
        // UnitEye's drift layer would shift its output under the new fit, so the fit's own anchors take over.
        gazeTracker.onlineDriftCorrection = false;
        webcamFitting = true;
        webcamFitMeasuring = webcamFitted = webcamMarkerValid = false;
        webcamFitPoint = 0;
        webcamTargetShownAt = Time.unscaledTime;
        webcamFitStatus = "화면 점을 바라봐 주세요. 자동으로 표본을 모읍니다.";
    }

    void CompleteWebcamFitPoint()
    {
        webcamFitMeasuring = false;
        // A webcam gives 10–30 samples a second, so 1.2 s holds far fewer than the iPhone's 60.
        if (webcamCapture.Count < 6)
        {
            webcamTargetShownAt = Time.unscaledTime;
            webcamFitStatus = !gazeTracker.IsUserPresent ? "얼굴이 잡히지 않았어요. 웹캠을 바라보고 같은 점을 다시 응시해 주세요."
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
        if (!webcamMapper.Fit(webcamMeasured, IPhoneCalibrationTargets, webcamMeasuredSamples))
        {
            CancelWebcamFit();
            webcamFitStatus = "점마다 시선 값이 충분히 달라지지 않았어요. 먼저 ‘1. 시선 보정’을 하고 다시 해 주세요.";
            return;
        }
        webcamFitted = true;
        webcamMarkerValid = false;
        webcamFitStatus = $"화면 맞춤 완료 · {WebcamFitAccuracyText()}";
    }

    void CancelWebcamFit()
    {
        webcamFitting = webcamFitMeasuring = webcamFitted = webcamMarkerValid = false;
        if (gazeTracker != null) gazeTracker.onlineDriftCorrection = true;
    }

    string WebcamFitAccuracyText()
    {
        float error = webcamMapper.ErrorPixels;
        string grade = error < 110f ? "좋음" : error < 190f ? "보통" : "낮음 · 다시 해 보세요";
        return $"{webcamMapper.ModelName} · 오차 약 {Mathf.RoundToInt(error)}px · {grade}";
    }

    void DrawWebcamFitScreen()
    {
        if (DrawGazeTargetScreen(IPhoneCalibrationTargets, webcamFitPoint, webcamFitMeasuring, webcamTargetShownAt, webcamMeasureAt,
                IPhoneCalibrationSettleSeconds, webcamFitStatus, "분홍 점을 바라봐 주세요 · 노트북 화면 아래쪽 점도 눈으로 끝까지 봐 주세요."))
        {
            CancelWebcamFit();
            webcamFitStatus = "화면 맞춤을 취소했어요.";
        }
    }
}
