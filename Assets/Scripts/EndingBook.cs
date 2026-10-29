using UnityEngine;

// Endings seen so far, one bitmask per faculty and professor.
public static class EndingBook
{
    public const int Endings = 8;

    static string Key(int faculty, int professor) => "AION.Endings." + faculty + "." + professor;
    static int Mask(int faculty, int professor) => PlayerPrefs.GetInt(Key(faculty, professor), 0);

    public static bool Seen(int faculty, int professor, int ending) => (Mask(faculty, professor) >> ending & 1) != 0;

    // Returns true the first time an ending is seen.
    public static bool Unlock(int faculty, int professor, int ending)
    {
        int mask = Mask(faculty, professor);
        if ((mask >> ending & 1) != 0) return false;
        PlayerPrefs.SetInt(Key(faculty, professor), mask | 1 << ending);
        PlayerPrefs.Save();
        return true;
    }

    public static int Count(int faculty, int professor)
    {
        int count = 0;
        for (int i = 0; i < Endings; i++) if (Seen(faculty, professor, i)) count++;
        return count;
    }
}
