using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

// The two gaze minigames outside the gaze test:
// - 미니게임 > 하이라이트 tab: free practice of 눈빛 맞추기 / 눈빛 피하기 (no 호감도 change), back to the menu after.
// - Story events, shared by every faculty:
//   눈빛 맞추기 once, after the first slot result that leaves 호감도 at 4 or more;
//   눈빛 피하기 after the 12th slot (before the ending) when 호감도 is 8 or more.
//   Each game's result adds 0..2 호감도. Without a calibrated eye tracker the player is led through calibration
//   first (the gaze test screen starts the game once ready) and can skip the event instead.
public partial class GameController
{
    enum GazeGame { None, Match, Dodge }

    bool practiceHighlight;
    GazeGame highlightChoice;
    bool forceManualHighlight;
    bool matchEventDone, dodgeEventDone, dodgePending;
    GazeGame storyGazeEvent;        // an event waiting on its intro card
    GazeGame gazeSession;           // a game launched from here (highlight or story), followed until it closes
    bool gazeSessionStory, gazeSessionSawResult, gazeSessionSawGame;
    int gazeSessionGain;

    bool storyGazeSession => gazeSession != GazeGame.None && gazeSessionStory;

    const int MatchEventAffection = 4, DodgeEventAffection = 8;
    // 눈빛 피하기 is endless (three catches end it), so its 호감도 comes from how long the player held out.
    const float DodgeGoodSeconds = 15f, DodgeGreatSeconds = 30f;

    void HighlightTab(Rect card)
    {
        if (highlightChoice != GazeGame.None)
        {
            NovelUI.Label(new Rect(card.x, card.y + 180, card.width, 55),
                (highlightChoice == GazeGame.Match ? "눈빛 맞추기" : "눈빛 피하기") + " · 모드 선택",
                NovelUI.Round, 28, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (Tile(new Rect(card.x+50, card.y+250, card.width-100, 125),
                "웹캠·iPhone 시선 모드", "실제 시선으로 플레이 · 보정이 필요하면 준비 화면으로 이동", false))
            {
                GazeGame chosen = highlightChoice;
                highlightChoice = GazeGame.None;
                StartGazeGame(chosen, false, false);
            }
            if (Tile(new Rect(card.x+50, card.y+395, card.width-100, 125),
                "웹캠 없는 모드", "눈빛 맞추기: 마우스 클릭·드래그 / 눈빛 피하기: 방향키", false))
            {
                GazeGame chosen = highlightChoice;
                highlightChoice = GazeGame.None;
                StartGazeGame(chosen, false, true);
            }
            if (NovelUI.Button(new Rect(card.center.x-100, card.y+550, 200, 48), "게임 선택으로", false, 20))
                highlightChoice = GazeGame.None;
            return;
        }
        NovelUI.Label(new Rect(card.x, card.y + 170, card.width, 28), "시선과 손으로 하는 특별한 체험", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
        GUI.enabled = !handProbeConfirm && !signRecalibrateNotice;
        if (Tile(new Rect(card.x + 50, card.y + 204, card.width - 100, 130), "눈빛 맞추기", "분홍 점을 시선으로 따라가요.\n스토리 등장: 호감도 4 이상", false)) highlightChoice = GazeGame.Match;
        if (Tile(new Rect(card.x + 50, card.y + 344, card.width - 100, 130), "눈빛 피하기", "다가오는 빨간 눈빛을 피해요.\n스토리 등장: 마지막 장면 · 호감도 8 이상", false)) highlightChoice = GazeGame.Dodge;
        if (Tile(new Rect(card.x + 50, card.y + 484, card.width - 100, 130), "수어 지문자", "웹캠으로 손 모양을 따라 해요.", false))
        {
            if (!gazeUseIphone && (gazeObject != null || requestingCamera)) handProbeConfirm = true;
            else StartHandProbe();
        }
        NovelUI.Label(new Rect(card.x + 40, card.y + 625, card.width - 80, 65), "눈빛 게임은 시선 모드 또는 마우스·방향키 모드를 선택해요.\n수어 지문자는 웹캠이 필요해요.", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleCenter);
        GUI.enabled = true;
        if (handProbeConfirm)
        {
            NovelUI.Card(new Rect(card.x + 70, card.y + 260, card.width - 140, 240), NovelUI.Cream, 25, NovelUI.Pink);
            NovelUI.Label(new Rect(card.x + 90, card.y + 290, card.width - 180, 80), "시선 추적을 끄면 보정이 초기화됩니다", NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(card.x + 110, card.y + 400, 210, 55), "끄고 시작", true, 20)) { handProbeConfirm = false; signRecalibrate = true; StopWebcamTracking(); StartHandProbe(); }
            if (NovelUI.Button(new Rect(card.x + 380, card.y + 400, 210, 55), "취소", false, 20)) handProbeConfirm = false;
        }
        if (signRecalibrateNotice)
        {
            NovelUI.Card(new Rect(card.x + 70, card.y + 260, card.width - 140, 240), NovelUI.Cream, 25, NovelUI.Pink);
            NovelUI.Label(new Rect(card.x + 90, card.y + 285, card.width - 180, 80), "시선 추적을 다시 쓰려면 보정이 필요해요", NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(card.x + 110, card.y + 400, 210, 55), "보정하러 가기", true, 19)) { signRecalibrateNotice = false; menu = Menu.None; OpenGazeTest(); }
            if (NovelUI.Button(new Rect(card.x + 380, card.y + 400, 210, 55), "닫기", false, 20)) signRecalibrateNotice = false;
        }
    }

