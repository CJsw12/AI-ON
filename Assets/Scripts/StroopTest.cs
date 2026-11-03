using UnityEngine;

// 스트룹 과제: 색 이름 글자가 다른 색 잉크로 칠해져 나온다. 글자의 뜻이 아니라 잉크 색을 고른다.
// 20시행(일치 6 ·불일치 14), 시행마다 2.5초. 틀린 반응도 기록되어 아무 키나 연타하면 손해다.
public class StroopTest : MiniGame
{
    static readonly string[] Names = { "빨강", "파랑", "초록", "노랑" };
    static readonly Color[] Inks = { new Color(.94f, .31f, .43f), new Color(.29f, .56f, .94f), new Color(.25f, .75f, .5f), new Color(.95f, .76f, .19f) };
    // { word, ink } — fixed order so every run is comparable; equal pairs are congruent trials.
    static readonly int[,] Trials = {
        { 0, 1 }, { 2, 2 }, { 3, 0 }, { 1, 3 }, { 0, 2 }, { 1, 1 }, { 2, 3 }, { 3, 1 }, { 0, 0 }, { 2, 0 },
        { 1, 2 }, { 3, 3 }, { 0, 3 }, { 2, 1 }, { 1, 0 }, { 3, 2 }, { 1, 1 }, { 0, 2 }, { 2, 2 }, { 3, 0 }
    };
    const float Limit = 2.5f, Gap = .45f;
    enum State { Instructions, Trial, Feedback, Completed }
    State state = State.Instructions;
    int trial, correct, wrong, congruentHits, incongruentHits;
    float stateAt, congruentRt, incongruentRt;
    bool lastOk;
    readonly bool[] held = new bool[4];

