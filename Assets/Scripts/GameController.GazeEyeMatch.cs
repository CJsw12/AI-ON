using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// "눈빛 맞추기": plays a real close-up of 심한뫼 교수님's eyes (IMG_6962.mov, cut into frames under Resources/EyeMatch)
// and shows where those eyes are looking as a pink dot. The direction was extracted offline from the video's irises
// with MediaPipe (ProfSimEyeTrack.txt, one row per video frame). Follow the pink dot with your gaze (the blue dot):
// matching for MatchGoal seconds in total within MatchDuration clears the game and raises 호감도. Holding his gaze
// for MatchShySeconds in one go makes him blush and say "...부끄럽습니다." Works with the webcam and the iPhone (StarGaze).
public partial class GameController
{
    const float MatchCountdown = 3f;
    const float MatchDuration = 25f;     // seconds of play
    const float MatchGoal = 10f;         // seconds matched in total to clear
    const float MatchShySeconds = 3f;    // one unbroken match this long → "부끄럽습니다"
    const float MatchRadius = 130f;      // canvas px between the two dots that still counts as matching
    const float MatchGrace = .25f;       // a brief slip out of the circle doesn't break the streak
    const float MatchVideoFps = 30f;
    const float MatchMaxFrame = .5f;
    const string MatchBestPref = "GazeTest.EyeMatchBestSeconds";
    static readonly Rect MatchVideoRect = new Rect(40f, 96f, 1200f, 400f);   // the eyes, life-size and feathered into the background
    static readonly Rect MatchArea = new Rect(140f, 540f, 1000f, 240f);   // where the pink dot moves
    static readonly Color MatchPink = new Color(1f, .3f, .58f), MatchBlue = new Color(.18f, .45f, 1f);
    static readonly string[] MatchSlipLines = { "어디 보세요?", "집중하세요.", "제 눈 보세요." };

    Texture2D[] matchFrames;
    Vector2[] matchTrack;
    bool matchActive, matchFinished, matchCleared, matchLost, matchHasGaze, matchShyThisStreak, matchResultSounded;
    float matchStartsAt, matchLastTick, matchPlayed, matchHit, matchStreak, matchBestStreak, matchOutFor, matchResultAt;
    float matchShyAt = -100f, matchSlipSaidAt = -100f;
    int matchShyCount, matchAffectionGain;
    float matchBest = -1f;
    Vector2 matchGaze;
    string matchSay = "";
    float matchSayAt, matchSayHold;
    bool matchAfterCalibration;

    // Presentation: particles, the moment the dots meet, and the outro clip before the result card.
    struct MatchParticle { public Vector2 Pos, Vel; public float Born, Life, Spin, Angle, Size; public Color Color; public bool Petal; }
    readonly List<MatchParticle> matchParticles = new List<MatchParticle>();
    bool matchWasInside;
    float matchEnterAt = -100f, matchOutroAt;
    Texture2D matchVignette, matchBlush;
    const float MatchOutroSeconds = 2.6f;   // the professor's reaction clip plays this long before the result fades in
    const float MatchResultFade = .6f;
    // Frame ranges in IMG_6962 used as reaction clips (30 fps): looking away for a miss, looking straight ahead for a clear.
    const int MatchAwayFrom = 18, MatchAwayTo = 80, MatchFrontFrom = 186, MatchFrontTo = 206;

#if UNITY_EDITOR
    public static bool MatchDevFakeGaze;   // Tools/Eye Match/Fake Gaze On Target: pretend the player follows perfectly
#endif

    bool MatchScreenOpen => matchActive || matchFinished;
    bool MatchOn => matchActive && Time.unscaledTime >= matchStartsAt && !matchLost;

    /// <summary>From the gaze test screens, after calibration.</summary>
    void StartGazeEyeMatch() => StartEyeMatchGame();

    /// <summary>From the title screen: opens the gaze test, starts the webcam, and launches the game once calibrated.</summary>
    void StartEyeMatchFromTitle()
    {
        novelAudio.Play("Select");
        OpenGazeTest();
        matchAfterCalibration = true;
        if (gazeTracker == null && !requestingCamera) gazeCameraRequest = StartCoroutine(StartGazeCamera());
    }

