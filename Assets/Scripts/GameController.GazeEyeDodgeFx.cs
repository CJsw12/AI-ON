using UnityEngine;

// Effects and sounds for "눈빛 피하기" (the beams themselves are drawn shapes). The game screen is drawn with IMGUI, so effects are sprite sheets drawn frame by
// frame (from the "118 sprite effects bundle", downscaled into Resources/EyeDodge) instead of particle systems, which a
// camera would draw underneath the GUI. Sounds come from "100 Retro Magic Sound Effects" (Resources/Audio/EyeDodge).
public partial class GameController
{
    /// <summary>A sprite sheet in Resources/EyeDodge: cols × rows cells read left to right, top to bottom.</summary>
    readonly struct DodgeSheet
    {
        public readonly string Path;
        public readonly int Cols, Rows, Frames;
        public DodgeSheet(string path, int cols, int rows, int frames) { Path = path; Cols = cols; Rows = rows; Frames = frames; }
    }
    static readonly DodgeSheet DodgeCharge = new DodgeSheet("EyeDodge/Charge", 5, 5, 25);       // charging glow, plays once
    static readonly DodgeSheet DodgeHitSpark = new DodgeSheet("EyeDodge/Hit", 4, 4, 13);        // catch spark, plays once
    static readonly DodgeSheet DodgeExplosion = new DodgeSheet("EyeDodge/Explosion", 4, 4, 6);  // last catch: only the fiery first 6 cells (the rest is dark smoke)

    const float DodgeChargeLead = 1.2f;     // the eyes charge for the last part of the countdown
    const float DodgeSpeedUpEvery = 10f;    // play seconds between "속도 UP!"
    const float DodgeDangerRadius = 190f;   // a beam closer than this to the gaze shows a warning ring

    AudioSource dodgeSfx, dodgeLoop, dodgeChargeSound;   // one-shots, the beam hum, the charge (cut when the beams fire)
    bool dodgeCharging, dodgeFinalBoom;
    int dodgeSpeedLevel, dodgeScoreThousands;
    float dodgeChargeAt = -100f, dodgeSpeedUpAt = -100f, dodgeScorePopAt = -100f, dodgeShakeUntil = -100f, dodgeShakeSize;

    void ResetDodgeFx()
    {
        dodgeCharging = dodgeFinalBoom = false;
        dodgeSpeedLevel = dodgeScoreThousands = 0;
        dodgeChargeAt = dodgeSpeedUpAt = dodgeScorePopAt = dodgeShakeUntil = -100f;
        StopDodgeSounds();
    }

    // ---- Sounds ----------------------------------------------------------------------------------

    float DodgeVolume => .4f * GameSettings.Sfx;   // same level as NovelAudio's one-shots

    void EnsureDodgeAudio()
    {
        if (dodgeSfx != null) return;
        dodgeSfx = gameObject.AddComponent<AudioSource>();
        dodgeLoop = gameObject.AddComponent<AudioSource>();
        dodgeChargeSound = gameObject.AddComponent<AudioSource>();
        dodgeSfx.playOnAwake = dodgeLoop.playOnAwake = dodgeChargeSound.playOnAwake = false;
        dodgeSfx.spatialBlend = dodgeLoop.spatialBlend = dodgeChargeSound.spatialBlend = 0f;
        dodgeLoop.loop = true;
        dodgeLoop.clip = Resources.Load<AudioClip>("Audio/EyeDodge/Beam");
        dodgeChargeSound.clip = Resources.Load<AudioClip>("Audio/EyeDodge/Charge");
    }

    void PlayDodge(string name, float level = 1f)
    {
        EnsureDodgeAudio();
        AudioClip clip = Resources.Load<AudioClip>("Audio/EyeDodge/" + name);
        if (clip != null) dodgeSfx.PlayOneShot(clip, level * DodgeVolume);
    }

    void StopDodgeSounds()
    {
        if (dodgeSfx != null) dodgeSfx.Stop();
        if (dodgeLoop != null) dodgeLoop.Stop();
        if (dodgeChargeSound != null) dodgeChargeSound.Stop();
    }

    bool DodgeIsCharging(float now) => dodgeActive && (now < dodgeStartsAt ? dodgeStartsAt - now <= DodgeChargeLead : now < dodgeSafeUntil);