    bool handProbeConfirm, handProbeOpen;
    HandLandmarkSource handProbe;
    SignDataset signData;
    SignGameSession signSession;
    KnnClassifier signClassifier;
    float[] signRatios;
    readonly float[] signLandmarks = new float[63];
    readonly Stopwatch signClassificationTimer = new Stopwatch();
    SignVote signVote;
    SignVote.Result signVoteResult;
    SignPlayLog signPlayLog;
    bool signUseKnn, signRecordOther;
    long signOtherStarted, signOtherElapsed;
    long signNextTimingUpdate;
    string signTiming, signDebugTiming, signLogError;
    long signCountdownTenths = -1;
    string signCountdown;
    bool signDebug, signBrowse, signRecalibrate, signRecalibrateNotice, signGapCleared;
    int signBrowseIndex;
    Coroutine signStartCamera;
    long signFrameCount, signFrameTime;
    long signFeedbackUntil;
    string signFeedback;
    Vector2 signScoreScroll;
    string signError;
    const bool SignLabelsAreMirrored = false;
    static readonly SignLabels SignRuleLabels = new SignLabels(SignRuleClassifier.Letters);
    SignLabels ActiveSignLabels => signUseKnn ? signData.Labels : SignRuleLabels;
    bool SignHasKnn => signClassifier != null;
    string SignMode => signUseKnn ? "knn" : "rule";
    static long SignTimeMs => (long)(Time.realtimeSinceStartupAsDouble * 1000);
    static readonly string[] signLines = { "…천천히, 손 모양을 살펴봐요.", "…네, 잘했어요.", "다시 해 보세요 ({0}로 보였어요)", "…시간이 지났어요. 괜찮아요.", "…수고했어요. 함께 살펴봐요." };
    static readonly int[] handEdges = { 0,1,1,2,2,3,3,4, 0,5,5,6,6,7,7,8, 5,9,9,10,10,11,11,12, 9,13,13,14,14,15,15,16, 13,17,17,18,18,19,19,20, 17,0 };
    // Landmarks already include FrameFlipH/FrameFlipV, so only mirror the result for the preview.
    static Vector2 HandPreviewPoint(Rect view, Vector3 point) =>
        new Vector2(view.x + (1f - point.x) * view.width, view.y + point.y * view.height);
    void StartHandProbe()
    {
        StopSignRecording();
        if (signStartCamera != null) { StopCoroutine(signStartCamera); signStartCamera = null; }
        if (handProbe != null) { handProbe.Release(); Destroy(handProbe); handProbe = null; }
        menu = Menu.None; handProbeOpen = true;
        signDebug = signBrowse = signUseKnn = false;
        signBrowseIndex = 0; signError = signLogError = null; signData = null;
        signSession = null; signClassifier = null; signVote = new SignVote();
        signOtherElapsed = signNextTimingUpdate = 0;
        signTiming = signDebugTiming = null;
        signClassificationTimer.Reset();
        try
        {
            signData = SignDataset.Load(Path.Combine(Application.streamingAssetsPath, "SignData"), SignLabelsAreMirrored);
            if (!signData.IsEmpty && (signData.Labels.Count > 1 || signData.Labels[0] != SignLabels.Other))
                signClassifier = new KnnClassifier(signData.Samples, signData.Labels, maxDistance: signData.SuggestedMaxDistance);
            RestartSignSession();
        }
        catch (System.Exception e) { signError = "모델 생성 실패: " + e.Message; }
        if (signError == null) signStartCamera = StartCoroutine(StartSignCameraNextFrame());
    }
    System.Collections.IEnumerator StartSignCameraNextFrame()
    {
        yield return null;
        signStartCamera = null;
        if (handProbeOpen && signError == null) handProbe = gameObject.AddComponent<HandLandmarkSource>();
    }
    void CloseHandProbe(bool showNotice = true)
    {
        handProbeOpen = false; handProbeConfirm = false;
        StopSignRecording();
        if (signStartCamera != null) { StopCoroutine(signStartCamera); signStartCamera = null; }
        if (handProbe != null) { handProbe.Release(); Destroy(handProbe); handProbe = null; }
        if (signRecalibrate) { signRecalibrate = false; signRecalibrateNotice = showNotice; }
    }
    bool SignCameraFailed => handProbe != null && (handProbe.State == HandSourceState.NoModel || handProbe.State == HandSourceState.NoCamera || handProbe.State == HandSourceState.PermissionDenied || handProbe.State == HandSourceState.Failed || handProbe.State == HandSourceState.UnsupportedRotation);

