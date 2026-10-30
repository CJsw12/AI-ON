// AI-SW융합학부: fixed events and choices; relationship changes only the professor's response.
// The professor's tone lives in AiSwPersonality, the quizzes in AiSwQuizzes.
public static class AiSwStory
{
    public static readonly string[][] Dialogue = {
        new string[0],
        new[] {
            "나레이션|입학 첫날. 지도 앱은 도착했다고 하는데, 내가 찾는 304호는 보이지 않는다.",
            "신입생 단톡방|OT 시작한대요! / 304호 어디예요? / 저 지금 옆 건물인데요…",
            "나|나만 길 잃은 건 아니네. 그래도 지각은 피하고 싶은데.",
            "나레이션|휴대폰을 보며 걷다가 누군가와 부딪칠 뻔했다.",
            "교수|앞을 보고 걸어야죠. 강의실보다 보건실을 먼저 찾겠어요.",
            "나|죄송합니다! 혹시 304호 아세요?",
            "교수|알죠. 제가 거기서 OT를 해야 하니까.",
            "나|아… 신입생입니다. 오늘 처음 왔어요.",
            "교수|이 건물은 입구가 두 개라 처음엔 헷갈려요. 게시판 옆 계단을 기억해 두세요.",
            "나레이션|첫 만남부터 망했다. 가방끈을 고쳐 잡으며 급히 인사했다."
        },
        new[] {
            "나레이션|OT를 마치고 받은 시간표를 휴대폰에 저장했다. 첫 전공 수업은 일주일 뒤, 같은 건물이었다.",
            "선배|프로그래밍 기초는 실습 분반을 확인해 주세요. 개발 동아리에서는 Git으로 코드 변경 기록을 남기는 법도 알려 줘요!",
            "나레이션|며칠 뒤, 첫 전공 수업. OT에서 봤던 얼굴들이 같은 강의실로 들어왔다.",
            "동기|너도 이 수업이지? 옆자리 비워 놨어.",
            "나|고마워. 근데 다들 벌써 잘하는 것 같지 않아?",
            "동기|나도 개발 도구 설치하다가 막혔어. 실행 버튼이 어딘지도 한참 찾았다. 모르면 같이 물어보자.",
            "나레이션|동기의 노트 첫 장에도 '일단 해 보기'라고 적혀 있었다. 혼자 긴장했던 게 조금 우스워졌다.",
            "교수|지금부터 배우면 됩니다. 처음부터 알고 있었다면 이 수업을 들을 이유가 없겠죠.",
            "교수|첫 학기에는 변수와 반복문으로 코드를 씁니다. 다음에는 자료구조로 데이터를 정리하고, 알고리즘으로 찾는 방법을 배웁니다.",
            "나레이션|교수님은 강의실 목록을 배열로 펼쳐 보였다. 번호를 하나씩 확인하는 것도 알고리즘이라는 설명이었다.",
            "교수|먼저 하나 물어보죠. 이 학과에서 무엇을 해 보고 싶나요?"
        },
        new[] {
            "나레이션|강의실에서 작은 웃음이 터졌다. 긴장이 조금 풀렸다. 이어서 첫 실습이 시작됐다.",
            "교수|학과 소개를 듣기만 하는 것보다 직접 해 보는 게 빠르겠죠. 실행 버튼을 눌러 보세요.",
            "나|오늘부터 시험인가요?",
            "교수|시험이면 그렇게 친절하게 예고하지 않습니다.",
            "나레이션|출력 창에 0, -1, -2가 끝없이 쌓였다. 오류 밑줄은 없었는데 실행 결과는 예상과 달랐다.",
            "동기|내 화면도 똑같아. 종료 버튼부터 눌러야겠다.",
            "교수|좋아요. 우선 실행을 멈추세요. 이제 어떤 순서로 움직였는지 읽어 봅시다.",
            "나|제 컴퓨터만 왜 이러죠?",
            "교수|문법이 맞아도 원하는 대로 작동한다는 뜻은 아닙니다. 출력 기록을 따라 변수 값부터 확인합시다.",
            "교수|0부터 시작해서 10이 되면 끝내려 합니다. 그런데 숫자가 계속 줄어드는군요."
        },
        new[] {
            "나레이션|교수님과 증감 방향을 확인하자 프로그램이 정상적으로 멈췄다. 실행 결과보다 원리를 이해한 게 더 기뻤다.",
            "교수|오늘은 정답보다 왜 틀렸는지 기억하면 됩니다. 오류를 읽는 것도 공부예요.",
            "나레이션|몇 주가 지났다. 강의실 위치는 외웠지만, 과제는 여전히 낯설었다.",
            "신입생 단톡방|학식 먹고 과제할 사람? / 저요. 혼자 보니까 하나도 모르겠음.",
            "나레이션|점심마다 빈 강의실을 찾아 헤매던 우리는 '사용 가능한 강의실을 보여 주는 프로그램'을 수업 프로젝트로 정했다.",
            "동기|직접 겪는 문제라 설명하기는 쉽겠다. 대신 학교 데이터를 마음대로 가져오면 안 되겠지?",
            "나|우선 예시 시간표로 만들어 보자. 강의실 번호와 예약 시간을 목록으로 묶고, 빈 시간을 찾는 함수를 만들면 되겠다.",
            "동기|목록 검색은 네가, 입력 화면은 내가 맡자. Git 저장소도 만들었어. 파일 이름에 최종, 진짜최종 붙이는 건 그만하고.",
            "나레이션|동기와 과제를 하다가 막힌 부분을 들고 교수님 연구실을 찾았다.",
            "교수|문 앞에서 세 번째 지나가는 것 같은데요. 들어와요.",
            "나|검색 함수가 빈 목록만 돌려줘요. 테스트에 쓴 시간표랑 실행 결과를 같이 가져왔어요.",
            "교수|질문받으려고 정해 둔 시간이 오피스아워입니다. 어디서 막혔나요?"
        },
        new[] {
            "나레이션|교수님은 코드 리뷰를 하며 입력, 검색, 출력 부분을 나눠 표시했다. 한 함수에 모두 넣었을 때보다 읽기 쉬웠다.",
            "나|제가 이해한 걸 한번 설명해 봐도 돼요?",
            "교수|좋죠. 설명할 수 있으면 어디까지 이해했는지도 알 수 있습니다.",
            "나레이션|그날 상담에서 실제 강의실 시간표 대신 연습용 데이터를 쓰기로 했다. 개인정보 없이도 작동 원리를 보여 줄 수 있었다.",
            "나레이션|중간고사가 지나고, 축제 주간에는 팀원들과 학과 부스를 구경했다. 전에는 낯설던 선배들이 이제 먼저 인사를 건넸다.",
            "동기|우리 것도 학기 끝나면 저렇게 보여 줄 수 있겠지?",
            "나|최소한 빈 강의실 하나는 제대로 찾아 주게 만들자.",
            "나레이션|학기 말. 동기들과 만든 '빈 강의실 찾기' 프로그램의 발표가 내일이다.",
            "동기|입력 화면 수정한 거 Git에 올렸어. 시연할 버전에 합친 뒤 전체 테스트 한번 해 줄래?",
            "나|내가 볼게. 새 입력 화면을 연결한 뒤에 한 번 더 확인하자.",
            "동기|잠깐. 검색 함수만 테스트하면 통과하는데 새 입력 화면을 연결하면 목록이 안 떠. 연결 부분이 문제인가 봐.",
            "나레이션|내가 마지막으로 수정한 화면이었다. 교수님이 진행 상황을 보러 들어왔다."
        },
        new[] {
            "나레이션|수정한 입력 화면을 저장한 뒤, 기존 목록과 연결해 보았다. 강의실 이름이 다시 화면에 나타났다.",
            "동기|304호, 305호… 비어 있는 시간이랑 일치해. 드디어 된다!",
            "나|예약이 꽉 찬 경우, 빈 시간표, 없는 강의실 번호도 테스트하자. 정상 입력 한 번 된 걸로 끝내면 안 되겠네.",
            "교수|결과가 없다는 안내도 필요하죠. 사용자에게는 빈 화면과 고장이 똑같이 보일 수 있어요.",
            "교수|원인을 찾았으면 같은 오류가 다시 생기는지도 확인해 보세요.",
            "동기|추천 기능은 시간이 모자라니 실험용으로 빼 두자. 대신 교수님 말대로 빈 강의실이 없을 때 안내부터 넣자.",
            "나레이션|예약이 꽉 찬 시간을 넣어 보니 화면이 텅 비었다. 정말 고장 난 것처럼 보였다."
        }
    };

