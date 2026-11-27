using System;
using System.Globalization;
using System.Text;

/// <summary>Tracks per-stream packet rate, gaps, sequence loss, and restarts without per-frame allocations.</summary>
public sealed class WatchStreamDiagnostics
{
    public const int RateRingCapacity = 256;
    const double WindowSeconds = 1.0;
    const double Epsilon = 1e-9;

    readonly double[] arrivalTimes = new double[RateRingCapacity];
    int ringHead;
    int ringCount;
    bool hasLast;
    double lastTime;
    long lastSeq;

    public long Received { get; private set; }
    public long Lost { get; private set; }
    public long Restarts { get; private set; }
    public double MaxGapSeconds { get; private set; }

    public WatchStreamDiagnostics()
    {
        Reset();
    }

    public void Reset()
    {
        ringHead = 0;
        ringCount = 0;
        hasLast = false;
        lastTime = double.NaN;
        lastSeq = 0;
        Received = 0;
        Lost = 0;
        Restarts = 0;
        MaxGapSeconds = 0.0;
    }

    public void Record(long seq, double nowSeconds)
    {
        if (!Finite(nowSeconds))
        {
            return;
        }

        if (hasLast)
        {
            double gap = nowSeconds - lastTime;
            if (gap > MaxGapSeconds)
            {
                MaxGapSeconds = gap;
            }

            if (seq < lastSeq)
            {
                Restarts++;
            }
            else if (lastSeq < long.MaxValue && seq > lastSeq + 1)
            {
                Lost += (seq - lastSeq - 1);
            }
        }

        hasLast = true;
        lastTime = nowSeconds;
        lastSeq = seq;
        Received++;

        arrivalTimes[ringHead] = nowSeconds;
        ringHead = (ringHead + 1) % RateRingCapacity;
        if (ringCount < RateRingCapacity)
        {
            ringCount++;
        }
    }

    public double PerSecond(double nowSeconds)
    {
        if (!Finite(nowSeconds) || ringCount == 0)
        {
            return 0.0;
        }

        int count = 0;
        for (int i = 0; i < ringCount; i++)
        {
            double age = nowSeconds - arrivalTimes[i];
            if (age >= -Epsilon && age < WindowSeconds - Epsilon)
            {
                count++;
            }
        }
        return count;
    }

    public double SecondsSinceLast(double nowSeconds)
    {
        if (!hasLast || !Finite(nowSeconds) || !Finite(lastTime))
        {
            return double.NaN;
        }
        double elapsed = nowSeconds - lastTime;
        return elapsed < 0.0 ? 0.0 : elapsed;
    }

    static bool Finite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

/// <summary>Collects real-time Wear OS HR/IMU reception diagnostics, rejection reasons, and signal loss/regain transitions.</summary>
public sealed class HeartRateDiagnostics
{
    public const double SignalLostTimeoutSeconds = 5.0;
    public const string EventLost = "hr signal lost";
    public const string EventRegained = "hr signal regained";

    readonly WatchStreamDiagnostics hr = new WatchStreamDiagnostics();
    readonly WatchStreamDiagnostics imu = new WatchStreamDiagnostics();
    readonly StringBuilder summaryBuilder = new StringBuilder(160);

    bool hasValidHr;
    double lastValidHrTime;
    bool hrSignalLost;
    bool pendingLost;
    bool pendingRegained;

    public WatchStreamDiagnostics Hr { get { return hr; } }
    public WatchStreamDiagnostics Imu { get { return imu; } }

    public long UnavailableCount { get; private set; }
    public double UnavailableRatio
    {
        get { return hr.Received > 0 ? (double)UnavailableCount / hr.Received : 0.0; }
    }

    public long Restarts { get { return hr.Restarts + imu.Restarts; } }

    public long BadJson { get; private set; }
    public long BadVersion { get; private set; }
    public long BadBpmRange { get; private set; }
    public long BadFields { get; private set; }
    public long TooLong { get; private set; }
    public long Other { get; private set; }
    public long RejectedTotal
    {
        get { return BadJson + BadVersion + BadBpmRange + BadFields + TooLong + Other; }
    }

    public bool IsHeartRateLost { get { return hrSignalLost; } }
    public long LostEvents { get; private set; }
    public long RegainedEvents { get; private set; }

    public HeartRateDiagnostics()
    {
        Reset();
    }

    public void Reset()
    {
        hr.Reset();
        imu.Reset();
        hasValidHr = false;
        lastValidHrTime = double.NaN;
        hrSignalLost = false;
        pendingLost = false;
        pendingRegained = false;
        UnavailableCount = 0;
        BadJson = 0;
        BadVersion = 0;
        BadBpmRange = 0;
        BadFields = 0;
        TooLong = 0;
        Other = 0;
        LostEvents = 0;
        RegainedEvents = 0;
    }