    bool LoadEyeMatchAssets()
    {
        if (matchFrames != null && matchTrack != null) return true;
        var frames = new List<Texture2D>();
        for (int i = 0; ; i++)
        {
            var bytes = Resources.Load<TextAsset>($"EyeMatch/ProfSimEye_{i:000}");
            if (bytes == null) break;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            tex.LoadImage(bytes.bytes, true);
            frames.Add(tex);
            Resources.UnloadAsset(bytes);
        }
        var track = new List<Vector2>();
        var text = Resources.Load<TextAsset>("EyeMatch/ProfSimEyeTrack");
        if (text != null)
            foreach (string line in text.text.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split(',');
                if (p.Length >= 3 && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    track.Add(new Vector2(x, y));
            }
        if (frames.Count == 0 || track.Count == 0)
        {
            Debug.LogError($"[eye match] assets missing: frames={frames.Count} track={track.Count}");
            foreach (var t in frames) Destroy(t);
            return false;
        }
        matchFrames = frames.ToArray();
        matchTrack = track.ToArray();
        return true;
    }

    void StartEyeMatchGame()
    {
        if (!LoadEyeMatchAssets()) return;
        StopStarGame();   // also ends 눈빛 피하기 and any earlier 눈빛 맞추기
        matchAfterCalibration = false;
        if (matchBest < 0f) matchBest = PlayerPrefs.GetFloat(MatchBestPref, 0f);
        matchActive = true;
        matchFinished = matchCleared = matchLost = matchHasGaze = matchShyThisStreak = matchResultSounded = false;
        matchStartsAt = Time.unscaledTime + MatchCountdown;
        matchLastTick = Time.unscaledTime;
        matchPlayed = matchHit = matchStreak = matchBestStreak = matchOutFor = 0f;
        matchShyAt = matchSlipSaidAt = -100f;
        matchShyCount = matchAffectionGain = 0;
        matchParticles.Clear();
        matchWasInside = false;
        matchEnterAt = -100f;
        SayMatch("제 눈을 잘 따라와 보세요.", MatchCountdown + 1f);
    }

    void StopEyeMatchGame() => matchActive = matchFinished = false;

    void SayMatch(string line, float hold = 2.2f)
    {
        matchSay = line;
        matchSayAt = Time.unscaledTime;
        matchSayHold = hold;
    }

    /// <summary>The professor's look target on the canvas at a given play time (loops with the video).</summary>
    Vector2 MatchTarget(float played)
    {
        float f = Mathf.Repeat(played * MatchVideoFps, matchTrack.Length);
        int i = Mathf.FloorToInt(f) % matchTrack.Length;
        Vector2 v = Vector2.Lerp(matchTrack[i], matchTrack[(i + 1) % matchTrack.Length], f - Mathf.Floor(f));
        return new Vector2(MatchArea.center.x + v.x * MatchArea.width / 2f, MatchArea.center.y + v.y * MatchArea.height / 2f);
    }

    Texture2D MatchFrame(float played) => matchFrames[Mathf.FloorToInt(played * MatchVideoFps) % matchFrames.Length];

    void TickEyeMatchGame()
    {
        float now = Time.unscaledTime;
        if (matchFinished && !matchResultSounded && now >= matchResultAt)
        {
            matchResultSounded = true;
            NovelUI.Sound?.Invoke(matchCleared ? "Unlock" : "Chapter");
        }
        if (matchAfterCalibration && gazeTestOpen && !MatchScreenOpen && !DodgeScreenOpen && !StarScreenOpen && GazeReadyForDodge)
            StartGazeEyeMatch();
        if (!matchActive) return;
        float raw = Mathf.Max(0f, now - matchLastTick);
        matchLastTick = now;
        float dt = Mathf.Min(raw, MatchMaxFrame);

        bool valid = StarGaze(out Vector2 gaze, out bool blinking);
#if UNITY_EDITOR
        if (MatchDevFakeGaze && now >= matchStartsAt) { valid = true; blinking = false; gaze = MatchTarget(matchPlayed) + new Vector2(30f, -20f); }
#endif
        matchLost = !valid;
        if (valid)
        {
            matchGaze = new Vector2(Mathf.Clamp(gaze.x, 0f, 1280f), Mathf.Clamp(gaze.y, 0f, 900f));
            matchHasGaze = true;
        }
        if (now < matchStartsAt) return;   // countdown
        if (!valid) { matchSayAt += raw; return; }   // the game (and the video) waits for a live gaze
        float played = Mathf.Min(dt, now - matchStartsAt);
        matchPlayed += played;

        bool inside = !blinking && (matchGaze - MatchTarget(matchPlayed)).sqrMagnitude <= MatchRadius * MatchRadius;
        if (inside && !matchWasInside) matchEnterAt = now;   // the dots just met: ripple + heart
        matchWasInside = inside;
        if (inside || blinking)
        {
            matchOutFor = 0f;
            if (inside)
            {
                matchHit += played;
                matchStreak += played;
                matchBestStreak = Mathf.Max(matchBestStreak, matchStreak);
            }
        }
        else
        {
            matchOutFor += played;
            if (matchOutFor > MatchGrace)
            {
                if (matchStreak >= 1.5f && now - matchSlipSaidAt > 4f && now - matchShyAt > 3f)
                {
                    matchSlipSaidAt = now;
                    SayMatch(MatchSlipLines[Random.Range(0, MatchSlipLines.Length)], 1.6f);
                }
                matchStreak = 0f;
                matchShyThisStreak = false;
            }
        }
        if (!matchShyThisStreak && matchStreak >= MatchShySeconds)
        {
            matchShyThisStreak = true;
            matchShyCount++;
            matchShyAt = now;
            SayMatch("...부끄럽습니다.", 2.6f);
            MatchComboBurst();   // 3 s in a row: petals and sparkles from the screen edges
            NovelUI.Sound?.Invoke("Unlock");
        }
        if (matchHit >= MatchGoal || matchPlayed >= MatchDuration) FinishEyeMatchGame();
    }

    void FinishEyeMatchGame()
    {
        matchActive = false;
        matchFinished = true;
        matchCleared = matchHit >= MatchGoal;
        // Up to +2 like the story minigames: +1 for clearing, +1 more if he got shy at least once.
        matchAffectionGain = matchCleared ? 1 + (matchShyCount > 0 ? 1 : 0) : 0;
        // The reaction clip plays first (looking away with a sigh, or a blush), then the result card fades in.
        matchOutroAt = Time.unscaledTime;
        matchResultAt = matchOutroAt + MatchOutroSeconds;
        matchResultBurst = false;
        if (matchCleared) matchShyAt = matchOutroAt + .3f;   // blush over the straight-ahead clip
        SayMatch(matchCleared ? (matchShyCount > 0 ? "...이번만 봐 드리는 겁니다." : "나쁘지 않네요.") : "...하아. 다음엔 더 집중하세요.", 99f);
        if (matchHit > matchBest)
        {
            matchBest = matchHit;
            PlayerPrefs.SetFloat(MatchBestPref, matchBest);
            PlayerPrefs.Save();
        }
        Debug.Log($"[eye match] cleared={matchCleared} matched={matchHit:F1}s played={matchPlayed:F1}s bestStreak={matchBestStreak:F1}s shy={matchShyCount} affection+{matchAffectionGain}");
    }

    // ---- Effects ------------------------------------------------------------------------------------

    void SpawnMatchParticle(Vector2 pos, Vector2 vel, float life, bool petal, Color color, float size)
    {
        if (!GameSettings.Effects || matchParticles.Count > 220) return;
        matchParticles.Add(new MatchParticle { Pos = pos, Vel = vel, Born = Time.unscaledTime, Life = life, Petal = petal, Color = color,
            Size = size, Angle = Random.Range(0f, 360f), Spin = Random.Range(-220f, 220f) });
    }

    /// <summary>3 s combo: cherry petals drift in from all four edges, sparkles pop along them.</summary>
    void MatchComboBurst()
    {
        Color[] petals = { new Color(1f, .72f, .82f), new Color(1f, .82f, .88f), new Color(1f, .6f, .75f) };
        for (int i = 0; i < 46; i++)
        {
            int side = i % 4;
            Vector2 p = side == 0 ? new Vector2(Random.Range(0f, 1280f), -10f) : side == 1 ? new Vector2(Random.Range(0f, 1280f), 910f)
                : side == 2 ? new Vector2(-10f, Random.Range(0f, 900f)) : new Vector2(1290f, Random.Range(0f, 900f));
            Vector2 inward = (new Vector2(640f, 450f) - p).normalized * Random.Range(90f, 260f) + Random.insideUnitCircle * 60f;
            SpawnMatchParticle(p, inward, Random.Range(1.6f, 2.6f), true, petals[i % petals.Length], Random.Range(14f, 24f));
            if (i % 2 == 0)
                SpawnMatchParticle(p + inward * .3f, inward * .5f, Random.Range(.7f, 1.3f), false, new Color(1f, .95f, .7f), Random.Range(10f, 18f));
        }
    }

    /// <summary>Pink burst around a point (the affection line on a clear).</summary>
    void MatchBurstAt(Vector2 at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            SpawnMatchParticle(at + dir * 20f, dir * Random.Range(160f, 380f), Random.Range(.8f, 1.4f), i % 3 != 0,
                i % 3 == 0 ? new Color(1f, .95f, .75f) : new Color(1f, .45f, .68f), Random.Range(12f, 22f));
        }
    }

