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
    const float EyeContactLookAwayDwellSeconds = 7f;
    const float EyeContactLookAwayChance = .25f;
    const float EyeContactLookAwayCooldownSeconds = 60f;
    const float EyeContactLookAwayMinimumPlaySeconds = 90f;
    const string EyeContactDevicePref = "GazeTest.EyeContactDevice";

    bool eyeContactPreparationMode, eyeContactPreparationFromLoad, eyeContactSessionActive;
    bool eyeContactTrackingEnabled, eyeContactFaceDrawn, eyeContactAwaitExit, eyeContactGazeValid;
    bool lookAwayTriggered, lookAwayInterruptActive;
    int eyeContactCount, eyeContactFaceFrame = -1;
    float eyeContactTotalSeconds, eyeContactDwell, eyeContactExitSeconds, eyeContactFaceAlpha;
    float eyeContactLookAwayDwell, eyeContactLookAwayCooldown, eyeContactLookAwayPlaySeconds, lookAwayInterruptStartedAt;
    Vector2 eyeContactFaceCenter, eyeContactGaze;
    float eyeContactFaceRadiusX, eyeContactFaceRadiusY;

    public int EyeContactCount => eyeContactCount;
    public float EyeContactTotalSeconds => eyeContactTotalSeconds;
    public bool EyeContactTrackingEnabled => eyeContactTrackingEnabled;
    public bool LookAwayTriggered => lookAwayTriggered;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    bool eyeContactDebugVisible;
    bool eyeContactLookAwayDialogueBlocked;
    string eyeContactDebugState = "대기";
    float eyeContactCountedAt = -1f;
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
        eyeContactLookAwayDwell = 0f;
        eyeContactLookAwayCooldown = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        eyeContactDebugState = "대기";
        eyeContactLookAwayDialogueBlocked = false;
        eyeContactCountedAt = -1f;
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
        lookAwayTriggered = false;
        lookAwayInterruptActive = false;
        eyeContactLookAwayPlaySeconds = 0f;
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

        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, EyeContactMaxFrameSeconds);
        bool storyPlayTime = scene >= 1 && scene <= 6 && memorial <= 0f && introDone && practice == null && menu == Menu.None;
        if (storyPlayTime)
        {
            eyeContactLookAwayPlaySeconds += dt;
            eyeContactLookAwayCooldown = Mathf.Max(0f, eyeContactLookAwayCooldown - dt);
        }

        if (lookAwayInterruptActive)
        {
            eyeContactLookAwayDwell = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            eyeContactDebugState = "시선 이탈 끼어들기";
            eyeContactLookAwayDialogueBlocked = false;
#endif
            return;
        }

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
            eyeContactLookAwayDwell = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            eyeContactDebugState = "깜빡임 · 진행 정지";
            eyeContactLookAwayDialogueBlocked = false;
#endif
            return;
        }

        bool portraitVisible = scene >= 1 && scene <= 6 && portraitAlpha >= EyeContactPortraitAlphaThreshold &&
            eyeContactFaceDrawn && Time.frameCount - eyeContactFaceFrame <= 1 && eyeContactFaceAlpha >= EyeContactPortraitAlphaThreshold;
        bool heldSkip = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool chapterCardVisible = scene >= 1 && scene <= 6 && (chapterShown != scene || Time.unscaledTime - chapterAt <= 2.6f);
        bool dialogueBlocked = scene < 1 || scene > 6 || showResult || ChoiceUiVisible || AiBoxField || AiWaiting || AiHintShown || chapterCardVisible;
        bool blocked = menu != Menu.None || miniGame != null || practice != null || skip || heldSkip || eyeContactPreparationMode;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        eyeContactLookAwayDialogueBlocked = dialogueBlocked && !blocked && portraitVisible && gazeValid;
