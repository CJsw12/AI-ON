using System;
using System.Threading.Tasks;
using UnityEngine;

// A three-topic lecture followed by one deterministic quiz. Network replies only supply display text.
public sealed class Lecture : MiniGame
{
    enum Screen { Topics, Lecture, WaitingForQuestion, Question, Result }

    readonly int professor;
    readonly bool experienced;
    bool aiMode;
    Story story;
    int affection;
    Screen screen;
    LectureTopic topic;
    LectureProblem problem;
    AiSwLectureLine[] script;
    int lineIndex;
    float lineStartedAt, questionDeadline, explanationDeadline;
    string typedAnswer = "", questionText, solutionText, pickedValue, pickedDisplay, mistakeText;
    bool correct, questionFailed, questionWindowClosed, aiQuestionReady;
    Task<AiProfessor.Result> questionTask, explanationTask;
    int voicedScreen = -1, voicedLine = -1;

    public Lecture(MiniGameTheme theme, int professor, bool experienced) : base(theme)
    {
        this.professor = professor;
        this.experienced = experienced;
        story = AiSwStory.Data;
        questionText = solutionText = "";
    }

    public void ConfigureAi(bool enabled, Story context, int currentAffection)
    {
        aiMode = enabled;
        story = context ?? AiSwStory.Data;
        affection = currentAffection;
    }

    public override string[][] Replies => Theme.Replies ?? AiSwLectures.Replies;
    protected override bool ShowStars => false;

