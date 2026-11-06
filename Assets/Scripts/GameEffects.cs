using System.Collections.Generic;
using UnityEngine;

// Motion and reactions on top of the existing pictures: the professor breathes, hops on a good answer and
// shakes on a bad one, the background drifts slowly, hearts and stars burst out, and each chapter opens
// with a title card. Everything except the chapter card follows the "화면 효과" setting.
public partial class GameController
{
    float reactAt = -10;
    int reactDelta;
    bool reactCorrect;
    bool nonsenseResultActive;
    int chapterShown;
    float chapterAt = -10;

    struct Spark { public Vector2 Position, Velocity; public float Born, Life, Size, Spin; public Color Color; public bool Star, Outline; }
    readonly List<Spark> sparks = new List<Spark>();

    float SinceReaction => Time.unscaledTime - reactAt;

    // The professor smiles or frowns for a moment after the player's choice.
    string ReactionFace
    {
        get
        {
            if (lookAwayInterruptActive || nonsenseResultActive) return "Frown";
            if (SinceReaction > 2.5f) return null;
            return reactDelta > 0 ? "Happy" : reactDelta < 0 ? "Frown" : null;
        }
    }

    // Reacts to a result: sound, face, motion and particles. `quiz` marks right/wrong answers and good minigames.
    void React(int delta, bool correct, bool quiz = false, bool silent = false)
    {
        reactAt = Time.unscaledTime;
        reactDelta = delta;
        reactCorrect = correct;
        string sound = quiz && correct ? "Bonus" : delta > 0 ? "Up" : delta < 0 || !correct ? "Down" : "Select";
        if (!silent) novelAudio.Play(sound);
        if (!GameSettings.Effects) return;
        if (delta > 0)
            for (int i = 0; i < 16; i++)
            {
                float angle = Random.Range(-160f, -20f) * Mathf.Deg2Rad, speed = Random.Range(220f, 480f);
                AddSpark(new Vector2(640 + Random.Range(-60f, 60f), 420), new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Random.Range(1f, 1.6f), Random.Range(22f, 44f), i % 3 == 0 ? Color.white : i % 3 == 1 ? NovelUI.Pink : NovelUI.DeepPink, false, false);
            }
        if (quiz && correct)
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI / 5;
                AddSpark(new Vector2(640, 380), new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 300, 1.1f, 34, NovelUI.Lemon, true, false);
            }
        if (delta < 0)
            for (int i = 0; i < 8; i++)
                AddSpark(new Vector2(NovelUI.Window.x + Random.Range(80f, NovelUI.Window.width - 80), -40), new Vector2(Random.Range(-20f, 20f), Random.Range(140f, 220f)),
                    2.4f, Random.Range(26f, 40f), NovelUI.Plum, false, true);
    }

    void AddSpark(Vector2 position, Vector2 velocity, float life, float size, Color color, bool star, bool outline)
    {
        sparks.Add(new Spark { Position = position, Velocity = velocity, Born = Time.unscaledTime, Life = life, Size = size, Spin = Random.Range(-90f, 90f), Color = color, Star = star, Outline = outline });
    }

    // Particles and the pink flash, drawn on the canvas over the dialogue.
    void Particles()
    {
        if (!GameSettings.Effects) { sparks.Clear(); return; }
        if (reactDelta > 0 && SinceReaction < .35f) NovelUI.Tint(NovelUI.Alpha(NovelUI.Pink, .28f * (1 - SinceReaction / .35f)));
        if (!NovelUI.Repaint) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            float age = (Time.unscaledTime - s.Born) / s.Life;
            if (age >= 1) { sparks.RemoveAt(i); continue; }
            // Bursting hearts slow down and float; falling ones keep drifting down.
            s.Velocity = s.Outline ? s.Velocity : s.Velocity * Mathf.Pow(.12f, dt) + new Vector2(0, -30) * dt;
            s.Position += s.Velocity * dt;
            sparks[i] = s;
            float alpha = 1 - age * age, size = s.Size * (s.Star ? 1 - age * .5f : .7f + .3f * Mathf.Clamp01(age * 6));
            Rect rect = new Rect(s.Position.x - size / 2, s.Position.y - size / 2, size, size);
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(s.Spin * age + (s.Outline ? Mathf.Sin(age * 12) * 20 : 0), s.Position);
            if (s.Star) NovelUI.DrawIcon(rect, "IconStar", NovelUI.Alpha(s.Color, alpha));
            else NovelUI.Heart(rect, NovelUI.Alpha(s.Color, alpha * (s.Outline ? .8f : 1)), !s.Outline);
            GUI.matrix = before;
        }
    }

    // A slow pan and zoom so still backgrounds feel alive (screen coordinates).
    Rect Drift(Rect full)
    {
        if (!GameSettings.Effects) return full;
        float t = Time.unscaledTime, grow = .05f;
        float w = full.width * (1 + grow), h = full.height * (1 + grow);
        float x = full.x - (w - full.width) / 2 + Mathf.Sin(t * .07f) * (w - full.width) * .45f;
        float y = full.y - (h - full.height) / 2 + Mathf.Cos(t * .05f) * (h - full.height) * .45f;
        return new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(w), Mathf.Round(h));
    }

    // Where the professor's picture goes: breathing, a slide up as they appear, a hop or a shake after a reaction.
    Rect PortraitRect()
    {
        // Shin's source bust is larger than the existing portraits; keep its head at the same height.
        float framing = S.Professors[professor].Image == "ProfessorShin" ? .78f : 1f;
        Rect full = new Rect(Screen.width * (1 - framing) / 2, Screen.height * (1 - framing) * (60f / 926f),
            Screen.width * framing, Screen.height * framing);
        if (!GameSettings.Effects) return new Rect(Mathf.Round(full.x), Mathf.Round(full.y), Mathf.Round(full.width), Mathf.Round(full.height));
        float t = Time.unscaledTime, unit = Screen.height / 900f;
        float scale = 1 + .008f * Mathf.Sin(t * 1.7f);
        float x = 0, y = (1 - portraitAlpha) * 28 * unit;
        float k = SinceReaction;
        if (reactDelta > 0 && k < .5f) y -= Mathf.Sin(k / .5f * Mathf.PI) * 30 * unit;
        if ((reactDelta < 0 || !reactCorrect) && k < .55f) x += Mathf.Sin(k * 46) * 14 * unit * (1 - k / .55f);
        float w = full.width * scale, h = full.height * scale;
        // Scale around the bottom centre so the feet stay put.
        return new Rect(Mathf.Round(full.x + x + (full.width - w) / 2), Mathf.Round(full.y + y + full.height - h), Mathf.Round(w), Mathf.Round(h));
    }

    // "CHAPTER n" banner that slides in when a chapter starts (or a save is loaded).
    void ChapterCard()
    {
        // Dialogue() may have just advanced past the last chapter in this same frame (scene 7 = ending).
        if (scene < 1 || scene >= S.Chapters.Length) return;
        if (chapterShown != scene)
        {
            chapterShown = scene;
            chapterAt = Time.unscaledTime;
            novelAudio.Play("Chapter");
        }
        float k = Time.unscaledTime - chapterAt;
        if (k > 2.6f) return;
        float enter = Mathf.Clamp01(k / .45f), leave = Mathf.Clamp01((2.6f - k) / .4f);
        float ease = 1 - Mathf.Pow(1 - enter, 3), alpha = Mathf.Min(enter, leave);
        float x = (1 - ease) * -420 + (1 - leave) * 160;
        Rect band = new Rect(NovelUI.Window.x - 40, 300, NovelUI.Window.width + 80, 150);
        NovelUI.Fill(new Rect(band.x + x, band.y, band.width, band.height), NovelUI.Alpha(NovelUI.Cream, .9f * alpha), 0);
        NovelUI.Fill(new Rect(band.x + x, band.y + 8, band.width, 4), NovelUI.Alpha(NovelUI.Pink, alpha), 0);
        NovelUI.Fill(new Rect(band.x + x, band.yMax - 12, band.width, 4), NovelUI.Alpha(NovelUI.Pink, alpha), 0);
        string tag = "CHAPTER " + scene;
        float width = NovelUI.Width(tag, NovelUI.Round, 22) + 40;
        NovelUI.Pill(640 - width / 2 + x, band.y + 22, 44, tag, NovelUI.Alpha(NovelUI.DeepPink, alpha), NovelUI.Alpha(Color.white, alpha), 22);
        NovelUI.Outlined(new Rect(x, band.y + 70, 1280, 64), ChapterName, NovelUI.Round, 50, NovelUI.Alpha(NovelUI.Berry, alpha), NovelUI.Alpha(Color.white, alpha), 3);
        for (int i = 0; i < 2; i++)
            NovelUI.Heart(new Rect(640 + (i == 0 ? -1 : 1) * (NovelUI.Width(ChapterName, NovelUI.Round, 50) / 2 + 40) - 16 + x, band.y + 86 + Mathf.Sin(k * 5 + i) * 4, 32, 32), NovelUI.Alpha(NovelUI.Pink, alpha));
    }
}
