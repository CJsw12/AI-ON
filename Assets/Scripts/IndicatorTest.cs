using UnityEngine;

// pH 지시약 판별: 정체 모를 용액 네 개에 지시약을 떨어뜨려 색을 보고 산성·중성·염기성으로 분류한다.
// 지시약은 모두 합쳐 6방울. BTB 한 방울이면 하나를 확정할 수 있어 언제나 풀 수 있다.
public class IndicatorTest : MiniGame
{
    static readonly string[] Indicators = { "리트머스", "페놀프탈레인", "BTB" };
    static readonly string[] Classes = { "산성", "중성", "염기성" };
    // [indicator][class] color name and color. Litmus: red / purple / blue. Phenolphthalein: clear / clear / pink. BTB: yellow / green / blue.
    static readonly string[,] ColorNames = { { "붉은색", "보라색", "푸른색" }, { "무색", "무색", "분홍색" }, { "노란색", "초록색", "파란색" } };
    static readonly Color[,] Colors = {
        { new Color(.9f, .3f, .35f), new Color(.6f, .4f, .75f), new Color(.3f, .45f, .9f) },
        { new Color(.92f, .95f, 1f), new Color(.92f, .95f, 1f), new Color(1f, .45f, .7f) },
        { new Color(1f, .85f, .25f), new Color(.4f, .8f, .45f), new Color(.25f, .45f, .95f) }
    };
    const int Tubes = 4, Drops = 6;
    const float Limit = 60f;
    enum State { Instructions, Playing, Completed }
    State state = State.Instructions;
    readonly int[] truth = new int[Tubes], guess = { -1, -1, -1, -1 }, dropped = { -1, -1, -1, -1 };
    int selected = 2, left = Drops, correct;
    float startedAt;

