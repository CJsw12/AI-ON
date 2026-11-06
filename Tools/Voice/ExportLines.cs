// Lists every line one professor speaks in one faculty, one per row, for the voice generator.
// Built by export_lines.ps1 against the game scripts; nothing here ships with the game.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class ExportLines
{
    static int Main(string[] args)
    {
        string voice = args[0], output = args[1];
        var lines = new List<string>();
        int found = 0;
        for (int f = 0; f < Stories.All.Length; f++)
            for (int p = 0; p < Stories.All[f].Professors.Length; p++)
                if (Stories.All[f].Professors[p].Voice == voice) { Collect(Stories.All[f], p, lines); found++; }
        if (found == 0) { Console.Error.WriteLine("No professor has Voice = " + voice); return 1; }
        var unique = lines.Select(ProfessorVoice.Normalize).Where(t => t.Length > 0).Distinct().ToList();
        File.WriteAllLines(output, unique, new System.Text.UTF8Encoding(false));
        Console.WriteLine(unique.Count + " lines -> " + output);
        return 0;
    }

    static void Collect(Story s, int p, List<string> lines)
    {
        foreach (bool experienced in new[] { false, true })
            for (int chapter = 1; chapter < s.Dialogue.Length; chapter++)
            {
                string[][] source = experienced && s.ExperiencedDialogue != null ? s.ExperiencedDialogue : s.Dialogue;
                for (int i = 0; i < source[chapter].Length; i++)
                {
                    s.Line(p, chapter, i, out string speaker, out string text, experienced);
                    if (speaker == "교수") lines.Add(text);
                }
                string[] follow = (experienced && s.ExperiencedFollowUp != null ? s.ExperiencedFollowUp : s.FollowUp)[chapter].Split('\n');
                for (int i = 0; i < follow.Length; i++)
                {
                    string text = s.FollowUpLine(p, chapter, i, experienced);
                    if (text.StartsWith("교수님: ")) lines.Add(text.Substring(5));
                }
            }
        foreach (Slot slot in s.Slots)
        {
            if (slot.Replies != null && slot.Replies[p] != null) lines.AddRange(slot.Replies[p]);
            foreach (Quiz quiz in new[] { slot.Major, slot.Minor })
                if (quiz?.Replies != null) lines.AddRange(quiz.Replies[p]);
            if (slot.Game?.Replies != null) lines.AddRange(slot.Game.Replies[p]);
        }
        lines.AddRange(s.EndingLines[p]);
        if (s.Fond != null) lines.Add(s.Fond[p]);
        if (s.Wary != null) lines.Add(s.Wary[p]);
        if (!string.IsNullOrEmpty(s.Professors[p].LookAwayLine)) lines.Add(s.Professors[p].LookAwayLine);
    }
}