    // Lines where the professor is not part of the conversation (alone, or only classmates and seniors),
    // so the portrait is hidden. Indexes are per chapter: Dialogue lines, then FollowUp lines.
    static readonly int[][] HiddenDialogue = {
        new int[0],
        new[] { 0, 1, 2, 3 },             // Lost on campus, before bumping into the professor.
        new[] { 0, 1, 2, 3, 4, 5, 6 },    // After OT and chatting with a classmate before the lecture.
        new int[0],
        new[] { 2, 3, 4, 5, 6, 7, 8 },    // Weeks later with classmates, until entering the office.
        new[] { 4, 5, 6, 7, 8, 9, 10 },   // Festival and the night before, until the professor walks in.
        new[] { 0, 1, 2, 5, 6 }           // Team testing, then the team trying the blank result.
    };
    static readonly int[][] HiddenFollowUp = {
        new int[0],
        new[] { 1, 2 },                   // Seniors explain courses and clubs.
        new[] { 0, 1, 2, 3 },             // Review cards with a classmate at lunch.
        new[] { 1 },                      // Classmate's remark after the explanation.
        new[] { 0, 1 },                   // Reading the lab's project board.
        new[] { 1, 2 },                   // Team splits the remaining work.
        new[] { 0, 1, 2 }                 // Presentation day, until greeting the professor.
    };

