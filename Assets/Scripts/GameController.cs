using System.Text.RegularExpressions;
using UnityEngine;

public partial class GameController : MonoBehaviour
{
    int scene, affection, faculty, professor = -1;
    bool showResult;
    string resultLine;
    float memorial;
    int dialogueIndex;
    int choiceStage, questions, teamwork, interest, solo;
    int entryStep;
    bool experienced;
    Texture2D previousBackground;
    float backgroundChangedAt;
    NovelAudio novelAudio;
    MiniGame miniGame;
    string typed = "";
    readonly string[] faculties = { "AI-SW융합학부", "스마트시스템공학부", "경영-휴먼라이프학부", "예술-건강학부" };

    // Presentation state: typewriter, auto mode, portrait fade and the affection popup.
    string lineKey;
    float lineStartedAt, typedAt, portraitAlpha, affectionChangedAt = -10;
    int shownAffection, lastDelta;
    float introTime;   // Advances at most 50 ms per frame, so a loading hitch can't skip a line.
    bool introDone, introChimed;
    bool endingRecorded, endingNew;   // The ending screen adds its ending to the book once.
    static readonly Regex Tags = new Regex("<[^>]+>");
    static Matrix4x4 Canvas => NovelUI.Canvas;


    void Awake()
    {
        Application.targetFrameRate = 60;
        GameSettings.Load();
        novelAudio = gameObject.AddComponent<NovelAudio>();
        NovelUI.Sound = novelAudio.Play;
        if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        if (Camera.main == null)
        {
            Camera camera = new GameObject("Background Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .1f, .18f);
        }
    }
    void Update()
    {
        string mood = scene == 5 || (scene == 6 && choiceStage == 0) ? "Crisis" : scene == 0 || scene == 1 || scene == 7 || (scene == 2 && choiceStage == 1) || (scene == 6 && choiceStage == 1) ? "Campus" : "Study";
        novelAudio.SetMood(mood);
    }
    string ProfessorName { get { return professor == 0 ? "김가람 교수" : "이명희 교수"; } }
    string ChapterName => ChapterTitle(scene);
    static string ChapterTitle(int chapter) => chapter == 1 ? "입학 OT" : chapter == 2 ? "첫 수업" : chapter == 3 ? "첫 실습" : chapter == 4 ? "오피스아워" : chapter == 5 ? "발표 전날" : "함께 마무리하기";
    Texture2D Portrait
    {
        get
        {
            string character = professor == 0 ? "ProfessorKim" : "ProfessorLee";
            string face = ReactionFace;
            if (face != null) character += face;
            else if (affection < 0) character += "Frown";
            else if (affection >= 3) character += "Happy";
            return Resources.Load<Texture2D>(character + "Transparent");
        }
    }
    Texture2D Background
    {
        get
        {
            if (scene == 1 && choiceStage == 1) return Resources.Load<Texture2D>("Classroom");
            if (scene == 2) return Resources.Load<Texture2D>(choiceStage == 1 ? "Cafeteria" : "Classroom");
            if (scene == 3) return Resources.Load<Texture2D>("Classroom");
            if (scene == 4 && choiceStage == 0 && dialogueIndex < 6) return Resources.Load<Texture2D>("Cafeteria");
            if (scene == 6 && choiceStage == 1 && dialogueIndex >= 1) return Resources.Load<Texture2D>("Auditorium");
            if (scene <= 1 || scene == 7) return Resources.Load<Texture2D>("CampusExterior");
            if (scene == 2) return Resources.Load<Texture2D>("CampusHallway");
            return Resources.Load<Texture2D>("ComputerLab");
        }
    }

    void DrawScene(bool portrait = true)
    {
        Texture2D background = Background;
        if (previousBackground != background) { previousBackground = background; backgroundChangedAt = Time.unscaledTime; }
        GUI.DrawTexture(Drift(new Rect(0, 0, Screen.width, Screen.height)), background, ScaleMode.ScaleAndCrop);
        // Choice results are the professor's replies, so the portrait is always shown there.
        bool showProfessor = portrait && (showResult || FirstSemesterStory.ProfessorOnStage(scene, choiceStage, dialogueIndex));
        if (NovelUI.Repaint) portraitAlpha = Mathf.MoveTowards(portraitAlpha, showProfessor ? 1 : 0, Time.unscaledDeltaTime * 4);
        if (portraitAlpha > 0)
        {
            GUI.color = new Color(1, 1, 1, portraitAlpha);
            GUI.DrawTexture(PortraitRect(), Portrait, ScaleMode.ScaleAndCrop);
            GUI.color = Color.white;
        }
        float fade = 1f - Mathf.Clamp01((Time.unscaledTime - backgroundChangedAt) / .45f);
        if (fade > 0)
        {
            GUI.color = new Color(1f, .95f, .97f, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    void OnGUI()
    {
        // Fixed canvas prevents dialogue and three-choice questions falling below short windows.
        GUI.matrix = Matrix4x4.identity;
        NovelUI.Load();
        NovelUI.MakeStyles();
        GUI.color = new Color(.08f, .1f, .18f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;

        // With a menu open the screen below still draws but gets no input: its events are turned into Ignore.
        bool open = menu != Menu.None;
        EventType type = Event.current.type;
        if (open && type != EventType.Repaint && type != EventType.Layout) Event.current.type = EventType.Ignore;
        NovelUI.Blocked = open;
        DrawScreen();
        NovelUI.Blocked = false;
        if (open && Event.current.type == EventType.Ignore) Event.current.type = type;

        GUI.matrix = Canvas;
        GUI.enabled = true;
        if (menu != Menu.None) DrawMenu();
        else if (InGame && miniGame == null && Pressed(KeyCode.Escape)) OpenMenu(Menu.Settings);
        Toast();
        NovelUI.EndFrame();
    }
    bool InGame => scene >= 1 && scene <= 6 && memorial <= 0 && introDone;

    void DrawScreen()
    {
        if (memorial > 0) { Memorial(); return; }
        if (!introDone) { Intro(); return; }
        if (scene == 0) { StartScreen(); return; }
        if (scene == 7) { Ending(); return; }

        DrawScene();
        GUI.matrix = Canvas;
        TrackAffection();
        if (miniGame != null)
        {
            DrawMiniGame();
            // Settings stay reachable from the corner outside the minigame panel; saving waits until it ends.
            if (NovelUI.IconButton(new Rect(1228, 10, 44, 44), "IconSettings", null)) OpenMenu(Menu.Settings);
            return;
        }
        Hud();
        Dialogue();
        Particles();
        ChapterCard();
    }

    // ---- In-game screen -----------------------------------------------------------------------

    void TrackAffection()
    {
        if (shownAffection == affection) return;
        lastDelta = affection - shownAffection;
        shownAffection = affection;
        affectionChangedAt = Time.unscaledTime;
    }

    void Hud()
    {
        string slot = ChapterQuizzes.Find(scene, choiceStage, experienced) != null ? "문제 " : MiniGames.Has(scene, choiceStage) ? "미니게임 " : "선택 ";
        float x = 28;
        x += NovelUI.Pill(x, 24, 42, "CHAPTER " + scene, NovelUI.Pink, Color.white, 20) + 8;
        x += NovelUI.Pill(x, 24, 42, ChapterName, NovelUI.Alpha(NovelUI.Cream, .95f), NovelUI.Berry, 20) + 8;
        NovelUI.Pill(x, 29, 32, slot + ((scene - 1) * 2 + choiceStage + 1) + "/12", NovelUI.Alpha(NovelUI.Berry, .7f), NovelUI.Blush, 16);

        float since = Time.unscaledTime - affectionChangedAt;
        float pulse = lastDelta > 0 ? 1 - Mathf.Clamp01(since / .6f) : 0;
        NovelUI.HeartGauge(new Rect(860, 24, 200, 42), affection, 20, pulse);
        if (since < 1.6f && lastDelta != 0)
        {
            float k = since / 1.6f;
            string text = (lastDelta > 0 ? "+" : "") + lastDelta + (lastDelta > 0 ? " ♥" : "");
            Color color = NovelUI.Alpha(lastDelta > 0 ? NovelUI.DeepPink : NovelUI.Plum, 1 - k * k);
            NovelUI.Outlined(new Rect(850, 72 + k * 36, 120, 44), text, NovelUI.Round, 30, color, NovelUI.Alpha(Color.white, 1 - k * k), 3);
        }

        if (NovelUI.IconButton(new Rect(1072, 23, 44, 44), "IconSave", "저장 · 불러오기")) OpenMenu(Menu.SaveSlots);
        if (NovelUI.IconButton(new Rect(1126, 23, 44, 44), "IconAuto", GameSettings.Auto ? "자동 진행 끄기" : "자동 진행", GameSettings.Auto))
        {
            GameSettings.Auto = !GameSettings.Auto;
            GameSettings.Save();
            novelAudio.Play("Toggle");
            ShowToast(GameSettings.Auto ? "자동 진행을 켰어요" : "자동 진행을 껐어요");
        }
        if (NovelUI.IconButton(new Rect(1180, 23, 44, 44), "IconSettings", "설정")) OpenMenu(Menu.Settings);
    }

    void CurrentLine(out string speaker, out string text)
    {
        if (showResult) { speaker = ProfessorName; text = resultLine; return; }
        if (choiceStage == 1)
        {
            // Follow-up lines are narration, but some open with "동기: " or "교수님: ".
            speaker = "나레이션";
            text = ProfessorPersonality.FollowUp(professor, scene, dialogueIndex);
            int colon = text.IndexOf(": ");
            if (colon > 0 && colon <= 3)
            {
                string who = text.Substring(0, colon);
                if (who == "동기" || who == "나" || who == "선배" || who == "교수님") { speaker = who == "교수님" ? ProfessorName : who; text = text.Substring(colon + 2); }
            }
            return;
        }
        string[] current = FirstSemesterStory.Dialogue[scene];
        string[] line = current[Mathf.Min(dialogueIndex, current.Length - 1)].Split('|');
        speaker = line[0] == "교수" ? ProfessorName : line[0];
        text = line[0] == "교수" ? ProfessorPersonality.Spoken(professor, line[1]) : line[1];
    }
    bool AtChoice => !showResult && dialogueIndex >= (choiceStage == 0 ? FirstSemesterStory.Dialogue[scene].Length : FirstSemesterStory.FollowUp[scene].Split('\n').Length) - 1;

    void Dialogue()
    {
        CurrentLine(out string speaker, out string text);
        string key = scene + "/" + choiceStage + "/" + dialogueIndex + "/" + showResult;
        if (key != lineKey) { lineKey = key; lineStartedAt = Time.unscaledTime; typedAt = -1; }
        string plain = Tags.Replace(text, "");
        int shown = Mathf.FloorToInt((Time.unscaledTime - lineStartedAt) * GameSettings.LettersPerSecond);
        bool typing = shown < plain.Length;
        if (!typing && typedAt < 0) typedAt = Time.unscaledTime;

        Rect box = new Rect(70, 628, 1140, 244);
        NovelUI.Card(box, NovelUI.Alpha(NovelUI.Cream, .96f), 30, NovelUI.Pink);
        bool narration = speaker == "나레이션";
        if (!narration)
        {
            Color tag = speaker == ProfessorName ? NovelUI.DeepPink : speaker == "나" ? NovelUI.Sky : speaker == "동기" ? NovelUI.Mint : NovelUI.Lemon;
            NovelUI.Pill(box.x + 34, box.y - 26, 52, speaker, tag, speaker == ProfessorName || speaker == "나" ? Color.white : NovelUI.Berry, 24);
        }
        Rect textRect = new Rect(box.x + 42, box.y + 46, box.width - 100, box.height - 64);
        int size = NovelUI.Height(text, NovelUI.Soft, 26, textRect.width) > textRect.height ? 22 : 26;
        NovelUI.Label(textRect, typing ? plain.Substring(0, shown) : text, NovelUI.Soft, size, narration ? NovelUI.Plum : NovelUI.Berry, TextAnchor.UpperLeft, FontStyle.Bold);

        bool canAdvance = showResult || !AtChoice;
        if (!typing && canAdvance)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 5) * 4;
            NovelUI.Heart(new Rect(box.xMax - 58, box.yMax - 46 + bob, 28, 28), NovelUI.DeepPink);
        }

        BackButton(box);
        if (!typing && AtChoice) Choices();
        // Click the box, or press Space/Enter, to finish the line or move on.
        bool pressed = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if ((GUI.Button(box, GUIContent.none, GUIStyle.none) || pressed) && (typing || canAdvance))
        {
            if (pressed) Event.current.Use();
            Advance(typing);
        }
        // Auto mode waits on the finished line, then moves on; it stops at choices and while a menu is open.
        else if (GameSettings.Auto && menu == Menu.None && !typing && canAdvance && NovelUI.Repaint && Time.unscaledTime - typedAt > GameSettings.AutoWait(plain.Length))
            Advance(false);
    }

    void Advance(bool typing)
    {
        if (typing) { lineStartedAt = -1000; return; }
        novelAudio.Play("Advance");
        if (!showResult) { dialogueIndex++; return; }
        showResult = false;
        if (choiceStage == 0) { choiceStage = 1; dialogueIndex = 0; }
        else { choiceStage = dialogueIndex = 0; scene++; }
    }

    // Steps back one line. Never crosses a choice: once chosen, its affection change is final.
    void BackButton(Rect box)
    {
        bool canGoBack = !showResult && (dialogueIndex > 0 || (scene == 1 && choiceStage == 0));
        if (!canGoBack || !NovelUI.Button(new Rect(box.xMax - 150, box.y - 22, 110, 44), "이전", false, 19)) return;
        novelAudio.Play("Back");
        if (dialogueIndex > 0) dialogueIndex--;
        else { scene = 0; professor = -1; }
    }

    void Choices()
    {
        const float bottom = 584;
        if (MiniGames.Has(scene, choiceStage))
        {
            Rect card = new Rect(290, bottom - 190, 700, 176);
            NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 32, NovelUI.Pink);
            float width = NovelUI.Width("미니게임", NovelUI.Round, 18) + 36;
            NovelUI.Pill(card.center.x - width / 2, card.y - 18, 36, "미니게임", NovelUI.Lemon, NovelUI.Berry, 18);
            NovelUI.Label(new Rect(card.x, card.y + 24, card.width, 50), MiniGames.Title(scene), NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(card.center.x - 130, card.y + 90, 260, 60), "도전하기!", true, 26)) { novelAudio.Play("Select"); miniGame = MiniGames.Create(scene, professor); }
            return;
        }
        Quiz quiz = ChapterQuizzes.Find(scene, choiceStage, experienced);
        if (quiz != null && quiz.ShortAnswer)
        {
            Prompt(quiz, bottom - 92);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            Rect field = new Rect(250, bottom - 66, 580, 62);
            NovelUI.Card(field, Color.clear, 31);
            NovelUI.Ring(field, NovelUI.Pink, 31, 3);
            GUI.SetNextControlName("Answer");
            typed = GUI.TextField(field, typed, 40, NovelUI.Input);
            GUI.FocusControl("Answer");
            if (typed == "") NovelUI.Label(new Rect(field.x + 28, field.y, 400, field.height), "정답을 입력하세요", NovelUI.Soft, 20, NovelUI.Alpha(NovelUI.Plum, .6f), TextAnchor.MiddleLeft);
            GUI.enabled = typed.Trim() != "";
            if (NovelUI.Button(new Rect(850, bottom - 66, 180, 62), "제출", true, 24) || (enter && GUI.enabled))
            {
                if (enter) Event.current.Use();
                Answer(quiz, quiz.IsCorrect(typed) ? 1 : 0, quiz.IsCorrect(typed));
            }
            GUI.enabled = true;
            return;
        }
        string[] available = quiz != null ? quiz.Options : choiceStage == 0 ? FirstSemesterStory.Choices(scene) : FirstSemesterStory.ExtraChoices(scene);
        const float height = 62, gap = 14;
        float y = bottom - available.Length * (height + gap) + gap;
        if (quiz != null) Prompt(quiz, y - 20);
        for (int i = 0; i < available.Length; i++)
            if (NovelUI.Choice(new Rect(190, y + i * (height + gap), 900, height), i + 1, available[i])) { Choose(i); return; }
        // Number keys pick an answer too.
        Event e = Event.current;
        int picked = e.type == EventType.KeyDown ? e.keyCode - KeyCode.Alpha1 : -1;
        if (picked < 0 || picked >= available.Length) picked = e.type == EventType.KeyDown ? e.keyCode - KeyCode.Keypad1 : -1;
        if (picked >= 0 && picked < available.Length) { e.Use(); Choose(picked); }
    }

    // Draws the question card so that it ends at `bottom`; returns its top.
    float Prompt(Quiz quiz, float bottom)
    {
        Font font = quiz.ShortAnswer ? NovelUI.Code : NovelUI.Soft;
        float height = NovelUI.Height(quiz.Prompt, font, 22, 900) + 40;
        Rect card = new Rect(170, bottom - height, 940, height);
        NovelUI.Card(card, new Color(.24f, .1f, .17f, .93f), 22, NovelUI.Pink);
        NovelUI.Label(new Rect(card.x + 20, card.y + 20, 900, height - 40), quiz.Prompt, font, 22, Color.white);
        NovelUI.Pill(card.x + 24, card.y - 18, 36, "문제", NovelUI.Lemon, NovelUI.Berry, 18);
        return card.y;
    }

    void Choose(int option)
    {
        Quiz quiz = ChapterQuizzes.Find(scene, choiceStage, experienced);
        if (quiz != null) { Answer(quiz, option, option == quiz.Answer); return; }
        int delta = ProfessorPersonality.Score(professor, scene, choiceStage, option);
        if (choiceStage == 1)
        {
            // Follow-up choices remain in chapters 1, 4 and 6.
            if (option == 2) solo++;
            else if (scene == 1 && option == 0) questions++;
            else if (option == 1) teamwork++;
            if ((scene == 4 || scene == 6) && option == 0) interest++;
        }
        else
        {
            if (scene == 2 && option < 2) interest++;
            if (scene == 5 && option == 0) teamwork++;
            if (scene == 5 && option == 2) solo++;
        }
        affection += delta;
        React(delta, true);
        resultLine = ProfessorPersonality.Reply(professor, scene, choiceStage, option, affection);
        resultLine += AffectionLine(delta);
        showResult = true;
    }
    static string AffectionLine(int delta) => "\n<color=" + NovelUI.AffectionColor + ">호감도 " + (delta > 0 ? "+" : "") + delta + "</color>";

    // Quiz slots: a right answer earns a +2 bonus, a wrong one costs nothing.
    // reply indexes Replies[professor]: the chosen option, or 0/1 (wrong/right) for short answers.
    void Answer(Quiz quiz, int reply, bool correct)
    {
        int delta = correct ? 2 : 0;
        if (correct && scene == 4) questions++;
        affection += delta;
        React(delta, correct, true);
        typed = "";
        resultLine = (correct ? "정답! 호감도 보너스 +2.\n" : "오답이어도 감점은 없어요.\n") + ProfessorPersonality.Decorate(professor, quiz.Replies[professor][reply], affection);
        resultLine += AffectionLine(delta);
        showResult = true;
    }

    void DrawMiniGame()
    {
        NovelUI.Card(new Rect(60, 40, 1160, 830), new Color(.22f, .09f, .16f, .95f), 36, NovelUI.Alpha(NovelUI.Pink, .8f));
        float x = 100 + NovelUI.Pill(100, 58, 44, "MINI GAME", NovelUI.Pink, Color.white, 20) + 12;
        NovelUI.Label(new Rect(x, 58, 700, 44), miniGame.Title, NovelUI.Round, 26, NovelUI.Cream, TextAnchor.MiddleLeft);
        miniGame.Draw(NovelUI.MiniTitle, NovelUI.MiniBody, NovelUI.MiniButton);
        if (miniGame.Done && NovelUI.Button(new Rect(980, 790, 210, 50), "결과 보기", true, 20)) FinishMiniGame();
    }

    // Minigame slots: up to +2 affection, never negative. A good result keeps the ending stat
    // the replaced choice used to give: teamwork (cards), interest (loop), questions (coffee).
    void FinishMiniGame()
    {
        int delta = miniGame.Delta;
        if (miniGame.CountsForStat)
        {
            if (scene == 2) teamwork++;
            else if (scene == 3) interest++;
            else questions++;
        }
        affection += delta;
        React(delta, true, delta == 2);
        resultLine = miniGame.Summary + "\n" + ProfessorPersonality.Decorate(professor, miniGame.Replies[professor][delta], affection);
        resultLine += AffectionLine(delta);
        miniGame = null;
        showResult = true;
    }

    // ---- Title, ending and memorial -----------------------------------------------------------

    // Opening shown once per launch: three lines over the campus with falling petals, then the logo pops in
    // exactly where the title screen draws it. The first click skips to the logo, the next one starts.
    const float LogoAt = 8;
    static readonly string[] IntroLines = { "3월, 입학 첫날.", "낯선 캠퍼스, 처음 만나는 사람들.", "그리고 그날, 나는 교수님을 만났다." };
    void Intro()
    {
        if (NovelUI.Repaint) introTime += Mathf.Min(Time.unscaledDeltaTime, .05f);
        float t = introTime;

        // Slow zoom on the campus with a pink haze.
        float zoom = 1.08f - .08f * Mathf.Clamp01(t / (LogoAt + 2));
        float w = Screen.width * zoom, h = Screen.height * zoom;
        GUI.DrawTexture(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), Resources.Load<Texture2D>("CampusExterior"), ScaleMode.ScaleAndCrop);
        GUI.matrix = Canvas;
        NovelUI.Tint(new Color(1f, .82f, .9f, t < LogoAt ? .45f : .35f));
        Petals(t);

        if (t < LogoAt)
        {
            int index = Mathf.Min(IntroLines.Length - 1, (int)((t - .8f) / 2.4f));
            float local = (t - .8f) - index * 2.4f;
            float alpha = t < .8f ? 0 : Mathf.Clamp01(local / .5f) * Mathf.Clamp01((2.4f - local) / .5f);
            float y = 400 - (1 - alpha) * 12, band = NovelUI.Width(IntroLines[index], NovelUI.Soft, 38) + 140;
            NovelUI.Fill(new Rect(640 - band / 2, y, band, 84), NovelUI.Alpha(NovelUI.Cream, .82f * alpha), 42);
            NovelUI.Label(new Rect(0, y, 1280, 84), IntroLines[index], NovelUI.Soft, 38, NovelUI.Alpha(NovelUI.Berry, alpha), TextAnchor.MiddleCenter, FontStyle.Bold);
            if (NovelUI.Button(new Rect(28, 24, 120, 42), "건너뛰기", false, 18) || IntroPressed()) { novelAudio.Play("Select"); introTime = LogoAt; }
        }
        else
        {
            float k = t - LogoAt;
            if (!introChimed) { introChimed = true; novelAudio.Play("Bonus"); }
            // White flash, then the logo pops in with a small overshoot and a ring of hearts.
            NovelUI.Tint(new Color(1, 1, 1, Mathf.Clamp01(1 - k / .4f)));
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6, reach = 120 + 260 * Mathf.Clamp01(k / 1.2f);
                float fade = 1 - Mathf.Clamp01(k / 1.4f), size = 26 + i % 3 * 10;
                Vector2 c = new Vector2(640 + Mathf.Cos(angle) * reach * 1.4f, 130 + Mathf.Sin(angle) * reach * .6f);
                NovelUI.Heart(new Rect(c.x - size / 2, c.y - size / 2, size, size), NovelUI.Alpha(i % 2 == 0 ? NovelUI.DeepPink : Color.white, fade));
            }
            float pop = k < .35f ? Mathf.Lerp(.4f, 1.12f, k / .35f) : k < .6f ? Mathf.Lerp(1.12f, 1f, (k - .35f) / .25f) : 1;
            Matrix4x4 before = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(640, 130));
            NovelUI.Outlined(new Rect(0, 70, 1280, 120), "두근두근 교수님", NovelUI.Round, 92, Color.white, NovelUI.DeepPink, 6);
            GUI.matrix = before;

