using UnityEngine;

// 믹싱 콘솔: 보컬·기타·베이스·드럼 네 채널 페이더를 브리프대로 맞춘다.
// 세 라운드, 라운드마다 조건 세 개와 제한 시간. 지켜진 조건 수로 점수를 매긴다.
public class MixingBoard : MiniGame
{
    static readonly string[] Channels = { "보컬", "기타", "베이스", "드럼" };
    class Rule { public string Text; public System.Func<int[], bool> Ok; }
    class Brief { public string Note; public Rule[] Rules; }
    static bool Loudest(int[] v, int c) { for (int i = 0; i < 4; i++) if (i != c && v[i] >= v[c]) return false; return true; }
    static readonly Brief[] Briefs = {
        new Brief { Note = "1곡 · 잔잔한 발라드", Rules = new[] {
            new Rule { Text = "보컬이 가장 크게", Ok = v => Loudest(v, 0) },
            new Rule { Text = "기타는 베이스보다 크게", Ok = v => v[1] > v[2] },
            new Rule { Text = "드럼은 4 이하로 작게", Ok = v => v[3] <= 4 } } },
        new Brief { Note = "2곡 · 신나는 록 간주", Rules = new[] {
            new Rule { Text = "드럼이 가장 크게", Ok = v => Loudest(v, 3) },
            new Rule { Text = "기타는 보컬보다 크게", Ok = v => v[1] > v[0] },
            new Rule { Text = "베이스는 3 이상", Ok = v => v[2] >= 3 } } },
        new Brief { Note = "3곡 · 거리 버스킹 후렴", Rules = new[] {
            new Rule { Text = "보컬이 가장 크게", Ok = v => Loudest(v, 0) },
            new Rule { Text = "드럼은 베이스보다 조금(1~2칸) 작게", Ok = v => v[2] - v[3] >= 1 && v[2] - v[3] <= 2 },
            new Rule { Text = "기타와 베이스는 같은 크기", Ok = v => v[1] == v[2] } } }
    };
    const float RoundTime = 25f;
    const int Steps = 10;
    const float TrackY = 270, TrackH = 360;
    enum State { Instructions, Mixing, Feedback, Completed }
    State state = State.Instructions;
    readonly int[] level = new int[4];
    readonly bool[] met = new bool[3];
    int round, score, dragging = -1;
    float stateAt;

