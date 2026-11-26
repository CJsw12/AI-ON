// Excluded from WebGL player builds: depends on the native MediaPipe plugin.
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;

namespace Mediapipe.Unity
{
    /// <summary>
    /// Minimal webcam source for the MediaPipe 0.16.3 Task-API path. Replaces the old Solution-era
    /// ImageSource/WebCamSource stack (which depended on the removed Solution API): it just opens a
    /// WebCamTexture, exposes the current frame + device selection, and lets FaceMeshSolution read a frame
    /// each Update to feed the FaceLandmarker. Kept in the Mediapipe.Unity namespace + same file (GUID) so
    /// the prefab's WebCamSource component and the UnitEye consumers keep working.
    /// </summary>
    // Runs before FaceMeshSolution and the gaze provider so a new MJPG frame is on the texture when they look.
    [DefaultExecutionOrder(-200)]
    public class WebCamSource : MonoBehaviour
    {
        public readonly struct Resolution
        {
            public readonly int width;
            public readonly int height;
            public Resolution(int width, int height) { this.width = width; this.height = height; }
        }

        [Tooltip("The available webcam whose width is closest to this value is chosen by default. Lower = faster.")]
        [SerializeField] private int _preferableDefaultWidth = 1280;
        [Tooltip("Preferred webcam device name; empty picks the closest to Preferable Default Width.")]
        [SerializeField] private string _name = "";
        //At ~60cm, 1° of gaze rotation moves the iris ~0.3px at 720p-class capture — the per-frame gaze
        //signal is SUB-PIXEL and landmark jitter is the pipeline's binding accuracy constraint. Higher
        //frame rates also double the samples per fixation (~sqrt(2) precision after aggregation) and
        //shorten exposure (less motion blur on the iris). The driver delivers the closest supported rate.
        [Tooltip("Requested camera frame rate. 60fps = 2x samples per fixation and shorter exposure (sharper iris); the driver falls back to the closest supported rate.")]
        [SerializeField] private int _requestedFps = 60;

        [Tooltip("Windows: read the camera in its compressed MJPG mode through Media Foundation. WebCamTexture often gets the uncompressed mode, which USB bandwidth caps at ~10 fps (720p) / ~5 fps (1080p); MJPG runs at 30. Falls back to WebCamTexture when it cannot.")]
        [SerializeField] private bool _preferMjpg = true;

        private WebCamTexture _webCamTexture;
        private int _deviceIndex = -1;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private MfCameraCapture _mjpg;
        private Texture2D _mjpgTexture;
        private byte[] _mjpgFrame;
        private long _mjpgUploaded;
        private int _mjpgUpdatedFrame = -1;
        private float _mjpgOpenedAt;
        private bool Mjpg => _mjpg != null;
#else
        private bool Mjpg => false;
#endif

        /// <summary>True once the webcam has delivered a real frame.</summary>
        public bool isPrepared => Mjpg ? MjpgTexture != null && _mjpgUploadedAny : _webCamTexture != null && _webCamTexture.width > 16;
        /// <summary>True only on frames where the camera delivered a NEW image. Lets consumers skip
        /// reprocessing the same frame when the display refresh outruns the camera rate.</summary>
        public bool didUpdateThisFrame => Mjpg ? MjpgUpdatedThisFrame : _webCamTexture != null && _webCamTexture.didUpdateThisFrame;
        public string sourceName => Mjpg ? MjpgName : _webCamTexture != null ? _webCamTexture.deviceName : "";
        /// <summary>"MJPG 1280x720" or "WebCamTexture 1280x720": which path delivers the frames (diagnostics).</summary>
        public string captureMode => Mjpg ? MjpgMode : _webCamTexture != null ? $"WebCamTexture {_webCamTexture.width}x{_webCamTexture.height}" : "";
        // The MJPG frames are laid out bottom row first like a WebCamTexture, so the same flips apply.
        public bool isVerticallyFlipped => !Mjpg && _webCamTexture != null && _webCamTexture.videoVerticallyMirrored;
        public bool isFrontFacing
        {
            get
            {
                var devices = WebCamTexture.devices;
                return _deviceIndex >= 0 && _deviceIndex < devices.Length && devices[_deviceIndex].isFrontFacing;
            }
        }
        public int rotation => !Mjpg && _webCamTexture != null ? _webCamTexture.videoRotationAngle : 0;
        public int textureWidth => Mjpg ? MjpgWidth : _webCamTexture != null ? _webCamTexture.width : 0;
        public int textureHeight => Mjpg ? MjpgHeight : _webCamTexture != null ? _webCamTexture.height : 0;
        public Resolution resolution => new Resolution(textureWidth, textureHeight);

        /// <summary>
        /// The frame rate this source asked the device for. Exposed so consumers can compare it with
        /// the rate actually delivered: gaze updates are gated on new camera frames, so a camera
        /// running well below its requested rate silently caps the entire pipeline.
        /// </summary>
        public int requestedFps => _requestedFps;

