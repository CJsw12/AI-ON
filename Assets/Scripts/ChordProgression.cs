using UnityEngine;

// 코드 진행 완성: 네 마디 진행의 빈칸에 어울리는 다이어토닉 코드를 고른다.
// 다섯 곡, 빈칸은 모두 여덟 개. 틀려도 정답을 보여 주고 다음으로 넘어간다.
public class ChordProgression : MiniGame
{
    class Song { public string Key, Name, Hint; public string[] Bars; public int[] Blanks; public string[][] Wrong; public string[] Why; }
    static readonly Song[] Songs = {
        new Song { Key = "C장조", Name = "I – V – vi – IV · 팝 진행", Bars = new[] { "C", "G", "Am", "F" },
            Hint = "C장조 다이어토닉: C  Dm  Em  F  G  Am  Bdim",
            Blanks = new[] { 2 }, Wrong = new[] { new[] { "A", "E♭" } },
            Why = new[] { "vi는 6번째 음 라에서 시작하는 단화음 Am이에요." } },
        new Song { Key = "G장조", Name = "ii – V – I · 재즈의 기본", Bars = new[] { "Am", "D", "G", "G" },
            Hint = "G장조 다이어토닉: G  Am  Bm  C  D  Em  F#dim",
            Blanks = new[] { 1 }, Wrong = new[] { new[] { "Dm", "F" } },
            Why = new[] { "G장조의 V는 D(레·파#·라) 장화음이에요." } },
        new Song { Key = "C장조", Name = "vi – ii – V – I · 4도 진행", Bars = new[] { "Am", "Dm", "G", "C" },
            Hint = "4도 진행: 근음이 4도씩 올라가요 (라 → 레 → 솔 → 도)",
            Blanks = new[] { 1, 3 }, Wrong = new[] { new[] { "D", "E" }, new[] { "Cm", "B♭" } },
            Why = new[] { "라에서 4도 위는 레, C장조의 ii는 Dm이에요.", "솔에서 4도 위는 도, 으뜸화음 C로 끝나요." } },
        new Song { Key = "F장조", Name = "I – IV – V – I · 기본 세 코드", Bars = new[] { "F", "B♭", "C", "F" },
            Hint = "F장조 다이어토닉: F  Gm  Am  B♭  C  Dm  Edim",
            Blanks = new[] { 1, 2 }, Wrong = new[] { new[] { "B", "E" }, new[] { "Cm", "D" } },
            Why = new[] { "F장조의 IV는 시♭에서 시작하는 B♭이에요.", "F장조의 V는 C(도·미·솔)예요." } },
        new Song { Key = "D장조", Name = "I – vi – IV – V · 50년대 진행", Bars = new[] { "D", "Bm", "G", "A" },
            Hint = "D장조 다이어토닉: D  Em  F#m  G  A  Bm  C#dim",
            Blanks = new[] { 1, 3 }, Wrong = new[] { new[] { "B", "B♭m" }, new[] { "Am", "A♭" } },
            Why = new[] { "D장조의 vi는 Bm(시·레·파#) 단화음이에요.", "D장조의 V는 A(라·도#·미) 장화음이에요." } }
    };
    const int Total = 8;
    enum State { Instructions, Asking, Feedback, Completed }
    State state = State.Instructions;
    int song, blank, correct, picked = -1;
    string[] options;
    int answer;

