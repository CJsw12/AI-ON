using UnitEye;
using UnityEngine;

// "내 사진 사용하기": the player's own face, taken once from the webcam, becomes the head of their character in the
// two-person dialogue. The webcam runs through the same UnitEye face tracker as the gaze test and stops as soon as
// the photo is taken or the screen is closed. The photo lives only in memory for this session: it is never saved
// to disk, put in a save slot or sent anywhere. Without a photo the story keeps the single centred professor.
public partial class GameController
{
    const float PhotoHoldSeconds = 1.2f;    // a face must stay in view this long before the shot is taken
    const float PhotoMinFace = .22f;        // face height as a share of the frame; smaller means too far away
    const float PhotoFaceHeight = 170f;     // canvas px of the face (forehead to chin) on the player's character
    const float PhotoFaceY = 335f;          // its centre, level with the professor's eye line
    const int PhotoPixels = 400;            // height of the stored photo
    const int PhotoMinFrames = 6;           // fresh camera frames with the face needed during the hold
    const float PhotoStallSeconds = .5f;    // no new frame for this long means the camera stopped

    bool usePlayerPhoto, photoCaptureOpen;
    Texture2D playerPhoto, photoDraft;
    Vector3 playerPhotoFace, photoDraftFace;    // face centre x, y and height as shares of the photo (y from the top)
    float photoFaceSince = -1f, photoFreshAt;
    long photoSequence = -1;                // last UnitEye sample counter seen: it advances once per frame with a face
    int photoFrames;
    string photoStatus = "";

    bool PlayerPhotoReady => usePlayerPhoto && playerPhoto != null;

    /// <summary>The switch beside the AI mode switch on the professor screen.</summary>
    void PlayerPhotoToggle(Rect rect)
    {
        if (!NovelUI.Button(rect, PlayerPhotoReady ? "내 사진 사용 중" : "내 사진 사용하기", PlayerPhotoReady, 19)) return;
        novelAudio.Play("Toggle");
        if (PlayerPhotoReady)
        {
            usePlayerPhoto = false;
            DiscardTexture(ref playerPhoto);
            ShowToast("내 사진을 지웠어요 · 교수님과 1:1 화면으로 진행해요");
            return;
        }
        OpenPhotoCapture();
    }

    void OpenPhotoCapture()
    {
        photoCaptureOpen = true;
        DiscardTexture(ref photoDraft);
        ResetPhotoHold();
        photoSequence = -1;
        photoFreshAt = Time.unscaledTime;
        photoStatus = "웹캠을 켜는 중이에요...";
        if (gazeObject == null && !requestingCamera) gazeCameraRequest = StartCoroutine(StartGazeCamera());
    }

    void ClosePhotoCapture(bool use)
    {
        if (use && photoDraft != null)
        {
            DiscardTexture(ref playerPhoto);
            playerPhoto = photoDraft;
            playerPhotoFace = photoDraftFace;
            photoDraft = null;
            usePlayerPhoto = true;
            ShowToast("내 사진으로 대화해요 · 교수님이 오른쪽, 내가 왼쪽에 서요");
        }
        DiscardTexture(ref photoDraft);
        photoCaptureOpen = false;
        if (!gazeTestOpen) StopWebcamTracking();
    }

    static void DiscardTexture(ref Texture2D texture)
    {
        if (texture != null) Destroy(texture);
        texture = null;
    }

    void ResetPhotoHold()
    {
        photoFaceSince = -1f;
        photoFrames = 0;
    }

    // Called every frame from Update: waits for a steady, close enough face on live camera frames, then takes the photo.
    // UnitEye keeps the last face it found when the camera stops, so liveness comes from the webcam itself, and the
    // hold only counts frames that produced a new face sample (UnitEye's counter advances once per such frame).
    void UpdatePhotoCapture()
    {
        if (!photoCaptureOpen || photoDraft != null) return;
        var source = gazeTracker != null && gazeTracker.Provider != null ? gazeTracker.Provider as IGazeRecordingSource : null;
        if (source == null || source.CameraTexture == null) { photoStatus = gazeTracker == null ? gazeStatus : "웹캠을 준비하고 있어요..."; ResetPhotoHold(); return; }
        gazeTracker.Provider.SetRendering(false);   // UnitEye would otherwise draw its own camera view over the game
        bool sample = gazeTracker.GazeSampleSequence != photoSequence;
        if (sample) photoSequence = gazeTracker.GazeSampleSequence;
        bool cameraFrame = source.CameraTexture is WebCamTexture webcam ? webcam.didUpdateThisFrame : sample;
        if (cameraFrame) photoFreshAt = Time.unscaledTime;
        if (Time.unscaledTime - photoFreshAt > PhotoStallSeconds)
        {
            ResetPhotoHold();
            photoStatus = "카메라 영상을 기다리고 있어요...";
            return;
        }
        Rect face = source.FaceBoundsNormalized;
        if (!gazeTracker.IsUserPresent || face.width <= 0f || face.height <= 0f)
        {
            ResetPhotoHold();
            photoStatus = "얼굴이 보이도록 웹캠을 바라봐 주세요.";
            return;
        }
        if (!CameraFlips(source, out bool flipH, out bool flipV))
        {
            ResetPhotoHold();
            photoStatus = "세로로 돌아간 카메라는 지원하지 않아요. 다른 카메라를 써 주세요.";
            return;
        }
        if (face.height < PhotoMinFace)
        {
            ResetPhotoHold();
            photoStatus = "조금만 더 가까이 와 주세요.";
            return;
        }
        if (!sample) return;   // judge (and shoot) only on frames with a new face sample
        if (photoFaceSince < 0f) photoFaceSince = Time.unscaledTime;
        photoFrames++;
        photoStatus = "얼굴 인식됨 · 그대로 있어 주세요";
        if (Time.unscaledTime - photoFaceSince < PhotoHoldSeconds || photoFrames < PhotoMinFrames) return;
        photoDraft = TakePhoto(source.CameraTexture, face, flipH, flipV, out photoDraftFace);
        if (photoDraft == null) { ResetPhotoHold(); return; }
        novelAudio.Play("Bonus");
        photoStatus = "찍었어요! 이 사진으로 할까요?";
    }

