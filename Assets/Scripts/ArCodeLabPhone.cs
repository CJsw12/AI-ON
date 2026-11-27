#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Snapshot-only LAN bridge. It never calls Unity from a network thread.
sealed class ArCodeLabPhone : IDisposable
{
    readonly TcpListener http;
    TcpListener https;
    X509Certificate2 certificate;
    byte[] rootCertificate;
    readonly string token = Guid.NewGuid().ToString("N"), ip;
    readonly Dictionary<string, byte[]> assets = new Dictionary<string, byte[]>();
    readonly ConcurrentQueue<string> commands = new ConcurrentQueue<string>();
    readonly HashSet<string> seen = new HashSet<string>();
    readonly Queue<string> recent = new Queue<string>();
    readonly SemaphoreSlim slots = new SemaphoreSlim(8);
    volatile bool closed;
    volatile string state = "{}", secure = "";
    public string Address { get; private set; }
    public string LocalAddress { get; private set; }
    public string SecureAddress { get { return secure; } }
    public ArCodeLabPhone(string ip, string assetFolder)
    {
        this.ip = IPAddress.Parse(ip).ToString();
        foreach (string name in new[] { "index.html", "app.js", "three.min.js", "ar-threex.js", "camera_para.dat", "patt.hiro", "hiro.png" })
            assets[name] = File.ReadAllBytes(Path.Combine(assetFolder, name));
        http = new TcpListener(IPAddress.Any, 0);
        http.Start(8);
        int port = ((IPEndPoint)http.LocalEndpoint).Port;
        Address = "http://" + this.ip + ":" + port + "/" + token + "/";
        LocalAddress = "http://127.0.0.1:" + port + "/" + token + "/";
        Task.Run(() => Accept(http, false));
    }
    public void EnableTls(string folder)
    {
        if (closed || https != null) return;
        var loaded = new X509Certificate2(Path.Combine(folder,"server.pfx"), File.ReadAllText(Path.Combine(folder,"password.txt")), X509KeyStorageFlags.EphemeralKeySet);
        rootCertificate = File.ReadAllBytes(Path.Combine(folder,"root.cer"));
        var server = new TcpListener(IPAddress.Any,0);
        server.Start(8);
        certificate=loaded; https=server;
        secure="https://"+ip+":"+((IPEndPoint)https.LocalEndpoint).Port+"/"+token+"/";
        Task.Run(() => Accept(server,true));
    }
    public void Publish(string json) { state=json; }
    public bool TryRead(out string command) { return commands.TryDequeue(out command); }
    async Task Accept(TcpListener server, bool tls)
    {
        while(!closed)
        {
            try
            {
                var client=await server.AcceptTcpClientAsync().ConfigureAwait(false);
                if(!slots.Wait(0)) { client.Close(); continue; }
                _ = Task.Run(() => { try { Handle(client,tls); } finally { slots.Release(); } });
            }
            catch(SocketException) { if(closed) return; }
            catch(ObjectDisposedException) { return; }
        }
    }
    void Handle(TcpClient client,bool tls)
    {
        using(client)
        {
            try
            {
                client.ReceiveTimeout=4000; client.SendTimeout=4000;
                using(Stream raw=client.GetStream())
                {
                    if(tls)
                    {
                        using(var ssl=new SslStream(raw,false))
                        { ssl.AuthenticateAsServer(certificate,false,SslProtocols.Tls12,false); Route(ssl); }
                    }
                    else Route(raw);
                }
            }
            catch(IOException) { }
            catch(AuthenticationException) { }
            catch(ObjectDisposedException) { }
            catch(SocketException) { }
        }
    }
    void Route(Stream stream)
    {
        string request=ReadLine(stream);
        for(int i=0;i<40 && ReadLine(stream).Length>0;i++) { }
        var parts=request.Split(' ');
        string path=parts.Length==3 && parts[0]=="GET" ? parts[1] : "";
        string prefix="/"+token+"/";
        if(!path.StartsWith(prefix,StringComparison.Ordinal)) { Reply(stream,404,"text/plain",Encoding.UTF8.GetBytes("Not found")); return; }
        string route=path.Substring(prefix.Length);
        if(route=="state") { Reply(stream,200,"application/json",Encoding.UTF8.GetBytes(state)); return; }
        if(route=="config") { Reply(stream,200,"text/plain",Encoding.UTF8.GetBytes(secure)); return; }
        if(route=="root.cer" && rootCertificate!=null) { Reply(stream,200,"application/x-x509-ca-cert",rootCertificate); return; }
        if(route.StartsWith("action?",StringComparison.Ordinal))
        {
            string name="",id="";
            foreach(string item in route.Substring(7).Split('&'))
            { var pair=item.Split(new[]{'='},2); if(pair.Length!=2)continue; if(pair[0]=="name")name=WebUtility.UrlDecode(pair[1]); if(pair[0]=="id")id=pair[1]; }
            if(name.Length>180 || id.Length<8 || id.Length>64 || !(name=="step" || name=="play" || name=="reset" || name=="repair" || name=="start" || name.StartsWith("ask:",StringComparison.Ordinal)))
            { Reply(stream,400,"text/plain",Encoding.UTF8.GetBytes("Invalid command")); return; }
            lock(seen)
            {
                if(!seen.Contains(id))
                {
                    if(commands.Count>=12){Reply(stream,429,"text/plain",new byte[0]);return;}
                    seen.Add(id); recent.Enqueue(id); if(recent.Count>64) seen.Remove(recent.Dequeue()); commands.Enqueue(name);
                }
            }
            Reply(stream,200,"text/plain",new byte[0]); return;
        }
        if(route=="marker")
        {
            Reply(stream,200,"text/html",Encoding.UTF8.GetBytes("<!doctype html><meta charset='utf-8'><title>AR 학습 카드</title><style>body{text-align:center;font:20px sans-serif}img{width:70vmin;image-rendering:pixelated}</style><h1>AR 코드 실행 실험실 · 학습 카드</h1><p>휴대폰으로 아래 카드의 검은 테두리 전체를 비춰 주세요. 인쇄해서 책상에 놓아도 됩니다.</p><img src='hiro.png'><p>PC 화면을 비추면 화면 위에, 인쇄 카드를 비추면 책상 위에 표시됩니다.</p>")); return;
        }
        if(route=="")route="index.html";
        byte[] data;
        if(!assets.TryGetValue(route,out data)) { Reply(stream,404,"text/plain",new byte[0]); return; }
        string type=route.EndsWith(".html")?"text/html":route.EndsWith(".js")?"application/javascript":route.EndsWith(".png")?"image/png":"application/octet-stream";
        Reply(stream,200,type,data);
    }
    static string ReadLine(Stream s)
    {
        var line=new StringBuilder();
        while(line.Length<2200){int c=s.ReadByte();if(c<0||c==10)return line.ToString();if(c!=13)line.Append((char)c);}
        throw new IOException("Header too long");
    }
    static void Reply(Stream s,int status,string type,byte[] bytes)
    {
        string reason=status==200?"OK":status==404?"Not Found":status==429?"Too Many Requests":"Bad Request";
        byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" "+reason+"\r\nContent-Type: "+type+"\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\n\r\n");
        s.Write(header,0,header.Length);s.Write(bytes,0,bytes.Length);
    }
    public void Dispose() { closed=true;http.Stop();https?.Stop(); }
}
#endif
