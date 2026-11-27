#if UNITY_5_3_OR_NEWER
using System;
using System.Net;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

sealed class CodeTutorPractice : MiniGame
{
    static readonly string[] Commands = { "테스트", "첫 번째", "두 번째", "세 번째", "수정", "시범", "다음", "제출" };
    static readonly int[][] Cases = { new[] { 8 }, new[] { 4, 8, 12 }, new int[0] };
    static readonly string[] CaseLabels = { "[8]", "[4, 8, 12]", "빈 입력" };
    readonly string professor;
    int stage, selected, demoStep, requestStage;
    bool corrected, tested, demo, microphone, ready;
    float began;
    static GUIStyle tutorInput;
    string question = "", phoneError = "";
    string feedback = "", explanation = "", micStatus = "";
    public bool WantsIme { get; private set; }
    bool demoRequest;
    int demoTest = 1;
    Task<AiProfessor.Result> request;
    TutorPhone phone;
    Texture2D phoneQr;
    bool showPhone;
    float pairingStarted;
    string Lesson => stage == 0 ? "1/2 · 여러 점수의 평균 고치기" : "2/2 · 입력 개수가 바뀌어도 맞는 평균";
    string Goal => stage == 0
        ? "점수 [4, 8, 12]의 평균은 8입니다. 왜 이 코드가 4를 내는지 찾아보세요."
        : "점수가 [8] 하나라면 평균은 8입니다. 점수 개수가 바뀌어도 맞게 고쳐 보세요.";

    static Texture2D MakeQr(string address)
    {
        using (var data = QRCoder.QRCodeGenerator.GenerateQrCode(address, QRCoder.QRCodeGenerator.ECCLevel.M))
        {
            var rows = data.ModuleMatrix;
            int size = rows.Count;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[(size - 1 - y) * size + x] = rows[y][x]
                        ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
            var qr = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            qr.SetPixels32(pixels);
            qr.Apply(false, true);
            return qr;
        }
    }

    void ShowPhone()
    {
        OpenPhone();
        if (phone == null) return;
        if (phoneQr == null)
        {
            try { phoneQr = MakeQr(phone.Address); }
            catch (Exception e) { Debug.LogWarning("Tutor QR: " + e.Message); }
        }
        pairingStarted = Now;
        showPhone = true;
    }

