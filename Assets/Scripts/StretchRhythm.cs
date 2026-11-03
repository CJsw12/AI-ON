using UnityEngine;

// 체육학과 전용: 메트로놈 박에 맞춰 흘러오는 스트레칭 동작을 판정선에서 따라 한다.
// 세 줄(위로·옆으로·앞으로)을 버튼이나 방향키로 누른다. 뒤 절반은 박이 빨라진다.
public class StretchRhythm : MiniGame
{
    static readonly string[] Moves = { "위로 뻗기", "옆으로 펴기", "앞으로 숙이기" };
    static readonly string[] Keys = { "↑", "←", "↓" };
    static readonly int[] Pattern = { 0, 0, 1, 1, 2, 2, 0, 1, 0, 2, 1, 0, 2, 1, 1, 0 };
    const float Lead = 3f, HitX = 290, Speed = 380, Perfect = .12f, Good = .28f;
    enum State { Instructions, Playing, Completed }
    State state = State.Instructions;
    float startedAt, flashAt = -10;
    string flash = "";
    readonly float[] times = new float[Pattern.Length];
    readonly int[] judged = new int[Pattern.Length];   // 0 waiting, 1 miss, 2 good, 3 perfect.
    int lastBeat = -1, misses;   // misses: presses with no move in reach.
    readonly bool[] held = new bool[3];   // Ignores OS key repeat until the key is released.

