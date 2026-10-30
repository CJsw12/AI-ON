using UnityEngine;

// A minigame that replaces one of the twelve fixed choices. Majors and non-majors play the same game.
// Drawn on the 1280x900 canvas; GameController shows the professor's reply once Done.
// Each faculty plays the same three games with its own words (MiniGameTheme).
public abstract class MiniGame
{
    public bool Done;
    public int Delta;            // Affection change, 0 to 2.
    public string Summary;
    protected readonly MiniGameTheme Theme;
    protected MiniGame(MiniGameTheme theme) { Theme = theme; }
    public string Title => Theme.Title;
    public abstract string[][] Replies { get; }   // [professor][Delta]; use Theme.Replies when the faculty sets it.
    // Whether the result counts toward the chapter's ending stat.
    public virtual bool CountsForStat => Delta > 0;
    public abstract void Draw(GUIStyle title, GUIStyle body, GUIStyle button);

    // Minigame clock in seconds. It stands still while a menu (settings) is open, so use it instead of Time.unscaledTime.
    public static float Now { get; private set; }
    public static void Tick(float seconds) => Now += seconds;

    static GUIStyle face;
    protected static GUIStyle Face(GUIStyle body)
    {
        if (face == null) face = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        return face;
    }
    protected static void Box(Rect rect, Color color)
    {
        GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
    }
}

public enum MiniGameKind { Cards, Timing, Coffee }

// One faculty's words for a minigame. Fields left null keep the game's own AI-SW text.
public class MiniGameTheme
{
    public MiniGameKind Kind;
    public string Title;
    public string Place, Note;     // Background picture and the line shown in the practice menu.
    public string[][] Replies;     // [professor][Delta]
    public string[,] Pairs;        // Cards: six { term, meaning }.
    // Timing: the panel header, the code or rule line, how the counter is shown ("i = {0}"),
    // the line under the counter, the stop button, the instructions, the three level names and the verdicts.
    public string Header, Rule, Counter, Running, Button, Instructions, Hit, Near, Miss;
    public string[] Levels;
}

public static class MiniGames
{
    public static MiniGame Create(MiniGameTheme theme, int professor)
    {
        switch (theme.Kind) {
            case MiniGameKind.Cards: return new CardMatch(theme);
            case MiniGameKind.Timing: return new LoopTiming(theme);
            default: return new CoffeeOrder(professor, theme);
        }
    }
}
