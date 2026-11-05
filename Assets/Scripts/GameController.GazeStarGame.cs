using UnityEngine;

// "시선 별잡기": look at a star and hold your gaze on it to catch it. Works with the webcam (UnitEye) and the iPhone.
// Every catch is also a free calibration check: the player was certainly looking at the star, so the gap between
// the pointer and the star corrects slow drift (posture, phone or laptop moved) while they play.
public partial class GameController
{
    const float StarGameDuration = 30f;
    const float StarCountdown = 3f;
    const float StarDwell = .65f;
    const float StarLiveSeconds = .35f;      // a catch only counts while samples keep arriving
    const float StarHitRadius = 165f;
    const float StarLeaveRadius = 215f;     // once inside, a jittery pointer may wander this far before it counts as leaving
    const float StarMinJump = 320f;
    const float StarQuickCatch = 1.2f;      // caught this soon after it appeared → bonus point
    const float StarMoveAfter = 10f;        // stars start drifting after this many seconds
    const string StarBestPref = "GazeTest.StarBestScore";
    static readonly Rect StarArea = new Rect(200f, 290f, 880f, 400f);

    bool starGameActive, starGameFinished, starInside, starNewBest;
    float starStartedAt, starDwell, starLastTick, starSpawnedAt, starPopAt = -100f;
    int starScore, starCaught, starBest = -1, starPopPoints;
    Vector2 starTarget, starVelocity, starPopPos, starGazeSum;
    float starGazeSquareSum;
    long starLastAnchorSequence;
    int starGazeCount;

    bool StarScreenOpen => starGameActive || starGameFinished;

    /// <summary>The gaze point on the 1280×900 canvas from whichever tracker is active.</summary>
    bool StarGaze(out Vector2 point, out bool blinking)
    {
        if (gazeUseIphone)
        {
            point = iphoneCanvasGaze;
            blinking = iphoneBlinking;
            // Scoring needs a live stream: the source counts a packet as fresh for 1 s, longer than a 0.65 s catch,
            // so a pointer frozen on a star after the connection drops must not keep scoring.
            return iphoneSource != null && iphoneMarkerValid && Time.unscaledTime - iphoneLastLiveAt <= StarLiveSeconds;
        }
        point = default;
        blinking = false;
        // A slow webcam model may space samples further apart than StarLiveSeconds; allow ~2.5 frames, still under a catch.
        float live = Mathf.Min(StarDwell * .9f, Mathf.Max(StarLiveSeconds, 2.5f / Mathf.Max(1, webcamRate)));
        if (gazeTracker == null || !gazeTracker.IsUserPresent || lastGazeSequence == 0 || Time.unscaledTime - lastGazeAt > live) return false;
        point = WebcamCanvasGaze;
        blinking = webcamBlinking;   // only real blinks: a long half-closure is a look at the bottom of the screen
        return !webcamFitted || webcamMarkerValid;
    }

    void StartStarGame()
    {
        dodgeAfterCalibration = false;   // the player chose the star game instead of the title's 눈빛 피하기
        if (starBest < 0) starBest = PlayerPrefs.GetInt(StarBestPref, 0);
        starGameActive = true;
        starGameFinished = starInside = starNewBest = false;
        starStartedAt = Time.unscaledTime + StarCountdown;
        starLastTick = Time.unscaledTime;
        starDwell = 0f;
        starPopAt = -100f;
        starScore = starCaught = 0;
        SpawnStar(new Vector2(640f, 490f));
        starSpawnedAt = starStartedAt;
    }

    void StopStarGame()
    {
        if (gazeTracker != null) gazeTracker.RemovePursuitTarget("star");
        starGameActive = starGameFinished = false;
        StopDodgeGame();   // every place that stops the gaze games (tracker stop, recalibration) also ends 눈빛 피하기
    }