    public MixingBoard(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "브리프를 읽지 않고 페이더를 올렸습니다. 조건을 하나씩 확인하며 움직이세요.", "절반은 지켰습니다. 악기끼리의 크기 관계를 다시 비교하세요.", "정확합니다. 소리를 덜어 낼 줄 아는 사람이 좋은 믹싱을 합니다." },
        new[] { "앗, 소리가 다 같이 싸웠어요! 괜찮아요, 하나씩 들어 보면 돼요.", "반은 딱 맞았어요! 귀가 밸런스를 찾아가고 있죠!", "완벽한 밸런스! 오늘 엔지니어 자리 맡겨도 되겠어요!" }
    };

    float TimeLeft => Mathf.Max(0, RoundTime - (Now - stateAt));

    void StartRound()
    {
        for (int i = 0; i < 4; i++) level[i] = 5;
        dragging = -1;
        state = State.Mixing; stateAt = Now;
    }

    void Submit()
    {
        int got = 0;
        for (int k = 0; k < 3; k++) { met[k] = Briefs[round].Rules[k].Ok(level); if (met[k]) got++; }
        score += got;
        dragging = -1;
        state = State.Feedback; stateAt = Now;
        Judge(got == 3, new Vector2(950, 300), got == 3 ? "완벽한 믹스!" : "조건 " + got + "/3");
    }

    Rect Track(int c) => new Rect(205 + c * 140, TrackY, 50, TrackH);
    int LevelAt(float y) => Mathf.Clamp(Mathf.RoundToInt((TrackY + TrackH - y) / TrackH * Steps), 0, Steps);

    void HandleFaders()
    {
        Event e = Event.current;
        // A MouseUp lost to a menu or outside the window must not leave a fader stuck to the mouse.
        if (NovelUI.Repaint && !Input.GetMouseButton(0)) dragging = -1;
        if (e.type == EventType.MouseDown && e.button == 0) dragging = -1;
        if (e.type == EventType.MouseDown && e.button == 0)
            for (int c = 0; c < 4; c++)
            {
                Rect hit = Track(c); hit.xMin -= 25; hit.xMax += 25; hit.yMin -= 15; hit.yMax += 15;
                if (hit.Contains(e.mousePosition)) { dragging = c; level[c] = LevelAt(e.mousePosition.y); NovelUI.Sound?.Invoke("Toggle|Select"); e.Use(); break; }
            }
        else if (e.type == EventType.MouseDrag && dragging >= 0) { level[dragging] = LevelAt(e.mousePosition.y); e.Use(); }
        else if (e.type == EventType.MouseUp) dragging = -1;
    }

    void Next()
    {
        if (++round >= Briefs.Length)
        {
            state = State.Completed; Done = true;
            Delta = score >= 8 ? 2 : score >= 5 ? 1 : 0;
            Summary = "브리프 조건 9개 중 " + score + "개 달성";
            if (Delta == 0)
            {
                ExplainFailure(
                    "총 9개 브리프 조건 중 달성한 조건이 " + score + "개로 기준(5개 이상)에 미치지 못했어요.\n각 악기 페이더의 상대적 크기 조건과 최대/최소 제한을 동시에 만족해야 해요.",
                    "믹싱 콘솔 조절 요령:\n1. '가장 크게': 해당 채널을 8~10 정도로 확실히 높이고 다른 채널을 그보다 낮게 둡니다.\n2. 'A는 B보다 크게': A 페이더 위치가 B보다 무조건 위에 있어야 합니다.\n3. '기타와 베이스는 같은 크기': 두 채널 페이더의 숫자 단계를 똑같이 맞춥니다.\n4. '드럼은 4 이하': 슬라이더를 0~4 구간 안에 위치시킵니다."
                );
            }
        }
        else StartRound();
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Mixing && TimeLeft <= 0) Submit();
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Pink, 2);
        string head = "MIXING CONSOLE  ·  " + (Done ? "완료" : (Mathf.Min(round, 2) + 1) + "/3곡") + "  ·  조건 " + score;
        if (state == State.Mixing) head += "  ·  남은 시간 " + Mathf.CeilToInt(Real(TimeLeft)) + "초";
        NovelUI.Label(new Rect(160, 168, 950, 35), head, NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 260), "오른쪽 브리프대로 네 채널의 볼륨을 맞추세요.\n페이더(세로 막대)를 누르거나 끌어서 0~10으로 조절해요.\n다 맞췄으면 \"믹스 확정\"을 누르세요. 시간이 다 되면 그대로 제출돼요.\n세 곡, 곡마다 조건 세 개를 몇 개 지켰는지로 점수가 나와요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) StartRound();
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 330, 940, 100), Summary, NovelUI.Round, 40, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        if (state == State.Mixing) HandleFaders();
        Color[] colors = { NovelUI.Pink, NovelUI.Lemon, NovelUI.Sky, NovelUI.Mint };
        for (int c = 0; c < 4; c++)
        {
            Rect t = Track(c);
            NovelUI.Label(new Rect(t.x - 45, TrackY - 50, 140, 30), Channels[c], NovelUI.Round, 21, NovelUI.Berry, TextAnchor.MiddleCenter);
            // Fader track with ticks, a level meter beside it and the knob.
            Box(new Rect(t.x + 20, t.y, 10, t.height), NovelUI.Alpha(NovelUI.Plum, .25f));
            for (int s = 0; s <= Steps; s++) Box(new Rect(t.x + 8, t.y + t.height - s * TrackH / Steps - 1, 34, 2), NovelUI.Alpha(NovelUI.Plum, s % 5 == 0 ? .5f : .2f));
            float bounce = level[c] == 0 ? 0 : .8f + .2f * Mathf.Abs(Mathf.Sin(Now * (5 + c * 1.7f) + c));
            float meter = level[c] / (float)Steps * bounce * TrackH;
            Box(new Rect(t.x + 58, t.y, 14, t.height), NovelUI.Alpha(NovelUI.Plum, .12f));
            Box(new Rect(t.x + 58, t.y + t.height - meter, 14, meter), level[c] >= 9 ? NovelUI.DeepPink : colors[c]);
            float ky = t.y + t.height - level[c] * TrackH / Steps;
            NovelUI.Fill(new Rect(t.x - 5, ky - 14, 60, 28), dragging == c ? NovelUI.DeepPink : NovelUI.Berry, 8);
            NovelUI.Label(new Rect(t.x - 5, ky - 14, 60, 28), level[c].ToString(), NovelUI.Round, 18, Color.white, TextAnchor.MiddleCenter);
        }

        Brief b = Briefs[round];
        NovelUI.Card(new Rect(780, 225, 340, 330), Color.white, 16, NovelUI.Alpha(NovelUI.Plum, .2f), 2);
        NovelUI.Label(new Rect(800, 238, 300, 34), "브리프 · " + b.Note, NovelUI.Round, 20, NovelUI.Plum);
        for (int k = 0; k < 3; k++)
        {
            string mark = state == State.Feedback ? (met[k] ? "<color=#3aa57a>○</color> " : "<color=#e0457b>×</color> ") : "• ";
            NovelUI.Label(new Rect(800, 290 + k * 82, 300, 76), mark + b.Rules[k].Text, NovelUI.Soft, 21, NovelUI.Berry);
        }
        if (state == State.Mixing) { if (NovelUI.Button(new Rect(800, 600, 300, 60), "믹스 확정", true, 22)) Submit(); }
        else
        {
            NovelUI.Label(new Rect(780, 565, 340, 65), met[0] && met[1] && met[2] ? "볼륨 조건을 모두 맞췄어요!" : "표시된 조건과 수치를 다시 확인해요.", NovelUI.Round, 22, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(800, 655, 300, 65), round == Briefs.Length - 1 ? "결과 보기" : "다음 곡", button)) Next();
        }
    }
}
