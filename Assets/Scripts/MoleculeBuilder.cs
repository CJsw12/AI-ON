using UnityEngine;

// 원자 조립: 목표 분자식에 맞게 H·C·N·O 원자 수를 맞춰 제출한다.
// 다섯 분자, 전체 제한 시간 75초. 맞힌 분자 수로 판정.
public class MoleculeBuilder : MiniGame
{
    class Target { public string Name, Formula; public int[] Count; }   // H, C, N, O
    static readonly Target[] Pool = {
        new Target { Name = "물", Formula = "H2O", Count = new[] { 2, 0, 0, 1 } },
        new Target { Name = "이산화탄소", Formula = "CO2", Count = new[] { 0, 1, 0, 2 } },
        new Target { Name = "암모니아", Formula = "NH3", Count = new[] { 3, 0, 1, 0 } },
        new Target { Name = "메테인", Formula = "CH4", Count = new[] { 4, 1, 0, 0 } },
        new Target { Name = "산소 기체", Formula = "O2", Count = new[] { 0, 0, 0, 2 } },
        new Target { Name = "과산화수소", Formula = "H2O2", Count = new[] { 2, 0, 0, 2 } },
        new Target { Name = "에탄올", Formula = "C2H5OH", Count = new[] { 6, 2, 0, 1 } },
        new Target { Name = "질소 기체", Formula = "N2", Count = new[] { 0, 0, 2, 0 } }
    };
    static readonly string[] Atoms = { "H", "C", "N", "O" };
    const int Rounds = 5, MaxEach = 6;
    const float Limit = 75f;
    enum State { Instructions, Playing, Feedback, Completed }
    State state = State.Instructions;
    readonly int[] order = new int[Pool.Length];
    readonly int[] built = new int[4];
    int round, correct;
    float startedAt, stateAt;
    bool lastOk;

