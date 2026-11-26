using System;
using System.Collections.Generic;

public enum BondShape { Linear, Bent, TrigonalPlanar, TrigonalPyramidal, Tetrahedral }
public enum BondPolarity { Nonpolar, Polar }
public enum BondStage { Bond, Shape, Polarity, Finished, PolarityReview }
public enum AtomStatus { Short, Exact, Excess }

public sealed class BondMolecule
{
    public readonly string Id, Name;
    public readonly string[] Atoms;
    public readonly int[,] Edges;
    public readonly int[] Answers;
    public readonly int Center;
    public readonly BondShape Shape;
    public readonly BondPolarity Polarity;
    public bool Diatomic { get { return Atoms.Length == 2; } }
    public BondMolecule(string id, string name, string[] atoms, int[,] edges, int[] answers, int center, BondShape shape, BondPolarity polarity)
    { Id=id; Name=name; Atoms=atoms; Edges=edges; Answers=answers; Center=center; Shape=shape; Polarity=polarity; }
}

public sealed class BondRequestToken
{
    public readonly int Round, Molecule, Attempt;
    public readonly BondStage Stage;
    internal readonly double Started;
    internal BondRequestToken(int round, int molecule, BondStage stage, int attempt, double started)
    { Round=round; Molecule=molecule; Stage=stage; Attempt=attempt; Started=started; }
}

public sealed class BondPuzzle
{
    public const double CommentDeadlineSeconds = 15.0;
    public static readonly BondMolecule[] Molecules = {
        new BondMolecule("H2O","물",new[]{"O","H","H"},new[,]{{0,1},{0,2}},new[]{1,1},0,BondShape.Bent,BondPolarity.Polar),
        new BondMolecule("NH3","암모니아",new[]{"N","H","H","H"},new[,]{{0,1},{0,2},{0,3}},new[]{1,1,1},0,BondShape.TrigonalPyramidal,BondPolarity.Polar),
        new BondMolecule("CH4","메테인",new[]{"C","H","H","H","H"},new[,]{{0,1},{0,2},{0,3},{0,4}},new[]{1,1,1,1},0,BondShape.Tetrahedral,BondPolarity.Nonpolar),
        new BondMolecule("CO2","이산화 탄소",new[]{"C","O","O"},new[,]{{0,1},{0,2}},new[]{2,2},0,BondShape.Linear,BondPolarity.Nonpolar),
        new BondMolecule("N2","질소",new[]{"N","N"},new[,]{{0,1}},new[]{3},0,BondShape.Linear,BondPolarity.Nonpolar),
        new BondMolecule("O2","산소",new[]{"O","O"},new[,]{{0,1}},new[]{2},0,BondShape.Linear,BondPolarity.Nonpolar),
        new BondMolecule("HCN","사이안화 수소",new[]{"C","H","N"},new[,]{{0,1},{0,2}},new[]{1,3},0,BondShape.Linear,BondPolarity.Polar),
        new BondMolecule("CH2O","폼알데하이드",new[]{"C","O","H","H"},new[,]{{0,1},{0,2},{0,3}},new[]{2,1,1},0,BondShape.TrigonalPlanar,BondPolarity.Polar),
        new BondMolecule("C2H4","에텐",new[]{"C","C","H","H","H","H"},new[,]{{0,1},{0,2},{0,3},{1,4},{1,5}},new[]{2,1,1,1,1},0,BondShape.TrigonalPlanar,BondPolarity.Nonpolar),
        new BondMolecule("C2H2","에타인",new[]{"C","C","H","H"},new[,]{{0,1},{0,2},{1,3}},new[]{3,1,1},0,BondShape.Linear,BondPolarity.Nonpolar)
    };
    public static int Required(string element) { switch(element) { case "H": return 1; case "C": return 4; case "N": return 3; case "O": return 2; default: throw new ArgumentException("element"); } }
    public static int Valence(string element) { switch(element) { case "H": return 1; case "C": return 4; case "N": return 5; case "O": return 6; default: throw new ArgumentException("element"); } }
    public static int Cycle(int order, bool reverse) { if(order < 0 || order > 3) throw new ArgumentOutOfRangeException("order"); return (order + (reverse ? 3 : 1)) % 4; }
    public static int BondSum(BondMolecule molecule, int[] orders, int atom) { if(molecule==null) throw new ArgumentNullException("molecule"); if(orders==null) throw new ArgumentNullException("orders"); if(orders.Length!=molecule.Answers.Length) throw new ArgumentException("Incorrect bond order count", "orders"); if(atom<0 || atom>=molecule.Atoms.Length) throw new ArgumentOutOfRangeException("atom"); int sum=0; for(int i=0;i<orders.Length;i++) if(molecule.Edges[i,0]==atom || molecule.Edges[i,1]==atom) sum+=orders[i]; return sum; }
    public static AtomStatus Status(BondMolecule molecule, int[] orders, int atom) { int diff=BondSum(molecule,orders,atom)-Required(molecule.Atoms[atom]); return diff<0 ? AtomStatus.Short : diff>0 ? AtomStatus.Excess : AtomStatus.Exact; }
    public static int FirstWrong(BondMolecule molecule, int[] orders) { for(int i=0;i<molecule.Atoms.Length;i++) if(Status(molecule,orders,i)!=AtomStatus.Exact) return i; return -1; }
    public static bool Complete(BondMolecule molecule, int[] orders) { if(orders==null || orders.Length!=molecule.Answers.Length) return false; for(int i=0;i<orders.Length;i++) if(orders[i]<0 || orders[i]>3) return false; return FirstWrong(molecule,orders)<0; }
    public static int? LonePairs(BondMolecule molecule, int[] orders, int atom) { if(!Complete(molecule,orders)) return null; return (Valence(molecule.Atoms[atom])-BondSum(molecule,orders,atom))/2; }
    public static int? ElectronRegions(BondMolecule molecule, int[] orders) { int? pairs=LonePairs(molecule,orders,molecule.Center); if(!pairs.HasValue) return null; int bonds=0; for(int i=0;i<orders.Length;i++) if(orders[i]>0 && (molecule.Edges[i,0]==molecule.Center || molecule.Edges[i,1]==molecule.Center)) bonds++; return bonds+pairs.Value; }
    public static int DeltaFor(int score, int maximum) { if(maximum<=0) throw new ArgumentOutOfRangeException("maximum"); return score*100>=maximum*85 ? 2 : score*100>=maximum*60 ? 1 : 0; }

