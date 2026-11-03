using UnityEngine;
using System.Collections.Generic;

// DNA 염기쌍 잇기: 주형 가닥의 염기마다 상보적인 염기를 입력한다.
// 1라운드는 DNA 복제(A-T, G-C), 2라운드는 mRNA 전사(A→U, T→A, G→C, C→G). 라운드마다 10개, 제한 시간 25초.
// 틀리면 다음 칸으로 넘어가지 않으므로 시간 안에 언제든 다시 맞힐 수 있다.
public class BasePairing : MiniGame
{
    const int Length = 10;
    const float RoundTime = 25f;
    enum State { Instructions, Playing, Feedback, Completed }
    State state = State.Instructions;
    readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    char[] strand;
    readonly List<char> typed = new List<char>();
    int round, firstTry, mistakes, totalDone;
    bool missedHere;
    float stateAt, usedTime;
    string lastText = "";

    public BasePairing(MiniGameTheme theme) : base(theme) { }

    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "짝이 많이 틀렸습니다. A는 T, G는 C. RNA에는 T 대신 U가 들어갑니다.", "대부분 맞았습니다. 속도보다 정확도가 먼저입니다.", "정확하고 빠릅니다. 세포가 하는 일을 그대로 해냈습니다." },
        new[] { "앗, 염기들이 짝꿍을 잃었어요! 다시 하면 금방이에요.", "거의 다 짝꿍 성공! U가 살짝 헷갈렸죠?", "염기쌍 마스터! 복제 효소도 울고 가겠어요!" }
    };

    bool Rna => round == 1;
    char Pair(char b) => b == 'A' ? (Rna ? 'U' : 'T') : b == 'T' ? 'A' : b == 'G' ? 'C' : 'G';
    string[] Keys => Rna ? new[] { "A", "U", "G", "C" } : new[] { "A", "T", "G", "C" };

    void StartRound()
    {
        const string bases = "ATGC";
        strand = new char[Length];
        for (int i = 0; i < Length; i++) strand[i] = bases[Random.Range(0, 4)];
        typed.Clear(); missedHere = false;
        state = State.Playing; stateAt = Now;
    }

    void EndRound(bool timeout)
    {
        usedTime += Mathf.Min(RoundTime, Now - stateAt);
        totalDone += typed.Count;
        lastText = timeout ? "시간 초과 · " + typed.Count + "/" + Length + "개 연결" : (Rna ? "mRNA 전사 완료!" : "DNA 복제 완료!");
        if (!timeout) Burst(new Vector2(640, 420));
        state = State.Feedback; stateAt = Now;
    }

    void Enter(char b)
    {
        if (state != State.Playing) return;
        int i = typed.Count;
        Vector2 at = new Vector2(Slot(i) + 30, 470);
        if (b == Pair(strand[i]))
        {
            if (!missedHere) firstTry++;
            typed.Add(b); missedHere = false;
            NovelUI.Sound?.Invoke("Select");
            Popup(strand[i] + "-" + b, at, NovelUI.Mint);
            if (typed.Count >= Length) EndRound(false);
        }
        else
        {
            mistakes++; missedHere = true;
            Judge(false, at, b == 'T' && Rna ? "RNA에는 U!" : "짝이 아니에요");
        }
    }

    void HandleKeys()
    {
        Event e = Event.current;
        // KeyUps that arrive outside this state (feedback screen, open menu) are missed; drop keys no longer down.
        if (NovelUI.Repaint) held.RemoveWhere(k => !Input.GetKey(k));
        if (e.type == EventType.KeyUp) { held.Remove(e.keyCode); return; }
        if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
        if (!held.Add(e.keyCode)) { e.Use(); return; }   // ignore key repeat
        char b;
        switch (e.keyCode)
        {
            case KeyCode.A: b = 'A'; break;
            case KeyCode.T: b = 'T'; break;
            case KeyCode.G: b = 'G'; break;
            case KeyCode.C: b = 'C'; break;
            case KeyCode.U: b = 'U'; break;
            default: return;
        }
        e.Use();
        Enter(b);
    }

    static float Slot(int i) => 175 + i * 93;

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Playing && Now - stateAt >= RoundTime) EndRound(true);
        if (state == State.Feedback && Now - stateAt >= 2f)
        {
            if (++round >= 2)
            {
                state = State.Completed; Done = true;
                int total = Length * 2;
                // Accuracy first, speed as a bonus for the top grade.
                Delta = totalDone == total && firstTry >= 18 && usedTime <= 36f ? 2 : firstTry >= 12 ? 1 : 0;
                Summary = "첫 시도 정답 " + firstTry + "/" + total + " · 실수 " + mistakes + " · " + Real(usedTime).ToString("0.0") + "초";
            }
            else StartRound();
        }

        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Mint, 2);
        NovelUI.Label(new Rect(160, 168, 950, 35), (Theme.Header ?? "BASE PAIRING") + "  ·  " + (Done ? "완료" : "라운드 " + (Mathf.Min(round, 1) + 1) + "/2") + "  ·  첫 시도 정답 " + firstTry, NovelUI.Round, 21, NovelUI.Berry);
        if (state == State.Instructions)
        {
            NovelUI.Label(new Rect(175, 230, 930, 260), "위쪽 주형 가닥의 염기마다 짝이 되는 염기를 이어 주세요.\n1라운드 DNA 복제: A-T, G-C\n2라운드 mRNA 전사: A→U, T→A, G→C, C→G (RNA에는 T 대신 U!)\n키보드 A·T·G·C·U 또는 아래 버튼으로 입력해요. 라운드마다 25초.\n틀리면 그 칸에서 다시 고르면 돼요. 정확하고 빠를수록 좋아요.", NovelUI.Soft, 23, NovelUI.Berry);
            if (GUI.Button(new Rect(440, 560, 400, 75), "시작하기", button)) StartRound();
            return;
        }
        if (state == State.Completed) { NovelUI.Label(new Rect(170, 300, 940, 100), Summary, NovelUI.Round, 32, NovelUI.Berry, TextAnchor.MiddleCenter); return; }

        if (state == State.Playing) HandleKeys();

        NovelUI.Label(new Rect(170, 215, 940, 35), Rna ? "mRNA 전사 · DNA 주형 가닥 → mRNA" : "DNA 복제 · 주형 가닥 → 새 가닥", NovelUI.Round, 24, NovelUI.Plum, TextAnchor.MiddleCenter);
        Color[] tint = { NovelUI.Pink, NovelUI.Sky, NovelUI.Mint, NovelUI.Lemon };
        for (int i = 0; i < Length; i++)
        {
            float x = Slot(i);
            bool current = state == State.Playing && i == typed.Count;
            NovelUI.Fill(new Rect(x, 280, 60, 60), tint["ATGC".IndexOf(strand[i])], 14);
            NovelUI.Label(new Rect(x, 280, 60, 60), strand[i].ToString(), NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
            Box(new Rect(x + 28, 345, 4, 40), NovelUI.Alpha(NovelUI.Plum, i < typed.Count ? .6f : .15f));
            Rect slot = new Rect(x, 390, 60, 60);
            if (i < typed.Count)
            {
                NovelUI.Fill(slot, NovelUI.Berry, 14);
                NovelUI.Label(slot, typed[i].ToString(), NovelUI.Round, 30, Color.white, TextAnchor.MiddleCenter);
            }
            else NovelUI.Card(slot, current ? NovelUI.Lemon : NovelUI.Blush, 14, current ? NovelUI.DeepPink : default, current ? 3 : 0);
        }
        Box(new Rect(170, 340, 940, 4), NovelUI.Alpha(NovelUI.Plum, .3f));

        if (state == State.Playing)
        {
            float left = Mathf.Max(0, RoundTime - (Now - stateAt));
            Box(new Rect(170, 480, 940, 12), NovelUI.Alpha(NovelUI.Plum, .15f));
            Box(new Rect(170, 480, 940 * left / RoundTime, 12), left < 7 ? NovelUI.DeepPink : NovelUI.Mint);
            string[] keys = Keys;
            for (int k = 0; k < 4; k++)
                if (NovelUI.Button(new Rect(250 + k * 200, 540, 170, 80), keys[k], true, 34)) Enter(keys[k][0]);
            NovelUI.Label(new Rect(170, 640, 940, 40), "남은 시간 " + Real(left).ToString("0.0") + "초 · 실수 " + mistakes, NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleCenter);
        }
        else NovelUI.Label(new Rect(170, 550, 940, 60), lastText, NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
    }
}
