using UnityEngine;

// The two gaze minigames outside the gaze test:
// - 미니게임 > 하이라이트 tab: free practice of 눈빛 맞추기 / 눈빛 피하기 (no 호감도 change), back to the menu after.
// - Story events, shared by every faculty:
//   눈빛 맞추기 once, after the first slot result that leaves 호감도 at 4 or more;
//   눈빛 피하기 after the 12th slot (before the ending) when 호감도 is 8 or more.
//   Each game's result adds 0..2 호감도. Without a calibrated eye tracker the player is led through calibration
//   first (the gaze test screen starts the game once ready) and can skip the event instead.
public partial class GameController
{
    enum GazeGame { None, Match, Dodge }

    bool practiceHighlight;
    bool matchEventDone, dodgeEventDone, dodgePending;
    GazeGame storyGazeEvent;        // an event waiting on its intro card
    GazeGame gazeSession;           // a game launched from here (highlight or story), followed until it closes
    bool gazeSessionStory, gazeSessionSawResult, gazeSessionSawGame;
    int gazeSessionGain;

    bool storyGazeSession => gazeSession != GazeGame.None && gazeSessionStory;

    const int MatchEventAffection = 4, DodgeEventAffection = 8;
    // 눈빛 피하기 is endless (three catches end it), so its 호감도 comes from how long the player held out.
    const float DodgeGoodSeconds = 15f, DodgeGreatSeconds = 30f;

