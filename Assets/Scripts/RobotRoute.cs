using UnityEngine;

// 배달 로봇 길 찾기: 앞으로·왼쪽·오른쪽 명령을 줄 세운 뒤 실행해 배달이를 목적지까지 보낸다.
// 지도 세 장, 지도마다 두 번까지 시도할 수 있다. 도착한 지도 수를 센다.
public class RobotRoute : MiniGame
{
    class Map { public Vector2Int Start, Goal; public Vector2Int[] Walls; public string Solution; }
    static readonly Map[] Maps = {
        new Map { Start = new Vector2Int(0, 2), Goal = new Vector2Int(6, 2), Walls = new[] { new Vector2Int(3, 2), new Vector2Int(5, 3) }, Solution = "앞으로 2번 → 왼쪽 → 앞으로 1번 → 오른쪽\n→ 앞으로 4번 → 오른쪽 → 앞으로 1번" },
        new Map { Start = new Vector2Int(0, 4), Goal = new Vector2Int(6, 0), Walls = new[] { new Vector2Int(2, 4), new Vector2Int(2, 3), new Vector2Int(4, 1), new Vector2Int(4, 0) }, Solution = "왼쪽 → 앞으로 2번 → 오른쪽 → 앞으로 6번\n→ 왼쪽 → 앞으로 2번" },
        new Map { Start = new Vector2Int(0, 0), Goal = new Vector2Int(0, 4), Walls = new[] { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(4, 2), new Vector2Int(5, 1) }, Solution = "앞으로 3번 → 오른쪽 → 앞으로 4번 → 오른쪽 → 앞으로 3번" }
    };
    static readonly string[] Names = { "▲ 앞으로", "↶ 왼쪽", "↷ 오른쪽" };
    static readonly int[] Dx = { 1, 0, -1, 0 }, Dy = { 0, 1, 0, -1 };   // Facing 0 right, 1 down, 2 left, 3 up.
    const int Cols = 7, Rows = 5, MaxCommands = 14, Cell = 70;
    enum State { Instructions, Editing, Running, Feedback, Completed }
    State state = State.Instructions;
    readonly System.Collections.Generic.List<int> program = new System.Collections.Generic.List<int>();
    int map, tries, cleared, step, facing;
    Vector2Int pos;
    bool arrived;
    float stateAt;
    string message = "";

