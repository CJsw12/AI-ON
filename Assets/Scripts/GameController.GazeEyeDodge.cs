using UnityEngine;

// "눈빛 피하기": two red beams leave the selected professor's eyes and chase the player's gaze (the blue dot). Keep moving your
// eyes to dodge them; three catches end the game. Every 0.1 s survived is worth 10 points, and the beams speed up
// the longer you last. Works with the webcam (UnitEye) and the iPhone through StarGaze.
// The selected professor's smiling portrait stays fixed throughout play and results. The professor talks in a
// speech bubble: "학생?" at the start, a random frown line, and one line per catch (the last one typed slowly).
public partial class GameController
{
    /// <summary>One professor photo with its pupil centres in source pixels, drawn so its eyes sit on the same screen spot.</summary>
    readonly struct DodgeFace
    {
        public readonly string Picture;
        public readonly Rect Screen;                 // where the whole picture is drawn on the 1280×900 canvas
        readonly Vector2 left, right;                // pupils on the canvas
        public DodgeFace(string picture, float width, float height, Vector2 leftEye, Vector2 rightEye)
        {
            Picture = picture;
            // Match the small UV inset used when drawing the texture (source coordinates are top-down).
            leftEye.y = (leftEye.y - height * .002f) / .996f;
            rightEye.y = (rightEye.y - height * .002f) / .996f;
            Vector2 middle = (leftEye + rightEye) / 2f;
            // Same eye spacing as the neutral photo, but never so small that the cut-off torso ends above the screen bottom.
            float scale = Mathf.Max(DodgeEyeSpacing / (rightEye.x - leftEye.x), (900f - DodgeEyeLine.y) / (height - middle.y));
            Screen = new Rect(DodgeEyeLine.x - middle.x * scale, DodgeEyeLine.y - middle.y * scale, width * scale, height * scale);
            left = Screen.position + leftEye * scale;
            right = Screen.position + rightEye * scale;
        }
        public Vector2 Eye(int i) => i == 0 ? left : right;
    }

    static readonly Vector2 DodgeEyeLine = new Vector2(640f, 257f);   // midpoint between the pupils on screen
    const float DodgeEyeSpacing = 60f;
    DodgeFace dodgePortrait;
    string dodgeProfessorName;
    static readonly string[] DodgeFrownLines = { "흠..", "바빠요?", "놓치지 않아!" };
    static readonly string[] DodgeCatchLines = { "커피 한 잔 하죠", "제 강의실로 와주세요", "대학원생 해볼래요?" };

    const int DodgeLives = 3;
    const float DodgeCountdown = 3f;
    const float DodgeStartSpeed = 110f;    // canvas px per second
    const float DodgeSpeedGain = 9f;       // added every second survived
    const float DodgeMaxSpeed = 560f;
    const float DodgeHitRadius = 46f;      // beam centre to gaze point
    const float DodgeSafeSeconds = 1.6f;   // after a catch the beams rest in the eyes and can't catch
    const float DodgeFrownAfter = 5f;      // seconds of play after the start or a catch
    const float DodgeLastLineSeconds = 2f; // the last line types out this slowly before the result appears
    const float DodgeLineHold = 2.2f;      // a finished line stays this long
    const string DodgeBestPref = "GazeTest.EyeDodgeBestScore";
    static readonly Color DodgeRed = new Color(1f, .16f, .2f), DodgeBlue = new Color(.18f, .45f, 1f);

    bool dodgeActive, dodgeFinished, dodgeNewBest, dodgeLost, dodgeFrowned, dodgeResultSounded;
    float dodgeStartsAt, dodgeLastTick, dodgeAlive, dodgeSafeUntil, dodgeHitAt = -100f, dodgeMoodFrom, dodgeResultAt;
    int dodgeLivesLeft, dodgeBest = -1;
    readonly Vector2[] dodgeBeam = new Vector2[2], dodgeBeamVelocity = new Vector2[2];
    Vector2 dodgeGaze, dodgeGazeVelocity, dodgeHitPos;
    bool dodgeHasGaze;
    string dodgeSay = "";
    float dodgeSayAt, dodgeSayTyping, dodgeSayHold;

