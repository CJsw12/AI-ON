using System;
using System.IO;
using UnityEngine;

// Uses the same canvas and gaze pipeline as the games; recording never trains against the target.
public partial class GameController
{
    bool gazeLabOpen, gazeLabRecording, gazeLabPaused, gazeLabShowGaze = true;
    bool gazeLabPreviousDrift;
    GazeLabPath gazeLabPath = GazeLabPath.FigureEight;
    float gazeLabPeriod = 10, gazeLabDuration = 60, gazeLabElapsed, gazeLabNextPublish;
    double gazeLabLastObservation;
    long gazeLabLastObservedSequence = long.MinValue;
    int gazeLabLastFrame = -1;
    string gazeLabRevision = "baseline", gazeLabMessage = "먼저 추적 장치를 연결하고 보정해 주세요. 공의 움직임은 바로 미리 볼 수 있어요.";
    GazeLabSession gazeLabSession;
    double[] gazeLabWebcamFeatures;
    Vector3 gazeLabWebcamHeadPosition;
    Vector2 gazeLabWebcamRay;
    Vector2 gazeLabTarget;
    GazeLabReading gazeLabReading;
    readonly Vector2[] gazeLabTrail = new Vector2[90];
    int gazeLabTrailCount, gazeLabTrailAt;
    static readonly string[] GazeLabPaths = { "가로 왕복", "세로 왕복", "타원", "8자", "9점 고정" };

    string GazeLabDirectory => Application.isEditor
        ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/GazeLab"))
        : Path.Combine(Application.persistentDataPath, "GazeLab");

    void OpenGazeLab()
    {
        StopStarGame();
        gazeLabOpen = true;
        gazeLabElapsed = 0;
        gazeLabTarget = GazeLabMotion.Target(gazeLabPath, 0, gazeLabPeriod);
        gazeLabPaused = false;
        gazeLabTrailCount = gazeLabTrailAt = 0;
        if (gazeTracker != null)
        {
            gazeLabPreviousDrift = gazeTracker.onlineDriftCorrection;
            gazeTracker.onlineDriftCorrection = false;
        }
    }

    void CloseGazeLab()
    {
        EndGazeLabRecording("closed");
        if (gazeLabOpen && gazeTracker != null) gazeTracker.onlineDriftCorrection = gazeLabPreviousDrift;
        gazeLabOpen = false;
    }

    // Domain reload, stopping Play mode and disabling the controller must close the shared log handle.
    void OnDisable() => CloseGazeLab();

    GazeLabReading ReadGazeLab()
    {
        var r = new GazeLabReading();
        if (gazeUseIphone)
        {
            if (iphoneSource == null) return r;
            r.sequence = iphoneSource.Sequence;
            r.face = iphoneSource.FaceFound;
            r.age = Mathf.Max(0, Time.unscaledTime - iphoneLastLiveAt);
            r.fresh = iphoneSource.HasFreshSample && r.age <= .35f;
            r.marker = iphoneMarkerValid; r.blink = iphoneBlinking; r.calibrated = iphoneCalibrated;
            r.blinkStrength = iphoneSource.Blink;
            r.shown = iphoneCanvasGaze; r.eye = iphoneSource.EyeFeature; r.head = iphoneSource.HeadFeature;
            r.look = iphoneSource.LookFeature; r.position = iphoneSource.PositionFeature; r.drift = iphoneMapper.Drift;
            r.hasRaw = iphoneMapper.Ready && r.face && r.fresh;
            if (r.hasRaw) r.raw = Vector2.Scale(iphoneMapper.PredictUnclamped(GazeMapper.Features(r.eye, r.head, r.look, r.position)), new Vector2(1280, 900));
            r.latencyMs = -1; // transport has no synchronized camera exposure timestamp
            return r;
        }
        if (gazeTracker == null) return r;
        r.sequence = lastGazeSequence; r.age = Mathf.Max(0, Time.unscaledTime - lastGazeAt);
        r.face = gazeTracker.IsUserPresent;
        r.fresh = r.sequence > 0 && r.age <= Mathf.Min(.55f, Mathf.Max(.35f, 2.5f / Mathf.Max(1, webcamRate)));
        r.marker = webcamMarkerValid; r.blink = webcamBlinking; r.calibrated = webcamFitted;
        r.blinkStrength = Mathf.Max(webcamShapes[8], webcamShapes[9]);
        r.shown = WebcamCanvasGaze; r.drift = webcamMapper.Drift;
        r.position = gazeLabWebcamHeadPosition;
        r.headDistance = gazeLabWebcamHeadPosition.z;
        r.networkRay = gazeLabWebcamRay;
        r.latencyMs = gazeTracker.MeasuredLatencySeconds * 1000;
        if (gazeLabWebcamFeatures != null)
        {
            var f = gazeLabWebcamFeatures;
            r.eye = new Vector2((float)f[0], (float)f[1]); r.head = new Vector2((float)f[2], (float)f[3]);
            r.look = new Vector2((float)f[4], (float)f[5]);
            r.hasRaw = webcamMapper.Ready && r.face && r.fresh;
            if (r.hasRaw) r.raw = Vector2.Scale(webcamMapper.PredictUnclamped(f), new Vector2(1280, 900));
        }
        return r;
    }