    public IndicatorTest(MiniGameTheme theme) : base(theme)
    {
        // One of each class plus one random, shuffled.
        truth[0] = 0; truth[1] = 1; truth[2] = 2; truth[3] = Random.Range(0, 3);
        for (int i = Tubes - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int t = truth[i]; truth[i] = truth[j]; truth[j] = t; }
    }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "분류가 많이 틀렸습니다. 지시약마다 색이 변하는 범위가 다릅니다. 표를 다시 확인하세요.", "절반 이상 맞혔습니다. 페놀프탈레인이 무색이면 산성인지 중성인지는 알 수 없다는 점을 기억하세요.", "모두 정확합니다. 근거가 되는 색을 보고 판단했습니다. 그게 분석입니다." },
        new[] { "괜찮아요! 색이 헷갈렸죠? BTB는 노랑·초록·파랑 신호등이라고 외워 봐요.", "절반은 넘겼어요! 무색 페놀프탈레인에 속은 거죠? 다음엔 안 속아요!", "완벽한 판별! 시험관들이 정체를 다 들켰네요!" }
    };

    float Left => Mathf.Max(0, Limit - (Now - startedAt));
    bool AllGuessed { get { foreach (int g in guess) if (g < 0) return false; return true; } }

    void Finish()
    {
        state = State.Completed; Done = true;
        correct = 0;
        for (int i = 0; i < Tubes; i++) if (guess[i] == truth[i]) correct++;
        Delta = correct == Tubes ? 2 : correct >= 2 ? 1 : 0;
        Summary = "용액 " + Tubes + "개 중 " + correct + "개 판별 · 남은 지시약 " + left + "방울";
        Judge(correct == Tubes, new Vector2(640, 260), correct == Tubes ? "모두 정답!" : correct + "개 정답");
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Playing && Left <= 0) Finish();
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Mint, 2);
        string head = "pH TEST  ·  " + (Done ? "완료" : "지시약 " + left + "/" + Drops + "방울");
        if (state == State.Playing) head += "  ·  남은 시간 " + Mathf.CeilToInt(Real(Left)) + "초";
        NovelUI.Label(new Rect(160, 168, 950, 35), head, NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 230, 930, 300), "정체 모를 용액 네 개를 산성·중성·염기성으로 나누세요.\n위에서 지시약을 고르고 시험관을 누르면 한 방울 떨어져 색이 변해요.\n리트머스: 산성 붉은색 · 중성 보라색 · 염기성 푸른색\n페놀프탈레인: 염기성에서만 분홍색 (산성·중성은 무색)\nBTB: 산성 노란색 · 중성 초록색 · 염기성 파란색\n지시약은 모두 " + Drops + "방울, 제한 시간은 " + Mathf.CeilToInt(Real(Limit)) + "초예요.", NovelUI.Soft, 22, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 580, 400, 75), "시작하기", button)) { startedAt = Now; state = State.Playing; }
            return;
        }
        bool play = state == State.Playing;

        // Indicator picker.
        if (play)
            for (int k = 0; k < 3; k++)
                if (NovelUI.Button(new Rect(170 + k * 250, 210, 230, 50), Indicators[k], selected == k, 20)) { selected = k; NovelUI.Sound?.Invoke("Select"); }

        for (int i = 0; i < Tubes; i++)
        {
            float x = 200 + i * 230;
            Rect tube = new Rect(x + 50, 285, 90, 190);
            NovelUI.Card(new Rect(tube.x - 6, tube.y - 6, tube.width + 12, tube.height + 12), NovelUI.Blush, 22);
            Color liquid = dropped[i] < 0 ? new Color(.92f, .95f, 1f) : Colors[dropped[i], truth[i]];
            NovelUI.Fill(new Rect(tube.x + 8, tube.y + 70, tube.width - 16, tube.height - 80), liquid, 18);
            NovelUI.Label(new Rect(x, 255, 190, 30), "용액 " + (char)('A' + i), NovelUI.Round, 20, NovelUI.Berry, TextAnchor.MiddleCenter);
            string seen = dropped[i] < 0 ? "아직 안 넣음" : Indicators[dropped[i]] + " → " + ColorNames[dropped[i], truth[i]];
            NovelUI.Label(new Rect(x - 15, 485, 220, 30), seen, NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (play && left > 0 && NovelUI.Hover(tube)) NovelUI.Ring(tube, NovelUI.DeepPink, 18, 3);
            if (play && left > 0 && GUI.Button(tube, GUIContent.none, GUIStyle.none))
            {
                dropped[i] = selected; left--;
                NovelUI.Sound?.Invoke("Select");
                Popup(ColorNames[selected, truth[i]], new Vector2(tube.center.x, tube.y), NovelUI.Plum);
            }
            for (int c = 0; c < 3; c++)
            {
                Rect r = new Rect(x - 5 + c * 67, 525, 63, 40);
                bool on = guess[i] == c;
                if (!play)
                {
                    bool right = truth[i] == c;
                    if (on || right) NovelUI.Fill(r, right ? NovelUI.Mint : NovelUI.DeepPink, 12);
                    NovelUI.Label(r, Classes[c], NovelUI.Round, 16, on || right ? Color.white : NovelUI.Plum, TextAnchor.MiddleCenter);
                    continue;
                }
                if (NovelUI.Button(r, Classes[c], on, 16)) { guess[i] = c; NovelUI.Sound?.Invoke("Select"); }
            }
        }
        if (play)
        {
            NovelUI.Label(new Rect(170, 605, 600, 60), left == 0 ? "지시약을 다 썼어요. 본 색으로 분류를 마치세요." : "BTB 한 방울이면 세 가지를 한 번에 가를 수 있어요.", NovelUI.Soft, 20, NovelUI.Plum);
            GUI.enabled = AllGuessed;
            if (NovelUI.Button(new Rect(810, 600, 300, 60), "판별 제출", true, 22)) Finish();
            GUI.enabled = true;
        }
        else NovelUI.Label(new Rect(170, 600, 780, 60), Summary, NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
    }
    protected override bool ShowStars => false;
}
