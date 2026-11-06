using System;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class DodgeProfessorChecks
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Eye Dodge/Preview Selected Professor")]
    public static void PreviewSelected()
    {
        var g = UnityEngine.Object.FindFirstObjectByType<GameController>();
        if (!EditorApplication.isPlaying || g == null || (int)typeof(GameController).GetField("professor", Fields).GetValue(g) < 0)
        {
            EditorUtility.DisplayDialog("눈빛 피하기 확인", "Play 모드에서 학과와 교수님을 먼저 선택해 주세요.", "확인");
            return;
        }
        typeof(GameController).GetMethod("OpenGazeTest", Fields).Invoke(g, null);
        typeof(GameController).GetMethod("StartDodgeGame", Fields).Invoke(g, null);
        Debug.Log("[eye dodge] Previewing the selected professor; live play still requires a calibrated gaze source.");
    }

    [MenuItem("Tools/Checks/Dodge Selected Professor")]
    public static void Run()
    {
        var host = new GameObject("Dodge portrait check");
        host.SetActive(false);
        try
        {
            var g = host.AddComponent<GameController>();
            var type = typeof(GameController);
            var prepare = type.GetMethod("PrepareDodgePortrait", Fields);
            var now = type.GetProperty("DodgeFaceNow", Fields);
            int count = 0;
            for (int faculty = 0; faculty < Stories.All.Length; faculty++)
                for (int who = 0; who < Stories.All[faculty].Professors.Length; who++)
                {
                    type.GetField("faculty", Fields).SetValue(g, faculty);
                    type.GetField("professor", Fields).SetValue(g, who);
                    if (prepare != null) prepare.Invoke(g, null);
                    string expected = Professor.Picture(Stories.All[faculty].Professors[who], who, "Happy");
                    if (!ProfessorDodgeEyes.TryGet(expected, out var anchor)) throw new Exception("Missing pupil anchors " + expected);
                    using (var md5 = MD5.Create())
                    {
                        string file = Path.Combine(Application.dataPath, "Resources", expected + ".png");
                        string hash = BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
                        if (hash != anchor.SourceHash) throw new Exception("Portrait changed; remeasure its pupils: " + expected);
                    }
                    foreach (float time in new[] { 0f, 10f, 100f })
                    {
                        type.GetField("dodgeAlive", Fields).SetValue(g, time);
                        type.GetField("dodgeFinished", Fields).SetValue(g, time == 100f);
                        object face = now.GetValue(g);
                        Type ft = face.GetType();
                        string actual = (string)ft.GetField("Picture").GetValue(face);
                        if (actual != expected) throw new Exception("Selected smile not retained: " + expected + " got " + actual);
                        if (Resources.Load<Texture2D>(actual) == null) throw new Exception("Missing portrait " + actual);
                        Rect screen = (Rect)ft.GetField("Screen").GetValue(face);
                        var eyeMethod = ft.GetMethod("Eye");
                        Vector2 left = (Vector2)eyeMethod.Invoke(face, new object[] { 0 });
                        Vector2 right = (Vector2)eyeMethod.Invoke(face, new object[] { 1 });
                        if (!screen.Contains(left) || !screen.Contains(right) || right.x <= left.x)
                            throw new Exception("Invalid eyes " + actual);
                        if (Vector2.Distance((left + right) * .5f, new Vector2(640, 257)) > .02f)
                            throw new Exception("Image and eyes do not share the same transform " + actual);
                        Vector2 source = anchor.Left;
                        Vector2 drawn = screen.position + new Vector2(source.x / anchor.Width * screen.width,
                            (source.y / anchor.Height - .002f) / .996f * screen.height);
                        if (Vector2.Distance(drawn, left) > .02f) throw new Exception("Pupil does not match the inset texture " + actual);
                    }
                    count++;
                }
            Debug.Log("[Dodge professor checks] PASS: " + count + " selected happy portraits, eye transforms and unchanged portrait during play/results.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