        public Texture GetCurrentTexture() => Mjpg ? (Texture)MjpgTexture : _webCamTexture;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private bool _mjpgUploadedAny => _mjpgUploaded > 0;
        private Texture2D MjpgTexture => _mjpgTexture;
        private bool MjpgUpdatedThisFrame => _mjpgUpdatedFrame == Time.frameCount;
        private string MjpgName => _mjpg.DeviceName;
        private string MjpgMode => $"MJPG {_mjpg.Width}x{_mjpg.Height}";
        private int MjpgWidth => _mjpg.Width;
        private int MjpgHeight => _mjpg.Height;

        // Uploads the newest decoded frame, at most once per Unity frame.
        private void Update()
        {
            if (_mjpg == null) return;
            long frames = _mjpg.FrameCount;
            if (frames != _mjpgUploaded && _mjpg.CopyLatest(_mjpgFrame))
            {
                _mjpgUploaded = frames;
                _mjpgTexture.LoadRawTextureData(_mjpgFrame);
                _mjpgTexture.Apply(false);
                _mjpgUpdatedFrame = Time.frameCount;
                return;
            }
            // Opened but no frames (e.g. another app holds the camera): use WebCamTexture instead.
            if (_mjpgUploaded == 0 && (_mjpg.Error != null || Time.unscaledTime - _mjpgOpenedAt > 4f))
            {
                Debug.LogWarning($"WebCamSource: MJPG capture delivered no frames ({_mjpg.Error ?? "timeout"}); using WebCamTexture.");
                StopMjpg();
                OpenWebCamTexture(_deviceIndex);
            }
        }

        private bool TryOpenMjpg(string deviceName)
        {
            if (!_preferMjpg) return false;
            int height = Mathf.RoundToInt(_preferableDefaultWidth * 9f / 16f);
            var capture = MfCameraCapture.Open(deviceName, _preferableDefaultWidth, height, 30, 5000, out string error);
            if (capture == null) { Debug.LogWarning($"WebCamSource: MJPG capture unavailable ({error}); using WebCamTexture."); return false; }
            _mjpg = capture;
            _mjpgFrame = new byte[capture.Width * capture.Height * 4];
            _mjpgTexture = new Texture2D(capture.Width, capture.Height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            _mjpgUploaded = 0;
            _mjpgOpenedAt = Time.unscaledTime;
            Debug.Log($"WebCamSource: '{capture.DeviceName}' MJPG {capture.Width}x{capture.Height} @ {capture.NativeFps:F0} fps via Media Foundation.");
            return true;
        }

        private void StopMjpg()
        {
            _mjpg?.Dispose();
            _mjpg = null;
            if (_mjpgTexture != null) Destroy(_mjpgTexture);
            _mjpgTexture = null;
        }
#else
        private bool _mjpgUploadedAny => false;
        private Texture2D MjpgTexture => null;
        private bool MjpgUpdatedThisFrame => false;
        private string MjpgName => "";
        private string MjpgMode => "";
        private int MjpgWidth => 0;
        private int MjpgHeight => 0;
        private bool TryOpenMjpg(string deviceName) => false;
        private void StopMjpg() { }
#endif

        // On mobile, the camera permission must be granted BEFORE WebCamTexture.Play(); otherwise the
        // texture silently stays at the 16x16 placeholder and the pipeline idles with no error (the old
        // Solution-era WebCamSource had this flow; the migration dropped it). Desktop needs no prompt.
        private System.Collections.IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                bool answered = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => answered = true;
                callbacks.PermissionDenied += _ => answered = true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
                yield return new WaitUntil(() => answered);
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Debug.LogWarning("WebCamSource: camera permission denied; no webcam input available.");
                    yield break;
                }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogWarning("WebCamSource: camera permission denied; no webcam input available.");
                yield break;
            }
#endif
            SelectSource(-1);
            yield break;
        }

        private void OnDestroy() => StopCamera();

        private void StopCamera()
        {
            StopMjpg();
            if (_webCamTexture == null) return;
            _webCamTexture.Stop();
            Destroy(_webCamTexture);
            _webCamTexture = null;
        }

        /// <summary>Index of the current device in WebCamTexture.devices, or -1 if none.</summary>
        public int GetCameraIndex() => _deviceIndex;

        /// <summary>
        /// Opens the webcam device at the given index (wrapped into range). index &lt; 0 picks the device
        /// whose reported name matches _name, else the one closest to _preferableDefaultWidth (or the first).
        /// </summary>
        public void SelectSource(int index)
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("WebCamSource: no webcam devices found.");
                return;
            }

            if (index < 0)
            {
                index = 0;
                if (!string.IsNullOrEmpty(_name))
                {
                    for (int i = 0; i < devices.Length; i++)
                        if (devices[i].name == _name) { index = i; break; }
                }
            }
            index = ((index % devices.Length) + devices.Length) % devices.Length;

            StopCamera();
            _deviceIndex = index;
            _name = devices[index].name;
            if (TryOpenMjpg(_name)) return;
            OpenWebCamTexture(index);
        }

        private void OpenWebCamTexture(int index)
        {
            var devices = WebCamTexture.devices;
            if (index < 0 || index >= devices.Length) return;
            // Request the preferred width + frame rate; the device delivers the closest it supports.
            _webCamTexture = new WebCamTexture(devices[index].name, _preferableDefaultWidth,
                Mathf.RoundToInt(_preferableDefaultWidth * 9f / 16f), Mathf.Max(1, _requestedFps));
            _webCamTexture.Play();
        }
    }
}
#endif
