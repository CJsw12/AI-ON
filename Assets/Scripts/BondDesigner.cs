using System;
using System.Threading.Tasks;
using UnityEngine;

// 경험자용 결합 설계실. 판정과 해설의 근거는 BondPuzzle과 BondTexts가 소유한다.
public sealed class BondDesigner : MiniGame
{
    enum Screen { Instructions, Playing, Completed }
    readonly int professor;
    readonly BondPuzzle puzzle;
    readonly BondMolecule[] molecules;
    readonly Vector2[][] positions = new Vector2[5][];
    readonly int?[,] results = new int?[5, 3];
    readonly bool[] visited = new bool[5];
    Screen screen;
    float lastTime, yaw, galleryYaw;
    float frameElapsed;
    int galleryIndex;
    bool aiMode, firstBondWrong;
    Story story;
    int affection;
    string fact = "", confusion = "", comment = "";
    Task<AiProfessor.Result> commentTask;
    BondRequestToken commentToken;
    static readonly BondShape[] Shapes = { BondShape.Linear, BondShape.Bent, BondShape.TrigonalPlanar, BondShape.TrigonalPyramidal, BondShape.Tetrahedral };

    public BondDesigner(MiniGameTheme theme, int professor) : base(theme)
    {
        this.professor = professor;
        puzzle = new BondPuzzle(UnityEngine.Random.Range(0, int.MaxValue));
        molecules = puzzle.Selection;
        Rect board = new Rect(135, 180, 570, 400);
        for (int i = 0; i < molecules.Length; i++) positions[i] = Positions(molecules[i], board);
    }
    public void ConfigureAi(bool enabled, Story context, int currentAffection) { aiMode = enabled; story = context; affection = currentAffection; }
    public override string[][] Replies => BondTexts.Replies;
    protected override bool ShowStars => false;
    protected override void OnSkip()
    {
        if (commentToken != null) puzzle?.CancelRequest(commentToken);
        commentTask = null;
        commentToken = null;
        comment = "";
        puzzle?.End();
        screen = Screen.Completed;
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        PollComment();
        if (screen == Screen.Instructions) { DrawInstructions(); return; }
        if (screen == Screen.Completed) { DrawCompleted(); return; }
        frameElapsed = 0;
        if (Event.current.type == EventType.Repaint) { frameElapsed = Mathf.Max(0, Now - lastTime); lastTime = Now; }
        puzzle.Frame(frameElapsed, DrawPlaying);
        if (puzzle.Stage == BondStage.Finished) Finish();
    }

