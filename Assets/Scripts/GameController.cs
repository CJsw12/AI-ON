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
    int wrongPick = -1;   // Beginners get a second try at a multiple-choice quiz; this is the option already ruled out.
    Texture2D previousBackground;
    float backgroundChangedAt;
    NovelAudio novelAudio;
    MiniGame miniGame;
    string typed = "";

    // Presentation state: typewriter, auto mode, portrait fade and the affection popup.
    string lineKey;
    float lineStartedAt, typedAt, portraitAlpha, affectionChangedAt = -10;
    int shownAffection, lastDelta;
    float introTime;   // Advances at most 50 ms per frame, so a loading hitch can't skip a line.
    bool introDone, introChimed;
    bool endingRecorded, endingNew;   // The ending screen adds its ending to the book once.
    // Skip mode: readKey names the line on screen in ReadLog; resultKey names the reply to the last choice.
    string readKey, resultKey;
    bool skip;
    float skippedAt;
    MiniGameTheme practice;            // The minigame being practised from the title, or null.
    int practiceCollege, practiceFaculty, practiceProfessor;
    bool practiceExperienced;
    int titleCollege;                  // The college tab on the title's faculty list.
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
        UpdateGazeTest();
        UpdatePhotoCapture();
        novelAudio.SetMood(DodgeScreenOpen ? "Darkness" : S.Mood(scene, choiceStage));   // tense music for the gaze-dodge game
        AiImeUpdate();
        if (menu == Menu.None && (miniGame == null || !miniGame.Reviewing)) MiniGame.Tick(Time.unscaledDeltaTime);
    }
    Story S => Stories.All[faculty];   // The chosen faculty's semester.
    string ProfessorName => S.Professors[professor].Name;
    string ChapterName => S.Chapters[scene];
    Texture2D Portrait
    {
        get
        {
            string mood = ReactionFace;
            if (mood == null) mood = affection < 0 ? "Frown" : affection >= 3 ? "Happy" : "";
            return Load(Professor.Picture(S.Professors[professor], professor, mood));
        }
    }
    Texture2D Background => Load(S.Background(scene, choiceStage, dialogueIndex));

    // OnGUI asks for the same few pictures many times a frame; keep them by path.
    static readonly System.Collections.Generic.Dictionary<string, Texture2D> textures = new System.Collections.Generic.Dictionary<string, Texture2D>();
    static Texture2D Load(string path)
    {
        if (path == null) return null;
        if (!textures.TryGetValue(path, out Texture2D texture)) textures[path] = texture = Resources.Load<Texture2D>(path);
        return texture;
    }

    void OnApplicationPause(bool paused) { if (paused) { ReadLog.Flush(); GameSettings.Save(); } }
    void OnApplicationQuit() { CloseGazeTest(); ReadLog.Flush(); GameSettings.Save(); }

    void DrawScene(bool portrait = true)
    {
        Texture2D background = Background;
        if (previousBackground != background) { previousBackground = background; backgroundChangedAt = Time.unscaledTime; }
        GUI.DrawTexture(Drift(new Rect(0, 0, Screen.width, Screen.height)), background, ScaleMode.ScaleAndCrop);
        // Choice results are the professor's replies, so the portrait is always shown there.
        bool showProfessor = portrait && (showResult || S.ProfessorOnStage(scene, choiceStage, dialogueIndex));
        if (NovelUI.Repaint) portraitAlpha = Mathf.MoveTowards(portraitAlpha, showProfessor ? 1 : 0, Time.unscaledDeltaTime * 4);
        bool duo = portrait && DuoActive;
        if (duo) UpdateDuoFocus();
        if (portraitAlpha > 0 && duo) DrawPlayerCharacter(portraitAlpha);
        Matrix4x4 sceneMatrix = GUI.matrix;
        if (portraitAlpha > 0)
        {
            float lit = duo ? DuoBrightness(true) : 1f;
            Color tint = new Color(lit, lit, lit, portraitAlpha);
            // Laid out by the face (same size for everyone); in a duo right of centre, facing the player on the left.
            if (!DrawStageProfessor(Portrait, tint, duo))
            {
                if (duo) GUI.matrix = DuoProfessorMatrix * sceneMatrix;
                GUI.color = tint;
                string professorImage = professor >= 0 ? S.Professors[professor].Image : "";
                bool fullFigure = professorImage.StartsWith("ProfessorJungChem") || professorImage.StartsWith("ProfessorOhChem") ||
                    professorImage.StartsWith("ProfessorGuMath") || professorImage.StartsWith("ProfessorSeolMath") ||
                    professorImage.StartsWith("ProfessorMoonBio") || professorImage.StartsWith("ProfessorLimBio") ||
                    professorImage.StartsWith("ProfessorShinDoYoon") || professorImage.StartsWith("ProfessorRyuBoRa") ||
                    professorImage.StartsWith("ProfessorNamgungHyun") || professorImage.StartsWith("ProfessorHaEunChae") ||
                    professorImage.StartsWith("ProfessorEomTaeO") || professorImage.StartsWith("ProfessorChaYuRi");
                if (HasUpperBodyPortrait(professorImage))
                {
                    Texture2D professorTexture = Portrait;
                    if (professorTexture != null)
                    {
                        Rect frame = PortraitRect();
                        Rect upperBody = new Rect(frame.x, frame.y + frame.height * .04f, frame.width, frame.height * .66f);
                        DrawUpperBodyPortrait(upperBody, professorTexture, professorImage);
                    }
                }
                else if (fullFigure)
                {
                    Texture2D professorTexture = Portrait;
                    if (professorTexture != null)
                    {
                        float height = Screen.height * .82f;
                        float width = height * professorTexture.width / professorTexture.height * 1.45f;
                        GUI.DrawTexture(new Rect((Screen.width - width) / 2, Screen.height - height, width, height), professorTexture, ScaleMode.StretchToFill, true);
                    }
                }
                else
                    GUI.DrawTexture(PortraitRect(), Portrait, ScaleMode.ScaleAndCrop);
            }
            GUI.color = Color.white;
            GUI.matrix = sceneMatrix;
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
        if (GazeCalibrationActive)
        {
            GUI.matrix = Matrix4x4.identity;
            // Skip the rest of step 1: train on the samples collected so far.
            if (gazeCalibration.Running && GUI.Button(new Rect(Screen.width - 230, Screen.height - 80, 200, 50), "여기까지 하고 넘어가기"))
                gazeCalibration.FinishEarly();
            return;
        }
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
        else if ((InGame || practice != null) && Pressed(KeyCode.Escape)) OpenMenu(Menu.Settings);   // Minigames pause while it is open.
        Toast();
        NovelUI.EndFrame();
        GUI.matrix = Matrix4x4.identity;
    }
    bool InGame => scene >= 1 && scene <= 6 && memorial <= 0 && introDone;

    void DrawScreen()
    {
        if (gazeTestOpen) { DrawGazeTest(); return; }
        if (photoCaptureOpen) { DrawPhotoCapture(); return; }
        if (memorial > 0) { Memorial(); return; }
        if (!introDone) { Intro(); return; }
        if (practice != null) { Practice(); return; }
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
        AiPoll();
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
        Slot current = S.Slot(scene, choiceStage);
        string slot = current.Major != null ? "문제 " : current.Game != null ? "미니게임 " : "선택 ";
        float x = 28;
        x += NovelUI.Pill(x, 24, 42, "CHAPTER " + scene, NovelUI.Pink, Color.white, 20) + 8;
        x += NovelUI.Pill(x, 24, 42, ChapterName, NovelUI.Alpha(NovelUI.Cream, .95f), NovelUI.Berry, 20) + 8;
        x += NovelUI.Pill(x, 29, 32, slot + ((scene - 1) * 2 + choiceStage + 1) + "/12", NovelUI.Alpha(NovelUI.Berry, .7f), NovelUI.Blush, 16) + 8;
        if (aiMode) NovelUI.Pill(x, 29, 32, "AI 모드", NovelUI.Mint, NovelUI.Berry, 16);

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
            text = S.FollowUpLine(professor, scene, dialogueIndex, experienced);
            int colon = text.IndexOf(": ");
            if (colon > 0 && colon <= 3)
            {
                string who = text.Substring(0, colon);
                if (who == "동기" || who == "나" || who == "선배" || who == "사장님" || who == "교수님") { speaker = who == "교수님" ? ProfessorName : who; text = text.Substring(colon + 2); }
            }
            return;
        }
        S.Line(professor, scene, dialogueIndex, out speaker, out text, experienced);
        if (speaker == "교수") speaker = ProfessorName;
    }
    bool AtChoice => !showResult && dialogueIndex >= S.Lines(scene, choiceStage) - 1;

    void Dialogue()
    {
        CurrentLine(out string speaker, out string text);
        string key = scene + "/" + choiceStage + "/" + dialogueIndex + "/" + showResult;
        if (AiHintShown) { speaker = ProfessorName; text = aiHint; key += "/h" + aiHintsUsed; }
        if (key != lineKey) { lineKey = key; lineStartedAt = Time.unscaledTime; typedAt = -1; AddToBacklog(key, speaker, text); }
        string position = showResult && resultKey == null ? null : faculty + "/" + professor + "/" + scene + "/" + choiceStage + "/" + (showResult ? resultKey : dialogueIndex.ToString()) + (experienced ? "/E" : "");
        readKey = ReadLog.ContentKey(position, text);
        string plain = Tags.Replace(text, "");
        int shown = Mathf.FloorToInt((Time.unscaledTime - lineStartedAt) * GameSettings.LettersPerSecond);
        bool typing = shown < plain.Length;
        if (!typing && typedAt < 0) typedAt = Time.unscaledTime;

        Rect box = new Rect(70, 628, 1140, 244);
        NovelUI.Card(box, NovelUI.Alpha(NovelUI.Cream, .96f), 30, NovelUI.Pink);
        bool narration = speaker == "나레이션";
        if (!narration)
        {
            NovelUI.Pill(box.x + 34, box.y - 26, 52, speaker, SpeakerTag(speaker), SpeakerInk(speaker), 24);
        }
        Rect textRect = new Rect(box.x + 42, box.y + 46, box.width - 100, box.height - 64 - (AiBoxField ? 76 : 0));
        int size = NovelUI.Height(text, NovelUI.Soft, 26, textRect.width) > textRect.height ? 22 : 26;
        NovelUI.Label(textRect, typing ? plain.Substring(0, shown) : text, NovelUI.Soft, size, narration ? NovelUI.Plum : NovelUI.Berry, TextAnchor.UpperLeft, FontStyle.Bold);

        bool canAdvance = showResult || !AtChoice;
        if (!typing && canAdvance)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 5) * 4;
            NovelUI.Heart(new Rect(box.xMax - 58, box.yMax - 46 + bob, 28, 28), NovelUI.DeepPink);
        }

        // Back on the first line returns to professor select; stop here so nothing reads scene 0's slots.
        if (LineButtons(box)) return;
        if ((!typing || AiHintShown) && AtChoice) Choices();   // A hint typing out keeps the quiz on screen.
        // Click the box, or press Space/Enter, to finish the line or move on.
        bool pressed = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if ((GUI.Button(box, GUIContent.none, GUIStyle.none) || pressed) && (typing || canAdvance))
        {
            if (pressed) Event.current.Use();
            Advance(typing);
        }
        // Auto mode waits on the finished line, then moves on; it stops at choices and while a menu is open.
        // Skip mode (the button, or holding Ctrl) races through lines already read and stops at new ones and at choices.
        else if ((skip || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && menu == Menu.None && NovelUI.Repaint)
        {
            if (canAdvance && ReadLog.Seen(readKey)) { if (Time.unscaledTime - skippedAt > .06f) { skippedAt = Time.unscaledTime; Advance(false, true); } }
            else if (skip) { skip = false; ShowToast(canAdvance ? "처음 보는 대사에서 멈췄어요" : "선택지에서 멈췄어요"); }
        }
        else if (GameSettings.Auto && menu == Menu.None && !typing && canAdvance && NovelUI.Repaint && Time.unscaledTime - typedAt > GameSettings.AutoWait(plain.Length))
            Advance(false);
        // Scrolling up opens the backlog.
        if (Event.current.type == EventType.ScrollWheel && Event.current.delta.y < 0) { Event.current.Use(); OpenMenu(Menu.Backlog); }
    }
    Color SpeakerTag(string speaker) => speaker == ProfessorName ? NovelUI.DeepPink : speaker == "나" ? NovelUI.Sky : speaker == "동기" ? NovelUI.Mint : NovelUI.Lemon;
    Color SpeakerInk(string speaker) => speaker == ProfessorName || speaker == "나" ? Color.white : NovelUI.Berry;

    void Advance(bool typing, bool quiet = false)
    {
        if (typing) { lineStartedAt = -1000; return; }
        if (!quiet) novelAudio.Play("Advance");
        ReadLog.Mark(readKey);
        if (!showResult) { dialogueIndex++; return; }
        showResult = false;
        if (choiceStage == 0) { choiceStage = 1; dialogueIndex = 0; }
        else { choiceStage = dialogueIndex = 0; scene++; ReadLog.Flush(); }
    }

    // Skip, backlog and back buttons on the dialogue box's top edge.
    // Back steps one line and never crosses a choice: once chosen, its affection change is final.
    bool LineButtons(Rect box)
    {
        Rect button = new Rect(box.xMax - 394, box.y - 22, 110, 44);
        if (NovelUI.Button(button, "스킵", skip, 19))
        {
            skip = !skip;
            novelAudio.Play("Toggle");
            ShowToast(skip ? "읽은 대사를 건너뛰어요 (Ctrl을 누르고 있어도 돼요)" : "스킵을 껐어요");
        }
        button.x += 122;
        if (NovelUI.Button(button, "기록", false, 19)) OpenMenu(Menu.Backlog);
        button.x += 122;
        bool canGoBack = !showResult && (dialogueIndex > 0 || (scene == 1 && choiceStage == 0));
        if (!canGoBack || !NovelUI.Button(button, "이전", false, 19)) return false;
        novelAudio.Play("Back");
        AiCancelPending();
        if (dialogueIndex > 0) { dialogueIndex--; return false; }
        scene = 0; professor = -1;
        return true;
    }

    void Choices()
    {
        const float bottom = 584;
        Slot slot = S.Slot(scene, choiceStage);
        if (slot.Game != null)
        {
            Rect card = new Rect(290, bottom - 190, 700, 176);
            NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 32, NovelUI.Pink);
            float width = NovelUI.Width("미니게임", NovelUI.Round, 18) + 36;
            NovelUI.Pill(card.center.x - width / 2, card.y - 18, 36, "미니게임", NovelUI.Lemon, NovelUI.Berry, 18);
            NovelUI.Label(new Rect(card.x, card.y + 24, card.width, 50), slot.Game.Title, NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(card.center.x - 130, card.y + 90, 260, 60), "도전하기!", true, 26)) { novelAudio.Play("Select"); MiniGame.Easy = !experienced; miniGame = MiniGames.Create(slot.Game, professor, false, experienced); }
            return;
        }
        Quiz quiz = slot.Quiz(experienced);
        if (aiMode && AiChoices(slot, quiz, bottom)) return;
        if (quiz != null && quiz.ShortAnswer)
        {
            Prompt(quiz, bottom - 92);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            Rect field = new Rect(250, bottom - 66, 580, 62);
            NovelUI.Card(field, Color.clear, 31);
            // The default IMGUI caret is white, invisible on the cream field; tint it so typing is visibly active.
            GUI.skin.settings.cursorColor = NovelUI.DeepPink;
            GUI.skin.settings.cursorFlashSpeed = 1.2f;
            GUI.SetNextControlName("Answer");
            typed = GUI.TextField(field, typed, 40, NovelUI.Input);
            if (!AiBoxField && menu == Menu.None) GUI.FocusControl("Answer");   // AI mode's question field takes the keys while it is open.
            bool active = GUI.GetNameOfFocusedControl() == "Answer";
            // A pulsing deep-pink ring and an "입력 중" tag show the field is taking keys.
            float pulse = .65f + .35f * Mathf.Sin(Time.unscaledTime * 4);
            NovelUI.Ring(field, active ? NovelUI.Alpha(NovelUI.DeepPink, pulse) : NovelUI.Pink, 31, active ? 4 : 3);
            if (active) NovelUI.Pill(field.x + 24, field.y - 16, 32, "입력 중 · 바로 타자를 치세요", NovelUI.DeepPink, Color.white, 15);
            if (typed == "") NovelUI.Label(new Rect(field.x + 40, field.y, 400, field.height), "정답을 입력하세요", NovelUI.Soft, 20, NovelUI.Alpha(NovelUI.Plum, .6f), TextAnchor.MiddleLeft);
            GUI.enabled = typed.Trim() != "";
            // Enter submits only from the answer field: while AI mode's question box is open (typing a question or
            // waiting for the hint) Enter belongs to that box, and the click on 제출 stays a deliberate choice.
            if (NovelUI.Button(new Rect(850, bottom - 66, 180, 62), "제출", true, 24) || (enter && GUI.enabled && active && !AiBoxField))
            {
                if (enter) Event.current.Use();
                Answer(quiz, quiz.IsCorrect(typed) ? 1 : 0, quiz.IsCorrect(typed));
            }
            GUI.enabled = true;
            return;
        }
        string[] available = quiz != null ? quiz.Options : slot.Options;
        const float height = 62, gap = 14;
        float y = bottom - available.Length * (height + gap) + gap;
        if (quiz != null) Prompt(quiz, y - 20);
        for (int i = 0; i < available.Length; i++)
        {
            Rect row = new Rect(190, y + i * (height + gap), 900, height);
            if (quiz != null && i == wrongPick)
            {
                // The ruled-out answer stays visible but faded, and can't be picked again.
                Color saved = GUI.color; GUI.color = new Color(1, 1, 1, .35f);
                NovelUI.Choice(row, i + 1, available[i]);
                GUI.color = saved;
                continue;
            }
            if (NovelUI.Choice(row, i + 1, available[i])) { Choose(i); return; }
        }
        // Number keys pick an answer too.
        Event e = Event.current;
        int picked = e.type == EventType.KeyDown ? e.keyCode - KeyCode.Alpha1 : -1;
        if (picked < 0 || picked >= available.Length) picked = e.type == EventType.KeyDown ? e.keyCode - KeyCode.Keypad1 : -1;
        if (picked >= 0 && picked < available.Length && !(quiz != null && picked == wrongPick) && !AiBoxField) { e.Use(); Choose(picked); }
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
        Slot slot = S.Slot(scene, choiceStage);
        Quiz quiz = slot.Quiz(experienced);
        if (quiz != null)
        {
            // Beginners: the first wrong pick is ruled out and they choose again for a smaller bonus.
            if (!experienced && option != quiz.Answer && wrongPick < 0)
            {
                wrongPick = option;
                novelAudio.Play("Down");
                ShowToast("아쉬워요! 그 답은 아니에요. 한 번 더 골라 보세요");
                return;
            }
            Answer(quiz, option, option == quiz.Answer);
            return;
        }
        int delta = slot.Scores[professor][option];
        Raise(option < slot.Stats.Length ? slot.Stats[option] : '-');
        affection += delta;
        React(delta, true);
        resultLine = Stories.Decorate(S, professor, slot.Replies[professor][option], affection);
        resultKey = "c" + option;
        resultLine += AffectionLine(delta);
        showResult = true;
    }
    // Ending stats, named by one letter in the story's slots.
    void Raise(char stat)
    {
        if (stat == 'q') questions++;
        else if (stat == 't') teamwork++;
        else if (stat == 'i') interest++;
        else if (stat == 's') solo++;
    }
    static string AffectionLine(int delta) => "\n<color=" + NovelUI.AffectionColor + ">호감도 " + (delta > 0 ? "+" : "") + delta + "</color>";

    // Quiz slots: a right answer earns a +2 bonus, a wrong one costs nothing.
    // reply indexes Replies[professor]: the chosen option, or 0/1 (wrong/right) for short answers.
    void Answer(Quiz quiz, int reply, bool correct)
    {
        bool retried = wrongPick >= 0;
        wrongPick = -1;
        int delta = correct ? (retried ? 1 : 2) : 0;
        if (correct) Raise(S.Slot(scene, choiceStage).Stat);
        affection += delta;
        React(delta, correct, true);
        typed = "";
        resultKey = "q" + reply;
        string feedback;
        if (correct)
        {
            feedback = retried ? "두 번째에 정답! 호감도 보너스 +1.\n" : "정답! 호감도 보너스 +2.\n";
        }
        else
        {
            string answerText = quiz.ShortAnswer
                ? (quiz.Accepted != null && quiz.Accepted.Length > 0 ? quiz.Accepted[0] : "")
                : (quiz.Options != null && quiz.Answer >= 0 && quiz.Answer < quiz.Options.Length ? quiz.Options[quiz.Answer] : "");
            feedback = "오답이어도 감점은 없어요." + (!string.IsNullOrEmpty(answerText) ? " (정답: " + answerText + ")\n" : "\n");
        }
        resultLine = feedback + Stories.Decorate(S, professor, quiz.Replies[professor][reply], affection);
        resultLine += AffectionLine(delta);
        showResult = true;
    }

    void DrawMiniGame()
    {
        NovelUI.Card(new Rect(60, 40, 1160, 830), new Color(.22f, .09f, .16f, .95f), 36, NovelUI.Alpha(NovelUI.Pink, .8f));
        float x = 100 + NovelUI.Pill(100, 58, 44, "MINI GAME", NovelUI.Pink, Color.white, 20) + 12;
        NovelUI.Label(new Rect(x, 58, 700, 44), miniGame.Title, NovelUI.Round, 26, NovelUI.Cream, TextAnchor.MiddleLeft);
        miniGame.Render(NovelUI.MiniTitle, NovelUI.MiniBody, NovelUI.MiniButton);
        if (!miniGame.Done || miniGame.Reviewing || !NovelUI.Button(new Rect(980, 790, 210, 50), practice != null ? "돌아가기" : "결과 보기", true, 20)) return;
        if (practice != null) EndPractice();
        else FinishMiniGame();
    }

    // Minigame slots: up to +2 affection, never negative. A good result raises the slot's ending stat.
    void FinishMiniGame()
    {
        int delta = miniGame.Delta;
        if (miniGame.CountsForStat) Raise(S.Slot(scene, choiceStage).Stat);
        affection += delta;
        React(delta, true, delta == 2);
        resultKey = "m" + delta;
        resultLine = miniGame.Summary + "\n" + Stories.Decorate(S, professor, miniGame.Replies[professor][delta], affection);
        resultLine += AffectionLine(delta);
        miniGame = null;
        showResult = true;
    }

    // ---- Title, ending and memorial -----------------------------------------------------------

    // Opening shown once per launch: three lines over the campus with falling petals, then the logo pops in
    // exactly where the title screen draws it. The first click skips to the logo, the next one starts.
    const float LogoAt = 8;
    const string Subtitle = "설레는 대학 첫 학기";
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
            NovelUI.Outlined(new Rect(0, 70, 1280, 120), "두근두근 캠퍼스", NovelUI.Round, 92, Color.white, NovelUI.DeepPink, 6);
            GUI.matrix = before;

            float sub = Mathf.Clamp01((k - .5f) / .5f);
            float width = NovelUI.Width(Subtitle, NovelUI.Round, 26) + 46;
            NovelUI.Pill(640 - width / 2, 192 + (1 - sub) * 16, 52, Subtitle, NovelUI.Alpha(NovelUI.Cream, .95f * sub), NovelUI.Alpha(NovelUI.DeepPink, sub), 26);
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
        NovelUI.Outlined(new Rect(0, 70, 1280, 120), "두근두근 캠퍼스", NovelUI.Round, 92, Color.white, NovelUI.DeepPink, 6);
        float width = NovelUI.Width(Subtitle, NovelUI.Round, 26) + 46;
        NovelUI.Pill(640 - width / 2, 192, 52, Subtitle, NovelUI.Alpha(NovelUI.Cream, .95f), NovelUI.DeepPink, 26);
        NovelUI.Outlined(new Rect(0, 252, 1280, 40), "낯선 캠퍼스에서 시작하는 나의 첫 학기", NovelUI.Soft, 24, NovelUI.Berry, NovelUI.Alpha(Color.white, .9f), 2);

        if (entryStep == 0)
        {
            Rect card = new Rect(310, 316, 660, 564);
            NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .96f), 36, NovelUI.Pink);
            NovelUI.Label(new Rect(card.x, card.y + 24, card.width, 40), "체험할 학과를 선택하세요", NovelUI.Round, 28, NovelUI.Berry, TextAnchor.MiddleCenter);
            // Back to the intro's "click to start" screen, without replaying the opening.
            if (NovelUI.Button(new Rect(card.x + 30, card.yMax - 66, 96, 46), "이전", false, 19)) { novelAudio.Play("Back"); introDone = false; introTime = LogoAt + 1.5f; }
            GUI.enabled = GameSave.Any();
            if (NovelUI.Button(new Rect(card.x + 136, card.yMax - 66, 112, 46), "이어하기", false, 19)) OpenMenu(Menu.LoadSlots);
            GUI.enabled = true;
            if (NovelUI.Button(new Rect(card.x + 258, card.yMax - 66, 112, 46), "미니게임", false, 19)) OpenMenu(Menu.Practice);
            if (NovelUI.Button(new Rect(card.x + 380, card.yMax - 66, 124, 46), "엔딩 도감", false, 19)) OpenMenu(Menu.Gallery);
            if (NovelUI.Button(new Rect(card.x + 514, card.yMax - 66, 116, 46), "설정", false, 19)) OpenMenu(Menu.Settings);
            if (NovelUI.Button(new Rect(990, 744, 250, 54), "눈빛 피하기", true, 20)) StartDodgeFromTitle();
            if (NovelUI.Button(new Rect(990, 810, 250, 54), "시선 추적 테스트", false, 20)) OpenGazeTest();
            int college = NovelUI.Segmented(new Rect(card.x + 40, card.y + 80, card.width - 80, 48), Stories.Colleges, titleCollege, 18);
            if (college != titleCollege) { novelAudio.Play("Toggle"); titleCollege = college; }
            int[] faculties = Stories.In(titleCollege);
            if (faculties.Length == 0)
                NovelUI.Label(new Rect(card.x, card.y + 230, card.width, 90), "준비 중인 학부예요\n곧 새 학과가 열려요!", NovelUI.Soft, 22, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            // Room for five faculties per college above the bottom buttons.
            for (int i = 0; i < faculties.Length; i++)
            {
                Story story = Stories.All[faculties[i]];
                Rect row = new Rect(card.x + 50, card.y + 146 + i * 70, card.width - 100, 58);
                if (NovelUI.Button(row, story.Name, i == 0, 23)) { novelAudio.Play("Select"); faculty = faculties[i]; entryStep = 1; }
                float tag = NovelUI.Width(story.Tag, NovelUI.Round, 16) + 32;
                NovelUI.Pill(row.xMax - tag - 22, row.y + 13, 32, story.Tag, i == 0 ? NovelUI.Alpha(Color.white, .9f) : NovelUI.Blush, NovelUI.DeepPink, 16);
            }
            return;
        }
        if (entryStep == 1)
        {
            Rect card = new Rect(310, 320, 660, 470);
            NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .96f), 36, NovelUI.Pink);
            NovelUI.Label(new Rect(card.x, card.y + 26, card.width, 40), S.LevelQuestion, NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(card.x, card.y + 68, card.width, 30), "경험에 맞는 설명과 문제가 나와요", NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (Tile(new Rect(card.x + 50, card.y + 120, card.width - 100, 110), S.Levels[0], S.LevelNotes[0], true)) { novelAudio.Play("Select"); experienced = true; entryStep = 2; }
            if (Tile(new Rect(card.x + 50, card.y + 250, card.width - 100, 110), S.Levels[1], S.LevelNotes[1], false)) { novelAudio.Play("Select"); experienced = false; entryStep = 2; }
            if (NovelUI.Button(new Rect(card.x + 30, card.yMax - 74, 110, 46), "이전", false, 19)) { novelAudio.Play("Back"); entryStep = 0; }
            return;
        }
        Rect panel = new Rect(170, 316, 940, 540);
        NovelUI.Card(panel, NovelUI.Alpha(NovelUI.Cream, .96f), 36, NovelUI.Pink);
        NovelUI.Label(new Rect(panel.x, panel.y + 22, panel.width, 44), "공략할 교수님을 선택하세요", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (ProfessorCard(new Rect(panel.x + 50, panel.y + 86, 400, 380), 0, S.Professors[0])) Begin(0);
        if (ProfessorCard(new Rect(panel.xMax - 450, panel.y + 86, 400, 380), 1, S.Professors[1])) Begin(1);
        if (NovelUI.Button(new Rect(panel.x + 30, panel.yMax - 62, 110, 44), "이전", false, 19)) { novelAudio.Play("Back"); entryStep = 1; }
        AiModeControls(panel);
    }
    void Begin(int chosen)
    {
        if (aiMode && !AiProfessor.HasKey) { novelAudio.Play("Down"); ShowToast("AI 모드는 API 키를 먼저 저장해야 해요"); return; }
        AiReset();
        novelAudio.Play("Bonus");
        professor = chosen; scene = 1;
        backlog.Clear();   // A different professor reuses the same line keys.
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

    // Each faculty's own professor picture, from Professor.Image.
    bool ProfessorCard(Rect rect, int who, Professor info)
    {
        bool hover = NovelUI.Hover(rect);
        Rect face = hover ? new Rect(rect.x, rect.y - 5, rect.width, rect.height) : rect;
        if (hover) NovelUI.Glow(new Rect(face.x - 40, face.y - 40, face.width + 80, face.height + 80), NovelUI.Alpha(NovelUI.Pink, .45f));
        NovelUI.Card(face, Color.white, 30, hover ? NovelUI.DeepPink : NovelUI.Pink, hover ? 4 : 3);
        Rect photo = new Rect(face.x + 18, face.y + 18, face.width - 36, 210);
        NovelUI.Fill(photo, NovelUI.Blush, 22);
        string picture = Professor.Picture(info, who, hover ? (info.Image == "ProfessorKim" ? "Hover" : "Happy") : "");
        Texture2D portrait = Resources.Load<Texture2D>(picture);
        // Hovering zooms in on the face.
        if (portrait != null && !DrawPortraitInFrame(photo, portrait, hover ? .44f : .3f, hover ? .42f : .3f) && NovelUI.Repaint)
        {
            if (hover)
            {
                Rect closeUp = info.Image == "ProfessorBaekSoDam" ? new Rect(.37f, .70f, .26f, .30f)
                    : HasUpperBodyPortrait(info.Image) && !HasFullBodySource(info.Image) ? new Rect(.24f, .48f, .52f, .52f)
                    : FaceCrop(picture);
                DrawFittedPortrait(photo, portrait, closeUp);
            }
            else
            {
                DrawUpperBodyPortrait(photo, portrait, info.Image);
            }
        }
        NovelUI.Label(new Rect(face.x, photo.yMax + 14, face.width, 40), info.Name, NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(face.x + 20, photo.yMax + 56, face.width - 40, 30), info.Description, NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
        float a = NovelUI.Width(info.TagA, NovelUI.Round, 17) + 30, b = NovelUI.Width(info.TagB, NovelUI.Round, 17) + 30;
        float x = face.center.x - (a + b + 10) / 2;
        NovelUI.Pill(x, photo.yMax + 98, 32, info.TagA, NovelUI.Blush, NovelUI.DeepPink, 17);
        NovelUI.Pill(x + a + 10, photo.yMax + 98, 32, info.TagB, NovelUI.Blush, NovelUI.DeepPink, 17);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    void Ending()
    {
        // The ending card covers the middle of the screen, so the portrait would only peek out at the sides.
        DrawScene(false);
        GUI.matrix = Canvas;
        NovelUI.Tint(new Color(1f, .82f, .9f, .3f));
        int ending = Story.ResolveEnding(affection, questions, teamwork, interest, solo);
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
        NovelUI.Label(new Rect(card.x, card.y + 86, card.width, 64), S.EndingNames[ending], NovelUI.Round, 48, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(card.x + 70, card.y + 176, card.width - 140, 250), S.Ending(professor, ending), NovelUI.Soft, 23, NovelUI.Berry, TextAnchor.UpperLeft, FontStyle.Bold);

        string[] stats = { "♥ 호감도 " + affection, "질문 " + questions, "협업 " + teamwork, "관심 " + interest, "혼자 해결 " + solo };   // all four decide the ending
        float total = -14;
        foreach (string s in stats) total += NovelUI.Width(s, NovelUI.Round, 20) + 40 + 14;
        float x = 640 - total / 2;
        for (int i = 0; i < stats.Length; i++)
            x += NovelUI.Pill(x, card.y + 440, 40, stats[i], i == 0 ? NovelUI.DeepPink : NovelUI.Blush, i == 0 ? Color.white : NovelUI.Berry, 20) + 14;

        // The memorial (Memorial()) is not finished yet, so its button stays greyed out as 준비 중.
        if (trueEnd)
        {
            GUI.enabled = false;
            NovelUI.Button(new Rect(640 - 230, card.y + 520, 220, 62), "메모리얼 준비 중", false, 22);
            GUI.enabled = true;
        }
        Rect restart = trueEnd ? new Rect(650, card.y + 520, 220, 62) : new Rect(640 - 120, card.y + 520, 240, 62);
        if (NovelUI.Button(restart, "처음부터", true, 24)) { novelAudio.Play("Select"); ResetRun(); }
    }

    // Back to the title screen with a fresh first semester.
    void ResetRun()
    {
        scene = affection = entryStep = dialogueIndex = choiceStage = questions = teamwork = interest = solo = 0;
        professor = -1; showResult = false; resultLine = typed = ""; resultKey = null; skip = false; backlog.Clear(); wrongPick = -1;
        shownAffection = lastDelta = 0; lineKey = null; miniGame = null;
        endingRecorded = endingNew = false; chapterShown = 0; reactAt = -10; sparks.Clear();
        AiReset();
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