#endif
        if (blocked || !portraitVisible || !gazeValid)
        {
            eyeContactDwell = 0f;
            eyeContactExitSeconds = 0f;
            eyeContactLookAwayDwell = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            eyeContactDebugState = blocked ? "기록 제외 상태" : !portraitVisible ? "교수 얼굴 미표시" : "유효 시선 없음";
#endif
            return;
        }

        float nx = (eyeContactGaze.x - eyeContactFaceCenter.x) / eyeContactFaceRadiusX;
        float ny = (eyeContactGaze.y - eyeContactFaceCenter.y) / eyeContactFaceRadiusY;
        bool inside = nx * nx + ny * ny <= 1f;
        float leaveX = nx / EyeContactLeaveRadiusFactor;
        float leaveY = ny / EyeContactLeaveRadiusFactor;
        bool outsideLeave = leaveX * leaveX + leaveY * leaveY > 1f;

        if (!lookAwayTriggered && !dialogueBlocked && eyeContactLookAwayPlaySeconds >= EyeContactLookAwayMinimumPlaySeconds && eyeContactLookAwayCooldown <= 0f)
        {
            if (outsideLeave) eyeContactLookAwayDwell += dt;
            else eyeContactLookAwayDwell = 0f;

            if (eyeContactLookAwayDwell >= EyeContactLookAwayDwellSeconds)
            {
                eyeContactLookAwayDwell = 0f;
                if (Random.value < EyeContactLookAwayChance)
                {
                    TriggerLookAwayInterruption();
                    return;
                }
                eyeContactLookAwayCooldown = EyeContactLookAwayCooldownSeconds;
            }
        }
        else eyeContactLookAwayDwell = 0f;

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
        eyeContactCountedAt = Time.unscaledTime;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void HandleEyeContactDebugKey()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (e.keyCode == KeyCode.F9)
        {
            e.Use();
            if (!lookAwayTriggered && InGame && menu == Menu.None && miniGame == null && practice == null)
                TriggerLookAwayInterruption();
            return;
        }
        if (e.keyCode != KeyCode.F8) return;
        eyeContactDebugVisible = !eyeContactDebugVisible;
        e.Use();
    }
