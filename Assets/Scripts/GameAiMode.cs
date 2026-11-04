using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// AI mode (AiProfessor): at choice slots the player types their own answer and the professor model replies
// in a tone set by the current affection, scoring it against the slot's designed options; at quiz slots the
// player may ask the professor up to three questions for hints. Minigames are unchanged.
public partial class GameController
{
    const int HintLimit = 3;
    bool aiMode;                 // Chosen on the professor screen; kept in save slots.
    string aiKeyDraft = "";
    int aiRun;                   // Bumped on every new run or load so a late reply can't land in another game.
    string aiSlot;               // The slot the state below belongs to.
    string aiTyped = "", aiQuestion = "", aiHint, aiError;
    bool aiShowOptions, aiAsking;
    int aiHintsUsed, aiMin, aiMax;
    Task<AiProfessor.Result> aiPending;
    string aiPendingSlot;
    bool aiPendingHint;
    string aiAnswer = "";        // The free answer being graded.
    bool aiImeWanted;            // An AI text field had focus during the last GUI pass.

    // IMGUI only receives Hangul when IME composition is forced on; Auto drops it (notably in the Editor's Game view).
    void AiImeUpdate()
    {
        Input.imeCompositionMode = aiImeWanted ? IMECompositionMode.On : IMECompositionMode.Auto;
        aiImeWanted = false;
    }
    // A syllable still being composed when Enter or the button is pressed would otherwise be lost.
    static string WithComposition(string text) => (text + Input.compositionString).Trim();

    string AiSlotKey => aiRun + "/" + faculty + "/" + professor + "/" + scene + "/" + choiceStage;
    bool AiWaiting => aiPending != null;
    // The professor's latest hint replaces the quiz's question line in the dialogue box.
    bool AiHintShown => aiMode && aiHint != null && !showResult && AtChoice && aiSlot == AiSlotKey;
    bool AiBoxField => aiMode && aiAsking && !showResult && AtChoice;

    void AiReset()
    {
        aiRun++;
        aiPending = null;
        aiSlot = null;
        aiTyped = aiQuestion = "";
        aiHint = aiError = null;
        aiShowOptions = aiAsking = false;
        aiHintsUsed = 0;
    }

    // Per-slot state starts fresh whenever the slot on screen changes.
    void AiSync()
    {
        string key = AiSlotKey;
        if (aiSlot == key) return;
        aiSlot = key;
        aiTyped = aiQuestion = "";
        aiHint = aiError = null;
        aiShowOptions = aiAsking = false;
        aiHintsUsed = 0;
    }

    // Applies a finished request on the main thread; replies for a slot no longer on screen are dropped.
    void AiPoll()
    {
        if (aiPending == null || !aiPending.IsCompleted) return;
        AiProfessor.Result result = aiPending.IsFaulted || aiPending.IsCanceled ? new AiProfessor.Result { Error = "연결 실패" } : aiPending.Result;
        aiPending = null;
        if (aiPendingSlot != AiSlotKey || showResult || miniGame != null) return;
        if (result.Error != null)
        {
            aiError = result.Error;
            if (aiPendingHint) aiHintsUsed--;   // A failed question doesn't use up a turn.
            novelAudio.Play("Down");
            return;
        }
        if (aiPendingHint)
        {
            aiHint = AiProfessor.Clean(result.Text);
            novelAudio.Play("Select");
            return;
        }
        Slot slot = S.Slot(scene, choiceStage);
        AiProfessor.Grade grade = AiProfessor.GradeAnswer(result.Text, aiAnswer, slot.Scores[professor], slot.Stats, aiMin, aiMax);
        // The labels and the rule that set the score, for tuning (Console only).
        Debug.Log("[AI 채점] " + ProfessorName + " · \"" + aiAnswer + "\" → " + grade.Delta + " (" + grade.Why + ")\n" + result.Text);
        int delta = grade.Delta;
        Raise(grade.Stat);
        affection += delta;
        React(delta, true);
        resultLine = grade.Reply + AffectionLine(delta);
        resultKey = null;   // A generated reply is new text every time, so skip mode stops on it.
        showResult = true;
        aiTyped = "";
    }

