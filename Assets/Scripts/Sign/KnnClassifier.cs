using System;
using System.Collections.Generic;
public sealed class KnnClassifier
{
    public sealed class Sample { public readonly float[] Features; public readonly int Label; public Sample(float[] features, int label) { if(features==null) throw new ArgumentException("특징이 없습니다."); Features=(float[])features.Clone(); Label=label; } }
    public sealed class Result { public int FirstLabel=-1, SecondLabel=-1; public float FirstScore, SecondScore, NearestDistance=float.PositiveInfinity, FirstLabelDistance=float.PositiveInfinity; public bool Rejected=true, IsOther; public int PredictedLabel { get { return Rejected ? SignVote.Unknown : FirstLabel; } } }
    readonly List<Sample> samples; readonly SignLabels labels; readonly int k, minVoteMargin, featureCount; readonly float maxDistance;
    public KnnClassifier(IEnumerable<Sample> samples, SignLabels labels, int k=5, float maxDistance=float.PositiveInfinity, int minVoteMargin=1) {
        if(samples==null||labels==null||k<1||float.IsNaN(maxDistance)||maxDistance<0||minVoteMargin<0) throw new ArgumentException("분류기 설정이 잘못되었습니다.");
        this.samples=new List<Sample>(); this.labels=labels; this.k=k; this.maxDistance=maxDistance; this.minVoteMargin=minVoteMargin; int length=-1;
        foreach(var s in samples) { if(s==null||s.Label<0||s.Label>=labels.Count||s.Features==null||s.Features.Length==0||(length>=0&&s.Features.Length!=length)) throw new ArgumentException("학습 표본이 잘못되었습니다."); foreach(float v in s.Features) if(float.IsNaN(v)||float.IsInfinity(v)) throw new ArgumentException("학습 특징이 유한하지 않습니다."); length=s.Features.Length; this.samples.Add(new Sample(s.Features,s.Label)); } featureCount=length;
    }
    public Result Classify(float[] features) {
        var r=new Result(); if(features==null||features.Length==0||features.Length!=featureCount) return r;
        foreach(float v in features) if(float.IsNaN(v)||float.IsInfinity(v)) return r;
        var distances=new List<KeyValuePair<double,int>>(); foreach(var s in samples) { double d=0; for(int i=0;i<features.Length;i++) { double v=(double)features[i]-s.Features[i]; d+=v*v; } distances.Add(new KeyValuePair<double,int>(Math.Sqrt(d),s.Label)); }
        distances.Sort((a,b)=> { int c=a.Key.CompareTo(b.Key); return c!=0?c:a.Value.CompareTo(b.Value); }); if(distances.Count==0) return r;
        int count=Math.Min(k,distances.Count); double cutoff=distances[count-1].Key; while(count<distances.Count && distances[count].Key-cutoff<=1e-9) count++;
        var votes=new int[labels.Count]; for(int i=0;i<count;i++) votes[distances[i].Value]++;
        for(int i=0;i<votes.Length;i++) if(votes[i]>0) { if(r.FirstLabel<0||votes[i]>votes[r.FirstLabel]) { r.SecondLabel=r.FirstLabel; r.FirstLabel=i; } else if(r.SecondLabel<0||votes[i]>votes[r.SecondLabel]) r.SecondLabel=i; }
        int second=r.SecondLabel<0?0:votes[r.SecondLabel]; r.FirstScore=(float)votes[r.FirstLabel]/count; r.SecondScore=(float)second/count; r.NearestDistance=(float)distances[0].Key;
        for(int i=0;i<count;i++) if(distances[i].Value==r.FirstLabel) { r.FirstLabelDistance=(float)distances[i].Key; break; }
        r.IsOther=labels[r.FirstLabel]==SignLabels.Other; int margin=votes[r.FirstLabel]-second; r.Rejected=r.IsOther || r.FirstLabelDistance>maxDistance || margin==0 || margin<minVoteMargin; return r;
    }
}
