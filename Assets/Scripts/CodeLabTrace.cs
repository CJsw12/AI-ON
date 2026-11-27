using System;
using System.Collections.Generic;

// Only this deterministic trace computes the lesson. The LLM never executes student code.
public sealed class CodeLabTrace
{
    public sealed class Frame
    {
        public int Line, Index, Sum, Value;
        public double? Average;
        public bool Finished;
        public string Note;
    }
    readonly int[] values;
    readonly List<Frame> frames = new List<Frame>();
    public bool Repaired { get; private set; }
    public int Position { get; private set; }
    public int Revision { get; private set; }
    public Frame Current { get { return frames[Position]; } }
    public CodeLabTrace(int[] input) { values = (int[])input.Clone(); Reset(); }
    public void Repair(bool repaired) { Repaired = repaired; Reset(); }
    public void Reset()
    {
        Revision++; Position = 0; frames.Clear();
        frames.Add(new Frame { Index = -1, Line = 0, Note = "합계 변수를 0으로 준비합니다." });
        int sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            frames.Add(new Frame { Index = i, Line = 1, Sum = sum, Value = values[i], Note = "배열에서 다음 점수를 읽습니다." });
            if (!Repaired)
            {
                sum = 0;
                frames.Add(new Frame { Index = i, Line = 2, Value = values[i], Note = "반복문 안에서 합계를 0으로 지웠습니다. 이전 값이 사라집니다." });
            }
            sum += values[i];
            frames.Add(new Frame { Index = i, Line = 3, Sum = sum, Value = values[i], Note = "현재 점수를 합계에 더했습니다." });
        }
        frames.Add(new Frame { Index = -1, Line = 4, Sum = sum, Average = values.Length == 0 ? (double?)null : sum / (double)values.Length,
            Finished = true, Note = values.Length == 0 ? "입력이 없어 평균을 계산하지 않습니다." : "합계를 입력 개수로 나눠 평균을 구했습니다." });
    }
    public bool Step() { if (Current.Finished) return false; Position++; Revision++; return true; }
}