            float sub = Mathf.Clamp01((k - .5f) / .5f);
            float width = NovelUI.Width("AI-SW융합학부 첫 학기", NovelUI.Round, 26) + 46;
            NovelUI.Pill(640 - width / 2, 192 + (1 - sub) * 16, 52, "AI-SW융합학부 첫 학기", NovelUI.Alpha(NovelUI.Cream, .95f * sub), NovelUI.Alpha(NovelUI.DeepPink, sub), 26);
            if (k > 1.2f)
            {
                float blink = .55f + .45f * Mathf.Sin((k - 1.2f) * 4);
                float start = NovelUI.Width("클릭해서 시작하기", NovelUI.Round, 28) + 56;
                NovelUI.Pill(640 - start / 2, 630, 60, "클릭해서 시작하기", NovelUI.Alpha(NovelUI.Cream, .9f * blink), NovelUI.Alpha(NovelUI.DeepPink, blink), 28);
            }
            if (k > .6f && (GUI.Button(NovelUI.Window, GUIContent.none, GUIStyle.none) || IntroPressed())) { novelAudio.Play("Advance"); introDone = true; }
        }
        GUI.matrix = Matrix4x4.identity;
        float fadeIn = 1 - Mathf.Clamp01(t / .8f);
        if (fadeIn > 0) { GUI.color = new Color(.24f, .1f, .17f, fadeIn); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white; }
    }
    bool IntroPressed()
    {
        Event e = Event.current;
        bool pressed = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape);
        if (pressed) e.Use();
        return pressed;
    }
    // Cherry blossom petals drifting down and to the right, each spinning slowly.
    void Petals(float t)
    {
        for (int i = 0; i < 26; i++)
        {
            float speed = 40 + i % 5 * 12, size = 20 + i % 4 * 6;
            float y = (t * speed + i * 97) % 1000 - 50;
            Rect window = NovelUI.Window;
            float x = window.x + (i * 151 + t * speed * .5f) % (window.width + 60) - 30 + Mathf.Sin(t * 1.3f + i) * 20;
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(t * (30 + i % 3 * 25) + i * 40, new Vector2(x, y));
            NovelUI.Fill(new Rect(x - size / 2, y - size * .3f, size, size * .6f), NovelUI.Alpha(i % 3 == 0 ? Color.white : i % 3 == 1 ? NovelUI.Pink : NovelUI.Blush, .85f), size * .3f);
            GUI.matrix = before;
        }
    }

    // Background with a soft pink haze and a few hearts drifting up.
    void Dreamy(float haze)
    {
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Background, ScaleMode.ScaleAndCrop);
        GUI.matrix = Canvas;
        NovelUI.Tint(new Color(1f, .82f, .9f, haze));
        for (int i = 0; i < 12; i++)
        {
            float size = 18 + i % 3 * 12;
            float y = 940 - (Time.unscaledTime * (14 + i % 4 * 6) + i * 173) % 1040;
            float x = NovelUI.Window.x + (i * 211 + 60) % (NovelUI.Window.width - 40) + Mathf.Sin(Time.unscaledTime * .8f + i) * 18;
            NovelUI.Heart(new Rect(x, y, size, size), NovelUI.Alpha(i % 2 == 0 ? Color.white : NovelUI.Pink, .55f), i % 3 != 0);
        }
    }

    void StartScreen()
    {
        Dreamy(.35f);
        NovelUI.Outlined(new Rect(0, 70, 1280, 120), "두근두근 교수님", NovelUI.Round, 92, Color.white, NovelUI.DeepPink, 6);
        float width = NovelUI.Width("AI-SW융합학부 첫 학기", NovelUI.Round, 26) + 46;
        NovelUI.Pill(640 - width / 2, 192, 52, "AI-SW융합학부 첫 학기", NovelUI.Alpha(NovelUI.Cream, .95f), NovelUI.DeepPink, 26);
        NovelUI.Outlined(new Rect(0, 252, 1280, 40), "낯선 캠퍼스에서 시작하는 나의 첫 학기", NovelUI.Soft, 24, NovelUI.Berry, NovelUI.Alpha(Color.white, .9f), 2);

        if (entryStep == 0)
        {
            Rect card = new Rect(310, 316, 660, 564);
            NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .96f), 36, NovelUI.Pink);
            NovelUI.Label(new Rect(card.x, card.y + 24, card.width, 40), "체험할 학부를 선택하세요", NovelUI.Round, 28, NovelUI.Berry, TextAnchor.MiddleCenter);
            // Back to the intro's "click to start" screen, without replaying the opening.
            if (NovelUI.Button(new Rect(card.x + 30, card.yMax - 66, 110, 46), "이전", false, 19)) { novelAudio.Play("Back"); introDone = false; introTime = LogoAt + 1.5f; }
            GUI.enabled = GameSave.Any();
            if (NovelUI.Button(new Rect(card.x + 156, card.yMax - 66, 148, 46), "이어하기", false, 19)) OpenMenu(Menu.LoadSlots);
            GUI.enabled = true;
            if (NovelUI.Button(new Rect(card.x + 316, card.yMax - 66, 148, 46), "엔딩 도감", false, 19)) OpenMenu(Menu.Gallery);
            if (NovelUI.Button(new Rect(card.x + 476, card.yMax - 66, 148, 46), "설정", false, 19)) OpenMenu(Menu.Settings);
            for (int i = 0; i < faculties.Length; i++)
            {
                Rect row = new Rect(card.x + 50, card.y + 86 + i * 86, card.width - 100, 66);
                GUI.enabled = i == 0;
                if (NovelUI.Button(row, faculties[i], i == 0, 24)) { novelAudio.Play("Select"); faculty = i; entryStep = 1; }
                GUI.enabled = true;
                NovelUI.Pill(row.xMax - (i == 0 ? 150 : 104), row.y + 17, 32, i == 0 ? "소프트웨어" : "준비 중", i == 0 ? NovelUI.Alpha(Color.white, .9f) : NovelUI.Blush, i == 0 ? NovelUI.DeepPink : NovelUI.Plum, 16);
            }
            return;
        }
        if (entryStep == 1)
        {
            Rect card = new Rect(310, 320, 660, 470);
            NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .96f), 36, NovelUI.Pink);
            NovelUI.Label(new Rect(card.x, card.y + 26, card.width, 40), "코딩 경험이 있나요?", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(card.x, card.y + 68, card.width, 30), "경험에 맞는 설명과 문제가 나와요", NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (Tile(new Rect(card.x + 50, card.y + 120, card.width - 100, 110), "전공자", "코드를 직접 입력해서 문제 풀기", true)) { novelAudio.Play("Select"); experienced = true; entryStep = 2; }
            if (Tile(new Rect(card.x + 50, card.y + 250, card.width - 100, 110), "비전공자", "쉬운 설명과 객관식으로 문제 풀기", false)) { novelAudio.Play("Select"); experienced = false; entryStep = 2; }
            if (NovelUI.Button(new Rect(card.x + 30, card.yMax - 74, 110, 46), "이전", false, 19)) { novelAudio.Play("Back"); entryStep = 0; }
            return;
        }
        Rect panel = new Rect(170, 316, 940, 540);
        NovelUI.Card(panel, NovelUI.Alpha(NovelUI.Cream, .96f), 36, NovelUI.Pink);
        NovelUI.Label(new Rect(panel.x, panel.y + 22, panel.width, 44), "공략할 교수님을 선택하세요", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (ProfessorCard(new Rect(panel.x + 50, panel.y + 86, 400, 380), 0, "김가람 교수", "엄격한 원칙파 · 근거와 준비를 중시", "#원칙파", "#꼼꼼함")) Begin(0);
        if (ProfessorCard(new Rect(panel.xMax - 450, panel.y + 86, 400, 380), 1, "이명희 교수", "유쾌한 소통파 · 호기심과 협업을 중시", "#소통파", "#호기심")) Begin(1);
        if (NovelUI.Button(new Rect(panel.x + 30, panel.yMax - 62, 110, 44), "이전", false, 19)) { novelAudio.Play("Back"); entryStep = 1; }
    }
    void Begin(int chosen)
    {
        novelAudio.Play("Bonus");
        professor = chosen; scene = 1;
        portraitAlpha = 0; lineKey = null; chapterShown = 0;
    }

    // A two-line option tile (title + description) used on the title screen.
    bool Tile(Rect rect, string heading, string description, bool primary)
    {
        bool hover = NovelUI.Hover(rect);
        Rect face = hover ? new Rect(rect.x, rect.y - 3, rect.width, rect.height) : rect;
        NovelUI.Card(face, primary ? (hover ? NovelUI.DeepPink : NovelUI.Pink) : (hover ? NovelUI.Blush : NovelUI.Cream), 28, primary ? default : NovelUI.Pink);
        Color ink = primary ? Color.white : NovelUI.Berry;
        NovelUI.Heart(new Rect(face.x + 30, face.y + face.height / 2 - 20, 40, 40), primary ? Color.white : NovelUI.Pink);
        NovelUI.Label(new Rect(face.x + 90, face.y + 18, face.width - 110, 40), heading, NovelUI.Round, 30, ink, TextAnchor.MiddleLeft);
        NovelUI.Label(new Rect(face.x + 90, face.y + 60, face.width - 110, 30), description, NovelUI.Soft, 19, ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    bool ProfessorCard(Rect rect, int who, string name, string description, string tagA, string tagB)
    {
        bool hover = NovelUI.Hover(rect);
        Rect face = hover ? new Rect(rect.x, rect.y - 5, rect.width, rect.height) : rect;
        if (hover) NovelUI.Glow(new Rect(face.x - 40, face.y - 40, face.width + 80, face.height + 80), NovelUI.Alpha(NovelUI.Pink, .45f));
        NovelUI.Card(face, Color.white, 30, hover ? NovelUI.DeepPink : NovelUI.Pink, hover ? 4 : 3);
        Rect photo = new Rect(face.x + 18, face.y + 18, face.width - 36, 210);
        NovelUI.Fill(photo, NovelUI.Blush, 22);
        Texture2D portrait = Resources.Load<Texture2D>((who == 0 ? "ProfessorKim" : "ProfessorLee") + (hover ? "Happy" : "") + "Transparent");
        if (portrait != null && NovelUI.Repaint) GUI.DrawTextureWithTexCoords(photo, portrait, FaceCrop(who));
        NovelUI.Label(new Rect(face.x, photo.yMax + 14, face.width, 40), name, NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(face.x + 20, photo.yMax + 56, face.width - 40, 30), description, NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
        float a = NovelUI.Width(tagA, NovelUI.Round, 17) + 30, b = NovelUI.Width(tagB, NovelUI.Round, 17) + 30;
        float x = face.center.x - (a + b + 10) / 2;
        NovelUI.Pill(x, photo.yMax + 98, 32, tagA, NovelUI.Blush, NovelUI.DeepPink, 17);
        NovelUI.Pill(x + a + 10, photo.yMax + 98, 32, tagB, NovelUI.Blush, NovelUI.DeepPink, 17);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    void Ending()
    {
        // The ending card covers the middle of the screen, so the portrait would only peek out at the sides.
        DrawScene(false);
        GUI.matrix = Canvas;
        NovelUI.Tint(new Color(1f, .82f, .9f, .3f));
        int ending = FirstSemesterStory.ResolveEnding(affection, questions, teamwork, interest, solo);
        bool trueEnd = ending == 7;
        if (!endingRecorded)
        {
            endingRecorded = true;
            endingNew = EndingBook.Unlock(faculty, professor, ending);
            if (endingNew) novelAudio.Play("Unlock");
        }
        Rect card = new Rect(150, 130, 980, 650);
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 40, NovelUI.Pink, 4);
        string badge = "ENDING " + (ending + 1) + " / 8";
        float width = NovelUI.Width(badge, NovelUI.Round, 22) + 48;
        NovelUI.Pill(640 - width / 2, card.y - 24, 50, badge, trueEnd ? NovelUI.DeepPink : NovelUI.Pink, Color.white, 22);
        if (trueEnd)
            for (int i = 0; i < 5; i++)
                NovelUI.Heart(new Rect(470 + i * 70, card.y + 44 + Mathf.Sin(Time.unscaledTime * 3 + i) * 5, 30, 30), NovelUI.Alpha(NovelUI.DeepPink, .8f));
        if (endingNew)
        {
            float wobble = Mathf.Sin(Time.unscaledTime * 4) * 3;
            NovelUI.Pill(card.xMax - 190, card.y + 24 + wobble, 40, "NEW! 도감 등록", NovelUI.Lemon, NovelUI.Berry, 18);
        }
        NovelUI.Label(new Rect(card.x, card.y + 86, card.width, 64), FirstSemesterStory.EndingNames[ending], NovelUI.Round, 48, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(card.x + 70, card.y + 176, card.width - 140, 250), ProfessorPersonality.Ending(professor, ending), NovelUI.Soft, 23, NovelUI.Berry, TextAnchor.UpperLeft, FontStyle.Bold);

        string[] stats = { "♥ 호감도 " + affection, "질문 " + questions, "협업 " + teamwork, "관심 " + interest };
        float total = -14;
        foreach (string s in stats) total += NovelUI.Width(s, NovelUI.Round, 20) + 40 + 14;
        float x = 640 - total / 2;
        for (int i = 0; i < stats.Length; i++)
            x += NovelUI.Pill(x, card.y + 440, 40, stats[i], i == 0 ? NovelUI.DeepPink : NovelUI.Blush, i == 0 ? Color.white : NovelUI.Berry, 20) + 14;

        if (trueEnd && NovelUI.Button(new Rect(640 - 230, card.y + 520, 220, 62), "메모리얼 보기", false, 22)) { novelAudio.Play("Select"); memorial = 0.01f; }
        Rect restart = trueEnd ? new Rect(650, card.y + 520, 220, 62) : new Rect(640 - 120, card.y + 520, 240, 62);
        if (NovelUI.Button(restart, "처음부터", true, 24)) { novelAudio.Play("Select"); ResetRun(); }
    }

    // Back to the title screen with a fresh first semester.
    void ResetRun()
    {
        scene = affection = entryStep = dialogueIndex = choiceStage = questions = teamwork = interest = solo = 0;
        professor = -1; showResult = false; resultLine = typed = "";
        shownAffection = lastDelta = 0; lineKey = null; miniGame = null;
        endingRecorded = endingNew = false; chapterShown = 0; reactAt = -10; sparks.Clear();
    }

    void Memorial()
    {
        if (NovelUI.Repaint) memorial += Time.unscaledDeltaTime;
        float zoom = Mathf.Lerp(.75f, 1.1f, Mathf.Clamp01(memorial / 4));
        DrawScene();
        GUI.matrix = Canvas;
        NovelUI.Tint(new Color(1f, .72f, .84f, .5f));
        for (int i = 0; i < 14; i++)
        {
            float y = 940 - (memorial * (40 + i % 4 * 15) + i * 131) % 1040;
            NovelUI.Heart(new Rect(NovelUI.Window.x + (i * 193 + 40) % (NovelUI.Window.width - 40), y, 22 + i % 3 * 10, 22 + i % 3 * 10), NovelUI.Alpha(Color.white, .7f));
        }
        NovelUI.Outlined(new Rect(1280 * (1 - zoom) / 2, 300, 1280 * zoom, 170), memorial < 2.5f ? "…수고했어요." : "다음 프로젝트도, 같이 할래요?", NovelUI.Soft, Mathf.RoundToInt(56 * zoom), Color.white, NovelUI.DeepPink, 5);
        float width = NovelUI.Width("진 엔딩 메모리얼", NovelUI.Round, 20) + 40;
        NovelUI.Pill(640 - width / 2, 760, 40, "진 엔딩 메모리얼", NovelUI.Alpha(NovelUI.Cream, .9f), NovelUI.DeepPink, 20);
        if (memorial > 5 && NovelUI.Button(new Rect(640 - 110, 812, 220, 56), "돌아가기", true, 22)) { novelAudio.Play("Back"); memorial = 0; }
    }
}
