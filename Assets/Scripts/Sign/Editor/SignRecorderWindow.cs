#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 수어 지문자 데이터 수집용 에디터 전용 녹화 창.
/// Editor 폴더 안에 위치해 플레이어 빌드에 포함되지 않으며,
/// 영상·이미지 없이 손 관절 좌표(21개)만 SignSample 공통 CSV로 시도 단위 원자 저장한다.
/// </summary>
public sealed class SignRecorderWindow : EditorWindow
{
    const string PrivacyNotice = "관절 좌표와, 켜 두면 손 부분만 잘라 낸 이미지(224×224)를 저장합니다. 전체 영상은 저장하지 않습니다. 팀 합의에 따라 녹화하세요.";
    const string LimitNotice = "한계: 단일 에디터·단일 창 기준이며 프로세스 간 파일 잠금은 지원하지 않습니다.";
    const string DefaultLabels = "ㅂ,ㅇ,ㅎ,ㄹ,ㅅ,_other";   // 확정 글자 5개 + 그 밖의 손 모양(2026-11-27)
    static readonly string[] ConfirmedLabels = { "ㅂ", "ㅇ", "ㅎ", "ㄹ", "ㅅ", "_other" };
    const int TargetTrialsPerLabel = 15, MinValidFramesPerTrial = 10, LandmarkCount = 21;
    const double RepaintIntervalSeconds = 0.08;

    static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
    static readonly char[] ForbiddenTokenChars = { '/', '\\', ',', '"', '\r', '\n' };
    static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    static readonly char[] ForbiddenLabelChars = { ',', '"', '\r', '\n' };

    // 같은 에디터 내 중복 창 차단용 기본 창 참조 및 OnGUI 정적 스타일 캐시
    static SignRecorderWindow s_PrimaryWindow;
    static GUIStyle s_Bold, s_Detected, s_NotDetected, s_Recording, s_MiniWrap, s_Right, s_RightDone;

    [SerializeField] string personId = "p01", sessionId = "s01", labelListInput = DefaultLabels;
    [SerializeField] int selectedLabelIndex;
    [SerializeField] bool saveHandImages = true;   // 이미지 CNN(GPU 학습)용. 실패해도 CSV 저장에는 영향이 없다.

    GameObject recorderHost;
    HandLandmarkSource handSource;
    long lastSeenDetectionFrameCount = -1, activeTrialFrameIndex, lastTrialTimestampMs = -1;
    double nextRepaintAt;

    bool isRecording, recordingStartedBySpace;
    string activePerson = "", activeSession = "", activeLabel = "", activeTrialText = "", activeCsvPath = "";
    int activeTrialSkippedFrames;
    readonly System.Diagnostics.Stopwatch trialStopwatch = new System.Diagnostics.Stopwatch();
    readonly List<SignSample> currentTrialSamples = new List<SignSample>(256);

    // 손 이미지(JPG). 시도가 CSV에 저장될 때만 파일로 쓴다.
    struct HandShot { public byte[] Jpg; public int Index; public bool Left; }
    readonly List<HandShot> currentTrialShots = new List<HandShot>(12), pendingShots = new List<HandShot>(12);
    long lastShotMs = -1;
    byte[] shotRgba;
    Color32[] shotPixels;
    string lastShotError = "";

    // 저장 실패 시 메모리에 보존해 '다시 저장'으로 재시도할 시도 버퍼
    readonly List<SignSample> pendingSaveSamples = new List<SignSample>();
    string pendingCsvPath = "", pendingLabel = "", pendingTrialText = "";
    int pendingSkippedFrames;

    // UI 파싱·집계 상태
    readonly List<string> parsedLabels = new List<string>();
    string[] parsedLabelArray = Array.Empty<string>();
    string cachedLabelInput, lastScannedPath = "", csvValidationError = "", noticeMessage = "";
    readonly Dictionary<string, int> labelTrialCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly Dictionary<string, int> labelMaxTrialNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
    bool hasLastTrialInFile;
    TrialKey lastTrialInFile;
    int lastTrialFrameCount, lastTrialSkippedFrames;
    MessageType noticeType = MessageType.Info;
    Vector2 scrollPos;

    enum ConflictKind { Clear, Blocked, Uncertain }
    struct TrialKey { public string Person, Session, Trial, Label; public int FrameCount; }

    bool IsSecondaryWindow => s_PrimaryWindow != null && s_PrimaryWindow != this;
    bool HasPendingSave => pendingSaveSamples.Count > 0;

    [MenuItem("Tools/Sign/Recorder")]
    public static void OpenWindow() {
        var window = GetWindow<SignRecorderWindow>("Sign Recorder");
        window.minSize = new Vector2(460f, 600f);
        window.Show();
    }