    bool DodgeScreenOpen => dodgeActive || dodgeFinished;
    int DodgeScore => Mathf.FloorToInt(dodgeAlive * 10f) * 10;
    float DodgeSpeed => Mathf.Min(DodgeMaxSpeed, DodgeStartSpeed + DodgeSpeedGain * dodgeAlive);
    bool DodgeShowingResult => dodgeFinished && Time.unscaledTime >= dodgeResultAt;

    DodgeFace DodgeFaceNow => dodgePortrait;
    Vector2 DodgeEye(int i) => DodgeFaceNow.Eye(i);

    void PrepareDodgePortrait()
    {
        // Story selection owns these fields. With no selection yet, use this faculty's first professor.
        Story story = Stories.All[Mathf.Clamp(faculty, 0, Stories.All.Length - 1)];
        int who = Mathf.Clamp(professor, 0, story.Professors.Length - 1);
        Professor selected = story.Professors[who];
        string picture = Professor.Picture(selected, who, "Happy");
        dodgeProfessorName = selected.Name;
        if (ProfessorDodgeEyes.TryGet(picture, out var eyes))
            dodgePortrait = new DodgeFace(picture, eyes.Width, eyes.Height, eyes.Left, eyes.Right);
        else
        {
            // Keep the selected professor if a new portrait has not had its exact anchors measured yet.
            bool framed = PortraitFaces.Face.TryGetValue(picture, out var face);
            float aspect = framed ? face.Aspect : 1f;
            float x = framed ? face.X * aspect : .5f, y = framed ? face.Y - face.Size * .12f : .2f;
            float half = framed ? face.Size * .18f : .06f;
            dodgePortrait = new DodgeFace(picture, aspect, 1f, new Vector2(x - half, y), new Vector2(x + half, y));
            Debug.LogWarning("[eye dodge] Missing measured pupil anchors for " + picture + "; update ProfessorDodgeEyes.");
        }
    }

    bool dodgeAfterCalibration;   // opened from the title: start the game as soon as the eye tracker is calibrated

    /// <summary>From the gaze test screens, after calibration.</summary>
    void StartGazeDodge() => StartDodgeGame();

    /// <summary>From the title screen: opens the gaze test, starts the webcam, and launches the game once calibrated.</summary>
    void StartDodgeFromTitle()
    {
        novelAudio.Play("Select");
        OpenGazeTest();
        dodgeAfterCalibration = true;
        if (gazeTracker == null && !requestingCamera) gazeCameraRequest = StartCoroutine(StartGazeCamera());
    }

    // iphoneCalibrated survives a disconnect, so a live source is required too.
    bool GazeReadyForDodge => gazeUseIphone ? iphoneSource != null && iphoneCalibrated && !iphoneCalibrating
        : gazeTracker != null && webcamFitted && !webcamFitting;

    /// <summary>"돌아가기" / "게임 끝내기": back to the gaze test screen.</summary>
    void LeaveDodgeGame() => StopDodgeGame();

    void StartDodgeGame()
    {
        StopStarGame();
        PrepareDodgePortrait();
        dodgeAfterCalibration = false;   // started by hand: no second automatic start later
        if (dodgeBest < 0) dodgeBest = PlayerPrefs.GetInt(DodgeBestPref, 0);
        dodgeActive = true;
        dodgeFinished = dodgeNewBest = dodgeLost = dodgeHasGaze = dodgeFrowned = dodgeResultSounded = false;
        dodgeStartsAt = Time.unscaledTime + DodgeCountdown;
        dodgeLastTick = Time.unscaledTime;
        dodgeAlive = dodgeMoodFrom = 0f;
        dodgeLivesLeft = DodgeLives;
        dodgeHitAt = -100f;
        dodgeSafeUntil = 0f;
        dodgeGazeVelocity = Vector2.zero;
        ResetDodgeBeams();
        ResetDodgeFx();
        SayDodge("학생?", -1f, DodgeCountdown + 1f);   // stays through the countdown into the first second of play
    }

    void StopDodgeGame()
    {
        dodgeActive = dodgeFinished = false;
        StopDodgeSounds();
    }

    void ResetDodgeBeams()
    {
        for (int i = 0; i < 2; i++)
        {
            dodgeBeam[i] = DodgeEye(i);
            dodgeBeamVelocity[i] = Vector2.zero;
        }
    }

