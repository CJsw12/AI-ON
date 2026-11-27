#if UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// One owned child process. Only the background thread touches its redirected streams.
public sealed class LocalSpeechWorker : IDisposable
{
    [Serializable] sealed class Request { public string path, hint; }
    [Serializable] sealed class Response { public bool ready; public string text, error; }
    readonly string script;
    readonly string model;
    readonly object sync = new object();
    System.Diagnostics.Process process;
    volatile bool disposed;
    public volatile string Stage = "음성 모델 준비 중";
    public volatile string Diagnostic;
    public LocalSpeechWorker(string script, string model = "small") { this.script = script; this.model = model == "medium" ? "medium" : "small"; }
    public Task<string> Transcribe(string path, string hint)
    {
        return Task.Run(() => {
            try
            {
                lock (sync)
                {
                    if (disposed) throw new ObjectDisposedException("LocalSpeechWorker");
                    if (process == null || process.HasExited)
                    {
                        Stage = "음성 모델 준비 중";
                        var start = new System.Diagnostics.ProcessStartInfo("uv",
                            "run --with faster-whisper==1.2.1 --with av==16.1.0 python " + Quote(script) + " --worker --model " + model)
                        {
                            UseShellExecute = false, CreateNoWindow = true,
                            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
                        };
                        process = System.Diagnostics.Process.Start(start);
                        if (disposed) { StopProcess(process); throw new ObjectDisposedException("LocalSpeechWorker"); }
                        process.ErrorDataReceived += (sender, args) => { if (!string.IsNullOrEmpty(args.Data)) Diagnostic = args.Data; };
                        process.BeginErrorReadLine();
                        string first = ReadLine(180000);
                        if (first == null || !JsonUtility.FromJson<Response>(first).ready)
                            throw new IOException("음성 모델을 시작하지 못했습니다.");
                        Stage = "한국어 음성 인식 중";
                    }
                    process.StandardInput.WriteLine(JsonUtility.ToJson(new Request { path = path, hint = hint }));
                    process.StandardInput.Flush();
                    Stage = "한국어 문장 변환 중";
                    string line = ReadLine(60000);
                    if (line == null) throw new IOException("음성 인식 프로그램이 종료되었습니다.");
                    Response result = JsonUtility.FromJson<Response>(line);
                    if (!string.IsNullOrEmpty(result.error)) throw new IOException(result.error);
                    Stage = "인식 완료";
                    return result.text ?? "";
                }
            }
            catch { StopProcess(process); process = null; throw; }
            finally { try { File.Delete(path); } catch (IOException) { } }
        });
    }
    string ReadLine(int timeout)
    {
        var read = process.StandardOutput.ReadLineAsync();
        if (!read.Wait(timeout)) throw new IOException("음성 인식 대기 시간이 초과되었습니다.");
        return read.Result;
    }
    static void StopProcess(System.Diagnostics.Process running)
    {
        if (running == null) return;
        try
        {
            if (!running.HasExited)
            {
                // uv owns a Python child; stop the whole owned tree, not unrelated Python processes.
                using (var kill = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskkill", "/PID " + running.Id + " /T /F")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                    kill.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException) { }
        finally { running.Dispose(); }
    }
    public static string Quote(string text) => "\"" + text.Replace("\"", "") + "\"";
    public void Dispose()
    {
        disposed = true;
        var running = process;
        if (running == null) return;
        Task.Run(() => StopProcess(running));
        // Do not wait on the worker lock from Unity's main thread.
    }
}
#endif
