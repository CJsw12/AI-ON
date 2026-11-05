using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Reads iPhone eye tracking from the VTube Studio desktop API after its USB link is established.</summary>
public sealed class VTubeStudioUsbApiBridge : MonoBehaviour, IIPhoneGazeSource
{
    const string PluginName = "AI-ON Gaze Test";
    const string PluginDeveloper = "AI-ON";
    const int PollIntervalMs = 33;
    const int FaceCheckIntervalMs = 330;

    sealed class EyeSample
    {
        public Vector2 Feature;
        public Vector2 Head;
        public float Blink;
        public bool FaceFound;
    }

    readonly ConcurrentQueue<EyeSample> samples = new ConcurrentQueue<EyeSample>();
    ClientWebSocket socket;
    CancellationTokenSource cancellation;
    Task worker;
    volatile string status = "VTube Studio PC 앱 연결을 기다리고 있어요.";
    volatile string error;
    string apiToken;

    public Vector2 EyeFeature { get; private set; }
    public Vector2 HeadFeature { get; private set; }
    public Vector2 PositionFeature => Vector2.zero;   // The desktop API path has no head position yet.
    public float Blink { get; private set; } = -1f;
    public Vector2 LookFeature => Vector2.zero;   // the desktop API exposes no ARKit look blendshapes
    public bool FaceFound { get; private set; }
    public bool HasFreshSample { get; private set; }
    public int Sequence { get; private set; }
    public string Status => error ?? status;
    public string ApiToken => apiToken;

    public bool StartConnecting(int port, string rememberedToken)
    {
        StopReceiving();
        if (port < 1 || port > 65535)
        {
            error = "VTube Studio API 포트가 올바르지 않아요.";
            return false;
        }
        try
        {
            apiToken = rememberedToken;
            cancellation = new CancellationTokenSource();
            socket = new ClientWebSocket();
            error = null;
            status = "VTube Studio PC 앱에 연결 중이에요...";
            worker = Run(socket, port, cancellation.Token);
            return true;
        }
        catch (Exception e)
        {
            error = "VTube Studio API 연결을 시작하지 못했어요: " + e.Message;
            StopReceiving();
            return false;
        }
    }