    void DrawPairing(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        GUI.Label(new Rect(150, 185, 980, 50), "휴대폰으로 풀이 보기", title);
        GUI.Label(new Rect(160, 240, 960, 52), "PC와 같은 네트워크에서 휴대폰 카메라로 QR을 스캔하거나 아래 주소를 열어 주세요.", body);
        if (phoneQr != null)
        {
            Color oldColor = GUI.color;
            GUI.color = Color.white;
            int size = phoneQr.width * Mathf.Max(5, Mathf.FloorToInt(300f / phoneQr.width));
            GUI.DrawTexture(new Rect(640 - size / 2, 292, size, size), phoneQr, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }
        else GUI.Label(new Rect(200, 370, 880, 60), "QR을 만들 수 없어요. 아래 주소로 접속할 수 있어요.", body);
        GUI.SetNextControlName("TutorAddress");
        GUI.TextField(new Rect(180, 608, 765, 43), phone.Address);
        if (GUI.Button(new Rect(955, 608, 145, 43), "주소 복사", button))
            GUIUtility.systemCopyBuffer = phone.Address;
        if (GUI.Button(new Rect(480, 690, 320, 50), "실습으로 돌아가기", button))
        {
            began += Now - pairingStarted;
            showPhone = false;
            GUI.FocusControl(null);
        }
    }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    KeywordRecognizer recognizer;
#endif
    volatile string pendingCommand;

    public CodeTutorPractice(MiniGameTheme theme, string professor) : base(theme) { this.professor = professor; }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "실행 결과를 다시 확인해요.", "원인을 찾았군요.", "예외 입력까지 확인했네요." },
        new[] { "테스트를 하나 더 해 볼까요?", "좋아요. 원인을 찾았어요!", "예외 입력까지 확인했네요!" } };

    // The two exercises execute only their fixed arithmetic rules.
    static string Run(int[] values, int exercise, bool fixedCode)
    {
        if (values.Length == 0) return "입력 없음";
        int sum = 0;
        foreach (int value in values)
        {
            if (exercise == 0 && !fixedCode) sum = 0;
            sum += value;
        }
        int divisor = exercise == 1 && !fixedCode ? 3 : values.Length;
        return (sum / (float)divisor).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    string Code => stage == 0
        ? corrected ? "합계 = 0\n각 점수에 대해:\n    합계 += 점수\n평균 = 합계 / 점수 개수"
                    : "합계 = 0\n각 점수에 대해:\n    합계 = 0\n    합계 += 점수\n평균 = 합계 / 점수 개수"
        : "합계 = 모든 점수의 합\n평균 = 합계 / " + (corrected ? "점수 개수" : "3");

    void DoTest()
    {
        tested = true;
        int[] input = Cases[selected];
        feedback = $"{CaseLabels[selected]} → 결과 {Run(input, stage, corrected)} / 기대값 {Run(input, stage, true)}";
    }

    void Ask(string text, bool showDemo)
    {
        if (request != null) return;
        if (showDemo) { demo = true; demoStep = 0; demoTest = 1; }
        demoRequest = showDemo;
        requestStage = stage;
        explanation = "AI 조교가 답변을 준비하고 있어요.";
        request = AiProfessor.Ask(
            $"당신은 {professor} 역할의 컴퓨터공학 실습 조교다. 한국어 두 문장으로만 답한다. " +
            (showDemo ? "첫 줄은 TEST=0 또는 TEST=1 또는 TEST=2만 쓴다. 0은 [8], 1은 [4,8,12], 2는 빈 입력이다. 반복문에서 합계를 초기화하는 오류가 드러나는 테스트를 선택한다. 그 다음 줄에 이유를 쓴다. " : "테스트 결과와 평균의 원리를 설명한다. ") +
            "빈 입력은 코드 밖에서 처리하며 변형 문제의 답은 직접 알려 주지 않는다.",
            $"문제 단계: {stage + 1}\n현재 코드:\n{Code}\n실행 결과: {feedback}\n학생 질문: {text}");
    }

    void NextStep()
    {
        if (!demo || stage != 0 || (demoRequest && request != null)) return;
        if (demoStep == 0) { selected = demoTest; DoTest(); }
        else if (demoStep == 1) { corrected = true; DoTest(); }
        else return;
        demoStep++;
    }

    void Submit()
    {
        if (!tested || !corrected) { feedback = "테스트를 실행한 뒤 코드 수정을 선택해 보세요."; return; }
        foreach (int[] input in Cases)
            if (Run(input, stage, corrected) != Run(input, stage, true)) return;
        if (stage == 1)
        {
            Delta = 2;
            Summary = "평균 계산의 두 오류를 수정하고 정상·경계 입력 테스트를 통과했습니다.";
            Done = true;
            Release();
            return;
        }
        stage++;
        selected = 0;
        corrected = tested = demo = false;
        demoStep = 0;
        explanation = "";
        feedback = "변형 문제입니다. 점수가 한 개일 때도 올바른지 확인해 보세요.";
    }

    void StartMic()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            recognizer = new KeywordRecognizer(Commands);
            recognizer.OnPhraseRecognized += OnPhrase;
            recognizer.Start();
            micStatus = "음성 명령을 듣고 있어요.";
        }
        catch (Exception)
        {
            StopMic();
            micStatus = "마이크 인식 시작에 실패했어요. Windows 음성 언어와 마이크 권한을 확인해 주세요.";
        }
#else
        micStatus = "이 환경에서는 Windows 음성 명령을 사용할 수 없어요.";
#endif
    }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    void OnPhrase(PhraseRecognizedEventArgs args) => pendingCommand = args.text;
#endif
    void StopMic()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (recognizer != null)
        {
            recognizer.OnPhraseRecognized -= OnPhrase;
            if (recognizer.IsRunning) recognizer.Stop();
            recognizer.Dispose();
            recognizer = null;
        }
