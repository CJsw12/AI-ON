using UnityEngine;

public partial class GameController
{
    const float EyeContactDwellSeconds = .45f;
    const float EyeContactLeaveSeconds = .3f;
    const float EyeContactFaceRadiusXFactor = 1.4f;
    const float EyeContactFaceRadiusYFactor = 1.4f;
    const float EyeContactLeaveRadiusFactor = 1.25f;
    const float EyeContactFallbackFaceY = .18f;
    const float EyeContactFallbackFaceSize = .2f;
    const float EyeContactPortraitAlphaThreshold = .5f;
    const float EyeContactMaxFrameSeconds = .1f;
    const string EyeContactDevicePref = "GazeTest.EyeContactDevice";

    bool eyeContactPreparationMode, eyeContactPreparationFromLoad, eyeContactSessionActive;
    bool eyeContactTrackingEnabled, eyeContactFaceDrawn, eyeContactAwaitExit, eyeContactGazeValid;
    int eyeContactCount, eyeContactFaceFrame = -1;
    float eyeContactTotalSeconds, eyeContactDwell, eyeContactExitSeconds, eyeContactFaceAlpha;
    Vector2 eyeContactFaceCenter, eyeContactGaze;
    float eyeContactFaceRadiusX, eyeContactFaceRadiusY;

    public int EyeContactCount => eyeContactCount;
    public float EyeContactTotalSeconds => eyeContactTotalSeconds;
    public bool EyeContactTrackingEnabled => eyeContactTrackingEnabled;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    bool eyeContactDebugVisible;
    string eyeContactDebugState = "대기";
#endif

    void BeginEyeContactPreparation(bool fromLoad)
    {
        eyeContactSessionActive = false;
        CloseGazeTest();
        gazeTestOpen = true;
        gazeUseIphone = PlayerPrefs.GetInt(EyeContactDevicePref, 0) == 1;
        webcamFitStatus = "";
        iphoneCalibrated = iphoneUncalibrated = iphoneMarkerValid = false;
        eyeContactPreparationMode = true;
        eyeContactPreparationFromLoad = fromLoad;
        ResetEyeContactProgress();
    }

    void CompleteEyeContactPreparation()
    {
        if (!eyeContactPreparationMode) return;
        eyeContactPreparationMode = false;
        eyeContactPreparationFromLoad = false;
        gazeTestOpen = false;
        eyeContactSessionActive = true;
        eyeContactTrackingEnabled = true;
        ResetEyeContactProgress();
    }

    void TryCompleteEyeContactPreparation()
    {
        if (eyeContactPreparationMode && (gazeUseIphone ? iphoneCalibrated : webcamFitted))
            CompleteEyeContactPreparation();
    }

    void CancelEyeContactPreparation()
    {
        if (!eyeContactPreparationMode) return;
        bool fromLoad = eyeContactPreparationFromLoad;
        eyeContactPreparationMode = false;
        eyeContactPreparationFromLoad = false;
        CloseGazeTest();
        eyeContactTrackingEnabled = false;
        eyeContactSessionActive = false;
        ResetEyeContactProgress();
        if (fromLoad)
        {
            ResetRun();
            entryStep = 0;
        }
        else
        {
            scene = 0;
            professor = -1;
            portraitAlpha = 0;
        }
        ShowToast(fromLoad ? "이어하기를 취소했어요" : "교수 선택으로 돌아갑니다");
    }

    void EndEyeContactSession()
    {
        eyeContactSessionActive = false;
        gazeTestOpen = false;
        CloseGazeTest();
        ResetEyeContactProgress();
    }

    void ResetEyeContactProgress()
    {
        eyeContactDwell = 0f;
        eyeContactExitSeconds = 0f;
        eyeContactAwaitExit = false;
        eyeContactGaze = Vector2.zero;
        eyeContactGazeValid = false;
        eyeContactFaceDrawn = false;
        eyeContactFaceFrame = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        eyeContactDebugState = "대기";
#endif
    }

    void ResetEyeContactRun()
    {
        eyeContactSessionActive = false;
        eyeContactPreparationMode = false;
        eyeContactPreparationFromLoad = false;
        eyeContactCount = 0;
        eyeContactTotalSeconds = 0f;
        eyeContactTrackingEnabled = false;
        ResetEyeContactProgress();
    }

