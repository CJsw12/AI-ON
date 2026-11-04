using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// Warns when a professor portrait is new or changed since Tools/generate_portrait_faces.py last ran: such a picture
// is drawn with the old fallback framing (wrong size, possibly squashed) until the table is regenerated.
public class PortraitFacesCheck : AssetPostprocessor
{
    [InitializeOnLoadMethod]
    static void CheckOnLoad() => EditorApplication.delayCall += Check;

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
            if (path.StartsWith("Assets/Resources/Professor") && path.EndsWith("Transparent.png")) { Check(); return; }
    }

    static void Check()
    {
        var stale = new List<string>();
        foreach (string path in Directory.GetFiles("Assets/Resources", "Professor*Transparent.png"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!PortraitFaces.SourceHash.TryGetValue(name, out string hash)) { stale.Add(name + " (new)"); continue; }
            using (var md5 = MD5.Create())
            {
                string actual = System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                if (!actual.StartsWith(hash)) stale.Add(name + " (changed)");
            }
        }
        if (stale.Count > 0)
            Debug.LogWarning($"교수 초상 {stale.Count}장이 얼굴 위치 표와 맞지 않아요. 저장소 루트에서 `python Tools/generate_portrait_faces.py`를 실행해 PortraitFaces.cs를 다시 만들어 주세요.\n" + string.Join("\n", stale));
    }
}