    /// <summary>The flips UnitEye really applied when it gave this frame to MediaPipe: its configured ones, plus the
    /// camera's own vertical mirroring, plus a 180° rotation (both axes). Same rule as FaceMeshSolution. False for a
    /// camera turned 90°/270°, which UnitEye itself does not support.</summary>
    static bool CameraFlips(IGazeRecordingSource source, out bool flipH, out bool flipV)
    {
        flipH = source.FrameFlippedHorizontally;
        flipV = source.FrameFlippedVertically;
        if (!(source.CameraTexture is WebCamTexture webcam)) return true;
        flipV ^= webcam.videoVerticallyMirrored;
        int rotation = webcam.videoRotationAngle;
        if (rotation == 180) { flipH = !flipH; flipV = !flipV; }
        return rotation != 90 && rotation != 270;
    }

    /// <summary>Texture coordinates that show the camera frame upright (as MediaPipe saw it) and mirrored.</summary>
    static Rect PreviewTexCoords(bool flipH, bool flipV) => new Rect(flipH ? 0 : 1, flipV ? 0 : 1, flipH ? 1 : -1, flipV ? 1 : -1);

    /// <summary>Crops the head (face box plus room for hair) from the camera frame, mirrors it like a selfie
    /// and fades it out in an oval with a white rim.</summary>
    static Texture2D TakePhoto(Texture camera, Rect face, bool flipH, bool flipV, out Vector3 faceInPhoto)
    {
        faceInPhoto = default;
        if (camera == null || camera.width <= 16 || camera.height <= 16) return null;
        // Normalized, y-down like the face box.
        Rect crop = Rect.MinMaxRect(Mathf.Clamp01(face.xMin - face.width * .32f), Mathf.Clamp01(face.yMin - face.height * .42f),
            Mathf.Clamp01(face.xMax + face.width * .32f), Mathf.Clamp01(face.yMax + face.height * .1f));
        int height = PhotoPixels;
        int width = Mathf.Clamp(Mathf.RoundToInt(height * crop.width * camera.width / (crop.height * camera.height)), 16, 1024);
        // The face box describes the image MediaPipe saw; undo its flips to find the same region in the texture.
        float u = flipH ? 1f - crop.xMax : crop.xMin;
        float v = flipV ? 1f - crop.yMax : crop.yMin;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(camera, target, new Vector2(crop.width, crop.height), new Vector2(u, v));
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var photo = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        photo.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Color32[] pixels = photo.GetPixels32(), shaped = new Color32[pixels.Length];
        Color32 rim = new Color32(255, 255, 255, 255);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            // Upright and mirrored like the preview, whichever way the texture holds the frame.
            int sx = flipH ? x : width - 1 - x, sy = flipV ? y : height - 1 - y;
            Color32 c = pixels[sy * width + sx];
            float dx = (x + .5f) / width * 2f - 1f, dy = (y + .5f) / height * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);            // 1 on the oval's edge
            if (d > .93f) c = Color32.Lerp(c, rim, Mathf.InverseLerp(.93f, .955f, d));
            c.a = (byte)(255 * (1f - Mathf.InverseLerp(.975f, 1f, d)));
            shaped[y * width + x] = c;
        }
        photo.SetPixels32(shaped);
        photo.Apply(false, true);   // no longer readable: drops the CPU copy

        // Where the face sits in the finished (mirrored) photo, y from the top.
        float centreX = (face.center.x - crop.xMin) / crop.width;
        faceInPhoto = new Vector3(1f - centreX, (face.center.y - crop.yMin) / crop.height, face.height / crop.height);
        return photo;
    }

    /// <summary>The player's face on the character, at the professors' face size and eye line.</summary>
    void DrawPlayerPhoto(float centreX, float brightness, float alpha, float bob)
    {
        float height = PhotoFaceHeight / Mathf.Max(.05f, playerPhotoFace.z);
        float width = height * playerPhoto.width / playerPhoto.height;
        Rect rect = new Rect(centreX - playerPhotoFace.x * width, PhotoFaceY + bob - playerPhotoFace.y * height, width, height);
        GUI.color = new Color(brightness, brightness, brightness, alpha);
        GUI.DrawTexture(rect, playerPhoto, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    // Full screen while the webcam looks for the face; drawn instead of the title screens.
    void DrawPhotoCapture()
    {
        Dreamy(.5f);
        Rect card = new Rect(290, 70, 700, 760);
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 36, NovelUI.Pink);
        NovelUI.Label(new Rect(card.x, card.y + 24, card.width, 42), "내 사진으로 대화하기", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(card.x, card.y + 68, card.width, 30), "얼굴을 화면 가운데에 맞추면 자동으로 찍어요", NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);

        Rect view = new Rect(card.x + 70, card.y + 114, 560, 420);
        NovelUI.Fill(view, NovelUI.Alpha(NovelUI.Berry, .92f), 26);
        var source = gazeTracker != null && gazeTracker.Provider != null ? gazeTracker.Provider as IGazeRecordingSource : null;
        Texture camera = source != null ? source.CameraTexture : null;
        if (photoDraft != null)
        {
            NovelUI.Fill(view, NovelUI.Blush, 26);
            float h = view.height - 40, w = h * photoDraft.width / photoDraft.height;
            GUI.DrawTexture(new Rect(view.center.x - w / 2, view.y + 20, w, h), photoDraft, ScaleMode.StretchToFill, true);
        }
        else if (camera != null && camera.width > 16)
        {
            // Fit the frame inside the view, mirrored like a mirror.
            float scale = Mathf.Min(view.width / camera.width, view.height / camera.height);
            Rect frame = new Rect(view.center.x - camera.width * scale / 2, view.center.y - camera.height * scale / 2, camera.width * scale, camera.height * scale);
            // Shows the frame as MediaPipe saw it (upright), mirrored; the face box then needs no flips of its own.
            CameraFlips(source, out bool flipH, out bool flipV);
            GUI.DrawTextureWithTexCoords(frame, camera, PreviewTexCoords(flipH, flipV));
            Rect face = source.FaceBoundsNormalized;
            if (gazeTracker.IsUserPresent && face.width > 0f && Time.unscaledTime - photoFreshAt <= PhotoStallSeconds)
            {
                float x0 = 1f - face.xMax, y0 = face.yMin;
                Rect box = new Rect(frame.x + x0 * frame.width, frame.y + y0 * frame.height, face.width * frame.width, face.height * frame.height);
                float held = photoFaceSince < 0f ? 0f : Mathf.Clamp01((Time.unscaledTime - photoFaceSince) / PhotoHoldSeconds);
                NovelUI.Ring(new Rect(box.x - 10, box.y - 10, box.width + 20, box.height + 20), held > 0f ? NovelUI.Mint : NovelUI.Pink, 30, 4);
                if (held > 0f)
                {
                    Rect bar = new Rect(view.x + 40, view.yMax - 30, view.width - 80, 10);
                    NovelUI.Fill(bar, NovelUI.Alpha(Color.white, .35f), 5);
                    NovelUI.Fill(new Rect(bar.x, bar.y, Mathf.Max(10, bar.width * held), bar.height), NovelUI.Mint, 5);
                }
            }
        }
        else
            NovelUI.Label(view, "카메라 준비 중...", NovelUI.Round, 24, NovelUI.Alpha(Color.white, .85f), TextAnchor.MiddleCenter);

        NovelUI.Label(new Rect(card.x, view.yMax + 16, card.width, 34), photoStatus, NovelUI.Round, 22, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(card.x + 40, view.yMax + 56, card.width - 80, 50), "사진은 이번 플레이 동안 이 PC 메모리에만 있어요.\n파일로 저장하거나 어디로 보내지 않아요.", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);

        if (NovelUI.Button(new Rect(card.x + 30, card.yMax - 74, 120, 50), "취소", false, 20)) { novelAudio.Play("Back"); ClosePhotoCapture(false); return; }
        if (photoDraft == null) return;
        if (NovelUI.Button(new Rect(card.xMax - 400, card.yMax - 74, 160, 50), "다시 찍기", false, 20))
        {
            novelAudio.Play("Toggle");
            DiscardTexture(ref photoDraft);
            ResetPhotoHold();
        }
        if (NovelUI.Button(new Rect(card.xMax - 230, card.yMax - 74, 200, 50), "이 사진 쓰기", true, 20)) { novelAudio.Play("Select"); ClosePhotoCapture(true); }
    }
}
