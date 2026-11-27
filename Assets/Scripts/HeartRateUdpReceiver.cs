using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>Receives Wear OS heart-rate and IMU UDP packets on a background thread and feeds trackers on Poll.</summary>
public sealed class HeartRateUdpReceiver : IDisposable
{
    public const int DefaultPort = 11126;
    const int MaxQueueSize = 100;
    const int ReceivePollMilliseconds = 250;

    readonly int port;
    readonly HeartRateTracker tracker = new HeartRateTracker();
    readonly WatchImuTracker imu = new WatchImuTracker();
    readonly HeartRateDiagnostics diagnostics = new HeartRateDiagnostics();
    readonly ConcurrentQueue<string> hrPackets = new ConcurrentQueue<string>();
    readonly ConcurrentQueue<string> imuPackets = new ConcurrentQueue<string>();
    readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();

    UdpClient socket;
    Thread receiveThread;
    volatile bool stopping;
    volatile bool running;
    bool disposed;
    bool hasLastSummaryLog;
    double lastSummaryLogTime;

    public Action<string> Log;

    public HeartRateTracker Tracker { get { return tracker; } }
    public WatchImuTracker Imu { get { return imu; } }
    public HeartRateDiagnostics Diagnostics { get { return diagnostics; } }
    public long ImuRejected { get; private set; }
    public string Error { get; private set; }
    public bool Running { get { return running; } }
    public int Port { get { return port; } }

    public HeartRateUdpReceiver(int port = DefaultPort)
    {
        this.port = port;
    }

    public bool Start()
    {
        if (disposed)
        {
            Error = "심박 수신기가 이미 종료되었어요.";
            running = false;
            return false;
        }

        StopInternal();

        if (port < 1 || port > 65535)
        {
            Error = "심박 UDP 포트가 올바르지 않아요: " + port;
            running = false;
            return false;
        }

        UdpClient activeSocket = null;
        try
        {
            activeSocket = new UdpClient();
            activeSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            activeSocket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            activeSocket.Client.ReceiveTimeout = ReceivePollMilliseconds;

            socket = activeSocket;
            stopping = false;
            running = true;
            Error = null;

            receiveThread = new Thread(delegate() { ReceiveLoop(activeSocket); })
            {
                IsBackground = true,
                Name = "AI-ON HeartRate UDP"
            };
            receiveThread.Start();
            return true;
        }
        catch (SocketException e)
        {
            CloseQuietly(activeSocket);
            socket = null;
            running = false;
            Error = e.SocketErrorCode == SocketError.AddressAlreadyInUse || e.SocketErrorCode == SocketError.AccessDenied
                ? "심박 UDP 포트 " + port + "을(를) 열 수 없어요. 다른 프로그램이 사용 중이거나 권한이 없어요 (" + e.SocketErrorCode + ")."
                : "심박 UDP 포트 " + port + " 바인드에 실패했어요: " + e.Message;
            return false;
        }
        catch (Exception e)
        {
            CloseQuietly(activeSocket);
            socket = null;
            running = false;
            Error = "심박 UDP 수신기를 시작하지 못했어요: " + e.Message;
            return false;
        }
    }

    public void Poll(double nowSeconds)
    {
        string err;
        while (errors.TryDequeue(out err))
        {
            Error = "심박 UDP 수신 오류: " + err;
        }

        string raw;
        while (hrPackets.TryDequeue(out raw))
        {
            HeartRatePacket packet;
            string reason;
            if (HeartRatePacket.TryParse(raw, out packet, out reason))
            {
                tracker.Feed(packet, nowSeconds);
                diagnostics.OnHeartRate(packet, nowSeconds);
            }
            else
            {
                tracker.RejectBadPacket();
                diagnostics.OnRejected(reason, nowSeconds);
            }
        }

        while (imuPackets.TryDequeue(out raw))
        {
            WatchImuPacket imuPacket;
            string reason;
            if (WatchImuPacket.TryParse(raw, out imuPacket, out reason))
            {
                imu.Feed(imuPacket, nowSeconds);
                diagnostics.OnImu(imuPacket, nowSeconds);
            }
            else
            {
                ImuRejected++;
                diagnostics.OnRejected(reason, nowSeconds);
            }
        }

        Action<string> log = Log;
        diagnostics.FlushSignalEvents(nowSeconds, log);

        if (!double.IsNaN(nowSeconds) && !double.IsInfinity(nowSeconds))
        {
            if (!hasLastSummaryLog)
            {
                hasLastSummaryLog = true;
                lastSummaryLogTime = nowSeconds;
            }
            else if (nowSeconds - lastSummaryLogTime >= 1.0)
            {
                lastSummaryLogTime = nowSeconds;
                if (log != null)
                {
                    log(diagnostics.Summary(nowSeconds));
                }
            }
            else if (nowSeconds < lastSummaryLogTime)
            {
                lastSummaryLogTime = nowSeconds;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        StopInternal();
    }

    void ReceiveLoop(UdpClient activeSocket)
    {
        try
        {
            while (!stopping)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = activeSocket.Receive(ref sender);
                    if (bytes == null || bytes.Length == 0)
                    {
                        continue;
                    }
                    string text = Encoding.UTF8.GetString(bytes);
                    ConcurrentQueue<string> target = IsImuPayload(text) ? imuPackets : hrPackets;
                    string dropped;
                    while (target.Count >= MaxQueueSize && target.TryDequeue(out dropped)) { }
                    target.Enqueue(text);
                }
                catch (SocketException e)
                {
                    if (stopping)
                    {
                        break;
                    }
                    if (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock)
                    {
                        continue;
                    }
                    errors.Enqueue(e.Message);
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (!stopping)
                    {
                        errors.Enqueue(e.Message);
                    }
                    break;
                }
            }
        }
        finally
        {
            running = false;
        }
    }

    internal static bool IsImuPayload(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }
        if (raw.IndexOf("\"k\":\"imu\"", StringComparison.Ordinal) >= 0)
        {
            return true;
        }
        int searchFrom = 0;
        while (searchFrom < raw.Length)
        {
            int keyIndex = raw.IndexOf("\"k\"", searchFrom, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return false;
            }
            int pos = keyIndex + 3;
            HeartRatePacket.SkipWhitespace(raw, ref pos);
            if (pos < raw.Length && raw[pos] == ':')
            {
                pos++;
                HeartRatePacket.SkipWhitespace(raw, ref pos);
                if (pos + 5 <= raw.Length &&
                    raw[pos] == '"' &&
                    raw[pos + 1] == 'i' &&
                    raw[pos + 2] == 'm' &&
                    raw[pos + 3] == 'u' &&
                    raw[pos + 4] == '"')
                {
                    return true;
                }
            }
            searchFrom = keyIndex + 3;
        }
        return false;
    }

    void StopInternal()
    {
        stopping = true;
        running = false;
        hasLastSummaryLog = false;

        UdpClient activeSocket = socket;
        socket = null;
        CloseQuietly(activeSocket);

        Thread thread = receiveThread;
        receiveThread = null;
        if (thread != null && thread.IsAlive && Thread.CurrentThread != thread)
        {
            thread.Join(500);
        }

        string dropped;
        while (hrPackets.TryDequeue(out dropped)) { }
        while (imuPackets.TryDequeue(out dropped)) { }
        while (errors.TryDequeue(out dropped)) { }
    }

    static void CloseQuietly(UdpClient client)
    {
        if (client == null)
        {
            return;
        }
        try
        {
            client.Close();
        }
        catch (Exception)
        {
        }
    }
}
