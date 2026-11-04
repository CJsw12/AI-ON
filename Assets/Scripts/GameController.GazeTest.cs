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
    string gazeStatus = "웹캠 시작을 눌러 주세요.";
    long lastGazeSequence;
    float lastGazeAt;
    bool requestingCamera;
    Coroutine gazeCameraRequest;

    bool GazeCalibrationActive => gazeTestOpen && gazeCalibration != null && gazeCalibration.enabled;

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
        if (gazeCameraRequest != null) StopCoroutine(gazeCameraRequest);
        gazeCameraRequest = null;
        requestingCamera = false;
        if (gazeObject != null) Destroy(gazeObject);
        gazeObject = null;
        gazeTracker = null;
        gazeCalibration = null;
    }

    IEnumerator StartGazeCamera()
    {
        requestingCamera = true;
        gazeStatus = "카메라 권한 확인 중...";
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!gazeTestOpen) { requestingCamera = false; yield break; }
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
        gazeTracker.onlineDriftCorrection = false;
        lastGazeSequence = 0;
        lastGazeAt = -100;
        gazeObject.SetActive(true);
        gazeStatus = "웹캠을 준비하고 있어요...";
        requestingCamera = false;
    }

    void UpdateGazeTest()
    {
        UpdateIPhoneGaze();
        if (gazeUseIphone) return;
        if (!gazeTestOpen || gazeTracker == null) return;
        if (!gazeTracker.enabled) { gazeStatus = "추적 시작에 실패했어요. Unity Console을 확인해 주세요."; return; }
        if (!GazeCalibrationActive && gazeTracker.Provider != null) gazeTracker.Provider.SetRendering(false);
        if (gazeTracker.GazeSampleSequence != lastGazeSequence)
        {
            lastGazeSequence = gazeTracker.GazeSampleSequence;
            lastGazeAt = Time.unscaledTime;
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
        GUI.matrix = Canvas;
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
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
            NovelUI.Label(new Rect(50, 184, 1180, 42), "보정 화면을 클릭해 시작하고 움직이는 점을 바라봐 주세요. 오른쪽 클릭으로 취소할 수 있어요.", NovelUI.Soft, 18, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (gazeTracker == null)
            {
                GUI.enabled = !requestingCamera;
                if (NovelUI.Button(new Rect(440, 245, 400, 62), "웹캠 시작", true, 24)) gazeCameraRequest = StartCoroutine(StartGazeCamera());
                GUI.enabled = true;
            }
            else
            {
                GUI.enabled = gazeTracker.IsUserPresent && Time.unscaledTime - lastGazeAt < 2f;
                if (NovelUI.Button(new Rect(440, 245, 400, 62), "시선 보정 시작", true, 24)) gazeTracker.LoadCalibration(9f, 20f, 1);
                GUI.enabled = true;
                if (gazeTracker.IsUserPresent && lastGazeSequence > 0 && Time.unscaledTime - lastGazeAt < 2f)
                {
                    Vector2 point = Canvas.inverse.MultiplyPoint3x4(gazeTracker.gazeLocation);
                    if (point.x >= 0 && point.x <= 1280 && point.y >= 0 && point.y <= 900)
                        NovelUI.Fill(new Rect(point.x - 14, point.y - 14, 28, 28), NovelUI.DeepPink, 14);
                }
            }
        }
        if (NovelUI.Button(new Rect(40, 820, 180, 52), "돌아가기", false, 21)) CloseGazeTest();
    }
}