    public ChordProgression(MiniGameTheme theme) : base(theme) { }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "코드를 감으로 골랐습니다. 조성의 다이어토닉 코드부터 적어 두고 시작하세요.", "절반은 맞았습니다. 장화음과 단화음이 몇 번째 자리에 오는지 다시 확인하세요.", "정확합니다. 이 정도면 자작곡 코드 진행을 직접 짤 수 있습니다." },
        new[] { "괜찮아요! 코드는 외우는 것보다 쳐 보는 게 빨라요. 같이 쳐 봐요.", "반은 맞았어요! vi가 왜 살짝 슬픈지, 들어 보면 바로 알죠!", "완벽해요! 이제 후렴 코드는 둘이 직접 짜도 되겠어요!" }
    };

    void Prepare()
    {
        Song s = Songs[song];
        string right = s.Bars[s.Blanks[blank]];
        string[] wrong = s.Wrong[blank];
        answer = Random.Range(0, 3);
        options = new string[3];
        options[answer] = right;
        for (int i = 0, w = 0; i < 3; i++) if (i != answer) options[i] = wrong[w++];
        picked = -1;
        state = State.Asking;
    }

    void Pick(int i)
    {
        picked = i;
        bool good = i == answer;
        if (good) correct++;
        state = State.Feedback;
        Judge(good, new Vector2(640, 330), good ? "딱 어울려요!" : "정답은 " + options[answer]);
    }

    void Next()
    {
        if (++blank >= Songs[song].Blanks.Length) { blank = 0; song++; }
        if (song >= Songs.Length)
        {
            state = State.Completed; Done = true;
            Delta = correct >= 7 ? 2 : correct >= 4 ? 1 : 0;
            Summary = "빈칸 " + Total + "개 중 " + correct + "개 정답";
            if (Delta == 0)
            {
                ExplainFailure(
                    "빈칸 8개 중 맞힌 개수가 " + correct + "개로 기준(4개 이상)에 미치지 못했어요.\n조성(Key)에 따른 다이어토닉 화음 구조를 파악하는 것이 필요해요.",
                    "다이어토닉 코드(Diatonic Chord) 가이드:\n1. 로마 숫자 표기: I, IV, V는 장화음(Major, 대문자) / ii, iii, vi는 단화음(Minor, 소문자m) / vii°는 감화음(dim)\n2. C장조 기준: I=C, ii=Dm, iii=Em, IV=F, V=G, vi=Am, vii°=Bdim\n3. 4도 상행 진행: 근음이 4도씩 위로 이동하며 진행 (라 Am → 레 Dm → 솔 G → 도 C)"
                );
            }
        }
        else Prepare();
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Sky, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), "CHORD CHART  ·  " + (Done ? "완료" : (Mathf.Min(song, Songs.Length - 1) + 1) + "/" + Songs.Length + "곡") + "  ·  정답 " + correct, NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 240, 930, 260), "네 마디 코드 진행에 빈칸이 있어요.\n위에 적힌 조성과 진행 이름(I–V–vi–IV, ii–V–I 등)을 보고\n빈칸에 들어갈 코드를 세 개 중에서 고르세요.\n로마 숫자는 조성의 몇 번째 음에서 시작하는 화음인지 뜻해요.\n대문자는 장화음, 소문자는 단화음이에요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) Prepare();
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 330, 940, 100), Summary, NovelUI.Round, 40, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        Song s = Songs[song];
        NovelUI.Label(new Rect(170, 210, 940, 40), s.Key + "  ·  " + s.Name, NovelUI.Round, 26, NovelUI.Plum);
        NovelUI.Label(new Rect(170, 250, 940, 32), s.Hint, NovelUI.Soft, 19, NovelUI.Plum);
        int now = s.Blanks[blank];
        for (int b = 0; b < 4; b++)
        {
            Rect r = new Rect(190 + b * 230, 300, 210, 120);
            int k = System.Array.IndexOf(s.Blanks, b);
            bool hidden = k > blank || (k == blank && state == State.Asking);
            bool current = b == now;
            NovelUI.Card(r, current ? NovelUI.Blush : Color.white, 16, current ? NovelUI.Pink : NovelUI.Alpha(NovelUI.Plum, .2f), 2);
            NovelUI.Label(new Rect(r.x, r.y + 6, r.width, 26), (b + 1) + "마디", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(r.x, r.y + 34, r.width, 70), hidden ? "?" : s.Bars[b], NovelUI.Round, 44, current ? NovelUI.DeepPink : NovelUI.Berry, TextAnchor.MiddleCenter);
        }
        for (int i = 0; i < 3; i++)
        {
            Rect r = new Rect(250 + i * 270, 460, 240, 90);
            Color c = state == State.Feedback && i == answer ? NovelUI.Mint : state == State.Feedback && i == picked ? NovelUI.Alpha(NovelUI.DeepPink, .5f) : NovelUI.Hover(r) && state == State.Asking ? NovelUI.Lemon : NovelUI.Blush;
            NovelUI.Card(r, c, 16);
            NovelUI.Label(r, options[i], NovelUI.Round, 36, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (state == State.Asking && GUI.Button(r, GUIContent.none, GUIStyle.none)) { NovelUI.Sound?.Invoke("Select"); Pick(i); }
        }
        string line = state == State.Asking ? (now + 1) + "마디에 어울리는 코드를 골라요." : s.Why[blank];
        NovelUI.Label(new Rect(170, 575, 940, 60), line, NovelUI.Round, 24, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (state == State.Feedback && GUI.Button(new Rect(440, 655, 400, 65), song == Songs.Length - 1 && blank == Songs[song].Blanks.Length - 1 ? "결과 보기" : "다음 문제", button)) Next();
    }
}
