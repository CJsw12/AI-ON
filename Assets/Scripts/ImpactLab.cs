using UnityEngine;

// Physics lab demonstration: stop a moving cart with the shortest safe cushion.
public class ImpactLab : MiniGame
{
    sealed class Round
    {
        public readonly string Setup;
        public readonly float Mass, Speed, StopDistance, SafeForce;
        public readonly float[] Pads;

        public Round(string setup, float mass, float speed, float stopDistance, float safeForce)
        {
            Setup = setup; Mass = mass; Speed = speed; StopDistance = stopDistance; SafeForce = safeForce;
            Pads = new[] { .5f, 1f, 1.5f };
        }
    }

    enum State { Instructions, Selecting, Traveling, Feedback, Completed }
    static readonly Round Tutorial = new Round("입문 실험 · 기본 카트", 1, 4, 1.2f, 16);
    static readonly Round[][] BeginnerRounds = {
        new[] { new Round("실험 1 · 빈 카트", 1, 4, .8f, 16), new Round("실험 2 · 장비 상자", 2, 4, 1.2f, 16), new Round("실험 3 · 축제 준비", 2, 4, 1.5f, 12) },
        new[] { new Round("실험 1 · 렌즈 운반", 2, 3, .8f, 18), new Round("실험 2 · 무거운 카트", 2, 4, 1.2f, 16), new Round("실험 3 · 빠른 카트", 2, 6, 1.5f, 24) },
        new[] { new Round("실험 1 · 무대 장비", 2, 5, .8f, 50), new Round("실험 2 · 측정 기구", 3, 4, 1.2f, 24), new Round("실험 3 · 최종 시연", 4, 3, 1.5f, 12) }
    };
    static readonly Round[][] ExperiencedRounds = {
        new[] { new Round("실험 1 · 빈 카트", 2, 4, .8f, 32), new Round("실험 2 · 장비 상자", 3, 4, 1.2f, 24), new Round("실험 3 · 축제 준비", 3, 4, 1.5f, 16) },
        new[] { new Round("실험 1 · 렌즈 운반", 3, 3, .8f, 27), new Round("실험 2 · 무거운 카트", 3, 4, 1.2f, 24), new Round("실험 3 · 빠른 카트", 3, 6, 1.5f, 36) },
        new[] { new Round("실험 1 · 무대 장비", 4, 5, .8f, 100), new Round("실험 2 · 측정 기구", 4, 4, 1.2f, 32), new Round("실험 3 · 최종 시연", 6, 3, 1.5f, 18) }
    };
    static readonly string[][] DefaultReplies = {
        new[] { "카트가 안전 구간을 벗어났습니다. 운동량과 stopping distance를 다시 보세요.", "절반은 안전하게 멈췄습니다. momentum이 바뀌는 시간을 생각해 보세요.", "훌륭합니다. 운동량 변화와 average force를 정확히 읽었습니다." },
        new[] { "카트가 아슬아슬했어요. 쿠션 길이에 따라 충격이 어떻게 달라지는지 다시 볼까요?", "세 번 중 일부는 성공! 다음엔 속도와 질량도 같이 살펴봐요.", "멋진 시연이었어요! 힘을 낮추는 방법을 잘 찾았네요." }
    };

    readonly bool experienced, practice;
    readonly Round[] rounds;
    State state = State.Instructions;
    bool tutorial = true, lastOk;
    int roundIndex, passed, selectedPad = -1;
    float stateAt, averageForce;
    string feedback;

    Round Current => tutorial ? Tutorial : rounds[roundIndex];

    public ImpactLab(MiniGameTheme theme, int professor, bool experienced, bool practice) : base(theme)
    {
        this.experienced = experienced;
        this.practice = practice;
        int variant = Mathf.Clamp(theme.ImpactVariant, 0, 2);
        rounds = (experienced ? ExperiencedRounds : BeginnerRounds)[variant];
    }

    public override string[][] Replies => Theme.Replies ?? DefaultReplies;

    void Begin()
    {
        tutorial = true;
        roundIndex = 0;
        passed = 0;
        selectedPad = -1;
        state = State.Selecting;
    }