    /// <summary>Shows a line in the professor's speech bubble, typed out over typingSeconds (default: quick), then held.</summary>
    void SayDodge(string line, float typingSeconds = -1f, float holdSeconds = DodgeLineHold)
    {
        dodgeSay = line;
        dodgeSayAt = Time.unscaledTime;
        dodgeSayTyping = typingSeconds >= 0f ? typingSeconds : line.Length * .06f;
        dodgeSayHold = holdSeconds;
    }

    void TickDodgeGame()
    {
        float now = Time.unscaledTime;
        if (dodgeFinished && !dodgeResultSounded && now >= dodgeResultAt)
        {
            dodgeResultSounded = true;
            NovelUI.Sound?.Invoke(dodgeNewBest ? "Unlock" : "Chapter");
        }
        if (dodgeAfterCalibration && gazeTestOpen && !DodgeScreenOpen && !StarScreenOpen && GazeReadyForDodge)
        {
            dodgeAfterCalibration = false;
            StartGazeDodge();
        }
        TickDodgeFx(now);
        if (!dodgeActive) return;
        float raw = Mathf.Max(0f, now - dodgeLastTick);
        dodgeLastTick = now;
        // Slow frames count in full (score is real time survived); only a stall longer than DodgeMaxFrame
        // (window drag, breakpoint) is skipped like a pause.
        float dt = Mathf.Min(raw, DodgeMaxFrame);

        bool valid = StarGaze(out Vector2 gaze, out _);   // a blink freezes the point where it was, so the beams still come
        dodgeLost = !valid;
        if (valid)
        {
            // Looking off the screen doesn't hide you: the point stays at the edge.
            gaze = new Vector2(Mathf.Clamp(gaze.x, 0f, 1280f), Mathf.Clamp(gaze.y, 0f, 900f));
            if (dodgeHasGaze && raw > 0f)
                dodgeGazeVelocity = Vector2.Lerp(dodgeGazeVelocity, (gaze - dodgeGaze) / raw, 1f - Mathf.Exp(-6f * dt));
            dodgeGaze = gaze;
            dodgeHasGaze = true;
        }
        if (now < dodgeStartsAt) return;   // countdown
        float sinceStart = now - dodgeStartsAt;
        // Without a live gaze the game waits instead of letting the beams catch a frozen point.
        if (!valid)
        {
            PauseDodgeClocks(Mathf.Min(raw, sinceStart));
            return;
        }
        PauseDodgeClocks(raw - dt);
        float played = Mathf.Min(dt, sinceStart);   // the frame that ends the countdown counts only from the start

        // Small fixed steps, so a slow frame neither lets a fast beam jump over the gaze nor changes how it steers.
        int steps = Mathf.Max(1, Mathf.CeilToInt(played / DodgeStep));
        float h = played / steps;
        for (int k = 0; k < steps && dodgeActive; k++)
        {
            float t = now - played + h * (k + 1);
            dodgeAlive += h;
            if (!dodgeFrowned && dodgeAlive >= dodgeMoodFrom + DodgeFrownAfter)
            {
                dodgeFrowned = true;
                SayDodge(DodgeFrownLines[Random.Range(0, DodgeFrownLines.Length)]);
            }
            if (t < dodgeSafeUntil) continue;   // beams rest in the eyes after a catch
            MoveDodgeBeams(h);
            CatchDodge(t);
        }
    }

    const float DodgeMaxFrame = .5f;
    const float DodgeStep = 1f / 60f;

    /// <summary>Holds the real-time clocks (safe time after a catch, the speech bubble) while play is paused.</summary>
    void PauseDodgeClocks(float seconds)
    {
        if (seconds <= 0f) return;
        dodgeSafeUntil += seconds;
        dodgeSayAt += seconds;
    }

    void MoveDodgeBeams(float dt)
    {
        float speed = DodgeSpeed;
        for (int i = 0; i < 2; i++)
        {
            // The second beam aims a little ahead of where the eyes are heading, so the two close in from different sides.
            Vector2 aim = i == 0 ? dodgeGaze : dodgeGaze + Vector2.ClampMagnitude(dodgeGazeVelocity * .35f, 160f);
            Vector2 toAim = aim - dodgeBeam[i];
            Vector2 desired = toAim.sqrMagnitude > 1f ? toAim.normalized * speed * (i == 0 ? 1f : .9f) : Vector2.zero;
            // Limited acceleration: after a quick look away the beam overshoots and has to turn, which is the chance to escape.
            dodgeBeamVelocity[i] = Vector2.MoveTowards(dodgeBeamVelocity[i], desired, speed * 3f * dt);
            Vector2 apart = dodgeBeam[i] - dodgeBeam[1 - i];
            if (apart.sqrMagnitude < 70f * 70f && apart.sqrMagnitude > .01f) dodgeBeamVelocity[i] += apart.normalized * 240f * dt;
            dodgeBeam[i] += dodgeBeamVelocity[i] * dt;
            dodgeBeam[i] = new Vector2(Mathf.Clamp(dodgeBeam[i].x, 0f, 1280f), Mathf.Clamp(dodgeBeam[i].y, 0f, 900f));
        }
    }