    void CaptureEyeContactFace(Vector2 center, float faceSize)
    {
        if (!NovelUI.Repaint) return;
        RecordEyeContactFace(center, faceSize, faceSize);
    }

    void CaptureEyeContactFallback(Rect drawnRect)
    {
        if (!NovelUI.Repaint || drawnRect.width <= 0f || drawnRect.height <= 0f) return;
        Vector2 center = new Vector2(drawnRect.center.x, drawnRect.y + drawnRect.height * EyeContactFallbackFaceY);
        float size = drawnRect.height * EyeContactFallbackFaceSize;
        RecordEyeContactFace(center, size, size);
    }

    void RecordEyeContactFace(Vector2 localCenter, float localWidth, float localHeight)
    {
        Matrix4x4 toCanvas = Canvas.inverse * GUI.matrix;
        Vector3 center = toCanvas.MultiplyPoint3x4(localCenter);
        Vector3 xEdge = toCanvas.MultiplyPoint3x4(new Vector3(localCenter.x + localWidth, localCenter.y, 0));
        Vector3 yEdge = toCanvas.MultiplyPoint3x4(new Vector3(localCenter.x, localCenter.y + localHeight, 0));
        eyeContactFaceCenter = new Vector2(center.x, center.y);
        eyeContactFaceRadiusX = Mathf.Abs(xEdge.x - center.x) * EyeContactFaceRadiusXFactor;
        eyeContactFaceRadiusY = Mathf.Abs(yEdge.y - center.y) * EyeContactFaceRadiusYFactor;
        eyeContactFaceAlpha = portraitAlpha;
        eyeContactFaceDrawn = eyeContactFaceRadiusX > 0f && eyeContactFaceRadiusY > 0f;
        eyeContactFaceFrame = Time.frameCount;
    }

    void UpdateEyeContactTracking()
    {
        if (!eyeContactSessionActive) return;

        bool fresh, faceFound, gazeValid, blinking;
        if (gazeUseIphone)
        {
            fresh = iphoneSource != null && iphoneSource.HasFreshSample;
            faceFound = fresh && iphoneSource.FaceFound;
            gazeValid = faceFound && iphoneMarkerValid;
            blinking = gazeValid && iphoneBlinking;
            eyeContactGaze = iphoneCanvasGaze;
        }
        else
        {
            fresh = gazeTracker != null && gazeTracker.IsUserPresent && Time.unscaledTime - lastGazeAt <= 2f;
            faceFound = fresh && gazeTracker != null && gazeTracker.IsUserPresent;
            gazeValid = faceFound && webcamMarkerValid;
            blinking = gazeValid && webcamBlinking;
            eyeContactGaze = WebcamCanvasGaze;
        }
        eyeContactGazeValid = gazeValid;

        if (blinking)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            eyeContactDebugState = "깜빡임 · 진행 정지";
#endif
            return;
        }

