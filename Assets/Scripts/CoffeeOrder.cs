using System.Collections.Generic;
using UnityEngine;

// Three short orders; the first is the selected professor's drink.
public class CoffeeOrder : MiniGame
{
    const float Duration = 60f;
    const int Capacity = 8;
    static readonly string[] Names = { "샷", "물", "우유", "시럽" };
    static readonly Color[] Colors = { new Color(.36f,.16f,.1f), new Color(.55f,.8f,.95f), new Color(1f,.94f,.8f), new Color(.84f,.55f,.2f) };
    readonly string[] orders;
    readonly int[][] recipes;
    readonly List<int> cup = new List<int>();
    readonly float endsAt = Time.unscaledTime + Duration;
    int round, correct;
    float roundStartedAt, pourStartedAt = -10, resultUntil;
    bool professorCorrect, showingResult, lastCorrect;
    string warning = "";

    public CoffeeOrder(int professor)
    {
        int[][] professorOptions = professor == 0
            ? new[] { new[] { 0, 0, 1 }, new[] { 0, 0, 1, 1 }, new[] { 0, 0, 0, 1 } }
            : new[] { new[] { 0, 2, 2, 3 }, new[] { 0, 2, 3, 3 }, new[] { 0, 0, 2, 2, 3 } };
        int[][] friendOptions = { new[] { 0, 1, 3, 3 }, new[] { 1, 0, 0, 3 }, new[] { 0, 1, 1, 3, 3 } };
        int[][] finalOptions = { new[] { 2, 0, 3, 2, 0, 3 }, new[] { 1, 0, 3, 0, 1, 3 }, new[] { 2, 3, 0, 2, 3, 0 } };
        orders = new[] { professor == 0 ? "교수님 · 진한 아메리카노" : "교수님 · 바닐라 라떼", "동기 · 맞춤 커피", "마무리 주문 · 맞춤 커피" };
        recipes = new[] { professorOptions[Random.Range(0, professorOptions.Length)], friendOptions[Random.Range(0, friendOptions.Length)], finalOptions[Random.Range(0, finalOptions.Length)] };
        roundStartedAt = Time.unscaledTime;
    }
    public override string Title => MiniGames.Title(5);
    public override bool CountsForStat => professorCorrect;
    public override string[][] Replies => new[] {
        new[] { "주문을 다시 확인해 보세요. 요구 사항을 확인하는 과정이 중요합니다.", "잘한 부분이 있습니다. 다음에는 순서도 확인하세요.", "주문을 꼼꼼히 확인했군요. 요구 사항을 지키는 것도 실력입니다." },
        new[] { "괜찮아요! 다음에는 주문을 같이 확인해 봐요.", "한 잔은 잘 만들었네요. 다음에도 함께 해봐요!", "완벽한 바리스타네요! 덕분에 힘이 나요." }
    };
    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        float now = Time.unscaledTime;
        if (!Done && now >= endsAt) Finish();
        if (Done) { GUI.Label(new Rect(100, 300, 1080, 120), Summary, title); return; }
        if (showingResult && now >= resultUntil)
        {
            showingResult = false; round++; cup.Clear(); warning = "";
            if (round >= 3) { Finish(); return; }
            roundStartedAt = now;
        }
        GUI.Label(new Rect(100, 120, 1080, 35), "주문 " + (round + 1) + "/3   ·   남은 시간 " + Mathf.CeilToInt(endsAt - now) + "초", body);
        Rect cupRect = new Rect(200, 200, 260, 300);
        float age = now - pourStartedAt;
        float shake = age < .3f ? Mathf.Sin(age * 65) * (1 - age / .3f) * 4 : 0;
        cupRect.x += shake;
        NovelUI.Card(cupRect, new Color(.95f,.97f,1f,.3f), 15, NovelUI.Cream, 3);
        float height = 240f / Capacity;
        for (int i = 0; i < cup.Count; i++)
        {
            float fill = i == cup.Count - 1 ? Mathf.Clamp01(age / .3f) : 1;
            if (fill > 0) NovelUI.Fill(new Rect(cupRect.x + 15, cupRect.y + 270 - i * height - height * fill, cupRect.width - 30, height * fill), Colors[cup[i]], 3);
        }
        NovelUI.Label(new Rect(200, 510, 260, 35), "컵 " + cup.Count + "/" + Capacity, NovelUI.Soft, 18, NovelUI.Cream, TextAnchor.MiddleCenter);
        NovelUI.Card(new Rect(570, 195, 570, 300), new Color(1f,.96f,.76f), 14);
        bool visible = round < 2 || now - roundStartedAt < 3f;
        NovelUI.Label(new Rect(600, 215, 510, 45), orders[round], NovelUI.Round, 25, NovelUI.Berry);
        string note = visible ? Describe(recipes[round], round > 0) : "주문서가 사라졌어요!\n기억한 순서대로 만들어 보세요.";
        NovelUI.Label(new Rect(600, 265, 510, 180), note, NovelUI.Soft, 23, NovelUI.Berry);
        if (showingResult) NovelUI.Label(new Rect(570, 500, 570, 40), lastCorrect ? "주문대로 완성했어요!" : "주문과 달라요. 다음 주문을 확인해요.", NovelUI.Soft, 19, NovelUI.Cream);
        else NovelUI.Label(new Rect(570, 500, 570, 40), warning, NovelUI.Soft, 19, NovelUI.Cream);
        for (int i = 0; i < 4; i++)
        {
            GUI.enabled = !showingResult && cup.Count < Capacity;
            if (GUI.Button(new Rect(110 + i * 268, 560, 250, 68), Names[i], button)) Add(i);
        }
        GUI.enabled = !showingResult && cup.Count > 0;
        if (GUI.Button(new Rect(110, 655, 420, 68), "컵 비우기", button)) { cup.Clear(); warning = "컵을 비웠어요."; NovelUI.Sound?.Invoke("Back"); }
        if (GUI.Button(new Rect(750, 655, 420, 68), "제출하기", button)) Serve();
        GUI.enabled = true;
    }
    static string Describe(int[] recipe, bool ordered)
    {
        if (ordered) { string line = "순서대로 넣기\n"; for (int i = 0; i < recipe.Length; i++) line += (i == 0 ? "" : " → ") + Names[recipe[i]]; return line; }
        int[] counts = new int[4]; foreach (int ingredient in recipe) counts[ingredient]++;
        string result = "순서 무관\n"; for (int i = 0; i < counts.Length; i++) if (counts[i] > 0) result += Names[i] + " " + counts[i] + "회  "; return result;
    }
    void Add(int ingredient)
    {
        cup.Add(ingredient); pourStartedAt = Time.unscaledTime;
        int index = cup.Count - 1;
        warning = round > 0 && (index >= recipes[round].Length || recipes[round][index] != ingredient) ? "순서가 달라요. 컵을 비우고 다시 시도할 수 있어요." : "";
        NovelUI.Sound?.Invoke("Select");
    }
    static bool Matches(IReadOnlyList<int> actual, int[] expected, bool ordered)
    {
        if (actual.Count != expected.Length) return false;
        if (ordered) { for (int i = 0; i < expected.Length; i++) if (actual[i] != expected[i]) return false; return true; }
        int[] counts = new int[4]; foreach (int i in expected) counts[i]++; foreach (int i in actual) counts[i]--; foreach (int n in counts) if (n != 0) return false; return true;
    }
    void Serve()
    {
        lastCorrect = Matches(cup, recipes[round], round > 0);
        if (lastCorrect) correct++;
        if (round == 0) professorCorrect = lastCorrect;
        NovelUI.Sound?.Invoke(lastCorrect ? "Up" : "Down");
        showingResult = true; resultUntil = Time.unscaledTime + 1.8f;
    }
    void Finish() { Done = true; Delta = Mathf.Min(correct, 2); Summary = "3잔 중 " + correct + "잔 주문대로"; }
}