    void OnEnable() {
        titleContent = new GUIContent("Sign Recorder");
        if (s_PrimaryWindow == null) s_PrimaryWindow = this;
        ParseLabels(); RefreshCsvSummary();
        EditorApplication.update -= OnEditorUpdate; EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged; EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    void OnDisable() {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        StopAndCleanupTracker(saveActiveTrial: true);
        if (s_PrimaryWindow == this) s_PrimaryWindow = null;
    }

    void OnDestroy() { StopAndCleanupTracker(saveActiveTrial: true); if (s_PrimaryWindow == this) s_PrimaryWindow = null; }
    void OnLostFocus() { if (isRecording && recordingStartedBySpace) { StopTrial(); Repaint(); } }

    void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) { StopAndCleanupTracker(saveActiveTrial: true); Repaint(); }
        else if (state == PlayModeStateChange.EnteredPlayMode) { RefreshCsvSummary(); Repaint(); }
    }

    void OnEditorUpdate() {
        if (!EditorApplication.isPlaying) {
            if (recorderHost != null || handSource != null || isRecording) { StopAndCleanupTracker(saveActiveTrial: true); Repaint(); }
            return;
        }
        if (handSource == null) { if (isRecording) StopTrial(); return; }

        bool needRepaint = false;
        long count = handSource.DetectionStats.FrameCount;
        if (count != lastSeenDetectionFrameCount) {
            lastSeenDetectionFrameCount = count;
            needRepaint = true;
            if (isRecording && handSource.IsHandDetected) CaptureFrame();
        }
        double now = EditorApplication.timeSinceStartup;
        if (needRepaint || now >= nextRepaintAt) { nextRepaintAt = now + RepaintIntervalSeconds; Repaint(); }
    }

    void CaptureFrame() {
        long ts = Math.Max(trialStopwatch.ElapsedMilliseconds, lastTrialTimestampMs + 1);
        if (!TryBuildValidSample(handSource, activePerson, activeSession, activeTrialText, activeLabel, activeTrialFrameIndex, ts, out SignSample sample)) {
            activeTrialSkippedFrames++;
            return;
        }
        lastTrialTimestampMs = ts;
        currentTrialSamples.Add(sample);
        activeTrialFrameIndex++;
        TryCaptureShot(sample, ts);
    }

    // 손 부분 이미지 1장. 어떤 실패도 녹화(CSV)를 막지 않는다. 건너뛴 이유는 lastShotError에 남겨 저장 안내에 보여 준다.
    void TryCaptureShot(SignSample sample, long ts) {
        if (!saveHandImages || !SignHandCrop.ShouldCapture(ts, lastShotMs, currentTrialShots.Count)) return;
        try {
            WebCamTexture cam = handSource.CameraTexture;
            if (cam == null || !cam.isPlaying) { lastShotError = "카메라 없음"; return; }
            int w = cam.width, h = cam.height;
            if (w != sample.Width || h != sample.Height) { lastShotError = "카메라 해상도가 바뀜"; return; }
            if (!SignHandCrop.IsSafeLabel(sample.Label)) { lastShotError = "글자 이름에 쓸 수 없는 문자(-, /, 공백 등)"; return; }
            if (HasCaseOnlyDuplicateLabel(sample.Label)) { lastShotError = "대소문자만 다른 글자가 있어 이미지 이름이 겹침"; return; }
            int x, y, size;
            if (!SignHandCrop.TryRect(sample.Landmarks, w, h, SignHandCrop.DefaultMargin, out x, out y, out size)) { lastShotError = "손이 너무 작거나 영상 밖"; return; }
            if (shotPixels == null || shotPixels.Length != w * h) { shotPixels = new Color32[w * h]; shotRgba = new byte[Math.Min(w, h) * Math.Min(w, h) * 4]; }
            cam.GetPixels32(shotPixels);
            // 잘라 낼 영역만 변환한다. 관절 좌표와 같은 방향(좌상단 원점, FrameFlip 적용)으로 펼쳐 size×size RGBA로 만든다.
            for (int v = 0; v < size; v++) {
                for (int u = 0; u < size; u++) {
                    int sx, sy;
                    SignHandCrop.ToSource(x + u, y + v, w, h, handSource.FrameFlipH, handSource.FrameFlipV, out sx, out sy);
                    Color32 c = shotPixels[sy * w + sx];
                    int o = (v * size + u) * 4;
                    shotRgba[o] = c.r; shotRgba[o + 1] = c.g; shotRgba[o + 2] = c.b; shotRgba[o + 3] = 255;
                }
            }
            // 이미 영상 방향으로 펼쳤으므로 flip 없이 224×224로 줄인다.
            byte[] rgb = SignHandCrop.CropRgb(shotRgba, size, size, false, false, 0, 0, size, SignHandCrop.ImageSize);
            var tex = new Texture2D(SignHandCrop.ImageSize, SignHandCrop.ImageSize, TextureFormat.RGB24, false);
            byte[] jpg;
            try { tex.LoadRawTextureData(SignHandCrop.FlipRows(rgb, SignHandCrop.ImageSize)); tex.Apply(false); jpg = tex.EncodeToJPG(90); }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
            if (jpg == null || jpg.Length == 0) { lastShotError = "JPG 인코딩 실패"; return; }
            currentTrialShots.Add(new HandShot { Jpg = jpg, Index = currentTrialShots.Count + 1, Left = sample.Handedness == "Left" });
            lastShotMs = ts;
        } catch (Exception e) {
            lastShotError = e.Message;
            lastShotMs = ts;   // 같은 오류를 매 프레임 반복하지 않는다.
            UnityEngine.Debug.LogWarning("[SignRecorder] 손 이미지 저장 건너뜀: " + e.Message);
        }
    }

    // NTFS는 대소문자를 구분하지 않아 A와 a의 이미지 이름이 겹친다. 그런 글자는 이미지를 저장하지 않는다.
    bool HasCaseOnlyDuplicateLabel(string label) {
        for (int i = 0; i < parsedLabels.Count; i++)
            if (!string.Equals(parsedLabels[i], label, StringComparison.Ordinal) && string.Equals(parsedLabels[i], label, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static bool TryBuildValidSample(HandLandmarkSource src, string person, string session, string trial, string label, long frame, long tsMs, out SignSample sample) {
        sample = null;
        if (src == null || src.FrameWidth <= 0 || src.FrameHeight <= 0) return false;
        if (src.Handedness != "Left" && src.Handedness != "Right") return false;
        if (!IsFinite(src.HandednessScore) || src.Landmarks == null || src.Landmarks.Length != LandmarkCount) return false;

        var flat = new float[LandmarkCount * 3];
        for (int i = 0; i < LandmarkCount; i++) {
            Vector3 p = src.Landmarks[i];
            if (!IsFinite(p.x) || !IsFinite(p.y) || !IsFinite(p.z)) return false;
            flat[3 * i] = p.x; flat[3 * i + 1] = p.y; flat[3 * i + 2] = p.z;
        }
        sample = new SignSample {
            Person = person, Session = session, Trial = trial, Label = label,
            Frame = frame, TimestampMs = tsMs, Width = src.FrameWidth, Height = src.FrameHeight,
            Handedness = src.Handedness, HandednessScore = src.HandednessScore, Landmarks = flat
        };
        return true;
    }

    static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    static void EnsureStyles() {
        if (s_Bold != null) return;
        s_Bold = new GUIStyle(EditorStyles.boldLabel);
        s_Detected = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.15f, 0.65f, 0.25f) } };
        s_NotDetected = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.80f, 0.35f, 0.15f) } };
        s_Recording = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.85f, 0.15f, 0.15f) } };
        s_MiniWrap = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        s_Right = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
        s_RightDone = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.15f, 0.65f, 0.25f) } };
    }

    void OnGUI() {
        EnsureStyles();
        if (s_PrimaryWindow == null) s_PrimaryWindow = this;
        if (!string.Equals(cachedLabelInput, labelListInput, StringComparison.Ordinal)) ParseLabels();
        HandleSpaceKey();

        EditorGUILayout.HelpBox(PrivacyNotice, MessageType.Info);
        if (IsSecondaryWindow) EditorGUILayout.HelpBox("이미 다른 Sign Recorder 창이 열려 있습니다. 이 창에서는 녹화·저장·삭제가 비활성화됩니다.", MessageType.Warning);
        if (!EditorApplication.isPlaying) EditorGUILayout.HelpBox("Play 모드에서만 동작합니다. Unity 상단의 Play 버튼을 눌러 실행하세요.", MessageType.Warning);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        try {
            DrawInputSection();
            DrawControlsAndStatus();
            DrawTrialTable();
            DrawLastTrialAndPending();
            if (!string.IsNullOrEmpty(csvValidationError)) EditorGUILayout.HelpBox(csvValidationError, MessageType.Error);
            if (!string.IsNullOrEmpty(noticeMessage)) EditorGUILayout.HelpBox(noticeMessage, noticeType);
        }
        finally { EditorGUILayout.EndScrollView(); }

        EditorGUILayout.LabelField(LimitNotice, s_MiniWrap);
    }

    void HandleSpaceKey() {
        Event evt = Event.current;
        if (evt == null || evt.keyCode != KeyCode.Space || IsSecondaryWindow) return;
        if (EditorWindow.focusedWindow != this || EditorGUIUtility.editingTextField || !EditorApplication.isPlaying || handSource == null) return;
        if (evt.type == EventType.KeyDown) { if (!isRecording && CanStartTrial(out _)) StartTrial(startedBySpace: true); evt.Use(); Repaint(); }
        else if (evt.type == EventType.KeyUp) { if (isRecording && recordingStartedBySpace) StopTrial(); evt.Use(); Repaint(); }
    }

    void DrawInputSection() {
        EditorGUILayout.LabelField("녹화 설정", s_Bold);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            EditorGUI.BeginDisabledGroup(isRecording);
            EditorGUI.BeginChangeCheck();
            personId = EditorGUILayout.TextField("사람 ID (가명)", personId);
            sessionId = EditorGUILayout.TextField("세션 ID", sessionId);
            labelListInput = EditorGUILayout.TextField("글자 목록 (쉼표 구분)", labelListInput);
            if (EditorGUI.EndChangeCheck()) { ParseLabels(); RefreshCsvSummary(); }
            saveHandImages = EditorGUILayout.ToggleLeft("손 이미지도 함께 저장 (이미지 학습용, 손 부분만 224×224 JPG)", saveHandImages);
            if (parsedLabels.Count > 0) {
                selectedLabelIndex = Mathf.Clamp(selectedLabelIndex, 0, parsedLabels.Count - 1);
                selectedLabelIndex = GUILayout.SelectionGrid(selectedLabelIndex, parsedLabelArray, Mathf.Clamp(parsedLabels.Count, 1, 4));
            }
            EditorGUI.EndDisabledGroup();
            if (!ValidateInputs(out string err)) EditorGUILayout.HelpBox(err, MessageType.Error);
            else EditorGUILayout.LabelField("저장 경로: " + GetCsvPath(), s_MiniWrap);
            string unexpected = UnexpectedLabels();
            if (unexpected.Length > 0) EditorGUILayout.HelpBox("확정 글자(ㅂ, ㅇ, ㅎ, ㄹ, ㅅ, _other)가 아닌 글자가 있습니다: " + unexpected + "\n학습 데이터에는 확정 글자만 쓰세요. 오타나 옛 목록(A,B,C)이면 입력칸을 고치세요.", MessageType.Warning);
            else {
                string missing = MissingLabels();
                if (missing.Length > 0) EditorGUILayout.HelpBox("글자 목록에 없는 확정 글자: " + missing, MessageType.Info);
            }
        }
    }

    void DrawControlsAndStatus() {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("추적 · 시도 녹화", s_Bold);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            bool running = handSource != null;
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginDisabledGroup(IsSecondaryWindow || !EditorApplication.isPlaying || running);
                if (GUILayout.Button("손 추적 시작", GUILayout.Height(26f))) TryStartTracker();
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(!running);
                if (GUILayout.Button("손 추적 종료", GUILayout.Height(26f))) {
                    StopAndCleanupTracker(saveActiveTrial: true);
                    SetNotice("손 추적기를 종료하고 임시 오브젝트를 해제했습니다.", MessageType.Info);
                }
                EditorGUI.EndDisabledGroup();
            }

            string curLabel = GetCurrentLabel();
            string trialDisp = isRecording ? activeTrialText : GetNextTrialNumber(curLabel).ToString(CultureInfo.InvariantCulture);
            EditorGUILayout.LabelField($"선택 글자: {(string.IsNullOrEmpty(curLabel) ? "-" : curLabel)}  |  시도 번호: #{trialDisp}");

            bool canStart = !IsSecondaryWindow && EditorApplication.isPlaying && running && CanStartTrial(out _);
            EditorGUI.BeginDisabledGroup(!canStart && !isRecording);
            string recLabel = isRecording
                ? $"시도 녹화 종료 ({activeLabel} #{activeTrialText} · 유효 {currentTrialSamples.Count}, 건너뜀 {activeTrialSkippedFrames})"
                : "시도 녹화 시작 (또는 창 포커스 중 Space 유지)";
            if (GUILayout.Button(recLabel, GUILayout.Height(32f))) { if (isRecording) StopTrial(); else StartTrial(startedBySpace: false); }
            EditorGUI.EndDisabledGroup();

            string stateText = !EditorApplication.isPlaying ? "대기 중 (Edit 모드)" : !running ? "대기 중 (손 추적 미시작)"
                : isRecording ? $"녹화 중{(recordingStartedBySpace ? " [Space]" : "")} · 유효 {currentTrialSamples.Count} / 건너뜀 {activeTrialSkippedFrames}"
                : "추적 중 (녹화 대기)";
            EditorGUILayout.LabelField("현재 상태", stateText, isRecording ? s_Recording : EditorStyles.label);

            if (!running) return;
            bool det = handSource.IsHandDetected;
            EditorGUILayout.LabelField("추적기 상태", handSource.Status ?? "");
            EditorGUILayout.LabelField("손 감지 여부", det ? "감지됨" : "미감지", det ? s_Detected : s_NotDetected);
            string side = det ? $"{handSource.Handedness} ({handSource.HandednessScore.ToString("0.00", CultureInfo.InvariantCulture)})" : "-";
            EditorGUILayout.LabelField("좌우 / 크기", $"{side}  |  {handSource.FrameWidth} x {handSource.FrameHeight}");
            DrawLandmarkDotsPreview();
        }
    }

    void DrawLandmarkDotsPreview() {
        Rect box = GUILayoutUtility.GetRect(160f, 92f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(box, new Color(0.12f, 0.13f, 0.16f));
        if (handSource == null || !handSource.IsHandDetected || handSource.Landmarks == null) return;
        Vector3[] pts = handSource.Landmarks;
        for (int i = 0, len = Math.Min(LandmarkCount, pts.Length); i < len; i++) {
            float px = box.x + Mathf.Clamp01(1f - pts[i].x) * box.width;
            float py = box.y + Mathf.Clamp01(pts[i].y) * box.height;
            EditorGUI.DrawRect(new Rect(px - 2.5f, py - 2.5f, 5f, 5f), i == 0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 0.9f, 0.95f));
        }
    }

    void DrawTrialTable() {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"글자별 시도 수 (목표 {TargetTrialsPerLabel}회)", s_Bold);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            if (parsedLabels.Count == 0) { EditorGUILayout.LabelField("표시할 글자가 없습니다."); return; }
            string cur = GetCurrentLabel();
            for (int i = 0; i < parsedLabels.Count; i++) {
                string label = parsedLabels[i];
                int count = labelTrialCounts.TryGetValue(label, out int c) ? c : 0;
                bool done = count >= TargetTrialsPerLabel;
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUI.BeginDisabledGroup(isRecording);
                    if (GUILayout.Button(label == cur ? $"▶ {label}" : label, GUILayout.Width(86f))) selectedLabelIndex = i;
                    EditorGUI.EndDisabledGroup();
                    Rect bar = GUILayoutUtility.GetRect(80f, 18f, GUILayout.ExpandWidth(true));
                    float ratio = Mathf.Clamp01((float)count / TargetTrialsPerLabel);
                    EditorGUI.ProgressBar(bar, ratio, done ? "목표 달성" : $"{Mathf.RoundToInt(ratio * 100f)}%");
                    EditorGUILayout.LabelField($"{count} / {TargetTrialsPerLabel}", done ? s_RightDone : s_Right, GUILayout.Width(76f));
                }
            }
        }
    }

    void DrawLastTrialAndPending() {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("최근 시도 · 정리", s_Bold);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            int frames = isRecording ? currentTrialSamples.Count : lastTrialFrameCount;
            int skipped = isRecording ? activeTrialSkippedFrames : lastTrialSkippedFrames;
            EditorGUILayout.LabelField(isRecording ? "현재 시도 프레임" : "최근 시도 프레임", $"유효 {frames}프레임 (건너뜀 {skipped})");
            EditorGUILayout.LabelField("파일 마지막 시도", hasLastTrialInFile ? $"{lastTrialInFile.Label} #{lastTrialInFile.Trial} ({lastTrialInFile.FrameCount}프레임)" : "없음");

            if (HasPendingSave) {
                EditorGUILayout.HelpBox($"저장 실패로 메모리에 보관 중인 시도: {pendingLabel} #{pendingTrialText} ({pendingSaveSamples.Count}프레임)", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUI.BeginDisabledGroup(IsSecondaryWindow || isRecording);
                    if (GUILayout.Button("다시 저장", GUILayout.Height(26f))) TrySavePendingTrial();
                    if (GUILayout.Button("보관 시도 버리기", GUILayout.Width(120f), GUILayout.Height(26f))) { pendingSaveSamples.Clear(); pendingShots.Clear(); SetNotice("보관 중이던 미저장 시도를 비웠습니다.", MessageType.Info); }
                    EditorGUI.EndDisabledGroup();
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginDisabledGroup(IsSecondaryWindow || isRecording || !hasLastTrialInFile || !string.IsNullOrEmpty(csvValidationError));
                if (GUILayout.Button(hasLastTrialInFile ? $"마지막 시도 삭제 ({lastTrialInFile.Label} #{lastTrialInFile.Trial})" : "마지막 시도 삭제", GUILayout.Height(24f))) DeleteLastTrial();
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(isRecording);
                if (GUILayout.Button("새로고침", GUILayout.Width(84f), GUILayout.Height(24f))) RefreshCsvSummary();
                EditorGUI.EndDisabledGroup();
            }
        }
    }

    void TryStartTracker() {
        if (IsSecondaryWindow || !EditorApplication.isPlaying || handSource != null) return;
        if (WebCamTexture.devices.Length == 0) { SetNotice("연결된 웹캠을 찾을 수 없습니다.", MessageType.Error); return; }

        ConflictKind conflict = CheckWebcamConflict(out string msg);
        if (conflict == ConflictKind.Blocked) { SetNotice(msg, MessageType.Warning); UnityEngine.Debug.LogWarning("[SignRecorder] " + msg); return; }
        if (conflict == ConflictKind.Uncertain && !EditorUtility.DisplayDialog("다른 카메라 사용 중일 수 있음", msg, "계속 시작", "취소")) return;

        recorderHost = new GameObject("[SignRecorder] HandLandmarkSource") { hideFlags = HideFlags.DontSave };
        handSource = recorderHost.AddComponent<HandLandmarkSource>();
        lastSeenDetectionFrameCount = handSource.DetectionStats.FrameCount;
        SetNotice("임시 HandLandmarkSource를 생성해 손 추적을 시작했습니다.", MessageType.Info);
        Repaint();
    }

    ConflictKind CheckWebcamConflict(out string message) {
        foreach (var h in UnityEngine.Object.FindObjectsByType<HandLandmarkSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (h != null && h != handSource) { message = "게임의 손 추적 화면이 이미 웹캠을 사용 중입니다. 닫은 뒤 시작하세요."; return ConflictKind.Blocked; }
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == "HomulerGaze") { message = "게임의 시선 추적이 웹캠을 사용 중입니다. 중지한 뒤 시작하세요."; return ConflictKind.Blocked; }
        foreach (var cam in Resources.FindObjectsOfTypeAll<WebCamTexture>())
            if (cam != null && cam.isPlaying && (handSource == null || cam != handSource.CameraTexture)) { message = "다른 웹캠(WebCamTexture)이 실행 중입니다. 중지한 뒤 시작하세요."; return ConflictKind.Blocked; }

        GameController gc = UnityEngine.Object.FindFirstObjectByType<GameController>();
        if (gc != null) {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.NonPublic;
            Type t = typeof(GameController);
            FieldInfo fObj = t.GetField("gazeObject", f), fTrk = t.GetField("gazeTracker", f), fReq = t.GetField("requestingCamera", f), fPhoto = t.GetField("photoCaptureOpen", f);
            if (fObj == null || fReq == null) { message = "다른 카메라 사용 중일 수 있음: 게임의 시선 추적 상태를 자동 판단하기 어렵습니다.\n웹캠 시선 추적이 꺼져 있는지 확인한 뒤 계속하시겠습니까?"; return ConflictKind.Uncertain; }
            if ((fObj.GetValue(gc) as UnityEngine.Object) != null || (fTrk != null && (fTrk.GetValue(gc) as UnityEngine.Object) != null) ||
                (fReq.GetValue(gc) is bool req && req) || (fPhoto != null && fPhoto.GetValue(gc) is bool ph && ph)) {
                message = "게임의 시선 추적 또는 웹캠 사진 촬영이 카메라를 사용 중입니다. 중지한 뒤 시작하세요.";
                return ConflictKind.Blocked;
            }
        }
        message = "";
        return ConflictKind.Clear;
    }

    void StopAndCleanupTracker(bool saveActiveTrial) {
        if (isRecording) {
            if (saveActiveTrial) StopTrial();
            else { isRecording = false; recordingStartedBySpace = false; trialStopwatch.Stop(); currentTrialSamples.Clear(); }
        }
        if (handSource != null) { handSource.Release(); handSource = null; }
        if (recorderHost != null) {
            if (Application.isPlaying) UnityEngine.Object.Destroy(recorderHost);
            else UnityEngine.Object.DestroyImmediate(recorderHost);
            recorderHost = null;
        }
        lastSeenDetectionFrameCount = -1;
    }

    bool CanStartTrial(out string reason) {
        if (IsSecondaryWindow) { reason = "다른 녹화 창이 활성화되어 있습니다."; return false; }
        if (HasPendingSave) { reason = "저장되지 않은 이전 시도가 있습니다. 먼저 '다시 저장' 또는 버리기를 선택하세요."; return false; }
        if (!ValidateInputs(out reason)) return false;
        if (!string.IsNullOrEmpty(csvValidationError)) { reason = csvValidationError; return false; }
        if (string.IsNullOrEmpty(GetCurrentLabel())) { reason = "현재 선택된 글자가 없습니다."; return false; }
        reason = "";
        return true;
    }

    void StartTrial(bool startedBySpace) {
        if (!CanStartTrial(out string reason)) { SetNotice(reason, MessageType.Error); return; }
        if (handSource == null) { SetNotice("먼저 손 추적을 시작하세요.", MessageType.Warning); return; }
        string path = GetCsvPath();
        if (!string.Equals(lastScannedPath, path, StringComparison.Ordinal)) RefreshCsvSummary();
        if (!string.IsNullOrEmpty(csvValidationError)) { SetNotice(csvValidationError, MessageType.Error); return; }

        activePerson = personId.Trim(); activeSession = sessionId.Trim(); activeLabel = GetCurrentLabel();
        activeCsvPath = path; activeTrialText = GetNextTrialNumber(activeLabel).ToString(CultureInfo.InvariantCulture);
        activeTrialFrameIndex = 0; activeTrialSkippedFrames = 0; lastTrialTimestampMs = -1;
        currentTrialSamples.Clear(); currentTrialShots.Clear(); lastShotMs = -1; lastShotError = "";
        lastSeenDetectionFrameCount = handSource.DetectionStats.FrameCount;
        trialStopwatch.Restart();
        isRecording = true; recordingStartedBySpace = startedBySpace;
        SetNotice($"시도 녹화 시작: {activeLabel} #{activeTrialText}", MessageType.Info);
    }

    void StopTrial() {
        if (!isRecording) return;
        isRecording = false; recordingStartedBySpace = false; trialStopwatch.Stop();
        lastTrialFrameCount = currentTrialSamples.Count;
        lastTrialSkippedFrames = activeTrialSkippedFrames;

        if (currentTrialSamples.Count < MinValidFramesPerTrial) {
            currentTrialSamples.Clear(); currentTrialShots.Clear();
            SetNotice($"너무 짧음: 유효 프레임 {lastTrialFrameCount}개 (최소 {MinValidFramesPerTrial}개 필요, 건너뜀 {lastTrialSkippedFrames}개) — 저장하지 않았습니다.", MessageType.Warning);
            return;
        }
        pendingSaveSamples.Clear(); pendingSaveSamples.AddRange(currentTrialSamples);
        pendingShots.Clear(); pendingShots.AddRange(currentTrialShots); currentTrialShots.Clear();
        pendingCsvPath = activeCsvPath; pendingLabel = activeLabel; pendingTrialText = activeTrialText; pendingSkippedFrames = activeTrialSkippedFrames;
        currentTrialSamples.Clear();
        TrySavePendingTrial();
    }

    void TrySavePendingTrial() {
        if (!HasPendingSave) return;
        try {
            var newLines = new List<string>(pendingSaveSamples.Count);
            for (int i = 0; i < pendingSaveSamples.Count; i++) newLines.Add(pendingSaveSamples[i].ToCsvLine());

            bool exists = File.Exists(pendingCsvPath);
            List<string> outputLines;
            if (exists) {
                if (!TryReadAndValidateCsv(pendingCsvPath, out outputLines, out _, out string err)) {
                    csvValidationError = err; SetNotice(err + " (시도 데이터는 메모리에 유지됨)", MessageType.Error); return;
                }
                outputLines.AddRange(newLines);
            } else {
                outputLines = new List<string>(1 + newLines.Count) { SignSample.Header };
                outputLines.AddRange(newLines);
            }

            WriteCsvAtomically(pendingCsvPath, outputLines, exists);
            string imageNote = SavePendingShots(pendingCsvPath, pendingLabel, pendingTrialText);
            int saved = pendingSaveSamples.Count, skipped = pendingSkippedFrames;
            string savedLabel = pendingLabel, savedTrial = pendingTrialText;
            pendingSaveSamples.Clear();
            RefreshCsvSummary();
            lastTrialFrameCount = saved; lastTrialSkippedFrames = skipped;
            SetNotice($"시도 저장 완료: {savedLabel} #{savedTrial} (유효 {saved}프레임, 건너뜀 {skipped}{imageNote})", MessageType.Info);
        } catch (Exception e) {
            SetNotice($"CSV 저장 실패(메모리 유지됨, 다시 저장 가능): {e.Message}", MessageType.Error);
            UnityEngine.Debug.LogError("[SignRecorder] CSV 저장 실패: " + e);
        }
    }

    void DeleteLastTrial() {
        if (IsSecondaryWindow || isRecording || !hasLastTrialInFile) return;
        if (!ValidateInputs(out string inputErr)) { SetNotice(inputErr, MessageType.Error); return; }
        string csvPath = GetCsvPath();
        if (!File.Exists(csvPath)) { RefreshCsvSummary(); SetNotice("CSV 파일을 찾을 수 없습니다.", MessageType.Warning); return; }
        if (!TryReadAndValidateCsv(csvPath, out List<string> rawLines, out List<SignSample> samples, out string valErr)) {
            csvValidationError = valErr; SetNotice(valErr, MessageType.Error); return;
        }

        TrialKey target = lastTrialInFile;
        var kept = new List<string>(rawLines.Count) { rawLines[0] };
        int removed = 0;
        for (int i = 0; i < samples.Count; i++) {
            SignSample s = samples[i];
            if (s.Person == target.Person && s.Session == target.Session && s.Label == target.Label && s.Trial == target.Trial) removed++;
            else kept.Add(rawLines[i + 1]);
        }

        try {
            WriteCsvAtomically(csvPath, kept, replaceExisting: true);
            int removedImages = DeleteTrialImages(csvPath, target.Label, target.Trial);
            RefreshCsvSummary();
            lastTrialFrameCount = hasLastTrialInFile ? lastTrialInFile.FrameCount : 0; lastTrialSkippedFrames = 0;
            SetNotice($"마지막 시도 삭제 완료: {target.Label} #{target.Trial} ({removed}줄 제거{(removedImages > 0 ? $", 이미지 {removedImages}장 삭제" : "")})", MessageType.Info);
        } catch (Exception e) {
            SetNotice("마지막 시도 삭제 실패: " + e.Message, MessageType.Error);
            UnityEngine.Debug.LogError("[SignRecorder] 마지막 시도 삭제 실패: " + e);
        }
    }

    static bool TryReadAndValidateCsv(string path, out List<string> normalizedLines, out List<SignSample> samples, out string error) {
        normalizedLines = new List<string>(); samples = new List<SignSample>(); error = "";
        string[] lines;
        try { lines = File.ReadAllLines(path, Utf8NoBom); } catch (Exception e) { error = $"기존 CSV 읽기 실패: {e.Message}"; return false; }
        if (lines.Length == 0) { error = "기존 CSV 검증 실패 (1줄): 파일이 비어 있어 헤더가 없습니다."; return false; }

        string header = lines[0].TrimStart('\uFEFF').TrimEnd('\r');
        if (!string.Equals(header, SignSample.Header, StringComparison.Ordinal)) { error = "기존 CSV 검증 실패 (1줄): 헤더가 SignSample.Header와 일치하지 않습니다."; return false; }
        normalizedLines.Add(header);

        for (int i = 1; i < lines.Length; i++) {
            string row = lines[i].TrimEnd('\r');
            if (!SignSample.TryParse(row, out SignSample parsed, out string parseErr)) { error = $"기존 CSV 검증 실패 ({i + 1}줄): {parseErr}"; return false; }
            normalizedLines.Add(row); samples.Add(parsed);
        }
        return true;
    }

    // 세션 CSV 옆의 <세션>_images 폴더. 이미지 파일 이름은 시도번호-순번-손-글자.jpg (SignHandCrop.FileName).
    static string ImageDir(string csvPath) => Path.Combine(Path.GetDirectoryName(csvPath) ?? "", Path.GetFileNameWithoutExtension(csvPath) + "_images");

    // 이미 CSV에 저장된 시도의 이미지를 쓴다. 실패해도 예외를 밖으로 내지 않는다(CSV는 이미 안전).
    string SavePendingShots(string csvPath, string label, string trialText) {
        string note = "";
        try {
            if (pendingShots.Count > 0 && int.TryParse(trialText, NumberStyles.None, CultureInfo.InvariantCulture, out int trial)) {
                DeleteTrialImages(csvPath, label, trialText);   // 지우지 못해 남은 같은 시도 이미지가 섞이지 않게 먼저 정리
                string dir = ImageDir(csvPath);
                Directory.CreateDirectory(dir);
                for (int i = 0; i < pendingShots.Count; i++) File.WriteAllBytes(Path.Combine(dir, SignHandCrop.FileName(trial, pendingShots[i].Index, pendingShots[i].Left, label)), pendingShots[i].Jpg);
                note = $", 손 이미지 {pendingShots.Count}장";
            } else if (saveHandImages) {
                note = ", 손 이미지 0장" + (string.IsNullOrEmpty(lastShotError) ? "" : " (" + lastShotError + ")");
            }
        } catch (Exception e) {
            note = ", 손 이미지 저장 실패: " + e.Message;
            UnityEngine.Debug.LogWarning("[SignRecorder] 손 이미지 쓰기 실패: " + e);
        }
        pendingShots.Clear();
        return note;
    }

    static int DeleteTrialImages(string csvPath, string label, string trialText) {
        int removed = 0;
        try {
            string dir = ImageDir(csvPath);
            if (!Directory.Exists(dir) || !int.TryParse(trialText, NumberStyles.None, CultureInfo.InvariantCulture, out int trial)) return 0;
            foreach (string file in Directory.GetFiles(dir, "*.jpg")) {
                if (SignHandCrop.TryParseFileName(Path.GetFileName(file), out int t, out int _, out bool _, out string l) && t == trial && l == label) { File.Delete(file); removed++; }
            }
        } catch (Exception e) { UnityEngine.Debug.LogWarning("[SignRecorder] 이미지 삭제 실패: " + e.Message); }
        return removed;
    }

    static void WriteCsvAtomically(string csvPath, List<string> lines, bool replaceExisting) {
        string dir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tempPath = Path.Combine(dir ?? "", Path.GetFileName(csvPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try {
            using (var writer = new StreamWriter(tempPath, false, Utf8NoBom)) {
                writer.NewLine = "\n";
                for (int i = 0; i < lines.Count; i++) writer.WriteLine(lines[i]);
            }
            if (replaceExisting) File.Replace(tempPath, csvPath, null);
            else File.Move(tempPath, csvPath);
        } catch {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }

    void RefreshCsvSummary() {
        labelTrialCounts.Clear(); labelMaxTrialNumbers.Clear();
        hasLastTrialInFile = false; lastTrialInFile = default; csvValidationError = ""; lastScannedPath = "";
        if (!ValidateInputs(out _)) return;

        string person = personId.Trim(), session = sessionId.Trim(), csvPath = GetCsvPath();
        lastScannedPath = csvPath;
        if (!File.Exists(csvPath)) { lastTrialFrameCount = 0; return; }
        if (!TryReadAndValidateCsv(csvPath, out _, out List<SignSample> samples, out string err)) { csvValidationError = err; lastTrialFrameCount = 0; return; }

        var uniqueTrials = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var trialFrames = new Dictionary<string, int>(StringComparer.Ordinal);
        bool found = false;
        TrialKey last = default;

        for (int i = 0; i < samples.Count; i++) {
            SignSample s = samples[i];
            if (s.Person != person || s.Session != session) continue;
            if (!uniqueTrials.TryGetValue(s.Label, out HashSet<string> set)) uniqueTrials[s.Label] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(s.Trial);
            if (int.TryParse(s.Trial, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num) &&
                (!labelMaxTrialNumbers.TryGetValue(s.Label, out int curMax) || num > curMax)) labelMaxTrialNumbers[s.Label] = num;
            string key = s.Label + "\0" + s.Trial;
            trialFrames[key] = trialFrames.TryGetValue(key, out int fc) ? fc + 1 : 1;
            found = true;
            last = new TrialKey { Person = s.Person, Session = s.Session, Trial = s.Trial, Label = s.Label, FrameCount = trialFrames[key] };
        }

        foreach (var kv in uniqueTrials) labelTrialCounts[kv.Key] = kv.Value.Count;
        if (found) {
            last.FrameCount = trialFrames[last.Label + "\0" + last.Trial];
            hasLastTrialInFile = true; lastTrialInFile = last; lastTrialFrameCount = last.FrameCount;
        } else lastTrialFrameCount = 0;
    }

    void ParseLabels() {
        cachedLabelInput = labelListInput ?? "";
        parsedLabels.Clear();
        if (!string.IsNullOrWhiteSpace(cachedLabelInput)) {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in cachedLabelInput.Split(',')) {
                string t = raw.Trim();
                if (t.Length == 0 || t.IndexOfAny(ForbiddenLabelChars) >= 0 || HasWhitespace(t)) continue;
                if (seen.Add(t)) parsedLabels.Add(t);
            }
        }
        parsedLabelArray = parsedLabels.ToArray();
        selectedLabelIndex = parsedLabels.Count == 0 ? 0 : Mathf.Clamp(selectedLabelIndex, 0, parsedLabels.Count - 1);
    }

    string GetCurrentLabel() => parsedLabels.Count == 0 ? "" : parsedLabels[Mathf.Clamp(selectedLabelIndex, 0, parsedLabels.Count - 1)];

    int GetNextTrialNumber(string label) {
        if (string.IsNullOrEmpty(label)) return 1;
        int maxNum = labelMaxTrialNumbers.TryGetValue(label, out int m) ? m : 0;
        int count = labelTrialCounts.TryGetValue(label, out int c) ? c : 0;
        return Math.Max(maxNum, count) + 1;
    }

    string UnexpectedLabels() {
        var bad = new List<string>();
        for (int i = 0; i < parsedLabels.Count; i++) if (Array.IndexOf(ConfirmedLabels, parsedLabels[i]) < 0) bad.Add(parsedLabels[i]);
        return string.Join(", ", bad.ToArray());
    }

    string MissingLabels() {
        var missing = new List<string>();
        for (int i = 0; i < ConfirmedLabels.Length; i++) if (!parsedLabels.Contains(ConfirmedLabels[i])) missing.Add(ConfirmedLabels[i]);
        return string.Join(", ", missing.ToArray());
    }

    bool ValidateInputs(out string error) {
        if (!IsValidToken(personId, "사람 ID", out error)) return false;
        if (!IsValidToken(sessionId, "세션 ID", out error)) return false;
        if (parsedLabels.Count == 0) { error = "유효한 글자 목록을 1개 이상 입력하세요. (공백·쉼표·따옴표 불가)"; return false; }
        error = ""; return true;
    }

    static bool IsValidToken(string raw, string fieldName, out string error) {
        string v = raw != null ? raw.Trim() : "";
        if (string.IsNullOrWhiteSpace(v) || HasWhitespace(v) || v == "." || v == ".." || v.Contains("..") ||
            v.IndexOfAny(ForbiddenTokenChars) >= 0 || v.IndexOfAny(InvalidFileNameChars) >= 0) {
            error = $"{fieldName}은(는) 공백·쉼표·따옴표·경로 문자 없이 입력하세요."; return false;
        }
        error = ""; return true;
    }

    static bool HasWhitespace(string s) { for (int i = 0; i < s.Length; i++) if (char.IsWhiteSpace(s[i])) return true; return false; }
    string GetCsvPath() => Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Tools", "SignData", "raw", personId.Trim(), sessionId.Trim() + ".csv");
    void SetNotice(string message, MessageType type) { noticeMessage = message; noticeType = type; }
}
#endif