    void DrawMatchParticles(float now)
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = matchParticles.Count - 1; i >= 0; i--)
        {
            var p = matchParticles[i];
            float age = (now - p.Born) / p.Life;
            if (age >= 1f) { matchParticles.RemoveAt(i); continue; }
            if (NovelUI.Repaint)
            {
                // Petals drift and sway with a little gravity; sparkles slow down.
                p.Vel = p.Petal ? p.Vel * (1f - .6f * dt) + new Vector2(Mathf.Sin(now * 3f + p.Size) * 40f, 30f) * dt : p.Vel * (1f - 2.5f * dt);
                p.Pos += p.Vel * dt;
                p.Angle += p.Spin * dt;
                matchParticles[i] = p;
            }
            float alpha = age < .15f ? age / .15f : 1f - Mathf.InverseLerp(.6f, 1f, age);
            if (p.Petal)
            {
                Matrix4x4 keep = GUI.matrix;
                GUIUtility.RotateAroundPivot(p.Angle, p.Pos);
                NovelUI.Fill(new Rect(p.Pos.x - p.Size / 2f, p.Pos.y - p.Size * .3f, p.Size, p.Size * .6f), NovelUI.Alpha(p.Color, alpha), p.Size * .3f);
                GUI.matrix = keep;
            }
            else
            {
                float s = p.Size * (1f + .4f * Mathf.Sin(now * 18f + p.Angle));
                NovelUI.Glow(new Rect(p.Pos.x - s, p.Pos.y - s, s * 2f, s * 2f), NovelUI.Alpha(p.Color, alpha));
                NovelUI.Fill(new Rect(p.Pos.x - 2.5f, p.Pos.y - 2.5f, 5f, 5f), NovelUI.Alpha(Color.white, alpha), 2.5f);
            }
        }
    }

    /// <summary>Dark-pink edges so the eye goes to the professor's eyes in the middle.</summary>
    void DrawMatchVignette(float strength)
    {
        if (strength <= 0f || !NovelUI.Repaint) return;
        if (matchVignette == null)
        {
            const int n = 128;
            matchVignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * .8f + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1.3f, d));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            matchVignette.SetPixels(px);
            matchVignette.Apply();
        }
        GUI.color = new Color(.42f, .06f, .2f, .62f * strength);
        GUI.DrawTexture(NovelUI.Window, matchVignette, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    /// <summary>A soft red-pink blob on a cheek (procedural radial falloff).</summary>
    void DrawMatchBlush(Rect r, float strength)
    {
        if (!NovelUI.Repaint || strength <= 0f) return;
        if (matchBlush == null)
        {
            const int n = 64;
            matchBlush = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Exp(-(dx * dx + dy * dy) * 3.2f) * Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)));
                }
            matchBlush.SetPixels(px);
            matchBlush.Apply();
        }
        GUI.color = new Color(1f, .28f, .4f, .55f * strength);
        GUI.DrawTexture(r, matchBlush, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    /// <summary>A heartbeat curve: a strong thump and a softer second one, 0..1.</summary>
    static float MatchThump(float phase) => Mathf.Exp(-phase * 14f) + .6f * (phase > .22f ? Mathf.Exp(-(phase - .22f) * 14f) : 0f);

    /// <summary>A grey heart with a zigzag crack, for "no change".</summary>
    static void DrawCrackedHeart(Rect r)
    {
        NovelUI.Heart(r, new Color(.62f, .58f, .6f), true);
        Vector2[] crack = { new Vector2(.5f, .16f), new Vector2(.42f, .38f), new Vector2(.56f, .52f), new Vector2(.45f, .72f), new Vector2(.5f, .9f) };
        for (int i = 0; i + 1 < crack.Length; i++)
        {
            Vector2 a = new Vector2(r.x + crack[i].x * r.width, r.y + crack[i].y * r.height);
            Vector2 b = new Vector2(r.x + crack[i + 1].x * r.width, r.y + crack[i + 1].y * r.height);
            DodgeLine(a, b, 3.5f, NovelUI.Cream);
        }
    }

    // ---- Drawing ------------------------------------------------------------------------------------

    /// <summary>The frame to show: the play clock during the game, then the reaction clip once it ends.</summary>
    Texture2D MatchShownFrame(float now, bool counting)
    {
        if (!matchFinished) return MatchFrame(counting ? 0f : matchPlayed);
        int from = matchCleared ? MatchFrontFrom : MatchAwayFrom, to = matchCleared ? MatchFrontTo : MatchAwayTo;
        int length = Mathf.Max(1, to - from);
        float t = (now - matchOutroAt) * MatchVideoFps;
        // The miss clip plays once and holds the looked-away frame; the clear clip ping-pongs so the gaze stays on you.
        int k = matchCleared ? Mathf.RoundToInt(Mathf.PingPong(t, length)) : Mathf.Min(Mathf.FloorToInt(t), length);
        return matchFrames[Mathf.Clamp(from + k, 0, matchFrames.Length - 1)];
    }

    void DrawEyeMatchGame()
    {
        float now = Time.unscaledTime;
        NovelUI.Fill(new Rect(-40, -40, 1360, 980), NovelUI.Cream, 0);
        NovelUI.Label(new Rect(0, 12, 1280, 48), "눈빛 맞추기 · 심한뫼 교수님", NovelUI.Round, 32, NovelUI.Berry, TextAnchor.MiddleCenter);

        bool counting = matchActive && now < matchStartsAt;
        if (NovelUI.Repaint) GUI.DrawTexture(MatchVideoRect, MatchShownFrame(now, counting), ScaleMode.StretchToFill);
        float shy = matchFinished && matchCleared ? Mathf.Clamp01((now - matchShyAt) / .6f) : Mathf.Clamp01(1f - (now - matchShyAt) / 2.6f);
        if (shy > 0f && GameSettings.Effects)
        {
            // Blush on both cheeks under the eyes.
            DrawMatchBlush(new Rect(MatchVideoRect.x + 190, MatchVideoRect.y + 235, 340, 150), shy);
            DrawMatchBlush(new Rect(MatchVideoRect.x + 670, MatchVideoRect.y + 235, 340, 150), shy);
        }

        // The vignette closes in once play starts and eases off when the result card arrives.
        float vignette = matchFinished ? 1f - Mathf.Clamp01((now - matchResultAt) / MatchResultFade) * .5f
            : counting ? 0f : Mathf.Clamp01((now - matchStartsAt) / 1.2f);
        if (GameSettings.Effects) DrawMatchVignette(vignette);

        if (matchFinished)
        {
            if (now < matchResultAt && now - matchOutroAt > .5f)
                NovelUI.Label(new Rect(0, MatchVideoRect.yMax - 10, 1280, 40), matchCleared ? "(볼이 빨개진다)" : "(시선을 돌리며 한숨)", NovelUI.Soft, 22,
                    NovelUI.Alpha(matchCleared ? NovelUI.DeepPink : NovelUI.Plum, Mathf.Clamp01((now - matchOutroAt - .5f) / .4f)), TextAnchor.MiddleCenter, FontStyle.Bold);
            DrawEyeMatchSpeech(now);
            if (now >= matchResultAt) DrawEyeMatchResult(now);
            DrawMatchParticles(now);
            return;
        }

        // Progress toward the goal; the heart beats faster as time runs out.
        float progress = Mathf.Clamp01(matchHit / MatchGoal);
        Rect bar = new Rect(240, 66, 800, 18);
        NovelUI.Fill(bar, NovelUI.Alpha(NovelUI.Plum, .18f), 11);
        if (progress > 0f) NovelUI.Fill(new Rect(bar.x, bar.y, bar.width * progress, bar.height), NovelUI.DeepPink, 11);
        float bpm = counting ? 70f : Mathf.Lerp(70f, 160f, Mathf.Clamp01(matchPlayed / MatchDuration));
        float thump = Mathf.Clamp01(MatchThump(Mathf.Repeat(now * bpm / 60f, 1f)));
        float hs = 36f * (1f + .3f * thump);
        Vector2 hc = new Vector2(bar.x - 22, bar.y + 9);
        NovelUI.Glow(new Rect(hc.x - hs, hc.y - hs, hs * 2, hs * 2), NovelUI.Alpha(MatchPink, .4f * thump));
        NovelUI.Heart(new Rect(hc.x - hs / 2, hc.y - hs / 2, hs, hs), NovelUI.Alpha(NovelUI.DeepPink, .55f + .45f * thump), true);
        NovelUI.Label(new Rect(bar.xMax + 12, bar.y - 6, 220, 34), $"{Mathf.Max(0f, MatchDuration - matchPlayed):F0}초 남음", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);

        if (!counting && matchActive)
        {
            Vector2 t = MatchTarget(matchPlayed);
            bool on = matchHasGaze && !matchLost && (matchGaze - t).sqrMagnitude <= MatchRadius * MatchRadius;
            NovelUI.Ring(new Rect(t.x - MatchRadius, t.y - MatchRadius, MatchRadius * 2, MatchRadius * 2), NovelUI.Alpha(MatchPink, on ? .9f : .35f), MatchRadius, on ? 5 : 3);
            if (on)
            {
                // Matching: light spreads out in rings and the two dots become one beating heart.
                for (int k = 0; k < 3; k++)
                {
                    float ph = Mathf.Repeat(now * .9f + k / 3f, 1f);
                    float r = Mathf.Lerp(30f, MatchRadius * 1.6f, ph);
                    NovelUI.Ring(new Rect(t.x - r, t.y - r, r * 2, r * 2), NovelUI.Alpha(MatchPink, .55f * (1f - ph)), r, 3);
                }
                float enter = Mathf.Clamp01((now - matchEnterAt) / .45f);
                float er = Mathf.Lerp(20f, 160f, enter);
                if (enter < 1f) NovelUI.Ring(new Rect(t.x - er, t.y - er, er * 2, er * 2), NovelUI.Alpha(Color.white, 1f - enter), er, 6);
                float beat = Mathf.Clamp01(MatchThump(Mathf.Repeat(now * 1.6f, 1f)));
                float g = 120f + 40f * beat;
                NovelUI.Glow(new Rect(t.x - g, t.y - g, g * 2, g * 2), NovelUI.Alpha(MatchPink, .55f));
                float s = 64f * (1f + .25f * beat) * Mathf.Lerp(.4f, 1f, Mathf.Min(1f, enter * 2f));
                NovelUI.Heart(new Rect(t.x - s / 2f, t.y - s / 2f, s, s), MatchPink, true);
                if (matchStreak > .6f)
                    NovelUI.Label(new Rect(t.x - 120, t.y - MatchRadius - 44, 240, 40), $"{matchStreak:F1}초", NovelUI.Round, 28, MatchPink, TextAnchor.MiddleCenter);
            }
            else
            {
                float pulse = .5f + .5f * Mathf.Sin(now * 6f);
                NovelUI.Glow(new Rect(t.x - 46, t.y - 46, 92, 92), NovelUI.Alpha(MatchPink, .6f + .3f * pulse));
                NovelUI.Fill(new Rect(t.x - 16, t.y - 16, 32, 32), MatchPink, 16);
                NovelUI.Fill(new Rect(t.x - 6, t.y - 6, 12, 12), Color.white, 6);
            }
            if (matchHasGaze && !on)
            {
                Vector2 g = matchGaze;
                float alpha = matchLost ? .35f : 1f;
                NovelUI.Fill(new Rect(g.x - 26, g.y - 26, 52, 52), NovelUI.Alpha(MatchBlue, .22f * alpha), 26);
                NovelUI.Fill(new Rect(g.x - 14, g.y - 14, 28, 28), NovelUI.Alpha(MatchBlue, alpha), 14);
                NovelUI.Fill(new Rect(g.x - 5, g.y - 5, 10, 10), NovelUI.Alpha(Color.white, alpha), 5);
            }
        }

        DrawMatchParticles(now);
        DrawEyeMatchSpeech(now);

        if (counting)
        {
            float left = matchStartsAt - now;
            float beat = 1f - (left % 1f);
            NovelUI.Card(new Rect(290, 520, 700, 300), NovelUI.Alpha(Color.white, .9f), 26, NovelUI.Alpha(NovelUI.DeepPink, .3f));
            NovelUI.Label(new Rect(290, 536, 700, 50), "교수님이 보는 곳(분홍 점)을 시선으로 따라가세요!", NovelUI.Soft, 24, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(290, 584, 700, 32), $"파란 점이 내 시선 · {MatchDuration:F0}초 안에 {MatchGoal:F0}초 맞추면 클리어 · {MatchShySeconds:F0}초 연속이면?", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
            int size = Mathf.RoundToInt(Mathf.Lerp(130f, 96f, beat));
            NovelUI.Label(new Rect(440, 620, 400, 190), Mathf.CeilToInt(left).ToString(), NovelUI.Round, size, NovelUI.Alpha(NovelUI.DeepPink, 1f - beat * .5f), TextAnchor.MiddleCenter);
        }
        else if (matchLost && matchActive)
        {
            NovelUI.Card(new Rect(220, 790, 840, 52), NovelUI.Alpha(Color.white, .92f), 24, NovelUI.Alpha(NovelUI.DeepPink, .35f));
            NovelUI.Label(new Rect(240, 796, 800, 40), gazeUseIphone ? "시선을 잃었어요 · 휴대폰 화면에 얼굴이 보이게 해 주세요 (잠시 멈춤)" : "시선을 잃었어요 · 웹캠에 얼굴이 보이게 해 주세요 (잠시 멈춤)",
                NovelUI.Soft, 18, NovelUI.DeepPink, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
        if (NovelUI.Button(new Rect(1030, 836, 200, 48), "게임 끝내기", false, 18)) StopEyeMatchGame();
    }

    bool matchResultBurst;

    /// <summary>The result card: fades and rises in after the reaction clip; on a clear the affection line bounces with a pink burst.</summary>
    void DrawEyeMatchResult(float now)
    {
        float since = now - matchResultAt;
        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(since / MatchResultFade));
        float lift = (1f - a) * 30f;
        if (matchCleared && !matchResultBurst && since >= MatchResultFade * .5f)
        {
            matchResultBurst = true;
            MatchBurstAt(new Vector2(640f, 613f), 36);
        }

        Rect card = new Rect(340, 500 + lift, 600, 350);
        // Drawn here instead of NovelUI.Card so the drop shadow fades with the card.
        NovelUI.Fill(new Rect(card.x, card.y + 6, card.width, card.height), new Color(.3f, .05f, .15f, .18f * a), 28);
        NovelUI.Fill(card, NovelUI.Alpha(Color.white, .95f * a), 28);
        NovelUI.Ring(card, NovelUI.Alpha(NovelUI.DeepPink, .35f * a), 28, 3);
        NovelUI.Label(new Rect(340, 524 + lift, 600, 70), matchCleared ? "클리어!" : "아쉬워요", NovelUI.Round, 52, NovelUI.Alpha(matchCleared ? NovelUI.DeepPink : NovelUI.Plum, a), TextAnchor.MiddleCenter);

        // Affection line with its icon: a pink heart that bounces in with the text, or a grey cracked heart.
        string line = gazeSession != GazeGame.None && !gazeSessionStory ? (matchCleared ? "성공! (연습이라 호감도는 그대로예요)" : "아쉬워요! (연습)")
            : matchCleared ? $"호감도 +{matchAffectionGain}" : "호감도 변화 없음";
        float w = NovelUI.Width(line, NovelUI.Soft, 24) * 1.15f + 16f;   // bold is wider than the measured regular text
        Vector2 center = new Vector2(640f + 22f, 613f + lift);
        Matrix4x4 keep = GUI.matrix;
        if (matchCleared)
        {
            float t = Mathf.Max(0f, since - MatchResultFade * .5f);
            float bounce = 1f + .35f * Mathf.Exp(-t * 4f) * Mathf.Abs(Mathf.Sin(t * 11f));
            GUIUtility.ScaleAroundPivot(new Vector2(bounce, bounce), center);
        }
        Rect icon = new Rect(center.x - w / 2f - 46f, center.y - 18f, 36f, 36f);
        if (matchCleared) NovelUI.Heart(icon, NovelUI.Alpha(NovelUI.DeepPink, a), true);
        else if (a > .9f) DrawCrackedHeart(icon);
        NovelUI.Label(new Rect(center.x - w / 2f, center.y - 18f, w + 40f, 36f), line, NovelUI.Soft, 24,
            NovelUI.Alpha(matchCleared ? NovelUI.DeepPink : NovelUI.Plum, a), TextAnchor.MiddleLeft, FontStyle.Bold);
        GUI.matrix = keep;

        NovelUI.Label(new Rect(340, 646 + lift, 600, 30), $"눈빛 맞춘 시간 {matchHit:F1}초 / 목표 {MatchGoal:F0}초 · 최장 {matchBestStreak:F1}초 연속", NovelUI.Soft, 17, NovelUI.Alpha(NovelUI.Plum, a), TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(340, 678 + lift, 600, 30), matchShyCount > 0 ? $"교수님이 {matchShyCount}번 부끄러워했어요" : $"{MatchShySeconds:F0}초 연속으로 맞추면 교수님이 부끄러워해요",
            NovelUI.Soft, 17, NovelUI.Alpha(NovelUI.Plum, a), TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(340, 708 + lift, 600, 30), $"최고 기록 {Mathf.Max(matchBest, 0f):F1}초", NovelUI.Soft, 16, NovelUI.Alpha(NovelUI.Plum, a), TextAnchor.MiddleCenter);
        if (a < 1f) return;   // the buttons appear once the card has settled
        if (!storyGazeSession && NovelUI.Button(new Rect(370, 770, 250, 62), "한 판 더", true, 23)) StartEyeMatchGame();
        if (NovelUI.Button(new Rect(storyGazeSession ? 515 : 660, 770, 250, 62), "돌아가기", false, 23)) StopEyeMatchGame();
    }

    void DrawEyeMatchSpeech(float now)
    {
        if (string.IsNullOrEmpty(matchSay)) return;
        float since = now - matchSayAt;
        if (since > matchSayHold) return;
        float fade = Mathf.Clamp01((matchSayHold - since) / .3f) * Mathf.Clamp01(since / .15f);
        const int size = 28;
        float width = Mathf.Max(140f, NovelUI.Width(matchSay, NovelUI.Round, size) + 56f);
        Rect bubble = new Rect(MatchVideoRect.xMax - width - 30f, MatchVideoRect.y + 14f, width, 62f);   // in the faded top-right corner of the eyes
        NovelUI.Card(bubble, NovelUI.Alpha(Color.white, .96f * fade), 30, NovelUI.Alpha(NovelUI.DeepPink, .5f * fade));
        NovelUI.Label(new Rect(bubble.x + 28, bubble.y, bubble.width - 40, bubble.height), matchSay, NovelUI.Round, size, NovelUI.Alpha(NovelUI.Berry, fade), TextAnchor.MiddleLeft);
    }
}