    void Launch(int padIndex)
    {
        selectedPad = padIndex;
        float pad = Current.Pads[padIndex];
        lastOk = PhysicsImpactRules.TryEvaluate(Current.Mass, Current.Speed, pad,
            Current.StopDistance, Current.SafeForce, out averageForce);
        stateAt = Now;
        state = State.Traveling;
        NovelUI.Sound?.Invoke("Select");
    }

    void FinishTravel()
    {
        state = State.Feedback;
        stateAt = Now;
        if (lastOk)
        {
            feedback = "안전 구간 안에서 멈췄어요! average force " + averageForce.ToString("0.#") + " N";
            Judge(true, new Vector2(875, 415), "안전 정지!");
            if (!tutorial) passed++;
        }
        else if (Current.Pads[selectedPad] > Current.StopDistance)
        {
            feedback = "쿠션이 정지선을 넘어가 장비 앞에서 멈추지 못했어요.";
            Judge(false, new Vector2(875, 415), "정지선 초과");
        }
        else
        {
            feedback = "평균힘 " + averageForce.ToString("0.#") + " N · 허용 힘 " + Current.SafeForce.ToString("0.#") + " N. 더 긴 접촉 구간을 골라 보세요.";
            Judge(false, new Vector2(875, 415), "충격이 너무 커요");
        }
    }

    void Advance()
    {
        selectedPad = -1;
        if (tutorial)
        {
            tutorial = false;
            state = State.Selecting;
            return;
        }
        roundIndex++;
        if (roundIndex >= rounds.Length)
        {
            state = State.Completed;
            Done = true;
            Delta = passed == 3 ? 2 : passed > 0 ? 1 : 0;
            Summary = "세 번의 카트 실험 중 " + passed + "번 안전하게 멈췄어요";
            return;
        }
        state = State.Selecting;
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (state == State.Traveling && Now - stateAt >= .75f) FinishTravel();
        if (state == State.Feedback && Now - stateAt >= 1.65f) Advance();

        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 18, NovelUI.Mint, 2);
        NovelUI.Label(new Rect(160, 168, 950, 36), "IMPACT LAB  ·  " + (tutorial ? "연습" : Done ? "완료" : (roundIndex + 1) + "/3 라운드") + "  ·  성공 " + passed,
            NovelUI.Round, 21, NovelUI.Berry);

