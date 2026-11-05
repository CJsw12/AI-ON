using System.Collections;
using System.Reflection;
using UnitEye;
using UnityEngine;

public partial class GameController
{
    bool gazeTestOpen;
    bool gazeUseIphone;
    GameObject gazeObject;
    HomulerGaze gazeTracker;
    HomulerGazeCalibration gazeCalibration;
    const string WebcamModelPref = "GazeTest.WebcamModel";   // 0 = EyeMU (fast), 1 = EyeMU + ResNet-34 (accurate)
    int webcamSamplesThisSecond, webcamRate;
    float webcamRateSince;
    string gazeStatus = "웹캠 시작을 눌러 주세요.";
    long lastGazeSequence;
    float lastGazeAt;
    bool requestingCamera;
    Coroutine gazeCameraRequest;

    bool GazeCalibrationActive => gazeTestOpen && gazeCalibration != null && gazeCalibration.enabled;

    // EyeMU reads only the two eye crops, so looking down (eyelids cover the iris) and to the far sides is weak.
    // The ensemble adds ResNet-34's whole-face gaze direction, which keeps working there, at about twice the cost.
    static GazeBackbone WebcamModel => PlayerPrefs.GetInt(WebcamModelPref, 1) == 1 ? GazeBackbone.EyeMUPlusResNet34 : GazeBackbone.EyeMU;

    void OpenGazeTest()
    {
        gazeTestOpen = true;
        gazeUseIphone = false;
        gazeStatus = "웹캠 시작을 눌러 주세요.";
    }

    void CloseGazeTest()
    {
        gazeTestOpen = false;
        StopWebcamTracking();
        StopIPhoneGaze();
    }

    void StopWebcamTracking()
    {
        if (!gazeUseIphone) StopStarGame();
        if (gazeCameraRequest != null) StopCoroutine(gazeCameraRequest);
        gazeCameraRequest = null;
        requestingCamera = false;
        if (gazeObject != null) Destroy(gazeObject);
        gazeObject = null;
        gazeTracker = null;
        gazeCalibration = null;
        CancelWebcamFit();
    }