    // Chapters 3, 4 and 6 open with a quiz instead (AiSwQuizzes).
    public static string[] Choices(int chapter)
    {
        switch (chapter) {
            case 1: return new[] { "그럼 교수님만 따라가면 되겠네요!", "죄송합니다. 제가 길을 잘 못 찾아서요." };
            case 2: return new[] { "불편했던 걸 해결하는 프로그램을 만들고 싶어요.", "아직 모르겠어요. 이것저것 해 보고 싶어요.", "과제만 적으면 좋겠어요." };
            case 5: return new[] { "제가 합친 뒤부터 안 돼요. 변경 기록부터 같이 볼까요?", "제 함수 테스트는 통과했으니 제 문제는 아닌 것 같아요.", "제가 혼자 고칠 테니까 먼저 가세요." };
            default: return new string[0];
        }
    }

    public static readonly string[] FollowUp = {
        "", "교수님을 따라 OT 교실로 들어왔다. 복도 끝에서 선배들이 이름표를 나눠 주고 있었다.\n선배가 수강신청과 동아리 안내를 설명했다. 모르는 것은 단톡방에서 편하게 물어보라고 했다.\n소개 자료에는 프로그래밍, 데이터, 팀 프로젝트가 적혀 있었다. 같은 학부에서도 해 볼 일이 꽤 많았다.\n교수님이 짧은 자기소개 종이를 나눠 주었다. 학과를 고른 이유와 궁금한 것을 적어 보자.",
        "수업이 끝나자 옆자리 동기가 가방을 챙기며 말을 걸었다.\n동기: 학식 먹으면서 오늘 배운 거 복습할래? 내가 용어 카드 만들어 왔어.\n학식당 테이블 위에 동기가 카드를 엎어 놓았다. 변수, 반복문, 자료구조, 알고리즘… 오늘 수업에서 들은 단어들이었다.\n동기: 용어랑 뜻을 짝지어 봐. 많이 틀리면 디저트는 네가 사는 거다!",
        "교수님과 반복문을 확인했다. 숫자가 증가하면 10에 도달하고, 그때 조건이 거짓이 되어 반복이 끝난다.\n동기: 설명 듣고 나니까 알겠는데, 다음에 혼자 보면 또 헷갈릴 것 같아.\n교수님: 그럼 눈으로 확인해 봅시다. 화면의 숫자가 10이 되는 순간 멈춤 버튼을 누르세요.\n화면의 숫자가 빠르게 올라가기 시작했다. 반복이 끝나는 바로 그 순간을 잡아야 한다.",
        "질문을 마치고 나오려는데 연구실 게시판의 프로젝트 모집 공고가 눈에 들어왔다.\n화면에는 보행 안내 앱과 데이터 시각화 프로젝트가 소개되어 있었다. 교실에서 배운 코드가 쓰이는 모습이었다.\n교수님은 구경은 가능하지만 정기 참여는 일정과 기초 학습을 함께 고려해야 한다고 설명했다.\n당장 참여할지 정할 필요는 없었다. 지금 궁금한 것부터 물어보기로 했다.",
        "교수님과 실행 기록을 확인했다. 새 입력 화면은 강의실 번호를 글자로 보내는데, 기존 코드는 숫자만 비교하고 있었다.\n동기: 누가 잘못했냐보다 어디서 달라졌는지 보니까 빨리 찾네.\n나는 입력 형식을 맞추고, 동기는 테스트용 강의실 목록을 준비했다. 수정한 부분을 따로 저장해 두었다.\n교수님: 잠깐 쉬었다 합시다. 연구실 커피 머신 쓸 줄 알아요? 제 것도 한 잔 부탁해요.",
        "역할을 나눠 작업을 마쳤다. 추천 기능은 실험용으로 따로 두고, 발표에서는 검증한 기본 기능을 보여 주기로 했다.\n다음 날, 발표 중 질문 하나에는 말을 더듬었다. 그래도 강의실 목록은 끝까지 정상적으로 표시됐다.\n동기: 끝났다! 밥 먹으러 가자. 나는 잠깐 교수님께 인사하고 가겠다고 했다.\n교수님: 첫 학기를 보내 보니 어떤가요? 다음에는 무엇을 해 보고 싶어요?"
    };
    // Chapters 2, 3 and 5 continue with a minigame instead (MiniGames).
    public static string[] ExtraChoices(int chapter)
    {
        switch (chapter) {
            case 1: return new[] { "전공 수업에서 뭘 배우는지 질문한다.", "친구들과 함께 학교생활을 해 보고 싶다고 적는다.", "아직은 혼자 생각해 보고 싶다고 적는다." };
            case 4: return new[] { "연구실 프로젝트 체험 방법을 물어본다.", "친구와 함께 참가할 수 있는지 물어본다.", "우선 수업부터 혼자 익히겠다고 한다." };
            case 6: return new[] { "새로운 전공 프로젝트에 도전하고 싶어요.", "친구들과 함께 더 배우고 싶어요.", "제 속도로 혼자 더 공부해 보고 싶어요." };
            default: return new string[0];
        }
    }
    public static readonly string[] EndingNames = { "아직 낯선 캠퍼스", "첫 학기 수료", "학식 메이트", "혼자서도 해냈어요", "질문왕 신입생", "디버깅 파트너", "연구실 문은 열려 있다", "다음 학기도 잘 부탁드립니다" };
    public static readonly string[] EndingText = {
        "발표는 끝났지만, 캠퍼스는 여전히 낯설다.\n나: 아직 이 전공에 맞는지 모르겠어요.\n교수님: 어려웠던 것과 흥미 없었던 것을 구분해 보세요.\n다음 학기 상담 날짜를 달력에 적었다.",
        "낯설던 강의실에서 첫 학기를 끝냈다.\n교수님: 수고했어요. 다음엔 후배 길도 알려 주세요.\n나: 이제 304호는 확실히 알아요.\n시간표를 접으며 조금 웃었다.",
        "동기: 다음 학기에도 같이 수업 듣자!\n나: 학식 메뉴 확인부터 해 놓을게.\n교수님: 같이 배우는 사람을 만났네요.\n단톡방 이름이 '과제 생존자들'로 바뀌었다.",
        "혼자 복습하던 노트가 어느새 꽉 찼다.\n교수님: 자기 속도를 찾았군요. 막히면 찾아와요.\n나: 다음엔 질문도 정리해서 올게요.\n혼자 해낸 기쁨과 함께 다음 목표를 적었다.",
        "교수님: 오늘도 질문이 있나요?\n나: 이번엔 세 개뿐이에요.\n교수님은 웃으며 의자를 내주었다.\n모르는 것을 묻는 일이 더는 두렵지 않았다.",
        "동기: 이번엔 내가 고친 코드도 봐 줄래?\n나: 좋아. 같이 확인하자.\n교수님: 서로의 오류를 찾아 주는 좋은 팀이네요.\n다음 프로젝트의 공동 작업 폴더를 만들었다.",
        "교수님: 방학에 연구실 프로젝트를 구경하러 올래요?\n나: 제가 참여해도 돼요?\n교수님: 배우고 싶으면 문은 열려 있습니다.\n게시판의 모집 공고가 이제 내 이야기처럼 느껴졌다.",
        "나: 모르는 건 더 많아졌는데, 해 보고 싶은 것도 생겼어요.\n교수님: 그럼 제대로 배운 겁니다.\n나: 다음 학기도 잘 부탁드립니다.\n처음 길을 잃었던 곳에 다시 만나고 싶은 사람들이 생겼다."
    };

