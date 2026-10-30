using System;

// One faculty's first semester. Every faculty shares the same frame: six chapters, two choice slots per
// chapter (six choices, three quizzes, three minigames in any order) and eight endings.
// Dialogue lines are "speaker|text"; a professor line may hold both tones as "교수|strict|cheerful".
// Follow-up lines that start with "교수님: " may do the same with "strict|cheerful".
public class Story
{
    public string College;                     // One of Stories.Colleges.
    public string Name, Short, Tag;            // "AI-SW융합학부", a short tab label, the pill on the title row.
    public string LevelQuestion;               // Asked before the professor is chosen.
    public string[] Levels, LevelNotes;        // { experienced, novice } and what each one plays.
    public Professor[] Professors;             // [0] strict, [1] cheerful; their pictures are shared for now.
    public string[] Chapters;                  // Chapter titles at [1]..[6].
    public string[][] Dialogue;                // [chapter] lines before the first slot.
    public string[] FollowUp;                  // [chapter] lines between the slots, separated by '\n'.
    public int[][] HiddenDialogue, HiddenFollowUp;   // [chapter] lines where the professor is not on stage.
    public Slot[] Slots;                       // Twelve, at (chapter - 1) * 2 + stage.
    public string[] EndingNames, EndingText;   // EndingText's "교수님:" line is filled from EndingLines.
    public string[][] EndingLines;             // [professor][ending]
    public Func<int, int, int, string> Background;   // (chapter, stage, line) → texture in Resources.
    public Func<int, string, string> Spoken = (professor, text) => text;   // Extra per-professor rewording.
    public Func<int, int, string> Mood = Stories.Mood;   // (chapter, stage) → music: Campus, Study or Crisis.

    public Slot Slot(int chapter, int stage) => Slots[(chapter - 1) * 2 + stage];
    public int Lines(int chapter, int stage) => stage == 0 ? Dialogue[chapter].Length : FollowUp[chapter].Split('\n').Length;

    public bool ProfessorOnStage(int chapter, int stage, int line)
    {
        int[][] hidden = stage == 0 ? HiddenDialogue : HiddenFollowUp;
        return chapter < 1 || chapter >= hidden.Length || Array.IndexOf(hidden[chapter], line) < 0;
    }

    // Speaker "교수" stands for the chosen professor; the caller puts the name in.
    public void Line(int professor, int chapter, int index, out string speaker, out string text)
    {
        string[] lines = Dialogue[chapter];
        string[] parts = lines[Math.Min(index, lines.Length - 1)].Split('|');
        speaker = parts[0];
        text = parts.Length > 2 ? parts[1 + professor] : parts[1];
        if (speaker == "교수") text = Spoken(professor, text);
    }

    public string FollowUpLine(int professor, int chapter, int index)
    {
        string text = FollowUp[chapter].Split('\n')[index];
        const string prefix = "교수님: ";
        return text.StartsWith(prefix) ? prefix + Spoken(professor, Pick(professor, text.Substring(prefix.Length))) : text;
    }

    public string Ending(int professor, int ending)
    {
        string[] lines = EndingText[ending].Split('\n');
        for (int i = 0; i < lines.Length; i++) if (lines[i].StartsWith("교수님:")) lines[i] = "교수님: " + EndingLines[professor][ending];
        return string.Join("\n", lines);
    }

    static string Pick(int professor, string text)
    {
        int bar = text.IndexOf('|');
        return bar < 0 ? text : professor == 0 ? text.Substring(0, bar) : text.Substring(bar + 1);
    }

    // Priority resolves overlapping achievements to exactly one ending; the same rule for every faculty.
    public static int ResolveEnding(int affection, int questions, int teamwork, int interest, int solo)
    {
        if (affection <= 0) return 0;
        if (affection >= 10 && questions >= 2 && teamwork >= 2 && interest >= 2) return 7;
        if (affection >= 7 && interest >= 2) return 6;
        if (affection >= 6 && teamwork >= 2 && questions >= 1) return 5;
        if (questions >= 3) return 4;
        if (solo >= 3) return 3;
        if (teamwork >= 2) return 2;
        return 1;
    }
}

public class Professor
{
    public string Name, Description, TagA, TagB;
}

// One of the twelve slots: a choice (Options), a quiz (Major/Minor) or a minigame (Game).
public class Slot
{
    public string[] Options;
    public int[][] Scores;        // [professor][option] affection change.
    public string[][] Replies;    // [professor][option]
    public string Stats = "";     // Per option, the ending stat it raises: q questions, t teamwork, i interest, s solo, - none.
    public Quiz Major, Minor;     // Experienced players type the answer, novices pick one.
    public MiniGameTheme Game;
    public char Stat = '-';       // Raised by a right quiz answer, or a minigame result that CountsForStat.

    public Quiz Quiz(bool experienced) => experienced ? Major : Minor;
}

// A problem that replaces one of the twelve fixed choices.
// Short answer when Accepted is set (Replies[professor] = { wrong, right }),
// otherwise multiple choice (Replies[professor][option]).
public class Quiz
{
    public string Prompt;
    public string[] Accepted;   // Normalized answers: lower case, no spaces, no trailing ';'.
    public string[] Options;
    public int Answer;
    public string[][] Replies;

    public bool ShortAnswer => Accepted != null;
    public bool IsCorrect(string typed)
    {
        string text = typed.Replace(" ", "").Trim().TrimEnd(';').ToLowerInvariant();
        return Array.IndexOf(Accepted, text) >= 0;
    }
}

public static class Stories
{
    // The title, the ending book and the practice menu group faculties by college; one with none yet says "준비 중".
    public static readonly string[] Colleges = { "공과대학", "자연과학대학", "인문사회대학", "예체능대학" };

    // Add new faculties at the end only: the index is kept in save slots and the ending book.
    public static readonly Story[] All = { AiSwStory.Data, SmartSystemsStory.Data, BusinessStory.Data, ArtsHealthStory.Data };

    // Faculty indexes in one college, in the order above.
    public static int[] In(int college)
    {
        var faculties = new System.Collections.Generic.List<int>();
        for (int i = 0; i < All.Length; i++) if (All[i].College == Colleges[college]) faculties.Add(i);
        return faculties.ToArray();
    }
    public static int CollegeOf(int faculty) => Math.Max(0, Array.IndexOf(Colleges, All[faculty].College));

    // Wraps a reply with the professor's tone for very high or negative affection.
    public static string Decorate(int professor, string text, int affection)
    {
        if (affection >= 7) return text + (professor == 0 ? " 지난번보다 판단이 좋아졌습니다. 계속 그렇게 해 보세요." : " 다음에 또 이야기해 줘요. 은근히 기다리게 되네요!");
        if (affection < 0) return (professor == 0 ? "원칙부터 확인하겠습니다. " : "잠깐, 이 부분은 짚고 갈게요. ") + text;
        return text;
    }

    // AI-SW's music; other faculties set their own Story.Mood where their trouble comes at another time.
    public static string Mood(int chapter, int stage) =>
        chapter == 5 || (chapter == 6 && stage == 0) ? "Crisis" : chapter <= 1 || chapter == 7 || (chapter == 2 && stage == 1) || (chapter == 6 && stage == 1) ? "Campus" : "Study";
}