    void DrawInstructions()
    {
        NovelUI.Card(new Rect(130, 150, 1020, 600), NovelUI.Cream, 24, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(180, 205, 920, 55), "결합 설계실", NovelUI.Round, 36, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(200, 290, 880, 205), BondTexts.Introduction(professor) + "\n\n결합 자리를 왼쪽 클릭해 선을 놓고, 오른쪽 클릭으로 되돌려요.\n완성한 뒤 모양과 극성을 골라요. 다섯 분자에 150초!", NovelUI.Soft, 23, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (NovelUI.Button(new Rect(440, 575, 400, 65), "시작하기", true, 24)) { lastTime = Now; visited[0] = true; screen = Screen.Playing; }
    }

    void DrawPlaying()
    {
        BondMolecule m = puzzle.Current;
        if (m == null) return;
        int index = puzzle.Index;
        BondStage stage = puzzle.Stage;
        int[] orders = puzzle.Orders;
        NovelUI.Card(new Rect(110, 105, 1060, 680), NovelUI.Cream, 20, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(140, 120, 1000, 38), "결합 설계실   " + (index + 1) + " / 5       남은 시간 " + Mathf.CeilToInt((float)puzzle.Remaining) + "초       점수 " + puzzle.Score + " / " + puzzle.MaxScore, NovelUI.Round, 22, NovelUI.Berry);
        Rect board = new Rect(135, 180, 570, 400);
        NovelUI.Card(board, new Color(.12f, .15f, .21f), 16);
        NovelUI.Label(new Rect(735, 175, 405, 65), m.Name + " · " + m.Id, NovelUI.Round, 27, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (stage == BondStage.Bond || stage == BondStage.Shape) DrawSkeleton(m, positions[index], orders, stage == BondStage.Bond);
        else
        {
            bool dragging = MoleculeView.HandleDrag(board, ref yaw);
            if (!dragging) yaw += frameElapsed * MoleculeView.AutoRotateSpeedDegrees;
            MoleculeView.Draw(board, m.Id, orders, yaw, true, true);
            bool hasPairs = false;
            for (int i = 0; i < m.Atoms.Length; i++) if (BondPuzzle.LonePairs(m, orders, i).GetValueOrDefault() > 0) { hasPairs = true; break; }
            if (hasPairs) NovelUI.Label(new Rect(145, 582, 560, 35), MoleculeView.LonePairNotice, NovelUI.Soft, 15, NovelUI.Plum, TextAnchor.MiddleCenter);
        }
        if (stage == BondStage.Bond)
        {
            NovelUI.Label(new Rect(735, 260, 390, 95), "① 결합 자리를 눌러 바꾸세요\n좌클릭 순환 · 우클릭 역순환", NovelUI.Soft, 20, NovelUI.Berry);
            if (NovelUI.Button(new Rect(760, 450, 340, 55), "결합 제출", true, 22)) SubmitBond(m, orders);
        }
        else if (stage == BondStage.Shape)
        {
            NovelUI.Label(new Rect(730, 242, 410, 55), (m.Id == "C2H4" || m.Id == "C2H2" ? "강조된 C 주변의 모양은?" : "② 중심 원자 주변의 모양은?"), NovelUI.Round, 19, NovelUI.Berry);
            for (int i = 0; i < Shapes.Length; i++)
                if (NovelUI.Button(new Rect(750, 300 + i * 53, 355, 45), BondTexts.ShapeName(Shapes[i]), false, 18)) { SubmitShape(m, Shapes[i]); break; }
        }
        else if (stage == BondStage.Polarity)
        {
            NovelUI.Label(new Rect(745, 290, 370, 48), "③ 분자의 극성은?", NovelUI.Round, 23, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(755, 360, 165, 60), "극성", false, 22)) SubmitPolarity(m, BondPolarity.Polar);
            if (NovelUI.Button(new Rect(935, 360, 165, 60), "무극성", false, 22)) SubmitPolarity(m, BondPolarity.Nonpolar);
        }
        else if (stage == BondStage.PolarityReview)
        {
            NovelUI.Label(new Rect(750, 280, 360, 70), "해설을 확인하고 다음 분자로 가요.", NovelUI.Soft, 22, NovelUI.Berry, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(760, 420, 340, 60), index == 4 ? "결과 보기" : "다음 분자", true, 22)) { puzzle.Advance(); fact = confusion = comment = ""; firstBondWrong = false; if (puzzle.Stage != BondStage.Finished) visited[puzzle.Index] = true; }
        }
        if (stage == BondStage.Bond && puzzle.HintVisible)
        {
            int wrong = BondPuzzle.FirstWrong(m, orders);
            fact = wrong >= 0 ? BondTexts.Hint(professor, m, orders, wrong) : "이제 제출해 보세요.";
        }
        NovelUI.Label(new Rect(145, 625, 990, 45), fact, NovelUI.Soft, 19, NovelUI.Berry);
        NovelUI.Label(new Rect(145, 670, 990, 35), confusion, NovelUI.Soft, 17, NovelUI.Plum);
        if (!string.IsNullOrEmpty(comment)) NovelUI.Label(new Rect(145, 705, 990, 48), "AI 교수 코멘트: " + comment, NovelUI.Soft, 17, NovelUI.Berry);
        else if (commentTask != null && puzzle.IsCommentPending(commentToken)) NovelUI.Label(new Rect(145, 705, 990, 48), "AI 교수가 생각 중…", NovelUI.Soft, 17, NovelUI.Berry);
        if (stage == BondStage.PolarityReview && m.Id == "O2") NovelUI.Label(new Rect(145, 755, 990, 30), BondTexts.OxygenLimitation(professor), NovelUI.Soft, 16, NovelUI.Plum);
    }

    void SubmitBond(BondMolecule m, int[] answer)
    {
        bool correct = puzzle.SubmitBond();
        if (correct) { results[puzzle.Index, 0] = firstBondWrong ? 0 : 1; fact = confusion = comment = ""; }
        else
        {
            comment = "";
            results[puzzle.Index, 0] = 0;
            int wrong = BondPuzzle.FirstWrong(m, answer);
            fact = BondTexts.Hint(professor, m, answer, wrong);
            string[] candidates = BondPuzzle.PossibleConfusions(m, BondStage.Bond, answer);
            confusion = Confusions(candidates);
            if (!firstBondWrong) RequestComment(m, BondStage.Bond, string.Join(",", Array.ConvertAll(answer, x => x.ToString())), candidates, fact);
            firstBondWrong = true;
        }
    }
    void SubmitShape(BondMolecule m, BondShape answer)
    {
        bool correct = puzzle.SubmitShape(answer);
        results[puzzle.Index, 1] = correct ? 1 : 0;
        fact = BondTexts.Fact(professor, m, BondStage.Shape, correct);
        string[] candidates = BondPuzzle.PossibleConfusions(m, BondStage.Shape, answer);
        confusion = Confusions(candidates); comment = "";
        if (!correct) RequestComment(m, BondStage.Shape, BondTexts.ShapeName(answer), candidates, fact);
    }
    void SubmitPolarity(BondMolecule m, BondPolarity answer)
    {
        bool correct = puzzle.SubmitPolarity(answer);
        results[puzzle.Index, 2] = correct ? 1 : 0;
        fact = BondTexts.Fact(professor, m, BondStage.Polarity, correct);
        string[] candidates = BondPuzzle.PossibleConfusions(m, BondStage.Polarity, answer);
        confusion = Confusions(candidates); comment = "";
        if (!correct) RequestComment(m, BondStage.Polarity, answer == BondPolarity.Polar ? "극성" : "무극성", candidates, fact);
    }
    static string Confusions(string[] values) { return values.Length == 0 ? "" : BondTexts.Confusion(string.Join(" · ", values)); }

