using System.Collections.Generic;
using UnityEngine;

// Learning from clicks (WebGazer / Tri-Cam): while gaze is live, every mouse click the player makes is a
// moment they were almost certainly looking at the click point. Clicks that pass the checks below are handed
// to the mapper as lightly weighted calibration points. Nothing here moves the gaze pointer toward the cursor.
public partial class GameController
{
    const float ClickLookFrom = .45f, ClickLookTo = .05f;   // eyes land on a target just before the click
    const int ClickMinSamples = 3;
    const float ClickMaxScatter = 60f;                       // canvas px: gaze must be resting, not sweeping

    readonly List<(float time, double[] features, Vector2 point)> clickGazeHistory = new List<(float, double[], Vector2)>();
    int clickCalibrationAccepted;
    // Tri-Cam's "deliberate click" test: the cursor must have travelled to the click point just before.
    // Repeated clicks with an idle cursor (advancing dialogue while reading) say nothing about where the eyes are.
    const float ClickAimSeconds = 1.5f, ClickAimMinTravel = 40f;
    readonly List<(float time, Vector2 mouse)> clickMouseHistory = new List<(float, Vector2)>();

    // Pending press: the button fires on release (in OnGUI, after this Update), so the press waits for it.
    bool clickPending;
    float clickPendingAt;
    int clickPendingFrame;
    Vector2 clickPendingMouse;   // GUI space, y down
    const float ClickReleaseTimeout = 1.5f;

    // Called with every fitted gaze sample (unsmoothed prediction, canvas px). Stamped with the camera capture
    // time, so the look window before a click is measured in the time the eyes actually moved.
    void RecordClickGaze(double[] features, Vector2 point)
    {
        float now = Time.unscaledTime;
        if (!Finite(point)) return;
        foreach (double f in features) if (double.IsNaN(f) || double.IsInfinity(f)) return;   // never let NaN reach the refit
        float seen = gazeUseIphone ? now : WebcamFrameTime();
        clickGazeHistory.Add((seen, features, point));
        while (clickGazeHistory.Count > 0 && clickGazeHistory[0].time < now - ClickLookFrom - ClickReleaseTimeout - 1f) clickGazeHistory.RemoveAt(0);
    }

    void TickClickCalibration()
    {
        float now = Time.unscaledTime;
        Vector2 mouseNow = Input.mousePosition;
        clickMouseHistory.Add((now, mouseNow));
        while (clickMouseHistory.Count > 0 && clickMouseHistory[0].time < now - ClickAimSeconds) clickMouseHistory.RemoveAt(0);
        // Resolve the previous press first, so a fast second press cannot overwrite a click that already fired.
        if (clickPending && NovelUI.LastPressedFrame >= clickPendingFrame) { clickPending = false; LearnPendingClick(); }
        else if (clickPending && now - clickPendingAt > ClickReleaseTimeout) clickPending = false;
        if (Input.GetMouseButtonDown(0))
        {
            float travel = 0;
            foreach (var m in clickMouseHistory) travel = Mathf.Max(travel, Vector2.Distance(m.mouse, mouseNow));
            clickPending = travel >= ClickAimMinTravel;
            clickPendingAt = now; clickPendingFrame = Time.frameCount;
            clickPendingMouse = new Vector2(mouseNow.x, Screen.height - mouseNow.y);
        }
    }

    static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

    // Learn only from a press that a real control (button, choice) accepted: the eyes aim at it, and
    // clicks on empty space or on clipped-away parts say nothing about gaze.
    void LearnPendingClick()
    {
        Rect control = NovelUI.LastPressed;
        if (control.width > 700 * Screen.width / 1280f || control.height > 160 * Screen.height / 900f) return;   // too big to know where the eyes were
        if (!gazeTestOpen && !eyeContactSessionActive) return;
        if (GazeCalibrationActive || webcamFitting || iphoneCalibrating) return;
        GazeMapper mapper = gazeUseIphone ? iphoneMapper : webcamMapper;
        if (!(gazeUseIphone ? iphoneCalibrated : webcamFitted) || !mapper.Ready) return;

        float at = clickPendingAt;
        var window = new List<double[]>();
        Vector2 centre = Vector2.zero;
        foreach (var s in clickGazeHistory)
            if (s.time >= at - ClickLookFrom && s.time <= at - ClickLookTo) { window.Add(s.features); centre += s.point; }
        if (window.Count < ClickMinSamples) return;
        centre /= window.Count;
        foreach (var s in clickGazeHistory)
            if (s.time >= at - ClickLookFrom && s.time <= at - ClickLookTo && Vector2.Distance(s.point, centre) > ClickMaxScatter) return;

        Vector2 click = Canvas.inverse.MultiplyPoint3x4(Vector2.Lerp(clickPendingMouse, control.center, .5f));
        if (click.x < 0 || click.x > 1280 || click.y < 0 || click.y > 900) return;
        if (mapper.AddClick(GazeMapper.Median(window), new Vector2(click.x / 1280f, click.y / 900f)))
        {
            clickCalibrationAccepted++;
            Debug.Log($"[gaze click] learned click #{clickCalibrationAccepted} at {click.x:F0},{click.y:F0}; gaze was {centre.x:F0},{centre.y:F0}; kept {mapper.ClickCount}");
        }
    }
}
