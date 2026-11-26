using System;

public static class BondTexts
{
    public static readonly string[][] Replies = {
        new[]{"괜찮아요. 결합 수부터 천천히 다시 살펴봐요.","잘했어요. 전자 영역과 대칭도 함께 살펴봐요.","차분하게 완성했군요. 정말 든든하고 안심이에요."},
        new[]{"앗, 다음 실험에서는 원자마다 차근차근 세어 봐요!", "좋아요! 모양과 대칭까지 살피면 더 멋질 거예요!", "짜잔! 완벽한 결합 설계 솜씨네요!"}
    };
    static void CheckProfessor(int professor) { if(professor<0 || professor>1) throw new ArgumentOutOfRangeException("professor"); }
    public static string Introduction(int professor) { CheckProfessor(professor); return professor==0 ? "서두르지 말고 결합 수부터 살펴봐요." : "자, 결합 설계실에서 분자의 모습을 만들어 볼까요?"; }
    public static string ShapeName(BondShape shape) { switch(shape) { case BondShape.Linear: return "직선형"; case BondShape.Bent: return "굽은형"; case BondShape.TrigonalPlanar: return "삼각평면형"; case BondShape.TrigonalPyramidal: return "삼각뿔형"; case BondShape.Tetrahedral: return "정사면체형"; default: throw new ArgumentOutOfRangeException("shape"); } }
    public static string Fact(int professor, BondMolecule molecule, BondStage stage) { return Fact(professor,molecule,stage,true); }
    public static string Fact(int professor, BondMolecule molecule, BondStage stage, bool correct) {
        CheckProfessor(professor); string prefix=professor==0 ? "차분히 살펴봐요. " : correct ? "짜잔! " : "자, 보세요! ";
        if(stage==BondStage.Shape) { if(molecule.Diatomic) throw new ArgumentException("Diatomic molecules have no shape question", "molecule"); int pairs=BondPuzzle.LonePairs(molecule,molecule.Answers,molecule.Center).Value; int regions=BondPuzzle.ElectronRegions(molecule,molecule.Answers).Value; string center=(molecule.Id=="C2H4" || molecule.Id=="C2H2") ? "강조된 C" : molecule.Atoms[molecule.Center]; return prefix+"정답 "+ShapeName(molecule.Shape)+" · "+center+" 주변 전자 영역 "+regions+"개(결합 원자 "+(regions-pairs)+" + 비공유 전자쌍 "+pairs+")"; }
        if(stage==BondStage.Polarity) { string reason=molecule.Diatomic ? "같은 원자끼리 결합" : molecule.Polarity==BondPolarity.Polar ? "쌍극자가 상쇄되지 않고 남음" : "결합 쌍극자가 대칭으로 상쇄"; return prefix+"정답 "+(molecule.Polarity==BondPolarity.Polar ? "극성" : "무극성")+" · "+molecule.Name+"("+molecule.Id+") · "+reason; }
        throw new ArgumentException("Fact requires shape or polarity stage");
    }
    public static string Hint(int professor, BondMolecule molecule, int[] orders, int atom) { CheckProfessor(professor); AtomStatus status=BondPuzzle.Status(molecule,orders,atom); if(status==AtomStatus.Exact) return ""; string word=status==AtomStatus.Short ? "부족" : "초과"; return (professor==0 ? "서두르지 말고 " : "자, 살펴보세요! ")+molecule.Atoms[atom]+"의 결합 수 "+word+" · 현재 결합 "+BondPuzzle.BondSum(molecule,orders,atom)+" · 필요 "+BondPuzzle.Required(molecule.Atoms[atom]); }
    public static string Confusion(string candidate) { return String.IsNullOrEmpty(candidate) ? "" : "이런 점을 헷갈리기 쉬워요: "+candidate+"."; }
    public static string OxygenLimitation(int professor) { CheckProfessor(professor); return (professor==0 ? "참고해요. " : "여기서 잠깐! ")+"O2의 루이스 구조는 실제 산소의 홀전자 두 개와 상자성을 나타내지 못해요."; }

    static readonly string[][] s_commentTags = {
        new[] {
            "첫 실수 위로", "첫 실수 위로", "서두름 경계", "서두름 경계",
            "차분히 다시 보기", "차분히 다시 보기", "거의 맞혔을 때", "거의 맞혔을 때",
            "다음 문항 격려", "다음 문항 격려", "집중 칭찬", "집중 칭찬",
            "시간 압박 완화", "시간 압박 완화", "신뢰 격려", "신뢰 격려",
            "확인 습관", "확인 습관", "긴장 완화", "긴장 완화",
            "성장 응원", "성장 응원"
        },
        new[] {
            "첫 실수 위로", "첫 실수 위로", "서두름 경계", "서두름 경계",
            "차분히 다시 보기", "차분히 다시 보기", "거의 맞혔을 때", "거의 맞혔을 때",
            "다음 문항 격려", "다음 문항 격려", "집중 칭찬", "집중 칭찬",
            "시간 압박 완화", "시간 압박 완화", "신뢰 격려", "신뢰 격려",
            "시선 전환", "시선 전환", "긴장 완화", "긴장 완화",
            "성장 응원", "성장 응원"
        }
    };

