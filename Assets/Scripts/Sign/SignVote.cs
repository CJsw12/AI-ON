using System;
using System.Collections.Generic;
public sealed class SignVote
{
    public const int Unknown=-1, NoHand=-2;
    public sealed class Result { public int Label=Unknown; public bool Confirmed; public float HoldProgress; }
    struct Entry { public long Time; public int Label; }
    readonly List<Entry> entries=new List<Entry>();
    readonly double windowMs, gapMs, holdMs;
    long lastTime, holdStart; bool hasLast, confirmed; int held=Unknown;
    public SignVote(double windowSeconds=0.33, double gapSeconds=0.2, double holdSeconds=0.6) { if(double.IsNaN(windowSeconds)||double.IsInfinity(windowSeconds)||double.IsNaN(gapSeconds)||double.IsInfinity(gapSeconds)||double.IsNaN(holdSeconds)||double.IsInfinity(holdSeconds)||windowSeconds<=0||gapSeconds<=0||holdSeconds<0||double.IsInfinity(windowSeconds*1000)||double.IsInfinity(gapSeconds*1000)||double.IsInfinity(holdSeconds*1000)) throw new ArgumentException("시간 설정이 잘못되었습니다."); windowMs=windowSeconds*1000; gapMs=gapSeconds*1000; holdMs=holdSeconds*1000; }
    void Clear() { entries.Clear(); held=Unknown; confirmed=false; }
    public void Reset() { Clear(); hasLast=false; lastTime=0; holdStart=0; }
    public Result Add(long timestampMs, int label) {
        if(label<NoHand) throw new ArgumentException("결과가 잘못되었습니다.");
        if(hasLast && (timestampMs<lastTime || timestampMs-lastTime>=gapMs)) Clear(); lastTime=timestampMs; hasLast=true;
        var r=new Result(); if(label==NoHand) { Clear(); return r; }
        entries.Add(new Entry { Time=timestampMs, Label=label }); entries.RemoveAll(e=>timestampMs-e.Time>windowMs);
        var counts=new Dictionary<int,int>(); foreach(var e in entries) if(e.Label>=0) { int n; counts.TryGetValue(e.Label,out n); counts[e.Label]=n+1; }
        foreach(var pair in counts) if(pair.Value>entries.Count/2) { r.Label=pair.Key; break; }
        if(r.Label==Unknown) { held=Unknown; confirmed=false; return r; }
        if(held!=r.Label) { held=r.Label; holdStart=timestampMs; confirmed=false; }
        r.HoldProgress = holdMs <= 0 ? 1f : (float)Math.Min(1.0, Math.Max(0.0, (timestampMs-holdStart)/holdMs));
        if(!confirmed && timestampMs-holdStart>=holdMs) { confirmed=true; r.Confirmed=true; } return r;
    }
}