    public StretchRhythm(MiniGameTheme theme) : base(theme)
    {
        float t = 0;
        for (int i = 0; i < times.Length; i++) { times[i] = t; t += i < 8 ? .75f : .5f; }
    }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "박자를 많이 놓쳤습니다. 동작을 눈으로 쫓지 말고 박을 몸으로 세세요.", "절반은 맞췄습니다. 박이 빨라질 때 몸이 먼저 서두르는지 확인하세요.", "정확합니다. 빨라진 박에서도 동작이 흐트러지지 않았군요. 내일도 일곱 시입니다." },
        new[] { "팔이 박자보다 먼저 퇴근했네요! 괜찮아요, 내일 또 해요.", "반은 딱딱 맞았어요! 몸이 박자를 기억하기 시작했어요.", "와, 빨라져도 하나도 안 틀렸어요! 새벽 동지 자격 충분해요!" }
    };

    float Song => Now - startedAt - Lead;   // Seconds since the first note's beat.
    int Count(int value) { int n = 0; foreach (int j in judged) if (j == value) n++; return n; }

    void Press(int lane)
    {
        float t = Song;
        int target = -1;
        for (int i = 0; i < times.Length; i++)
            if (judged[i] == 0 && Pattern[i] == lane && Mathf.Abs(t - times[i]) <= Good) { target = i; break; }
        if (target < 0) { misses++; flash = "헛동작"; flashAt = Now; return; }
        float error = Mathf.Abs(t - times[target]);
        judged[target] = error <= Perfect ? 3 : 2;
        flash = error <= Perfect ? "퍼펙트!" : "좋아요";
        flashAt = Now;
        NovelUI.Sound?.Invoke("Select");
        if (error <= Perfect) Burst(new Vector2(HitX, 320 + lane * 95), 12);
    }
    void Finish()
    {
        state = State.Completed; Done = true;
        int score = Mathf.Max(0, Count(3) * 2 + Count(2) - misses), total = times.Length * 2;
        Delta = score >= total * .75f ? 2 : score >= total * .45f ? 1 : 0;
        Summary = "퍼펙트 " + Count(3) + " · 좋아요 " + Count(2) + " · 놓침 " + Count(1) + (misses > 0 ? " · 헛동작 " + misses : "");
        if (Count(1) > 0 || misses > 0)
        {
            string reason = "놓친 동작 " + Count(1) + "개 / 헛동작 " + misses + "회예요.\n놓침은 같은 동작을 판정 시간 안에 누르지 못한 경우이고,\n헛동작은 해당 동작의 판정 시간 밖에서 누른 경우예요.";
            for (int i = 0; i < judged.Length; i++) if (judged[i] == 1) { reason += "\n처음 놓친 동작: " + (i + 1) + "번째 ‘" + Moves[Pattern[i]] + "’"; break; }
            ExplainFailure(reason, "카드가 왼쪽 판정선에 닿을 때 같은 동작 버튼이나 방향키를 한 번 눌러요.\n판정선 기준 앞뒤 0.28초까지 인정하고, 0.12초 이내면 퍼펙트예요.\n앞 절반은 0.75초, 뒤 절반은 0.5초 간격이에요. 뒤에서는 박을 조금 빠르게 세어 보세요.\n버튼을 계속 누르기보다 카드 하나에 입력 한 번을 맞춰요.");
        }
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        NovelUI.Card(new Rect(130, 150, 1020, 600), new Color(.1f, .13f, .1f), 18, NovelUI.Lemon, 2);
        GUI.Label(new Rect(155, 170, 950, 35), "METRONOME  ·  " + (Done ? "완료" : state == State.Playing && times.Length > 8 && Song > times[8] - .3f ? "빨라지는 박" : "느린 박"), body);
        if (state == State.Instructions)
        {
            GUI.Label(new Rect(175, 240, 930, 220), "동작 카드가 왼쪽 선에 닿는 순간 같은 동작을 누르세요.\n버튼을 누르거나 방향키 ↑ ← ↓ 를 써도 돼요.\n동작은 16개, 뒤 절반은 박이 빨라집니다.\n동작이 없을 때 누르면 헛동작으로 점수가 깎여요.", body);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) { state = State.Playing; startedAt = Now; }
            return;
        }
        if (state == State.Completed) { GUI.Label(new Rect(170, 330, 940, 100), Summary, title); return; }

        float song = Song;
        for (int i = 0; i < times.Length; i++)
            if (judged[i] == 0 && song > times[i] + Good) { judged[i] = 1; flash = "놓쳤어요"; flashAt = Now; }
        if (song > times[times.Length - 1] + .8f) { Finish(); return; }
        int beat = Mathf.FloorToInt((song + Lead) / .75f);
        if (song < times[8] - .3f && beat != lastBeat && NovelUI.Repaint) { lastBeat = beat; NovelUI.Sound?.Invoke("LoopTick"); }

        // Keyboard input; menus turn these events into Ignore, so nothing fires while settings are open.
        Event e = Event.current;
        // A KeyUp swallowed while a menu was open must not leave a lane stuck as held.
        if (NovelUI.Repaint)
        {
            if (!Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.W)) held[0] = false;
            if (!Input.GetKey(KeyCode.LeftArrow) && !Input.GetKey(KeyCode.A)) held[1] = false;
            if (!Input.GetKey(KeyCode.DownArrow) && !Input.GetKey(KeyCode.S)) held[2] = false;
        }
        if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
        {
            int lane = e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W ? 0 : e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.A ? 1 : e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S ? 2 : -1;
            if (lane >= 0)
            {
                if (e.type == EventType.KeyUp) held[lane] = false;
                else if (!held[lane]) { held[lane] = true; Press(lane); }
                e.Use();
            }
        }

        if (song < 0) GUI.Label(new Rect(170, 210, 940, 60), "준비  " + Mathf.CeilToInt(-song), title);
        for (int lane = 0; lane < 3; lane++)
        {
            Rect row = new Rect(170, 280 + lane * 95, 940, 80);
            NovelUI.Fill(row, new Color(1, 1, 1, .05f), 16);
            NovelUI.Label(new Rect(row.x + 14, row.y, 60, row.height), Keys[lane], NovelUI.Round, 28, NovelUI.Plum, TextAnchor.MiddleLeft);
        }
        Box(new Rect(HitX, 270, 6, 290), NovelUI.Lemon);
        for (int i = 0; i < times.Length; i++)
        {
            if (judged[i] >= 2) continue;
            float x = HitX + (times[i] - song) * Speed;
            if (x > 1060 || x < 150) continue;
            Rect note = new Rect(x - 60, 290 + Pattern[i] * 95, 120, 60);
            NovelUI.Card(note, judged[i] == 1 ? NovelUI.Alpha(NovelUI.Plum, .6f) : NovelUI.Mint, 30);
            NovelUI.Label(note, Moves[Pattern[i]], NovelUI.Round, 18, NovelUI.Berry, TextAnchor.MiddleCenter);
        }
        if (Now - flashAt < .5f) GUI.Label(new Rect(HitX - 120, 225, 240, 40), flash, Face(body));
        for (int lane = 0; lane < 3; lane++)
        {
            Rect pad = new Rect(220 + lane * 290, 600, 260, 80);
            NovelUI.Card(pad, NovelUI.Hover(pad) ? NovelUI.Pink : NovelUI.Blush, 40);
            NovelUI.Label(pad, Keys[lane] + "  " + Moves[lane], NovelUI.Round, 22, NovelUI.Berry, TextAnchor.MiddleCenter);
            // Judge on mouse down, not on release, so the timing matches the moment of the press.
            if (e.type == EventType.MouseDown && pad.Contains(e.mousePosition)) { Press(lane); e.Use(); }
        }
    }
}
