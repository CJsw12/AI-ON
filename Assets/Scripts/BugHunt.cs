using UnityEngine;

// 컴퓨터공학과 전용: 짧은 코드 세 개에서 결과를 틀리게 만드는 줄을 하나씩 짚는다. 문제당 20초.
public class BugHunt : MiniGame
{
    class Puzzle { public string Goal, Why, Guide, Example, Hint, Fix, Trace; public string[] Lines, Meanings; public int Bug; }
    static readonly Puzzle[] Puzzles = {
        new Puzzle {
            Goal = "목표 · 0부터 9까지 차례로 출력하기",
            Lines = new[] { "int count = 0;", "for (int i = 0; i < 10; i--)", "{", "    Print(i);", "}" }, Bug = 1,
            Why = "i--는 숫자를 줄여요. i++로 늘려야 10에서 반복이 끝납니다.",
            Fix = "for (int i = 0; i < 10; i++)", Trace = "수정 전: 0, -1, -2… / 수정 후: 0, 1, 2…9",
            Guide = "반복문은 같은 일을 여러 번 하라는 명령이에요.\ni는 현재 숫자예요. for 안에는 시작값, 계속할 조건, 숫자를 바꾸는 방법이 들어 있어요.",
            Example = "예: 2에서 시작해 5보다 작을 동안 1씩 늘리면 → 2, 3, 4\n++는 1 늘리기 / --는 1 줄이기 / Print는 화면에 보여 주기",
            Hint = "생각해 보기 · 목표에 도착하려면 숫자가 어느 방향으로 움직여야 할까요?",
            Meanings = new[] { "count라는 숫자 보관함에 0을 넣어요.", "0에서 시작 / 10보다 작으면 계속 / 매번 1 줄이기", "여기부터 반복할 일이에요.", "현재 숫자를 화면에 보여 줘요.", "반복할 일이 끝나는 표시예요." }
        },
        new Puzzle {
            Goal = "목표 · 강의실 304, 305, 306호를 모두 출력하기",
            Lines = new[] { "string[] rooms = { \"304\", \"305\", \"306\" };", "for (int i = 0; i < rooms.Length; i++)", "{", "    Print(rooms[i + 1]);", "}" }, Bug = 3,
            Why = "i + 1이면 304호를 건너뛰고 없는 rooms[3]까지 읽어요. rooms[i]가 맞습니다.",
            Fix = "Print(rooms[i]);", Trace = "수정 전: 305, 306 출력 후 범위 오류 / 수정 후: 304, 305, 306",
            Guide = "배열은 여러 값을 순서대로 담은 목록이에요.\n목록의 위치 번호는 1이 아니라 0부터 시작해요. rooms.Length는 목록에 든 항목 수예요.",
            Example = "예: 과일 목록 [사과, 배, 귤] → 위치 0은 사과, 1은 배, 2는 귤\n목록[번호]는 그 위치의 값을 꺼내라는 뜻이에요.",
            Hint = "생각해 보기 · i가 0일 때 첫 번째 강의실을 꺼내고 있나요?",
            Meanings = new[] { "강의실 이름 세 개를 순서대로 보관해요.", "위치 0부터 시작 / 항목 수보다 작으면 계속 / 1씩 늘리기", "여기부터 반복할 일이에요.", "현재 위치보다 한 칸 뒤의 강의실을 보여 줘요.", "반복할 일이 끝나는 표시예요." }
        },
        new Puzzle {
            Goal = "목표 · 1 + 2 + 3을 더해서 6 출력하기",
            Lines = new[] { "int total = 0;", "for (int i = 1; i <= 3; i++)", "{", "    total = i;", "}", "Print(total);" }, Bug = 3,
            Why = "total = i는 값을 덮어써서 3이 나와요. total += i로 더해야 합니다.",
            Fix = "total += i;", Trace = "수정 전 합계: 1 → 2 → 3 / 수정 후 합계: 1 → 3 → 6",
            Guide = "변수는 값을 담는 보관함이에요. total에는 지금까지 더한 합계를 담으려고 해요.\n코드의 =는 수학의 등호와 달리, 오른쪽 값을 왼쪽 보관함에 넣으라는 뜻이에요.",
            Example = "예: 합계가 5일 때 = 2를 하면 2로 바뀌어요.\n+= 2를 하면 원래 값 5에 2를 더해 7이 돼요.",
            Hint = "생각해 보기 · 이전 합계가 남아 있나요, 새 숫자로 바뀌고 있나요?",
            Meanings = new[] { "합계를 담을 total을 0으로 준비해요.", "1부터 시작 / 3까지 반복 / 매번 1 늘리기", "여기부터 반복할 일이에요.", "합계를 현재 숫자로 바꿔요.", "반복할 일이 끝나는 표시예요.", "마지막 합계를 화면에 보여 줘요." }
        }
    };
    const float Limit = 20f;
    enum State { Instructions, Guide, Playing, Feedback, Completed }
    State state = State.Instructions;
    int round, picked, hits;
    float stateAt;
    readonly bool beginner;

