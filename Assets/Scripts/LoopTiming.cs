using UnityEngine;

// A three-level timing exercise; only the scored attempt contributes to affection.
public class LoopTiming : MiniGame
{
    enum State { Instructions, Countdown, Running, Result, Completed }
    State state = State.Instructions;
    int round, attempt, hits, value, stopped;
    float stateAt, nextStepAt;
    string verdict = "";
    static readonly string[] LevelNames = { "일정한 속도", "점점 빨라지는 속도", "숫자 숨기기" };
    public override string Title => MiniGames.Title(3);
    public override string[][] Replies => new[] {
        new[] { "종료 조건을 다시 살펴보세요. 숫자가 10이 되면 i < 10은 거짓입니다.", "한 번은 맞혔군요. 조건이 거짓이 되는 순간을 다시 확인하세요.", "정확합니다. 조건이 거짓이 되는 순간을 잘 파악했군요." },
        new[] { "괜찮아요! 10이 되면 조건이 거짓이 된다는 것을 기억해요.", "한 번은 딱 잡았네요! 한 번씩 차근차근 살펴봐요.", "와, 반복이 끝나는 순간을 정확히 잡았네요!" }
    };
    float Interval(int n) => round == 0 ? 1f / 3f : round == 1 ? (n < 6 ? .28f : n < 9 ? .20f : .15f) : .25f;
    void BeginCountdown() { state = State.Countdown; stateAt = Time.unscaledTime; }
    void Stop(int n)
    {
        stopped = n;
        if (n == 10) { verdict = "Breakpoint Hit! · 정확히 10입니다."; if (attempt == 1) hits++; }
        else if (n == 9 || n == 11) verdict = "±1 타이밍 오차 · 정지 지점에 가까웠어요.";
        else verdict = "Breakpoint Missed · 정지 지점을 놓쳤어요.";
        state = State.Result; stateAt = Time.unscaledTime;
        NovelUI.Sound?.Invoke(n == 10 ? "Up" : "Down");
    }
    void Finish()
    {
        state = State.Completed; Done = true; Delta = hits >= 2 ? 2 : hits;
        Summary = "본 도전 3번 중 " + hits + "번 정확히 멈춤";
    }
    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        float now = Time.unscaledTime;
        if (state == State.Countdown && now - stateAt >= 3f) { state = State.Running; value = 0; nextStepAt = now + Interval(0); }
        if (state == State.Running)
        {
            while (now >= nextStepAt && state == State.Running)
            {
                value++; NovelUI.Sound?.Invoke("LoopTick");
                if (value >= 12) Stop(value); else nextStepAt += Interval(value);
            }
        }
        if (state == State.Result && now - stateAt >= 1.5f)
        {
            if (attempt == 0) attempt = 1;
            else { round++; }
            if (round >= 3) Finish(); else BeginCountdown();
        }
        float shake = state == State.Running && value >= 8 ? Mathf.Sin(now * 45f) * 1.5f : 0;
        NovelUI.Card(new Rect(130 + shake, 150, 1020, 560), new Color(.08f,.1f,.15f), 18, NovelUI.Pink, 2);
        GUI.Label(new Rect(155, 170, 950, 35), "LOOP DEBUGGER  ·  " + (Done ? "완료" : (round + 1) + "/3  " + LevelNames[round]) + "  ·  " + (attempt == 0 ? "연습" : "본 도전"), body);
        GUI.Label(new Rect(155, 220, 960, 38), "for (int i = 0; i < 10; i++)    // i가 10이면 조건 종료", body);
        if (state == State.Instructions)
        {
            GUI.Label(new Rect(175, 300, 930, 130), "숫자가 10이 되는 순간 정지하세요.\n처음 한 번 연습한 뒤 세 단계에 도전합니다.\n마지막 단계는 8부터 숫자가 숨겨집니다.", body);
            if (GUI.Button(new Rect(440, 550, 400, 75), "시작하기", button)) BeginCountdown();
        }
        else if (state == State.Countdown)
            GUI.Label(new Rect(170, 330, 940, 100), "준비  " + Mathf.Max(1, 3 - Mathf.FloorToInt(now - stateAt)), title);
        else if (state == State.Running)
        {
            bool blind = round == 2 && value >= 8 && value <= 11;
            GUI.Label(new Rect(170, 310, 940, 100), "i = " + (blind ? "??" : value.ToString()), title);
            GUI.Label(new Rect(170, 415, 940, 45), (value % 2 == 0 ? "●" : "○") + " 반복 진행 중 · 10에서 정지", Face(body));
            if (GUI.Button(new Rect(440, 535, 400, 85), "●  Break Point", button)) Stop(value);
        }
        else if (state == State.Result)
        {
            GUI.Label(new Rect(170, 300, 940, 90), "i = " + stopped, title);
            GUI.Label(new Rect(170, 415, 940, 80), verdict, Face(body));
        }
        else GUI.Label(new Rect(170, 330, 940, 100), Summary, title);
    }
}