    IEnumerator StartGazeCamera()
    {
        requestingCamera = true;
        gazeStatus = "카메라 권한 확인 중...";
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!gazeTestOpen && !photoCaptureOpen) { requestingCamera = false; yield break; }
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            gazeStatus = "카메라 권한이 없어요. Windows 카메라 권한을 확인해 주세요.";
            requestingCamera = false;
            yield break;
        }
        if (WebCamTexture.devices.Length == 0)
        {
            gazeStatus = "웹캠을 찾지 못했어요. 카메라 연결을 확인해 주세요.";
            requestingCamera = false;
            yield break;
        }
        GameObject prefab = Resources.Load<GameObject>("GazeTest/UnitEye");
        if (prefab == null)
        {
            gazeStatus = "시선 추적 리소스를 찾지 못했어요.";
            requestingCamera = false;
            yield break;
        }
        gazeObject = Instantiate(prefab);
        gazeObject.SetActive(false);
        gazeObject.transform.SetParent(transform, false);
        gazeTracker = gazeObject.GetComponent<HomulerGaze>();
        gazeCalibration = gazeObject.AddComponent<HomulerGazeCalibration>();
        gazeCalibration.enabled = false;
        var field = typeof(HomulerGaze).GetField("_calibrationScript", BindingFlags.NonPublic | BindingFlags.Instance);
        if (gazeTracker == null || field == null)
        {
            Destroy(gazeObject);
            gazeObject = null;
            gazeTracker = null;
            gazeCalibration = null;
            gazeStatus = "시선 추적 초기화에 실패했어요.";
            requestingCamera = false;
            yield break;
        }
        field.SetValue(gazeTracker, gazeCalibration);
        if (gazeTracker.CSVLogger != null) gazeTracker.CSVLogger.enabled = false;
        gazeTracker.drawDot = false;
        gazeTracker.showEyes = false;
        gazeTracker.showGazeUI = false;
        gazeTracker.showFaceMesh = false;
        // Drift correction learns from things the player certainly looked at (the star game reports each star).
        // Click anchors stay off: menu clicks here are often made without looking at the button.
        gazeTracker.onlineDriftCorrection = true;
        gazeTracker.clickAnchors = false;
        // A long half-closure while looking down is not a blink; the fit layer holds the pointer for real blinks.
        gazeTracker.maxBlinkHoldSeconds = BlinkMaxSeconds;
        lastGazeSequence = 0;
        lastGazeAt = -100;
        gazeObject.SetActive(true);
        gazeStatus = "웹캠을 준비하고 있어요...";
        requestingCamera = false;
        webcamRate = webcamSamplesThisSecond = 0;
        webcamRateSince = Time.unscaledTime;
        yield return null;   // HomulerGaze.Start builds the provider first
        // Taking the player's photo needs only the face mesh, so it runs the lightest gaze model.
        GazeBackbone backbone = gazeTestOpen ? WebcamModel : GazeBackbone.EyeMU;
        if (gazeTracker != null && gazeTracker.GazeBackbone != backbone) gazeTracker.SetBackbone(backbone);
    }

    void SetWebcamModel(GazeBackbone backbone)
    {
        PlayerPrefs.SetInt(WebcamModelPref, backbone == GazeBackbone.EyeMU ? 0 : 1);
        PlayerPrefs.Save();
        if (gazeTracker != null && !GazeCalibrationActive) { CancelWebcamFit(); gazeTracker.SetBackbone(backbone); }
    }

    void UpdateGazeTest()
    {
        UpdateIPhoneGaze();
        UpdateWebcamGaze();
        TickStarGame();
        TraceIPhoneGaze();
    }

    void UpdateWebcamGaze()
    {
        if (gazeUseIphone) return;
        if (!gazeTestOpen || gazeTracker == null) return;
        if (!gazeTracker.enabled) { gazeStatus = "추적 시작에 실패했어요. Unity Console을 확인해 주세요."; return; }
        if (!GazeCalibrationActive && gazeTracker.Provider != null) gazeTracker.Provider.SetRendering(false);
        if (gazeTracker.GazeSampleSequence != lastGazeSequence)
        {
            webcamSamplesThisSecond += (int)(gazeTracker.GazeSampleSequence - lastGazeSequence);
            lastGazeSequence = gazeTracker.GazeSampleSequence;
            lastGazeAt = Time.unscaledTime;
            if (!GazeCalibrationActive && gazeTracker.Provider != null) OnWebcamSample();
        }
        TickWebcamFit();
        webcamRawShown = webcamRawHold.Filter(WebcamRawCanvasGaze, Time.unscaledTime);
        if (Time.unscaledTime - webcamRateSince >= 1f)
        {
            webcamRate = webcamSamplesThisSecond;
            webcamSamplesThisSecond = 0;
            webcamRateSince = Time.unscaledTime;
        }
        gazeStatus = !gazeTracker.IsUserPresent ? "얼굴이 보이도록 웹캠을 바라봐 주세요. 카메라 연결도 확인해 주세요." :
            Time.unscaledTime - lastGazeAt > 2f ? "시선 데이터를 기다리고 있어요." : "얼굴 인식 중 · 보정 후 시선 위치를 확인하세요.";
    }

    void DrawGazeTest()
    {
        if (GazeCalibrationActive)
        {
            GUI.matrix = Matrix4x4.identity;
            return;
        }
        if (gazeUseIphone && iphoneCalibrating)
        {
            DrawIPhoneCalibrationScreen();
            return;
        }
        if (gazeUseIphone && iphoneVerticalDiagnostic)
        {
            DrawIPhoneVerticalDiagnostic();
            return;
        }
        if (!gazeUseIphone && webcamFitting)
        {
            DrawWebcamFitScreen();   // sets its own matrix
            return;
        }
        GUI.matrix = Canvas;
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
        if (StarScreenOpen)
        {
            DrawStarGame();   // has its own buttons
            return;
        }
        NovelUI.Label(new Rect(0, 18, 1280, 55), "시선 추적 테스트", NovelUI.Round, 32, NovelUI.Berry, TextAnchor.MiddleCenter);
        bool chooseIphone = gazeUseIphone;
        if (NovelUI.Button(new Rect(390, 91, 230, 48), "노트북 웹캠", !gazeUseIphone, 19)) chooseIphone = false;
        if (NovelUI.Button(new Rect(660, 91, 230, 48), "iPhone 추적", gazeUseIphone, 19)) chooseIphone = true;
        if (chooseIphone != gazeUseIphone)
        {
            if (chooseIphone) StopWebcamTracking();
            else StopIPhoneGaze();
            gazeUseIphone = chooseIphone;
        }
        if (gazeUseIphone)
        {
            DrawIPhoneGazePanel();
        }
        else
        {
            NovelUI.Label(new Rect(50, 146, 1180, 38), gazeStatus, NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(50, 184, 1180, 42), "1번 보정은 화면을 클릭해 시작하고 움직이는 점을 따라 보세요(오른쪽 클릭 취소). 그다음 2번으로 화면 끝을 맞춰요.", NovelUI.Soft, 17, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (gazeTracker == null)
            {
                GUI.enabled = !requestingCamera;
                if (NovelUI.Button(new Rect(440, 245, 400, 62), "웹캠 시작", true, 24)) gazeCameraRequest = StartCoroutine(StartGazeCamera());
                GUI.enabled = true;
            }
            else
            {
                bool tracking = gazeTracker.IsUserPresent && Time.unscaledTime - lastGazeAt < 2f;
                GUI.enabled = tracking;
                // Two rounds of every calibration pattern (corners, interior, head turns, sweeps).
                if (NovelUI.Button(new Rect(280, 238, 350, 58), "1. 시선 보정", !webcamFitted, 22))
                {
                    CancelWebcamFit();   // a new base calibration changes what the screen fit was measured on
                    gazeTracker.LoadCalibration(9f, 20f, 2);
                }
                if (NovelUI.Button(new Rect(650, 238, 350, 58), webcamFitted ? "2. 화면 맞춤 다시" : "2. 화면 맞춤 (16점)", false, 22)) BeginWebcamFit();
                NovelUI.Label(new Rect(140, 304, 1000, 54), webcamFitted || webcamFitStatus.Length > 0 ? webcamFitStatus
                    : "1번 보정 뒤 2번을 하면, 화면 옆·아래 끝까지 시선이 닿도록 다시 맞춰요.", NovelUI.Soft, 16,
                    webcamFitted ? NovelUI.Mint : NovelUI.Plum, TextAnchor.MiddleCenter, webcamFitted ? FontStyle.Bold : FontStyle.Normal);
                if (NovelUI.Button(new Rect(440, 380, 400, 58), "시선 별잡기", true, 23)) StartStarGame();
                GUI.enabled = true;

                bool accurate = gazeTracker.GazeBackbone == GazeBackbone.EyeMUPlusResNet34;
                NovelUI.Label(new Rect(190, 492, 900, 30), "추적 모델", NovelUI.Soft, 18, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (NovelUI.Button(new Rect(390, 528, 240, 46), "기본 (빠름)", !accurate, 18) && accurate) SetWebcamModel(GazeBackbone.EyeMU);
                if (NovelUI.Button(new Rect(650, 528, 240, 46), "정밀 (눈+얼굴 방향)", accurate, 18) && !accurate) SetWebcamModel(GazeBackbone.EyeMUPlusResNet34);
                string speed = webcamRate > 0 ? $"초당 {webcamRate}번 측정 · 지연 {Mathf.RoundToInt(gazeTracker.MeasuredLatencySeconds * 1000f)}ms" : "측정 속도 확인 중";
                string advice = webcamRate > 0 && webcamRate < 12 && accurate ? " · 끊기면 기본 모델을 써 보세요" : "";
                NovelUI.Label(new Rect(140, 582, 1000, 28), $"{speed}{advice} · 모델을 바꾸면 다시 보정해야 해요.", NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleCenter);

                if (tracking && lastGazeSequence > 0 && (!webcamFitted || webcamMarkerValid))
                {
                    Vector2 point = WebcamCanvasGaze;
                    if (point.x >= 0 && point.x <= 1280 && point.y >= 0 && point.y <= 900)
                        NovelUI.Fill(new Rect(point.x - 14, point.y - 14, 28, 28), NovelUI.DeepPink, 14);
                }
            }
        }
        if (NovelUI.Button(new Rect(40, 820, 180, 52), "돌아가기", false, 21)) CloseGazeTest();
    }
}
