#if UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
public partial class GameController
{
    bool storyListening, storyVoiceAuto;
    string storyMicStatus = "마이크를 선택한 뒤 자동 음성 입력을 켜 주세요.";
    string storyDictated, storyDictatedSlot;
    AudioClip storyRecording;
    string storyMic;
    int speechMicIndex, speechGeneration, pendingSpeechGeneration;
    string[] speechDevices;
    float speechLevel, speechNoise = .002f, speechStarted, speechNextSample, speechReviewAt = -1;
    float speechThreshold = .012f;
    float speechResumeAt;
    readonly float[] speechWindow = new float[1024];
    readonly SpeechTurnGate speechGate = new SpeechTurnGate();
    Task<string> storyTranscription;
    LocalSpeechWorker speechWorker;
    bool speechAutoSubmitted;
    bool speechAccurate;
    const int SpeechAnswerLimit = 240;

    bool StoryVoiceContext => !voiceChatOpen && InGame && menu == Menu.None && aiMode && AtChoice &&
        !showResult && !aiShowOptions && !speechSettingsOpen && !aiAsking && miniGame == null && !lookAwayInterruptActive &&
        S.Slot(scene, choiceStage).Options != null && aiSlot == AiSlotKey;

    void RefreshSpeechDevices() { speechDevices = Microphone.devices; speechMicIndex = Mathf.Clamp(speechMicIndex, 0, Mathf.Max(0, speechDevices.Length - 1)); }
    void StartStoryDictation()
    {
        if (storyListening || storyTranscription != null || novelAudio.Speaking || Time.unscaledTime < speechResumeAt) return;
        RefreshSpeechDevices();
        if (speechDevices.Length == 0) { storyMicStatus = "연결된 마이크가 없습니다. Windows 마이크 권한을 확인해 주세요."; storyVoiceAuto = false; return; }
        storyMic = speechDevices[speechMicIndex];
        storyDictatedSlot = voiceChatOpen ? "chat/" + voiceChatRun : AiSlotKey;
        speechReviewAt = -1;
        speechAutoSubmitted = false;
        speechNoise = .002f;
        speechStarted = Time.unscaledTime;
        speechNextSample = 0;
        speechGate.Reset(speechStarted);
        try { storyRecording = Microphone.Start(storyMic, false, 30, 16000); }
        catch (Exception) { storyRecording = null; }
        storyListening = storyRecording != null;
        storyMicStatus = storyListening ? "듣는 중 · 말한 뒤 1.3초 쉬면 인식합니다." : "마이크를 시작하지 못했습니다.";
        if (!storyListening) storyVoiceAuto = false;
    }

