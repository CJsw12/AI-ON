using UnityEngine;

// 학식 식판 짜기: 반찬 세 가지를 골라 상황에 맞는 탄수화물·단백질·지방 범위를 맞춘다.
// 세 끼, 끼니마다 목표가 다르다. 세 칸이 모두 범위 안이면 성공.
public class PlateBalance : MiniGame
{
    class Dish { public string Name; public int C, P, F; }
    static readonly Dish[] Dishes = {
        new Dish { Name = "흰쌀밥", C = 3 }, new Dish { Name = "잡곡밥", C = 3, P = 1 }, new Dish { Name = "닭가슴살", P = 3 },
        new Dish { Name = "제육볶음", C = 1, P = 2, F = 2 }, new Dish { Name = "계란말이", P = 2, F = 1 }, new Dish { Name = "돈가스", C = 2, P = 2, F = 3 },
        new Dish { Name = "나물무침", C = 1 }, new Dish { Name = "바나나", C = 2 }, new Dish { Name = "두부조림", P = 2, F = 1 }, new Dish { Name = "모둠튀김", C = 1, F = 3 }
    };
    class Meal { public string Note; public int[] Min, Max; }
    static readonly Meal[] Meals = {
        new Meal { Note = "공연 두 시간 전 · 힘은 내되 속은 가볍게", Min = new[] { 4, 1, 0 }, Max = new[] { 6, 3, 1 } },
        new Meal { Note = "근력 훈련 직후 · 근육 회복이 먼저", Min = new[] { 3, 5, 0 }, Max = new[] { 4, 7, 2 } },
        new Meal { Note = "주말 회복 식사 · 골고루 든든하게", Min = new[] { 3, 3, 1 }, Max = new[] { 5, 5, 3 } }
    };
    static readonly string[] Labels = { "탄수화물", "단백질", "지방" };
    enum State { Instructions, Picking, Feedback, Completed }
    State state = State.Instructions;
    readonly System.Collections.Generic.List<int> plate = new System.Collections.Generic.List<int>();
    int meal, passed;
    float stateAt;
    bool lastOk;

