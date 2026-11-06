using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// Recorded (TTS) lines for professors whose Professor.Voice is set.
// Resources/Voice/<Voice>/lines.txt has one "file<TAB>text" row per spoken line, and the clip is
// Resources/Voice/<Voice>/<file>. Tools/Voice/generate.ps1 writes both; see Tools/Voice/README.md.
public static class ProfessorVoice
{
    static readonly Regex Tags = new Regex("<[^>]+>"), Spaces = new Regex(@"\s+");
    static readonly Dictionary<string, Dictionary<string, string>> indexes = new Dictionary<string, Dictionary<string, string>>();

    // The form a line is matched by: no rich-text tags, single spaces, trimmed.
    public static string Normalize(string text) => Spaces.Replace(Tags.Replace(text ?? "", ""), " ").Trim();

    // Clips for what the professor says in text, in order. Each row of text is looked up whole, or as a
    // run of known lines joined by spaces (Stories.Decorate puts Wary before a reply and Fond after it).
    // Rows that are not the professor's (quiz feedback, minigame summaries, the affection line) are skipped.
    public static List<AudioClip> Clips(string voice, string text)
    {
        var clips = new List<AudioClip>();
        var index = Index(voice);
        if (index == null || string.IsNullOrEmpty(text)) return clips;
        foreach (string row in text.Split('\n'))
        {
            var files = new List<string>();
            if (!Split(index, Normalize(row), files)) continue;
            foreach (string file in files)
            {
                AudioClip clip = Resources.Load<AudioClip>("Voice/" + voice + "/" + file);
                if (clip != null) clips.Add(clip);
            }
        }
        return clips;
    }

    static bool Split(Dictionary<string, string> index, string text, List<string> files)
    {
        if (text.Length == 0) return files.Count > 0;
        if (index.TryGetValue(text, out string whole)) { files.Add(whole); return true; }
        for (int cut = text.Length - 1; cut > 0; cut--)
        {
            if (text[cut] != ' ' || !index.TryGetValue(text.Substring(0, cut), out string head)) continue;
            files.Add(head);
            if (Split(index, text.Substring(cut + 1), files)) return true;
            files.RemoveAt(files.Count - 1);
        }
        return false;
    }

    static Dictionary<string, string> Index(string voice)
    {
        if (string.IsNullOrEmpty(voice)) return null;
        if (indexes.TryGetValue(voice, out var index)) return index;
        index = null;
        TextAsset list = Resources.Load<TextAsset>("Voice/" + voice + "/lines");
        if (list != null)
        {
            index = new Dictionary<string, string>();
            foreach (string row in list.text.Split('\n'))
            {
                int tab = row.IndexOf('\t');
                if (tab > 0) index[Normalize(row.Substring(tab + 1))] = row.Substring(0, tab).Trim();
            }
        }
        indexes[voice] = index;
        return index;
    }
}