    void RestartSignSession(int seed = 17)
    {
        signSession = new SignGameSession(ActiveSignLabels, seed: seed);
        signScoreScroll = Vector2.zero;
        signFeedback = null;
        signFeedbackUntil = 0;
        signFrameCount = handProbe != null ? handProbe.DetectionStats.FrameCount : 0;
        signFrameTime = 0;
        signGapCleared = false;
        signClassificationTimer.Reset();
        signNextTimingUpdate = 0;
        ResetSignVote();
    }

    void ResetSignVote() { signVote.Reset(); signVoteResult = new SignVote.Result(); signRatios = null; }

    long SignSessionTime(long now) => (signRecordOther ? signOtherStarted : now) - signOtherElapsed;

    void SetSignRecordOther(bool active, long now)
    {
        if (signRecordOther == active) return;
        if (active) signOtherStarted = now;
        else signOtherElapsed += now - signOtherStarted;
        signRecordOther = active;
        ResetSignVote();
    }

    void StopSignRecording()
    {
        SetSignRecordOther(false, SignTimeMs);
        if (signPlayLog != null && !signPlayLog.TryClose()) signLogError = "기록 파일 저장에 실패했습니다.";
        signPlayLog = null;
    }

    void RefreshSignTiming(long now)
    {
        if (now < signNextTimingUpdate) return;
        signNextTimingUpdate = now + 500;
        double interval95 = handProbe.UpdateIntervalStats.Percentile95;
        signTiming = $"감지 {handProbe.DetectionMilliseconds:F1} ms · 분류 {signClassificationTimer.Elapsed.TotalMilliseconds:F2} ms · 프레임 간격 95백분위 {interval95:F1} ms · CPU";
        if (!signDebug) return;
        var d = handProbe.DetectionStats;
        var f = handProbe.FrameStats;
        string distanceInfo = "";
        if (signUseKnn)
        {
            distanceInfo = float.IsPositiveInfinity(signData.SuggestedMaxDistance) ? "거리 상한 계산 불가(시도 부족)" : $"거리 상한 {signData.SuggestedMaxDistance:F3}";
            distanceInfo += $" · 계산 {signData.DistanceCalculationMilliseconds:F1} ms";
        }
        signDebugTiming = $"{handProbe.Status} · 손 {handProbe.Handedness} ({handProbe.HandednessScore:F2})\n감지 평균/중앙값/95%/최대 {d.Average:F1} / {d.Median:F1} / {d.Percentile95:F1} / {d.Maximum:F1} ms\nFPS {(f.Average > 0 ? 1000 / f.Average : 0):F1} · 갱신 {handProbe.UpdateRate:F1}회/초 · 간격 95% {interval95:F1} ms\n{distanceInfo}";
    }