    void TickDodgeFx(float now)
    {
        if (!dodgeActive)
        {
            if (dodgeLoop != null && dodgeLoop.isPlaying) dodgeLoop.Stop();
            if (dodgeChargeSound != null && dodgeChargeSound.isPlaying) dodgeChargeSound.Stop();
            return;
        }
        EnsureDodgeAudio();
        // Charging glow and sound when the eyes power up; the sound is cut when the beams fire.
        bool charging = DodgeIsCharging(now);
        if (charging && !dodgeCharging)
        {
            dodgeChargeAt = now;
            dodgeChargeSound.volume = .8f * DodgeVolume;
            if (dodgeChargeSound.clip != null) dodgeChargeSound.Play();
        }
        else if (!charging && dodgeCharging) dodgeChargeSound.Stop();
        dodgeCharging = charging;

        // A low beam hum while the beams fly.
        bool flying = now >= dodgeStartsAt && now >= dodgeSafeUntil && !dodgeLost;
        if (flying && !dodgeLoop.isPlaying && dodgeLoop.clip != null) dodgeLoop.Play();
        else if (!flying && dodgeLoop.isPlaying) dodgeLoop.Stop();
        dodgeLoop.volume = .35f * DodgeVolume;

        int level = Mathf.FloorToInt(dodgeAlive / DodgeSpeedUpEvery);
        if (level > dodgeSpeedLevel)
        {
            dodgeSpeedLevel = level;
            dodgeSpeedUpAt = now;
            PlayDodge("SpeedUp", .7f);
        }
        int thousands = DodgeScore / 1000;
        if (thousands > dodgeScoreThousands)
        {
            dodgeScoreThousands = thousands;
            dodgeScorePopAt = now;
        }
    }

    /// <summary>A catch: spark (or a big explosion on the last one), screen shake and sound.</summary>
    void DodgeFxCatch(bool final)
    {
        dodgeFinalBoom = final;
        dodgeShakeUntil = Time.unscaledTime + (final ? .6f : .35f);
        dodgeShakeSize = final ? 22f : 14f;
        PlayDodge(final ? "Boom" : "Hit");
    }

    // ---- Drawing --------------------------------------------------------------------------------

    Vector3 DodgeShake(float now)
    {
        float left = dodgeShakeUntil - now;
        if (left <= 0f || !GameSettings.Effects) return Vector3.zero;   // "화면 효과 끔"
        float size = dodgeShakeSize * Mathf.Clamp01(left / .35f);
        return new Vector3(Random.Range(-size, size), Random.Range(-size, size), 0f);
    }

    /// <summary>Draws one cell of a sprite sheet into rect, optionally rotated (degrees) around pivot.</summary>
    static void DrawDodgeSheet(DodgeSheet sheet, int frame, Rect rect, Color tint, float angle = 0f, Vector2 pivot = default)
    {
        if (Event.current.type != EventType.Repaint) return;
        Texture2D texture = Load(sheet.Path);
        if (texture == null) return;
        frame = Mathf.Clamp(frame, 0, sheet.Frames - 1);
        int col = frame % sheet.Cols, row = frame / sheet.Cols;
        // Texture coordinates start at the bottom-left, the sheet's first row is at the top.
        Rect uv = new Rect((float)col / sheet.Cols, 1f - (row + 1f) / sheet.Rows, 1f / sheet.Cols, 1f / sheet.Rows);
        Matrix4x4 keep = GUI.matrix;
        if (angle != 0f) GUI.matrix = keep * DodgeRotation(angle, pivot);
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(rect, texture, uv);
        GUI.color = Color.white;
        GUI.matrix = keep;
    }

    /// <summary>Frame of a sheet played once over duration seconds from start; -1 before or after.</summary>
    static int DodgeOnce(DodgeSheet sheet, float now, float start, float duration)
    {
        float t = (now - start) / duration;
        return t < 0f || t >= 1f ? -1 : Mathf.FloorToInt(t * sheet.Frames);
    }

    void DrawDodgeCharge(float now)
    {
        if (!dodgeCharging || !GameSettings.Effects) return;
        float length = now < dodgeStartsAt ? DodgeChargeLead : DodgeSafeSeconds;
        float t = Mathf.Clamp01((now - dodgeChargeAt) / length);
        int frame = Mathf.Min(DodgeCharge.Frames - 1, Mathf.FloorToInt(t * DodgeCharge.Frames));
        float size = Mathf.Lerp(70f, 130f, t);
        for (int i = 0; i < 2; i++)
        {
            Vector2 e = DodgeEye(i);
            DrawDodgeSheet(DodgeCharge, frame, new Rect(e.x - size / 2f, e.y - size / 2f, size, size), new Color(1f, .75f, .7f, .95f));
        }
    }

