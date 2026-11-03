using System.Collections.Generic;
using UnityEngine;

// Three short orders: the professor's usual drink first, then two random ones for the classmate.
public class CoffeeOrder : MiniGame
{
    const float Duration = 60f;
    const int Capacity = 8;
    static readonly string[] Names = { "샷", "물", "우유", "시럽" };
    static readonly Color[] Colors = { new Color(.36f,.16f,.1f), new Color(.55f,.8f,.95f), new Color(1f,.94f,.8f), new Color(.84f,.55f,.2f) };
    readonly string[] orders;
    readonly int[][] recipes;
    readonly List<int> cup = new List<int>();
    readonly float endsAt = Now + Duration;
    int round, correct;
    float roundStartedAt, pourStartedAt = -10, resultUntil;
    bool showingResult, lastCorrect;
    string warning = "";

    public CoffeeOrder(int professor, MiniGameTheme theme, bool practice = false) : base(theme)
    {
        orders = new[] { professor == 0 ? "교수님 · 진한 아메리카노" : "교수님 · 바닐라 라떼", "동기 · 두 번째 주문", "동기 · 마지막 주문" };
        // Kim: two shots and two waters. Lee: a shot, two milks and syrup.
        recipes = new[] { professor == 0 ? new[] { 0, 0, 1, 1 } : new[] { 0, 2, 2, 3 }, MakeRecipe(professor, 1, practice), MakeRecipe(professor, 2, practice) };
        roundStartedAt = Now;
    }
    static int[] MakeRecipe(int professor, int round, bool practice)
    {
        int length = round == 1 ? Random.Range(4, 6) : 6;
        int[] recipe = new int[length];
        for (int i = 0; i < recipe.Length; i++) recipe[i] = Random.Range(0, Names.Length);
        string key = "AION.Coffee.LastOrder." + professor + "." + round;
        string previous = PlayerPrefs.GetString(key, "");
        string current = string.Join(",", recipe);
        if (current == previous)
        {
            recipe[recipe.Length - 1] = (recipe[recipe.Length - 1] + Random.Range(1, Names.Length)) % Names.Length;
            current = string.Join(",", recipe);
        }
        if (practice) return recipe;   // Practice must not change the story's "not the same as last time" record.
        PlayerPrefs.SetString(key, current);
        PlayerPrefs.Save();
        return recipe;
    }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "주문을 다시 확인해 보세요. 요구 사항을 확인하는 과정이 중요합니다.", "잘한 부분이 있습니다. 다음에는 순서도 확인하세요.", "주문을 꼼꼼히 확인했군요. 요구 사항을 지키는 것도 실력입니다." },
        new[] { "괜찮아요! 다음에는 주문을 같이 확인해 봐요.", "한 잔은 잘 만들었네요. 다음에도 함께 해봐요!", "완벽한 바리스타네요! 덕분에 힘이 나요." }
    };
    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        float now = Now;
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
        cup.Add(ingredient); pourStartedAt = Now;
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
        NovelUI.Sound?.Invoke(lastCorrect ? "Up" : "Down");
        showingResult = true; resultUntil = Now + 1.8f;
        if (!lastCorrect) ExplainFailure(OrderDifference(), "주문을 다시 확인해요:\n" + Describe(recipes[round], round > 0) + "\n" + (round == 0 ? "순서는 상관없지만 재료별 횟수와 총량이 모두 같아야 해요." : "수량뿐 아니라 처음부터 끝까지 순서도 같아야 해요. 잘못 넣으면 컵을 비우고 다시 담아요."));
    }
    string OrderDifference()
    {
        int[] expected = recipes[round];
        string reason = orders[round] + "\n담은 양 " + cup.Count + "회 / 주문량 " + expected.Length + "회\n";
        if (round == 0)
        {
            for (int ingredient = 0; ingredient < Names.Length; ingredient++)
            {
                int needed = 0, actual = 0;
                foreach (int x in expected) if (x == ingredient) needed++;
                foreach (int x in cup) if (x == ingredient) actual++;
                if (needed != actual) reason += Names[ingredient] + " " + actual + "/" + needed + "회(담음/주문)  ";
            }
        }
        else
        {
            int i = 0;
            while (i < cup.Count && i < expected.Length && cup[i] == expected[i]) i++;
            reason += i >= expected.Length ? "주문에 없는 재료를 더 넣었어요." : i >= cup.Count ? (i + 1) + "번째부터 재료를 덜 넣었어요." : "처음 다른 위치: " + (i + 1) + "번째 / 내 재료 " + Names[cup[i]] + " / 주문 " + Names[expected[i]];
        }
        return reason;
    }
    void Finish()
    {
        Done = true; Delta = Mathf.Min(correct, 2); Summary = "3잔 중 " + correct + "잔 주문대로";
        int unfinished = showingResult ? round + 1 : round;
        if (unfinished < recipes.Length && Now >= endsAt)
            ExplainFailure("전체 제한 시간 60초가 끝나 " + (unfinished + 1) + "번째 주문부터 제출을 마치지 못했어요.\n제출해서 맞힌 잔은 " + correct + "잔이에요.",
                "다음에 만들 차례였던 주문:\n" + Describe(recipes[unfinished], unfinished > 0) + "\n주문을 먼저 확인하고 필요한 만큼만 담아 제출해요. 컵만 채우고 제출하지 않으면 완료되지 않아요.");
    }
}