    public static string[] PossibleConfusions(BondMolecule m, BondStage stage, object answer)
    {
        List<string> result=new List<string>();
        if(stage==BondStage.Bond) {
            int[] orders=answer as int[]; if(orders==null || orders.Length!=m.Answers.Length || Complete(m,orders)) return result.ToArray();
            result.Add("원자별 필요 결합 수");
            bool excess=false, missingMultiple=false; for(int i=0;i<orders.Length;i++) { if(orders[i]>m.Answers[i]) excess=true; if(m.Answers[i]>=2 && orders[i]<=1) missingMultiple=true; }
            if(!excess && missingMultiple) result.Add("다중결합으로 옥텟을 채우는 경우");
        } else if(stage==BondStage.Shape && answer is BondShape) {
            BondShape shape=(BondShape)answer; if(shape==m.Shape || m.Diatomic) return result.ToArray();
            if((m.Id=="NH3" && shape==BondShape.TrigonalPlanar) || (m.Id=="H2O" && shape==BondShape.Linear)) result.Add("비공유 전자쌍도 전자 영역으로 세는 점");
            if(m.Id=="NH3" && shape==BondShape.Tetrahedral) result.Add("전자 배치와 분자 모양의 구분");
            if(m.Id=="C2H4" && shape==BondShape.Tetrahedral) result.Add("다중결합도 전자 영역 하나로 세는 점");
        } else if(stage==BondStage.Polarity && answer is BondPolarity) {
            BondPolarity polarity=(BondPolarity)answer; if(polarity==m.Polarity) return result.ToArray();
            if(m.Id=="CO2") result.Add("결합 극성과 분자 극성의 구분 및 대칭 상쇄");
            if(m.Id=="NH3" || m.Id=="H2O") result.Add("비대칭 구조에서 쌍극자가 남는 점");
            if(m.Id=="HCN") result.Add("직선형이어도 양 끝 원자가 달라 쌍극자가 상쇄되지 않는 점");
            if(result.Count==0) result.Add(m.Polarity==BondPolarity.Polar ? "분자 구조에서 쌍극자가 남는 점" : "대칭 또는 같은 원자에 의한 쌍극자 상쇄");
        }
        return result.ToArray();
    }
    static HashSet<string> s_allowedCommentPhrases;
    static HashSet<string> AllowedCommentPhrases()
    {
        if(s_allowedCommentPhrases!=null) return s_allowedCommentPhrases;
        Type textsType=typeof(BondPuzzle).Assembly.GetType("BondTexts",false);
        if(textsType==null) {
            System.Reflection.Assembly[] assemblies=AppDomain.CurrentDomain.GetAssemblies();
            for(int i=0;i<assemblies.Length && textsType==null;i++) textsType=assemblies[i].GetType("BondTexts",false);
        }
        if(textsType==null) return null;
        System.Reflection.MethodInfo method=textsType.GetMethod("CommentPhrases",new Type[]{typeof(int)});
        if(method==null) return null;
        HashSet<string> set=new HashSet<string>(StringComparer.Ordinal);
        for(int p=0;p<2;p++) {
            string[] phrases=method.Invoke(null,new object[]{p}) as string[];
            if(phrases!=null) for(int i=0;i<phrases.Length;i++) if(!String.IsNullOrEmpty(phrases[i])) set.Add(phrases[i]);
        }
        if(set.Count>0) s_allowedCommentPhrases=set;
        return s_allowedCommentPhrases;
    }
    public static bool AcceptComment(string text)
    {
        if(String.IsNullOrEmpty(text) || text.Length>80) return false;
        HashSet<string> phrases=AllowedCommentPhrases();
        if(phrases==null) return false;
        if(phrases.Contains(text)) return true;
        for(int i=1;i<text.Length-1;i++) {
            if(text[i]!=' ') continue;
            string left=text.Substring(0,i);
            string right=text.Substring(i+1);
            if(left!=right && phrases.Contains(left) && phrases.Contains(right)) return true;
        }
        return false;
    }

