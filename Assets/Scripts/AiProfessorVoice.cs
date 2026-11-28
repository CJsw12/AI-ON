using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Only generated story free-answer replies use this path. Failure keeps subtitles unchanged.
public static class AiProfessorVoice
{
    public sealed class Speech { public float[] Samples; }
    public static bool CanSpeak(string voice) { return AiProfessor.Supported && !string.IsNullOrEmpty(voice) && voice != "ShimHanmoe"; }

    public static async Task<Speech> Request(string voice, string text, CancellationToken cancellation)
    {
        if (!CanSpeak(voice) || string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var request = new HttpRequestMessage(HttpMethod.Post, AiProfessor.ProxyEndpoint.TrimEnd('/') + "/tts"))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(4));
                request.Headers.Add("X-AION-Client", "dugeun-campus");
                request.Content = new StringContent("{\"voice\":" + AiProfessor.Json(voice) + ",\"text\":" + AiProfessor.Json(text) + "}", Encoding.UTF8, "application/json");
                using (var response = await AiProfessor.client.SendAsync(request, HttpCompletionOption.ResponseContentRead, deadline.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType == null || response.Content.Headers.ContentType.MediaType != "audio/wav") return null;
                    byte[] wav = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                    float volume = 0;
                    // Server owns voices.json; the header keeps builds in sync without shipping a second table.
                    System.Collections.Generic.IEnumerable<string> values;
                    if (response.Headers.TryGetValues("X-AION-Volume", out values))
                        foreach (string value in values) { float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out volume); break; }
                    if (float.IsNaN(volume) || float.IsInfinity(volume) || volume < -30 || volume > 30) return null;
                    return Decode(wav, volume);
                }
            }
        }
        catch { return null; } // Errors, 429 and timeouts silently retain the subtitle-only result.
    }

    // Off-thread, no Unity objects: only Azure PCM16 mono 24 kHz RIFF is accepted.
    public static Speech Decode(byte[] wav, float volume)
    {
        if (wav == null || wav.Length < 44 || wav.Length > 16 * 1024 * 1024 || float.IsNaN(volume) || float.IsInfinity(volume) || volume < -30 || volume > 30) return null;
        using (var stream = new MemoryStream(wav, false))
        using (var reader = new BinaryReader(stream))
        {
            if (reader.ReadUInt32() != 0x46464952) return null;
            long end = reader.ReadUInt32() + 8L;
            if (end > wav.Length || end < 44 || reader.ReadUInt32() != 0x45564157) return null;
            bool format = false;
            int dataAt = -1, dataSize = 0;
            while (stream.Position + 8 <= end)
            {
                uint id = reader.ReadUInt32(), size = reader.ReadUInt32();
                long start = stream.Position;
                if (size > end - start) return null;
                if (id == 0x20746d66) // fmt (extra metadata is allowed)
                {
                    if (size < 16) return null;
                    format = reader.ReadUInt16() == 1 && reader.ReadUInt16() == 1 && reader.ReadUInt32() == 24000 &&
                        reader.ReadUInt32() == 48000 && reader.ReadUInt16() == 2 && reader.ReadUInt16() == 16;
                    if (!format) return null;
                }
                else if (id == 0x61746164) { dataAt = (int)start; dataSize = (int)size; }
                stream.Position = start + size + (size & 1);
            }
            if (!format || dataAt < 0 || dataSize < 2 || (dataSize & 1) != 0) return null;
            stream.Position = dataAt;
            var samples = new float[dataSize / 2];
            double squares = 0;
            float peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = samples[i] = reader.ReadInt16() / 32768f;
                squares += sample * sample;
                peak = Math.Max(peak, Math.Abs(sample));
            }
            // Pre-generated clips' target, with RMS approximating LUFS at runtime.
            double targetDb = -20 + 20 * Math.Log10(1 + volume / 100.0);
            double rms = Math.Sqrt(squares / samples.Length);
            double gain = rms > 1e-9 ? Math.Pow(10, targetDb / 20) / rms : 1;
            if (peak > 0) gain = Math.Min(gain, Math.Pow(10, -1.0 / 20) / peak);
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)(samples[i] * gain);
            return new Speech { Samples = samples };
        }
    }

    // Only the controller's main-thread poll creates a clip after validating the dialogue.
    public static AudioClip CreateClip(Speech speech)
    {
        if (speech == null || speech.Samples == null || speech.Samples.Length == 0) return null;
        var clip = AudioClip.Create("AI professor reply", speech.Samples.Length, 1, 24000, false);
        if (clip.SetData(speech.Samples, 0)) return clip;
        UnityEngine.Object.Destroy(clip);
        return null;
    }
}
