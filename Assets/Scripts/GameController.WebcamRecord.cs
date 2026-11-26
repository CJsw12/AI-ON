using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnitEye;
using UnityEngine;

// Research recording for the webcam gaze (editor / development builds): nine points from each of four
// postures, saving every raw signal on every frame to <persistentDataPath>/gaze-fits/record-*.jsonl.
// Nothing is fitted here; Tools/GazeAnalysis picks the model offline by how well a fit from some postures
// predicts the others, which a single-posture calibration cannot tell.
public partial class GameController
{
    static readonly Vector2[] RecordGrid =
    {
        new Vector2(.10f, .12f), new Vector2(.50f, .12f), new Vector2(.90f, .12f),
        new Vector2(.10f, .50f), new Vector2(.50f, .50f), new Vector2(.90f, .50f),
        new Vector2(.10f, .88f), new Vector2(.50f, .88f), new Vector2(.90f, .88f),
    };
    static readonly string[] RecordPostures =
    {
        "평소 자세로 편하게 앉아 주세요.",
        "몸을 왼쪽으로 10~15cm 옮겨 주세요. 고개는 화면 쪽으로.",
        "몸을 오른쪽으로 10~15cm 옮겨 주세요. 고개는 화면 쪽으로.",
        "화면 쪽으로 15cm쯤 다가가 주세요.",
    };
    // Iris centres, eye corners, upper/lower lids, nose tip, forehead and chin.
    static readonly int[] RecordLandmarks = { 468, 473, 33, 133, 362, 263, 159, 145, 386, 374, 1, 10, 152 };
    const float RecordSettleSeconds = .8f, RecordCaptureSeconds = 1.5f;

    bool webcamRecording, webcamRecordWaiting;
    int webcamRecordPoint;
    float webcamRecordShownAt;
    long webcamRecordSequence;
    StringBuilder webcamRecordLog;
    float[] webcamRecordLandmarks = new float[478 * 3];
    readonly float[] webcamRecordShapes = new float[10];

    bool WebcamRecordMeasuring => webcamRecording && !webcamRecordWaiting &&
        Time.unscaledTime >= webcamRecordShownAt + RecordSettleSeconds;

    void BeginWebcamRecording()
    {
        StopStarGame();
        webcamRecording = webcamRecordWaiting = true;
        webcamRecordPoint = 0;
        webcamRecordLog = new StringBuilder();
        webcamRecordSequence = gazeTracker != null ? gazeTracker.GazeSampleSequence : 0;
    }