    void TickSignGame()
    {
        if (handProbe == null) return;
        long now = SignTimeMs;
        RefreshSignTiming(now);
        if (SignCameraFailed)
        {
            if (signPlayLog != null) StopSignRecording();
            return;
        }
        if (signSession == null || signBrowse) return;
        var stage = signSession.CurrentStage;
        if (stage != SignGameSession.Stage.Practice && stage != SignGameSession.Stage.Challenge) return;
        long sessionTime = SignSessionTime(now);
        if (handProbe.DetectionStats.FrameCount == signFrameCount)
        {
            if (signFrameTime > 0 && !signGapCleared && now - signFrameTime >= 200) { signVoteResult = signVote.Add(now, SignVote.NoHand); signGapCleared = true; signRatios = null; }
            if (!signRecordOther) UpdateSignSession(sessionTime, null);
            return;
        }
        signFrameCount = handProbe.DetectionStats.FrameCount; signFrameTime = now; signGapCleared = false;
        int prediction = SignVote.NoHand;
        string rule = null;
        float[] points = null;
        signClassificationTimer.Reset();
        bool left; float[] features;
        if (handProbe.IsHandDetected)
        {
            points = signLandmarks;
            for (int i = 0; i < 21; i++) { points[i * 3] = handProbe.Landmarks[i].x; points[i * 3 + 1] = handProbe.Landmarks[i].y; points[i * 3 + 2] = handProbe.Landmarks[i].z; }
            if (!signUseKnn) prediction = SignVote.Unknown;
            if (SignHandedness.TryIsLeft(handProbe.Handedness, SignLabelsAreMirrored, out left) &&
                SignFeatures.TryCompute(points, handProbe.FrameWidth, handProbe.FrameHeight, left, out features))
            {
                signClassificationTimer.Start();
                if (signUseKnn)
                {
                    var scores = signClassifier.Classify(features);
                    signRatios = scores.LabelScores;
                    prediction = scores.PredictedLabel;
                }
                else
                {
                    rule = SignRuleClassifier.Classify(features);
                    prediction = rule == null ? SignVote.Unknown : ActiveSignLabels.IndexOf(rule);
                }
                signClassificationTimer.Stop();
                if (signUseKnn && signPlayLog != null) rule = SignRuleClassifier.Classify(features);
            }
        }
        if (prediction == SignVote.NoHand) signRatios = null;
        signVoteResult = signVote.Add(now, prediction);
        string confirmed = signVoteResult.Confirmed && signVoteResult.Label >= 0 ? ActiveSignLabels[signVoteResult.Label] : null;
        bool recordingOther = signRecordOther;
        if (signPlayLog != null && !signPlayLog.TryWriteFrame(now, recordingOther ? SignLabels.Other : signSession.CurrentLabel,
            handProbe.FrameWidth, handProbe.FrameHeight, handProbe.Handedness, handProbe.HandednessScore, points,
            stage == SignGameSession.Stage.Practice ? "practice" : "challenge", SignMode, rule, confirmed))
        {
            StopSignRecording();
            signLogError = "기록 파일 저장에 실패했습니다.";
        }
        if (!recordingOther) UpdateSignSession(sessionTime, confirmed);
    }
    void UpdateSignSession(long now, string confirmed)
    {
        int previousAnswers = signSession.Answers.Count;
        int previousWrong = signSession.WrongAttempts;
        var previousStage = signSession.CurrentStage;
        string previousLabel = signSession.CurrentLabel;
        signSession.Update(now, confirmed);
        if (previousStage != signSession.CurrentStage || previousLabel != signSession.CurrentLabel) ResetSignVote();
        if (signSession.Answers.Count > previousAnswers)
        {
            var answer = signSession.Answers[signSession.Answers.Count - 1];
            signFeedback = signLines[answer.Correct ? 1 : 3]; signFeedbackUntil = SignTimeMs + 1500;
        }
        else if (signSession.WrongAttempts > previousWrong)
        {
            signFeedback = string.Format(signLines[2], signSession.LastWrongLabel); signFeedbackUntil = SignTimeMs + 1500;
        }
    }
    static readonly Rect SignSkipRect = new Rect(1090, 67, 86, 36);
    void SkipHandProbeAsSuccess()
    {
        signSession?.SkipAsCorrect();
        ResetSignVote();
        CloseHandProbe();
        practiceHighlight = true;
        OpenMenu(Menu.Practice);
        ShowToast("스킵 (정답 처리)");
    }
    void DrawHandProbe()
    {
        GUI.matrix = Canvas;
        NovelUI.Card(new Rect(80, 40, 1120, 820), NovelUI.Cream, 30, NovelUI.Pink);
        NovelUI.Label(new Rect(100, 60, 1080, 50), "수어 지문자", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (GameSettings.MiniGameSkip && (signSession == null || signSession.CurrentStage != SignGameSession.Stage.Result) && DrawSkipButton(SignSkipRect))
        {
            Event.current.Use();
            SkipHandProbeAsSuccess();
            return;
        }
        Rect view = new Rect(120, 130, 500, 375);
        if (handProbe != null && handProbe.CameraTexture != null && handProbe.FrameWidth > 32)
        {
            GUI.DrawTextureWithTexCoords(view, handProbe.CameraTexture, PreviewTexCoords(handProbe.FrameFlipH, handProbe.FrameFlipV));
            if (handProbe.IsHandDetected)
            {
                for (int i = 0; i < handEdges.Length; i += 2)
                {
                    Vector3 a = handProbe.Landmarks[handEdges[i]], b = handProbe.Landmarks[handEdges[i + 1]];
                    Vector2 from = HandPreviewPoint(view, a);
                    Vector2 to = HandPreviewPoint(view, b);
                    Matrix4x4 matrix = GUI.matrix; Vector2 delta = to - from;
                    NovelUI.RotateAround(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
                    GUI.color = Color.cyan; GUI.DrawTexture(new Rect(from.x, from.y - 2, delta.magnitude, 4), Texture2D.whiteTexture); GUI.color = Color.white; GUI.matrix = matrix;
                }
                foreach (Vector3 point in handProbe.Landmarks) { Vector2 p = HandPreviewPoint(view, point); NovelUI.Fill(new Rect(p.x - 5, p.y - 5, 10, 10), NovelUI.Pink, 5); }
            }
        }
        bool cameraFailed = SignCameraFailed;
        string label = signBrowse && signData != null && signData.Labels != null ? signData.Labels[signBrowseIndex] : signSession != null ? signSession.CurrentLabel : null;
        string stage = signSession == null ? "안내" : signSession.CurrentStage == SignGameSession.Stage.Intro ? "소개" : signSession.CurrentStage == SignGameSession.Stage.Practice ? "연습" : signSession.CurrentStage == SignGameSession.Stage.Challenge ? "도전" : "결과";
        NovelUI.Label(new Rect(650, 130, 480, 55), signBrowse ? "참고 그림 보기" : stage, NovelUI.Round, 25, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(650, 190, 480, 80), label ?? "", NovelUI.Round, 48, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (label != null && (signBrowse || (signError == null && !cameraFailed)))
        {
            Texture2D reference = Load("Sign/" + label);
            if (reference != null) GUI.DrawTexture(new Rect(930, 265, 180, 140), reference, ScaleMode.ScaleToFit);
            else NovelUI.Label(new Rect(930, 265, 180, 140), "참고 그림 준비 중", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleCenter);
        }
        if (signError != null || cameraFailed)
        {
            NovelUI.Label(new Rect(650, 410, 480, 60), signError ?? handProbe.Status, NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (cameraFailed && SignHasKnn && NovelUI.Button(new Rect(700, 480, 300, 45), signBrowse ? "다음 그림" : "카메라 없이 보기", false, 18))
            { if (signBrowse) signBrowseIndex = (signBrowseIndex + 1) % signData.Labels.Count; else { signBrowse = true; signBrowseIndex = 0; } while (signData.Labels[signBrowseIndex] == SignLabels.Other) signBrowseIndex = (signBrowseIndex + 1) % signData.Labels.Count; }
        }
        else if (signSession != null)
        {
            if (signSession.CurrentStage == SignGameSession.Stage.Intro)
            {
                NovelUI.Label(new Rect(650, 420, 480, 55), signLines[0], NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter);
                if (NovelUI.Button(new Rect(730, 480, 260, 50), "연습 시작", true, 20))
                {
                    signSession.BeginPractice();
                    if (handProbe != null) signFrameCount = handProbe.DetectionStats.FrameCount;
                    if (!signRecordOther) signSession.Update(SignSessionTime(SignTimeMs), null);
                    ResetSignVote();
                }
            }
            else if (signSession.CurrentStage == SignGameSession.Stage.Result)
            {
                string result = signLines[4] + "\n정답 " + signSession.CorrectCount + " / " + signSession.Answers.Count;
                foreach (var answer in signSession.Answers) result += $"\n{answer.Label}: {(answer.Correct ? "정답" : "오답")} {answer.ElapsedMs / 1000f:F1}초{(answer.WrongAttempts > 0 ? $" · 틀린 시도 {answer.WrongAttempts}회" : "")}";
                NovelUI.Label(new Rect(650, 290, 470, 260), result, NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.UpperLeft);
                if (signFeedback != null && SignTimeMs < signFeedbackUntil) NovelUI.Label(new Rect(650, 555, 470, 30), signFeedback, NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleLeft);
                if (NovelUI.Button(new Rect(740, 600, 250, 45), "다시 하기", true, 19)) RestartSignSession(System.Environment.TickCount);
            }
            else
            {
                long remaining = signSession.RemainingMs(SignSessionTime(SignTimeMs));
                long tenths = (remaining + 50) / 100;
                if (tenths != signCountdownTenths)
                {
                    signCountdownTenths = tenths;
                    signCountdown = $"남은 시간 {tenths / 10f:F1}초";
                }
                NovelUI.Label(new Rect(650, 270, 260, 35), signCountdown, NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleLeft);
                if (signFeedback != null && SignTimeMs < signFeedbackUntil) NovelUI.Label(new Rect(650, 305, 470, 30), signFeedback, NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleLeft);
                if (signUseKnn)
                {
                    var labels = ActiveSignLabels;
                    signScoreScroll = GUI.BeginScrollView(new Rect(650, 340, 270, 165), signScoreScroll, new Rect(0, 0, 245, labels.Count * 27));
                    for (int i = 0; i < labels.Count; i++)
                    {
                        float score = signRatios == null ? 0 : signRatios[i];
                        NovelUI.Label(new Rect(0, i * 27, 65, 24), labels[i] == SignLabels.Other ? "그 외" : labels[i], NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleLeft);
                        NovelUI.Fill(new Rect(65, 6 + i * 27, 170 * score, 12), NovelUI.Pink, 3);
                    }
                    GUI.EndScrollView();
                }
                if (signSession.CurrentStage == SignGameSession.Stage.Practice &&
                    NovelUI.Button(new Rect(930, 465, 180, 42), "다음 글자", false, 18))
                {
                    signSession.SkipPracticeLetter();
                    ResetSignVote();
                }
                NovelUI.Label(new Rect(650, 515, 300, 25), $"유지 {signVoteResult.HoldProgress * 100:F0}%", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleLeft);
                NovelUI.Fill(new Rect(650, 545, 260 * signVoteResult.HoldProgress, 12), NovelUI.Pink, 3);
            }
        }
        bool debug = GUI.Toggle(new Rect(120, 525, 160, 30), signDebug, "디버그 보기");
        if (debug != signDebug)
        {
            signDebug = debug;
            signNextTimingUpdate = 0;
        }
        if (handProbe != null && signDebug && signDebugTiming != null)
            NovelUI.Label(new Rect(120, 560, 520, 90), signDebugTiming, NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.UpperLeft);

        if (Application.isEditor || Debug.isDebugBuild)
        {
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && signError == null && !cameraFailed;
            bool record = GUI.Toggle(new Rect(300, 525, 90, 30), signPlayLog != null, "기록");
            GUI.enabled = wasEnabled;
            if (record && signPlayLog == null)
            {
                string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "SignPlay");
                signLogError = SignPlayLog.TryOpen(directory, System.DateTime.Now, SignTimeMs, out signPlayLog)
                    ? null : "기록 파일을 열지 못했습니다.";
            }
            else if (!record && signPlayLog != null) StopSignRecording();

            if (signPlayLog != null)
                SetSignRecordOther(GUI.Toggle(new Rect(405, 525, 170, 30), signRecordOther, "그 외 손 모양"), SignTimeMs);
        }
        string privacy = signPlayLog != null
            ? "손 관절 좌표와 제시 글자를 이 PC의 Logs/SignPlay 폴더에 저장합니다(영상은 저장하지 않음)"
            : "영상은 저장·전송되지 않고, 손 관절 위치만 이 PC 안에서 계산합니다.";
        NovelUI.Label(new Rect(120, 660, 1040, 28), privacy, NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (signTiming != null)
            NovelUI.Label(new Rect(120, 689, 1040, 28), signTiming, NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (signLogError != null)
            NovelUI.Label(new Rect(120, 800, 1040, 28), signLogError, NovelUI.Soft, 16, NovelUI.Berry, TextAnchor.MiddleCenter);
        Rect modeButton = new Rect(930, 730, 220, 44);
        string modeText = signUseKnn ? "인식 방식: k-NN" : "인식 방식: 규칙";
        if (SignHasKnn)
        {
            if (NovelUI.Button(modeButton, modeText, false, 18))
            {
                signUseKnn = !signUseKnn;
                RestartSignSession();
            }
        }
        else NovelUI.Label(modeButton, modeText, NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (NovelUI.Button(new Rect(490, 730, 300, 60), "돌아가기", false, 22)) { CloseHandProbe(); practiceHighlight = true; OpenMenu(Menu.Practice); }
    }
    // Opens the gaze test screen; the game starts right away when the tracker is ready, otherwise once calibrated.
    void StartGazeGame(GazeGame game, bool story, bool? manual = null)
    {
        forceManualHighlight = manual ?? !GazeReadyForDodge;
        novelAudio.Play("Select");
        menu = Menu.None;
        gazeSession = game;
        gazeSessionStory = story;
        gazeSessionSawResult = gazeSessionSawGame = false;
        gazeSessionGain = 0;
        bool alreadyOpen = gazeTestOpen;
        if (!alreadyOpen) OpenGazeTest();
        if (forceManualHighlight || GazeReadyForDodge)
        {
            if (game == GazeGame.Match) StartEyeMatchGame(); else StartDodgeGame();
        }
        else
        {
            matchAfterCalibration = game == GazeGame.Match;
            dodgeAfterCalibration = game == GazeGame.Dodge;
        }
    }

    // Called after a slot's result is dismissed (Advance).
    void QueueStoryGazeEvents(bool lastSlot)
    {
        if (scene < 1) return;
        if (lastSlot && !dodgeEventDone && affection >= DodgeEventAffection) { dodgeEventDone = true; dodgePending = true; }
        if (!matchEventDone && affection >= MatchEventAffection) { matchEventDone = true; storyGazeEvent = GazeGame.Match; return; }
        if (dodgePending) { dodgePending = false; storyGazeEvent = GazeGame.Dodge; }
    }

    // Intro card over the scene: the professor invites the player; start or skip.
    void DrawStoryGazeIntro()
    {
        if (scene >= 1 && scene <= 7) DrawScene(false);
        GUI.matrix = Canvas;
        NovelUI.Tint(new Color(1f, .82f, .9f, .35f));
        bool match = storyGazeEvent == GazeGame.Match;
        Rect card = new Rect(290, 250, 700, 400);
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 36, NovelUI.Pink);
        NovelUI.Pill(card.x + 40, card.y + 34, 40, "EVENT", NovelUI.Pink, Color.white, 18);
        NovelUI.Label(new Rect(card.x, card.y + 80, card.width, 50), match ? "눈빛 맞추기" : "눈빛 피하기", NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
        // The recorded eye-match video stars Sim; eye-dodge uses the selected professor's portrait.
        bool sim = !match || (professor >= 0 && ProfessorName.Contains("심한뫼"));
        string line = match ? "...잠깐, 제 눈을 한번 봐 줄래요?" : "마지막으로 하나만요. 제 눈빛, 피할 수 있겠어요?";
        string who = sim ? ProfessorName : "특별 출연 · 심한뫼 교수님";
        if (!sim) line = (match ? "옆 연구실 심한뫼 교수님이 불쑥 찾아오셨다. " : "엔딩 직전, 심한뫼 교수님이 다시 나타나셨다. ") + "\"" + line + "\"";
        else line = "\"" + line + "\"";
        NovelUI.Label(new Rect(card.x + 40, card.y + 140, card.width - 80, 100), $"{who}\n{line}", NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
        NovelUI.Label(new Rect(card.x + 40, card.y + 246, card.width - 80, 30), "결과에 따라 호감도 +0~2 · 장치 없이도 플레이 가능", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (NovelUI.Button(new Rect(card.x + 90, card.yMax - 96, 250, 60), "시작하기", true, 22))
        {
            GazeGame game = storyGazeEvent;
            storyGazeEvent = GazeGame.None;
            StartGazeGame(game, true);
        }
        if (NovelUI.Button(new Rect(card.xMax - 340, card.yMax - 96, 250, 60), "건너뛰기", false, 21))
        {
            novelAudio.Play("Back");
            storyGazeEvent = GazeGame.None;
            NextStoryGazeEvent();
        }
    }

    void NextStoryGazeEvent()
    {
        if (dodgePending) { dodgePending = false; storyGazeEvent = GazeGame.Dodge; }
    }

    // Follows a launched game: remembers its result, and when the player leaves (돌아가기 on the result card, or
    // closing the gaze test) applies the story 호감도 and returns to the story or the minigame menu.
    void TickGazeGameSession()
    {
        if (gazeSession == GazeGame.None) return;
        bool match = gazeSession == GazeGame.Match;
        if (match ? matchFinished : dodgeFinished)
        {
            gazeSessionSawResult = true;
            gazeSessionGain = match ? Mathf.Clamp(matchAffectionGain, 0, 2) : dodgeAlive >= DodgeGreatSeconds ? 2 : dodgeAlive >= DodgeGoodSeconds ? 1 : 0;
        }
        bool open = match ? MatchScreenOpen : DodgeScreenOpen;
        if (open) gazeSessionSawGame = true;
        // Left: the gaze test closed, or the game screen closed after it opened (result card or 게임 끝내기).
        bool left = !gazeTestOpen || (gazeSessionSawGame && !open);
        if (!left) return;

        bool story = gazeSessionStory;
        int gain = gazeSessionSawResult ? gazeSessionGain : 0;
        gazeSession = GazeGame.None;
        forceManualHighlight = false;
        gazeSessionStory = false;
        matchAfterCalibration = dodgeAfterCalibration = false;
        if (gazeTestOpen)
        {
            // Story eye-contact tracking shares the tracker, so only hide the screen while a session is running.
            if (eyeContactSessionActive) gazeTestOpen = false;
            else CloseGazeTest();
        }
        if (story)
        {
            if (gain > 0)
            {
                affection += gain;
                ShowToast($"호감도 +{gain}");
            }
            NextStoryGazeEvent();
        }
        else OpenMenu(Menu.Practice);
    }
}