    public BugHunt(MiniGameTheme theme, bool experienced = true) : base(theme) { beginner = !experienced; }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "버그를 놓쳤습니다. 결과를 바꾸는 줄은 대개 조건, 증감, 인덱스입니다. 그 세 곳부터 읽으세요.", "일부는 찾았습니다. 틀린 문제는 목표와 실제 출력을 비교해 다시 보세요.", "정확합니다. 목표와 코드를 한 줄씩 대조했군요. 디버깅은 그렇게 합니다." },
        new[] { "버그가 숨바꼭질을 잘하네요! 조건, 증감, 인덱스, 이 셋만 기억해요.", "몇 마리는 잡았어요! 놓친 녀석도 다음엔 바로 보일 거예요.", "버그 사냥꾼 탄생! 세 마리 다 잡았어요!" }
    };

    Puzzle Current => Puzzles[round];
    void Prepare() { if (beginner) state = State.Guide; else Begin(); }
    void Begin() { state = State.Playing; stateAt = Now; picked = -1; }
    void Next()
    {
        round++;
        if (round >= Puzzles.Length) Finish(); else Prepare();
    }
    void Pick(int line)
    {
        picked = line;
        if (line == Current.Bug) hits++;
        state = State.Feedback; stateAt = Now;
        Judge(line == Current.Bug, new Vector2(640, 270 + Mathf.Max(line, 0) * 52), line == Current.Bug ? "버그 잡았다!" : line < 0 ? "시간 초과" : "여기가 아니에요");
        if (line != Current.Bug)
            ExplainFailure(Current.Goal + "\n" + (line < 0 ? "제한 시간 안에 코드 줄을 선택하지 못했어요." : "선택한 " + (line + 1) + "번 줄이 아니라 " + (Current.Bug + 1) + "번 줄이 결과를 바꾸고 있어요.") + "\n" + Current.Why,
                (Current.Bug + 1) + "번 줄을 이렇게 바꿔요: " + Current.Fix + "\n" + Current.Trace);
    }
    void Finish()
    {
        state = State.Completed; Done = true; Delta = hits == 3 ? 2 : hits > 0 ? 1 : 0;
        Summary = "버그 3마리 중 " + hits + "마리 잡음";
    }

    void UpdateState()
    {
        if (state == State.Playing && Now - stateAt >= Limit) Pick(-1);
        if (state == State.Feedback && !beginner && Now - stateAt >= 3f) Next();
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        UpdateState();
        float now = Now;
        NovelUI.Card(new Rect(130, 150, 1020, 600), new Color(.08f, .1f, .15f), 18, NovelUI.Pink, 2);
        GUI.Label(new Rect(155, 170, 950, 35), "BUG HUNT  ·  " + (Done ? "완료" : (Mathf.Min(round, 2) + 1) + "/3") + "  ·  잡은 버그 " + hits, body);
        if (state == State.Instructions)
        {
            GUI.Label(new Rect(175, 260, 930, beginner ? 240 : 160), beginner
                ? "버그는 프로그램이 원하는 결과를 내지 못하게 하는 실수예요.\n이번에는 목표와 다른 결과를 만드는 줄 하나를 찾아요.\n\n① 문제별 설명과 예시를 읽어요. 이때는 시간이 흐르지 않아요.\n② 시작한 뒤 20초 안에 의심되는 코드 줄을 눌러요.\n③ 코드 아래의 쉬운 설명을 참고하고, 결과에서 풀이를 확인해요."
                : "코드마다 결과를 틀리게 만드는 줄이 딱 하나 숨어 있어요.\n목표를 읽고, 범인이라고 생각하는 줄을 누르세요.\n문제마다 20초 안에 골라야 합니다.", body);
            if (GUI.Button(new Rect(440, 560, 400, 75), beginner ? "코드 읽는 법 보기" : "시작하기", button)) Prepare();
            return;
        }
        if (state == State.Completed) { GUI.Label(new Rect(170, 330, 940, 100), Summary, title); return; }

        NovelUI.Label(new Rect(170, 215, 940, 40), Current.Goal, NovelUI.Round, 24, NovelUI.Lemon);
        if (state == State.Guide)
        {
            NovelUI.Label(new Rect(170, 270, 940, 35), "비전공자 안내 · 읽는 동안 시간 제한 없음", NovelUI.Round, 22, NovelUI.Mint);
            GUI.Label(new Rect(170, 325, 940, 110), Current.Guide, body);
            NovelUI.Fill(new Rect(170, 440, 940, 100), new Color(1, 1, 1, .06f), 10);
            GUI.Label(new Rect(185, 455, 910, 80), Current.Example, body);
            GUI.Label(new Rect(170, 560, 940, 65), Current.Hint, body);
            if (GUI.Button(new Rect(440, 650, 400, 60), "이해했어요 · 문제 시작 (20초)", button)) Begin();
            return;
        }
        for (int i = 0; i < Current.Lines.Length; i++)
        {
            Rect row = new Rect(170, 270 + i * 52, 940, 46);
            Color fill = new Color(1, 1, 1, .04f);
            if (state == State.Feedback && i == Current.Bug) fill = NovelUI.Alpha(NovelUI.Mint, .45f);
            else if (state == State.Feedback && i == picked) fill = NovelUI.Alpha(NovelUI.DeepPink, .5f);
            else if (state == State.Playing && NovelUI.Hover(row)) fill = NovelUI.Alpha(NovelUI.Pink, .25f);
            NovelUI.Fill(row, fill, 10);
            NovelUI.Label(new Rect(row.x + 14, row.y, 50, row.height), (i + 1).ToString(), NovelUI.Code, 20, NovelUI.Plum, TextAnchor.MiddleLeft);
            NovelUI.Label(new Rect(row.x + 64, row.y, row.width - 70, beginner ? 24 : row.height), Current.Lines[i], NovelUI.Code, beginner ? 20 : 24, NovelUI.Cream, TextAnchor.MiddleLeft);
            if (beginner) NovelUI.Label(new Rect(row.x + 64, row.y + 24, row.width - 70, 22), Current.Meanings[i], NovelUI.Round, 18, NovelUI.Mint, TextAnchor.MiddleLeft);
            if (state == State.Playing && GUI.Button(row, GUIContent.none, GUIStyle.none)) { Pick(i); break; }
        }
        if (state == State.Playing)
        {
            if (beginner) GUI.Label(new Rect(170, 605, 940, 75), Current.Hint + "\n결과를 바꾸는 코드 줄을 하나 눌러 주세요.", body);
            float left = Mathf.Clamp01(1 - (now - stateAt) / Limit);
            Box(new Rect(170, 700, 940, 10), new Color(1, 1, 1, .12f));
            Box(new Rect(170, 700, 940 * left, 10), left < .3f ? NovelUI.DeepPink : NovelUI.Mint);
        }
        else
        {
            string head = picked == Current.Bug ? "잡았다! " : picked < 0 ? "시간 초과 · " : "아쉬워요 · ";
            GUI.Label(new Rect(170, 600, 940, beginner ? 65 : 90), head + Current.Why, Face(body));
            if (beginner && GUI.Button(new Rect(440, 680, 400, 50), round == Puzzles.Length - 1 ? "풀이 확인 · 결과 보기" : "풀이 확인 · 다음 문제", button)) Next();
        }
    }
}