    public PlateBalance(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "식판이 상황과 맞지 않았습니다. 무엇을 하기 전인지, 후인지부터 생각하세요.", "일부는 맞았습니다. 단백질과 지방이 같이 오는 반찬을 조심하세요.", "정확합니다. 몸 상태에 맞춰 먹는 것도 훈련입니다." },
        new[] { "식판이 너무 자유로웠어요! 공연 전 돈가스는 무대에서 꾸벅해요.", "반은 영양사급! 나머지도 금방 감 잡을 거예요.", "식판 마스터! 동기가 반찬 다 넘겨줬대요!" }
    };

    int Sum(int k) { int s = 0; foreach (int i in plate) s += k == 0 ? Dishes[i].C : k == 1 ? Dishes[i].P : Dishes[i].F; return s; }
    void Serve()
    {
        lastOk = true;
        for (int k = 0; k < 3; k++) if (Sum(k) < Meals[meal].Min[k] || Sum(k) > Meals[meal].Max[k]) lastOk = false;
        if (lastOk) passed++;
        state = State.Feedback; stateAt = Now;
        Judge(lastOk, new Vector2(950, 260), lastOk ? "완벽한 식판!" : "균형이 깨졌어요");
        if (!lastOk)
        {
            Meal m = Meals[meal];
            string comparison = "";
            for (int k = 0; k < 3; k++) comparison += Labels[k] + ": 내 식판 " + Sum(k) + " / 목표 " + m.Min[k] + "~" + m.Max[k] + "\n";
            ExplainFailure(m.Note + "\n" + Missed(), comparison + Solution() + "\n다른 조합도 세 범위를 모두 맞추면 정답이에요. 숫자는 게임용 영양 점수예요.");
        }
    }
    string Solution()
    {
        Meal m = Meals[meal];
        for (int a = 0; a < Dishes.Length - 2; a++)
            for (int b = a + 1; b < Dishes.Length - 1; b++)
                for (int c = b + 1; c < Dishes.Length; c++)
                {
                    Dish x = Dishes[a], y = Dishes[b], z = Dishes[c];
                    int[] sums = { x.C + y.C + z.C, x.P + y.P + z.P, x.F + y.F + z.F };
                    bool valid = true;
                    for (int k = 0; k < 3; k++) if (sums[k] < m.Min[k] || sums[k] > m.Max[k]) valid = false;
                    if (valid) return "성공 조합 예시: " + x.Name + " + " + y.Name + " + " + z.Name;
                }
        return "목표 범위와 음식별 점수를 비교해 조합을 바꿔 보세요.";
    }

    string Missed()
    {
        string s = "";
        for (int k = 0; k < 3; k++)
            if (Sum(k) < Meals[meal].Min[k]) s += Labels[k] + " 부족 ";
            else if (Sum(k) > Meals[meal].Max[k]) s += Labels[k] + " 너무 많음 ";
        return s.Trim() + " · 초록 범위를 확인해요.";
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Feedback && Now - stateAt >= 2.5f)
        {
            plate.Clear();
            if (++meal >= Meals.Length) { state = State.Completed; Done = true; Delta = passed == 3 ? 2 : passed > 0 ? 1 : 0; Summary = "세 끼 중 " + passed + "끼 식판 성공"; }
            else state = State.Picking;
        }
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Mint, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), "MEAL TRAY  ·  " + (Done ? "완료" : (Mathf.Min(meal, 2) + 1) + "/3끼") + "  ·  성공 " + passed, NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 220), "오늘 할 일에 맞게 반찬 세 가지를 식판에 담으세요.\n반찬 아래 숫자는 탄(탄수화물)·단(단백질)·지(지방) 양이에요.\n오른쪽 막대가 초록 범위 안에 모두 들어가면 성공이에요.\n반찬을 다시 누르면 식판에서 빠져요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) state = State.Picking;
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 330, 940, 100), Summary, NovelUI.Round, 40, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        Meal m = Meals[meal];
        NovelUI.Label(new Rect(170, 210, 940, 40), m.Note, NovelUI.Round, 24, NovelUI.Plum);
        for (int i = 0; i < Dishes.Length; i++)
        {
            Rect r = new Rect(170 + i % 5 * 120, 265 + i / 5 * 125, 110, 110);
            bool on = plate.Contains(i);
            NovelUI.Card(r, on ? NovelUI.Pink : NovelUI.Hover(r) && state == State.Picking ? NovelUI.Lemon : NovelUI.Blush, 16);
            NovelUI.Label(new Rect(r.x, r.y + 12, r.width, 40), Dishes[i].Name, NovelUI.Round, 19, on ? Color.white : NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(r.x, r.y + 55, r.width, 40), "탄" + Dishes[i].C + " 단" + Dishes[i].P + " 지" + Dishes[i].F, NovelUI.Soft, 16, on ? Color.white : NovelUI.Plum, TextAnchor.MiddleCenter);
            if (state == State.Picking && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                if (on) plate.Remove(i); else if (plate.Count < 3) plate.Add(i);
                NovelUI.Sound?.Invoke("Select");
            }
        }
        // Nutrient bars: each unit is 40px, the green band is the goal range.
        for (int k = 0; k < 3; k++)
        {
            float y = 280 + k * 75;
            NovelUI.Label(new Rect(790, y - 30, 200, 28), Labels[k] + " " + Sum(k), NovelUI.Round, 18, NovelUI.Berry);
            Box(new Rect(790, y, 320, 22), NovelUI.Alpha(NovelUI.Plum, .15f));
            Box(new Rect(790 + m.Min[k] * 40, y, Mathf.Max(6, (m.Max[k] - m.Min[k]) * 40), 22), NovelUI.Alpha(NovelUI.Mint, .6f));
            Box(new Rect(790, y + 8, Mathf.Min(320, Sum(k) * 40), 6), NovelUI.DeepPink);
        }
        if (state == State.Picking)
        {
            NovelUI.Label(new Rect(170, 530, 600, 40), "식판 " + plate.Count + "/3", NovelUI.Soft, 21, NovelUI.Plum);
            if (plate.Count == 3 && NovelUI.Button(new Rect(810, 600, 300, 60), "배식 받기", true, 22)) Serve();
        }
        else NovelUI.Label(new Rect(170, 600, 940, 60), lastOk ? "완벽한 식판! 몸이 고마워해요." : Missed(), NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
    }
}