        if (state == State.Instructions)
        {
            string instructions = experienced
                ? "일반물리 실험 조의 완충 설계 검토입니다. p = mv, J = Δp와 등가속도 정지 model을 적용합니다.\n각 조의 계산 조건에서 F평균 = mv²/(2d)를 비교하고, 허용 힘과 정지선을 모두 만족하는 설계안을 고르세요.\n무점수 연습 뒤 세 실험 결과를 보고서 기준과 대조합니다."
                : "카트가 실험 장비를 싣고 굴러가요. 안전하게 세울 쿠션 길이를 골라 주세요.\n짧은 쿠션은 충격이 크고, 너무 긴 쿠션은 정지선을 넘어가요.\n먼저 연습하고 세 라운드를 해요. 틀려도 바로 이유를 알려드려요.";
            NovelUI.Label(new Rect(180, 250, 920, 215), instructions, NovelUI.Soft, 23, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(440, 560, 400, 70), "연습 시작", true, 24)) Begin();
            return;
        }

        if (state == State.Completed)
        {
            NovelUI.Label(new Rect(180, 345, 920, 90), Summary, NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(190, 450, 900, 70), experienced
                ? "Δp = J = F평균·Δt  ·  정지 구간을 늘리면 평균힘은 줄어듭니다."
                : "운동량 변화가 같다면 멈추는 시간이 길수록 평균힘이 작아져요.",
                NovelUI.Soft, 22, NovelUI.Plum, TextAnchor.MiddleCenter);
            return;
        }

        Round round = Current;
        NovelUI.Card(new Rect(175, 220, 930, 98), Color.white, 18, NovelUI.Blush, 2);
        NovelUI.Label(new Rect(198, 230, 885, 32), tutorial ? "연습 · " + round.Setup : round.Setup, NovelUI.Round, 23, NovelUI.Berry);
        string values = "질량 " + round.Mass.ToString("0.#") + " kg   ·   속도 " + round.Speed.ToString("0.#") + " m/s   ·   허용 힘 " + round.SafeForce.ToString("0.#") + " N   ·   정지 구간 " + round.StopDistance.ToString("0.#") + " m";
        NovelUI.Label(new Rect(198, 270, 885, 35), values, NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);

        string lesson = experienced
            ? "조별 예측 · p = mv = " + (round.Mass * round.Speed).ToString("0.#") + " kg·m/s   ·   정지 조건을 검토하세요."
            : "질량·속도가 클수록 운동량이 커요. 더 긴 접촉 구간은 평균힘을 낮춰요.";
        NovelUI.Label(new Rect(185, 326, 910, 34), lesson, NovelUI.Soft, 19, NovelUI.Plum, TextAnchor.MiddleCenter);

        DrawTrack(round);

        if (state == State.Selecting)
        {
            NovelUI.Label(new Rect(190, 510, 900, 32), experienced ? "조별 설계 후보 중 허용 힘과 정지 구간을 만족하는 안을 선택하세요." : "안전하게 멈출 쿠션을 눌러 보세요.",
                NovelUI.Round, 20, NovelUI.Berry, TextAnchor.MiddleCenter);
            for (int i = 0; i < round.Pads.Length; i++)
            {
                float x = 210 + i * 290;
                if (NovelUI.Button(new Rect(x, 555, 250, 66), round.Pads[i].ToString("0.#") + " m 쿠션", false, 22)) Launch(i);
            }
        }
        else if (state == State.Traveling)
        {
            float t = Mathf.Clamp01((Now - stateAt) / .75f);
            float x = Mathf.Lerp(250, 450 + round.Pads[selectedPad] * 120, t);
            DrawCart(x);
        }
        else if (state == State.Feedback)
        {
            NovelUI.Label(new Rect(170, 540, 940, 58), feedback, NovelUI.Round, 23, lastOk ? NovelUI.DeepPink : NovelUI.Berry, TextAnchor.MiddleCenter);
            if (experienced)
            {
                float stopTime = 2 * round.Pads[selectedPad] / round.Speed;
                NovelUI.Label(new Rect(200, 600, 880, 30), "Δp = " + (round.Mass * round.Speed).ToString("0.#") + " N·s   ·   Δt ≈ " + stopTime.ToString("0.##") + " s   ·   F평균 = " + averageForce.ToString("0.#") + " N",
                    NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
            }
        }
    }

    void DrawTrack(Round round)
    {
        const float trackY = 430, cushionStart = 450, scale = 120;
        NovelUI.Card(new Rect(195, 365, 890, 120), NovelUI.Blush, 18, NovelUI.Pink, 1);
        NovelUI.Fill(new Rect(220, trackY, 825, 12), NovelUI.Alpha(NovelUI.Berry, .35f), 6);
        float boundary = cushionStart + round.StopDistance * scale;
        NovelUI.Fill(new Rect(boundary, 385, 5, 82), NovelUI.DeepPink, 2);
        NovelUI.Label(new Rect(boundary - 48, 370, 110, 24), "정지선", NovelUI.Soft, 15, NovelUI.DeepPink, TextAnchor.MiddleCenter);
        float pad = selectedPad >= 0 ? round.Pads[selectedPad] : 1f;
        NovelUI.Fill(new Rect(cushionStart, 413, pad * scale, 42), state == State.Feedback && lastOk ? NovelUI.Mint : NovelUI.Alpha(NovelUI.Sky, .75f), 12);
        NovelUI.Label(new Rect(cushionStart, 453, pad * scale, 24), selectedPad >= 0 ? pad.ToString("0.#") + " m 쿠션" : "쿠션 구간", NovelUI.Soft, 15, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (state != State.Traveling) DrawCart(250);
    }

    void DrawCart(float x)
    {
        NovelUI.Card(new Rect(x, 397, 72, 38), new Color(.31f, .57f, .78f), 9, NovelUI.Berry, 2);
        NovelUI.Fill(new Rect(x + 10, 432, 16, 15), NovelUI.Berry, 8);
        NovelUI.Fill(new Rect(x + 48, 432, 16, 15), NovelUI.Berry, 8);
        NovelUI.Label(new Rect(x + 8, 399, 56, 30), "⚙", NovelUI.Round, 20, Color.white, TextAnchor.MiddleCenter);
    }
}