    void SpawnStar(Vector2 from)
    {
        Vector2 next = from;
        for (int i = 0; i < 12; i++)
        {
            next = new Vector2(Random.Range(StarArea.xMin, StarArea.xMax), Random.Range(StarArea.yMin, StarArea.yMax));
            if ((next - from).sqrMagnitude >= StarMinJump * StarMinJump) break;
        }
        starTarget = next;
        starVelocity = Random.insideUnitCircle.normalized;
        starSpawnedAt = Time.unscaledTime;
        starDwell = 0f;
        starInside = false;
        starGazeSum = Vector2.zero;
        starGazeSquareSum = 0f;
        starGazeCount = 0;
        starLastAnchorSequence = -1;
        // A new star pops up: the webcam tracker treats a gaze that settles on it as a light drift anchor.
        if (!gazeUseIphone && gazeTracker != null)
        {
            gazeTracker.RemovePursuitTarget("star");
            gazeTracker.ReportAttentionEvent(new Vector2(next.x / 1280f, next.y / 900f));
        }
    }

    void TickStarGame()
    {
        if (!starGameActive) return;
        float now = Time.unscaledTime;
        float dt = Mathf.Min(now - starLastTick, .1f);
        starLastTick = now;
        if (now < starStartedAt) return;   // countdown
        float elapsed = now - starStartedAt;
        if (elapsed >= StarGameDuration) { FinishStarGame(); return; }

        // Later stars drift and bounce, a little faster in the last third.
        if (elapsed > StarMoveAfter)
        {
            float speed = elapsed > StarGameDuration * 2f / 3f ? 95f : 60f;
            starTarget += starVelocity * speed * dt;
            if (starTarget.x < StarArea.xMin || starTarget.x > StarArea.xMax) starVelocity.x = -starVelocity.x;
            if (starTarget.y < StarArea.yMin || starTarget.y > StarArea.yMax) starVelocity.y = -starVelocity.y;
            starTarget = new Vector2(Mathf.Clamp(starTarget.x, StarArea.xMin, StarArea.xMax), Mathf.Clamp(starTarget.y, StarArea.yMin, StarArea.yMax));
            // Following a moving star is smooth pursuit, which UnitEye can also use to correct drift.
            if (!gazeUseIphone && gazeTracker != null)
                gazeTracker.FeedPursuitTarget("star", new Vector2(starTarget.x / 1280f, starTarget.y / 900f));
        }

        bool valid = StarGaze(out Vector2 gaze, out bool blinking);
        // The pointer is frozen while the eyes are closed, so a blink neither adds nor drains progress.
        if (blinking) return;
        // Entering needs the hit radius; leaving needs the wider one, so jitter at the edge doesn't break a catch.
        float radius = starInside ? StarLeaveRadius : StarHitRadius;
        starInside = valid && (gaze - starTarget).sqrMagnitude <= radius * radius;
        if (starInside)
        {
            starDwell += dt / StarDwell;
            long sequence = gazeUseIphone ? iphoneSource.Sequence : lastGazeSequence;
            if (elapsed <= StarMoveAfter && sequence != starLastAnchorSequence)
            {
                Vector2 miss = gaze - starTarget;
                starGazeSum += miss;
                starGazeSquareSum += miss.sqrMagnitude;
                starGazeCount++;
                starLastAnchorSequence = sequence;
            }
        }
        else
        {
            starDwell = Mathf.Max(0f, starDwell - dt * 1.5f / StarDwell);
            starGazeSum = Vector2.zero;
            starGazeSquareSum = 0f;
            starGazeCount = 0;
        }
        if (starDwell < 1f) return;

        bool quick = now - starSpawnedAt <= StarQuickCatch;
        starPopPoints = quick ? 2 : 1;
        starScore += starPopPoints;
        starCaught++;
        starPopAt = now;
        starPopPos = starTarget;
        if (elapsed <= StarMoveAfter && starGazeCount >= 3 && (gazeUseIphone || webcamFitted))
        {
            Vector2 miss = starGazeSum / starGazeCount;
            float variance = Mathf.Max(0f, starGazeSquareSum / starGazeCount - miss.sqrMagnitude);
            // A stationary target with stable, fresh samples is a safer inferred anchor.
            if (variance <= 45f * 45f)
                (gazeUseIphone ? iphoneMapper : webcamMapper).AddAnchor(new Vector2(-miss.x / 1280f, -miss.y / 900f));
        }
        NovelUI.Sound?.Invoke(quick ? "Game/Coin|Up" : "Game/Correct|Up");
        SpawnStar(starTarget);
    }

