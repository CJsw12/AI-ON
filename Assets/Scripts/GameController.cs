using UnityEngine;

public class GameController : MonoBehaviour
{
    int scene, affection, project, professor = -1;
    bool cared, answered, showResult, lastChoiceGood;
    string resultLine;
    float memorial;
    int dialogueIndex;
    int choiceStage, questions, teamwork, interest, solo;
    int entryStep;
    bool experienced;
    Texture2D previousBackground;
    float backgroundChangedAt;
    NovelAudio novelAudio;
    readonly string[] faculties = { "AI-SW융합학부", "스마트시스템공학부", "경영-휴먼라이프학부", "예술-건강학부" };
    GUIStyle title, body, button, stat;

    readonly string[] lines = {
        "두근두근 교수님\nAI-SW융합학부 첫 학기",
        "입학 OT 날. 낯선 캠퍼스에서 처음 만난 교수님이 조용히 물었다.\n\"코딩을 해 본 적 있나요?\"",
        "첫 수업. 과제를 실행하자 프로그램이 멈췄다. 교수님은 코드 한 줄을 가리켰다. 어디가 문제일까?",
        "연구실 문 앞. 선배들이 만드는 프로젝트를 구경하던 주인공은 교수님의 오래된 실패 연구를 발견한다.",
        "중간고사와 과제가 한꺼번에 몰렸다. 막막한 밤, 누구에게 도움을 청할 수 있을까?",
        "학과 축제 날. 평소와 다른 교수님의 모습을 발견했다. 과제 생각을 잠시 내려놓아도 될까?",
        "팀 프로젝트 시연 전날, 서비스가 멈췄다. 혼자 버틸까, 팀과 교수님에게 도움을 요청할까?",
        ""
    };