    void RequestComment(BondMolecule m, BondStage stage, string answer, string[] candidates, string factLine)
    {
        if (!aiMode || story == null || !AiProfessor.Supported || !AiProfessor.HasKey) return;
        BondRequestToken token = puzzle.BeginRequest(stage);
        if (token == null) return;
        try
        {
            string prompt = AiProfessor.BondCommentPrompt(story, professor, affection, m, stage, answer, candidates, factLine);
            commentTask = AiProfessor.Ask(prompt, "학생에게 건넬 짧은 격려 코멘트만 써 주세요.");
            if (commentTask == null) { puzzle.CancelRequest(token); return; }
            commentToken = token;
        }
        catch { puzzle.CancelRequest(token); }
    }
    void PollComment()
    {
        if (commentTask == null || !commentTask.IsCompleted) return;
        Task<AiProfessor.Result> task = commentTask;
        BondRequestToken token = commentToken;
        commentTask = null; commentToken = null;
        try
        {
            AiProfessor.Result result = task.Result;
            // The model only picks phrase numbers; the shown text is always a reviewed phrase from BondTexts.
            string reply = AiProfessor.Clean(result == null ? null : result.Text, 1000).Replace('\n', ' ').Replace('\r', ' ').Trim();
            string text = BondTexts.ComposeComment(professor, reply);
            if (text != null && puzzle.FinishRequest(token, text)) comment = text;
        }
        catch { }
        finally { puzzle.CancelRequest(token); }
    }

