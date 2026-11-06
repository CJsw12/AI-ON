public sealed class AiSwLectureLine
{
    public readonly string Speaker;
    public readonly string Text;
    public AiSwLectureLine(string speaker, string text) { Speaker = speaker; Text = text; }
}

// Professor-specific lecture scripts for the computer-science pilot.
public static class AiSwLectures
{
    static readonly string[] Names = { "자료구조", "알고리즘", "컴퓨터 기초" };
    static readonly string[] Descriptions = {
        "접시 쌓기와 줄 서기로 스택·큐를 배워요",
        "정렬된 목록에서 값을 찾는 순서를 비교해요",
        "2진수 자리값을 더해 숫자를 바꿔 봐요"
    };
    static readonly string[] Concepts = {
        "스택은 LIFO(나중에 넣은 값부터 꺼냄), 큐는 FIFO(먼저 넣은 값부터 꺼냄)입니다.",
        "순차 탐색은 왼쪽부터 비교하고, 이진 탐색은 정렬된 목록의 가운데부터 범위를 반으로 줄입니다.",
        "2진수 오른쪽 자리부터 값은 1, 2, 4, 8, 16, 32…이며 켜진 자리값을 더합니다."
    };

    static readonly AiSwLectureLine[][][] Lines = {
        new[] {
            new[] {
                new AiSwLectureLine("교수", "스택은 마지막에 넣은 걸 먼저 꺼내요. LIFO예요."),
                new AiSwLectureLine("동기", "접시를 쌓으면 맨 위 접시부터 꺼내죠?"),
                new AiSwLectureLine("교수", "큐는 먼저 온 값부터 꺼내요. FIFO예요."),
                new AiSwLectureLine("동기", "급식 줄처럼 먼저 선 사람이 먼저 받네요."),
                new AiSwLectureLine("교수", "문제는 k번째로 꺼낸 값이에요. 순서를 놓치지 말아요.")
            },
            new[] {
                new AiSwLectureLine("교수", "스택은 접시 타워예요! 마지막에 올린 접시가 먼저 나오는 LIFO죠!"),
                new AiSwLectureLine("동기", "큐는 급식 줄 같네요. 먼저 온 사람이 먼저 받으니까요."),
                new AiSwLectureLine("교수", "맞아요! 큐는 FIFO. 입장 순서가 출구까지 그대로 이어져요."),
                new AiSwLectureLine("동기", "그럼 문제에선 몇 번째로 꺼내는지도 봐야겠네요."),
                new AiSwLectureLine("교수", "정확해요! 넣고 꺼내는 순서를 따라가면 인벤토리 정리 퀘스트 완료!")
            }
        },
        new[] {
            new[] {
                new AiSwLectureLine("교수", "순차 탐색은 앞에서부터 확인해요."),
                new AiSwLectureLine("동기", "정렬된 목록이면 중간부터 볼 수도 있죠?"),
                new AiSwLectureLine("교수", "이진 탐색이에요. target이 크면 왼쪽 절반을 버려요."),
                new AiSwLectureLine("동기", "비교 횟수만 세면 되는 거군요."),
                new AiSwLectureLine("교수", "네. 가운데를 고르는 규칙부터 확인해요.")
            },
            new[] {
                new AiSwLectureLine("교수", "순차 탐색은 목록을 한 칸씩 훑는 거예요. 튜토리얼식 완주죠."),
                new AiSwLectureLine("동기", "정렬돼 있으면 더 빨리 찾는 방법도 있나요?"),
                new AiSwLectureLine("교수", "이진 탐색! 가운데를 확인하고, target이 크면 왼쪽 절반을 버려요."),
                new AiSwLectureLine("동기", "매번 후보가 반으로 줄면 비교 횟수도 줄겠네요."),
                new AiSwLectureLine("교수", "그렇죠! 오늘은 비교 횟수를 세며 검색 공략을 해 봐요.")
            }
        },
        new[] {
            new[] {
                new AiSwLectureLine("교수", "2진수는 각 자리에 값이 있어요."),
                new AiSwLectureLine("동기", "오른쪽부터 1, 2, 4, 8, 16…이죠?"),
                new AiSwLectureLine("교수", "맞아요. 1이 켜진 자리값만 더해요."),
                new AiSwLectureLine("동기", "0인 자리는 더하지 않으면 되네요."),
                new AiSwLectureLine("교수", "그 합을 10진수로 읽으면 돼요. 한 자리씩 봐요.")
            },
            new[] {
                new AiSwLectureLine("교수", "2진수는 스위치 보드예요! 오른쪽부터 자리값은 1, 2, 4, 8, 16…"),
                new AiSwLectureLine("동기", "1은 ON, 0은 OFF라고 생각하면 쉽겠네요."),
                new AiSwLectureLine("교수", "ON인 자리값만 더하면 일반 숫자가 나와요. 켜진 자리 수는 값이 아니고요."),
                new AiSwLectureLine("동기", "그럼 10101은 16+4+1이라 21이네요!"),
                new AiSwLectureLine("교수", "정답! 자릿값 합산 퀘스트예요. 0은 얌전히 패스!")
            }
        }
    };

    public static readonly string[][] Replies = {
        new[] {
            "…기초는 다시 짚었어요. 다음엔 천천히 계산해요.",
            "맞았어요. …흐름을 읽었네요.",
            "정답이에요. 제대로 배웠어요."
        },
        new[] {
            "아쉬워요! 오답이 나온 이유까지 봤으니 경험치 획득이에요!",
            "정답! 강의 퀘스트 클리어예요!",
            "완벽해요! 오늘부터 전공 지식 +1레벨!"
        }
    };

    public static string TopicName(LectureTopic topic) => Names[(int)topic];
    public static string Description(LectureTopic topic) => Descriptions[(int)topic];
    public static string Concept(LectureTopic topic) => Concepts[(int)topic];
    public static string ChoicePrompt(int professor) => professor == 0
        ? "오늘은 뭘 배워 볼까요? 하나 골라요."
        : "오늘은 뭘 배워 볼까요? 자료구조, 알고리즘, 컴퓨터 기초 중 먼저 레벨업할 주제를 골라요!";
    public static AiSwLectureLine[] Script(int professor, LectureTopic topic) => Lines[(int)topic][professor];
}
