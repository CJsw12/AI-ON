using UnityEngine;

// Match each term from the first lecture with its meaning; the faculty's theme can bring its own six pairs.
public class CardMatch : MiniGame
{
    static readonly string[,] Pairs = {
        { "변수", "값을 담아 두는 이름표" }, { "반복문", "같은 일을 여러 번 실행" }, { "조건문", "참·거짓에 따라 다른 길" },
        { "자료구조", "데이터를 정리하는 방법" }, { "알고리즘", "문제를 푸는 순서" }, { "Git", "코드 변경 기록 관리" }
    };
    readonly string[] faces = new string[12];
    readonly int[] pairOf = new int[12];
    readonly bool[] isTerm = new bool[12];
    readonly bool[] matched = new bool[12];
    int first = -1, second = -1, mistakes, found;
    float hideAt;
    float startedAt, shuffleAt;
    bool preview = true, shuffling;
    Texture2D cardBack;
    GUIStyle hiddenButton;

    protected override bool ShowStars => false;
    public CardMatch(MiniGameTheme theme) : base(theme)
    {
        string[,] pairs = theme.Pairs ?? Pairs;
        for (int i = 0; i < 12; i++) { pairOf[i] = i / 2; isTerm[i] = i % 2 == 0; faces[i] = pairs[i / 2, i % 2]; }
    }

    void Shuffle()
    {
        for (int i = 11; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (faces[i], faces[j]) = (faces[j], faces[i]);
            (pairOf[i], pairOf[j]) = (pairOf[j], pairOf[i]);
            (isTerm[i], isTerm[j]) = (isTerm[j], isTerm[i]);
        }
    }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "용어가 아직 섞여 있습니다. 정의만 외우지 말고 예제 코드에서 직접 찾아보세요.", "대부분 맞혔군요. 헷갈린 용어는 예제와 함께 다시 정리하세요.", "다음 수업 퀴즈에서 용어를 정확히 구분하더군요. 정의를 말로 설명할 수 있으면 코드도 읽힙니다." },
        new[] { "디저트 샀다면서요? 괜찮아요, 그만큼 확실히 기억나겠죠. 다음엔 이길 거예요!", "카드 게임으로 복습이라니 좋네요! 헷갈린 건 다음 수업 때 저한테 물어봐요.", "학식에서 용어 카드 했다면서요? 동기가 디저트 사게 됐다고 하던데요!" }
    };

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (cardBack == null) cardBack = Resources.Load<Texture2D>("CardMatch/DTTCardBack");
        if (hiddenButton == null) hiddenButton = new GUIStyle(GUIStyle.none);
        if (shuffling && Now - shuffleAt >= 1.8f)
        {
            Shuffle();
            shuffling = false;
            startedAt = Now;
        }
        bool playing = !preview && !shuffling;
        float remaining = playing ? Mathf.Max(0, 60f - (Now - startedAt)) : 60f;
        if (playing && !Done && remaining <= 0) Finish();
        if (second >= 0 && Now >= hideAt) first = second = -1;
        string instruction = preview ? "짝 번호를 보며 용어와 뜻을 익혀 보세요." : shuffling ? "카드를 뒤집어 섞고 있습니다..." : "같은 개념의 용어와 뜻을 짝지으세요.   틀린 횟수 " + mistakes + "   ·   찾은 짝 " + found + "/6";
        GUI.Label(new Rect(100, 120, 1080, 35), instruction, body);
        if (preview && GUI.Button(new Rect(450, 715, 380, 50), "준비 완료", button))
        {
            preview = false;
            shuffling = true;
            shuffleAt = Now;
        }
        Color timerColor = remaining < 10 ? Color.Lerp(NovelUI.Pink, Color.red, .5f + .5f * Mathf.Sin(Now * 5)) : NovelUI.Mint;
        // Timer sits at the right end of the instruction row, clear of the longest instruction.
        NovelUI.Fill(new Rect(970, 132, 200, 12), NovelUI.Blush, 6);
        if (remaining > 0) NovelUI.Fill(new Rect(970, 132, 200 * remaining / 60f, 12), timerColor, 6);
        GUI.Label(new Rect(790, 120, 165, 35), playing ? "남은 시간 " + Mathf.CeilToInt(remaining) + "초" : "시작 전", NovelUI.Text(NovelUI.Soft, 18, timerColor, TextAnchor.MiddleRight));
        for (int i = 0; i < 12; i++)
        {
            Rect rect = new Rect(110 + (i % 4) * 268, 175 + (i / 4) * 175, 250, 155);
            if (preview || matched[i] || i == first || i == second)
            {
                NovelUI.Card(rect, matched[i] ? NovelUI.Mint : NovelUI.Cream, 18);
                GUI.Label(new Rect(rect.x + 8, rect.y + 15, rect.width - 16, 25), (preview ? "짝 " + (pairOf[i] + 1) + " · " : "") + (isTerm[i] ? "전공 용어" : "뜻과 쓰임"), NovelUI.Text(NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(rect.x + 10, rect.y + 45, rect.width - 20, rect.height - 55), faces[i], NovelUI.Text(NovelUI.Soft, isTerm[i] ? 26 : 19, NovelUI.Berry, TextAnchor.MiddleCenter, isTerm[i] ? FontStyle.Bold : FontStyle.Normal));
            }
            else
            {
                bool hover = playing && rect.Contains(Event.current.mousePosition) && !Done && second < 0;
                Rect visual = new Rect(rect.x, rect.y - (hover ? 4 : 0), rect.width, rect.height);
                if (shuffling)
                {
                    float elapsed = Now - shuffleAt;
                    float gather = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .5f));
                    float spread = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 1.2f) / .6f));
                    float stack = gather * (1 - spread);
                    float orbit = Mathf.Sin(Mathf.Clamp01((elapsed - .5f) / .7f) * Mathf.PI);
                    Vector2 center = new Vector2(515 + (i % 4 - 1.5f) * 12, 350 + (i / 4 - 1) * 10);
                    visual.position = Vector2.Lerp(rect.position, center, stack) + new Vector2(Mathf.Sin(i * 2.1f + elapsed * 12) * 35, Mathf.Cos(i * 2.1f + elapsed * 12) * 25) * orbit;
                }
                NovelUI.Card(visual, new Color(.46f, .43f, .43f), 18, hover ? NovelUI.Pink : NovelUI.Blush, 2);
                if (cardBack != null && Event.current.type == EventType.Repaint) { GUI.color = new Color(1, 1, 1, .15f); GUI.DrawTexture(visual, cardBack, ScaleMode.ScaleToFit); GUI.color = Color.white; }
                GUI.Label(visual, "‹ / ›", NovelUI.Text(NovelUI.Round, 30, NovelUI.Cream, TextAnchor.MiddleCenter, FontStyle.Bold));
                if (GUI.Button(rect, GUIContent.none, hiddenButton) && playing && !Done && second < 0) Flip(i);
            }
        }
    }
    void Flip(int card)
    {
        if (first < 0) { first = card; return; }
        if (pairOf[first] == pairOf[card])
        {
            matched[first] = matched[card] = true; first = -1;
            if (++found == 6) Finish();
            return;
        }
        second = card; mistakes++; hideAt = Now + .8f;
    }
    void Finish()
    {
        Done = true;
        Delta = found == 6 ? (mistakes <= 3 ? 2 : mistakes <= 7 ? 1 : 0) : found >= 4 ? 1 : 0;   // Most pairs before time out still counts.
        Summary = (found == 6 ? "6쌍 완성" : "시간 종료 · " + found + "/6쌍") + " · 틀린 횟수 " + mistakes;
    }
}