    static int nextRound;
    readonly int round;
    readonly BondMolecule[] selection;
    int index, bondSubmissions, attempts, requests;
    int[] orders;
    bool inFlight, shapeFeedback;
    BondRequestToken active, pending;
    double clock;
    public BondStage Stage { get; private set; }
    public int Score { get; private set; }
    public double Remaining { get; private set; }
    public bool HintVisible { get { return bondSubmissions>0 && Stage==BondStage.Bond; } }
    public BondMolecule Current { get { return Stage==BondStage.Finished ? null : selection[index]; } }
    public int Index { get { return index; } }
    public int MaxScore { get; private set; }
    public int Delta { get { return DeltaFor(Score,MaxScore); } }
    public int[] Orders { get { return (int[])orders.Clone(); } }
    public BondMolecule[] Selection { get { return (BondMolecule[])selection.Clone(); } }
    public BondPuzzle(int seed) : this(Select(seed)) { }
    public BondPuzzle(BondMolecule[] selected) {
        if(selected==null || selected.Length!=5) throw new ArgumentException("Select five molecules");
        selection=(BondMolecule[])selected.Clone(); for(int i=0;i<5;i++) { if(selection[i]==null) throw new ArgumentException("Null molecule"); MaxScore+=selection[i].Diatomic ? 2 : 3; }
        round=System.Threading.Interlocked.Increment(ref nextRound); Stage=BondStage.Bond; Remaining=150; orders=new int[Current.Answers.Length];
    }
    static BondMolecule[] Select(int seed) { BondMolecule[] pool=(BondMolecule[])Molecules.Clone(); Random random=new Random(seed); for(int i=pool.Length-1;i>0;i--) { int j=random.Next(i+1); BondMolecule t=pool[i]; pool[i]=pool[j]; pool[j]=t; } BondMolecule[] selected=new BondMolecule[5]; Array.Copy(pool,selected,5); return selected; }
    public void CycleBond(int edge, bool reverse) { if(edge<0 || edge>=orders.Length) throw new ArgumentOutOfRangeException("edge"); if(Stage==BondStage.Bond && Remaining>0) orders[edge]=Cycle(orders[edge],reverse); }
    void Transition(BondStage stage) { active=null; Stage=stage; attempts++; if(stage==BondStage.Finished) { inFlight=false; pending=null; } }
    public bool SubmitBond() { if(Stage!=BondStage.Bond || Remaining<=0) return false; bondSubmissions++; attempts++; active=null; bool correct=Complete(Current,orders); if(correct) { if(bondSubmissions==1) Score++; Transition(Current.Diatomic ? BondStage.Polarity : BondStage.Shape); } return correct; }
    public bool SubmitShape(BondShape shape) { if(Stage!=BondStage.Shape || Remaining<=0) return false; bool correct=shape==Current.Shape; if(correct) Score++; Transition(BondStage.Polarity); shapeFeedback=true; return correct; }
    public bool SubmitPolarity(BondPolarity polarity) { if(Stage!=BondStage.Polarity || Remaining<=0) return false; bool correct=polarity==Current.Polarity; if(correct) Score++; shapeFeedback=false; Transition(BondStage.PolarityReview); return correct; }
    public void Advance() { if(Stage!=BondStage.PolarityReview || Remaining<=0) return; index++; if(index==selection.Length) Transition(BondStage.Finished); else { bondSubmissions=0; Transition(BondStage.Bond); orders=new int[Current.Answers.Length]; } }
    public void Frame(double elapsed, Action input) { if(elapsed<0 || Double.IsNaN(elapsed) || Double.IsInfinity(elapsed)) throw new ArgumentOutOfRangeException("elapsed"); if(Stage!=BondStage.Finished && Remaining>0 && input!=null) input(); clock+=elapsed; if(Stage!=BondStage.Finished) { Remaining=Math.Max(0,Remaining-elapsed); if(Remaining==0) Transition(BondStage.Finished); } }
    public void End() { Transition(BondStage.Finished); }
    public BondRequestToken BeginRequest() { return BeginRequest(Stage==BondStage.PolarityReview ? BondStage.Polarity : Stage); }
    public BondRequestToken BeginRequest(BondStage commentStage) { if(inFlight && clock-pending.Started>=45.0) { inFlight=false; pending=null; active=null; } bool allowed=commentStage==Stage && Stage!=BondStage.PolarityReview || (Stage==BondStage.Polarity && shapeFeedback && commentStage==BondStage.Shape) || (Stage==BondStage.PolarityReview && commentStage==BondStage.Polarity); if(Stage==BondStage.Finished || !allowed || inFlight || requests>=10) return null; requests++; attempts++; inFlight=true; active=new BondRequestToken(round,index,commentStage,attempts,clock); pending=active; return active; }
    public bool IsCommentPending(BondRequestToken token) { if(token==null || !inFlight || !Object.ReferenceEquals(pending,token)) return false; bool stageMatches=token.Stage==Stage || (token.Stage==BondStage.Shape && Stage==BondStage.Polarity && shapeFeedback) || (token.Stage==BondStage.Polarity && Stage==BondStage.PolarityReview); return Object.ReferenceEquals(active,token) && token.Round==round && token.Attempt==attempts && Stage!=BondStage.Finished && index==token.Molecule && stageMatches && clock-token.Started<=CommentDeadlineSeconds; }
    public bool FinishRequest(BondRequestToken token, string comment) { if(token==null || !inFlight || !Object.ReferenceEquals(pending,token)) return false; bool valid=IsCommentPending(token) && AcceptComment(comment); inFlight=false; pending=null; active=null; return valid; }
    public void CancelRequest(BondRequestToken token) { if(token!=null && Object.ReferenceEquals(pending,token)) { inFlight=false; pending=null; active=null; } }
}
