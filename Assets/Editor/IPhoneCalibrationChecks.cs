using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class IPhoneCalibrationChecks
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Checks/iPhone Calibration")]
    public static void Run()
    {
        var host = new GameObject("iPhone calibration check");
        host.SetActive(false); // Do not run GameController.Awake or start audio/cameras.
        string key = "Gaze.IPhoneMapper.WiFi";
        bool existed = PlayerPrefs.HasKey(key);
        string saved = PlayerPrefs.GetString(key);
        Action<string> sound = NovelUI.Sound;
        try
        {
            NovelUI.Sound = null;
            var g = host.AddComponent<GameController>();
            var targets = (Vector2[])typeof(GameController).GetField("IPhoneCalibrationTargets", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var features = (double[][])Get(g, "iphoneMeasuredFeatures");
            var samples = (List<double[]>[])Get(g, "iphoneMeasuredSamples");
            for (int i = 0; i < targets.Length; i++)
            {
                // The independent check points deliberately do not match their labels.
                Vector2 p = i < 13 ? targets[i] : new Vector2(.5f, .5f);
                features[i] = GazeMapper.Features(p * 10, Vector2.zero, p);
                samples[i] = new List<double[]>();
                for (int n = 0; n < 36; n++) samples[i].Add(features[i]);
            }
            foreach (string field in new[] { "iphoneCaptureAll", "iphoneCaptureOpen" })
            {
                var capture = (List<double[]>)Get(g, field);
                capture.AddRange(samples[15]);
            }
            Set(g, "iphoneCalibrationPoint", 15);
            Set(g, "iphoneCalibrating", true);
            typeof(GameController).GetMethod("CompleteIPhoneCalibrationPoint", Fields).Invoke(g, null);
            var mapper = (GazeMapper)Get(g, "iphoneMapper");
            if (mapper.CheckPixels <= 160) throw new Exception("Fixture must reproduce a bad independent check.");
            if ((bool)Get(g, "iphoneCalibrated")) throw new Exception("A failed independent check was accepted as calibrated.");
            if (PlayerPrefs.HasKey(key) != existed || PlayerPrefs.GetString(key) != saved)
                throw new Exception("A failed calibration overwrote the saved profile.");
            string failure = (string)Get(g, "iphoneStatus");
            Set(g, "gazeTestOpen", true);
            Set(g, "gazeUseIphone", true);
            Set(g, "iphoneSource", new TestSource());
            typeof(GameController).GetMethod("UpdateIPhoneGaze", Fields).Invoke(g, null);
            if ((string)Get(g, "iphoneStatus") != failure)
                throw new Exception("The validation failure message disappeared on the next tracking frame.");

            for (int i = 13; i < 16; i++)
            {
                features[i] = GazeMapper.Features(targets[i] * 10, Vector2.zero, targets[i]);
                samples[i].Clear();
                for (int n = 0; n < 36; n++) samples[i].Add(features[i]);
            }
            foreach (string field in new[] { "iphoneCaptureAll", "iphoneCaptureOpen" })
            {
                var capture = (List<double[]>)Get(g, field);
                capture.Clear(); capture.AddRange(samples[15]);
            }
            Set(g, "iphoneCalibrationPoint", 15);
            Set(g, "iphoneCalibrating", true);
            typeof(GameController).GetMethod("CompleteIPhoneCalibrationPoint", Fields).Invoke(g, null);
            if (!(bool)Get(g, "iphoneCalibrated") || !PlayerPrefs.HasKey(key))
                throw new Exception("A valid independent check could not finish and save calibration.");
            Debug.Log("[iPhone calibration checks] PASS: failed check rejected, old profile preserved, failure message persists, valid check finishes and saves.");
        }
        finally
        {
            NovelUI.Sound = sound;
            if (existed) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    static object Get(GameController g, string name) => typeof(GameController).GetField(name, Fields).GetValue(g);
    static void Set(GameController g, string name, object value) => typeof(GameController).GetField(name, Fields).SetValue(g, value);

    sealed class TestSource : IIPhoneGazeSource
    {
        public Vector2 EyeFeature => Vector2.zero;
        public Vector2 HeadFeature => Vector2.zero;
        public Vector2 PositionFeature => Vector2.zero;
        public Vector2 LookFeature => Vector2.zero;
        public float Blink => 0;
        public bool FaceFound => true;
        public bool HasFreshSample => true;
        public int Sequence => 1;
        public string Status => "test";
        public void Poll() { }
        public void StopReceiving() { }
    }
}