    public MoleculeBuilder(MiniGameTheme theme) : base(theme)
    {
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = order.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
    }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "분자식과 다른 분자가 많았습니다. 기호 뒤 숫자가 곧 원자 수입니다. 하나씩 세어 보세요.", "절반 넘게 맞혔습니다. 원소가 여러 개인 분자에서 개수를 서두르지 마세요.", "모두 정확합니다. 분자식을 읽는 눈이 생겼습니다. 반응식도 같은 방식으로 읽으세요." },
        new[] { "괜찮아요! 원자도 처음엔 다 헷갈려요. 기호 뒤 숫자만 잘 세면 금방이죠!", "오, 꽤 조립했네요! 남은 분자도 다음엔 뚝딱 만들 수 있어요.", "완벽한 조립! 분자 모형 장인이 탄생했죠!" }
    };

    Target Current => Pool[order[round]];
    float Left => Mathf.Max(0, Limit - (Now - startedAt));

    void Begin() { System.Array.Clear(built, 0, 4); state = State.Playing; }
    void Submit()
    {
        lastOk = true;
        for (int k = 0; k < 4; k++) if (built[k] != Current.Count[k]) lastOk = false;
        if (lastOk) correct++;
        state = State.Feedback; stateAt = Now;
        Judge(lastOk, new Vector2(640, 260), lastOk ? Current.Formula + " 완성!" : "원자 수가 달라요");
    }
    void Finish()
    {
        state = State.Completed; Done = true;
        Delta = correct >= 5 ? 2 : correct >= 3 ? 1 : 0;
        Summary = "분자 " + Rounds + "개 중 " + correct + "개 조립 성공";
        if (Delta == 0)
        {
            ExplainFailure(
                "조립에 성공한 분자가 " + correct + "/" + Rounds + "개로 기준(3개 이상)에 미치지 못했거나 시간이 초과되었어요.\n원소 기호 뒤에 붙은 숫자가 해당 원자의 개수예요.",
                "분자식 읽기 가이드:\n1. 기호 뒤에 숫자가 없으면 1개입니다. (예: H2O → 수소(H) 2개, 산소(O) 1개)\n2. 에탄올(C2H5OH) 같은 분자는 H가 나뉘어 적혀 있어도 총 개수를 합산합니다: C 2개, H (5+1)=6개, O 1개\n3. 제한 시간(75초) 내에 당황하지 말고 H·C·N·O 카드 순서대로 숫자를 맞추세요."
            );
        }
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Playing && Left <= 0) Finish();
        if (state == State.Feedback && Now - stateAt >= 1.6f)
        {
            if (++round >= Rounds || Left <= 0) Finish(); else Begin();
        }
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Sky, 2);
        string head = "MOLECULE KIT  ·  " + (Done ? "완료" : "분자 " + (Mathf.Min(round, Rounds - 1) + 1) + "/" + Rounds) + "  ·  성공 " + correct;
        if (state == State.Playing || state == State.Feedback) head += "  ·  남은 시간 " + Mathf.CeilToInt(Real(Left)) + "초";
        NovelUI.Label(new Rect(160, 168, 950, 35), head, NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 260), "위에 나온 분자식대로 원자를 조립하세요.\n원자 아래 + / − 버튼으로 개수를 바꿔요. 원소 기호 뒤 숫자가 그 원자의 개수예요.\n(숫자가 없으면 1개, 예: H2O = H 2개 + O 1개)\n다 맞췄다면 '조립 완료'! 분자 다섯 개, 제한 시간은 " + Mathf.CeilToInt(Real(Limit)) + "초예요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) { startedAt = Now; Begin(); }
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 330, 940, 100), Summary, NovelUI.Round, 40, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        Target t = Current;
        NovelUI.Label(new Rect(170, 210, 940, 50), t.Name + "  ·  " + t.Formula, NovelUI.Round, 34, NovelUI.Plum, TextAnchor.MiddleCenter);
        // Timer bar.
        Box(new Rect(170, 262, 940, 8), NovelUI.Alpha(NovelUI.Plum, .15f));
        Box(new Rect(170, 262, 940 * Left / Limit, 8), Left < 15 ? NovelUI.DeepPink : NovelUI.Mint);

        Color[] tint = { Color.white, new Color(.35f, .35f, .4f), NovelUI.Sky, NovelUI.Pink };
        for (int k = 0; k < 4; k++)
        {
            float x = 190 + k * 235;
            Rect card = new Rect(x, 290, 200, 230);
            NovelUI.Card(card, NovelUI.Blush, 16);
            NovelUI.Fill(new Rect(x + 60, 305, 80, 80), tint[k], 40);
            NovelUI.Label(new Rect(x + 60, 305, 80, 80), Atoms[k], NovelUI.Round, 34, k == 1 ? Color.white : NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(x, 395, 200, 50), built[k] + "개", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (state == State.Playing)
            {
                if (NovelUI.Button(new Rect(x + 15, 455, 75, 50), "−", false, 26) && built[k] > 0) { built[k]--; NovelUI.Sound?.Invoke("Select"); }
                if (NovelUI.Button(new Rect(x + 110, 455, 75, 50), "+", true, 26) && built[k] < MaxEach) { built[k]++; NovelUI.Sound?.Invoke("Select"); }
            }
        }
        // Assembled formula preview.
        string made = "";
        for (int k = 0; k < 4; k++) if (built[k] > 0) made += Atoms[k] + (built[k] > 1 ? built[k].ToString() : "") + " ";
        NovelUI.Label(new Rect(170, 535, 600, 40), "내 분자: " + (made == "" ? "(비어 있음)" : made.Trim()), NovelUI.Soft, 22, NovelUI.Plum);
        if (state == State.Playing)
        {
            if (NovelUI.Button(new Rect(810, 600, 300, 60), "조립 완료", true, 22)) Submit();
        }
        else
        {
            string need = "";
            for (int k = 0; k < 4; k++) if (t.Count[k] > 0) need += Atoms[k] + " " + t.Count[k] + "개 ";
            NovelUI.Label(new Rect(170, 600, 940, 60), lastOk ? "딱 맞아요! 분자가 반짝여요." : "정답은 " + need.Trim() + "이었어요.", NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
        }
    }
}
