#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.IO;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Experimental;
using UnityEngine;

public enum StretchSourceState { Starting, Running, NoModel, NoCamera, PermissionDenied, Failed, UnsupportedRotation }
public sealed class StretchPoseSource : MonoBehaviour
{
    public int DeviceIndex;
    PoseLandmarker landmarker;
    PoseLandmarkerResult result;
    TextureFramePool pool;
    WebCamTexture cameraTexture;
    readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    long lastTimestamp = -1;
    int poolWidth, poolHeight;
    bool warnedRotation, unsupportedRotation;
    public bool IsPoseDetected { get; private set; }
    public bool FrameFlipH { get; private set; }
    public bool FrameFlipV { get; private set; } = true;
    public Vector3[] Landmarks { get; } = new Vector3[33];
    public string Handedness { get; private set; } = "";
    public float HandednessScore { get; private set; }
    public int FrameWidth => cameraTexture != null ? cameraTexture.width : 0;
    public int FrameHeight => cameraTexture != null ? cameraTexture.height : 0;
    public double DetectionMilliseconds { get; private set; }
    public string Status { get; private set; } = "준비 중";
    public StretchSourceState State { get; private set; } = StretchSourceState.Starting;
    public WebCamTexture CameraTexture => cameraTexture;
    public FrameTimingStats DetectionStats { get; } = new FrameTimingStats(120);
    public FrameTimingStats FrameStats { get; } = new FrameTimingStats(120);
    float nextLog;
    public float[] Visibility { get; } = new float[33];
    public float LastFrameAt { get; private set; }
    float nextDetection;

    IEnumerator Start()
    {
        yield return null;
        string path = Path.Combine(Application.streamingAssetsPath, "pose_landmarker_full.bytes");
        if (!File.Exists(path)) { State = StretchSourceState.NoModel; Status = "자세 모델 파일이 없습니다"; yield break; }
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { State = StretchSourceState.PermissionDenied; Status = "카메라 권한이 거부되었습니다"; yield break; }
        try
        {
            if (WebCamTexture.devices.Length == 0) { State = StretchSourceState.NoCamera; Status = "카메라를 찾을 수 없습니다"; yield break; }
            var options = new PoseLandmarkerOptions(new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: File.ReadAllBytes(path)), runningMode: RunningMode.VIDEO, numPoses: 1, minPoseDetectionConfidence: .4f, minPosePresenceConfidence: .4f, minTrackingConfidence: .4f);
            landmarker = PoseLandmarker.CreateFromOptions(options);
            result = PoseLandmarkerResult.Alloc(1);
            cameraTexture = new WebCamTexture(WebCamTexture.devices[Mathf.Clamp(DeviceIndex, 0, WebCamTexture.devices.Length-1)].name, 640, 480, 30);
            cameraTexture.Play();
            clock.Start();
            Status = "카메라 준비 중";
        }
        catch (Exception e) { State = StretchSourceState.Failed; Status = "자세 추적 시작 실패: " + e.Message; Release(); }
    }
    void Update()
    {
        FrameStats.Add(Time.unscaledDeltaTime * 1000.0);
        if (Time.unscaledTime < nextDetection) return;
        nextDetection = Time.unscaledTime + .066f;
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
            DetectionMilliseconds = timer.Elapsed.TotalMilliseconds; DetectionStats.Add(DetectionMilliseconds);
            IsPoseDetected = found && result.poseLandmarks != null && result.poseLandmarks.Count > 0 && result.poseLandmarks[0].landmarks.Count == 33;
            if (IsPoseDetected)
            {
                var points = result.poseLandmarks[0].landmarks;
                for (int i = 0; i < 33; i++) Landmarks[i] = new Vector3(points[i].x, points[i].y, points[i].z);
                for (int i = 0; i < 33; i++) Visibility[i] = points[i].visibility ?? 0f;
                LastFrameAt = Time.unscaledTime;
            }
            Status = unsupportedRotation ? "카메라 90/270도 회전은 지원하지 않습니다" : IsPoseDetected ? "자세 감지 중" : "몸이 보이지 않습니다";
            State = unsupportedRotation ? StretchSourceState.UnsupportedRotation : StretchSourceState.Running;
        }
        catch (Exception e) { State = StretchSourceState.Failed; Status = "자세 감지 실패: " + e.Message; Release(); }
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
        IsPoseDetected = false;
        if (cameraTexture != null) { cameraTexture.Stop(); Destroy(cameraTexture); cameraTexture = null; }
        pool?.Dispose(); pool = null;
        landmarker?.Close(); landmarker = null;
    }
    void OnDisable() { Release(); }
    void OnDestroy() { Release(); }
}

#endif
