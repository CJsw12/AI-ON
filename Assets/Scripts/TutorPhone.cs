#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// A short-lived LAN companion. Network threads only read snapshots and queue input.
sealed class TutorPhone : IDisposable
{
    readonly TcpListener listener;
    readonly string token = Guid.NewGuid().ToString("N");
    readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
    volatile string snapshot = "";
    volatile bool closed;
    public string Address { get; }

    public TutorPhone(string ip)
    {
        listener = new TcpListener(IPAddress.Parse(ip), 0);
        listener.Start(4);
        Address = $"http://{ip}:{((IPEndPoint)listener.LocalEndpoint).Port}/{token}/";
        Task.Run(Serve);
    }

    public void Publish(string text) => snapshot = text;
    public bool TryRead(out string question) => queue.TryDequeue(out question);

    void Serve()
    {
        while (!closed)
        {
            try
            {
                using (var client = listener.AcceptTcpClient())
                {
                    client.ReceiveTimeout = 1000;
                    client.SendTimeout = 1000;
                    using (var stream = client.GetStream())
                    {
                        string request = ReadLine(stream);
                        for (int i = 0; i < 30 && ReadLine(stream).Length != 0; i++) { }
                        string[] parts = request.Split(' ');
                        string path = parts.Length == 3 && parts[0] == "GET" ? parts[1] : "";
                        string root = "/" + token + "/";
                        if (path == root + "state") Reply(stream, "200 OK", "text/plain", snapshot);
                        else if (path == root) Reply(stream, "200 OK", "text/html", Page);
                        else if (path.StartsWith(root + "ask?q=", StringComparison.Ordinal) && path.Length < 1700)
                        {
                            string input = WebUtility.UrlDecode(path.Substring((root + "ask?q=").Length)).Trim();
                            if (input.Length > 0 && input.Length <= 160 && queue.Count < 4) queue.Enqueue(input);
                            Reply(stream, "200 OK", "text/plain", "");
                        }
                        else Reply(stream, "404 Not Found", "text/plain", "");
                    }
                }
            }
            catch (SocketException) { if (!closed) Thread.Sleep(50); }
            catch (IOException) { }
            catch (ObjectDisposedException) { return; }
        }
    }

    static string ReadLine(Stream stream)
    {
        var line = new StringBuilder();
        while (line.Length < 2048)
        {
            int ch = stream.ReadByte();
            if (ch < 0 || ch == 10) return line.ToString();
            if (ch != 13) line.Append((char)ch);
        }
        throw new IOException("Request line too long");
    }

    static void Reply(Stream stream, string status, string type, string body)
    {
        byte[] payload = Encoding.UTF8.GetBytes(body);
        byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {type}; charset=utf-8\r\nContent-Length: {payload.Length}\r\nConnection: close\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\n\r\n");
        stream.Write(header, 0, header.Length);
        stream.Write(payload, 0, payload.Length);
    }

    public void Dispose()
    {
        closed = true;
        listener.Stop();
    }

    const string Page = @"<!doctype html><html lang='ko'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>두근두근 캠퍼스 · AI 조교</title><style>body{background:#fff5f8;color:#432439;font:17px system-ui;margin:24px auto;padding:0 18px;max-width:540px}pre{white-space:pre-wrap;background:white;padding:20px;border-radius:16px;font:inherit;min-height:130px}input,button{box-sizing:border-box;font:inherit;padding:12px;border-radius:10px;border:1px solid #dc9ab4}input{width:100%;margin-bottom:10px}button{background:#ffd4e4;color:#432439;margin-right:5px}</style>
<h1>AI 조교</h1><pre id='out'>실습 화면 연결 중</pre>
<form><label for='q'>조교에게 질문하기</label><input id='q' maxlength='160' required><button>질문 보내기</button><button type='button' id='next'>다음 시범 단계</button></form>
<script>
const out=document.querySelector('#out'), q=document.querySelector('#q');
async function send(v){try{await fetch('ask?q='+encodeURIComponent(v));}catch{out.textContent='PC 연결을 확인해 주세요.';}}
document.querySelector('form').onsubmit=e=>{e.preventDefault();send(q.value);q.value='';};
document.querySelector('#next').onclick=()=>send('/next');
async function refresh(){try{const r=await fetch('state');out.textContent=await r.text();}catch{out.textContent='PC 연결을 확인해 주세요.';}setTimeout(refresh,1000);}
refresh();
</script></html>";
}
#endif