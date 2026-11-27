using System;

/// <summary>Tracks Wear OS IMU freshness and normalized recent motion intensity without per-frame allocations.</summary>
public sealed class WatchImuTracker
{
    public const double SignalTimeoutSeconds = 1.5;
    public const double MotionWindowSeconds = 1.0;
    public const double GravityMetersPerSecondSq = 9.81;
    public const double MotionFullScale = 8.0;
    public const int RingCapacity = 40;

    readonly double[] sampleTimes = new double[RingCapacity];
    readonly double[] samplePeakAccels = new double[RingCapacity];

    int ringHead;
    int ringCount;
    bool hasPacket;
    double lastPacketTime;
    WatchImuPacket latest;

    public long Received { get; private set; }
    public WatchImuPacket Latest { get { return latest; } }
    public double Ax { get { return latest.Ax; } }
    public double Ay { get { return latest.Ay; } }
    public double Az { get { return latest.Az; } }
    public double Gx { get { return latest.Gx; } }
    public double Gy { get { return latest.Gy; } }
    public double Gz { get { return latest.Gz; } }
    public double PeakAccel { get { return latest.PeakAccel; } }
    public double PeakGyro { get { return latest.PeakGyro; } }
    public int Samples { get { return latest.Samples; } }

    public WatchImuTracker()
    {
        Reset();
    }

    public void Reset()
    {
        ringHead = 0;
        ringCount = 0;
        hasPacket = false;
        lastPacketTime = double.NaN;
        Received = 0;
        latest = new WatchImuPacket();
    }

    public void Feed(WatchImuPacket packet, double nowSeconds)
    {
        if (!Finite(nowSeconds))
        {
            return;
        }
        if (!Finite(packet.Ax) || !Finite(packet.Ay) || !Finite(packet.Az) ||
            !Finite(packet.Gx) || !Finite(packet.Gy) || !Finite(packet.Gz) ||
            !Finite(packet.PeakAccel) || !Finite(packet.PeakGyro) ||
            packet.PeakAccel < 0.0 || packet.PeakGyro < 0.0 || packet.Samples < 1)
        {
            return;
        }

        Received++;
        hasPacket = true;
        lastPacketTime = nowSeconds;
        latest = packet;

        sampleTimes[ringHead] = nowSeconds;
        samplePeakAccels[ringHead] = packet.PeakAccel;
        ringHead = (ringHead + 1) % RingCapacity;
        if (ringCount < RingCapacity)
        {
            ringCount++;
        }
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

    public double Motion(double now)
    {
        if (!HasSignal(now))
        {
            return 0.0;
        }

        double sum = 0.0;
        int count = 0;
        for (int i = 0; i < ringCount; i++)
        {
            double age = now - sampleTimes[i];
            if (age >= 0.0 && age <= MotionWindowSeconds)
            {
                sum += Math.Abs(samplePeakAccels[i] - GravityMetersPerSecondSq);
                count++;
            }
        }
        if (count == 0)
        {
            return 0.0;
        }

        double normalized = (sum / count) / MotionFullScale;
        if (normalized <= 0.0)
        {
            return 0.0;
        }
        if (normalized >= 1.0)
        {
            return 1.0;
        }
        return normalized;
    }

    static bool Finite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
