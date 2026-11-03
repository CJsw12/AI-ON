using UnityEngine;

// 스마트시스템공학과 전용: 한 줄로 이어진 부품 여덟 개 중 끊긴 하나를 멀티미터로 찾아 교체한다.
// 측정 세 번이면 반씩 좁혀 반드시 찾을 수 있다. 세 판 중 측정 3번 이내로 맞힌 판을 센다.
public class CircuitProbe : MiniGame
{
    static readonly string[] Parts = { "퓨즈", "스위치", "저항", "콘덴서", "다이오드", "트랜지스터", "코일", "스피커" };
    const int Rounds = 3, Budget = 3;
    enum State { Instructions, Playing, Feedback, Completed }
    State state = State.Instructions;
    int round, broken, probes, good, replaced;
    float stateAt;
    readonly string[] order = new string[8];
    readonly int[] reading = new int[7];   // Test point k sits after part k: 0 not measured, 1 = ON, 2 = OFF.
    readonly bool beginner;

    public CircuitProbe(MiniGameTheme theme, bool experienced = true) : base(theme) { beginner = !experienced; }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "측정 없이 부품을 뜯었습니다. 가운데부터 재서 범위를 반씩 줄이세요.", "찾기는 했습니다. 측정 횟수를 줄이려면 매번 남은 구간의 가운데를 재세요.", "정확합니다. 세 번 측정으로 범위를 좁혔군요. 이것이 고장 진단의 기본입니다." },
        new[] { "범인이 도망갔어요! 가운데부터 콕 찍어 보면 금방 잡혀요.", "범인 검거! 조금만 더 아껴 재면 탐정 레벨 업이에요.", "멀티미터 명탐정! 세 번 만에 딱딱 찾아냈어요!" }
    };

    void Begin()
    {
        for (int i = 0; i < 8; i++) order[i] = Parts[i];
        for (int i = 7; i > 0; i--) { int j = Random.Range(0, i + 1); string t = order[i]; order[i] = order[j]; order[j] = t; }
        broken = Random.Range(0, 8);
        probes = 0; replaced = -1;
        for (int i = 0; i < 7; i++) reading[i] = 0;
        state = State.Playing;
    }
    void Probe(int point)
    {
        if (reading[point] != 0) return;
        probes++;
        reading[point] = broken > point ? 1 : 2;
        NovelUI.Sound?.Invoke("LoopTick");
    }
    void Replace(int part)
    {
        replaced = part;
        bool ok = part == broken && probes <= Budget;
        if (ok) good++;
        state = State.Feedback; stateAt = Now;
        Judge(part == broken, new Vector2(262 + part * 113, 320), part == broken ? "수리 완료!" : "멀쩡한 부품이에요");
        if (!ok)
        {
            string reason = part == broken
                ? "고장 부품은 맞혔지만 측정을 " + probes + "번 했어요. 성공 기준은 3번 이내예요."
                : "교체한 " + order[part] + "(왼쪽에서 " + (part + 1) + "번째)은 정상이에요.\n실제 고장은 " + order[broken] + "(" + (broken + 1) + "번째)이에요." + (probes == 0 ? " 측정 없이 교체했어요." : " 측정 결과와 고장 위치를 함께 확인해요.");
            ExplainFailure(reason, RepairSteps());
        }
    }
    string RepairSteps()
    {
        int low = 0, high = 7;
        string steps = "가운데를 재서 후보를 반씩 줄여요. ON이면 오른쪽, OFF이면 왼쪽(바로 앞 부품 포함)이에요.\n";
        while (low < high)
        {
            int point = (low + high) / 2;
            bool on = broken > point;
            steps += (point + 1) + "번 부품 뒤 " + (on ? "ON → 오른쪽" : "OFF → 왼쪽") + "  /  ";
            if (on) low = point + 1; else high = point;
        }
        return steps + "\n" + (broken + 1) + "번째 " + order[broken] + "를 교체하면 돼요. (이 판의 해결 예시)";
    }
    void Finish()
    {
        state = State.Completed; Done = true; Delta = good >= 2 ? 2 : good;
        Summary = "세 대 중 " + good + "대를 측정 3번 안에 수리";
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Feedback && Now - stateAt >= 2.5f)
        {
            round++;
            if (round >= Rounds) Finish(); else Begin();
        }
        NovelUI.Card(new Rect(130, 150, 1020, 600), new Color(.08f, .12f, .14f), 18, NovelUI.Mint, 2);
        GUI.Label(new Rect(155, 170, 950, 35), "MULTIMETER  ·  " + (Done ? "완료" : "라디오 " + (Mathf.Min(round, 2) + 1) + "/3") + "  ·  측정 " + probes + "회", body);
        if (state == State.Instructions)
        {
            GUI.Label(new Rect(175, beginner ? 230 : 240, 930, beginner ? 380 : 220), beginner
                ? "멀티미터는 전기가 전달되는지 확인하는 도구예요.\n이 게임에서는 전기가 왼쪽에서 오른쪽으로 흐르고, 고장 뒤쪽은 OFF가 돼요.\n\n① 작은 ●를 누르면 측정해요. 부품 이름을 누르면 바로 교체해요.\n② ON(전기 옴)이면 그 지점 오른쪽에 고장이 있어요.\n③ OFF(전기 안 옴)이면 그 지점 왼쪽에 고장이 있어요. 바로 앞 부품도 포함해요.\n예: [1번] 뒤 ON, [2번] 뒤 OFF라면 → 2번 부품이 끊겼어요.\n\n가운데부터 재고 남은 후보의 가운데를 다시 재면, 3번 안에 찾을 수 있어요."
                : "라디오 안 부품 여덟 개가 한 줄로 이어져 있고, 그중 하나가 끊겼어요.\n부품 사이의 ● 지점을 누르면 전기가 오는지 재요. 오면 ON, 안 오면 OFF.\n전원 쪽에서 보면 ON, ON… 하다가 끊긴 부품 뒤부터 OFF가 돼요.\nON과 OFF 사이의 부품을 눌러 교체하세요. 측정 3번 안에 찾으면 성공!", body);
            if (GUI.Button(new Rect(440, beginner ? 650 : 560, 400, beginner ? 60 : 75), "시작하기", button)) Begin();
            return;
        }
        if (state == State.Completed) { GUI.Label(new Rect(170, 330, 940, 100), Summary, title); return; }
        if (beginner) GUI.Label(new Rect(170, 225, 940, 75), "작은 ● = 측정 / 부품 이름 = 교체\nON이면 오른쪽, OFF이면 왼쪽(바로 앞 부품 포함)을 살펴보세요.", body);

        // Battery on the left feeds the chain; the wire runs under the parts.
        NovelUI.Label(new Rect(150, 330, 60, 90), "전원", NovelUI.Round, 20, NovelUI.Lemon, TextAnchor.MiddleCenter);
        Box(new Rect(200, 374, 920, 4), NovelUI.Alpha(NovelUI.Cream, .5f));
        int lit = -1;
        for (int i = 0; i < 7; i++) if (reading[i] == 1) lit = i;
        if (lit >= 0) Box(new Rect(200, 373, 215 + lit * 113 + 95 - 200, 6), NovelUI.Lemon);
        for (int i = 0; i < 8; i++)
        {
            Rect part = new Rect(215 + i * 113, 330, 95, 90);
            bool reveal = state == State.Feedback && i == broken;
            Color fill = reveal ? NovelUI.Mint : state == State.Feedback && i == replaced ? NovelUI.DeepPink : NovelUI.Blush;
            if (state == State.Playing && NovelUI.Hover(part)) fill = NovelUI.Lemon;
            NovelUI.Card(part, fill, 14);
            NovelUI.Label(part, order[i], NovelUI.Round, order[i].Length > 4 ? 16 : 20, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (state == State.Playing && GUI.Button(part, GUIContent.none, GUIStyle.none)) { Replace(i); break; }
            if (i == 7) continue;
            Rect point = new Rect(part.xMax - 1, 440, 20, 20);
            NovelUI.Fill(point, reading[i] == 1 ? NovelUI.Mint : reading[i] == 2 ? NovelUI.DeepPink : NovelUI.Cream, 10);
            if (reading[i] != 0)
                NovelUI.Label(new Rect(point.center.x - 30, 465, 60, 30), reading[i] == 1 ? "ON" : "OFF", NovelUI.Round, 20, reading[i] == 1 ? NovelUI.Mint : NovelUI.Pink, TextAnchor.MiddleCenter);
            if (state == State.Playing && GUI.Button(new Rect(point.x - 10, point.y - 10, 40, 40), GUIContent.none, GUIStyle.none)) Probe(i);
        }
        string hint = state == State.Playing
            ? (probes == 0 ? "● 지점을 눌러 측정하세요. 가운데부터 재면 빨라요. 마지막 ON 바로 뒤, 첫 OFF 바로 앞 부품이 범인!" : probes > Budget ? "측정을 많이 했어요. 이번 판은 찾아도 성공으로 치지 않아요." : "범인 부품을 누르면 교체합니다.")
            : replaced == broken ? (probes <= Budget ? "수리 성공! 라디오에서 노래가 흘러나와요." : "찾았지만 측정이 " + probes + "번이었어요.")
            : "끊긴 부품은 " + order[broken] + "였어요.";
        GUI.Label(new Rect(170, 560, 940, 80), hint, Face(body));
    }
}