    public void OnHeartRate(HeartRatePacket packet, double nowSeconds)
    {
        if (!Finite(nowSeconds))
        {
            return;
        }

        hr.Record(packet.Seq, nowSeconds);

        if (hasValidHr && !hrSignalLost && (nowSeconds - lastValidHrTime) > SignalLostTimeoutSeconds)
        {
            hrSignalLost = true;
            LostEvents++;
            pendingLost = true;
        }

        if (!packet.Ok)
        {
            UnavailableCount++;
            return;
        }

        if (hrSignalLost)
        {
            hrSignalLost = false;
            RegainedEvents++;
            pendingRegained = true;
        }

        hasValidHr = true;
        lastValidHrTime = nowSeconds;
    }

    public void OnImu(WatchImuPacket packet, double nowSeconds)
    {
        if (!Finite(nowSeconds))
        {
            return;
        }

        imu.Record(packet.Seq, nowSeconds);
    }

    public void OnRejected(string reasonCode, double nowSeconds)
    {
        if (reasonCode == HeartRatePacket.ReasonBadJson || reasonCode == "BadJson")
        {
            BadJson++;
        }
        else if (reasonCode == HeartRatePacket.ReasonBadVersion || reasonCode == "BadVersion")
        {
            BadVersion++;
        }
        else if (reasonCode == HeartRatePacket.ReasonBadBpmRange || reasonCode == "BadBpmRange")
        {
            BadBpmRange++;
        }
        else if (reasonCode == HeartRatePacket.ReasonBadFields || reasonCode == "BadFields")
        {
            BadFields++;
        }
        else if (reasonCode == HeartRatePacket.ReasonTooLong || reasonCode == "TooLong")
        {
            TooLong++;
        }
        else
        {
            Other++;
        }
    }

    public void FlushSignalEvents(double nowSeconds, Action<string> log)
    {
        if (Finite(nowSeconds) && hasValidHr && !hrSignalLost && (nowSeconds - lastValidHrTime) > SignalLostTimeoutSeconds)
        {
            hrSignalLost = true;
            LostEvents++;
            pendingLost = true;
        }

        if (pendingLost)
        {
            pendingLost = false;
            if (log != null)
            {
                log(EventLost);
            }
        }

        if (pendingRegained)
        {
            pendingRegained = false;
            if (log != null)
            {
                log(EventRegained);
            }
        }
    }

    public string Summary(double nowSeconds)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        summaryBuilder.Length = 0;

        summaryBuilder.Append("hr ");
        summaryBuilder.Append(hr.PerSecond(nowSeconds).ToString("0.0", inv));
        summaryBuilder.Append("/s total=");
        summaryBuilder.Append(hr.Received);
        summaryBuilder.Append(" lost=");
        summaryBuilder.Append(hr.Lost);
        summaryBuilder.Append(" maxgap=");
        summaryBuilder.Append(hr.MaxGapSeconds.ToString("0.0", inv));
        summaryBuilder.Append("s unavail=");
        summaryBuilder.Append(UnavailableCount);

        summaryBuilder.Append(" | imu ");
        summaryBuilder.Append(imu.PerSecond(nowSeconds).ToString("0.0", inv));
        summaryBuilder.Append("/s total=");
        summaryBuilder.Append(imu.Received);
        summaryBuilder.Append(" lost=");
        summaryBuilder.Append(imu.Lost);

        long rejected = RejectedTotal;
        summaryBuilder.Append(" | rejected=");
        summaryBuilder.Append(rejected);
        if (rejected > 0)
        {
            summaryBuilder.Append('(');
            bool first = true;
            AppendReason(ref first, HeartRatePacket.ReasonBadJson, BadJson);
            AppendReason(ref first, HeartRatePacket.ReasonBadVersion, BadVersion);
            AppendReason(ref first, HeartRatePacket.ReasonBadBpmRange, BadBpmRange);
            AppendReason(ref first, HeartRatePacket.ReasonBadFields, BadFields);
            AppendReason(ref first, HeartRatePacket.ReasonTooLong, TooLong);
            AppendReason(ref first, HeartRatePacket.ReasonOther, Other);
            summaryBuilder.Append(')');
        }

        summaryBuilder.Append(" restarts=");
        summaryBuilder.Append(Restarts);

        return summaryBuilder.ToString();
    }

    void AppendReason(ref bool first, string label, long count)
    {
        if (count <= 0)
        {
            return;
        }
        if (!first)
        {
            summaryBuilder.Append(',');
        }
        first = false;
        summaryBuilder.Append(label);
        summaryBuilder.Append('=');
        summaryBuilder.Append(count);
    }

    static bool Finite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