    /// <summary>
    /// Rotation by angle degrees around pivot, both in the current GUI (canvas) coordinates. GUIUtility.RotateAroundPivot
    /// takes its pivot in screen pixels, so under the scaled canvas matrix it turns around the wrong point (only a
    /// 1280x900 window happens to match).
    /// </summary>
    static Matrix4x4 DodgeRotation(float angle, Vector2 pivot) =>
        Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);

    /// <summary>The beams: a glowing line from each eye to a red dot.</summary>
    void DrawDodgeBeams(float now)
    {
        for (int i = 0; i < 2; i++)
        {
            DodgeLine(DodgeEye(i), dodgeBeam[i], 7f, NovelUI.Alpha(DodgeRed, .32f));
            DodgeLine(DodgeEye(i), dodgeBeam[i], 2.5f, NovelUI.Alpha(Color.white, .5f));
        }
        for (int i = 0; i < 2; i++)
        {
            Vector2 b = dodgeBeam[i];
            NovelUI.Glow(new Rect(b.x - 46, b.y - 46, 92, 92), NovelUI.Alpha(DodgeRed, .9f));
            NovelUI.Fill(new Rect(b.x - 18, b.y - 18, 36, 36), DodgeRed, 18);
            NovelUI.Fill(new Rect(b.x - 7, b.y - 7, 14, 14), new Color(1f, .85f, .85f), 7);
        }
    }

    /// <summary>A pulsing red ring around the gaze while a beam is close.</summary>
    void DrawDodgeDanger(float now)
    {
        if (!dodgeHasGaze || !dodgeActive || now < dodgeStartsAt || now < dodgeSafeUntil) return;
        float nearest = Mathf.Min((dodgeBeam[0] - dodgeGaze).magnitude, (dodgeBeam[1] - dodgeGaze).magnitude);
        float danger = Mathf.Clamp01(1f - (nearest - DodgeHitRadius) / (DodgeDangerRadius - DodgeHitRadius));
        if (danger <= 0f) return;
        float pulse = .5f + .5f * Mathf.Sin(now * 18f);
        float r = 34f + 10f * pulse;
        Vector2 g = dodgeGaze;
        NovelUI.Ring(new Rect(g.x - r, g.y - r, r * 2, r * 2), NovelUI.Alpha(DodgeRed, danger * (.5f + .4f * pulse)), r, 4);
        NovelUI.Glow(new Rect(g.x - 70, g.y - 70, 140, 140), NovelUI.Alpha(DodgeRed, .35f * danger));
    }

    /// <summary>Spark / explosion sprites on a catch; off with "화면 효과" (the ring and "잡혔다!" still show the catch).</summary>
    void DrawDodgeImpact(float now)
    {
        if (!GameSettings.Effects) return;
        Vector2 p = dodgeHitPos;
        if (dodgeFinalBoom)
        {
            float t = (now - dodgeHitAt) / .7f;
            int frame = DodgeOnce(DodgeExplosion, now, dodgeHitAt, .7f);
            if (frame >= 0) DrawDodgeSheet(DodgeExplosion, frame, new Rect(p.x - 230, p.y - 230, 460, 460), new Color(1f, 1f, 1f, t > .6f ? 1f - (t - .6f) / .4f : 1f));
        }
        int spark = DodgeOnce(DodgeHitSpark, now, dodgeHitAt, .55f);
        if (spark >= 0) DrawDodgeSheet(DodgeHitSpark, spark, new Rect(p.x - 150, p.y - 150, 300, 300), Color.white);
    }

    /// <summary>0..1 brightness boost for the eyes right after a speed-up.</summary>
    float DodgeSpeedUpFlash(float now) => Mathf.Clamp01(1f - (now - dodgeSpeedUpAt) / .8f) * .6f;

    void DrawDodgeSpeedUp(float now)
    {
        float t = (now - dodgeSpeedUpAt) / 1.4f;
        if (t < 0f || t >= 1f) return;
        float grow = t < .15f ? Mathf.Lerp(.6f, 1.15f, t / .15f) : Mathf.Lerp(1.15f, 1f, (t - .15f) / .85f);
        float alpha = t > .7f ? 1f - (t - .7f) / .3f : 1f;
        int size = Mathf.RoundToInt(46f * grow);
        NovelUI.Outlined(new Rect(340, 640, 600, 80), "속도 UP!", NovelUI.Round, size, NovelUI.Alpha(DodgeRed, alpha), NovelUI.Alpha(Color.white, alpha), 4);
    }

    void DrawDodgeScore(float now)
    {
        float t = (now - dodgeScorePopAt) / .45f;
        float grow = t >= 0f && t < 1f ? 1f + .35f * Mathf.Sin(t * Mathf.PI) : 1f;
        Color color = t >= 0f && t < 1f ? Color.Lerp(NovelUI.Plum, NovelUI.DeepPink, Mathf.Sin(t * Mathf.PI)) : NovelUI.Plum;
        NovelUI.Label(new Rect(60, 60, 500, 56), $"점수  {DodgeScore}", NovelUI.Round, Mathf.RoundToInt(30 * grow), color, TextAnchor.MiddleLeft, FontStyle.Bold);
    }
}
