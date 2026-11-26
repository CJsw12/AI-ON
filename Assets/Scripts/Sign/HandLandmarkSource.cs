#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.IO;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Experimental;
using UnityEngine;

public enum HandSourceState { Starting, Running, NoModel, NoCamera, PermissionDenied, Failed, UnsupportedRotation }
public sealed class HandLandmarkSource : MonoBehaviour
{
    HandLandmarker landmarker;
    HandLandmarkerResult result;
    TextureFramePool pool;
    WebCamTexture cameraTexture;
    readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    long lastTimestamp = -1;
    int poolWidth, poolHeight;
    bool warnedRotation, unsupportedRotation;
    public bool IsHandDetected { get; private set; }
    public bool FrameFlipH { get; private set; }
    public bool FrameFlipV { get; private set; } = true;
    public Vector3[] Landmarks { get; } = new Vector3[21];
    public string Handedness { get; private set; } = "";
    public float HandednessScore { get; private set; }
    public int FrameWidth => cameraTexture != null ? cameraTexture.width : 0;
    public int FrameHeight => cameraTexture != null ? cameraTexture.height : 0;
    public double DetectionMilliseconds { get; private set; }
    public string Status { get; private set; } = "준비 중";
    public HandSourceState State { get; private set; } = HandSourceState.Starting;
    public WebCamTexture CameraTexture => cameraTexture;
    public FrameTimingStats DetectionStats { get; } = new FrameTimingStats(120);
    public FrameTimingStats FrameStats { get; } = new FrameTimingStats(120);
    public FrameTimingStats UpdateIntervalStats { get; } = new FrameTimingStats(120);
    public double UpdateRate => UpdateIntervalStats.Average > 0 ? 1000 / UpdateIntervalStats.Average : 0;
    double lastDetectionTime = -1;
    float nextLog;

    IEnumerator Start()
    {
        yield return null;
        string path = Path.Combine(Application.streamingAssetsPath, "hand_landmarker.bytes");
        if (!File.Exists(path)) { State = HandSourceState.NoModel; Status = "손 모델 파일이 없습니다"; yield break; }
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { State = HandSourceState.PermissionDenied; Status = "카메라 권한이 거부되었습니다"; yield break; }
        try
        {
            if (WebCamTexture.devices.Length == 0) { State = HandSourceState.NoCamera; Status = "카메라를 찾을 수 없습니다"; yield break; }
            var options = new HandLandmarkerOptions(new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: File.ReadAllBytes(path)), runningMode: RunningMode.VIDEO, numHands: 1);
            landmarker = HandLandmarker.CreateFromOptions(options);
            result = HandLandmarkerResult.Alloc(1);
            cameraTexture = new WebCamTexture();
            lastDetectionTime = -1;
            cameraTexture.Play();
            clock.Start();
            Status = "카메라 준비 중";
        }
        catch (Exception e) { State = HandSourceState.Failed; Status = "손 추적 시작 실패: " + e.Message; Release(); }
    }
    void Update()
    {
        FrameStats.Add(Time.unscaledDeltaTime * 1000.0);
        if (Time.unscaledTime >= nextLog) { nextLog = Time.unscaledTime + 1; Debug.Log($"[Hand probe] 상태={Status} 손={IsHandDetected} 감지 ms 평균={DetectionStats.Average:F1} 최대={DetectionStats.Maximum:F1} 중앙값={DetectionStats.Median:F1} 95%={DetectionStats.Percentile95:F1} 갱신={UpdateRate:F1}회/초 갱신간격 95%={UpdateIntervalStats.Percentile95:F1}ms 프레임 ms 평균={FrameStats.Average:F1} 최대={FrameStats.Maximum:F1} 중앙값={FrameStats.Median:F1} FPS={(FrameStats.Average > 0 ? 1000 / FrameStats.Average : 0):F1} 횟수={DetectionStats.FrameCount}"); }
        if (landmarker == null || cameraTexture == null || !cameraTexture.didUpdateThisFrame || cameraTexture.width < 32 || cameraTexture.height < 32) return;
        try
        {
            if (pool == null || poolWidth != cameraTexture.width || poolHeight != cameraTexture.height)
            {
                pool?.Dispose(); poolWidth = cameraTexture.width; poolHeight = cameraTexture.height;
                pool = new TextureFramePool(poolWidth, poolHeight, TextureFormat.RGBA32, 10);
            }
            if (!pool.TryGetTextureFrame(out var frame)) return;
            UpdateFrameOrientation();
            frame.ReadTextureOnCPU(cameraTexture, FrameFlipH, FrameFlipV);
            var image = frame.BuildCPUImage(); frame.Release();
            long timestamp = Math.Max(clock.ElapsedMilliseconds, lastTimestamp + 1); lastTimestamp = timestamp;
            bool found;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try { found = landmarker.TryDetectForVideo(image, timestamp, null, ref result); }
            finally { timer.Stop(); image.Dispose(); }
            double detectionTime = Time.realtimeSinceStartupAsDouble;
            if (lastDetectionTime >= 0) UpdateIntervalStats.Add((detectionTime - lastDetectionTime) * 1000);
            lastDetectionTime = detectionTime;
            DetectionMilliseconds = timer.Elapsed.TotalMilliseconds; DetectionStats.Add(DetectionMilliseconds);
            IsHandDetected = found && result.handLandmarks != null && result.handLandmarks.Count > 0 && result.handLandmarks[0].landmarks.Count == 21;
            if (IsHandDetected)
            {
                var points = result.handLandmarks[0].landmarks;
                for (int i = 0; i < 21; i++) Landmarks[i] = new Vector3(points[i].x, points[i].y, points[i].z);
                Handedness = "알 수 없음"; HandednessScore = 0;
                if (result.handedness != null && result.handedness.Count > 0 && result.handedness[0].categories != null && result.handedness[0].categories.Count > 0)
                { var c = result.handedness[0].categories[0]; Handedness = c.categoryName; HandednessScore = c.score; }
            }
            Status = unsupportedRotation ? "카메라 90/270도 회전은 지원하지 않습니다" : IsHandDetected ? "손 감지 중" : "손이 보이지 않습니다";
            State = unsupportedRotation ? HandSourceState.UnsupportedRotation : HandSourceState.Running;
        }
        catch (Exception e) { State = HandSourceState.Failed; Status = "손 감지 실패: " + e.Message; Release(); }
    }
    void UpdateFrameOrientation()
    {
        bool flipH = false;
        bool flipV = true ^ cameraTexture.videoVerticallyMirrored;
        int rotation = cameraTexture.videoRotationAngle;
        if (rotation == 180) { flipH = !flipH; flipV = !flipV; }
        unsupportedRotation = rotation == 90 || rotation == 270;
        if (unsupportedRotation && !warnedRotation)
        {
            warnedRotation = true;
            Debug.LogWarning($"[Hand probe] 카메라 회전 {rotation}도는 지원하지 않아 영상과 관절 위치가 올바르지 않을 수 있습니다.");
        }
        FrameFlipH = flipH;
        FrameFlipV = flipV;
    }
    public void Release()
    {
        lastDetectionTime = -1;
        IsHandDetected = false;
        if (cameraTexture != null) { cameraTexture.Stop(); Destroy(cameraTexture); cameraTexture = null; }
        pool?.Dispose(); pool = null;
        landmarker?.Close(); landmarker = null;
    }
    void OnDisable() { Release(); }
    void OnDestroy() { Release(); }
}

#endif
