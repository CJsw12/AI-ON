using System.Collections.Generic;
using UnityEngine;

// Lines the player has already read, across all playthroughs, so skip mode can pass over them.
// Kept in PlayerPrefs (AION.Read) as one key per line, separated by newlines.
public static class ReadLog
{
    const string Key = "AION.Read";
    static HashSet<string> read;

    static HashSet<string> Read
    {
        get
        {
            if (read == null) read = new HashSet<string>(PlayerPrefs.GetString(Key, "").Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries));
            return read;
        }
    }

    public static bool Seen(string line) => line != null && Read.Contains(line);

    public static void Mark(string line)
    {
        if (line == null || !Read.Add(line)) return;
        PlayerPrefs.SetString(Key, string.Join("\n", read));
        PlayerPrefs.Save();
    }
}