#endif
        pendingCommand = null;
    }

    void OpenPhone()
    {
        if (phone != null) return;
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().OrderByDescending(a => a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.GetIPProperties().GatewayAddresses.Count == 0 ||
                    (adapter.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                     adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet)) continue;
                foreach (var entry in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (entry.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(entry.Address) || entry.Address.ToString().StartsWith("169.254.")) continue;
                    phone = new TutorPhone(entry.Address.ToString());
                    return;
                }
            }
            phoneError = "Wi-Fi 또는 유선 네트워크에 연결해 주세요.";
        }
        catch (Exception) { phoneError = "휴대폰 연결을 열지 못했어요. PC에서 계속 실습할 수 있어요."; }
    }

    public void Release()
    {
        StopMic();
        phone?.Dispose();
        phone = null;
        if (phoneQr != null) UnityEngine.Object.Destroy(phoneQr);
        phoneQr = null;
    }

    protected override void OnSkip()
    {
        Release();
        Summary = "실습을 건너뛰었습니다.";
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (Done) { GUI.Label(new Rect(150, 280, 980, 120), Summary, body); return; }
        GUI.Label(new Rect(130, 115, 1020, 36), professor, title);
        if (!ready)
        {
            GUI.Label(new Rect(130, 185, 1020, 110), "점수 평균 계산기 고치기\n학습 목표: 반복문에서 변수 값이 변하는 과정과 입력 개수에 맞는 계산을 이해합니다.\n입력 선택 → 실행 결과 비교 → 원인 찾기 → 수정 → 다른 입력으로 검증", body);
            if (GUI.Button(new Rect(380, 320, 520, 60), "키보드·마우스로 플레이", button))
            { ready = true; began = Now; }

            return;
        }
        if (showPhone)
        {
            phone.Publish($"{professor}\n{Lesson}\n{Goal}\n\n{feedback}\n{explanation}");
            DrawPairing(title, body, button);
            return;
        }
        if (Now - began >= 60f)
        {
            Done = true;
            Delta = stage;
            Summary = "실습 시간이 끝났습니다.";
            Release();
            return;
        }
        Event input = Event.current;
        if (!microphone && input.type == EventType.KeyDown && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
        {
            switch (input.keyCode)
            {
                case KeyCode.Alpha1: selected = 0; input.Use(); break;
                case KeyCode.Alpha2: selected = 1; input.Use(); break;
                case KeyCode.Alpha3: selected = 2; input.Use(); break;
                case KeyCode.T: DoTest(); input.Use(); break;
                case KeyCode.R: corrected = !corrected; DoTest(); input.Use(); break;
                case KeyCode.Return: Submit(); input.Use(); break;
            }
        }
        if (Event.current.type == EventType.Repaint)
        {
            if (request != null && request.IsCompleted)
            {
                var result = request.GetAwaiter().GetResult();
                string reply = result.Text?.Trim();
                if (demoRequest && !string.IsNullOrEmpty(reply))
                {
                    int newline = reply.IndexOf('\n');
                    if (newline > 0 && reply.Substring(0, newline).Trim() == "TEST=1" &&
                        Run(Cases[1], 0, false) != Run(Cases[1], 0, true))
                    {
                        demoTest = 1;
                        reply = reply.Substring(newline + 1).Trim();
                    }
                    else reply = null;
                }
                explanation = string.IsNullOrWhiteSpace(reply)
                    ? "AI 연결이 어려워 기본 설명을 표시합니다. 여러 입력과 한 개 입력을 비교하고, 합계와 입력 개수를 확인해 보세요."
                    : reply.Substring(0, Math.Min(reply.Length, 350));
                if (requestStage != stage) explanation = "";
                request = null;
            }
            if (phone != null && phone.TryRead(out string fromPhone))
            {
                if (fromPhone == "/next") NextStep();
                else Ask(fromPhone, false);
            }
            if (pendingCommand != null)
            {
                string command = pendingCommand;
                pendingCommand = null;
                switch (command)
                {
                    case "테스트": DoTest(); break;
                    case "첫 번째": selected = 0; break;
                    case "두 번째": selected = 1; break;
                    case "세 번째": selected = 2; break;
                    case "수정": corrected = !corrected; DoTest(); break;
                    case "시범": if (stage == 0 && !demo && tested) Ask("평균 계산 오류의 풀이 과정을 설명해 주세요.", true); break;
                    case "다음": NextStep(); break;
                    case "제출": Submit(); break;
                }
            }
            phone?.Publish($"{professor}\n{Lesson}\n{Goal}\n\n{feedback}\n\n{explanation}\n\n시범 {demoStep}/2");
        }
        if (Done) return;
        GUI.Label(new Rect(130, 155, 1020, 36), $"{Lesson}   {Mathf.CeilToInt(60f - (Now - began))}초", body);
        GUI.Label(new Rect(145, 196, 980, 54), Goal, body);
        GUI.Label(new Rect(145, 255, 500, 155), Code, body);
        GUI.Label(new Rect(650, 250, 495, 55), tested ? feedback : "입력 선택 → 테스트 → 수정 → 제출", body);
        for (int i = 0; i < CaseLabels.Length; i++)
            if (GUI.Button(new Rect(650 + 155*i, 306, 145, 42), (selected == i ? "✓ " : "") + CaseLabels[i], button)) selected = i;
        if (GUI.Button(new Rect(650, 356, 145, 44), "테스트 실행", button)) DoTest();
        if (GUI.Button(new Rect(805, 356, 145, 44), corrected ? "수정 취소" : stage == 0 ? "초기화 옮기기" : "나누는 수 수정", button)) { corrected = !corrected; DoTest(); }
        if (GUI.Button(new Rect(960, 356, 145, 44), "검증 제출", button)) Submit();

        if (stage == 0 && tested && !demo &&
            GUI.Button(new Rect(145, 425, 220, 46), "AI 조교 시범 보기", button))
            Ask("평균 계산 오류의 풀이 과정을 설명해 주세요.", true);
        if (demo && demoStep < 2 && (!demoRequest || request == null) && GUI.Button(new Rect(380, 425, 260, 46),
                demoStep == 0 ? "1. 오답 재현" : "2. 초기화 위치 수정", button)) NextStep();
        GUI.Label(new Rect(145, 470, 965, 55), explanation, body);

        NovelUI.Label(new Rect(145, 525, 965, 24), "조교에게 질문하면 질문과 실습 상태가 AI 서버로 전송돼요.", NovelUI.Soft, 15, NovelUI.Cream);
        GUI.SetNextControlName("TutorQuestion");
        Rect questionRect = new Rect(145, 552, 740, 34);
        if (tutorInput == null)
        {
            tutorInput = new GUIStyle(NovelUI.Input);
            tutorInput.normal.background = tutorInput.focused.background = tutorInput.hover.background = null;
            tutorInput.padding = new RectOffset(12, 12, 4, 4);
            tutorInput.fontSize = 20;
            tutorInput.alignment = TextAnchor.MiddleLeft;
        }
        NovelUI.Card(questionRect, NovelUI.Cream, 12);
        question = GUI.TextField(questionRect, question, 160, tutorInput);
        WantsIme = GUI.GetNameOfFocusedControl() == "TutorQuestion";
        if (WantsIme)
        {
            Input.imeCompositionMode = IMECompositionMode.On;
            Input.compositionCursorPos = GUIUtility.GUIToScreenPoint(new Vector2(questionRect.x, questionRect.yMax));
        }
        if (GUI.Button(new Rect(900, 552, 210, 34), "조교에게 질문", button) &&
            request == null && !string.IsNullOrWhiteSpace(question))
        {
            Ask((question + Input.compositionString).Trim(), false);
            question = "";
        }
        if (GUI.Button(new Rect(145, 595, 250, 43), "휴대폰 주소 · QR", button)) ShowPhone();
        if (phone == null && !string.IsNullOrEmpty(phoneError))
            GUI.Label(new Rect(410, 595, 710, 50), phoneError, body);
        string trace = stage == 0
            ? (corrected ? "[4,8,12] 추적: 합계 4 → 12 → 24, 평균 24÷3=8. 합계를 반복문 밖에서 한 번만 초기화합니다."
                         : "[4,8,12] 추적: 합계가 매번 지워져 4 → 8 → 12만 남습니다. 평균 12÷3=4. 왜 틀렸을까요?")
            : "[8] 추적: 합계 8, 입력 개수 1. 평균은 8÷1=8이어야 합니다. 3으로 나누면 어떤 결과가 될까요?";
        GUI.Label(new Rect(145, 642, 970, 35), trace, body);
        if (!microphone) GUI.Label(new Rect(145, 675, 970, 50), "키보드: 1~3 테스트 선택 · T 실행 · R 수정 · Enter 제출", body);
        if (microphone)
            GUI.Label(new Rect(145, 675, 970, 75),
                micStatus + "\n명령: 테스트 · 첫 번째 · 두 번째 · 세 번째 · 수정 · 시범 · 다음 · 제출", body);
    }
}
#endif