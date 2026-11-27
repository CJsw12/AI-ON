using System;

public enum HeartLevel
{
    Unknown,
    Calm,
    Elevated,
    High
}

/// <summary>Tracks heart-rate freshness, rolling baseline median, and relative arousal level.</summary>
public sealed class HeartRateTracker
{
    public const double SignalTimeoutSeconds = 5.0;
    public const double BaselineMinDurationSeconds = 30.0;
    public const int BaselineMinSamples = 10;
    public const double BaselineWindowSeconds = 300.0;
    public const int BaselineRingCapacity = 400;
    public const double RecentAverageWindowSeconds = 5.0;
    public const double ElevatedDeltaThreshold = 8.0;
    public const double HighDeltaThreshold = 20.0;

    readonly double[] sampleTimes = new double[BaselineRingCapacity];
    readonly double[] sampleBpms = new double[BaselineRingCapacity];
    readonly double[] medianScratch = new double[BaselineRingCapacity];

    int ringHead;
    int ringCount;
    bool hasPacket;
    bool lastPacketOk;
    double lastPacketTime;
    bool hasFirstValid;
    double firstValidTime;
    bool baselineEstablished;

    public double Bpm { get; private set; }
    public double Baseline { get; private set; }
    public bool HasBaseline { get { return baselineEstablished; } }
    public long Received { get; private set; }
    public long Rejected { get; private set; }

    public HeartRateTracker()
    {
        Reset();
    }

    public void Reset()
    {
        ringHead = 0;
        ringCount = 0;
        hasPacket = false;
        lastPacketOk = false;
        lastPacketTime = double.NaN;
        hasFirstValid = false;
        firstValidTime = double.NaN;
        baselineEstablished = false;
        Bpm = double.NaN;
        Baseline = double.NaN;
        Received = 0;
        Rejected = 0;
    }

    public void RejectBadPacket()
    {
        Rejected++;
    }

    public void Feed(HeartRatePacket p, double nowSeconds)
    {
        if (!Finite(nowSeconds))
        {
            return;
        }

        if (p.Ok && (!Finite(p.Bpm) || p.Bpm < HeartRatePacket.MinBpm || p.Bpm > HeartRatePacket.MaxBpm))
        {
            Rejected++;
            return;
        }

        Received++;
        hasPacket = true;
        lastPacketTime = nowSeconds;
        lastPacketOk = p.Ok;

        if (!p.Ok)
        {
            return;
        }

        Bpm = p.Bpm;
        if (!hasFirstValid)
        {
            hasFirstValid = true;
            firstValidTime = nowSeconds;
        }

        sampleTimes[ringHead] = nowSeconds;
        sampleBpms[ringHead] = p.Bpm;
        ringHead = (ringHead + 1) % BaselineRingCapacity;
        if (ringCount < BaselineRingCapacity)
        {
            ringCount++;
        }

        UpdateBaseline(nowSeconds);
    }

    public bool HasSignal(double now)
    {
        if (!hasPacket || !Finite(now) || !Finite(lastPacketTime))
        {
            return false;
        }
        double elapsed = now - lastPacketTime;
        return elapsed >= 0.0 && elapsed <= SignalTimeoutSeconds;
    }

    public bool Valid(double now)
    {
        return HasSignal(now) && lastPacketOk;
    }

    public HeartLevel Level(double now)
    {
        if (!Valid(now))
        {
            return HeartLevel.Unknown;
        }

        if (!baselineEstablished)
        {
            UpdateBaseline(now);
        }
        if (!baselineEstablished || !Finite(Baseline))
        {
            return HeartLevel.Unknown;
        }

        double sum = 0.0;
        int count = 0;
        for (int i = 0; i < ringCount; i++)
        {
            double age = now - sampleTimes[i];
            if (age >= 0.0 && age <= RecentAverageWindowSeconds)
            {
                sum += sampleBpms[i];
                count++;
            }
        }
        if (count == 0)
        {
            return HeartLevel.Unknown;
        }

        double delta = (sum / count) - Baseline;
        if (delta >= HighDeltaThreshold)
        {
            return HeartLevel.High;
        }
        if (delta >= ElevatedDeltaThreshold)
        {
            return HeartLevel.Elevated;
        }
        return HeartLevel.Calm;
    }

    void UpdateBaseline(double now)
    {
        if (!hasFirstValid || !Finite(now) || !Finite(firstValidTime))
        {
            return;
        }

        if (!baselineEstablished)
        {
            if (now - firstValidTime < BaselineMinDurationSeconds)
            {
                return;
            }
            int initialCount = CollectBaselineSamples(now);
            if (initialCount < BaselineMinSamples)
            {
                return;
            }
            Baseline = ComputeMedian(initialCount);
            baselineEstablished = true;
            return;
        }

        int recentCount = CollectBaselineSamples(now);
        if (recentCount >= BaselineMinSamples)
        {
            Baseline = ComputeMedian(recentCount);
        }
    }

    int CollectBaselineSamples(double now)
    {
        int count = 0;
        for (int i = 0; i < ringCount; i++)
        {
            double age = now - sampleTimes[i];
            if (age >= 0.0 && age <= BaselineWindowSeconds)
            {
                medianScratch[count] = sampleBpms[i];
                count++;
            }
        }
        return count;
    }

    double ComputeMedian(int count)
    {
        Array.Sort(medianScratch, 0, count);
        int mid = count / 2;
        if ((count & 1) == 1)
        {
            return medianScratch[mid];
        }
        return (medianScratch[mid - 1] + medianScratch[mid]) * 0.5;
    }

    static bool Finite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