    string GazeLabPhase => !Application.isFocused ? "unfocused" : gazeLabPaused ? "paused" : gazeLabElapsed < 0 ? "countdown"
        : GazeLabMotion.Settling(gazeLabPath, gazeLabElapsed) ? "settling" : "tracking";

    void TickGazeLab()
    {
        if (!gazeLabOpen) return;
        if (!gazeLabPaused && Application.isFocused) gazeLabElapsed += Time.unscaledDeltaTime;
        gazeLabTarget = GazeLabMotion.Target(gazeLabPath, gazeLabElapsed, gazeLabPeriod);
        gazeLabReading = ReadGazeLab();
        if (!gazeLabRecording) return;
        if (gazeLabElapsed >= gazeLabDuration) { EndGazeLabRecording("completed"); return; }
        if (Time.unscaledTime >= gazeLabNextPublish)
        {
            gazeLabNextPublish = Time.unscaledTime + 1;
            try { gazeLabSession.Publish(gazeLabElapsed, GazeLabPhase, Application.isFocused); }
            catch (Exception e) { FailGazeLab(e); }
        }
    }

    void BeginGazeLabRecording()
    {
        if (gazeLabRecording) return;
        gazeLabReading = ReadGazeLab();
        if (!gazeLabReading.calibrated || !gazeLabReading.face || !gazeLabReading.fresh)
        { gazeLabMessage = "먼저 추적 장치를 연결하고 화면 보정을 완료해 주세요."; return; }
        try
        {
            string model = (gazeUseIphone ? iphoneMapper : webcamMapper).ModelName;
            gazeLabSession = new GazeLabSession(GazeLabDirectory, gazeUseIphone ? (iphoneUseUsb ? "iphone-usb" : "iphone-udp") : "webcam",
                gazeLabPath, gazeLabPeriod, gazeLabDuration, gazeLabRevision + " | " + model, (gazeUseIphone ? iphoneMapper : webcamMapper).Save());
            gazeLabRecording = true; gazeLabPaused = false; gazeLabElapsed = -3;
            gazeLabNextPublish = 0; gazeLabLastObservedSequence = long.MinValue;
            gazeLabTrailCount = gazeLabTrailAt = 0;
            gazeLabMessage = "기록 중 · 노란 공을 계속 바라봐 주세요. 시선 표시를 숨기면 공을 따라 보기 더 편해요.";
            Debug.Log("[GazeLab] Recording: " + gazeLabSession.CsvPath);
        }
        catch (Exception e) { FailGazeLab(e); }
    }

    void EndGazeLabRecording(string reason)
    {
        if (!gazeLabRecording) return;
        gazeLabRecording = false;
        try
        {
            gazeLabSession.Finish(gazeLabElapsed, reason, Application.isFocused);
            gazeLabMessage = $"기록 완료 · 유효 표본 {gazeLabSession.ValidCount}개 · 평균 {gazeLabSession.Mean:F1}px · RMSE {gazeLabSession.Rmse:F1}px";
            Debug.Log("[GazeLab] " + gazeLabMessage + " · " + gazeLabSession.CsvPath);
        }
        catch (Exception e) { gazeLabMessage = "로그 마무리 실패: " + e.Message; Debug.LogError("[GazeLab] " + e); }
    }