    // Cancellation discards audio; leaving a scene must never submit an old recording.
    void CancelStoryDictation()
    {
        speechGeneration++;
        if (storyListening) Microphone.End(storyMic);
        storyListening = false;
        speechLevel = 0;
        if (storyRecording != null) Destroy(storyRecording);
        storyRecording = null;
        storyDictated = null;
        speechReviewAt = -1;
        speechAutoSubmitted = false;
    }
    void StopStoryDictation()
    {
        if (!storyListening) return;
        storyListening = false;
        int samples = Microphone.GetPosition(storyMic);
        Microphone.End(storyMic);
        if (storyRecording == null || samples < 1600 || !speechGate.HasSpeech)
        {
            storyMicStatus = "말소리를 찾지 못했습니다. 마이크 음량·감도를 확인하고 다시 말해 주세요.";
            if (storyRecording != null) Destroy(storyRecording);
            storyRecording = null;
            return;
        }
        float[] audio = new float[samples];
        storyRecording.GetData(audio, 0);
        Destroy(storyRecording);
        storyRecording = null;
        string path = Path.Combine(Application.temporaryCachePath, "story-" + Guid.NewGuid().ToString("N") + ".wav");
        // Apply bounded gain to quiet recordings without amplifying silence without limit.
        float peak = .001f;
        foreach (float value in audio) peak = Mathf.Max(peak, Mathf.Abs(value));
        float gain = Mathf.Clamp(.85f / peak, 1, 5);
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            foreach (float value in audio) writer.Write((short)(Mathf.Clamp(value * gain, -1, 1) * 32767));
        }
        storyMicStatus = "한국어 인식 중 · 모델 첫 실행은 다운로드가 필요합니다.";
        if (speechWorker == null) speechWorker = new LocalSpeechWorker(Path.Combine(Application.dataPath, "../Tools/StorySpeech/transcribe.py"), speechAccurate ? "medium" : "small");
        Story topic = voiceChatOpen ? Stories.All[voiceChatFaculty] : S;
        int who = voiceChatOpen ? voiceChatProfessor : Mathf.Max(0, professor);
        string hint = topic.Name + ", " + topic.Professors[who].Name + ", " + topic.Professors[who].Description;
        if (!voiceChatOpen && scene >= 1 && scene <= 6) hint += ", " + ChapterName;
        pendingSpeechGeneration = speechGeneration;
        storyTranscription = speechWorker.Transcribe(path, hint);
    }

    void PollStoryDictation()
    {
        if (storyListening && Time.unscaledTime >= speechNextSample)
        {
            speechNextSample = Time.unscaledTime + .05f;
            int position = Microphone.GetPosition(storyMic);
            if (position >= speechWindow.Length)
            {
                storyRecording.GetData(speechWindow, position - speechWindow.Length);
                double sum = 0;
                foreach (float value in speechWindow) sum += value * value;
                speechLevel = (float)Math.Sqrt(sum / speechWindow.Length);
                if (Time.unscaledTime - speechStarted < .4f && speechLevel < speechThreshold)
                    speechNoise = Mathf.Min(.004f, Mathf.Max(speechNoise, speechLevel));
                else if (speechGate.Feed(speechLevel, Time.unscaledTime, Mathf.Max(speechThreshold, speechNoise * 2.5f))) StopStoryDictation();
            }
            if (storyListening && Time.unscaledTime - speechStarted > (speechGate.HasSpeech ? 28 : 12)) StopStoryDictation();
        }
        if (storyTranscription == null) return;
        if (!storyTranscription.IsCompleted)
        {
            if (speechWorker != null) storyMicStatus = speechWorker.Stage;
            return;
        }
        var task = storyTranscription;
        storyTranscription = null;
        if (pendingSpeechGeneration != speechGeneration) return;
        if (task.IsFaulted || task.IsCanceled)
        {
            storyMicStatus = "음성 인식 실패 · 자동 입력을 멈췄습니다. 다시 켜거나 직접 입력해 주세요.";
            storyVoiceAuto = false;
            voiceChatAuto = false;
        }
        else
        {
            storyDictated = task.Result.Trim();
            string command = SpeechTurnGate.Command(storyDictated);
            if (command == "pause")
            {
                storyVoiceAuto = voiceChatAuto = false;
                storyDictated = null;
                storyMicStatus = "음성 입력을 일시 중지했습니다.";
            }
            else if (command == "retry" || string.IsNullOrWhiteSpace(storyDictated))
            {
                storyDictated = null;
                storyMicStatus = "다시 말씀해 주세요.";
            }
            else storyMicStatus = "인식 완료 · 문장을 확인해 주세요.";
        }
    }

    void TickVoiceInput()
    {
        if (novelAudio.Speaking)
        {
            speechResumeAt = Time.unscaledTime + .45f;
            if (storyListening) CancelStoryDictation();
        }
        PollStoryDictation();
        if (voiceChatOpen) { TickVoiceChat(); return; }
        if (!StoryVoiceContext)
        {
            if (storyListening || speechReviewAt >= 0 || storyDictated != null ||
                (storyTranscription != null && pendingSpeechGeneration == speechGeneration)) CancelStoryDictation();
            return;
        }
        if (novelAudio.Speaking && storyListening) CancelStoryDictation();
        if (storyVoiceAuto && typedAt >= 0 && !AiWaiting && aiError == null && !novelAudio.Speaking &&
            storyTranscription == null && storyDictated == null && speechReviewAt < 0 && !speechAutoSubmitted && string.IsNullOrWhiteSpace(aiTyped))
            StartStoryDictation();
    }

    void DrawSpeechDevice(Rect rect)
    {
        if (speechDevices == null) RefreshSpeechDevices();
        string name = speechDevices.Length == 0 ? "마이크 없음 · 눌러 새로고침" : speechDevices[speechMicIndex];
        if (name.Length > 30) name = name.Substring(0, 30) + "…";
        if (NovelUI.Button(new Rect(rect.x, rect.y, rect.width * .60f, rect.height), "마이크: " + name, false, 14))
        {
            CancelStoryDictation(); RefreshSpeechDevices();
            if (speechDevices.Length > 0) speechMicIndex = (speechMicIndex + 1) % speechDevices.Length;
        }
        float x = rect.x + rect.width * .62f;
        NovelUI.Fill(new Rect(x, rect.y + 10, 70, 10), NovelUI.Blush, 4);
        NovelUI.Fill(new Rect(x, rect.y + 10, 70 * Mathf.Clamp01(speechLevel * 10), 10), NovelUI.Mint, 4);
        NovelUI.Label(new Rect(x + 78, rect.y, 60, rect.height), "감도", NovelUI.Soft, 14, NovelUI.Plum);
        // Higher sensitivity means a lower RMS threshold.
        speechThreshold = GUI.HorizontalSlider(new Rect(x + 118, rect.y + 10, Mathf.Max(50, rect.width * .38f - 120), 18), speechThreshold, .03f, .004f);
    }

    bool speechSettingsOpen;
    void DrawStoryDictation(Rect area)
    {
        if (NovelUI.Button(new Rect(area.x, area.y, 170, area.height), storyVoiceAuto ? "자동 듣기 끄기" : "음성 입력 켜기", storyVoiceAuto, 15))
        {
            storyVoiceAuto = !storyVoiceAuto;
            CancelStoryDictation();
            if (storyVoiceAuto) { aiError = null; speechAutoSubmitted = false; }
        }
        GUI.enabled = storyTranscription == null && !novelAudio.Speaking && !speechSettingsOpen;
        if (NovelUI.Button(new Rect(area.x + 180, area.y, 135, area.height), storyListening ? "녹음 마치기" : "다시 말하기", false, 15))
        {
            if (storyListening) StopStoryDictation();
            else { aiTyped = ""; CancelStoryDictation(); StartStoryDictation(); }
        }
        GUI.enabled = true;
        if (NovelUI.Button(new Rect(area.x + 325, area.y, 100, area.height), "마이크 설정", false, 14))
        {
            speechSettingsOpen = !speechSettingsOpen;
            if (speechSettingsOpen) CancelStoryDictation();
        }
        string status = storyListening ? "듣는 중 · 말한 뒤 잠시 쉬어 주세요." : storyTranscription != null ? "말씀하신 내용을 인식하고 있습니다…" : storyVoiceAuto ? "자동 음성 입력 대기 중" : "음성 입력 꺼짐";
        NovelUI.Label(new Rect(area.x + 440, area.y, area.width - 440, area.height), status, NovelUI.Soft, 14, NovelUI.Plum);
        if (!speechSettingsOpen) return;
        Rect panel = new Rect(16, 340, 450, 254);
        NovelUI.Card(panel, NovelUI.Cream, 20, NovelUI.Pink);
        NovelUI.Label(new Rect(34, 355, 350, 30), "마이크 설정", NovelUI.Round, 23, NovelUI.Berry);
        if (NovelUI.Button(new Rect(376, 352, 72, 30), "닫기", false, 14)) speechSettingsOpen = false;
        if (speechDevices == null) RefreshSpeechDevices();
        string device = speechDevices.Length == 0 ? "마이크 없음 · 새로고침" : speechDevices[speechMicIndex];
        if (NovelUI.Button(new Rect(34, 400, 414, 38), device, false, 13))
        {
            CancelStoryDictation(); RefreshSpeechDevices();
            if (speechDevices.Length > 0) speechMicIndex = (speechMicIndex + 1) % speechDevices.Length;
        }
        NovelUI.Label(new Rect(34, 447, 90, 30), "감도", NovelUI.Soft, 16, NovelUI.Plum);
        speechThreshold = GUI.HorizontalSlider(new Rect(130, 457, 295, 18), speechThreshold, .03f, .004f);
        if (NovelUI.Button(new Rect(34, 488, 414, 34), speechAccurate ? "인식 모델: 정확도 우선 (medium)" : "인식 모델: 속도 우선 (small)", false, 14))
        {
            speechAccurate = !speechAccurate; CancelStoryDictation();
            speechWorker?.Dispose(); speechWorker = null;
        }
        NovelUI.Label(new Rect(34, 533, 414, 50), storyMicStatus, NovelUI.Soft, 13, NovelUI.Plum);
    }
    void DisposeSpeechInput()
    {
        CancelStoryDictation();
        speechWorker?.Dispose();
        speechWorker = null;
    }
}
#endif
