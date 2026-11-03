using UnityEngine;
using System.Collections.Generic;

// 현미경 초점: 조동·미동 나사로 숨은 초점을 찾아 흐릿한 시료를 선명하게 만든 뒤, 보이는 것이 무엇인지 고른다.
// 슬라이드 3장, 장마다 제한 시간 18초. 초점(선명하게 맞춤)과 판별(정답) 각각 1점, 총 6점.
public class MicroscopeFocus : MiniGame
{
    static readonly string[] Names = { "양파 표피 세포", "구강 상피 세포", "짚신벌레" };
    const float SlideTime = 18f, Sharp = .03f;
    enum State { Instructions, Focusing, Feedback, Completed }
    State state = State.Instructions;
    readonly int[] order = { 0, 1, 2 };
    readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    int slide, points, focusedCount, correctCount;
    float coarse, fine, target, stateAt;
    bool lastFocused, lastCorrect;
    string lastText = "";

    public MicroscopeFocus(MiniGameTheme theme) : base(theme)
    {
        for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
    }

    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "초점도, 판별도 부족했습니다. 조동 나사로 대강, 미동 나사로 정확히 맞추세요.", "절반은 정확했습니다. 흐릿한 상태로 판단하지 마세요.", "모두 정확합니다. 선명한 상과 정확한 판별, 관찰의 기본입니다." },
        new[] { "앗, 시료가 안개 속에 숨었어요! 나사 돌리는 감은 금방 와요.", "반은 또렷하게 성공! 조금만 더 돌리면 완벽했겠죠!", "초점 마스터! 시료들이 반갑다고 손 흔들어요!" }
    };

    void NewSlide()
    {
        // Focus = coarse + fine * .1, both 0..1. The target is always reachable on the 0.05 / 0.01 grid.
        target = Random.Range(30, 81) * .01f + Random.Range(0, 10) * .001f;
        coarse = Random.value < .5f ? .05f : .95f;
        fine = .5f;
        state = State.Focusing; stateAt = Now;
    }

    float Blur => Mathf.Abs(coarse + fine * .1f - target);

    void Answer(int pick)
    {
        lastFocused = pick >= 0 && Blur <= Sharp;   // A timed-out slide earns no focus point.
        lastCorrect = pick == order[slide];
        if (lastFocused) { points++; focusedCount++; }
        if (lastCorrect) { points++; correctCount++; }
        lastText = pick < 0 ? "시간 초과 · 정답은 " + Names[order[slide]] :
            (lastCorrect ? "정답! " : "아쉬워요 · 정답은 " + Names[order[slide]] + " ") + (lastFocused ? "· 초점 선명" : "· 초점이 흐릿했어요");
        Judge(lastCorrect && lastFocused, new Vector2(640, 260), lastCorrect ? (lastFocused ? "또렷하게 정답!" : "정답, 하지만 흐릿") : "앗, 다른 시료예요");
        state = State.Feedback; stateAt = Now;
    }

    void Turn(int knob, int dir)
    {
        if (knob == 0) coarse = Mathf.Clamp01(Mathf.Round((coarse + dir * .05f) * 100) / 100);
        else fine = Mathf.Clamp01(Mathf.Round((fine + dir * .1f) * 10) / 10);
        NovelUI.Sound?.Invoke("Select");
    }

    void HandleKeys()
    {
        Event e = Event.current;
        // KeyUps that arrive outside this state (feedback screen, open menu) are missed; drop keys no longer down.
        if (NovelUI.Repaint) held.RemoveWhere(k => !Input.GetKey(k));
        if (e.type == EventType.KeyUp) { held.Remove(e.keyCode); return; }
        if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
        if (!held.Add(e.keyCode)) { e.Use(); return; }   // ignore key repeat
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow: Turn(0, -1); break;
            case KeyCode.RightArrow: Turn(0, 1); break;
            case KeyCode.DownArrow: Turn(1, -1); break;
            case KeyCode.UpArrow: Turn(1, 1); break;
            case KeyCode.Alpha1: case KeyCode.Keypad1: Answer(0); break;
            case KeyCode.Alpha2: case KeyCode.Keypad2: Answer(1); break;
            case KeyCode.Alpha3: case KeyCode.Keypad3: Answer(2); break;
            default: return;
        }
        e.Use();
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Focusing && Now - stateAt >= SlideTime) Answer(-1);
        if (state == State.Feedback && Now - stateAt >= 2.2f)
        {
            if (++slide >= 3)
            {
                state = State.Completed; Done = true;
                Delta = points >= 5 ? 2 : points >= 3 ? 1 : 0;
                Summary = "판별 " + correctCount + "/3 · 선명한 초점 " + focusedCount + "/3";
                if (Delta == 0)
                {
                    ExplainFailure(
                        "획득 점수가 " + points + "/6점으로 기준(3점 이상)에 미치지 못했어요.\n조동 나사로 큰 윤곽을 잡지 못했거나, 시료의 형태적 특징(양파 표피/구강 상피/짚신벌레) 판별에서 실수가 있었어요.",
                        "현미경 조작 및 시료 구별 팁:\n1. 조동 나사(←, →): 크게 움직이므로 화면에 상의 윤곽이 어렴풋이 보일 때까지 빠르게 이동시킵니다.\n2. 미동 나사(↑, ↓): 상의 테두리가 가장 또렷해지는 지점을 미세하게 맞춥니다.\n3. 시료 특징: 양파 표피(규칙적인 직사각형 벽돌 모양 식물 세포), 구강 상피(불규칙한 판 모양 동물 세포와 중심 핵), 짚신벌레(가장자리에 섬모가 있는 유선형 단세포 생물)"
                    );
                }
            }
            else NewSlide();
        }

        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Mint, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), (Theme.Header ?? "MICROSCOPE") + "  ·  " + (Done ? "완료" : "슬라이드 " + (Mathf.Min(slide, 2) + 1) + "/3") + "  ·  점수 " + points + "/6", NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 230, 930, 260), "흐릿한 슬라이드를 조동·미동 나사로 또렷하게 만드세요.\n조동 나사(← →)는 크게, 미동 나사(↑ ↓)는 아주 조금 움직여요.\n상이 선명해지면 무엇이 보이는지 아래에서 고르세요(숫자 1~3).\n슬라이드는 3장, 한 장에 18초. 초점과 정답이 각각 1점이에요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) NewSlide();
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 300, 940, 100), Summary, NovelUI.Round, 36, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        if (state == State.Focusing) HandleKeys();

        // Eyepiece view.
        Rect view = new Rect(180, 220, 400, 400);
        NovelUI.Fill(new Rect(view.x - 10, view.y - 10, view.width + 20, view.height + 20), NovelUI.Plum, 210);
        NovelUI.Fill(view, new Color(1f, .98f, .9f), 200);
        float blur = state == State.Feedback ? 0 : Blur;
        GUI.BeginGroup(new Rect(view.x + 40, view.y + 40, view.width - 80, view.height - 80));
        DrawSpecimen(order[slide], new Vector2(160, 160), blur);
        GUI.EndGroup();
        float clarity = Mathf.Clamp01(1 - blur / .3f);

        // Knobs and clarity meter.
        float x = 640;
        NovelUI.Label(new Rect(x, 225, 480, 30), "선명도", NovelUI.Round, 20, NovelUI.Berry);
        Box(new Rect(x, 258, 460, 20), NovelUI.Alpha(NovelUI.Plum, .15f));
        Box(new Rect(x, 258, 460 * clarity, 20), blur <= Sharp ? NovelUI.Mint : NovelUI.Pink);
        string[] knob = { "조동 나사 (← →)", "미동 나사 (↑ ↓)" };
        float[] val = { coarse, fine };
        for (int k = 0; k < 2; k++)
        {
            float y = 300 + k * 85;
            NovelUI.Label(new Rect(x, y, 300, 30), knob[k], NovelUI.Round, 20, NovelUI.Plum);
            Box(new Rect(x + 110, y + 45, 240, 8), NovelUI.Alpha(NovelUI.Plum, .2f));
            NovelUI.Fill(new Rect(x + 110 + 240 * val[k] - 9, y + 40, 18, 18), NovelUI.DeepPink, 9);
            bool on = state == State.Focusing;
            if (on && NovelUI.Button(new Rect(x, y + 32, 90, 36), "◀", true, 20)) Turn(k, -1);
            if (on && NovelUI.Button(new Rect(x + 370, y + 32, 90, 36), "▶", true, 20)) Turn(k, 1);
        }
        float left = Mathf.Max(0, SlideTime - (Now - stateAt));
        if (state == State.Focusing)
            NovelUI.Label(new Rect(x, 470, 460, 30), "남은 시간 " + Real(left).ToString("0.0") + "초" + (blur <= Sharp ? "  · 선명해요!" : ""), NovelUI.Soft, 20, left < 5 ? NovelUI.DeepPink : NovelUI.Plum);

        // Identification.
        if (state == State.Focusing)
        {
            NovelUI.Label(new Rect(x, 505, 460, 30), "무엇이 보이나요?", NovelUI.Round, 20, NovelUI.Berry);
            for (int i = 0; i < 3; i++)
                if (NovelUI.Button(new Rect(x, 540 + i * 50, 460, 42), (i + 1) + ". " + Names[i], true, 19)) Answer(i);
        }
        else NovelUI.Label(new Rect(x, 520, 480, 160), lastText, NovelUI.Round, 22, NovelUI.Berry);
    }

    // Blur is drawn as several faint offset copies; when sharp, one solid copy.
    void DrawSpecimen(int kind, Vector2 c, float blur)
    {
        float spread = Mathf.Min(blur, .4f) * 90;
        if (spread < 1.5f) { DrawShape(kind, c, 1f); return; }
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3;
            DrawShape(kind, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spread, .22f);
        }
    }

    void DrawShape(int kind, Vector2 c, float alpha)
    {
        Color wall = NovelUI.Alpha(new Color(.85f, .45f, .55f), alpha), nucleus = NovelUI.Alpha(new Color(.55f, .2f, .45f), alpha);
        if (kind == 0)
        {
            // Onion epidermis: brick-like rectangular cells with a nucleus each.
            for (int r = 0; r < 5; r++)
                for (int q = 0; q < 3; q++)
                {
                    float ox = (r % 2) * 45;
                    Rect cell = new Rect(c.x - 160 + q * 100 + ox - 20, c.y - 150 + r * 62, 96, 58);
                    Box(new Rect(cell.x, cell.y, cell.width, 3), wall); Box(new Rect(cell.x, cell.yMax - 3, cell.width, 3), wall);
                    Box(new Rect(cell.x, cell.y, 3, cell.height), wall); Box(new Rect(cell.xMax - 3, cell.y, 3, cell.height), wall);
                    NovelUI.Fill(new Rect(cell.x + 40, cell.y + 22, 14, 14), nucleus, 7);
                }
        }
        else if (kind == 1)
        {
            // Cheek epithelium: loose, rounded flat cells with a central nucleus.
            Vector2[] at = { new Vector2(-70, -60), new Vector2(60, -40), new Vector2(-20, 60), new Vector2(90, 80) };
            float[] size = { 110, 95, 120, 80 };
            for (int i = 0; i < at.Length; i++)
            {
                Vector2 p = c + at[i];
                NovelUI.Fill(new Rect(p.x - size[i] / 2, p.y - size[i] * .4f, size[i], size[i] * .8f), NovelUI.Alpha(new Color(1f, .75f, .82f), alpha * .8f), size[i] * .35f);
                NovelUI.Fill(new Rect(p.x - 9, p.y - 9, 18, 18), nucleus, 9);
            }
        }
        else
        {
            // Paramecium: slipper-shaped oval with cilia around the edge.
            Rect body = new Rect(c.x - 120, c.y - 50, 240, 100);
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28;
                Vector2 p = c + new Vector2(Mathf.Cos(a) * 128, Mathf.Sin(a) * 58);
                Box(new Rect(p.x - 2, p.y - 2, 4, 4), wall);
            }
            NovelUI.Fill(body, NovelUI.Alpha(new Color(.6f, .85f, .75f), alpha * .85f), 50);
            NovelUI.Fill(new Rect(c.x - 25, c.y - 18, 50, 36), nucleus, 18);
            NovelUI.Fill(new Rect(c.x + 50, c.y + 5, 26, 18), NovelUI.Alpha(NovelUI.Sky, alpha), 9);
            NovelUI.Fill(new Rect(c.x - 80, c.y - 15, 20, 20), NovelUI.Alpha(NovelUI.Sky, alpha), 10);
        }
    }
}