    void FinishStarGame()
    {
        starGameActive = false;
        starGameFinished = true;
        if (gazeTracker != null) gazeTracker.RemovePursuitTarget("star");
        starNewBest = starScore > starBest;
        if (starNewBest)
        {
            starBest = starScore;
            PlayerPrefs.SetInt(StarBestPref, starBest);
            PlayerPrefs.Save();
        }
        NovelUI.Sound?.Invoke(starNewBest ? "Unlock" : "Chapter");
    }

    void DrawStarGame()
    {
        NovelUI.Fill(new Rect(0, 0, 1280, 900), NovelUI.Cream, 0);
        NovelUI.Label(new Rect(0, 42, 1280, 56), "시선 별잡기", NovelUI.Round, 34, NovelUI.Berry, TextAnchor.MiddleCenter);
        if (starGameFinished)
        {
            NovelUI.Card(new Rect(340, 220, 600, 350), NovelUI.Alpha(Color.white, .9f), 28, NovelUI.Alpha(NovelUI.DeepPink, .35f));
            NovelUI.Label(new Rect(340, 248, 600, 80), $"{starScore}점", NovelUI.Round, 56, NovelUI.DeepPink, TextAnchor.MiddleCenter);
            NovelUI.Label(new Rect(340, 324, 600, 30), $"★ 별 {starCaught}개", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(340, 358, 600, 40), starNewBest ? "새 최고 기록이에요!" : $"최고 기록 {starBest}점",
                NovelUI.Soft, 22, starNewBest ? NovelUI.Berry : NovelUI.Plum, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(340, 400, 600, 30), "빨리 잡으면 2점! 자꾸 빗나가면 다시 보정해 보세요.", NovelUI.Soft, 16, NovelUI.Plum, TextAnchor.MiddleCenter);
            if (NovelUI.Button(new Rect(370, 460, 250, 62), "한 판 더", true, 23)) StartStarGame();
            if (NovelUI.Button(new Rect(660, 460, 250, 62), "돌아가기", false, 23)) starGameFinished = false;
            return;
        }

        float now = Time.unscaledTime;
        bool valid = StarGaze(out Vector2 gaze, out _);
        float remaining = Mathf.Clamp(StarGameDuration - (now - starStartedAt), 0f, StarGameDuration);
        NovelUI.Label(new Rect(80, 115, 330, 54), $"점수  {starScore}", NovelUI.Round, 26, NovelUI.Plum, TextAnchor.MiddleLeft, FontStyle.Bold);
        NovelUI.Label(new Rect(870, 115, 330, 54), $"남은 시간  {Mathf.CeilToInt(remaining)}초", NovelUI.Round, 26, NovelUI.Plum, TextAnchor.MiddleRight, FontStyle.Bold);
        NovelUI.Fill(new Rect(80, 172, 1120, 8), NovelUI.Alpha(NovelUI.Plum, .14f), 4);
        NovelUI.Fill(new Rect(80, 172, 1120 * remaining / StarGameDuration, 8), remaining < 6f ? NovelUI.DeepPink : NovelUI.Pink, 4);
        if (starBest > 0)
            NovelUI.Label(new Rect(440, 120, 400, 44), $"최고 기록 {starBest}점", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);

        if (now < starStartedAt)
        {
            float left = starStartedAt - now;
            float beat = 1f - (left % 1f);
            NovelUI.Label(new Rect(290, 290, 700, 60), "별을 바라보고 잠깐 머물면 잡아요!", NovelUI.Soft, 24, NovelUI.Berry, TextAnchor.MiddleCenter, FontStyle.Bold);
            NovelUI.Label(new Rect(290, 338, 700, 36), "빨리 잡으면 2점 · 10초 뒤부터 별이 움직여요", NovelUI.Soft, 18, NovelUI.Plum, TextAnchor.MiddleCenter);
            int size = Mathf.RoundToInt(Mathf.Lerp(150f, 110f, beat));
            NovelUI.Label(new Rect(440, 400, 400, 200), Mathf.CeilToInt(left).ToString(), NovelUI.Round, size, NovelUI.Alpha(NovelUI.DeepPink, 1f - beat * .5f), TextAnchor.MiddleCenter);
            if (valid) DrawIPhoneGazePointer(gaze, .55f);
            if (NovelUI.Button(new Rect(1030, 820, 200, 48), "게임 끝내기", false, 18)) StopStarGame();
            return;
        }

        Vector2 t = starTarget;
        float dwell = Mathf.Clamp01(starDwell);
        float wobble = starInside ? Mathf.Sin(now * 22f) * 4f * dwell : Mathf.Sin(now * 3f) * 3f;
        float glow = 98f + wobble;
        NovelUI.Fill(new Rect(t.x - glow, t.y - glow, glow * 2, glow * 2), NovelUI.Alpha(NovelUI.Lemon, starInside ? .85f : .55f), glow);
        // Catch progress fills the glow from the centre outward.
        if (dwell > 0f)
        {
            float r = 98f * dwell;
            NovelUI.Fill(new Rect(t.x - r, t.y - r, r * 2, r * 2), NovelUI.Alpha(NovelUI.DeepPink, .28f), r);
        }
        // The quick-catch window shows as a shrinking ring.
        float quickLeft = 1f - (now - starSpawnedAt) / StarQuickCatch;
        if (quickLeft > 0f)
        {
            float r = 104f + 40f * quickLeft;
            NovelUI.Ring(new Rect(t.x - r, t.y - r, r * 2, r * 2), NovelUI.Alpha(NovelUI.Berry, .35f * quickLeft), r, 3);
        }
        NovelUI.Ring(new Rect(t.x - 104, t.y - 104, 208, 208), NovelUI.Alpha(NovelUI.DeepPink, starInside ? .7f : .25f), 104, 4);
        int starSize = Mathf.RoundToInt(124 + 16 * dwell);
        NovelUI.Label(new Rect(t.x - 100, t.y - 104, 200, 200), "★", NovelUI.Round, starSize, NovelUI.DeepPink, TextAnchor.MiddleCenter);

        float pop = (now - starPopAt) / .55f;
        if (pop >= 0f && pop < 1f)
        {
            float r = Mathf.Lerp(60f, 170f, pop);
            Vector2 p = starPopPos;
            NovelUI.Ring(new Rect(p.x - r, p.y - r, r * 2, r * 2), NovelUI.Alpha(NovelUI.DeepPink, 1f - pop), r, 6);
            string text = starPopPoints > 1 ? "빨라요! +2" : "+1";
            NovelUI.Label(new Rect(p.x - 120, p.y - 70 - 60 * pop, 240, 60), text, NovelUI.Round, 40, NovelUI.Alpha(NovelUI.Berry, 1f - pop), TextAnchor.MiddleCenter);
        }

        if (valid) DrawIPhoneGazePointer(gaze, .55f);
        else NovelUI.Label(new Rect(290, 760, 700, 40), gazeUseIphone ? "시선을 잃었어요 · 휴대폰 화면에 얼굴이 보이게 해 주세요" : "시선을 잃었어요 · 웹캠에 얼굴이 보이게 해 주세요",
            NovelUI.Soft, 18, NovelUI.DeepPink, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (NovelUI.Button(new Rect(1030, 820, 200, 48), "게임 끝내기", false, 18)) StopStarGame();
    }
}