    void HighlightTab(Rect card)
    {
        NovelUI.Label(new Rect(card.x, card.y + 176, card.width, 30), "시선으로 하는 교수님과의 특별한 순간", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (Tile(new Rect(card.x + 50, card.y + 220, card.width - 100, 148), "눈빛 맞추기", "분홍 점을 시선으로 따라가요.\n스토리 등장: 호감도 4 이상", false))
            StartGazeGame(GazeGame.Match, false);
        if (Tile(new Rect(card.x + 50, card.y + 386, card.width - 100, 148), "눈빛 피하기", "다가오는 빨간 눈빛을 피해요.\n스토리 등장: 마지막 장면 · 호감도 8 이상", false))
            StartGazeGame(GazeGame.Dodge, false);
        NovelUI.Label(new Rect(card.x + 40, card.y + 560, card.width - 80, 66), "웹캠이나 iPhone 시선 보정이 필요해요.\n보정이 안 돼 있으면 보정 화면부터 열려요.", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleCenter);
    }

    // Opens the gaze test screen; the game starts right away when the tracker is ready, otherwise once calibrated.
    void StartGazeGame(GazeGame game, bool story)
    {
        novelAudio.Play("Select");
        menu = Menu.None;
        gazeSession = game;
        gazeSessionStory = story;
        gazeSessionSawResult = gazeSessionSawGame = false;
        gazeSessionGain = 0;
        bool alreadyOpen = gazeTestOpen;
        if (!alreadyOpen) OpenGazeTest();
        if (GazeReadyForDodge)
        {
            if (game == GazeGame.Match) StartEyeMatchGame(); else StartDodgeGame();
            return;
        }
        if (game == GazeGame.Match) matchAfterCalibration = true; else dodgeAfterCalibration = true;
        if (gazeTracker == null && !requestingCamera && !gazeUseIphone) gazeCameraRequest = StartCoroutine(StartGazeCamera());
    }

    // Called after a slot's result is dismissed (Advance).
    void QueueStoryGazeEvents(bool lastSlot)
    {
        if (lastSlot && !dodgeEventDone && affection >= DodgeEventAffection) { dodgeEventDone = true; dodgePending = true; }
        if (!matchEventDone && affection >= MatchEventAffection) { matchEventDone = true; storyGazeEvent = GazeGame.Match; return; }
        if (dodgePending) { dodgePending = false; storyGazeEvent = GazeGame.Dodge; }
    }

    // Intro card over the scene: the professor invites the player; start or skip.
    void DrawStoryGazeIntro()
    {
        if (scene >= 1 && scene <= 7) DrawScene(false);
        GUI.matrix = Canvas;
        NovelUI.Tint(new Color(1f, .82f, .9f, .35f));
        bool match = storyGazeEvent == GazeGame.Match;
        Rect card = new Rect(290, 250, 700, 400);
        NovelUI.Card(card, NovelUI.Alpha(NovelUI.Cream, .97f), 36, NovelUI.Pink);
        NovelUI.Pill(card.x + 40, card.y + 34, 40, "EVENT", NovelUI.Pink, Color.white, 18);
        NovelUI.Label(new Rect(card.x, card.y + 80, card.width, 50), match ? "눈빛 맞추기" : "눈빛 피하기", NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
        // Both games are filmed with 심한뫼 교수님, so with any other professor he appears as a special guest.
        bool sim = professor >= 0 && ProfessorName.Contains("심한뫼");
        string line = match ? "...잠깐, 제 눈을 한번 봐 줄래요?" : "마지막으로 하나만요. 제 눈빛, 피할 수 있겠어요?";
        string who = sim ? ProfessorName : "특별 출연 · 심한뫼 교수님";
        if (!sim) line = (match ? "옆 연구실 심한뫼 교수님이 불쑥 찾아오셨다. " : "엔딩 직전, 심한뫼 교수님이 다시 나타나셨다. ") + "\"" + line + "\"";
        else line = "\"" + line + "\"";
        NovelUI.Label(new Rect(card.x + 40, card.y + 140, card.width - 80, 100), $"{who}\n{line}", NovelUI.Soft, 21, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
        NovelUI.Label(new Rect(card.x + 40, card.y + 246, card.width - 80, 30), "결과에 따라 호감도 +0~2 · 시선 보정이 필요해요", NovelUI.Soft, 17, NovelUI.Plum, TextAnchor.MiddleCenter);
        if (NovelUI.Button(new Rect(card.x + 90, card.yMax - 96, 250, 60), "시작하기", true, 22))
        {
            GazeGame game = storyGazeEvent;
            storyGazeEvent = GazeGame.None;
            StartGazeGame(game, true);
        }
        if (NovelUI.Button(new Rect(card.xMax - 340, card.yMax - 96, 250, 60), "건너뛰기", false, 21))
        {
            novelAudio.Play("Back");
            storyGazeEvent = GazeGame.None;
            NextStoryGazeEvent();
        }
    }

    void NextStoryGazeEvent()
    {
        if (dodgePending) { dodgePending = false; storyGazeEvent = GazeGame.Dodge; }
    }

    // Follows a launched game: remembers its result, and when the player leaves (돌아가기 on the result card, or
    // closing the gaze test) applies the story 호감도 and returns to the story or the minigame menu.
    void TickGazeGameSession()
    {
        if (gazeSession == GazeGame.None) return;
        bool match = gazeSession == GazeGame.Match;
        if (match ? matchFinished : dodgeFinished)
        {
            gazeSessionSawResult = true;
            gazeSessionGain = match ? Mathf.Clamp(matchAffectionGain, 0, 2) : dodgeAlive >= DodgeGreatSeconds ? 2 : dodgeAlive >= DodgeGoodSeconds ? 1 : 0;
        }
        bool open = match ? MatchScreenOpen : DodgeScreenOpen;
        if (open) gazeSessionSawGame = true;
        // Left: the gaze test closed, or the game screen closed after it opened (result card or 게임 끝내기).
        bool left = !gazeTestOpen || (gazeSessionSawGame && !open);
        if (!left) return;

        bool story = gazeSessionStory;
        int gain = gazeSessionSawResult ? gazeSessionGain : 0;
        gazeSession = GazeGame.None;
        gazeSessionStory = false;
        matchAfterCalibration = dodgeAfterCalibration = false;
        if (gazeTestOpen)
        {
            // Story eye-contact tracking shares the tracker, so only hide the screen while a session is running.
            if (eyeContactSessionActive) gazeTestOpen = false;
            else CloseGazeTest();
        }
        if (story)
        {
            if (gain > 0)
            {
                affection += gain;
                ShowToast($"호감도 +{gain}");
            }
            NextStoryGazeEvent();
        }
        else OpenMenu(Menu.Practice);
    }
}
