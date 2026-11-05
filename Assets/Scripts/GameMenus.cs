using System.Collections.Generic;
using UnityEngine;

// Pop-up menus drawn over the game: settings, the five save slots, the ending book, the backlog and minigame practice.
// While one is open, OnGUI feeds the screen underneath only Repaint/Layout events, so it can't be clicked.
public partial class GameController
{
    enum Menu { None, Settings, SaveSlots, LoadSlots, Gallery, EndingDetail, Backlog, Practice }
    Menu menu;
    float menuAt, toastAt = -10;
    string toast;
    int confirmSlot = -1, galleryCollege, galleryFaculty, galleryProfessor, galleryEnding;
    bool confirmTitle;
    readonly GameSave.Data[] slots = new GameSave.Data[GameSave.Slots];
    // Lines shown this run, oldest first: { line key, speaker, text }.
    readonly List<string[]> backlog = new List<string[]>();
    Vector2 backlogScroll;

    static readonly string[] OffOn = { "끔", "켬" };
    const string Preview = "교수님, 오늘도 잘 부탁드려요! 두근두근한 첫 학기의 시작이에요.";

    static bool Pressed(KeyCode key)
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || e.keyCode != key) return false;
        e.Use();
        return true;
    }

    void OpenMenu(Menu next)
    {
        novelAudio.Play("Open");
        ReadLog.Flush();
        menu = next;
        menuAt = Time.unscaledTime;
        confirmSlot = -1;
        confirmTitle = false;
        for (int i = 0; i < slots.Length; i++) slots[i] = GameSave.Read(i);
        if (next == Menu.Gallery) { galleryFaculty = faculty; galleryCollege = Stories.CollegeOf(faculty); galleryProfessor = Mathf.Max(0, professor); }
        if (next == Menu.Practice) practiceCollege = Stories.CollegeOf(practiceFaculty);
        if (next == Menu.Backlog) backlogScroll = new Vector2(0, float.MaxValue);   // Opens on the newest line.
    }
    void CloseMenu()
    {
        novelAudio.Play("Close");
        if (menu == Menu.Settings) GameSettings.Save();
        menu = Menu.None;
        // Restart auto mode's wait so a long stay in a menu does not skip the line on close.
        if (typedAt >= 0) typedAt = Time.unscaledTime;
    }

    void ShowToast(string message) { toast = message; toastAt = Time.unscaledTime; }
    void Toast()
    {
        float k = Time.unscaledTime - toastAt;
        if (k > 2) return;
        float alpha = Mathf.Clamp01(k / .15f) * Mathf.Clamp01((2 - k) / .4f);
        float width = NovelUI.Width(toast, NovelUI.Round, 20) + 44 * .9f;
        NovelUI.Pill(640 - width / 2, 92 - (1 - alpha) * 10, 44, toast, NovelUI.Alpha(NovelUI.Berry, .92f * alpha), NovelUI.Alpha(Color.white, alpha), 20);
    }

    void DrawMenu()
    {
        if (Pressed(KeyCode.Escape))
        {
            if (menu == Menu.EndingDetail) { novelAudio.Play("Back"); menu = Menu.Gallery; }
            else CloseMenu();
            return;
        }
        NovelUI.Tint(NovelUI.Alpha(NovelUI.Berry, .45f));
        // Small pop-in.
        float pop = Mathf.Lerp(.94f, 1, Mathf.Clamp01((Time.unscaledTime - menuAt) / .15f));
        Matrix4x4 before = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(640, 450));
        if (menu == Menu.Settings) Settings();
        else if (menu == Menu.SaveSlots || menu == Menu.LoadSlots) Slots();
        else if (menu == Menu.Gallery) Gallery();
        else if (menu == Menu.EndingDetail) EndingDetail();
        else if (menu == Menu.Backlog) Backlog();
        else if (menu == Menu.Practice) PracticeMenu();
        GUI.matrix = before;
        GUI.enabled = true;
        // Clicks outside the panel land here instead of on the game.
        GUI.Button(NovelUI.Window, GUIContent.none, GUIStyle.none);
    }

    // Panel with an icon, a title and a close button; returns false once closed.
    bool Panel(Rect card, string icon, string title)
    {
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .98f), 36, NovelUI.Pink, 4);
        if (title != null)
        {
            float width = NovelUI.Width(title, NovelUI.Round, 32) + 52;
            NovelUI.DrawIcon(new Rect(card.center.x - width / 2, card.y + 30, 38, 38), icon, NovelUI.DeepPink);
            NovelUI.Label(new Rect(card.center.x - width / 2 + 52, card.y + 26, width, 46), title, NovelUI.Round, 32, NovelUI.Berry, TextAnchor.MiddleLeft);
        }
        if (!NovelUI.IconButton(new Rect(card.xMax - 70, card.y + 24, 46, 46), "IconClose", null)) return true;
        CloseMenu();
        return false;
    }
    static void RowLabel(Rect card, float y, string text) =>
        NovelUI.Label(new Rect(card.x + 48, y, 150, 46), text, NovelUI.Round, 22, NovelUI.Berry, TextAnchor.MiddleLeft);

    // ---- Settings -----------------------------------------------------------------------------

    void Settings()
    {
        Rect card = new Rect(300, 40, 680, 820);
        if (!Panel(card, "IconSettings", "설정")) return;
        float x = card.x + 200, width = card.width - 248, y = card.y + 98;
        const float row = 64;

        RowLabel(card, y, "글자 속도");
        GameSettings.TextSpeed = Changed(NovelUI.Segmented(new Rect(x, y, width, 46), GameSettings.SpeedNames, GameSettings.TextSpeed), GameSettings.TextSpeed);
        y += row;
        // Preview line typed at the chosen speed, on a loop.
        Rect sample = new Rect(x, y - 4, width, 50);
        NovelUI.Fill(sample, NovelUI.Blush, 16);
        float period = Preview.Length / GameSettings.LettersPerSecond + 1.4f;
        int shown = Mathf.Min(Preview.Length, Mathf.FloorToInt((Time.unscaledTime - menuAt) % period * GameSettings.LettersPerSecond));
        NovelUI.Label(new Rect(sample.x + 18, sample.y, sample.width - 30, sample.height), Preview.Substring(0, shown), NovelUI.Soft, 17, NovelUI.Berry, TextAnchor.MiddleLeft, FontStyle.Bold);
        NovelUI.Label(new Rect(card.x + 48, y - 4, 150, 50), "미리보기", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
        y += row;

        RowLabel(card, y, "자동 진행");
        GameSettings.Auto = Changed(NovelUI.Segmented(new Rect(x, y, width, 46), OffOn, GameSettings.Auto ? 1 : 0), GameSettings.Auto ? 1 : 0) == 1;
        y += row;
        RowLabel(card, y, "자동 속도");
        GUI.enabled = GameSettings.Auto;
        GameSettings.AutoSpeed = Changed(NovelUI.Segmented(new Rect(x, y, width, 46), GameSettings.AutoNames, GameSettings.AutoSpeed), GameSettings.AutoSpeed);
        GUI.enabled = true;
        y += row;

        RowLabel(card, y, "배경음");
        GameSettings.Music = NovelUI.Slider(new Rect(x, y, width - 76, 46), GameSettings.Music);
        NovelUI.Label(new Rect(x + width - 70, y, 70, 46), Mathf.RoundToInt(GameSettings.Music * 100) + "%", NovelUI.Round, 20, NovelUI.DeepPink, TextAnchor.MiddleRight);
        y += row;
        RowLabel(card, y, "효과음");
        GameSettings.Sfx = NovelUI.Slider(new Rect(x, y, width - 76, 46), GameSettings.Sfx);
        NovelUI.Label(new Rect(x + width - 70, y, 70, 46), Mathf.RoundToInt(GameSettings.Sfx * 100) + "%", NovelUI.Round, 20, NovelUI.DeepPink, TextAnchor.MiddleRight);
        y += row;

        RowLabel(card, y, "화면 효과");
        GameSettings.Effects = Changed(NovelUI.Segmented(new Rect(x, y, width, 46), OffOn, GameSettings.Effects ? 1 : 0), GameSettings.Effects ? 1 : 0) == 1;
        y += row;
        RowLabel(card, y, "화면 모드");
        int full = Screen.fullScreen ? 1 : 0;
        if (Changed(NovelUI.Segmented(new Rect(x, y, width, 46), new[] { "창 모드", "전체 화면" }, full), full) != full) SetFullScreen(full == 0);
        y += row;
        RowLabel(card, y, "AI API 키");
        AiKeyRow(x, y, width);

        Rect close = new Rect(card.xMax - 230, card.yMax - 82, 190, 54);
        if (InGame)
        {
            Rect title = new Rect(card.x + 40, card.yMax - 82, 220, 54);
            if (NovelUI.Button(title, confirmTitle ? "정말 나갈까요?" : "타이틀로", false, 21))
            {
                if (!confirmTitle) { confirmTitle = true; novelAudio.Play("Select"); ShowToast("저장하지 않은 진행은 사라져요"); }
                else { GameSettings.Save(); novelAudio.Play("Back"); ResetRun(); menu = Menu.None; }
            }
        }
        if (NovelUI.Button(close, "닫기", true, 22)) CloseMenu();
    }

    // Plays the toggle sound when a control's value changes.
    int Changed(int value, int previous)
    {
        if (value != previous) novelAudio.Play("Toggle");
        return value;
    }

    static void SetFullScreen(bool on)
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer) { Screen.fullScreen = on; return; }
        if (on) { Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow); return; }
        int height = Mathf.Min(900, Mathf.RoundToInt(Display.main.systemHeight * .85f));
        Screen.SetResolution(Mathf.RoundToInt(height * 1280f / 900), height, FullScreenMode.Windowed);
    }

    // ---- Save slots ---------------------------------------------------------------------------

    void Slots()
    {
        Rect card = new Rect(200, 60, 880, 790);
        bool saving = menu == Menu.SaveSlots;
        if (!Panel(card, "IconSave", InGame ? null : "불러오기")) return;
        if (InGame)
        {
            int tab = NovelUI.Segmented(new Rect(card.center.x - 170, card.y + 26, 340, 50), new[] { "저장하기", "불러오기" }, saving ? 0 : 1, 21);
            if (tab != (saving ? 0 : 1)) { novelAudio.Play("Toggle"); menu = tab == 0 ? Menu.SaveSlots : Menu.LoadSlots; confirmSlot = -1; saving = tab == 0; }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            GameSave.Data data = slots[i];
            Rect row = new Rect(card.x + 44, card.y + 100 + i * 120, card.width - 88, 106);
            GUI.enabled = saving || data != null;
            bool hover = NovelUI.Hover(row), confirm = confirmSlot == i;
            Rect face = hover ? new Rect(row.x, row.y - 2, row.width, row.height) : row;
            NovelUI.Card(face, hover || confirm ? NovelUI.Blush : GUI.enabled ? Color.white : new Color(.97f, .94f, .95f), 26, hover || confirm ? NovelUI.DeepPink : NovelUI.Alpha(NovelUI.Pink, GUI.enabled ? 1 : .5f), confirm ? 4 : 3);
            Rect badge = new Rect(face.x + 22, face.center.y - 22, 44, 44);
            NovelUI.Fill(badge, data != null ? NovelUI.Pink : NovelUI.Blush, 22);
            NovelUI.Label(badge, (i + 1).ToString(), NovelUI.Round, 22, data != null ? Color.white : NovelUI.Plum, TextAnchor.MiddleCenter);
            if (data == null)
                NovelUI.Label(new Rect(face.x + 90, face.y, 400, face.height), saving ? "빈 슬롯 · 여기에 저장" : "빈 슬롯", NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
            else
            {
                Rect photo = new Rect(face.x + 86, face.y + 12, 82, 82);
                NovelUI.Fill(photo, NovelUI.Blush, 20);
                string picture = Professor.Picture(Stories.All[data.Faculty].Professors[data.Professor], data.Professor);
                Texture2D portrait = Resources.Load<Texture2D>(picture);
                if (portrait != null && !DrawPortraitInFrame(photo, portrait, .5f, .46f) && NovelUI.Repaint) GUI.DrawTextureWithTexCoords(photo, portrait, FaceCrop(picture, true));
                float tx = photo.xMax + 20;
                tx += NovelUI.Pill(tx, face.y + 18, 36, "CHAPTER " + data.Scene, NovelUI.Pink, Color.white, 17) + 10;
                Story story = Stories.All[data.Faculty];
                NovelUI.Label(new Rect(tx, face.y + 14, 300, 44), story.Chapters[data.Scene], NovelUI.Round, 25, NovelUI.Berry, TextAnchor.MiddleLeft);
                string detail = story.Short + "  ·  " + story.Professors[data.Professor].Name + "  ·  ♥ " + data.Affection + "  ·  " + story.Levels[data.Experienced ? 0 : 1];
                NovelUI.Label(new Rect(photo.xMax + 22, face.y + 60, 420, 34), detail, NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
                NovelUI.Label(new Rect(face.xMax - 220, face.y + 14, 196, 40), data.SavedAt.ToString("MM.dd HH:mm"), NovelUI.Round, 19, NovelUI.Plum, TextAnchor.MiddleRight);
                if (confirm) NovelUI.Pill(face.xMax - 250, face.y + 58, 34, "한 번 더 누르면 덮어써요", NovelUI.DeepPink, Color.white, 16);
            }
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                if (!saving) LoadFrom(data);
                else if (data == null || confirm) SaveTo(i);
                else { confirmSlot = i; novelAudio.Play("Select"); }
            }
            GUI.enabled = true;
        }
        string hint = saving ? "언제든 저장해 두고 다른 선택을 해 보세요!" : "불러올 슬롯을 고르세요";
        NovelUI.Label(new Rect(card.x, card.yMax - 82, card.width, 40), hint, NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    void SaveTo(int slot)
    {
        GameSave.Write(slot, new GameSave.Data
        {
            Faculty = faculty, Scene = scene, Stage = choiceStage, Line = dialogueIndex, Professor = professor, Affection = affection,
            Questions = questions, Teamwork = teamwork, Interest = interest, Solo = solo, Experienced = experienced,
            ShowResult = showResult, ResultLine = resultLine, ResultKey = resultKey, WrongPick = wrongPick, AiMode = aiMode,
            // A question still waiting for its reply is not counted: the reply is lost with the save.
            HintsUsed = aiSlot == AiSlotKey ? Mathf.Max(0, aiHintsUsed - (AiWaiting && aiPendingHint ? 1 : 0)) : 0,
            Hint = aiSlot == AiSlotKey ? aiHint : null
        });
        slots[slot] = GameSave.Read(slot);
        confirmSlot = -1;
        novelAudio.Play("Save");
        ShowToast((slot + 1) + "번 슬롯에 저장했어요");
    }

    void LoadFrom(GameSave.Data data)
    {
        ResetRun();
        faculty = data.Faculty; scene = data.Scene; choiceStage = data.Stage; professor = data.Professor;
        affection = shownAffection = data.Affection;
        questions = data.Questions; teamwork = data.Teamwork; interest = data.Interest; solo = data.Solo;
        experienced = data.Experienced; showResult = data.ShowResult; resultLine = data.ResultLine; resultKey = data.ResultKey; wrongPick = data.WrongPick; aiMode = data.AiMode && AiProfessor.Supported;
        // The story may have changed since the save was made, so keep the line inside the chapter.
        dialogueIndex = Mathf.Clamp(data.Line, 0, S.Lines(scene, choiceStage) - 1);
        // Claim this slot now so AiSync keeps the saved count instead of resetting it to 0.
        aiSlot = AiSlotKey;
        aiHintsUsed = Mathf.Min(data.HintsUsed, HintLimit);
        aiHint = data.Hint;
        memorial = 0; introDone = true; portraitAlpha = 0;
        chapterShown = -1;   // Replays the chapter card as a reminder of where the player is.
        menu = Menu.None;
        novelAudio.Play("Load");
        ShowToast("불러왔어요");
    }

    static bool HasUpperBodyPortrait(string image)
    {
        if (HasFullBodySource(image)) return true;
        switch (image)
        {
            case "ProfessorTakGiHun":
            case "ProfessorHaEunByeol":
            case "ProfessorJinSeoHan":
            case "ProfessorNaYunSeul":
            case "ProfessorOhSeRin":
            case "ProfessorBaeJunWoo":
            case "ProfessorKwonDoKyung":
            case "ProfessorBaekSoDam":
            case "ProfessorJoMinHyuk":
            case "ProfessorSongDaOn":
                return true;
            default:
                return false;
        }
    }

    static bool HasFullBodySource(string image)
    {
        return image == "ProfessorPark" || image == "ProfessorChoi" ||
            image == "ProfessorHan" || image == "ProfessorYoon" ||
            image == "ProfessorSeo" || image == "ProfessorKang";
    }

    static void DrawUpperBodyPortrait(Rect frame, Texture2D texture, string image)
    {
        Rect crop = new Rect(0, 0, 1, 1);
        if (image == "ProfessorBaekSoDam" || image == "ProfessorKim")
            crop = new Rect(.34f, .50f, .32f, .50f);
        else if (HasFullBodySource(image))
            crop = new Rect(.34f, .46f, .32f, .54f);
        else if (texture.height > texture.width)
            crop = new Rect(0, .24f, 1, .76f);
        DrawFittedPortrait(frame, texture, crop);
    }

    static void DrawFittedPortrait(Rect frame, Texture2D texture, Rect crop)
    {
        float aspect = (float)texture.width / texture.height * crop.width / crop.height;
        float height = Mathf.Min(frame.height, frame.width / aspect);
        float width = height * aspect;
        Rect fitted = new Rect(frame.center.x - width / 2, frame.yMax - height, width, height);
        GUI.DrawTextureWithTexCoords(fitted, texture, crop);
    }

    // Crop for the compact portrait thumbnails in menus.
    static Rect FaceCrop(string picture, bool square = false)
    {
        string image = picture.Replace("HappyTransparent", "").Replace("FrownTransparent", "").Replace("Transparent", "");
        if (HasFullBodySource(image)) return square ? new Rect(.428f, .735f, .144f, .265f) : new Rect(.37f, .73f, .26f, .27f);
        if (picture.StartsWith("ProfessorBaekSoDam")) return square ? new Rect(.35f, .45f, .30f, .55f) : new Rect(.22f, .45f, .56f, .55f);
        if (picture.StartsWith("ProfessorKim")) return square ? new Rect(.418f, .66f, .164f, .3f) : new Rect(.31f, .6f, .38f, .4f);
        if (picture.StartsWith("ProfessorLee")) return square ? new Rect(.385f, .55f, .229f, .42f) : new Rect(.22f, .4f, .56f, .6f);
        if (picture.StartsWith("ProfessorJungChem") || picture.StartsWith("ProfessorOhChem") ||
            picture.StartsWith("ProfessorGuMath") || picture.StartsWith("ProfessorSeolMath") ||
            picture.StartsWith("ProfessorMoonBio") || picture.StartsWith("ProfessorLimBio") ||
            picture.StartsWith("ProfessorShinDoYoon") || picture.StartsWith("ProfessorRyuBoRa") ||
            picture.StartsWith("ProfessorNamgungHyun") || picture.StartsWith("ProfessorHaEunChae") ||
            picture.StartsWith("ProfessorEomTaeO") || picture.StartsWith("ProfessorChaYuRi"))
            return square ? new Rect(.05f, .38f, .9f, .6f) : new Rect(.03f, .64f, .94f, .34f);
        return square ? new Rect(.382f, .514f, .236f, .432f) : new Rect(.218f, .352f, .564f, .605f);
    }

    // College tabs, then the faculty tabs inside the chosen college (two rows from rect.y, 100 high).
    // Returns false and shows "준비 중" while the college has no faculty yet.
    bool FacultyTabs(Rect rect, ref int college, ref int faculty, bool shortNames)
    {
        int pick = NovelUI.Segmented(new Rect(rect.x, rect.y, rect.width, 46), Stories.Colleges, college, 18);
        if (pick != college) { novelAudio.Play("Toggle"); college = pick; }
        int[] faculties = Stories.In(college);
        if (faculties.Length == 0)
        {
            NovelUI.Label(new Rect(rect.x, rect.y + 120, rect.width, 90), "준비 중인 학부예요\n곧 새 학과가 열려요!", NovelUI.Soft, 22, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            return false;
        }
        int at = System.Array.IndexOf(faculties, faculty);
        if (at < 0) { at = 0; faculty = faculties[0]; }
        string[] names = new string[faculties.Length];
        for (int i = 0; i < names.Length; i++) names[i] = shortNames ? Stories.All[faculties[i]].Short : Stories.All[faculties[i]].Name;
        pick = NovelUI.Segmented(new Rect(rect.x + 60, rect.y + 54, rect.width - 120, 42), names, at, 17);
        if (pick != at) { novelAudio.Play("Toggle"); faculty = faculties[pick]; }
        return true;
    }

    // ---- Ending book --------------------------------------------------------------------------

    void Gallery()
    {
        Rect card = new Rect(90, 40, 1100, 820);
        if (!Panel(card, "IconBook", "엔딩 도감")) return;
        if (!FacultyTabs(new Rect(card.x + 40, card.y + 88, card.width - 80, 0), ref galleryCollege, ref galleryFaculty, false)) return;
        Story story = Stories.All[galleryFaculty];

        string[] names = {
            story.Professors[0].Name + "  " + EndingBook.Count(galleryFaculty, 0) + " / " + EndingBook.Endings,
            story.Professors[1].Name + "  " + EndingBook.Count(galleryFaculty, 1) + " / " + EndingBook.Endings
        };
        int who = NovelUI.Segmented(new Rect(card.center.x - 270, card.y + 196, 540, 48), names, galleryProfessor, 20);
        if (who != galleryProfessor) { novelAudio.Play("Toggle"); galleryProfessor = who; }

        // Every locked ending looks the same, so the true ending stays a surprise until it is found.
        const float width = 235, height = 250, gap = 20;
        float left = card.center.x - (width * 4 + gap * 3) / 2;
        for (int i = 0; i < EndingBook.Endings; i++)
        {
            Rect cell = new Rect(left + i % 4 * (width + gap), card.y + 260 + i / 4 * (height + gap), width, height);
            bool seen = EndingBook.Seen(galleryFaculty, galleryProfessor, i);
            string number = "ENDING " + (i + 1);
            if (!seen)
            {
                NovelUI.Fill(cell, new Color(.97f, .91f, .94f), 26);
                NovelUI.Ring(cell, NovelUI.Alpha(NovelUI.Pink, .45f), 26, 3);
                float w = NovelUI.Width(number, NovelUI.Round, 16) + 32 * .9f;
                NovelUI.Pill(cell.center.x - w / 2, cell.y + 18, 32, number, NovelUI.Alpha(NovelUI.Plum, .25f), NovelUI.Plum, 16);
                NovelUI.DrawIcon(new Rect(cell.center.x - 30, cell.y + 82, 60, 60), "IconLock", NovelUI.Alpha(NovelUI.Plum, .55f));
                NovelUI.Label(new Rect(cell.x, cell.y + 160, cell.width, 44), "???", NovelUI.Round, 30, NovelUI.Alpha(NovelUI.Plum, .7f), TextAnchor.MiddleCenter);
                continue;
            }
            bool special = i == 7;
            bool hover = NovelUI.Hover(cell);
            Rect face = hover ? new Rect(cell.x, cell.y - 4, cell.width, cell.height) : cell;
            if (special || hover) NovelUI.Glow(new Rect(face.x - 30, face.y - 30, face.width + 60, face.height + 60), NovelUI.Alpha(special ? NovelUI.Lemon : NovelUI.Pink, .5f));
            NovelUI.Card(face, Color.white, 26, special ? NovelUI.DeepPink : hover ? NovelUI.DeepPink : NovelUI.Pink, special ? 4 : 3);
            float pw = NovelUI.Width(number, NovelUI.Round, 16) + 32 * .9f;
            NovelUI.Pill(face.center.x - pw / 2, face.y + 18, 32, number, special ? NovelUI.DeepPink : NovelUI.Pink, Color.white, 16);
            NovelUI.Heart(new Rect(face.center.x - 32, face.y + 70 + Mathf.Sin(Time.unscaledTime * 3 + i) * 3, 64, 64), special ? NovelUI.DeepPink : NovelUI.Pink);
            if (special)
                for (int s = 0; s < 3; s++)
                    NovelUI.DrawIcon(new Rect(face.center.x - 62 + s * 50 + (s == 1 ? 6 : 0), face.y + 56 + (s == 1 ? -12 : 0), 26, 26), "IconStar", NovelUI.Lemon);
            // Long names shrink rather than break mid-word.
            string name = story.EndingNames[i];
            int size = NovelUI.Width(name, NovelUI.Round, 22) > face.width - 28 ? 18 : 22;
            NovelUI.Label(new Rect(face.x + 14, face.y + 148, face.width - 28, 84), name, NovelUI.Round, size, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) { novelAudio.Play("Select"); galleryEnding = i; menu = Menu.EndingDetail; menuAt = Time.unscaledTime; }
        }
    }

    void EndingDetail()
    {
        Rect card = new Rect(170, 100, 940, 700);
        if (!Panel(card, null, null)) return;
        Story story = Stories.All[galleryFaculty];
        bool special = galleryEnding == 7;
        string number = "ENDING " + (galleryEnding + 1);
        float width = NovelUI.Width(number, NovelUI.Round, 22) + 50 * .9f;
        NovelUI.Pill(640 - width / 2, card.y - 24, 50, number, special ? NovelUI.DeepPink : NovelUI.Pink, Color.white, 22);
        Rect photo = new Rect(card.x + 50, card.y + 60, 150, 150);
        NovelUI.Fill(photo, NovelUI.Blush, 30);
        string picture = Professor.Picture(story.Professors[galleryProfessor], galleryProfessor, galleryEnding == 0 ? "Frown" : "Happy");   // The failed ending keeps its sad face.
        Texture2D portrait = Resources.Load<Texture2D>(picture);
        if (portrait != null && !DrawPortraitInFrame(photo, portrait, .5f, .46f) && NovelUI.Repaint) GUI.DrawTextureWithTexCoords(photo, portrait, FaceCrop(picture, true));
        NovelUI.Label(new Rect(photo.xMax + 30, card.y + 70, card.width - 300, 64), story.EndingNames[galleryEnding], NovelUI.Round, 42, NovelUI.Berry, TextAnchor.MiddleLeft);
        NovelUI.Pill(photo.xMax + 30, card.y + 148, 40, story.Professors[galleryProfessor].Name, NovelUI.Blush, NovelUI.DeepPink, 20);
        NovelUI.Label(new Rect(card.x + 60, card.y + 250, card.width - 120, 330), story.Ending(galleryProfessor, galleryEnding), NovelUI.Soft, 22, NovelUI.Berry, TextAnchor.UpperLeft, FontStyle.Bold);
        if (NovelUI.Button(new Rect(640 - 110, card.yMax - 86, 220, 58), "목록으로", true, 22)) { novelAudio.Play("Back"); menu = Menu.Gallery; menuAt = Time.unscaledTime; }
    }

    // ---- Backlog ------------------------------------------------------------------------------

    // `replace`: a line sent again under the same key (an AI answer or question re-sent after a failure, or answered
    // anew after stepping back) supersedes the old one and moves to the end, so the next AI request sees what the
    // player actually said last.
    void AddToBacklog(string key, string speaker, string text, bool replace = false)
    {
        if (replace) backlog.RemoveAll(entry => entry[0] == key);
        // Stepping back and forward again revisits lines that are already listed.
        else foreach (string[] entry in backlog) if (entry[0] == key) return;
        backlog.Add(new[] { key, speaker, text });
        if (backlog.Count > 300) backlog.RemoveAt(0);
    }

    void Backlog()
    {
        Rect card = new Rect(140, 50, 1000, 800);
        if (!Panel(card, "IconBook", "대사 기록")) return;
        Rect view = new Rect(card.x + 44, card.y + 100, card.width - 88, card.height - 140);
        if (backlog.Count == 0)
        {
            NovelUI.Label(view, "아직 기록이 없어요", NovelUI.Soft, 22, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            return;
        }
        float width = view.width - 30, total = 0;
        foreach (string[] entry in backlog) total += EntryHeight(entry, width);
        float overflow = Mathf.Max(0, total - view.height);
        backlogScroll.y = Mathf.Clamp(backlogScroll.y, 0, overflow);
        backlogScroll = GUI.BeginScrollView(view, backlogScroll, new Rect(0, 0, width, total), GUIStyle.none, GUIStyle.none);
        float y = 0;
        foreach (string[] entry in backlog)
        {
            float height = EntryHeight(entry, width), top = y;
            y += height;
            if (y < backlogScroll.y || top > backlogScroll.y + view.height) continue;
            bool narration = entry[1] == "나레이션";
            if (!narration) NovelUI.Pill(0, top, 34, entry[1], SpeakerTag(entry[1]), SpeakerInk(entry[1]), 17);
            NovelUI.Label(new Rect(4, top + (narration ? 0 : 42), width - 8, height), entry[2], NovelUI.Soft, 21, narration ? NovelUI.Plum : NovelUI.Berry);
            NovelUI.Fill(new Rect(0, y - 14, width, 2), NovelUI.Blush, 1);
        }
        GUI.EndScrollView();
        if (overflow <= 0) return;
        // Scroll position bar; the wheel scrolls the list.
        Rect track = new Rect(view.xMax - 8, view.y, 8, view.height);
        NovelUI.Fill(track, NovelUI.Blush, 4);
        float thumb = Mathf.Max(40, track.height * view.height / total);
        NovelUI.Fill(new Rect(track.x, track.y + (track.height - thumb) * backlogScroll.y / overflow, 8, thumb), NovelUI.Pink, 4);
    }
    static float EntryHeight(string[] entry, float width) =>
        (entry[1] == "나레이션" ? 0 : 42) + NovelUI.Height(entry[2], NovelUI.Soft, 21, width - 8) + 30;

    // ---- Minigame practice --------------------------------------------------------------------

    void PracticeMenu()
    {
        Rect card = new Rect(290, 60, 700, 780);
        if (!Panel(card, "IconStar", "미니게임 연습")) return;
        NovelUI.Label(new Rect(card.x, card.y + 84, card.width, 30), "연습 결과는 호감도와 엔딩에 반영되지 않아요", NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (!FacultyTabs(new Rect(card.x + 30, card.y + 124, card.width - 60, 0), ref practiceCollege, ref practiceFaculty, true)) return;
        Story story = Stories.All[practiceFaculty];
        bool hasDifficulty = false;
        foreach (Slot slot in story.Slots)
            if (slot.Game != null && (slot.Game.Kind == MiniGameKind.ImpactLab || slot.Game.Kind == MiniGameKind.BugHunt || slot.Game.Kind == MiniGameKind.CircuitProbe))
                hasDifficulty = true;
        bool hasImpactLab = hasDifficulty;
        float y = card.y + 240;
        foreach (Slot slot in story.Slots)
        {
            if (slot.Game == null) continue;
            float tileHeight = hasImpactLab ? 90 : 110;
            if (Tile(new Rect(card.x + 50, y, card.width - 100, tileHeight), slot.Game.Title, slot.Game.Note, false)) StartPractice(slot.Game);
            y += hasImpactLab ? 98 : 128;
        }
        if (hasImpactLab)
        {
            NovelUI.Label(new Rect(card.x, card.y + 548, card.width, 30), "연습 난이도", NovelUI.Round, 20, NovelUI.Berry, TextAnchor.MiddleCenter);
            int mode = NovelUI.Segmented(new Rect(card.center.x - 190, card.y + 580, 380, 44), new[] { "비전공자", "전공자" }, practiceExperienced ? 1 : 0, 19);
            if ((mode == 1) != practiceExperienced) { novelAudio.Play("Toggle"); practiceExperienced = mode == 1; }
        }
        NovelUI.Label(new Rect(card.x, card.y + 640, card.width, 30), "함께할 교수님", NovelUI.Round, 20, NovelUI.Berry, TextAnchor.MiddleCenter);
        int who = NovelUI.Segmented(new Rect(card.center.x - 220, card.y + 676, 440, 48), new[] { story.Professors[0].Name, story.Professors[1].Name }, practiceProfessor, 19);
        if (who != practiceProfessor) { novelAudio.Play("Toggle"); practiceProfessor = who; }
    }

    void StartPractice(MiniGameTheme theme)
    {
        novelAudio.Play("Select");
        practice = theme;
        MiniGame.Easy = !practiceExperienced;
        miniGame = MiniGames.Create(theme, practiceProfessor, true, practiceExperienced);
        menu = Menu.None;
    }
    void EndPractice()
    {
        practice = null;
        miniGame = null;
        OpenMenu(Menu.Practice);
    }

    // A practice run over the chapter's own background; the close icon under the settings icon quits it.
    void Practice()
    {
        GUI.DrawTexture(Drift(new Rect(0, 0, Screen.width, Screen.height)), Resources.Load<Texture2D>(practice.Place), ScaleMode.ScaleAndCrop);
        GUI.matrix = Canvas;
        DrawMiniGame();
        if (NovelUI.IconButton(new Rect(1228, 10, 44, 44), "IconSettings", null)) OpenMenu(Menu.Settings);
        if (NovelUI.IconButton(new Rect(1228, 62, 44, 44), "IconClose", null)) EndPractice();
    }
}