    static readonly MiniGameTheme Cards = new MiniGameTheme { Kind = MiniGameKind.Cards, Title = "용어 카드 짝맞추기", Place = "Cafeteria", Note = "챕터 2 · 학식에서 용어 카드로 복습" };
    static readonly MiniGameTheme Loop = new MiniGameTheme { Kind = MiniGameKind.Timing, Title = "반복문 타이밍 맞추기", Place = "Classroom", Note = "챕터 3 · 반복문이 끝나는 순간 멈추기" };
    static readonly MiniGameTheme Coffee = new MiniGameTheme { Kind = MiniGameKind.Coffee, Title = "연구실 커피 제조", Place = "ComputerLab", Note = "챕터 5 · 주문서대로 커피 만들기" };

    static Slot[] Slots()
    {
        Slot[] slots = new Slot[12];
        for (int i = 0; i < slots.Length; i++)
        {
            int chapter = i / 2 + 1, stage = i % 2;
            slots[i] = new Slot {
                Options = stage == 0 ? Choices(chapter) : ExtraChoices(chapter),
                Scores = AiSwPersonality.Scores(i), Replies = AiSwPersonality.Replies(i),
                Major = AiSwQuizzes.Find(chapter, stage, true), Minor = AiSwQuizzes.Find(chapter, stage, false)
            };
        }
        slots[0].Stats = "--"; slots[1].Stats = "qts"; slots[2].Stats = "ii-"; slots[7].Stats = "its"; slots[8].Stats = "t-s"; slots[11].Stats = "its";
        slots[3].Game = Cards; slots[3].Stat = 't';
        slots[5].Game = Loop; slots[5].Stat = 'i';
        slots[9].Game = Coffee; slots[9].Stat = 'q';
        slots[6].Stat = 'q';   // The office-hour quiz counts as a question.
        return slots;
    }

