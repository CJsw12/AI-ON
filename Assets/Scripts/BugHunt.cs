using UnityEngine;

// 컴퓨터공학과 전용: 짧은 코드 세 개에서 결과를 틀리게 만드는 줄을 하나씩 짚는다. 문제당 20초.
public class BugHunt : MiniGame
{
    class Puzzle { public string Goal, Why; public string[] Lines; public int Bug; }
    static readonly Puzzle[] Puzzles = {
        new Puzzle {
            Goal = "목표 · 0부터 9까지 차례로 출력하기",
            Lines = new[] { "int count = 0;", "for (int i = 0; i < 10; i--)", "{", "    Print(i);", "}" }, Bug = 1,
            Why = "i--는 숫자를 줄여요. i++로 늘려야 10에서 반복이 끝납니다."
        },
        new Puzzle {
            Goal = "목표 · 강의실 304, 305, 306호를 모두 출력하기",
            Lines = new[] { "string[] rooms = { \"304\", \"305\", \"306\" };", "for (int i = 0; i < rooms.Length; i++)", "{", "    Print(rooms[i + 1]);", "}" }, Bug = 3,
            Why = "i + 1이면 304호를 건너뛰고 없는 rooms[3]까지 읽어요. rooms[i]가 맞습니다."
        },
        new Puzzle {
            Goal = "목표 · 1 + 2 + 3을 더해서 6 출력하기",
            Lines = new[] { "int total = 0;", "for (int i = 1; i <= 3; i++)", "{", "    total = i;", "}", "Print(total);" }, Bug = 3,
            Why = "total = i는 값을 덮어써서 3이 나와요. total += i로 더해야 합니다."
        }
    };
    const float Limit = 20f;
    enum State { Instructions, Playing, Feedback, Completed }
    State state = State.Instructions;
    int round, picked, hits;
    float stateAt;

    public BugHunt(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "버그를 놓쳤습니다. 결과를 바꾸는 줄은 대개 조건, 증감, 인덱스입니다. 그 세 곳부터 읽으세요.", "일부는 찾았습니다. 틀린 문제는 목표와 실제 출력을 비교해 다시 보세요.", "정확합니다. 목표와 코드를 한 줄씩 대조했군요. 디버깅은 그렇게 합니다." },
        new[] { "버그가 숨바꼭질을 잘하네요! 조건, 증감, 인덱스, 이 셋만 기억해요.", "몇 마리는 잡았어요! 놓친 녀석도 다음엔 바로 보일 거예요.", "버그 사냥꾼 탄생! 세 마리 다 잡았어요!" }
    };

    Puzzle Current => Puzzles[round];
    void Begin() { state = State.Playing; stateAt = Now; picked = -1; }
    void Pick(int line)
    {
        picked = line;
        if (line == Current.Bug) hits++;
        state = State.Feedback; stateAt = Now;
        Judge(line == Current.Bug, new Vector2(640, 270 + Mathf.Max(line, 0) * 52), line == Current.Bug ? "버그 잡았다!" : line < 0 ? "시간 초과" : "여기가 아니에요");
    }
    void Finish()
    {
        state = State.Completed; Done = true; Delta = hits == 3 ? 2 : hits > 0 ? 1 : 0;
        Summary = "버그 3마리 중 " + hits + "마리 잡음";
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        float now = Now;
        if (state == State.Playing && now - stateAt >= Limit) Pick(-1);
        if (state == State.Feedback && now - stateAt >= 3f)
        {
            round++;
            if (round >= Puzzles.Length) Finish(); else Begin();
        }
        NovelUI.Card(new Rect(130, 150, 1020, 600), new Color(.08f, .1f, .15f), 18, NovelUI.Pink, 2);
        GUI.Label(new Rect(155, 170, 950, 35), "BUG HUNT  ·  " + (Done ? "완료" : (Mathf.Min(round, 2) + 1) + "/3") + "  ·  잡은 버그 " + hits, body);
        if (state == State.Instructions)
        {
            GUI.Label(new Rect(175, 260, 930, 160), "코드마다 결과를 틀리게 만드는 줄이 딱 하나 숨어 있어요.\n목표를 읽고, 범인이라고 생각하는 줄을 누르세요.\n문제마다 20초 안에 골라야 합니다.", body);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) Begin();
            return;
        }
        if (state == State.Completed) { GUI.Label(new Rect(170, 330, 940, 100), Summary, title); return; }

        NovelUI.Label(new Rect(170, 215, 940, 40), Current.Goal, NovelUI.Round, 24, NovelUI.Lemon);
        for (int i = 0; i < Current.Lines.Length; i++)
        {
            Rect row = new Rect(170, 270 + i * 52, 940, 46);
            Color fill = new Color(1, 1, 1, .04f);
            if (state == State.Feedback && i == Current.Bug) fill = NovelUI.Alpha(NovelUI.Mint, .45f);
            else if (state == State.Feedback && i == picked) fill = NovelUI.Alpha(NovelUI.DeepPink, .5f);
            else if (state == State.Playing && NovelUI.Hover(row)) fill = NovelUI.Alpha(NovelUI.Pink, .25f);
            NovelUI.Fill(row, fill, 10);
            NovelUI.Label(new Rect(row.x + 14, row.y, 50, row.height), (i + 1).ToString(), NovelUI.Code, 20, NovelUI.Plum, TextAnchor.MiddleLeft);
            NovelUI.Label(new Rect(row.x + 64, row.y, row.width - 70, row.height), Current.Lines[i], NovelUI.Code, 24, NovelUI.Cream, TextAnchor.MiddleLeft);
            if (state == State.Playing && GUI.Button(row, GUIContent.none, GUIStyle.none)) { Pick(i); break; }
        }
        if (state == State.Playing)
        {
            float left = Mathf.Clamp01(1 - (now - stateAt) / Limit);
            Box(new Rect(170, 700, 940, 10), new Color(1, 1, 1, .12f));
            Box(new Rect(170, 700, 940 * left, 10), left < .3f ? NovelUI.DeepPink : NovelUI.Mint);
        }
        else
        {
            string head = picked == Current.Bug ? "잡았다! " : picked < 0 ? "시간 초과 · " : "아쉬워요 · ";
            GUI.Label(new Rect(170, 600, 940, 90), head + Current.Why, Face(body));
        }
    }
}