    // Called by the host on Repaint; one line change starts voice once, never once per GUI event.
    public void SpeakOnce(NovelAudio audio)
    {
        if (voicedScreen == (int)screen && voicedLine == lineIndex) return;
        voicedScreen = (int)screen;
        voicedLine = lineIndex;
        string text = screen == Screen.Topics ? AiSwLectures.ChoicePrompt(professor)
            : screen == Screen.Lecture && script[lineIndex].Speaker == "교수" ? script[lineIndex].Text
            : screen == Screen.Result && correct ? AiSwLectures.Replies[professor][1] : null;
        audio.Speak(ProfessorVoice.Clips(story.Professors[professor].Voice, text));
    }

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        PollQuestion();
        PollMisconception();
        switch (screen)
        {
            case Screen.Topics: DrawTopics(); break;
            case Screen.Lecture: DrawLecture(); break;
            case Screen.WaitingForQuestion: DrawQuestionWait(); break;
            case Screen.Question: DrawQuestion(); break;
            case Screen.Result: DrawResult(); break;
        }
    }

    void DrawTopics()
    {
        NovelUI.Label(new Rect(150, 135, 980, 58), AiSwLectures.ChoicePrompt(professor), NovelUI.Round, 34, NovelUI.Cream, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(160, 200, 960, 38), "배울 주제를 고르면 강의 뒤에 확인 문제 한 개가 이어져요", NovelUI.Soft, 21, NovelUI.Alpha(NovelUI.Cream, .84f), TextAnchor.MiddleCenter);
        for (int i = 0; i < 3; i++)
        {
            LectureTopic choice = (LectureTopic)i;
            Rect card = new Rect(135 + i * 340, 295, 310, 315);
            NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .98f), 30, NovelUI.Pink, 3);
            NovelUI.Pill(card.x + 24, card.y + 24, 34, "강의 주제 " + (i + 1), NovelUI.Blush, NovelUI.Berry, 16);
            NovelUI.Label(new Rect(card.x + 18, card.y + 78, card.width - 36, 64), AiSwLectures.TopicName(choice), NovelUI.Round, 29, NovelUI.Berry, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(card.x + 28, card.y + 148, card.width - 56, 86), AiSwLectures.Description(choice), NovelUI.Soft, 20, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(card.x + 28, card.y + 250, card.width - 56, 48), "이 주제로 배울게요", true, 18)) SelectTopic(choice);
        }
        NovelUI.Label(new Rect(185, 645, 910, 42), "비전공자는 보기에서 고르고, 전공자는 답을 직접 입력해요", NovelUI.Soft, 19, NovelUI.Alpha(NovelUI.Cream, .8f), TextAnchor.MiddleCenter);
    }

    void SelectTopic(LectureTopic selected)
    {
        topic = selected;
        problem = LectureProblems.Generate(topic, experienced,
            new System.Random(unchecked(Environment.TickCount ^ GetHashCode() ^ UnityEngine.Random.Range(0, int.MaxValue))));
        script = AiSwLectures.Script(professor, topic);
        lineIndex = 0;
        lineStartedAt = Time.unscaledTime;
        typedAnswer = "";
        questionText = problem.Prompt;
        solutionText = problem.Explanation;
        questionTask = null;
        explanationTask = null;
        questionFailed = false;
        questionWindowClosed = false;
        aiQuestionReady = false;

        if (CanUseAi())
        {
            string prompt = AiProfessor.LectureQuestionPrompt(story, professor, affection, problem);
            questionTask = AiProfessor.Ask(prompt, "강의에서 다룬 개념을 확인하는 퀴즈 문장과 풀이만 만들어 주세요.");
        }
        screen = Screen.Lecture;
    }

    void DrawLecture()
    {
        NovelUI.Label(new Rect(155, 120, 970, 42), AiSwLectures.TopicName(topic) + " · 강의", NovelUI.Round, 26, NovelUI.Lemon, TextAnchor.MiddleCenter);
        AiSwLectureLine line = script[lineIndex];
        Rect card = new Rect(145, 190, 990, 395);
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 28, NovelUI.Pink);
        NovelUI.Pill(card.x + 30, card.y + 28, 38, line.Speaker == "교수" ? story.Professors[professor].Name : "동기",
            line.Speaker == "교수" ? NovelUI.DeepPink : NovelUI.Mint, Color.white, 17);

        int visible = Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - lineStartedAt) * 38f), 0, line.Text.Length);
        string displayed = visible >= line.Text.Length ? line.Text : line.Text.Substring(0, visible);
        NovelUI.Label(new Rect(card.x + 54, card.y + 104, card.width - 108, 230), displayed, NovelUI.Soft, 29, NovelUI.Berry, TextAnchor.MiddleLeft);

        bool clicked = GUI.Button(card, GUIContent.none, GUIStyle.none);
        string next = lineIndex + 1 >= script.Length ? "문제 풀기" : "다음 대사";
        bool advance = clicked || NovelUI.Button(new Rect(465, 650, 350, 62), next, true, 24);
        Event e = Event.current;
        bool keyAdvance = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
        if (keyAdvance) { e.Use(); advance = true; }
        if (!advance) return;
        if (visible < line.Text.Length)
        {
            lineStartedAt = Time.unscaledTime - line.Text.Length / 38f;
            return;
        }
        if (lineIndex + 1 < script.Length)
        {
            lineIndex++;
            lineStartedAt = Time.unscaledTime;
            return;
        }
        BeginQuestion();
    }

    void BeginQuestion()
    {
        if (aiQuestionReady && !questionFailed)
        {
            screen = Screen.Question;
            return;
        }
        if (questionTask == null || questionFailed)
        {
            UseOfflineQuestion();
            return;
        }
        questionDeadline = Time.unscaledTime + 4f;
        screen = Screen.WaitingForQuestion;
    }

    void DrawQuestionWait()
    {
        NovelUI.Card(new Rect(205, 265, 870, 290), NovelUI.Alpha(NovelUI.Cream, .98f), 34, NovelUI.Pink, 3);
        NovelUI.Pill(530, 305, 38, AiSwLectures.TopicName(topic), NovelUI.Blush, NovelUI.Berry, 18);
        NovelUI.Label(new Rect(240, 380, 800, 72), "교수님이 문제를 내는 중…", NovelUI.Round, 32, NovelUI.Berry, TextAnchor.MiddleCenter);
        NovelUI.Label(new Rect(260, 465, 760, 40), "잠시만 기다려요. 강의는 멈추지 않아요.", NovelUI.Soft, 20, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (Time.unscaledTime >= questionDeadline)
        {
            questionWindowClosed = true;
            questionTask = null;
            UseOfflineQuestion();
        }
    }

    void UseOfflineQuestion()
    {
        questionWindowClosed = true;
        questionText = problem.Prompt;
        solutionText = problem.Explanation;
        screen = Screen.Question;
    }

    void DrawQuestion()
    {
        NovelUI.Label(new Rect(155, 118, 970, 42), "강의 확인 문제 · " + AiSwLectures.TopicName(topic), NovelUI.Round, 25, NovelUI.Lemon, TextAnchor.MiddleCenter);
        Rect prompt = new Rect(150, 178, 980, 162);
        NovelUI.Card(prompt, NovelUI.Alpha(NovelUI.Cream, .98f), 25, NovelUI.Pink);
        NovelUI.Label(new Rect(prompt.x + 34, prompt.y + 22, prompt.width - 68, prompt.height - 44), questionText, NovelUI.Soft, 22, NovelUI.Berry, TextAnchor.MiddleCenter);

        if (!experienced)
        {
            for (int i = 0; i < problem.Choices.Count; i++)
            {
                LectureOption option = problem.Choices[i];
                string label = topic == LectureTopic.Algorithms ? option.Value + "회 비교" : option.Value.ToString();
                if (NovelUI.Choice(new Rect(190, 365 + i * 80, 900, 66), i + 1, label))
                {
                    Submit(option.Value.ToString(), label, option.Value == problem.Answer);
                    return;
                }
            }
            Event e = Event.current;
            int picked = e.type == EventType.KeyDown ? e.keyCode - KeyCode.Alpha1 : -1;
            if (picked < 0 || picked >= problem.Choices.Count) picked = e.type == EventType.KeyDown ? e.keyCode - KeyCode.Keypad1 : -1;
            if (picked >= 0 && picked < problem.Choices.Count)
            {
                e.Use();
                LectureOption option = problem.Choices[picked];
                string label = topic == LectureTopic.Algorithms ? option.Value + "회 비교" : option.Value.ToString();
                Submit(option.Value.ToString(), label, option.Value == problem.Answer);
            }
            return;
        }

        string hint = topic == LectureTopic.ComputerBasics
            ? "2진수로 입력 · 공백과 앞자리 0은 무시해요"
            : "숫자로 입력 · 공백과 앞자리 0은 무시해요";
        NovelUI.Label(new Rect(220, 390, 840, 38), hint, NovelUI.Soft, 18, NovelUI.Alpha(NovelUI.Cream, .88f), TextAnchor.MiddleCenter);
        Rect field = new Rect(255, 455, 560, 62);
        NovelUI.Card(field, Color.clear, 30);
        GUI.skin.settings.cursorColor = NovelUI.DeepPink;
        GUI.SetNextControlName("LectureAnswer");
        typedAnswer = GUI.TextField(field, typedAnswer, 40, NovelUI.Input);
        GUI.FocusControl("LectureAnswer");
        NovelUI.Ring(field, NovelUI.Pink, 30, 3);
        if (string.IsNullOrWhiteSpace(typedAnswer)) NovelUI.Label(new Rect(field.x + 32, field.y, field.width - 64, field.height), topic == LectureTopic.ComputerBasics ? "예: 10101" : "정답을 입력하세요", NovelUI.Soft, 20, NovelUI.Alpha(NovelUI.Plum, .68f), TextAnchor.MiddleLeft);
        bool enabled = !string.IsNullOrWhiteSpace(typedAnswer);
        GUI.enabled = enabled;
        bool submit = NovelUI.Button(new Rect(850, 455, 180, 62), "제출", true, 22);
        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "LectureAnswer";
        if (submit || enter)
        {
            if (enter) Event.current.Use();
            string normalized = LectureProblems.NormalizeInput(topic, true, typedAnswer);
            bool isCorrect = normalized != "" && normalized == problem.CorrectInput;
            string display = normalized == "" ? typedAnswer.Trim()
                : topic == LectureTopic.ComputerBasics ? normalized + " (2진수)"
                : topic == LectureTopic.Algorithms ? normalized + "회" : normalized;
            Submit(normalized == "" ? typedAnswer : normalized, display, isCorrect);
        }
        GUI.enabled = true;
    }

    void Submit(string value, string display, bool isCorrect)
    {
        pickedValue = value;
        pickedDisplay = display;
        correct = isCorrect;
        questionWindowClosed = true;
        questionTask = null;
        solutionText = string.IsNullOrEmpty(solutionText) ? problem.Explanation : solutionText;
        mistakeText = correct ? "" : OfflineMisconception();
        if (!correct && CanUseAi())
        {
            string misconception = LectureProblems.MisconceptionFor(problem, value);
            string prompt = AiProfessor.LectureMisconceptionPrompt(story, professor, affection, problem, value, misconception);
            explanationTask = AiProfessor.Ask(prompt, "이 오답의 착각을 짚고 정답 풀이를 짧게 설명해 주세요.");
            explanationDeadline = Time.unscaledTime + 3f;
        }
        screen = Screen.Result;
    }

    string OfflineMisconception()
    {
        string misconception = LectureProblems.MisconceptionFor(problem, pickedValue);
        return professor == 0
            ? "그 답이 나온 착각이에요: " + misconception
            : "아깝다! " + misconception;
    }

    void DrawResult()
    {
        Rect card = new Rect(135, 142, 1010, 610);
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .98f), 30, correct ? NovelUI.Mint : NovelUI.Pink, 3);
        NovelUI.Pill(card.x + 38, card.y + 28, 38, correct ? "정답" : "오답 풀이", correct ? NovelUI.Mint : NovelUI.DeepPink, Color.white, 18);
        string answerLabel = problem.CorrectInput + (topic == LectureTopic.ComputerBasics && experienced ? " (2진수)" : topic == LectureTopic.Algorithms ? "회" : "");
        string resultHeader = correct
            ? (professor == 0 ? "맞았어요. …핵심을 잡았네요." : "정답! 강의 퀘스트 클리어예요!")
            : "내 답: " + pickedDisplay + "    ·    정답: " + answerLabel;
        NovelUI.Label(new Rect(card.x + 40, card.y + 86, card.width - 80, 58), resultHeader, NovelUI.Round, 26, NovelUI.Berry, TextAnchor.MiddleCenter);
        float y = card.y + 168;
        if (!correct)
        {
            NovelUI.Label(new Rect(card.x + 48, y, card.width - 96, 34), "왜 그런 답을 골랐을까요?", NovelUI.Round, 20, NovelUI.DeepPink);
            NovelUI.Label(new Rect(card.x + 48, y + 42, card.width - 96, 92), mistakeText, NovelUI.Soft, 20, NovelUI.Berry);
            y += 144;
        }
        NovelUI.Label(new Rect(card.x + 48, y, card.width - 96, 34), "정답 풀이", NovelUI.Round, 20, NovelUI.DeepPink);
        float explanationHeight = Mathf.Min(190, card.yMax - y - 106);
        int fontSize = NovelUI.Height(solutionText, NovelUI.Soft, 21, card.width - 100) > explanationHeight ? 19 : 21;
        NovelUI.Label(new Rect(card.x + 48, y + 40, card.width - 96, explanationHeight), solutionText, NovelUI.Soft, fontSize, NovelUI.Berry);
        if (NovelUI.Button(new Rect(435, card.yMax - 72, 410, 52), "확인했어요 · 계속하기", true, 20))
        {
            Summary = correct ? "강의 퀴즈를 맞혔어요." : "강의 퀴즈의 오답 풀이를 확인했어요.";
            Delta = correct ? 1 : 0;
            questionTask = null;
            explanationTask = null;
            Done = true;
        }
    }

    void PollQuestion()
    {
        if (questionTask == null || !questionTask.IsCompleted) return;
        Task<AiProfessor.Result> completed = questionTask;
        questionTask = null;
        if (Done || questionWindowClosed || problem == null) return;
        try
        {
            AiProfessor.Result result = completed.Result;
            string clean = AiProfessor.Clean(result == null ? null : result.Text, 380);
            if (LectureProblems.TryParseAiResponse(problem, clean, out string generatedQuestion, out string generatedSolution))
            {
                questionText = generatedQuestion;
                solutionText = generatedSolution;
                aiQuestionReady = true;
            }
            else questionFailed = true;
        }
        catch { questionFailed = true; }
        if (screen == Screen.WaitingForQuestion)
        {
            if (!questionFailed) screen = Screen.Question;
            else UseOfflineQuestion();
        }
    }

    void PollMisconception()
    {
        if (screen != Screen.Result) return;
        if (explanationTask != null && Time.unscaledTime >= explanationDeadline)
        {
            explanationTask = null;
            return;
        }
        if (explanationTask != null && explanationTask.IsCompleted)
        {
            Task<AiProfessor.Result> completed = explanationTask;
            explanationTask = null;
            try
            {
                AiProfessor.Result result = completed.Result;
                string clean = AiProfessor.Clean(result == null ? null : result.Text, 140).Replace('\n', ' ').Trim();
                string correctValue = problem.CorrectInput;
                string picked = pickedValue.Trim();
                bool mentionsCorrect = clean.IndexOf(correctValue, StringComparison.Ordinal) >= 0;
                bool mentionsPicked = picked.Length > 0 && clean.IndexOf(picked, StringComparison.Ordinal) >= 0;
                if (clean.Length > 0 && clean.Length <= 120 && LectureProblems.IsAtMostTwoSentences(clean) && !LectureProblems.ClaimsDistractorAsCorrect(problem, clean) && mentionsCorrect && mentionsPicked) mistakeText = clean;
            }
            catch { }
            return;
        }
        if (explanationTask != null && Time.unscaledTime >= explanationDeadline) explanationTask = null;
    }

    bool CanUseAi() => aiMode && AiProfessor.Supported && AiProfessor.HasKey;
}