    public StroopTest(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "글자의 뜻에 계속 끌려갔습니다. 자동으로 읽히는 정보를 억제하는 연습이 필요합니다.", "정확도는 괜찮습니다. 불일치 시행에서 느려진 만큼이 스트룹 효과입니다. 기록해 두세요.", "빠르고 정확합니다. 방해 자극을 잘 억제했습니다. 좋은 참여자입니다." },
        new[] { "하하, 글자한테 제대로 속았네요! 그 헷갈림이 바로 이 실험의 주인공이에요.", "오~ 꽤 잘했어요! 글자랑 색이 다를 때만 살짝 멈칫했죠?", "와, 완벽해요! 뇌가 글자를 읽는 걸 꾹 참아 냈네요!" }
    };

    int Word => Trials[trial, 0];
    int Ink => Trials[trial, 1];
    bool Congruent => Word == Ink;

    void Begin() { state = State.Trial; stateAt = Now; }
    void Answer(int pick)
    {
        float rt = (Now - stateAt) / (Easy ? .75f : 1);   // Real seconds: beginner mode runs Now at 0.75x.
        lastOk = pick == Ink;
        if (lastOk)
        {
            correct++;
            if (Congruent) { congruentHits++; congruentRt += rt; } else { incongruentHits++; incongruentRt += rt; }
        }
        else wrong++;
        Judge(lastOk, new Vector2(640, 300), pick < 0 ? "시간 초과" : lastOk ? "정반응!" : "오반응");
        state = State.Feedback; stateAt = Now;
    }
    void Finish()
    {
        state = State.Completed; Done = true;
        int hits = congruentHits + incongruentHits;
        float mean = hits > 0 ? (congruentRt + incongruentRt) / hits : 0;
        Delta = correct >= 17 && mean < (Easy ? 1.6f : 1.2f) ? 2 : correct >= 12 ? 1 : 0;
        Summary = "정확도 " + correct + "/20 · 평균 반응 시간 " + (hits > 0 ? mean.ToString("0.00") + "초" : "-");
        if (Delta == 0)
        {
            ExplainFailure(
                "정답 수 " + correct + "/20개 (기준: 12개 이상) · 오반응 " + wrong + "개\n글자의 뜻이 잉크 색과 충돌할 때 무의식적인 단어 읽기 자동화로 인해 오답이 발생했어요.",
                "스트룹 효과 극복 팁:\n1. 글자 전체를 하나의 단어로 읽으려 하지 말고, 글자의 외곽선이나 모서리의 잉크 색깔에만 시선을 두세요.\n2. 키보드(1:빨강, 2:파랑, 3:초록, 4:노랑) 위치를 손가락에 미리 올려두면 반응 속도가 빨라집니다."
            );
        }
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Trial && Now - stateAt >= Limit) Answer(-1);
        if (state == State.Feedback && Now - stateAt >= Gap)
        {
            if (++trial >= Trials.GetLength(0)) Finish(); else Begin();
        }
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), "STROOP TASK  ·  " + (Done ? "완료" : "시행 " + (Mathf.Min(trial, 19) + 1) + "/20") + "  ·  정반응 " + correct + "  ·  오반응 " + wrong, NovelUI.Round, 21, NovelUI.Berry);

        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 260), "화면에 색 이름이 나타나요. 그런데 글자와 잉크 색이 다를 수 있어요!\n글자를 읽지 말고, 글자가 칠해진 <b>잉크 색</b>을 고르세요.\n키보드 1 빨강 · 2 파랑 · 3 초록 · 4 노랑 (버튼도 돼요)\n20시행, 시행마다 2.5초. 틀린 반응도 기록돼요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) Begin();
            return;
        }
        if (state == State.Completed)
        {
            NovelUI.Label(new Rect(170, 280, 940, 80), Summary, NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
            string fact = congruentHits > 0 && incongruentHits > 0
                ? "일치 시행 " + (congruentRt / congruentHits).ToString("0.00") + "초 · 불일치 시행 " + (incongruentRt / incongruentHits).ToString("0.00") + "초\n차이 " + ((incongruentRt / incongruentHits - congruentRt / congruentHits) * 1000).ToString("0") + "ms, 이것이 나의 스트룹 효과예요!"
                : "일치·불일치 시행을 비교할 기록이 부족해요.";
            NovelUI.Label(new Rect(170, 390, 940, 120), fact + "\n글자 읽기는 너무 자동이라 멈추기가 어려워요.", NovelUI.Soft, 22, NovelUI.Plum, TextAnchor.MiddleCenter);
            return;
        }

        // Stimulus on a dark card so all four inks (yellow too) stay readable.
        NovelUI.Card(new Rect(390, 225, 500, 230), NovelUI.Berry, 24);
        if (state == State.Trial)
            NovelUI.Label(new Rect(390, 225, 500, 230), Names[Word], NovelUI.Round, 96, Inks[Ink], TextAnchor.MiddleCenter);
        else
            NovelUI.Label(new Rect(390, 225, 500, 230), lastOk ? "+" : "×", NovelUI.Round, 72, lastOk ? NovelUI.Mint : NovelUI.Pink, TextAnchor.MiddleCenter);

        if (state != State.Trial) return;
        float left = Mathf.Clamp01(1 - (Now - stateAt) / Limit);
        Box(new Rect(390, 470, 500, 8), NovelUI.Alpha(NovelUI.Plum, .2f));
        Box(new Rect(390, 470, 500 * left, 8), left < .3f ? NovelUI.DeepPink : NovelUI.Mint);

        // Keyboard: one answer per physical press; held[] ignores OS key repeat.
        Event e = Event.current;
        if (NovelUI.Repaint)
            for (int k = 0; k < 4; k++)
                if (!Input.GetKey(KeyCode.Alpha1 + k) && !Input.GetKey(KeyCode.Keypad1 + k)) held[k] = false;
        if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
        {
            int k = e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha4 ? e.keyCode - KeyCode.Alpha1
                : e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad4 ? e.keyCode - KeyCode.Keypad1 : -1;
            if (k >= 0)
            {
                if (e.type == EventType.KeyUp) held[k] = false;
                else if (!held[k]) { held[k] = true; e.Use(); Answer(k); return; }
                e.Use();
            }
        }
        for (int i = 0; i < 4; i++)
        {
            Rect r = new Rect(190 + i * 230, 520, 210, 80);
            Box(new Rect(r.x + 15, r.y + 92, r.width - 30, 10), Inks[i]);
            if (NovelUI.Button(r, (i + 1) + "  " + Names[i], false, 24)) { Answer(i); return; }
        }
    }
}
