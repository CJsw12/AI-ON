using UnityEngine;

// 카페 계산대 알바: 영수증에는 단가·개수·할인만 있고 합계는 직접 계산한다.
// 손님이 낸 돈에서 합계를 빼고 지폐·동전을 눌러 거스름돈을 맞춘다. 손님 다섯 명, 손님마다 20초.
public class ChangeMaker : MiniGame
{
    class Order { public string[] Lines; public int Price, Paid; }
    // Paid amounts are odd on purpose: customers hand over coins to get round change back.
    static readonly Order[] Orders = {
        new Order { Lines = new[] { "아메리카노 2,000 × 2", "쿠키 1,800 × 1" }, Price = 5800, Paid = 10000 },
        new Order { Lines = new[] { "카페라테 3,200 × 3", "텀블러 할인 -300 × 3" }, Price = 8700, Paid = 10200 },
        new Order { Lines = new[] { "샌드위치 4,500 × 1", "아이스티 2,800 × 1", "쿠폰 -1,000" }, Price = 6300, Paid = 20300 },
        new Order { Lines = new[] { "바닐라 라떼 3,700 × 2", "머핀 2,600 × 2", "세트 할인 -1,600" }, Price = 11000, Paid = 50000 },
        new Order { Lines = new[] { "아메리카노 2,000 × 7", "단체 10% 할인" }, Price = 12600, Paid = 15100 }
    };
    static readonly int[] Money = { 10000, 5000, 1000, 500, 100 };
    const float Limit = 20f;
    enum State { Instructions, Counting, Feedback, Completed }
    State state = State.Instructions;
    int customer, given, correct;
    float stateAt;
    bool lastOk;

    public ChangeMaker(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "거스름돈이 계속 틀렸습니다. 계산대의 신뢰는 동전 하나에서 무너집니다.", "절반은 맞았습니다. 큰 단위부터 세면 실수가 줄어듭니다.", "좋습니다. 바쁜 시간에도 거의 틀리지 않았군요." },
        new[] { "앗, 손님이 거스름돈 세다가 웃고 가셨대요! 다음엔 큰 돈부터 세 봐요.", "반은 딱 맞았어요! 손이 점점 빨라지던데요?", "계산대 에이스 등장! 사장님이 시급 올려 주신대요!" }
    };

    Order Current => Orders[customer];
    void Begin() { given = 0; state = State.Counting; stateAt = Now; }
    void Hand()
    {
        lastOk = given == Current.Paid - Current.Price;
        if (lastOk) correct++;
        state = State.Feedback; stateAt = Now;
        Judge(lastOk, new Vector2(885, 300), lastOk ? "딱 맞아요!" : "틀렸어요");
        if (!lastOk)
        {
            int expected = Current.Paid - Current.Price;
            ExplainFailure("건넨 돈은 " + Won(given) + ", 필요한 거스름돈은 " + Won(expected) + "이에요.\n" + Won(Mathf.Abs(given - expected)) + (given < expected ? " 부족해요." : " 더 건넸어요."),
                string.Join("\n", Current.Lines) + "\n할인까지 적용한 합계: " + Won(Current.Price) + "\n받은 돈 " + Won(Current.Paid) + " - 합계 " + Won(Current.Price) + " = " + Won(expected) + "\n큰 지폐부터 담고, 남은 금액을 동전으로 맞춰요.");
        }
    }
    static string Won(int v) => v.ToString("N0") + "원";

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Counting && Now - stateAt >= Limit) Hand();
        if (state == State.Feedback && Now - stateAt >= 2f)
        {
            if (++customer >= Orders.Length) { state = State.Completed; Done = true; Delta = correct >= 4 ? 2 : correct >= 3 ? 1 : 0; Summary = "손님 다섯 명 중 " + correct + "명 거스름돈 정확"; }
            else Begin();
        }
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), "CASHIER  ·  " + (Done ? "완료" : "손님 " + (Mathf.Min(customer, 4) + 1) + "/5") + "  ·  정확 " + correct, NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 220), "점심 피크 계산대예요. 포스기가 고장 나서 합계가 안 나와요!\n영수증의 단가·개수·할인으로 합계를 직접 계산하고,\n손님이 낸 돈에서 빼서 거스름돈을 건네세요. 손님마다 20초, 다섯 명이에요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) Begin();
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 330, 940, 100), Summary, NovelUI.Round, 40, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        NovelUI.Card(new Rect(170, 220, 450, 190), NovelUI.Blush, 20);
        for (int i = 0; i < Current.Lines.Length; i++)
            NovelUI.Label(new Rect(195, 230 + i * 32, 410, 32), Current.Lines[i], NovelUI.Soft, 21, NovelUI.Plum);
        NovelUI.Label(new Rect(195, 330, 410, 30), "합계  " + (state == State.Feedback ? Won(Current.Price) : "??? (포스기 고장)"), NovelUI.Soft, 21, NovelUI.Plum);
        NovelUI.Label(new Rect(195, 362, 410, 40), "받은 돈  " + Won(Current.Paid), NovelUI.Round, 28, NovelUI.Berry);
        NovelUI.Card(new Rect(660, 220, 450, 190), Color.white, 20);
        NovelUI.Label(new Rect(685, 235, 410, 40), "건넬 거스름돈", NovelUI.Soft, 22, NovelUI.Plum);
        NovelUI.Label(new Rect(685, 290, 410, 80), Won(given), NovelUI.Round, 44, NovelUI.Berry, TextAnchor.MiddleCenter);

        if (state == State.Counting)
        {
            for (int i = 0; i < Money.Length; i++)
                if (NovelUI.Button(new Rect(170 + i * 190, 440, 175, 70), Won(Money[i]), false, 22)) { given += Money[i]; NovelUI.Sound?.Invoke("Game/Coin|Select"); }
            if (NovelUI.Button(new Rect(170, 535, 300, 60), "↺ 다시 세기", false, 21)) given = 0;
            if (NovelUI.Button(new Rect(810, 535, 300, 60), "건네기", true, 22)) Hand();
            float left = Mathf.Clamp01(1 - (Now - stateAt) / Limit);
            Box(new Rect(170, 620, 940, 10), NovelUI.Alpha(NovelUI.Plum, .2f));
            Box(new Rect(170, 620, 940 * left, 10), left < .3f ? NovelUI.DeepPink : NovelUI.Mint);
        }
        else NovelUI.Label(new Rect(170, 470, 940, 60), lastOk ? "딱 맞아요! 손님이 웃으며 나가요." : "정답은 " + Won(Current.Paid - Current.Price) + "이었어요.", NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
    }
}
