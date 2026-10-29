using System.Collections;
using UnityEngine;

public class NovelAudio : MonoBehaviour
{
    AudioSource music, effects;
    string current;
    Coroutine transition;
    public bool Muted { get; private set; }

    void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = effects.playOnAwake = false;
        music.loop = true;
        music.spatialBlend = effects.spatialBlend = 0;
        music.volume = 0;
        effects.volume = .35f;
        Muted = PlayerPrefs.GetInt("SoundMuted", 0) == 1;
        music.mute = effects.mute = Muted;
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
        while (music.volume > 0)
        {
            music.volume = Mathf.MoveTowards(music.volume, 0, Time.unscaledDeltaTime * .5f);
            yield return null;
        }
        music.Stop();
        AudioClip old = music.clip;
        music.clip = Resources.Load<AudioClip>("Audio/" + mood);
        if (old != null && old != music.clip) Resources.UnloadAsset(old);
        if (music.clip == null) { Debug.LogWarning("Missing music: " + mood); yield break; }
        music.Play();
        while (music.volume < .32f)
        {
            music.volume = Mathf.MoveTowards(music.volume, .32f, Time.unscaledDeltaTime * .32f);
            yield return null;
        }
    }

    public void Play(string sound)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + sound);
        if (clip != null) effects.PlayOneShot(clip);
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        music.mute = effects.mute = Muted;
        PlayerPrefs.SetInt("SoundMuted", Muted ? 1 : 0);
        PlayerPrefs.Save();
    }
    void OnGUI()
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        int oldDepth = GUI.depth;
        GUI.matrix = Matrix4x4.identity;
        GUI.depth = -100;
        if (GUI.Button(new Rect(Screen.width - 140, 8, 130, 32), Muted ? "소리 켜기" : "음소거")) ToggleMute();
        GUI.depth = oldDepth;
        GUI.matrix = oldMatrix;
    }
}
