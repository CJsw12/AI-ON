using UnityEngine;

// Player options, kept in PlayerPrefs so they survive restarts.
public static class GameSettings
{
    public static readonly string[] SpeedNames = { "느리게", "보통", "빠르게", "즉시" };
    public static readonly string[] AutoNames = { "느긋하게", "보통", "빠르게" };
    static readonly float[] Letters = { 22, 45, 90, 100000 };
    static readonly float[] Waits = { 2.6f, 1.6f, .9f };

    public static int TextSpeed = 1, AutoSpeed = 1;
    public static bool Auto, Effects = true;
    public static float Music = .8f, Sfx = .9f, Voice = 1f;

    public static float LettersPerSecond => Letters[Mathf.Clamp(TextSpeed, 0, Letters.Length - 1)];
    // How long a finished line stays up before auto mode moves on; longer lines get more time.
    public static float AutoWait(int letters) => Waits[Mathf.Clamp(AutoSpeed, 0, Waits.Length - 1)] + letters * .03f;

    public static void Load()
    {
        TextSpeed = PlayerPrefs.GetInt("AION.TextSpeed", 1);
        AutoSpeed = PlayerPrefs.GetInt("AION.AutoSpeed", 1);
        Auto = PlayerPrefs.GetInt("AION.Auto", 0) == 1;
        Effects = PlayerPrefs.GetInt("AION.Effects", 1) == 1;
        Music = Mathf.Clamp01(PlayerPrefs.GetFloat("AION.Music", .8f));
        Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat("AION.Sfx", .9f));
        Voice = Mathf.Clamp01(PlayerPrefs.GetFloat("AION.Voice", 1f));
    }
    public static void Save()
    {
        PlayerPrefs.SetInt("AION.TextSpeed", TextSpeed);
        PlayerPrefs.SetInt("AION.AutoSpeed", AutoSpeed);
        PlayerPrefs.SetInt("AION.Auto", Auto ? 1 : 0);
        PlayerPrefs.SetInt("AION.Effects", Effects ? 1 : 0);
        PlayerPrefs.SetFloat("AION.Music", Music);
        PlayerPrefs.SetFloat("AION.Sfx", Sfx);
        PlayerPrefs.SetFloat("AION.Voice", Voice);
        PlayerPrefs.Save();
    }
}