#endif

    void TriggerLookAwayInterruption()
    {
        if (lookAwayTriggered || scene < 1 || scene > 6) return;
        lookAwayTriggered = true;
        lookAwayInterruptActive = true;
        lookAwayInterruptStartedAt = Time.unscaledTime;
        eyeContactLookAwayDwell = 0f;
        affection--;
        React(-1, false);
        Debug.Log($"[look away] scene={scene} line={dialogueIndex} affection={affection}");
    }

    void DrawLookAwayInterruption()
    {
        string line = S.Professors[professor].LookAwayLine;
        if (string.IsNullOrWhiteSpace(line)) line = "똑바로 봐!";
        SpeakOnce("lookaway", line);
        Rect box = new Rect(70, 628, 1140, 244);
        NovelUI.Card(box, NovelUI.Alpha(NovelUI.Cream, .96f), 30, NovelUI.Pink);
        NovelUI.Pill(box.x + 34, box.y - 26, 52, ProfessorName, NovelUI.DeepPink, Color.white, 24);
        Rect textRect = new Rect(box.x + 42, box.y + 46, box.width - 100, box.height - 64);
        int size = NovelUI.Height(line, NovelUI.Soft, 26, textRect.width) > textRect.height ? 22 : 26;
        NovelUI.Label(textRect, line, NovelUI.Soft, size, NovelUI.Berry, TextAnchor.UpperLeft, FontStyle.Bold);

        bool pressed = Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if (GUI.Button(box, GUIContent.none, GUIStyle.none) || pressed)
        {
            if (pressed) Event.current.Use();
            float paused = Mathf.Max(0f, Time.unscaledTime - lookAwayInterruptStartedAt);
            lineStartedAt += paused;
            if (typedAt >= 0f) typedAt += paused;
            lookAwayInterruptActive = false;
            reactAt = -10; // 닫는 즉시 원래 표정으로 복귀
            novelAudio.Play("Advance");
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

            int digits = eyeContactCount < 10 ? 1 : eyeContactCount < 100 ? 2 : eyeContactCount < 1000 ? 3 :
                eyeContactCount < 10000 ? 4 : eyeContactCount < 100000 ? 5 : eyeContactCount < 1000000 ? 6 :
                eyeContactCount < 10000000 ? 7 : eyeContactCount < 100000000 ? 8 : eyeContactCount < 1000000000 ? 9 : 10;
            const float countHeight = 42f, popWidth = 58f, popGap = 6f;
            const float popDuration = .6f, popRise = 28f;
            float countWidth = 112f + digits * 18f;
            float groupWidth = countWidth + popGap + popWidth;
            float countX = Mathf.Clamp(eyeContactFaceCenter.x - countWidth / 2f, 0f, 1280f - groupWidth);
            float labelY = Mathf.Clamp(eyeContactFaceCenter.y - eyeContactFaceRadiusY * EyeContactLeaveRadiusFactor - countHeight - 8f,
                0f, 900f - countHeight);
            Rect countRect = new Rect(countX, labelY, countWidth, countHeight);
            string countLabel = $"눈맞춤 {eyeContactCount}";
            NovelUI.Label(new Rect(countRect.x + 1, countRect.y + 2, countRect.width, countRect.height), countLabel,
                NovelUI.Round, 28, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(countRect, countLabel, NovelUI.Round, 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            float sinceCount = Time.unscaledTime - eyeContactCountedAt;
            if (eyeContactCountedAt >= 0f && sinceCount >= 0f && sinceCount < popDuration)
            {
                float progress = sinceCount / popDuration;
                float alpha = 1f - progress;
                float rise = progress * popRise;
                Rect popRect = new Rect(countRect.xMax + popGap,
                    Mathf.Clamp(labelY - rise, 0f, 900f - countHeight), popWidth, countHeight);
                Color popColor = NovelUI.Alpha(NovelUI.DeepPink, alpha);
                NovelUI.Label(new Rect(popRect.x + 1, popRect.y + 2, popRect.width, popRect.height), "+1",
                    NovelUI.Round, 27, NovelUI.Alpha(NovelUI.Berry, alpha), TextAnchor.MiddleLeft, FontStyle.Bold);
                NovelUI.Label(popRect, "+1", NovelUI.Round, 27, popColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            }
        }
        if (eyeContactGazeValid)
            NovelUI.Fill(new Rect(eyeContactGaze.x - 7, eyeContactGaze.y - 7, 14, 14), NovelUI.DeepPink, 7);
        NovelUI.Card(new Rect(14, 18, 390, 128), NovelUI.Alpha(Color.white, .94f), 12, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(28, 24, 360, 28), $"눈맞춤 {eyeContactCount}회 · 응시 {eyeContactTotalSeconds:F1}초", NovelUI.Soft, 17, NovelUI.Berry, TextAnchor.MiddleLeft, FontStyle.Bold);
        NovelUI.Label(new Rect(28, 56, 360, 30), eyeContactDebugState, NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleLeft);
        string areaState = lookAwayInterruptActive ? "끼어들기 중 · 눈맞춤 정지"
            : eyeContactLookAwayDialogueBlocked ? "눈맞춤 집계 · 이탈 판정 제외"
            : eyeContactFaceDrawn ? "영역: 분홍 진입 · 보라 이탈" : "영역 대기 중";
        NovelUI.Label(new Rect(28, 84, 360, 24), areaState, NovelUI.Soft, 13, NovelUI.Plum, TextAnchor.MiddleLeft);
        NovelUI.Label(new Rect(28, 106, 360, 24), $"이탈 {eyeContactLookAwayDwell:F1}/{EyeContactLookAwayDwellSeconds:F0}초 · 쿨다운 {eyeContactLookAwayCooldown:F0}초 · 발동 {(lookAwayTriggered ? "완료" : "대기")}", NovelUI.Soft, 13, NovelUI.Plum, TextAnchor.MiddleLeft);
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
