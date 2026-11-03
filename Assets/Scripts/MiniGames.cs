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

    // Feedback effects shared by every game: confetti bursts, floating text, screen shake and a star result.
    class Spark { public Vector2 At, Velocity; public Color Color; public float Born; }
    class Floater { public string Text; public Vector2 At; public Color Color; public float Born; }
    readonly System.Collections.Generic.List<Spark> sparks = new System.Collections.Generic.List<Spark>();
    readonly System.Collections.Generic.List<Floater> floaters = new System.Collections.Generic.List<Floater>();
    float shakeAt = -10, doneAt = -1;

    protected void Burst(Vector2 at, int count = 24)
    {
        if (!GameSettings.Effects) return;
        Color[] colors = { NovelUI.Pink, NovelUI.Lemon, NovelUI.Mint, NovelUI.Sky, Color.white };
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0, Mathf.PI * 2), speed = Random.Range(120f, 420f);
            sparks.Add(new Spark { At = at, Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) - .6f) * speed, Color = colors[i % colors.Length], Born = Now });
        }
    }
    protected void Popup(string text, Vector2 at, Color color) => floaters.Add(new Floater { Text = text, At = at, Color = color, Born = Now });
    protected void Shake() => shakeAt = Now;
    // Right or wrong in one call: sound, text and either confetti or a shake.
    protected void Judge(bool good, Vector2 at, string text = null)
    {
        NovelUI.Sound?.Invoke(good ? "Game/Correct|Up" : "Game/Wrong|Down");
        Popup(text ?? (good ? "좋아요!" : "앗!"), at, good ? NovelUI.Mint : NovelUI.DeepPink);
        if (good) Burst(at); else Shake();
    }

    // Games whose finished board stays on screen turn the star row off.
    protected virtual bool ShowStars => true;

    // Called by the host instead of Draw.
    public void Render(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        Matrix4x4 saved = GUI.matrix;
        float shake = Now - shakeAt;
        if (shake < .35f && GameSettings.Effects) GUI.matrix = saved * Matrix4x4.Translate(new Vector3(Mathf.Sin(shake * 90) * 10 * (1 - shake / .35f), 0, 0));
        Draw(title, body, button);
        GUI.matrix = saved;
        if (Done && doneAt < 0) { doneAt = Now; NovelUI.Sound?.Invoke(Delta == 2 ? "Game/Win|Unlock" : "Chapter"); if (Delta == 2) Burst(new Vector2(640, 460), 60); }
        if (Done && ShowStars) DrawStars();
        DrawEffects();
    }

    void DrawStars()
    {
        for (int i = 0; i < 3; i++)
        {
            float t = Mathf.Clamp01((Now - doneAt - .3f - i * .25f) / .25f);
            if (t <= 0) continue;
            bool lit = i <= Delta;
            float size = 70 * (1 + .4f * Mathf.Sin(t * Mathf.PI));
            NovelUI.Label(new Rect(520 + i * 120 - 40, 470 - size / 2, 140, size + 10), "★", NovelUI.Round, Mathf.RoundToInt(size), lit ? NovelUI.Lemon : NovelUI.Alpha(Color.white, .2f), TextAnchor.MiddleCenter);
        }
    }

    void DrawEffects()
    {
        float now = Now;
        sparks.RemoveAll(s => now - s.Born > 1.1f);
        floaters.RemoveAll(f => now - f.Born > 1f);
        foreach (var s in sparks)
        {
            float t = now - s.Born;
            Vector2 p = s.At + s.Velocity * t + new Vector2(0, 500) * t * t;
            NovelUI.Fill(new Rect(p.x - 5, p.y - 5, 10, 10), NovelUI.Alpha(s.Color, 1 - t / 1.1f), 5);
        }
        foreach (var f in floaters)
        {
            float t = now - f.Born;
            NovelUI.Label(new Rect(f.At.x - 200, f.At.y - 30 - t * 60, 400, 50), f.Text, NovelUI.Round, 30, NovelUI.Alpha(f.Color, 1 - t), TextAnchor.MiddleCenter);
        }
    }
}

public enum MiniGameKind { Cards, Timing, Coffee, BugHunt, CircuitProbe, PriceTag, Rhythm, Route, Cashier, Tray }

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
    public static MiniGame Create(MiniGameTheme theme, int professor, bool practice = false)
    {
        switch (theme.Kind) {
            case MiniGameKind.Cards: return new CardMatch(theme);
            case MiniGameKind.Timing: return new LoopTiming(theme);
            case MiniGameKind.BugHunt: return new BugHunt(theme);
            case MiniGameKind.CircuitProbe: return new CircuitProbe(theme);
            case MiniGameKind.PriceTag: return new PriceTag(theme);
            case MiniGameKind.Rhythm: return new StretchRhythm(theme);
            case MiniGameKind.Route: return new RobotRoute(theme);
            case MiniGameKind.Cashier: return new ChangeMaker(theme);
            case MiniGameKind.Tray: return new PlateBalance(theme);
            default: return new CoffeeOrder(professor, theme, practice);
        }
    }
}