    void FailGazeLab(Exception e)
    {
        EndGazeLabRecording("io_error");
        gazeLabMessage = "로그 저장 실패: " + e.Message;
        Debug.LogError("[GazeLab] " + e);
    }

    // Only a rendered frame is an observation. Layout/input events must never duplicate samples.
    void ObserveGazeLabRepaint()
    {
        if (Event.current.type != EventType.Repaint || gazeLabLastFrame == Time.frameCount) return;
        gazeLabLastFrame = Time.frameCount;
        var r = gazeLabReading;
        double now = Time.realtimeSinceStartupAsDouble;
        if (r.sequence == gazeLabLastObservedSequence && now - gazeLabLastObservation < 1.0 / 30) return;
        gazeLabLastObservedSequence = r.sequence; gazeLabLastObservation = now;
        if (r.face && r.fresh && r.marker && !r.blink && GazeLabSession.Finite(r.shown))
        {
            gazeLabTrail[gazeLabTrailAt] = r.shown;
            gazeLabTrailAt = (gazeLabTrailAt + 1) % gazeLabTrail.Length;
            gazeLabTrailCount = Mathf.Min(gazeLabTrailCount + 1, gazeLabTrail.Length);
        }
        if (!gazeLabRecording) return;
        try
        {
            gazeLabSession.Record(gazeLabElapsed, now, Time.frameCount, gazeLabTarget, r, GazeLabPhase,
                Application.isFocused, Screen.width, Screen.height, Canvas.m00);
        }
        catch (Exception e) { FailGazeLab(e); }
    }

