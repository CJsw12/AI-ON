using UnityEngine;

// Chapter 5: make two cups exactly as ordered before time runs out. The professor's order differs by professor.
public class CoffeeOrder : MiniGame
{
    const float Seconds = 30;
    static readonly string[] Ingredients = { "에스프레소 샷", "물", "우유", "시럽" };
    readonly string[] orders;
    readonly int[][] recipes;
    readonly int[] cup = new int[4];
    readonly float endsAt = Time.unscaledTime + Seconds;
    int order, correct;
    bool professorCorrect;

    public CoffeeOrder(int professor)
    {
        orders = new[] { professor == 0 ? "교수님 · 진한 아메리카노" : "교수님 · 바닐라 라떼", "동기 · 달달한 아메리카노" };
        recipes = new[] { professor == 0 ? new[] { 2, 1, 0, 0 } : new[] { 1, 0, 2, 1 }, new[] { 1, 1, 0, 2 } };
    }
    public override string Title => MiniGames.Title(5);
    public override bool CountsForStat => professorCorrect;
    public override string[][] Replies => new[] {
        new[] { "주문과 다릅니다. 확인하지 않고 만든 결과물은 다시 만들어야 합니다.", "한 잔은 주문과 다릅니다. 요구 사항은 끝까지 확인하세요. 코드도 마찬가지입니다.", "주문서대로군요. 동기 것까지 정확합니다. 요구 사항을 지키는 것도 실력입니다." },
        new[] { "오… 새로운 맛이네요? 괜찮아요, 커피도 디버깅하면 됩니다!", "음, 하나는 살짝 다른데요? 그래도 정성이 들어갔으니 맛있어요!", "완벽한 바리스타네요! 이걸로 오늘 밤 버그도 다 잡겠어요." }
    };

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        float left = endsAt - Time.unscaledTime;
        if (!Done && left <= 0) Finish();
        if (Done) { GUI.Label(new Rect(100, 300, 1080, 120), Summary, title); return; }
        GUI.Label(new Rect(100, 120, 1080, 35), "주문대로 정확히 만드세요.   남은 시간 " + Mathf.CeilToInt(left) + "초   ·   주문 " + (order + 1) + "/2", body);
        Box(new Rect(110, 175, 1060, 120), new Color(.98f, .93f, .84f, .95f));
        GUI.color = new Color(.3f, .16f, .08f);
        GUI.Label(new Rect(140, 185, 1000, 40), "주문서 · " + orders[order], body);
        GUI.Label(new Rect(140, 235, 1000, 40), Describe(recipes[order]), body);
        GUI.color = Color.white;
        GUI.Label(new Rect(110, 320, 1060, 40), "지금 컵:  " + (Describe(cup) == "" ? "비어 있음" : Describe(cup)), body);
        for (int i = 0; i < 4; i++)
            if (GUI.Button(new Rect(110 + i * 268, 390, 250, 80), Ingredients[i] + " +1", button)) cup[i]++;
        if (GUI.Button(new Rect(110, 500, 420, 70), "컵 비우기", button)) System.Array.Clear(cup, 0, 4);
        if (GUI.Button(new Rect(750, 500, 420, 70), "완성해서 건네기", button)) Serve();
    }
    static string Describe(int[] amounts)
    {
        string text = "";
        for (int i = 0; i < 4; i++) if (amounts[i] > 0) text += (text == "" ? "" : "  ·  ") + Ingredients[i] + " " + amounts[i];
        return text;
    }
    void Serve()
    {
        bool same = true;
        for (int i = 0; i < 4; i++) same &= cup[i] == recipes[order][i];
        if (same) correct++;
        if (order == 0) professorCorrect = same;
        System.Array.Clear(cup, 0, 4);
        if (++order == 2) Finish();
    }
    void Finish()
    {
        Done = true;
        Delta = correct;
        Summary = "2잔 중 " + correct + "잔 주문대로";
    }
}
