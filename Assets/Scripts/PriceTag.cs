using UnityEngine;

// 경영학과 전용: 사흘 동안 꾸러미 시범 판매 가격을 정한다. 이익 = (가격 - 원가) × 판매량.
// 날마다 상황이 바뀌어 가장 이익이 큰 가격도 달라진다. 최고 이익 가격을 고른 날을 센다.
public class PriceTag : MiniGame
{
    const int Cost = 3000;
    static readonly int[] Prices = { 4000, 5000, 6000, 8000, 10000 };
    class Day { public string Note, Lesson; public int[] Sold; }
    static readonly Day[] Days = {
        new Day { Note = "첫째 날 · 신입생 인터뷰: \"5천 원이면 바로 사요.\" \"만 원은 좀…\"", Sold = new[] { 150, 140, 110, 50, 10 },
            Lesson = "싸게 팔면 많이 나가도 남는 게 적어요. 오늘은 6,000원이 가장 많이 남았어요." },
        new Day { Note = "둘째 날 · 꾸러미 사진이 SNS에 퍼져서 학부모 문의가 쏟아진다.", Sold = new[] { 160, 150, 120, 90, 30 },
            Lesson = "찾는 사람이 많아지면 값을 올려도 팔려요. 오늘은 8,000원이 가장 많이 남았어요." },
        new Day { Note = "셋째 날 · 길 건너 할인점이 비슷한 세트를 5,500원에 내놓았다.", Sold = new[] { 170, 150, 40, 10, 0 },
            Lesson = "경쟁 상품보다 비싸면 손님이 떠나요. 오늘은 5,000원이 가장 많이 남았어요." }
    };
    enum State { Instructions, Choosing, Result, Completed }
    State state = State.Instructions;
    int day, chosen, best;

    public PriceTag(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "가격을 감으로 정했습니다. 판매량과 이익은 다른 숫자입니다. 표를 다시 계산해 보세요.", "하루는 정확했습니다. 상황이 바뀌면 최적 가격도 바뀐다는 점을 기억하세요.", "잘했습니다. 수요와 경쟁을 읽고 가격을 바꿨군요. 이것이 가격 전략입니다." },
        new[] { "앗, 많이 팔았는데 남는 게 별로 없네요! 이익은 (가격 - 원가) × 개수예요.", "하루는 대박! 손님 분위기 따라 가격도 바꿔 보는 거예요.", "최고 이익 가격을 척척 찾았어요! 사장님이 가격표 담당 맡기신대요!" }
    };

    static int Profit(int price, int sold) => (price - Cost) * sold;
    static int BestIndex(Day d)
    {
        int b = 0;
        for (int i = 1; i < Prices.Length; i++) if (Profit(Prices[i], d.Sold[i]) > Profit(Prices[b], d.Sold[b])) b = i;
        return b;
    }
    static string Won(int value) => value.ToString("N0") + "원";

    void Choose(int index)
    {
        chosen = index;
        if (index == BestIndex(Days[day])) best++;
        state = State.Result;
        Judge(index == BestIndex(Days[day]), new Vector2(260 + index * 188, 280), index == BestIndex(Days[day]) ? "최고 이익!" : "더 남길 수 있어요");
        int top = BestIndex(Days[day]);
        if (index != top)
        {
            Day d = Days[day];
            ExplainFailure("선택한 " + Won(Prices[index]) + "의 이익은 " + Won(Profit(Prices[index], d.Sold[index])) + "이에요.\n" + Won(Prices[top]) + "를 고르면 " + Won(Profit(Prices[top], d.Sold[top])) + "으로 더 많이 남아요.",
                "이익 = (판매 가격 - 원가) × 판매량\n내 선택: (" + Won(Prices[index]) + " - " + Won(Cost) + ") × " + d.Sold[index] + "개\n최고 이익: (" + Won(Prices[top]) + " - " + Won(Cost) + ") × " + d.Sold[top] + "개\n" + d.Lesson);
        }
    }
    void Next()
    {
        day++;
        if (day < Days.Length) { state = State.Choosing; return; }
        state = State.Completed; Done = true; Delta = best >= 2 ? 2 : best;
        Summary = "사흘 중 " + best + "일 최고 이익 가격";
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), "PRICE TAG  ·  " + (Done ? "완료" : "시범 판매 " + (day + 1) + "/3일") + "  ·  원가 " + Won(Cost), NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 220), "꾸러미 하나를 만드는 데 3,000원이 들어요.\n사흘 동안 매일 가격을 하나 골라 시범 판매를 합니다.\n이익 = (가격 - 원가) × 팔린 개수.\n많이 파는 것보다 많이 남기는 가격을 찾아보세요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) state = State.Choosing;
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 330, 940, 100), Summary, NovelUI.Round, 40, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        Day d = Days[day];
        NovelUI.Label(new Rect(170, 215, 940, 60), d.Note, NovelUI.Soft, 22, NovelUI.Plum);
        int top = BestIndex(d), max = Profit(Prices[top], d.Sold[top]);
        for (int i = 0; i < Prices.Length; i++)
        {
            Rect tag = new Rect(175 + i * 188, 290, 170, 70);
            bool picked = state == State.Result && i == chosen;
            if (state == State.Choosing)
            {
                if (NovelUI.Button(tag, Won(Prices[i]), false, 24)) { Choose(i); break; }
                // Expected sales are shown up front so the best price can be worked out, not guessed.
                NovelUI.Label(new Rect(tag.x, 372, tag.width, 26), "예상 " + d.Sold[i] + "개", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
                continue;
            }
            NovelUI.Card(tag, picked ? NovelUI.Pink : NovelUI.Blush, 35);
            NovelUI.Label(tag, Won(Prices[i]), NovelUI.Round, 24, picked ? Color.white : NovelUI.Berry, TextAnchor.MiddleCenter);
            // After choosing, show every price's sales and profit as bars so the trade-off is visible.
            int profit = Profit(Prices[i], d.Sold[i]);
            float height = max > 0 ? 150f * profit / max : 0;
            Box(new Rect(tag.x + 45, 540 - height, 80, height), i == top ? NovelUI.Mint : NovelUI.Alpha(NovelUI.Pink, .6f));
            NovelUI.Label(new Rect(tag.x, 372, tag.width, 26), d.Sold[i] + "개 판매", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(tag.x, 545, tag.width, 26), "이익 " + Won(profit), NovelUI.Soft, 18, NovelUI.Berry, TextAnchor.MiddleCenter);
        }
        if (state == State.Choosing)
        {
            NovelUI.Label(new Rect(170, 420, 940, 60), "예상 판매량을 보고 이익이 가장 큰 가격을 골라 주세요.", NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
            return;
        }
        NovelUI.Label(new Rect(170, 590, 940, 60), (chosen == top ? "최고 이익! " : "") + d.Lesson, NovelUI.Soft, 21, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (GUI.Button(new Rect(490, 660, 300, 60), day < Days.Length - 1 ? "다음 날" : "결과 정리", button)) Next();
    }
}