    public RobotRoute(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "배달이가 한 곳도 도착하지 못했습니다. 실행 전에 명령을 손가락으로 따라가 보세요.", "일부는 도착했습니다. 회전 방향은 배달이 기준이라는 점을 기억하세요.", "정확합니다. 실행 전에 경로를 머릿속으로 검증했군요. 이대로 운행하겠습니다." },
        new[] { "배달이가 벽이랑 친해졌네요! 왼쪽, 오른쪽은 배달이 눈으로 봐야 해요.", "몇 번은 도착! 배달이가 칭찬해 달래요.", "길 안내 만점! 배달이가 집 주소 외웠대요!" }
    };

    Map Current => Maps[map];
    void Reset() { pos = Current.Start; facing = 0; step = 0; arrived = false; }
    void Begin() { program.Clear(); tries = 0; Reset(); state = State.Editing; }
    bool Blocked(Vector2Int p)
    {
        if (p.x < 0 || p.y < 0 || p.x >= Cols || p.y >= Rows) return true;
        foreach (var w in Current.Walls) if (w == p) return true;
        return false;
    }
    void End(bool ok, string text)
    {
        arrived = ok; message = text; tries++;
        if (ok) cleared++;
        state = State.Feedback; stateAt = Now;
        Judge(ok, new Vector2(202 + pos.x * Cell, 200 + pos.y * Cell), ok ? "배달 완료!" : "쿵!");
        if (!ok) ExplainFailure(text + "\n" + step + "번째 명령까지 실행했고, 로봇은 " + new[] { "오른쪽", "아래쪽", "왼쪽", "위쪽" }[facing] + "을 보고 있어요.\n현재 위치: 왼쪽에서 " + (pos.x + 1) + "칸, 위에서 " + (pos.y + 1) + "칸 / 목적지: " + (Current.Goal.x + 1) + "칸, " + (Current.Goal.y + 1) + "칸",
            "회전은 이동이 아니라 로봇 기준 제자리 방향 전환이에요.\n시작 위치에서 가능한 경로 예시:\n" + Current.Solution + "\n실패한 명령 앞뒤의 방향과 벽 위치를 확인해 고쳐 보세요.");
    }
    void Tick()
    {
        if (step >= program.Count) { End(pos == Current.Goal, pos == Current.Goal ? "도착! 배달 완료예요." : "명령이 끝났는데 목적지가 아니에요."); return; }
        int c = program[step++];
        if (c == 1) facing = (facing + 3) % 4;
        else if (c == 2) facing = (facing + 1) % 4;
        else
        {
            var next = new Vector2Int(pos.x + Dx[facing], pos.y + Dy[facing]);
            if (Blocked(next)) { End(false, "쿵! 배달이가 부딪혔어요."); return; }
            pos = next;
            if (pos == Current.Goal) { End(true, "도착! 배달 완료예요."); return; }
        }
        NovelUI.Sound?.Invoke("LoopTick");
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Running && Now - stateAt >= .4f) { stateAt = Now; Tick(); }
        if (state == State.Feedback && Now - stateAt >= 2f)
        {
            if (!arrived && tries < 2) { Reset(); state = State.Editing; }
            else if (++map >= Maps.Length) { state = State.Completed; Done = true; Delta = cleared == 3 ? 2 : cleared > 0 ? 1 : 0; Summary = "지도 세 장 중 " + cleared + "곳 배달 성공"; }
            else Begin();
        }
        NovelUI.Card(new Rect(130, 150, 1020, 600), new Color(.08f, .12f, .14f), 18, NovelUI.Sky, 2);
        GUI.Label(new Rect(155, 170, 950, 35), "ROUTE  ·  " + (Done ? "완료" : "지도 " + (Mathf.Min(map, 2) + 1) + "/3 · 시도 " + Mathf.Min(tries + 1, 2) + "/2") + "  ·  도착 " + cleared, body);
        if (state == State.Instructions)
        {
            GUI.Label(new Rect(175, 240, 930, 220), "명령을 최대 14개까지 줄 세운 뒤 실행하면 배달이가 그대로 움직여요.\n왼쪽·오른쪽은 제자리에서 배달이 기준으로 도는 거예요.\n벽에 부딪히거나 목적지에 못 가면 실패, 지도마다 두 번 기회가 있어요.", body);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) Begin();
            return;
        }
        if (state == State.Completed) { GUI.Label(new Rect(170, 330, 940, 100), Summary, title); return; }

        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols; x++)
            {
                var p = new Vector2Int(x, y);
                Rect cell = new Rect(170 + x * Cell, 220 + y * Cell, Cell - 6, Cell - 6);
                bool wall = Blocked(p);
                NovelUI.Fill(cell, wall ? NovelUI.Plum : p == Current.Goal ? NovelUI.Alpha(NovelUI.Mint, .7f) : new Color(1, 1, 1, .07f), 10);
                if (wall) NovelUI.Label(cell, "■", NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
                else if (p == Current.Goal && pos != p) NovelUI.Label(cell, "🏠", NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
            }
        Rect bot = new Rect(170 + pos.x * Cell, 220 + pos.y * Cell, Cell - 6, Cell - 6);
        NovelUI.Card(bot, NovelUI.Lemon, 30);
        NovelUI.Label(bot, new[] { "▶", "▼", "◀", "▲" }[facing], NovelUI.Round, 28, NovelUI.Berry, TextAnchor.MiddleCenter);

        // Program list on the right.
        NovelUI.Label(new Rect(690, 215, 440, 30), "명령 " + program.Count + "/" + MaxCommands, NovelUI.Round, 20, NovelUI.Cream);
        for (int i = 0; i < program.Count; i++)
        {
            Rect chip = new Rect(690 + i % 3 * 145, 250 + i / 3 * 48, 135, 40);
            NovelUI.Fill(chip, state == State.Running && i == step - 1 ? NovelUI.Lemon : NovelUI.Blush, 20);
            NovelUI.Label(chip, (i + 1) + ". " + Names[program[i]], NovelUI.Round, 17, NovelUI.Berry, TextAnchor.MiddleCenter);
        }
        if (state == State.Editing)
        {
            for (int c = 0; c < 3; c++)
                if (NovelUI.Button(new Rect(170 + c * 165, 590, 155, 60), Names[c], false, 20) && program.Count < MaxCommands) program.Add(c);
            if (NovelUI.Button(new Rect(690, 590, 140, 60), "⌫ 지우기", false, 20) && program.Count > 0) program.RemoveAt(program.Count - 1);
            if (NovelUI.Button(new Rect(845, 590, 260, 60), "▶ 실행", true, 22) && program.Count > 0) { Reset(); state = State.Running; stateAt = Now; }
        }
        else if (state == State.Feedback) GUI.Label(new Rect(170, 590, 940, 60), message + (!arrived && tries < 2 ? " 명령을 고쳐서 다시 해 봐요." : ""), Face(body));
    }
}