        bool portraitVisible = scene >= 1 && scene <= 6 && portraitAlpha >= EyeContactPortraitAlphaThreshold &&
            eyeContactFaceDrawn && Time.frameCount - eyeContactFaceFrame <= 1 && eyeContactFaceAlpha >= EyeContactPortraitAlphaThreshold;
        bool heldSkip = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool blocked = menu != Menu.None || miniGame != null || practice != null || skip || heldSkip || eyeContactPreparationMode;
        if (blocked || !portraitVisible || !gazeValid)
        {
            eyeContactDwell = 0f;
            eyeContactExitSeconds = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            eyeContactDebugState = blocked ? "기록 제외 상태" : !portraitVisible ? "교수 얼굴 미표시" : "유효 시선 없음";
#endif
            return;
        }

        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, EyeContactMaxFrameSeconds);
        float nx = (eyeContactGaze.x - eyeContactFaceCenter.x) / eyeContactFaceRadiusX;
        float ny = (eyeContactGaze.y - eyeContactFaceCenter.y) / eyeContactFaceRadiusY;
        bool inside = nx * nx + ny * ny <= 1f;
        float leaveX = nx / EyeContactLeaveRadiusFactor;
        float leaveY = ny / EyeContactLeaveRadiusFactor;
        bool outsideLeave = leaveX * leaveX + leaveY * leaveY > 1f;

        if (inside) eyeContactTotalSeconds += dt;
        if (eyeContactAwaitExit)
        {
            eyeContactDwell = 0f;
            if (outsideLeave)
            {
                eyeContactExitSeconds += dt;
                if (eyeContactExitSeconds >= EyeContactLeaveSeconds)
                {
                    eyeContactAwaitExit = false;
                    eyeContactExitSeconds = 0f;
                }
            }
            else eyeContactExitSeconds = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            eyeContactDebugState = eyeContactAwaitExit ? $"재진입 대기 {eyeContactExitSeconds:F2}/{EyeContactLeaveSeconds:F2}초" : "다시 응시 가능";
#endif
            return;
        }

        if (!inside)
        {
            eyeContactDwell = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            eyeContactDebugState = "영역 밖";
#endif
            return;
        }

        eyeContactDwell += dt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        eyeContactDebugState = $"응시 {eyeContactDwell:F2}/{EyeContactDwellSeconds:F2}초";
#endif
        if (eyeContactDwell < EyeContactDwellSeconds) return;

        eyeContactCount++;
        eyeContactAwaitExit = true;
        eyeContactExitSeconds = 0f;
        eyeContactDwell = 0f;
        Debug.Log($"[eye contact] scene={scene}; time={Time.time:F2}s; count={eyeContactCount}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        eyeContactDebugState = "눈맞춤 기록";
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void HandleEyeContactDebugKey()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || e.keyCode != KeyCode.F8) return;
        eyeContactDebugVisible = !eyeContactDebugVisible;
        e.Use();
    }

    void DrawEyeContactDebugOverlay()
    {
        if (!eyeContactDebugVisible || !eyeContactSessionActive) return;
        Matrix4x4 saved = GUI.matrix;
        GUI.matrix = Canvas;
        if (eyeContactFaceDrawn)
        {
            DrawEyeContactEllipse(eyeContactFaceCenter, eyeContactFaceRadiusX, eyeContactFaceRadiusY, NovelUI.DeepPink);
            DrawEyeContactEllipse(eyeContactFaceCenter, eyeContactFaceRadiusX * EyeContactLeaveRadiusFactor,
                eyeContactFaceRadiusY * EyeContactLeaveRadiusFactor, NovelUI.Alpha(NovelUI.Berry, .7f));
        }
        if (eyeContactGazeValid)
            NovelUI.Fill(new Rect(eyeContactGaze.x - 7, eyeContactGaze.y - 7, 14, 14), NovelUI.DeepPink, 7);
        NovelUI.Card(new Rect(14, 18, 390, 104), NovelUI.Alpha(Color.white, .94f), 12, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(28, 24, 360, 28), $"눈맞춤 {eyeContactCount}회 · 응시 {eyeContactTotalSeconds:F1}초", NovelUI.Soft, 17, NovelUI.Berry, TextAnchor.MiddleLeft, FontStyle.Bold);
        NovelUI.Label(new Rect(28, 56, 360, 30), eyeContactDebugState, NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleLeft);
        NovelUI.Label(new Rect(28, 84, 360, 24), eyeContactFaceDrawn ? "영역: 분홍 진입 · 보라 이탈" : "영역 대기 중", NovelUI.Soft, 13, NovelUI.Plum, TextAnchor.MiddleLeft);
        GUI.matrix = saved;
    }

    static void DrawEyeContactEllipse(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int segments = 48;
        const float thickness = 2f;
        Vector2 previous = new Vector2(center.x + radiusX, center.y);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 next = new Vector2(center.x + Mathf.Cos(angle) * radiusX, center.y + Mathf.Sin(angle) * radiusY);
            Vector2 delta = next - previous;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, previous);
            NovelUI.Fill(new Rect(previous.x, previous.y - thickness / 2, delta.magnitude + 1, thickness), color, 0);
            GUI.matrix = saved;
            previous = next;
        }
    }
#endif
}