    void DrawGazeLab()
    {
        GUI.matrix = Canvas;
        Color bg = new Color(.055f, .075f, .12f), ink = new Color(.85f, .9f, .98f);
        Color targetColor = new Color(1, .78f, .18f), gazeColor = new Color(.16f, .88f, .95f);
        NovelUI.Fill(new Rect(0, 0, 1280, 900), bg, 0);
        NovelUI.Label(new Rect(32, 15, 700, 40), "시선 추적 · 움직이는 공 테스트", NovelUI.Round, 27, ink);
        NovelUI.Label(new Rect(32, 60, 1150, 30), "노란 공 = 목표   ·   청록 원 = 추정 시선   ·   오차는 보정에 사용하지 않습니다", NovelUI.Soft, 17, ink);
        GUI.enabled = !gazeLabRecording;
        for (int i = 0; i < GazeLabPaths.Length; i++)
            if (NovelUI.Button(new Rect(32 + i * 152, 104, 142, 40), GazeLabPaths[i], gazeLabPath == (GazeLabPath)i, 17))
            { gazeLabPath = (GazeLabPath)i; gazeLabElapsed = 0; gazeLabTrailCount = 0; }
        if (NovelUI.Button(new Rect(810, 104, 190, 40), $"주기 {gazeLabPeriod:0}초", false, 17)) gazeLabPeriod = gazeLabPeriod == 10 ? 16 : gazeLabPeriod == 16 ? 6 : 10;
        if (NovelUI.Button(new Rect(1020, 104, 220, 40), $"기록 {gazeLabDuration:0}초", false, 17)) gazeLabDuration = gazeLabDuration == 60 ? 120 : gazeLabDuration == 120 ? 30 : 60;
        GUI.enabled = true;
        for (int x = 120; x <= 1160; x += 260) NovelUI.Fill(new Rect(x, 165, 1, 580), new Color(.13f, .17f, .23f), 0);
        for (int y = 180; y <= 720; y += 135) NovelUI.Fill(new Rect(80, y, 1120, 1), new Color(.13f, .17f, .23f), 0);
        var r = gazeLabReading;
        bool visible = r.face && r.fresh && r.marker && !r.blink && GazeLabSession.Finite(r.shown);
        if (gazeLabShowGaze)
        {
            for (int i = 0; i < gazeLabTrailCount; i++)
            {
                int at = (gazeLabTrailAt - gazeLabTrailCount + i + gazeLabTrail.Length) % gazeLabTrail.Length;
                Vector2 p = gazeLabTrail[at];
                if (p.x < 0 || p.x > 1280 || p.y < 150 || p.y > 760) continue;
                NovelUI.Fill(new Rect(p.x - 2, p.y - 2, 4, 4), NovelUI.Alpha(gazeColor, .1f + .5f * i / gazeLabTrailCount), 2);
            }
            if (visible)
            {
                // Dotted error vector avoids rotating the global IMGUI matrix.
                for (int i = 1; i < 24; i++)
                {
                    Vector2 p = Vector2.Lerp(gazeLabTarget, r.shown, i / 24f);
                    NovelUI.Fill(new Rect(p.x - 1, p.y - 1, 2, 2), NovelUI.Alpha(gazeColor, .45f), 1);
                }
                NovelUI.Ring(new Rect(r.shown.x - 14, r.shown.y - 14, 28, 28), gazeColor, 14, 3);
            }
        }
        NovelUI.Fill(new Rect(gazeLabTarget.x - 17, gazeLabTarget.y - 17, 34, 34), targetColor, 17);
        NovelUI.Fill(new Rect(gazeLabTarget.x - 3, gazeLabTarget.y - 3, 6, 6), Color.white, 3);
        if (gazeLabElapsed < 0) NovelUI.Label(new Rect(450, 350, 380, 70), $"{Mathf.CeilToInt(-gazeLabElapsed)}", NovelUI.Round, 54, ink, TextAnchor.MiddleCenter);
        string state = !r.calibrated ? "연결·보정 필요" : !r.face ? "얼굴 없음" : !r.fresh ? "표본 지연" : r.blink ? "눈 감김" : "추적 중";
        string error = visible && r.calibrated ? $"현재 거리 {Vector2.Distance(r.shown, gazeLabTarget):F0}px" : "현재 거리: 측정 제외";
        string metrics = gazeLabSession != null && gazeLabSession.ValidCount > 0
            ? $"평균 {gazeLabSession.Mean:F0}px · RMSE {gazeLabSession.Rmse:F0}px · 편향 X {gazeLabSession.Bias.x:F0} / Y {gazeLabSession.Bias.y:F0} · {gazeLabSession.ValidCount}표본" : "유효 기록 표본을 기다리고 있어요.";
        NovelUI.Fill(new Rect(0, 760, 1280, 140), bg, 0);
        NovelUI.Label(new Rect(32, 758, 1210, 30), $"{state} · {error} · {Mathf.Max(0, gazeLabElapsed):F1}초 / {gazeLabDuration:0}초 · {GazeLabPhase}", NovelUI.Soft, 18, ink);
        NovelUI.Label(new Rect(32, 788, 1210, 26), metrics, NovelUI.Soft, 17, gazeColor);
        if (NovelUI.Button(new Rect(32, 825, 125, 40), "돌아가기", false, 17)) CloseGazeLab();
        if (NovelUI.Button(new Rect(175, 825, 195, 40), gazeLabRecording ? "기록 종료" : "3초 후 기록 시작", true, 17))
        { if (gazeLabRecording) EndGazeLabRecording("stopped"); else BeginGazeLabRecording(); }
        if (NovelUI.Button(new Rect(390, 825, 125, 40), gazeLabPaused ? "계속" : "일시 정지", false, 17)) gazeLabPaused = !gazeLabPaused;
        if (NovelUI.Button(new Rect(535, 825, 195, 40), gazeLabShowGaze ? "시선 표시 숨기기" : "시선 표시 켜기", false, 17)) gazeLabShowGaze = !gazeLabShowGaze;
        GUI.enabled = !gazeLabRecording;
        gazeLabRevision = GUI.TextField(new Rect(760, 825, 235, 40), gazeLabRevision, 80, NovelUI.Input);
        GUI.enabled = true;
        if (NovelUI.Button(new Rect(1015, 825, 225, 40), "로그 폴더 열기", false, 17))
        { Directory.CreateDirectory(GazeLabDirectory); Application.OpenURL(new Uri(GazeLabDirectory + Path.DirectorySeparatorChar).AbsoluteUri); }
        NovelUI.Label(new Rect(32, 872, 1210, 25), gazeLabMessage, NovelUI.Soft, 14, ink);
        if (gazeLabOpen) ObserveGazeLabRepaint();
    }
}