    void Awake()
    {
        Application.targetFrameRate = 60;
        novelAudio = gameObject.AddComponent<NovelAudio>();
        if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        if (Camera.main == null)
        {
            Camera camera = new GameObject("Background Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .1f, .18f);
        }
    }
    void Update()
    {
        string mood = scene == 5 || (scene == 6 && choiceStage == 0) ? "Crisis" : scene == 0 || scene == 1 || scene == 7 || (scene == 2 && choiceStage == 1) || (scene == 6 && choiceStage == 1) ? "Campus" : "Study";
        novelAudio.SetMood(mood);
    }
    string ProfessorName { get { return professor == 0 ? "김가람 교수" : "이명희 교수"; } }
    string Route { get { return professor == 0 ? "알고리즘 연구실" : "AI 응용 연구실"; } }
    string Location { get { return scene <= 1 || scene == 5 ? "컴퓨터공학관 앞" : scene == 2 ? "컴퓨터공학관 복도" : "CS Lab 304"; } }
    string Chapter { get { return "CHAPTER " + scene + "  ·  " + (scene == 1 ? "입학 OT" : scene == 2 ? "첫 수업" : scene == 3 ? "첫 실습" : scene == 4 ? "오피스아워" : scene == 5 ? "발표 전날" : "함께 마무리하기"); } }
    Texture2D Portrait
    {
        get
        {
            string character = professor == 0 ? "ProfessorKim" : "ProfessorLee";
            if (affection < 0) character += "Frown";
            else if (affection >= 3) character += "Happy";
            return Resources.Load<Texture2D>(character + "Transparent");
        }
    }
    Texture2D Background
    {
        get
        {
            if (scene == 1 && choiceStage == 1) return Resources.Load<Texture2D>("Classroom");
            if (scene == 2) return Resources.Load<Texture2D>(choiceStage == 1 ? "Cafeteria" : "Classroom");
            if (scene == 3) return Resources.Load<Texture2D>("Classroom");
            if (scene == 4 && choiceStage == 0 && dialogueIndex < 6) return Resources.Load<Texture2D>("Cafeteria");
            if (scene == 6 && choiceStage == 1 && dialogueIndex >= 1) return Resources.Load<Texture2D>("Auditorium");
            if (scene <= 1 || scene == 7) return Resources.Load<Texture2D>("CampusExterior");
            if (scene == 2) return Resources.Load<Texture2D>("CampusHallway");
            return Resources.Load<Texture2D>("ComputerLab");
        }
    }

    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = Color.white } };
        body = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true, normal = { textColor = Color.white } };
        button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        button.normal.background = MakeTexture(new Color(.95f, .72f, .82f));
        button.hover.background = MakeTexture(new Color(1f, .87f, .92f));
        button.normal.textColor = new Color(.35f, .05f, .17f);
        button.hover.textColor = new Color(.35f, .05f, .17f);
        stat = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperRight, normal = { textColor = new Color(1f, .88f, .93f) } };
    }
    Texture2D MakeTexture(Color color) { Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture; }
    void DrawScene()
    {
        Texture2D background = Background;
        if (previousBackground != background) { previousBackground = background; backgroundChangedAt = Time.unscaledTime; }
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background, ScaleMode.ScaleAndCrop);
        bool showProfessor = background != Resources.Load<Texture2D>("Cafeteria");
        if (showProfessor) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Portrait, ScaleMode.ScaleAndCrop);
        float fade = 1f - Mathf.Clamp01((Time.unscaledTime - backgroundChangedAt) / .45f);
        if (fade > 0)
        {
            GUI.color = new Color(.08f, .03f, .06f, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    void OnGUI()
    {
        // Fixed canvas prevents dialogue and three-choice questions falling below short windows.
        GUI.matrix = Matrix4x4.identity;
        Styles();
        GUI.color = new Color(.08f, .1f, .18f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        if (memorial > 0) { Memorial(); return; }
        if (scene == 0) { StartScreen(); return; }
        if (scene == 7) { Ending(); return; }

        DrawScene();
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 900f, 1));
        float dialogueY = 530;
        GUI.color = new Color(.74f, .18f, .43f, .9f);
        GUI.DrawTexture(new Rect(40, dialogueY - 15, 1200, 365), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(65, dialogueY + 5, 1150, 30), Chapter + "   |   선택 " + ((scene - 1) * 2 + choiceStage + 1) + "/12   |   호감도 " + affection, stat);
        string[] current = FirstSemesterStory.Dialogue[scene];
        string[] line = current[Mathf.Min(dialogueIndex, current.Length - 1)].Split('|');
        GUI.Label(new Rect(85, dialogueY + 35, 1110, 40), showResult ? ProfessorName : choiceStage == 1 ? "나레이션" : line[0] == "교수" ? ProfessorName : line[0], body);
        GUI.Label(new Rect(85, dialogueY + 82, 1110, 125), showResult ? resultLine : choiceStage == 1 ? ProfessorPersonality.FollowUp(professor, scene, dialogueIndex) : line[0] == "교수" ? ProfessorPersonality.Spoken(professor, line[1]) : line[1], body);
        Choices(dialogueY);
    }

    void StartScreen()
    {
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Background, ScaleMode.ScaleAndCrop);
        GUI.color = new Color(.18f, .03f, .1f, .7f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(80, 75, Screen.width - 160, 125), lines[0], title);
        GUI.Label(new Rect(0, 215, Screen.width, 50), "낯선 캠퍼스에서 시작하는 나의 첫 학기", body);
        if (entryStep == 0)
        {
            GUI.Label(new Rect(80, 280, Screen.width - 160, 45), "체험할 학부를 선택하세요", body);
            for (int i = 0; i < faculties.Length; i++)
            {
                GUI.enabled = i == 0;
                if (GUI.Button(new Rect(80, 335 + i * 60, Screen.width - 160, 50), faculties[i] + (i == 0 ? " · 소프트웨어 전공" : " · 준비 중"), button)) entryStep = 1;
            }
            GUI.enabled = true;
            return;
        }
        if (entryStep == 1)
        {
            GUI.Label(new Rect(80, 280, Screen.width - 160, 65), "AI-SW융합학부 · 소프트웨어\n경험에 맞는 설명과 문제를 선택하세요", body);
            if (GUI.Button(new Rect(80, 365, Screen.width - 160, 65), "전공자 · 코드로 문제 풀기", button)) { experienced = true; entryStep = 2; }
            if (GUI.Button(new Rect(80, 445, Screen.width - 160, 65), "비전공자 · 쉬운 설명으로 문제 풀기", button)) { experienced = false; entryStep = 2; }
            if (GUI.Button(new Rect(80, 530, 150, 40), "이전", button)) entryStep = 0;
            return;
        }
        GUI.color = new Color(.74f, .18f, .43f, .92f); GUI.DrawTexture(new Rect(Screen.width / 2 - 410, 295, 820, 190), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(0, 315, Screen.width, 35), "공략할 교수님을 선택하세요", title);
        if (GUI.Button(new Rect(Screen.width / 2 - 380, 375, 360, 78), "김가람 교수\n엄격한 원칙파 · 근거와 준비를 중시", button)) { professor = 0; scene = 1; }
        if (GUI.Button(new Rect(Screen.width / 2 + 20, 375, 360, 78), "이명희 교수\n유쾌한 소통파 · 호기심과 협업을 중시", button)) { professor = 1; scene = 1; }
        if (GUI.Button(new Rect(80, 510, 150, 40), "이전", button)) entryStep = 1;
    }

    void Choices(float dialogueY)
    {
        if (!showResult && choiceStage == 1 && dialogueIndex < FirstSemesterStory.FollowUp[scene].Split('\n').Length - 1)
        {
            if (GUI.Button(new Rect(980, 800, 210, 50), "다음 ▶", button)) { novelAudio.Play("Next"); dialogueIndex++; }
            return;
        }
        if (!showResult && choiceStage == 0 && dialogueIndex < FirstSemesterStory.Dialogue[scene].Length - 1)
        {
            if (GUI.Button(new Rect(980, 800, 210, 50), "다음 ▶", button)) { novelAudio.Play("Next"); dialogueIndex++; }
            return;
        }
        if (showResult)
        {
            if (GUI.Button(new Rect(100, dialogueY + 205, 1080, 52), "계속", button))
            {
                showResult = false;
                novelAudio.Play("Next");
                if (choiceStage == 0) { choiceStage = 1; dialogueIndex = 0; }
                else { choiceStage = dialogueIndex = 0; scene++; }
            }
            return;
        }

        string[] available = choiceStage == 0 ? FirstSemesterStory.Choices(scene, experienced) : FirstSemesterStory.ExtraChoices(scene);
        if (scene == 3 && choiceStage == 0) GUI.Label(new Rect(100, 690, 1080, 35), experienced ? "for (int i = 0; i < 10; i--)" : "0에서 10으로 가려면 숫자를 어떻게 바꿔야 할까요?", body);
        for (int i = 0; i < available.Length; i++)
            if (GUI.Button(new Rect(100, 733 + i * 49, 1080, 43), available[i], button)) Choose(i);
    }

    void Choose(int option)
    {
        novelAudio.Play(scene == 3 && choiceStage == 0 && option == 0 ? "Bonus" : "Click");
        int delta = ProfessorPersonality.Score(professor, scene, choiceStage, option);
        if (choiceStage == 1)
        {
            if (option == 2) solo++;
            else if ((scene == 1 || scene == 2 || scene == 3 || scene == 5) && option == (scene == 2 || scene == 5 ? 1 : 0)) questions++;
            else if ((scene == 1 || scene == 2 || scene == 4 || scene == 5 || scene == 6) && option == (scene == 2 || scene == 5 ? 0 : 1)) teamwork++;
            if ((scene == 3 && option == 1) || (scene == 4 && option == 0) || (scene == 6 && option == 0)) interest++;
        }
        else
        {
            if (scene == 2 && option < 2) interest++;
            if (scene == 4 && option == 0) questions++;
            if (scene == 5 && option == 0) teamwork++;
            if (scene == 5 && option == 2) solo++;
        }
        affection += delta;
        resultLine = ProfessorPersonality.Reply(professor, scene, choiceStage, option, affection);
        if (scene == 3 && choiceStage == 0) resultLine = (option == 0 ? "정답! 호감도 보너스 +2.\n" : "오답이어도 감점은 없어요.\n") + resultLine;
        resultLine += "\n<color=#ffd2e0>호감도 " + (delta > 0 ? "+" : "") + delta + "</color>";
        showResult = true;
    }

    void Ending()
    {
        DrawScene();
        GUI.color = new Color(.74f, .18f, .43f, .9f);
        GUI.DrawTexture(new Rect(40, 70, Screen.width - 80, 525), Texture2D.whiteTexture);
        GUI.color = Color.white;
        int ending = FirstSemesterStory.ResolveEnding(affection, questions, teamwork, interest, solo);
        bool trueEnd = ending == 7;
        string end = trueEnd ? "진 엔딩 — 다음 학기도 잘 부탁드립니다" : affection >= 1 ? "일반 엔딩 — 첫 학기 수료" : "새드 엔딩 — 아직 낯선 캠퍼스";
        string text = "역할을 나눠 작업을 마쳤다. 다음 날 발표에서 프로그램은 끝까지 작동했다.\n동기: 끝났다! 밥 먹으러 가자.\n\n" + (trueEnd ? "나: 모르는 건 더 많아졌는데, 해 보고 싶은 것도 생겼어요.\n교수님: 그럼 제대로 배운 겁니다. 다음 학기도 만나요." : affection >= 1 ? "교수님: 처음 길을 잃었던 학생이 여기까지 왔네요. 다음 학기엔 후배 길도 알려 주세요." : "교수님: 어려웠던 것과 흥미 없었던 것을 구분해 보세요. 다음 학기 상담 때 이야기합시다.");
        GUI.Label(new Rect(80, 110, Screen.width - 160, 80), "ENDING " + (ending + 1) + "/8 · " + FirstSemesterStory.EndingNames[ending], title);
        GUI.Label(new Rect(130, 215, Screen.width - 260, 190), ProfessorPersonality.Ending(professor, ending), body);
        GUI.Label(new Rect(130, 380, Screen.width - 260, 30), "호감도 " + affection + " · 질문 " + questions + " · 협업 " + teamwork + " · 관심 " + interest, body);
        if (trueEnd && GUI.Button(new Rect(Screen.width / 2 - 170, 410, 340, 60), "메모리얼 보기", button)) memorial = 0.01f;
        if (GUI.Button(new Rect(Screen.width / 2 - 170, 490, 340, 60), "처음부터", button)) { scene = affection = project = entryStep = dialogueIndex = choiceStage = questions = teamwork = interest = solo = 0; professor = -1; cared = answered = showResult = false; lastChoiceGood = false; resultLine = ""; }
    }

    void Memorial()
    {
        memorial += Time.deltaTime;
        float zoom = Mathf.Lerp(.75f, 1.1f, Mathf.Clamp01(memorial / 4));
        DrawScene();
        GUI.color = new Color(.45f, .08f, .28f, .55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width * (1 - zoom) / 2, 150, Screen.width * zoom, 170), memorial < 2.5f ? "…수고했어요." : "다음 프로젝트도, 같이 할래요?", title);
        GUI.Label(new Rect(0, Screen.height - 120, Screen.width, 40), "진 엔딩 메모리얼", stat);
        if (memorial > 5 && GUI.Button(new Rect(Screen.width / 2 - 120, Screen.height - 85, 240, 50), "돌아가기", button)) memorial = 0;
    }
}
