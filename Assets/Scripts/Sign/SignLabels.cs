using System;
using System.Collections.Generic;
public sealed class SignLabels
{
    public const string Other = "_other";
    readonly string[] labels;
    public SignLabels(string[] labels) { if (labels == null || labels.Length == 0) throw new ArgumentException("라벨이 필요합니다."); this.labels=(string[])labels.Clone(); var seen=new HashSet<string>(); foreach (string label in this.labels) if (string.IsNullOrEmpty(label) || !seen.Add(label)) throw new ArgumentException("라벨이 비었거나 중복되었습니다."); }
    public int Count { get { return labels.Length; } }
    public string this[int index] { get { return labels[index]; } }
    public int IndexOf(string label) { return Array.IndexOf(labels,label); }
}