    List<string[]> Recent()
    {
        int count = Mathf.Min(8, backlog.Count);
        return backlog.GetRange(backlog.Count - count, count);
    }

    void AiSend(string system, string user, bool hint)
    {
        aiError = null;
        aiPendingSlot = AiSlotKey;
        aiPendingHint = hint;
        aiPending = AiProfessor.Ask(system, user);
    }

    // Called from Choices(). Returns true when it drew the whole choice area (a free-answer slot).
    bool AiChoices(Slot slot, Quiz quiz, float bottom)
    {
        AiSync();
        if (quiz != null) { AiQuizQuestion(quiz); return false; }
        if (aiShowOptions)
        {
            float top = bottom - slot.Options.Length * (62 + 14) + 14;
            if (NovelUI.Button(new Rect(920, top - 56, 170, 40), "AI로 대답하기", false, 17)) { novelAudio.Play("Toggle"); aiShowOptions = false; }
            return false;
        }

        Rect card = new Rect(170, bottom - 150, 940, 136);
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 30, NovelUI.Pink);
        NovelUI.Pill(card.x + 26, card.y - 18, 36, "AI 자유 대답", NovelUI.Mint, NovelUI.Berry, 18);
        if (!AiWaiting && NovelUI.Button(new Rect(card.xMax - 190, card.y - 22, 170, 40), "선택지로 고르기", false, 17)) { novelAudio.Play("Toggle"); aiShowOptions = true; return true; }