    void Finish()
    {
        screen = Screen.Completed;
        Delta = puzzle.Delta;
        Summary = "결합 설계실 " + puzzle.Score + " / " + puzzle.MaxScore + "점";
        puzzle.End();
        commentTask = null; commentToken = null; comment = "";
        Done = true;
        if (Delta == 0) ExplainFailure("결합 수와 분자의 입체 배치를 다시 살펴봐요.", "원자마다 결합 수를 세고, 비공유 전자쌍을 포함해 전자 영역을 세어 모양을 판단해요. 마지막으로 분자의 대칭과 쌍극자 상쇄를 확인해요.");
        lastTime = Now;
    }
    void DrawCompleted()
    {
        float elapsed = 0;
        if (Event.current.type == EventType.Repaint) { elapsed = Mathf.Max(0, Now - lastTime); lastTime = Now; }
        NovelUI.Card(new Rect(110, 105, 1060, 680), NovelUI.Cream, 20, NovelUI.Pink, 2);
        NovelUI.Label(new Rect(150, 120, 980, 50), "결합 설계실 결과 · " + puzzle.Score + " / " + puzzle.MaxScore + "점", NovelUI.Round, 30, NovelUI.Berry, TextAnchor.MiddleCenter);
        Rect large = new Rect(155, 195, 460, 300);
        NovelUI.Card(large, new Color(.12f, .15f, .21f), 14);
        bool dragging = MoleculeView.HandleDrag(large, ref galleryYaw);
        if (!dragging) galleryYaw += elapsed * MoleculeView.AutoRotateSpeedDegrees;
        MoleculeView.Draw(large, molecules[galleryIndex].Id, molecules[galleryIndex].Answers, galleryYaw, true, true);
        NovelUI.Label(new Rect(165, 500, 440, 35), molecules[galleryIndex].Name + (visited[galleryIndex] ? "" : " · 미도달"), NovelUI.Round, 20, NovelUI.Berry, TextAnchor.MiddleCenter);
        for (int i = 0; i < 5; i++)
        {
            Rect thumb = new Rect(660, 190 + i * 76, 95, 68);
            NovelUI.Card(thumb, new Color(.12f, .15f, .21f), 8);
            MoleculeView.Draw(thumb, molecules[i].Id, molecules[i].Answers, 0, false, false);
            if (GUI.Button(thumb, GUIContent.none, GUIStyle.none)) galleryIndex = i;
            string row = molecules[i].Name + (visited[i] ? "" : " · 미도달") + "  ①" + Mark(i, 0) + " ②" + (molecules[i].Diatomic ? "생략" : Mark(i, 1)) + " ③" + Mark(i, 2);
            NovelUI.Label(new Rect(770, 190 + i * 76, 370, 68), row, NovelUI.Soft, 17, NovelUI.Berry);
        }
        NovelUI.Label(new Rect(165, 590, 950, 60), "모형을 누르면 크게 볼 수 있어요.  ○ 정답  × 오답  — 미응답", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
    }
    string Mark(int row, int col) { int? value = results[row, col]; return !value.HasValue ? "—" : value.Value == 1 ? "○" : "×"; }

    static Vector2[] Positions(BondMolecule m, Rect r)
    {
        float x = r.center.x, y = r.center.y;
        switch (m.Id)
        {
            case "N2": case "O2": return new[] { new Vector2(x-90,y), new Vector2(x+90,y) };
            case "H2O": case "CO2": case "HCN": return new[] { new Vector2(x,y), new Vector2(x-150,y), new Vector2(x+150,y) };
            case "NH3": case "CH2O": return new[] { new Vector2(x,y), new Vector2(x,y-125), new Vector2(x-150,y+105), new Vector2(x+150,y+105) };
            case "CH4": return new[] { new Vector2(x,y), new Vector2(x,y-135), new Vector2(x-155,y), new Vector2(x+155,y), new Vector2(x,y+135) };
            case "C2H4": return new[] { new Vector2(x-80,y), new Vector2(x+80,y), new Vector2(x-205,y-115), new Vector2(x-205,y+115), new Vector2(x+205,y-115), new Vector2(x+205,y+115) };
            default: return new[] { new Vector2(x-75,y), new Vector2(x+75,y), new Vector2(x-220,y), new Vector2(x+220,y) };
        }
    }
    void DrawSkeleton(BondMolecule m, Vector2[] points, int[] orders, bool interactive)
    {
        for (int i = 0; i < orders.Length; i++)
        {
            Vector2 a = points[m.Edges[i, 0]], b = points[m.Edges[i, 1]], d = (b-a).normalized, normal = new Vector2(-d.y,d.x);
            for (int line = 0; line < orders[i]; line++) DrawLine(a + normal * (line - (orders[i]-1)*.5f)*9, b + normal * (line - (orders[i]-1)*.5f)*9, Color.white, 4);
            if (interactive)
            {
                Vector2 mid = (a+b)*.5f;
                Rect hit = new Rect(mid.x-26, mid.y-26, 52, 52);
                NovelUI.Ring(hit, NovelUI.Sky, 26, 2);
                Event e = Event.current;
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1) && hit.Contains(e.mousePosition) && GUI.enabled && !NovelUI.Blocked) { puzzle.CycleBond(i, e.button == 1); NovelUI.Sound?.Invoke("Select"); e.Use(); }
            }
        }
        for (int i = 0; i < m.Atoms.Length; i++)
        {
            Vector2 p = points[i]; string atom = m.Atoms[i];
            Color color = atom == "H" ? Color.white : atom == "C" ? new Color(.38f,.4f,.46f) : atom == "N" ? NovelUI.Sky : NovelUI.DeepPink;
            Rect circle = new Rect(p.x-27,p.y-27,54,54);
            NovelUI.Fill(circle, color, 27);
            if (i == m.Center) NovelUI.Ring(circle, NovelUI.Lemon, 27, 3);
            NovelUI.Label(circle, atom, NovelUI.Round, 23, atom == "H" ? NovelUI.Berry : Color.white, TextAnchor.MiddleCenter);
            string label = BondPuzzle.BondSum(m, orders, i).ToString(); Color text = Color.white;
            if (interactive && puzzle.HintVisible) { AtomStatus s = BondPuzzle.Status(m, orders, i); label += s == AtomStatus.Short ? " 부족" : s == AtomStatus.Excess ? " 초과" : " 정확"; text = s == AtomStatus.Short ? NovelUI.Lemon : s == AtomStatus.Excess ? NovelUI.Pink : NovelUI.Mint; }
            if (!interactive) { int? pairs = BondPuzzle.LonePairs(m, orders, i); if (pairs.HasValue && pairs.Value > 0) label += " · 전자쌍 " + pairs.Value; }
            NovelUI.Label(new Rect(p.x-75,p.y+31,150,32), label, NovelUI.Soft, 20, text, TextAnchor.MiddleCenter);
        }
    }
    static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
    {
        if (Event.current.type != EventType.Repaint) return;
        Matrix4x4 old = GUI.matrix; Vector2 d = b-a;
        NovelUI.RotateAround(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg, a);
        NovelUI.Fill(new Rect(a.x,a.y-width*.5f,d.magnitude,width), color, 0);
        GUI.matrix = old;
    }
}