    void CatchDodge(float now)
    {
        for (int i = 0; i < 2; i++)
        {
            if ((dodgeBeam[i] - dodgeGaze).sqrMagnitude > DodgeHitRadius * DodgeHitRadius) continue;
            int caught = DodgeLives - dodgeLivesLeft;   // 0 for the first catch
            dodgeLivesLeft--;
            dodgeHitAt = now;
            dodgeHitPos = dodgeGaze;
            dodgeSafeUntil = now + DodgeSafeSeconds;
            dodgeMoodFrom = dodgeAlive;   // smile now, frown again 5 s later
            dodgeFrowned = false;
            DodgeFxCatch(dodgeLivesLeft <= 0);
            if (dodgeLivesLeft <= 0)
            {
                SayDodge(DodgeCatchLines[DodgeCatchLines.Length - 1], DodgeLastLineSeconds);
                FinishDodgeGame();
            }
            else SayDodge(DodgeCatchLines[Mathf.Min(caught, DodgeCatchLines.Length - 2)]);
            ResetDodgeBeams();
            return;
        }
    }

    void FinishDodgeGame()
    {
        dodgeActive = false;
        dodgeFinished = true;
        // The result card and its buttons wait until the last line has been typed out and read.
        dodgeResultAt = Time.unscaledTime + DodgeLastLineSeconds + .6f;
        int score = DodgeScore;
        dodgeNewBest = score > dodgeBest;
        if (dodgeNewBest)
        {
            dodgeBest = score;
            PlayerPrefs.SetInt(DodgeBestPref, dodgeBest);
            PlayerPrefs.Save();
        }
    }

    void DrawDodgeGame()
    {
        float now = Time.unscaledTime;
        Matrix4x4 unshaken = GUI.matrix;
        GUI.matrix = unshaken * Matrix4x4.Translate(DodgeShake(now));
        DrawDodgeScreen(now);
        GUI.matrix = unshaken;
    }

