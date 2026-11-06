using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Background music per mood plus one-shot UI sounds. Volumes follow GameSettings.
// UI sounds live in Resources/Audio/Ui/<Name>.wav (Advance, Back, Select, Open, Close, Toggle, Hover,
// Save, Load, Up, Down, Unlock, Chapter); replacing a file with the same name swaps the sound.
public class NovelAudio : MonoBehaviour
{
    const float MusicLevel = .4f, EffectsLevel = .4f, VoiceLevel = 1f;
    AudioSource music, effects, voice;
    string current;
    Coroutine transition, speaking;
    float fade;   // 0..1 crossfade between moods, multiplied by the music volume setting.
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        voice = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = effects.playOnAwake = voice.playOnAwake = false;
        music.loop = true;
        music.spatialBlend = effects.spatialBlend = voice.spatialBlend = 0;
        music.volume = 0;
    }

    void Update()
    {
        music.volume = fade * MusicLevel * GameSettings.Music;
        voice.volume = VoiceLevel * GameSettings.Voice;
    }

    // A professor's spoken line: the clips play one after another, and a new line cuts the old one off.
    public bool Speaking => speaking != null;
    public void Speak(List<AudioClip> lines)
    {
        StopVoice();
        if (lines != null && lines.Count > 0 && GameSettings.Voice > 0) speaking = StartCoroutine(SpeakAll(lines));
    }
    public void StopVoice()
    {
        if (speaking != null) StopCoroutine(speaking);
        speaking = null;
        voice.Stop();
    }
    IEnumerator SpeakAll(List<AudioClip> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) yield return new WaitForSecondsRealtime(.15f);
            voice.clip = lines[i];
            voice.Play();
            while (voice.isPlaying) yield return null;
        }
        speaking = null;
    }

    public void SetMood(string mood)
    {
        if (current == mood) return;
        current = mood;
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(ChangeMusic(mood));
    }

    IEnumerator ChangeMusic(string mood)
    {
        while (fade > 0)
        {
            fade = Mathf.MoveTowards(fade, 0, Time.unscaledDeltaTime * 1.5f);
            yield return null;
        }
        music.Stop();
        AudioClip old = music.clip;
        music.clip = Resources.Load<AudioClip>("Audio/" + mood);
        if (old != null && old != music.clip) Resources.UnloadAsset(old);
        if (music.clip == null) { Debug.LogWarning("Missing music: " + mood); yield break; }
        music.Play();
        while (fade < 1)
        {
            fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime);
            yield return null;
        }
    }

    public void Play(string sound)
    {
        // "A|B" plays A, or B when A has no file yet.
        if (!clips.TryGetValue(sound, out AudioClip clip))
        {
            foreach (string name in sound.Split('|'))
                if ((clip = Resources.Load<AudioClip>("Audio/Ui/" + name) ?? Resources.Load<AudioClip>("Audio/" + name)) != null) break;
            clips[sound] = clip;
        }
        if (clip == null) return;
        float level = sound == "Hover" ? .35f : 1;
        effects.PlayOneShot(clip, level * EffectsLevel * GameSettings.Sfx);
    }
}
