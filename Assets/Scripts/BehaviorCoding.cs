using UnityEngine;

// 행동 관찰 기록: 실험 참가자를 지켜본 기록 문장을 하나씩 코딩한다.
// 눈에 보이는 행동이면 협력·공격·회피 중 하나로, 마음을 추측한 문장이면 '해석'으로 분류한다. 8문장, 문장마다 12초.
public class BehaviorCoding : MiniGame
{
    static readonly string[] Codes = { "협력 행동", "공격 행동", "회피 행동", "해석 (관찰 아님)" };
    class Item { public string Text; public int Code; public string Why; }
    static readonly Item[] Items = {
        new Item { Text = "참가자 A가 짝에게 블록 두 개를 건넸다.", Code = 0, Why = "건네는 동작은 눈으로 본 그대로예요. 함께 과제를 하려는 협력 행동으로 코딩해요." },
        new Item { Text = "참가자 B는 짝을 싫어하는 것 같다.", Code = 3, Why = "'싫어한다'는 마음은 보이지 않아요. 추측은 해석 칸에 따로 적어요." },
        new Item { Text = "참가자 C가 짝이 쌓은 탑을 손으로 밀어 무너뜨렸다.", Code = 1, Why = "밀어 무너뜨린 동작이 기록돼 있어요. 의도를 묻지 않고 행동만으로 공격 행동이에요." },
        new Item { Text = "참가자 D가 질문을 받자 고개를 돌리고 의자를 뒤로 뺐다.", Code = 2, Why = "고개를 돌리고 물러난 동작이에요. '부끄러워서'라고 덧붙이는 순간 해석이 돼요." },
        new Item { Text = "참가자 E는 과제가 지루해서 대충 했다.", Code = 3, Why = "'지루해서'와 '대충'은 관찰자의 판단이에요. 실제로 무엇을 했는지 적어야 해요." },
        new Item { Text = "참가자 F가 \"같이 하자\"라고 말하며 블록을 가운데로 옮겼다.", Code = 0, Why = "말과 동작이 모두 기록돼 있어요. 협력 행동의 좋은 예시예요." },
        new Item { Text = "참가자 G가 짝에게 \"너 때문에 망쳤어\"라고 소리쳤다.", Code = 1, Why = "들은 말을 그대로 옮겼어요. 언어적 공격 행동으로 코딩해요." },
        new Item { Text = "참가자 H는 불안해 보였고 자신감이 없었다.", Code = 3, Why = "'불안해 보임'은 인상이에요. 손을 떨었다, 3초간 말이 없었다처럼 행동으로 바꿔 적어요." }
    };
    const float Limit = 12f;
    enum State { Instructions, Coding, Feedback, Completed }
    State state = State.Instructions;
    int item, correct, lastPick;
    float stateAt;
    readonly bool[] held = new bool[5];