    void DrawDodgeScreen(float now)
    {
        NovelUI.Fill(new Rect(-40, -40, 1360, 980), NovelUI.Cream, 0);
        DodgeFace face = DodgeFaceNow;
        Texture2D picture = Load(face.Picture);
        if (picture != null && Event.current.type == EventType.Repaint)
            // Slightly inset texture coordinates: the repeat-wrapped import bleeds its bottom row into a line along the top edge.
            GUI.DrawTextureWithTexCoords(face.Screen, picture, new Rect(0f, .002f, 1f, .996f));
        NovelUI.Label(new Rect(0, 14, 1280, 52), "눈빛 피하기 · " + dodgeProfessorName, NovelUI.Round, 32, NovelUI.Berry, TextAnchor.MiddleCenter);

        if (DodgeShowingResult)
        {
            NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Alpha(NovelUI.Cream, .55f), 0);
            DrawDodgeEyes(now, 1f);
            DrawDodgeSpeech(now);
            NovelUI.Card(new Rect(340, 330, 600, 350), NovelUI.Alpha(Color.white, .93f), 28, NovelUI.Alpha(NovelUI.DeepPink, .35f));
            NovelUI.Label(new Rect(340, 358, 600, 80), $"{DodgeScore}점", NovelUI.Round, 56, NovelUI.DeepPink, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(340, 434, 600, 30), $"{dodgeAlive:F1}초 버텼어요", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(340, 468, 600, 40), dodgeNewBest ? "새 최고 기록이에요!" : $"최고 기록 {dodgeBest}점",
                NovelUI.Soft, 22, dodgeNewBest ? NovelUI.Berry : NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(340, 510, 600, 30), "눈빛이 바로 따라오면 크게 휙 옮겨 보세요.", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (!storyGazeSession && NovelUI.Button(new Rect(370, 570, 250, 62), "한 판 더", true, 23)) StartDodgeGame();
            if (NovelUI.Button(new Rect(storyGazeSession ? 515 : 660, 570, 250, 62), "돌아가기", false, 23)) LeaveDodgeGame();
            return;
        }

        // Score and lives.
        DrawDodgeScore(now);
        if (dodgeBest > 0)
            NovelUI.Label(new Rect(60, 108, 400, 30), $"최고 기록 {dodgeBest}점", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleLeft);
        for (int i = 0; i < DodgeLives; i++)
            NovelUI.Heart(new Rect(1220 - (DodgeLives - i) * 52, 66, 44, 44), i < dodgeLivesLeft ? NovelUI.DeepPink : NovelUI.Alpha(NovelUI.Plum, .35f), i < dodgeLivesLeft);

        bool counting = now < dodgeStartsAt;
        bool resting = dodgeFinished || (!counting && now < dodgeSafeUntil);
        // The eyes glow brighter as the beams get faster.
        float heat = Mathf.InverseLerp(DodgeStartSpeed, DodgeMaxSpeed, DodgeSpeed);
        DrawDodgeEyes(now, counting ? .55f : .7f + .3f * heat + (GameSettings.Effects ? DodgeSpeedUpFlash(now) : 0f));
        DrawDodgeCharge(now);
        if (!counting && !resting) DrawDodgeBeams(now);
        DrawDodgeDanger(now);

        if (dodgeHasGaze)
        {
            Vector2 g = dodgeGaze;
            float alpha = dodgeLost ? .35f : 1f;
            NovelUI.Fill(new Rect(g.x - 28, g.y - 28, 56, 56), NovelUI.Alpha(DodgeBlue, .22f * alpha), 28);
            NovelUI.Fill(new Rect(g.x - 16, g.y - 16, 32, 32), NovelUI.Alpha(DodgeBlue, alpha), 16);
            NovelUI.Fill(new Rect(g.x - 6, g.y - 6, 12, 12), NovelUI.Alpha(Color.white, alpha), 6);
            // While safe after a catch the dot blinks.
            if (resting && !dodgeFinished && Mathf.Repeat(now * 6f, 1f) < .5f)
                NovelUI.Ring(new Rect(g.x - 30, g.y - 30, 60, 60), NovelUI.Alpha(DodgeBlue, .8f), 30, 3);
        }

        DrawDodgeImpact(now);
        DrawDodgeSpeedUp(now);
        // A catch: red flash, a ring where the gaze was, and "-♥" floating up.
        float hit = (now - dodgeHitAt) / .7f;
        if (hit >= 0f && hit < 1f)
        {
            if (GameSettings.Effects) NovelUI.Fill(NovelUI.Window, NovelUI.Alpha(DodgeRed, .28f * (1f - hit)), 0);
            float r = Mathf.Lerp(40f, 150f, hit);
            Vector2 p = dodgeHitPos;
            NovelUI.Ring(new Rect(p.x - r, p.y - r, r * 2, r * 2), NovelUI.Alpha(DodgeRed, 1f - hit), r, 6);
            NovelUI.Label(new Rect(p.x - 120, p.y - 80 - 60 * hit, 240, 60), "잡혔다! -♥", NovelUI.Round, 36, NovelUI.Alpha(DodgeRed, 1f - hit), TextAnchor.MiddleCenter);
        }

        DrawDodgeSpeech(now);

        if (counting)
        {
            float left = dodgeStartsAt - now;
            float beat = 1f - (left % 1f);
            NovelUI.Card(new Rect(290, 520, 700, 300), NovelUI.Alpha(Color.white, .88f), 26, NovelUI.Alpha(NovelUI.DeepPink, .3f));
            NovelUI.Label(new Rect(290, 536, 700, 50), "교수님 눈빛(빨간 점)을 피해 시선을 옮기세요!", NovelUI.Soft, 24, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(290, 584, 700, 32), "파란 점이 내 시선 · 3번 잡히면 끝 · 0.1초 버틸 때마다 10점", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
            int size = Mathf.RoundToInt(Mathf.Lerp(130f, 96f, beat));
            NovelUI.Label(new Rect(440, 620, 400, 190), Mathf.CeilToInt(left).ToString(), NovelUI.Round, size, NovelUI.Alpha(NovelUI.DeepPink, 1f - beat * .5f), TextAnchor.MiddleCenter);
        }
        else if (dodgeLost && !dodgeFinished)
        {
            NovelUI.Card(new Rect(220, 764, 840, 52), NovelUI.Alpha(Color.white, .92f), 24, NovelUI.Alpha(NovelUI.DeepPink, .35f));
            NovelUI.Label(new Rect(240, 770, 800, 40), gazeUseIphone ? "시선을 잃었어요 · 휴대폰 화면에 얼굴이 보이게 해 주세요 (잠시 멈춤)" : "시선을 잃었어요 · 웹캠에 얼굴이 보이게 해 주세요 (잠시 멈춤)",
                NovelUI.Soft, 18, NovelUI.DeepPink, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
        // No way out while the last line plays; the result card brings the buttons.
        if (!dodgeFinished && NovelUI.Button(new Rect(1030, 820, 200, 48), "게임 끝내기", false, 18)) LeaveDodgeGame();
    }

    /// <summary>The professor's speech bubble beside the head, typed out character by character.</summary>
    void DrawDodgeSpeech(float now)
    {
        if (string.IsNullOrEmpty(dodgeSay)) return;
        float since = now - dodgeSayAt;
        // The last line stays up with the result; the others fade after they've been read.
        if (!dodgeFinished && since > dodgeSayTyping + dodgeSayHold) return;
        float fade = dodgeFinished ? 1f : Mathf.Clamp01((dodgeSayTyping + dodgeSayHold - since) / .3f);
        int shown = dodgeSayTyping <= 0f ? dodgeSay.Length : Mathf.Clamp(Mathf.CeilToInt(dodgeSay.Length * since / dodgeSayTyping), 0, dodgeSay.Length);
        string text = dodgeSay.Substring(0, shown);
        const int size = 28;
        float width = Mathf.Max(120f, NovelUI.Width(dodgeSay, NovelUI.Round, size) + 56f);   // full-line width so the bubble doesn't grow while typing
        Rect bubble = new Rect(790f, 136f, width, 66f);
        NovelUI.Fill(new Rect(760, 204, 18, 18), NovelUI.Alpha(Color.white, .95f * fade), 9);
        NovelUI.Fill(new Rect(740, 222, 14, 14), NovelUI.Alpha(Color.white, .95f * fade), 7);
        NovelUI.Card(bubble, NovelUI.Alpha(Color.white, .95f * fade), 30, NovelUI.Alpha(NovelUI.DeepPink, .45f * fade));
        NovelUI.Label(new Rect(bubble.x + 28, bubble.y, bubble.width - 40, bubble.height), text, NovelUI.Round, size, NovelUI.Alpha(NovelUI.Berry, fade), TextAnchor.MiddleLeft);
    }

    /// <summary>Red glints on both pupils of the picture, pulsing.</summary>
    void DrawDodgeEyes(float now, float strength)
    {
        float pulse = .85f + .15f * Mathf.Sin(now * 7f);
        for (int i = 0; i < 2; i++)
        {
            Vector2 e = DodgeEye(i);
            float glow = 46f * pulse * (.8f + .4f * strength);
            NovelUI.Glow(new Rect(e.x - glow, e.y - glow, glow * 2, glow * 2), NovelUI.Alpha(DodgeRed, strength));
            float inner = glow * .5f;
            NovelUI.Glow(new Rect(e.x - inner, e.y - inner, inner * 2, inner * 2), NovelUI.Alpha(new Color(1f, .55f, .5f), strength));
            NovelUI.Fill(new Rect(e.x - 8, e.y - 8, 16, 16), NovelUI.Alpha(DodgeRed, .95f), 8);
            NovelUI.Fill(new Rect(e.x - 4, e.y - 4, 8, 8), NovelUI.Alpha(Color.white, .95f), 4);
        }
    }

    /// <summary>A straight bar from a to b in canvas coordinates.</summary>
    static void DodgeLine(Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = b - a;
        float length = d.magnitude;
        if (length < 1f || Event.current.type != EventType.Repaint) return;
        Matrix4x4 keep = GUI.matrix;
        GUI.matrix = keep * DodgeRotation(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        NovelUI.Fill(new Rect(a.x, a.y - width / 2f, length, width), color, width / 2f);
        GUI.matrix = keep;
    }
}