    static readonly string[][] s_commentPhrases = {
        // 0: 정도윤 교수 (걱정 많은 안전 지킴이, 차분·다정)
        new[] {
            "괜찮아요, 처음엔 누구나 헷갈려요.",
            "실수해도 괜찮아요, 여기서 배우는 거예요.",
            "급할수록 놓치기 쉬워요, 천천히 가요.",
            "서두르지 않아도 돼요, 숨을 고르세요.",
            "한 번만 더 차분히 확인해 볼까요?",
            "잠깐 멈춰서 위 설명을 천천히 읽어 봐요.",
            "거의 다 왔어요, 조금만 더 살펴봐요.",
            "방향은 아주 좋았어요, 아깝네요.",
            "이번 경험이 다음 문제에 힘이 될 거예요.",
            "다음엔 분명 더 수월하게 해낼 거예요.",
            "끝까지 차근차근 살피는 태도가 든든해요.",
            "꼼꼼하게 고민한 흔적이 보여서 안심이에요.",
            "시계는 신경 쓰지 말고 차분히 가요.",
            "조급해하지 마세요, 충분히 해낼 수 있어요.",
            "학생이라면 금방 감을 잡을 거예요.",
            "저는 학생을 믿어요, 천천히 이어 가요.",
            "확인하는 습관이 제일 든든한 기본이에요.",
            "놓친 부분이 없는지 차근차근 짚어 봐요.",
            "긴장되죠? 저도 그래요, 편하게 해 봐요.",
            "마음 놓으세요, 지금도 잘 따라오고 있어요.",
            "지금 헷갈린 게 나중에 꼭 기억에 남아요.",
            "차분한 걸음이 제일 빠른 길이에요."
        },
        // 1: 오하람 교수 (쇼맨십 마술사, "자, 보세요!/짜잔!" 계열)
        new[] {
            "괜찮아요! 마술도 리허설이 필요하죠!",
            "앗, 깜짝 반전이었네요! 다음엔 안 속아요!",
            "잠깐 멈춤! 서두르면 트릭을 놓쳐요!",
            "호흡 가다듬고, 무대를 다시 살펴봐요!",
            "자, 보세요! 위 해설에 비밀이 숨어 있어요!",
            "자, 시선 집중! 포인트를 다시 짚어 봐요!",
            "오, 거의 다 왔어요! 한 끗 차이였네요!",
            "아까워요! 감이 벌써 올라오고 있어요!",
            "자, 다음 무대로 멋지게 넘어가 볼까요?",
            "다음 장면에선 박수가 터질 거예요!",
            "빛나는 그 눈빛, 무대 체질이에요!",
            "끝까지 파고드는 집중력, 최고예요!",
            "시계 소리엔 흔들리지 말고 즐겨 봐요!",
            "여유 있게 가요! 무대의 주인공은 학생이에요!",
            "금방 요령을 터득할 거예요, 기대할게요!",
            "학생이라면 다음 쇼는 거뜬히 해내죠!",
            "다른 시선으로 바라보면 비밀이 보여요!",
            "한 걸음 떨어져서 보면 훨씬 잘 보여요!",
            "어깨 힘 풀고, 신나게 실험해 봐요!",
            "실수도 멋진 공연의 일부예요, 웃으면서 가요!",
            "방금 포인트만 챙기면 다음엔 짜잔, 성공이죠!",
            "지금 헷갈린 덕분에 다음엔 더 빛날 거예요!"
        }
    };

    public static string[] CommentPhrases(int professor)
    {
        CheckProfessor(professor);
        return (string[])s_commentPhrases[professor].Clone();
    }

    public static string[] CommentTags(int professor)
    {
        CheckProfessor(professor);
        return (string[])s_commentTags[professor].Clone();
    }

    // 모델 응답에서 ASCII 숫자(0-9)로 된 1-based 문구 번호를 최대 2개 추출해 공백으로 이어 붙인다.
    // 범위 밖·중복·전각 숫자는 무시하며, 유효 번호가 하나도 없으면 null을 반환한다.
    public static string ComposeComment(int professor, string modelReply)
    {
        CheckProfessor(professor);
        if (String.IsNullOrWhiteSpace(modelReply)) return null;
        string[] phrases = s_commentPhrases[professor];
        int max = phrases.Length;
        int first = 0, second = 0;
        int i = 0;
        while (i < modelReply.Length)
        {
            char c = modelReply[i];
            if (c < '0' || c > '9') { i++; continue; }
            int value = 0;
            bool overflow = false;
            while (i < modelReply.Length && modelReply[i] >= '0' && modelReply[i] <= '9')
            {
                int digit = modelReply[i] - '0';
                if (value > 10000) overflow = true;
                else value = value * 10 + digit;
                i++;
            }
            if (overflow || value < 1 || value > max) continue;
            if (first == 0) first = value;
            else if (value != first) { second = value; break; }
        }
        if (first == 0) return null;
        if (second == 0) return phrases[first - 1];
        return phrases[first - 1] + " " + phrases[second - 1];
    }
}