    async Task Run(ClientWebSocket activeSocket, int port, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            await activeSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port), token);
            // A remembered token can be revoked in VTube Studio; drop it and ask for a new one once.
            for (int attempt = 0; ; attempt++)
            {
                bool remembered = !string.IsNullOrEmpty(apiToken);
                if (!remembered)
                {
                    status = "VTube Studio에서 AI-ON의 API 접근을 승인해 주세요.";
                    string requestId = Guid.NewGuid().ToString("N");
                    JObject tokenResponse = await SendAndReceive(activeSocket, requestId, new JObject
                    {
                        ["apiName"] = "VTubeStudioPublicAPI",
                        ["apiVersion"] = "1.0",
                        ["requestID"] = requestId,
                        ["messageType"] = "AuthenticationTokenRequest",
                        ["data"] = new JObject { ["pluginName"] = PluginName, ["pluginDeveloper"] = PluginDeveloper }
                    }, token);
                    apiToken = tokenResponse["data"]?["authenticationToken"]?.Value<string>();
                    if (string.IsNullOrEmpty(apiToken)) throw new InvalidOperationException("VTube Studio에서 API 인증 토큰을 받지 못했어요.");
                }

                status = "VTube Studio API 인증 중이에요...";
                string authId = Guid.NewGuid().ToString("N");
                JObject authResponse = await SendAndReceive(activeSocket, authId, new JObject
                {
                    ["apiName"] = "VTubeStudioPublicAPI",
                    ["apiVersion"] = "1.0",
                    ["requestID"] = authId,
                    ["messageType"] = "AuthenticationRequest",
                    ["data"] = new JObject
                    {
                        ["pluginName"] = PluginName,
                        ["pluginDeveloper"] = PluginDeveloper,
                        ["authenticationToken"] = apiToken
                    }
                }, token);
                if (authResponse["data"]?["authenticated"]?.Value<bool>() == true) break;
                apiToken = null;   // never resend a rejected token
                if (!remembered || attempt > 0)
                    throw new InvalidOperationException("VTube Studio가 API 연결을 허용하지 않았어요. 다시 연결하면 새 승인을 요청해요.");
            }

            status = "USB 추적 연결됨 · 눈 파라미터를 기다리는 중이에요.";
            long nextFaceCheck = 0;
            bool faceFound = false;
            while (!token.IsCancellationRequested && activeSocket.State == WebSocketState.Open)
            {
                long now = clock.ElapsedMilliseconds;
                if (now >= nextFaceCheck)
                {
                    string faceId = Guid.NewGuid().ToString("N");
                    JObject faceResponse = await SendAndReceive(activeSocket, faceId, Request(faceId, "FaceFoundRequest"), token);
                    faceFound = faceResponse["data"]?["found"]?.Value<bool>() == true;
                    nextFaceCheck = clock.ElapsedMilliseconds + FaceCheckIntervalMs;
                }

                string paramsId = Guid.NewGuid().ToString("N");
                JObject paramsResponse = await SendAndReceive(activeSocket, paramsId, Request(paramsId, "InputParameterListRequest"), token);
                if (TryReadEyeFeature(paramsResponse, out Vector2 feature, out Vector2 head, out float blink))
                {
                    while (samples.Count >= 120 && samples.TryDequeue(out _)) { }
                    samples.Enqueue(new EyeSample { Feature = feature, Head = head, Blink = blink, FaceFound = faceFound });
                    status = faceFound ? "USB 추적 중 · 화면 보정이 필요해요." : "VTube Studio에서 얼굴을 찾지 못했어요.";
                }
                else status = "VTube Studio에서 EyeLeft/Right X/Y 값을 찾을 수 없어요.";

                await Task.Delay(PollIntervalMs, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested) error = "VTube Studio API 오류: " + e.Message;
        }
        finally
        {
            HasFreshSample = false;
            FaceFound = false;
            activeSocket.Dispose();
        }
    }

    static JObject Request(string requestId, string messageType) => new JObject
    {
        ["apiName"] = "VTubeStudioPublicAPI",
        ["apiVersion"] = "1.0",
        ["requestID"] = requestId,
        ["messageType"] = messageType
    };

    static bool TryReadEyeFeature(JObject response, out Vector2 feature, out Vector2 head, out float blink)
    {
        feature = head = default;
        blink = -1f;
        JArray parameters = response["data"]?["defaultParameters"] as JArray;
        if (parameters == null) return false;
        float leftX = float.NaN, leftY = float.NaN, rightX = float.NaN, rightY = float.NaN;
        float angleX = 0f, angleY = 0f, openLeft = float.NaN, openRight = float.NaN;
        foreach (JToken parameter in parameters)
        {
            string name = parameter["name"]?.Value<string>();
            float value = parameter["value"]?.Value<float>() ?? float.NaN;
            if (name == "EyeLeftX") leftX = value;
            else if (name == "EyeLeftY") leftY = value;
            else if (name == "EyeRightX") rightX = value;
            else if (name == "EyeRightY") rightY = value;
            else if (name == "FaceAngleX") angleX = value;
            else if (name == "FaceAngleY") angleY = value;
            else if (name == "EyeOpenLeft") openLeft = value;
            else if (name == "EyeOpenRight") openRight = value;
        }
        if (float.IsNaN(leftX) || float.IsNaN(leftY) || float.IsNaN(rightX) || float.IsNaN(rightY)) return false;
        feature = new Vector2((leftX + rightX) * .5f, (leftY + rightY) * .5f);
        head = new Vector2(angleX, angleY);
        if (!float.IsNaN(openLeft) && !float.IsNaN(openRight)) blink = Mathf.Clamp01(1f - Mathf.Min(openLeft, openRight));
        return true;
    }

    static async Task<JObject> SendAndReceive(ClientWebSocket activeSocket, string requestId, JObject request, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(request.ToString(Newtonsoft.Json.Formatting.None));
        await activeSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        while (true)
        {
            string message = await ReceiveText(activeSocket, token);
            JObject response = JObject.Parse(message);
            if (response["requestID"]?.Value<string>() == requestId) return response;
        }
    }

    static async Task<string> ReceiveText(ClientWebSocket activeSocket, CancellationToken token)
    {
        using (var stream = new MemoryStream())
        {
            byte[] buffer = new byte[8192];
            WebSocketReceiveResult result;
            do
            {
                result = await activeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("VTube Studio가 API 연결을 닫았어요.");
                stream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public void Poll()
    {
        EyeSample latest = null;
        while (samples.TryDequeue(out EyeSample sample)) latest = sample;
        if (latest == null) return;
        EyeFeature = latest.Feature;
        HeadFeature = latest.Head;
        Blink = latest.Blink;
        FaceFound = latest.FaceFound;
        HasFreshSample = true;
        Sequence++;
        lastSampleAt = Time.unscaledTime;
    }

    float lastSampleAt = -100f;

    public void StopReceiving()
    {
        HasFreshSample = false;
        FaceFound = false;
        if (cancellation != null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }
        if (socket != null)
        {
            socket.Abort();
            socket = null;
        }
        worker = null;
        while (samples.TryDequeue(out _)) { }
        lastSampleAt = -100f;
    }

    void Update()
    {
        if (HasFreshSample && Time.unscaledTime - lastSampleAt > 1f) HasFreshSample = false;
    }

    void OnDestroy() => StopReceiving();
    void OnApplicationQuit() => StopReceiving();
}