        Rect field = new Rect(card.x + 30, card.y + 34, 680, 58);
        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if (AiWaiting)
        {
            NovelUI.Card(field, NovelUI.Blush, 29);
            string dots = new string('.', 1 + (int)(Time.unscaledTime * 3) % 3);
            NovelUI.Label(new Rect(field.x + 28, field.y, field.width - 40, field.height), ProfessorName + "님이 생각 중" + dots, NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
        }
        else aiTyped = AiField(field, "FreeAnswer", aiTyped, 80, "교수님께 내 말로 대답해 보세요", true);

        GUI.enabled = !AiWaiting && WithComposition(aiTyped) != "";
        if (NovelUI.Button(new Rect(card.x + 725, card.y + 34, 185, 58), "대답하기", true, 22) || (enter && GUI.enabled))
        {
            if (enter) Event.current.Use();
            novelAudio.Play("Select");
            string answer = aiAnswer = WithComposition(aiTyped);
            AddToBacklog("ai/" + AiSlotKey + "/answer", "나", answer);
            string system = AiProfessor.AnswerPrompt(S, professor, affection, slot, Recent(), out aiMin, out aiMax);
            AiSend(system, "학생의 대답: " + answer, false);
        }
        GUI.enabled = true;

        string note = aiError != null ? "앗, " + aiError + " · 다시 보내거나 선택지로 골라도 돼요" : "AI 교수님이 대답을 듣고 호감도를 정해요 (Enter로 보내기)";
        NovelUI.Label(new Rect(card.x + 34, card.y + 98, card.width - 60, 30), note, NovelUI.Soft, 17, aiError != null ? NovelUI.DeepPink : NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
        return true;
    }

    // Quiz slots: a "질문하기" button on the dialogue box; the question field sits inside the box and the
    // professor's answer replaces the line above it.
    void AiQuizQuestion(Quiz quiz)
    {
        Rect box = new Rect(70, 628, 1140, 244);
        int left = HintLimit - aiHintsUsed;
        if (!aiAsking)
        {
            GUI.enabled = left > 0 && !AiWaiting;
            if (NovelUI.Button(new Rect(box.xMax - 590, box.y - 22, 184, 44), left > 0 ? "교수님께 질문 " + left : "질문 끝", true, 18)) { novelAudio.Play("Open"); aiAsking = true; aiError = null; }
            GUI.enabled = true;
            if (aiError != null) NovelUI.Label(new Rect(box.x + 42, box.yMax - 44, box.width - 100, 30), "앗, " + aiError, NovelUI.Soft, 17, NovelUI.DeepPink, TextAnchor.MiddleLeft, FontStyle.Bold);
            return;
        }

        if (NovelUI.Button(new Rect(box.xMax - 590, box.y - 22, 184, 44), "질문 닫기", false, 18)) { novelAudio.Play("Close"); aiAsking = false; return; }
        Rect field = new Rect(box.x + 42, box.yMax - 80, box.width - 290, 56);
        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if (AiWaiting)
        {
            NovelUI.Card(field, NovelUI.Blush, 28);
            string dots = new string('.', 1 + (int)(Time.unscaledTime * 3) % 3);
            NovelUI.Label(new Rect(field.x + 26, field.y, field.width - 40, field.height), "교수님이 생각 중" + dots, NovelUI.Soft, 20, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
        }
        else aiQuestion = AiField(field, "HintQuestion", aiQuestion, 80, aiError != null ? "앗, " + aiError : "무엇이 궁금한가요? (정답은 안 알려 주세요)", true);

        GUI.enabled = !AiWaiting && WithComposition(aiQuestion) != "" && left > 0;
        if (NovelUI.Button(new Rect(box.xMax - 232, box.yMax - 80, 170, 56), "물어보기", true, 21) || (enter && GUI.enabled))
        {
            if (enter) Event.current.Use();
            novelAudio.Play("Select");
            string question = WithComposition(aiQuestion);
            aiHintsUsed++;
            AddToBacklog("ai/" + AiSlotKey + "/q" + aiHintsUsed, "나", question);
            AiSend(AiProfessor.HintPrompt(S, professor, affection, quiz, Recent()), "학생의 질문: " + question, true);
            aiQuestion = "";
        }
        GUI.enabled = true;
    }

    // A cream rounded text field with a pink focus ring; `focus` keeps the caret in it.
    string AiField(Rect rect, string name, string value, int max, string placeholder, bool focus)
    {
        NovelUI.Card(rect, NovelUI.Cream, rect.height / 2);
        GUIStyle style = new GUIStyle(NovelUI.Input) { fontSize = 21, font = NovelUI.Soft, border = new RectOffset() };
        style.normal.background = style.focused.background = style.hover.background = null;
        GUI.skin.settings.cursorColor = NovelUI.DeepPink;
        GUI.SetNextControlName(name);
        value = GUI.TextField(rect, value, max, style);
        if (focus) GUI.FocusControl(name);
        bool active = GUI.GetNameOfFocusedControl() == name;
        if (active)
        {
            aiImeWanted = true;
            // The IME candidate window opens at the caret's end of the field, in screen pixels.
            Vector2 screen = GUIUtility.GUIToScreenPoint(new Vector2(rect.x + 30, rect.yMax));
            Input.compositionCursorPos = screen;
        }
        float pulse = .65f + .35f * Mathf.Sin(Time.unscaledTime * 4);
        NovelUI.Ring(rect, active ? NovelUI.Alpha(NovelUI.DeepPink, pulse) : NovelUI.Pink, rect.height / 2, active ? 4 : 3);
        if (value == "" && !(active && Input.compositionString != "")) NovelUI.Label(new Rect(rect.x + 30, rect.y, rect.width - 40, rect.height), placeholder, NovelUI.Soft, 19, NovelUI.Alpha(NovelUI.Plum, .6f), TextAnchor.MiddleLeft);
        return value;
    }

    // The AI mode switch along the professor screen's bottom edge; the key itself is entered in Settings.
    void AiModeControls(Rect panel)
    {
        Rect toggle = new Rect(panel.xMax - 250, panel.yMax - 62, 220, 44);
        if (!AiProfessor.Supported)
        {
            GUI.enabled = false;
            NovelUI.Button(toggle, "AI 모드 (PC 전용)", false, 18);
            GUI.enabled = true;
            return;
        }
        if (NovelUI.Button(toggle, aiMode ? "AI 모드 켜짐" : "AI 모드 끔", aiMode, 19))
        {
            aiMode = !aiMode;
            novelAudio.Play("Toggle");
            ShowToast(aiMode ? (AiProfessor.HasKey ? "AI 모드: 직접 대답하고 퀴즈 중에 질문할 수 있어요" : "설정에서 AI API 키를 먼저 넣어 주세요") : "AI 모드를 껐어요");
        }
        if (!aiMode) return;
        if (AiProfessor.HasKey) { NovelUI.Pill(panel.x + 170, panel.yMax - 58, 36, AiProfessor.Connection + " 연결됨", NovelUI.Mint, NovelUI.Berry, 17); return; }
        float note = NovelUI.Pill(panel.x + 170, panel.yMax - 58, 36, "API 키가 없어요", NovelUI.Blush, NovelUI.DeepPink, 17);
        if (NovelUI.Button(new Rect(panel.x + 180 + note, panel.yMax - 62, 150, 44), "설정에서 넣기", true, 17)) OpenMenu(Menu.Settings);
    }

    // Settings row: the saved key's service with a clear button, or a password field to paste one.
    // A note under it says the key is optional (the relay server works without one) but answers faster.
    void AiKeyRow(float x, float y, float width)
    {
        if (!AiProfessor.Supported)
        {
            NovelUI.Label(new Rect(x, y, width, 46), "PC 버전에서만 쓸 수 있어요", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
            return;
        }
        string note = AiProfessor.HasOwnKey ? "내 키로 바로 연결해서 응답이 더 빨라요. 지우면 AI 서버로 작동해요."
            : "키가 없어도 AI 모드는 작동해요. 내 Gemini 키를 넣으면 응답이 더 빨라져요.";
        // Starts under the row label so the sentence fits on one line.
        NovelUI.Label(new Rect(x - 152, y + 54, width + 152, 28), note, NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
        if (AiProfessor.HasOwnKey)
        {
            float pill = NovelUI.Pill(x, y + 5, 36, AiProfessor.Connection + " 저장됨", NovelUI.Mint, NovelUI.Berry, 17);
            if (NovelUI.Button(new Rect(x + pill + 12, y, 120, 46), "키 지우기", false, 18)) { novelAudio.Play("Back"); AiProfessor.ForgetKey(); aiKeyDraft = ""; ShowToast("저장한 키를 지웠어요"); }
            return;
        }
        Rect field = new Rect(x, y, width - 110, 46);
        NovelUI.Card(field, Color.white, 23);
        GUIStyle style = new GUIStyle(NovelUI.Input) { fontSize = 18, padding = new RectOffset(20, 20, 0, 0), border = new RectOffset() };
        style.normal.background = style.focused.background = style.hover.background = null;
        GUI.SetNextControlName("AiKey");
        aiKeyDraft = GUI.PasswordField(field, aiKeyDraft, '•', 200, style);
        NovelUI.Ring(field, GUI.GetNameOfFocusedControl() == "AiKey" ? NovelUI.DeepPink : NovelUI.Pink, 23, 3);
        // With the relay server no key is needed; a key pasted here makes this PC call the API directly instead.
        string placeholder = AiProfessor.UsesServer ? "AI 서버 사용 중 · 키는 선택 사항" : "Gemini 또는 FactChat 키 (Ctrl+V)";
        if (aiKeyDraft == "") NovelUI.Label(new Rect(field.x + 22, field.y, field.width - 30, field.height), placeholder, NovelUI.Soft, 16, NovelUI.Alpha(NovelUI.Plum, .6f), TextAnchor.MiddleLeft);
        GUI.enabled = aiKeyDraft.Trim() != "";
        if (NovelUI.Button(new Rect(field.xMax + 10, y, 100, 46), "저장", true, 19))
        {
            AiProfessor.SaveKey(aiKeyDraft);
            aiKeyDraft = "";
            novelAudio.Play("Save");
            ShowToast("API 키를 이 PC에 저장했어요");
        }
        GUI.enabled = true;
    }
}
