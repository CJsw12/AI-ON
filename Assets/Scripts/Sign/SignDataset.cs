using System;
using System.Collections.Generic;
using System.IO;

public sealed class SignDataset
{
    public readonly List<KnnClassifier.Sample> Samples = new List<KnnClassifier.Sample>();
    public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
    public SignLabels Labels { get; private set; }
    public int FileCount { get; private set; }
    public int InvalidLines { get; private set; }
    public bool IsEmpty { get { return Samples.Count == 0; } }
    public float SuggestedMaxDistance { get; private set; }
    public double DistanceCalculationMilliseconds { get; private set; }
    public int DistanceReferenceCount { get; private set; }
    public const float MinimumSuggestedMaxDistance = 0.05f;
    const int MaxDistanceReferences = 1000, DistanceSampleSeed = 1701;
    sealed class DatasetRow { public SignSample Source; public float[] Features; }
    SignDataset() { SuggestedMaxDistance = float.PositiveInfinity; }
    static bool SameTrial(SignSample a, SignSample b)
    {
        return a.Person == b.Person && a.Session == b.Session && a.Trial == b.Trial;
    }
    void CalculateSuggestedMaxDistance(List<DatasetRow> rows)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
        var groups = new Dictionary<string, List<int>>();
        for (int i = 0; i < rows.Count; i++)
        {
            List<int> group;
            if (!groups.TryGetValue(rows[i].Source.Label, out group)) { group = new List<int>(); groups.Add(rows[i].Source.Label, group); }
            group.Add(i);
        }
        foreach (var group in groups.Values)
        {
            bool found = false;
            for (int i = 1; i < group.Count; i++) if (!SameTrial(rows[group[0]].Source, rows[group[i]].Source)) { found = true; break; }
            if (!found) return;
        }
        var indices = new int[rows.Count];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        var random = new Random(DistanceSampleSeed);
        DistanceReferenceCount = Math.Min(indices.Length, MaxDistanceReferences);
        for (int i = 0; i < DistanceReferenceCount; i++)
        {
            int j = random.Next(i, indices.Length); int temp = indices[i]; indices[i] = indices[j]; indices[j] = temp;
        }
        var distances = new List<double>();
        for (int p = 0; p < DistanceReferenceCount; p++)
        {
            var reference = rows[indices[p]];
            double nearestSquared = double.PositiveInfinity;
            foreach (int j in groups[reference.Source.Label])
            {
                var candidate = rows[j];
                if (SameTrial(reference.Source, candidate.Source)) continue;
                double sum = 0;
                for (int n = 0; n < reference.Features.Length; n++) { double d = (double)reference.Features[n] - candidate.Features[n]; sum += d * d; if (sum >= nearestSquared) break; }
                nearestSquared = Math.Min(nearestSquared, sum);
            }
            distances.Add(Math.Sqrt(nearestSquared));
        }
        distances.Sort();
        if (distances.Count > 0) SuggestedMaxDistance = (float)Math.Max(MinimumSuggestedMaxDistance, distances[(int)Math.Ceiling(distances.Count * 0.95) - 1] * 1.5);
        }
        finally { clock.Stop(); DistanceCalculationMilliseconds = clock.Elapsed.TotalMilliseconds; }
    }
    // 이 폴더에는 팀이 동의 범위 안에서 고른 좌표 CSV만 둔다.
    public static SignDataset Load(string directory, bool labelsAreMirrored = false)
    {
        var data = new SignDataset();
        if (!Directory.Exists(directory)) return data;
        var rows = new List<DatasetRow>();
        string[] files = Directory.GetFiles(directory, "*.csv", SearchOption.AllDirectories); Array.Sort(files, StringComparer.Ordinal);
        data.FileCount = files.Length;
        foreach (string file in files)
        {
            bool first = true;
            foreach (string line in File.ReadLines(file))
            {
                if (first && line.TrimStart('\uFEFF') == SignSample.Header) { first = false; continue; }
                first = false;
                SignSample sample; string error; bool left; float[] features;
                if (!SignSample.TryParse(line, out sample, out error) ||
                    !SignHandedness.TryIsLeft(sample.Handedness, labelsAreMirrored, out left) ||
                    !SignFeatures.TryCompute(sample.Landmarks, sample.Width, sample.Height, left, out features))
                { data.InvalidLines++; continue; }
                rows.Add(new DatasetRow { Source = sample, Features = features });
                int count; data.Counts.TryGetValue(sample.Label, out count); data.Counts[sample.Label] = count + 1;
            }
        }
        if (rows.Count == 0) return data;
        var names = new List<string>(data.Counts.Keys); names.Sort(StringComparer.Ordinal);
        data.Labels = new SignLabels(names.ToArray());
        foreach (var row in rows) data.Samples.Add(new KnnClassifier.Sample(row.Features, data.Labels.IndexOf(row.Source.Label)));
        data.CalculateSuggestedMaxDistance(rows);
        return data;
    }
}
