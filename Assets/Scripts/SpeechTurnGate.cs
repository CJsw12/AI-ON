using System;

// Recording boundary only; recognition and gameplay are deliberately kept outside this rule.
public sealed class SpeechTurnGate
{
    double started, lastVoice, voiceDuration, previous;
    public bool HasSpeech { get { return voiceDuration >= .22; } }
    public void Reset(double now) { started = previous = now; lastVoice = now; voiceDuration = 0; }
    public bool Feed(double rms, double now, double threshold)
    {
        if (double.IsNaN(rms) || double.IsInfinity(rms)) return false;
        double dt = Math.Max(0, Math.Min(.15, now - previous));
        previous = now;
        if (rms >= threshold) { voiceDuration += dt; lastVoice = now; }
        return HasSpeech && (now - lastVoice >= 1.3 || now - started >= 28);
    }
    public static string Command(string text)
    {
        string value = (text ?? "").Replace(" ", "").Trim('.', '!', '?', '。', ',');
        if (value == "잠깐만요" || value == "음성중지") return "pause";
        if (value == "다시말할게요" || value == "다시녹음") return "retry";
        return "";
    }
}
