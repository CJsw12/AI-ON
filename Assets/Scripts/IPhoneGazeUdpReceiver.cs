using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>Receives VTube Studio iOS tracking packets without blocking Unity's main thread.</summary>
public sealed class IPhoneGazeUdpReceiver : MonoBehaviour, IIPhoneGazeSource
{
    const int LocalPort = 11125;
    const float RequestDurationSeconds = 2.5f;
    const int ReceivePollMilliseconds = 100;

    [Serializable]
    sealed class TrackingRequest
    {
        public string messageType = "iOSTrackingDataRequest";
        public float time = RequestDurationSeconds;
        public string sentBy = "AI-ON";
        public int[] ports = { LocalPort };
    }

    [Serializable]
    public sealed class TrackingData
    {
        public long Timestamp;
        public bool FaceFound;
        public Vector3 Rotation;
        public Vector3 Position;   // VTube Studio sends the face position next to its rotation.
        public Vector3 EyeLeft;
        public Vector3 EyeRight;
        public BlendShape[] BlendShapes;
    }

    [Serializable]
    public sealed class BlendShape
    {
        public string k;
        public float v;
    }

    readonly ConcurrentQueue<string> packets = new ConcurrentQueue<string>();
    readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
    UdpClient socket;
    Thread receiveThread;
    volatile bool stopping;
    public TrackingData Latest { get; private set; }
    public int Sequence { get; private set; }
    public int ReceivedPackets { get; private set; }
    public int RejectedPackets { get; private set; }
    public float LastPacketAt { get; private set; } = -100f;
    float startedAt;
    public string Error { get; private set; }
    public Vector2 EyeFeature => Latest == null ? Vector2.zero : new Vector2((Latest.EyeLeft.y + Latest.EyeRight.y) * .5f, (Latest.EyeLeft.x + Latest.EyeRight.x) * .5f);
    public Vector2 HeadFeature => Latest == null ? Vector2.zero : new Vector2(Latest.Rotation.y, Latest.Rotation.x);
    public Vector2 PositionFeature => Latest == null ? Vector2.zero : new Vector2(Latest.Position.x, Latest.Position.y);
    public float Blink { get; private set; } = -1f;
    public Vector2 LookFeature { get; private set; }
    public bool FaceFound => Latest != null && Latest.FaceFound;
    public bool HasFreshSample => HasFreshPacket;
    public string Status => Error ?? (Latest != null
        ? null
        : Time.unscaledTime - startedAt < 5f
            ? "아이폰 추적 데이터를 기다리는 중이에요."
            : ReceivedPackets == 0
                ? "요청은 보냈지만 UDP 데이터가 한 건도 안 왔어요. PC 네트워크 경로/방화벽을 확인해 주세요."
                : $"UDP 패킷 {ReceivedPackets}건을 받았지만 유효한 추적 데이터가 없어요. 거부 {RejectedPackets}건.");