    public BehaviorCoding(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "관찰과 해석이 섞여 있습니다. 보이는 행동만 적고, 추측은 따로 표시하세요.", "대부분 정확합니다. '~같다', '~해 보인다'가 나오면 해석인지 의심하세요.", "정확한 기록입니다. 이런 관찰지는 다른 연구자가 봐도 같은 결론이 나옵니다." },
        new[] { "괜찮아요, 사람을 보면 마음부터 짐작하게 되죠! 보이는 것부터 적어 봐요.", "오~ 거의 다 맞았어요! 추측이랑 관찰을 꽤 잘 나눴죠?", "완벽해요! 이 관찰지라면 참가자도 억울할 일 없겠어요!" }
    };

    Item Current => Items[item];
    void Begin() { state = State.Coding; stateAt = Now; }
    void Answer(int pick)
    {
        lastPick = pick;
        bool ok = pick == Current.Code;
        if (ok) correct++;
        Judge(ok, new Vector2(640, 300), pick < 0 ? "시간 초과" : ok ? "정확한 코딩!" : "다시 보기");
        state = State.Feedback; stateAt = Now;
    }
    void Next()
    {
        if (++item >= Items.Length)
        {
            state = State.Completed; Done = true;
            Delta = correct >= 7 ? 2 : correct >= 5 ? 1 : 0;
            Summary = "기록 8개 중 " + correct + "개 정확히 코딩";
        }
        else Begin();
    }
    // One action per physical key press; held[] ignores OS key repeat.
    int Key()
    {
        Event e = Event.current;
        if (NovelUI.Repaint)
        {
            for (int k = 0; k < 4; k++)
                if (!Input.GetKey(KeyCode.Alpha1 + k) && !Input.GetKey(KeyCode.Keypad1 + k)) held[k] = false;
            if (!Input.GetKey(KeyCode.Space) && !Input.GetKey(KeyCode.Return) && !Input.GetKey(KeyCode.KeypadEnter)) held[4] = false;
        }
        if (e.type != EventType.KeyDown && e.type != EventType.KeyUp) return -1;
        int k2 = e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha4 ? e.keyCode - KeyCode.Alpha1
            : e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad4 ? e.keyCode - KeyCode.Keypad1
            : e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter ? 4 : -1;
        if (k2 < 0) return -1;
        bool up = e.type == EventType.KeyUp;   // Read before Use(), which turns the type into Used.
        e.Use();
        if (up) { held[k2] = false; return -1; }
        if (held[k2]) return -1;
        held[k2] = true;
        return k2;
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Coding && Now - stateAt >= Limit) Answer(-1);
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), "OBSERVATION LOG  ·  " + (Done ? "완료" : "기록 " + (Mathf.Min(item, 7) + 1) + "/8") + "  ·  정확 " + correct, NovelUI.Round, 21, NovelUI.Berry);

        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 260), "실험 참가자를 관찰한 기록이 한 줄씩 나와요.\n눈에 보이는 행동이면 <b>협력 · 공격 · 회피</b> 중 하나로,\n마음을 짐작한 문장이면 <b>해석</b>으로 분류하세요.\n키보드 1~4 또는 버튼, 기록마다 " + Mathf.CeilToInt(Real(Limit)) + "초예요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) Begin();
            return;
        }
        if (state == State.Completed)
        {
            NovelUI.Label(new Rect(170, 300, 940, 90), Summary, NovelUI.Round, 38, NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(170, 400, 940, 80), "관찰은 '무엇을 했는가', 해석은 '왜 그랬을까'.\n둘을 나눠 적어야 다른 사람도 같은 기록을 얻어요.", NovelUI.Soft, 22, NovelUI.Plum, TextAnchor.MiddleCenter);
            return;
        }

        NovelUI.Card(new Rect(170, 225, 940, 150), Color.white, 20, NovelUI.Blush, 2);
        NovelUI.Label(new Rect(200, 235, 880, 30), "관찰 기록 #" + (item + 1), NovelUI.Soft, 19, NovelUI.Plum);
        NovelUI.Label(new Rect(200, 270, 880, 95), Current.Text, NovelUI.Round, 28, NovelUI.Berry, TextAnchor.MiddleCenter);

        int key = Key();
        if (state == State.Coding)
        {
            float left = Mathf.Clamp01(1 - (Now - stateAt) / Limit);
            Box(new Rect(170, 390, 940, 8), NovelUI.Alpha(NovelUI.Plum, .2f));
            Box(new Rect(170, 390, 940 * left, 8), left < .3f ? NovelUI.DeepPink : NovelUI.Mint);
            if (key >= 0 && key < 4) { Answer(key); return; }
            for (int i = 0; i < 4; i++)
                if (NovelUI.Button(new Rect(170 + i * 240, 430, 220, 75), (i + 1) + "  " + Codes[i], i == 3, 21)) { Answer(i); return; }
        }
        else
        {
            bool ok = lastPick == Current.Code;
            string head = (ok ? "정답! " : lastPick < 0 ? "시간 초과 · " : "아쉬워요 · ") + "정답은 '" + Codes[Current.Code] + "'";
            NovelUI.Card(new Rect(170, 420, 940, 170), ok ? NovelUI.Alpha(NovelUI.Mint, .35f) : NovelUI.Blush, 20);
            NovelUI.Label(new Rect(200, 432, 880, 36), head, NovelUI.Round, 24, NovelUI.Berry);
            NovelUI.Label(new Rect(200, 475, 880, 110), Current.Why, NovelUI.Soft, 21, NovelUI.Plum);
            if (key == 4 || NovelUI.Button(new Rect(810, 610, 300, 60), item == Items.Length - 1 ? "결과로" : "다음 기록", true, 21)) Next();
        }
    }
}