    public static readonly Story Data = new Story {
        College = "공과대학", Name = "AI-SW융합학부", Short = "AI-SW", Tag = "소프트웨어",
        LevelQuestion = "코딩 경험이 있나요?",
        Levels = new[] { "전공자", "비전공자" },
        LevelNotes = new[] { "코드를 직접 입력해서 문제 풀기", "쉬운 설명과 객관식으로 문제 풀기" },
        Professors = new[] {
            new Professor { Name = "김가람 교수", Description = "엄격한 원칙파 · 근거와 준비를 중시", TagA = "#원칙파", TagB = "#꼼꼼함" },
            new Professor { Name = "이명희 교수", Description = "유쾌한 소통파 · 호기심과 협업을 중시", TagA = "#소통파", TagB = "#호기심" }
        },
        Chapters = new[] { "", "입학 OT", "첫 수업", "첫 실습", "오피스아워", "발표 전날", "함께 마무리하기" },
        Dialogue = Dialogue, FollowUp = FollowUp, HiddenDialogue = HiddenDialogue, HiddenFollowUp = HiddenFollowUp,
        Slots = Slots(),
        EndingNames = EndingNames, EndingText = EndingText,
        EndingLines = new[] { AiSwPersonality.StrictEndings, AiSwPersonality.CheerfulEndings },
        Spoken = AiSwPersonality.Spoken,
        Background = (chapter, stage, line) =>
            chapter == 1 && stage == 1 ? "Classroom" :
            chapter == 2 ? (stage == 1 ? "Cafeteria" : "Classroom") :
            chapter == 3 ? "Classroom" :
            chapter == 4 && stage == 0 && line < 6 ? "Cafeteria" :
            chapter == 6 && stage == 1 && line >= 1 ? "Auditorium" :
            chapter <= 1 || chapter == 7 ? "CampusExterior" : "ComputerLab"
    };
}