    public bool StartReceiving(IPAddress phoneAddress, int phonePort)
    {
        StopReceiving();
        if (phoneAddress == null || phonePort < 1 || phonePort > 65535)
        {
            Error = "아이폰 IP 또는 포트가 올바르지 않아요.";
            return false;
        }

        try
        {
            UdpClient activeSocket = new UdpClient(new IPEndPoint(IPAddress.Any, LocalPort));
            activeSocket.Client.ReceiveTimeout = ReceivePollMilliseconds;
            socket = activeSocket;
            byte[] request = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new TrackingRequest()));
            IPEndPoint destination = new IPEndPoint(phoneAddress, phonePort);
            stopping = false;
            startedAt = Time.unscaledTime;
            receiveThread = new Thread(() => ReceiveLoop(activeSocket, destination, request))
            {
                IsBackground = true,
                Name = "AI-ON iPhone gaze UDP"
            };
            receiveThread.Start();
            Error = null;
            return true;
        }
        catch (Exception e)
        {
            Error = e is SocketException
                ? "UDP 포트 11125를 열 수 없어요. 다른 프로그램이 사용 중인지 확인해 주세요."
                : "아이폰 추적 연결을 시작하지 못했어요.";
            StopReceiving();
            return false;
        }
    }

    void ReceiveLoop(UdpClient activeSocket, IPEndPoint destination, byte[] request)
    {
        DateTime nextRequest = DateTime.MinValue;
        while (!stopping)
        {
            try
            {
                if (DateTime.UtcNow >= nextRequest)
                {
                    activeSocket.Send(request, request.Length, destination);
                    nextRequest = DateTime.UtcNow.AddSeconds(1);
                }

                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = activeSocket.Receive(ref sender);
                Interlocked.Increment(ref _receivedPackets);
                if (bytes.Length > 0 && sender.Address.Equals(destination.Address))
                {
                    while (packets.Count >= 120 && packets.TryDequeue(out _)) { }
                    packets.Enqueue(Encoding.UTF8.GetString(bytes));
                }
                else Interlocked.Increment(ref _rejectedPackets);
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock)
            {
                // The timeout lets the loop send keepalive requests and notice shutdown.
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception e)
            {
                if (!stopping) errors.Enqueue(e.Message);
                break;
            }
        }
    }

    int _receivedPackets, _rejectedPackets;

    /// <summary>Drain and deserialize packets on the Unity main thread.</summary>
    public void Poll()
    {
        ReceivedPackets = Volatile.Read(ref _receivedPackets);
        RejectedPackets = Volatile.Read(ref _rejectedPackets);
        string latestJson = null;
        while (packets.TryDequeue(out string packet)) latestJson = packet;
        while (errors.TryDequeue(out string error)) Error = "UDP 수신 오류: " + error;
        if (latestJson == null) return;

        try
        {
            TrackingData data = JsonUtility.FromJson<TrackingData>(latestJson);
            if (data == null || data.Timestamp <= 0)
            {
                Error = "UDP 패킷은 도착했지만 추적 JSON의 Timestamp를 읽지 못했어요.";
                return;
            }
            Latest = data;
            Blink = ReadBlink(data.BlendShapes);
            LookFeature = ReadLook(data.BlendShapes);
            LastPacketAt = Time.unscaledTime;
            Sequence++;
            Error = null;
        }
        catch (Exception e)
        {
            Error = "아이폰 추적 데이터 형식을 읽지 못했어요: " + e.Message;
        }
    }

    static float ReadBlink(BlendShape[] shapes)
    {
        if (shapes == null) return -1f;
        float blink = -1f;
        foreach (BlendShape shape in shapes)
            if (shape != null && (shape.k == "EyeBlinkLeft" || shape.k == "EyeBlinkRight")) blink = Mathf.Max(blink, shape.v);
        return blink;
    }

    // Horizontal: the left eye looking "out" and the right eye looking "in" both mean looking to the user's left.
    static Vector2 ReadLook(BlendShape[] shapes)
    {
        if (shapes == null) return Vector2.zero;
        float inL = 0, outL = 0, inR = 0, outR = 0, upL = 0, upR = 0, downL = 0, downR = 0;
        foreach (BlendShape shape in shapes)
        {
            if (shape == null) continue;
            switch (shape.k)
            {
                case "EyeLookInLeft": inL = shape.v; break;
                case "EyeLookOutLeft": outL = shape.v; break;
                case "EyeLookInRight": inR = shape.v; break;
                case "EyeLookOutRight": outR = shape.v; break;
                case "EyeLookUpLeft": upL = shape.v; break;
                case "EyeLookUpRight": upR = shape.v; break;
                case "EyeLookDownLeft": downL = shape.v; break;
                case "EyeLookDownRight": downR = shape.v; break;
            }
        }
        return new Vector2((outL - inL + inR - outR) * .5f, (upL + upR - downL - downR) * .5f);
    }

    public bool HasFreshPacket => Latest != null && Time.unscaledTime - LastPacketAt <= 1f;

    public void StopReceiving()
    {
        stopping = true;
        if (socket != null)
        {
            socket.Close();
            socket = null;
        }
        if (receiveThread != null)
        {
            if (receiveThread.IsAlive && Thread.CurrentThread != receiveThread) receiveThread.Join(250);
            receiveThread = null;
        }
        while (packets.TryDequeue(out _)) { }
        while (errors.TryDequeue(out _)) { }
        Latest = null;
        Blink = -1f;
        LookFeature = Vector2.zero;
        LastPacketAt = -100f;
        Sequence = 0;
        Interlocked.Exchange(ref _receivedPackets, 0);
        Interlocked.Exchange(ref _rejectedPackets, 0);
        ReceivedPackets = RejectedPackets = 0;
        startedAt = Time.unscaledTime;
        Error = null;
    }

    void OnDestroy() => StopReceiving();
    void OnApplicationQuit() => StopReceiving();
}