    void StopWebcamRecording(bool save)
    {
        if (!webcamRecording) return;
        webcamRecording = false;
        if (!save || webcamRecordLog == null || webcamRecordLog.Length == 0) { webcamFitStatus = "자세별 기록을 취소했어요."; return; }
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "gaze-fits");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "record-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            File.WriteAllText(file, webcamRecordLog.ToString());
            Debug.Log("[gaze record] saved: " + file);
            webcamFitStatus = "자세별 기록 완료 · 파일로 저장했어요.";
        }
        catch (Exception e) { webcamFitStatus = "기록 저장 실패: " + e.Message; }
        webcamRecordLog = null;
    }

    // Every Unity frame while recording: one JSON line, stale or fresh, so the face-mesh rate is visible too.
    void TickWebcamRecording()
    {
        if (!webcamRecording || gazeTracker == null) return;
        if (webcamRecordWaiting)
        {
            bool go = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
            if (go) { webcamRecordWaiting = false; webcamRecordShownAt = Time.unscaledTime; }
            return;
        }
        if (Time.unscaledTime >= webcamRecordShownAt + RecordSettleSeconds + RecordCaptureSeconds)
        {
            NovelUI.Sound?.Invoke("Select");
            webcamRecordPoint++;
            if (webcamRecordPoint >= RecordGrid.Length * RecordPostures.Length) { StopWebcamRecording(true); return; }
            webcamRecordShownAt = Time.unscaledTime;
            if (webcamRecordPoint % RecordGrid.Length == 0) webcamRecordWaiting = true;
            return;
        }
        AppendRecordLine();
    }

    void AppendRecordLine()
    {
        var provider = gazeTracker.Provider;
        if (provider == null) return;
        long sequence = gazeTracker.GazeSampleSequence;
        bool fresh = sequence != webcamRecordSequence;
        webcamRecordSequence = sequence;
        Vector2 target = RecordGrid[webcamRecordPoint % RecordGrid.Length];
        var line = new StringBuilder(2048);
        line.Append("{\"t\":").Append(F(Time.unscaledTime))
            .Append(",\"posture\":").Append(webcamRecordPoint / RecordGrid.Length)
            .Append(",\"point\":").Append(webcamRecordPoint)
            .Append(",\"target\":[").Append(F(target.x)).Append(',').Append(F(target.y)).Append(']')
            .Append(",\"measuring\":").Append(WebcamRecordMeasuring ? "true" : "false")
            .Append(",\"fresh\":").Append(fresh ? "true" : "false")
            .Append(",\"seq\":").Append(sequence)
            .Append(",\"present\":").Append(gazeTracker.IsUserPresent ? "true" : "false")
            .Append(",\"blink\":").Append(gazeTracker.Blinking ? "true" : "false");
        Vector2 raw = WebcamRawCanvasGaze;
        line.Append(",\"raw\":[").Append(F(raw.x)).Append(',').Append(F(raw.y)).Append(']');
        Vector3 pose = provider.HeadPoseEuler;
        line.Append(",\"head\":[").Append(F(pose.x)).Append(',').Append(F(pose.y)).Append(',').Append(F(pose.z)).Append(']');
        Arr(line, "feat", provider.GetFeatures());
        if (provider is IGazeRecordingSource source)
        {
            if (source.TryCopyEyeBlendshapes(webcamRecordShapes)) Arr(line, "shapes", webcamRecordShapes);
            if (source.LandmarkCount * 3 > webcamRecordLandmarks.Length) webcamRecordLandmarks = new float[source.LandmarkCount * 3];
            if (source.TryCopyLandmarks(webcamRecordLandmarks) >= 478 * 3)
            {
                line.Append(",\"lm\":[");
                for (int i = 0; i < RecordLandmarks.Length; i++)
                {
                    int at = RecordLandmarks[i] * 3;
                    if (i > 0) line.Append(',');
                    line.Append(F(webcamRecordLandmarks[at])).Append(',').Append(F(webcamRecordLandmarks[at + 1])).Append(',').Append(F(webcamRecordLandmarks[at + 2]));
                }
                line.Append(']');
            }
            line.Append(",\"frame\":[").Append(source.FrameWidth).Append(',').Append(source.FrameHeight).Append(']');
        }
        line.Append("}\n");
        webcamRecordLog.Append(line);
    }

    static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "null" : v.ToString("R", CultureInfo.InvariantCulture);
    static void Arr(StringBuilder line, string name, float[] values)
    {
        if (values == null) return;
        line.Append(",\"").Append(name).Append("\":[");
        for (int i = 0; i < values.Length; i++) { if (i > 0) line.Append(','); line.Append(F(values[i])); }
        line.Append(']');
    }

    void DrawWebcamRecordScreen()
    {
        int posture = Mathf.Min(webcamRecordPoint / RecordGrid.Length, RecordPostures.Length - 1);
        if (webcamRecordWaiting)
        {
            GUI.matrix = Canvas;
            NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
            NovelUI.Label(new Rect(0, 250, 1280, 60), $"자세 {posture + 1} / {RecordPostures.Length}", NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(140, 330, 1000, 60), RecordPostures[posture], NovelUI.Soft, 26, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(140, 400, 1000, 40), "이 자세를 유지한 채 눈으로만 점을 따라가 주세요. 준비되면 클릭하거나 스페이스를 눌러요.", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(1040, 24, 190, 46), "기록 취소", false, 18)) StopWebcamRecording(false);
            return;
        }
        var targets = new Vector2[RecordGrid.Length * RecordPostures.Length];
        for (int i = 0; i < targets.Length; i++) targets[i] = RecordGrid[i % RecordGrid.Length];
        bool measuring = WebcamRecordMeasuring;
        if (DrawGazeTargetScreen(targets, webcamRecordPoint, measuring, webcamRecordShownAt, webcamRecordShownAt + RecordSettleSeconds,
                RecordSettleSeconds, RecordPostures[posture], "분홍 점을 눈으로 바라봐 주세요 · 자세는 그대로"))
            StopWebcamRecording(false);
    }
}
