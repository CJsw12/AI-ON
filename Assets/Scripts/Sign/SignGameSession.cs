using System;
using System.Collections.Generic;

public sealed class SignGameSession
{
    public enum Stage { Intro, Practice, Challenge, Result }
    public sealed class Answer { public string Label; public bool Correct; public long ElapsedMs; public int WrongAttempts; }
    readonly List<string> letters = new List<string>();
    readonly List<string> challenge = new List<string>();
    readonly List<Answer> answers = new List<Answer>();
    readonly long limitMs;
    int index; long started, lastTime; bool armed = true, hasLastTime;
    public Stage CurrentStage { get; private set; }
    public string CurrentLabel { get { var current = CurrentStage == Stage.Challenge ? challenge : letters; return CurrentStage != Stage.Result && index < current.Count ? current[index] : null; } }
    public IList<Answer> Answers { get { return answers.AsReadOnly(); } }
    public int CorrectCount { get; private set; }
    public string LastWrongLabel { get; private set; }
    public int WrongAttempts { get; private set; }
    public long RemainingMs(long time) { return Math.Max(0, limitMs - Math.Max(0, time - started)); }
    public SignGameSession(SignLabels labels, long limitMs = 8000, int seed = 17)
    {
        if (labels == null || limitMs <= 0) throw new ArgumentException("게임 설정이 잘못되었습니다.");
        this.limitMs = limitMs;
        for (int i = 0; i < labels.Count; i++) if (labels[i] != SignLabels.Other) letters.Add(labels[i]);
        if (letters.Count == 0) throw new ArgumentException("연습할 글자가 없습니다.");
        challenge.AddRange(letters);
        var random = new Random(seed);
        for (int i = challenge.Count - 1; i > 0; i--) { int j = random.Next(i + 1); string t = challenge[i]; challenge[i] = challenge[j]; challenge[j] = t; }
        if (challenge.Count > 5) challenge.RemoveRange(5, challenge.Count - 5);
    }
    public void BeginPractice() { if (CurrentStage == Stage.Intro) { CurrentStage = Stage.Practice; index = 0; armed = true; } }
    public void Update(long time, string confirmedLabel)
    {
        if (CurrentStage != Stage.Practice && CurrentStage != Stage.Challenge) return;
        if (hasLastTime && time < lastTime) return;
        lastTime = time; hasLastTime = true;
        if (string.IsNullOrEmpty(confirmedLabel)) armed = true;
        bool confirmed = !string.IsNullOrEmpty(confirmedLabel) && armed;
        if (confirmed) armed = false;
        if (CurrentStage == Stage.Practice)
        {
            if (confirmed && confirmedLabel == CurrentLabel && ++index == letters.Count)
            { CurrentStage = Stage.Challenge; index = 0; started = time; }
            return;
        }
        long elapsed = Math.Max(0, time - started);
        if (elapsed >= limitMs || (confirmed && confirmedLabel == CurrentLabel))
        {
            bool correct = elapsed < limitMs && confirmedLabel == CurrentLabel;
            answers.Add(new Answer { Label = CurrentLabel, Correct = correct, ElapsedMs = Math.Min(elapsed, limitMs), WrongAttempts = WrongAttempts });
            if (correct) CorrectCount++;
            index++; started = time; WrongAttempts = 0; LastWrongLabel = null;
            if (index == challenge.Count) CurrentStage = Stage.Result;
        }
        else if (confirmed) { LastWrongLabel = confirmedLabel; WrongAttempts++; }
    }
}
