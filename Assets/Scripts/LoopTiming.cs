using UnityEngine;

// A three-level timing exercise; only the scored attempt contributes to affection.
public class LoopTiming : MiniGame
{
    enum State { Instructions, Countdown, Running, Result, Completed }
    State state = State.Instructions;
    int round, attempt, hits, value, stopped;
    float stateAt, nextStepAt;
    string verdict = "";
    readonly string header, rule, counter, running, stopButton, instructions, hit, near, miss;
    readonly string[] levelNames;
    public LoopTiming(MiniGameTheme theme) : base(theme)
    {
        header = theme.Header ?? "LOOP DEBUGGER";
        rule = theme.Rule ?? "for (int i = 0; i < 10; i++)    // i가 10이면 조건 종료";
        counter = theme.Counter ?? "i = {0}";
        running = theme.Running ?? "반복 진행 중 · 10에서 정지";
        stopButton = theme.Button ?? "●  Break Point";
        instructions = theme.Instructions ?? "숫자가 10이 되는 순간 정지하세요.\n처음 한 번 연습한 뒤 세 단계에 도전합니다.\n마지막 단계는 8부터 숫자가 숨겨집니다.";
        hit = theme.Hit ?? "Breakpoint Hit! · 정확히 10입니다.";
        near = theme.Near ?? "±1 타이밍 오차 · 정지 지점에 가까웠어요.";
        miss = theme.Miss ?? "Breakpoint Missed · 정지 지점을 놓쳤어요.";
        levelNames = theme.Levels ?? new[] { "일정한 속도", "점점 빨라지는 속도", "숫자 숨기기" };
    }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "종료 조건을 다시 살펴보세요. 숫자가 10이 되면 i < 10은 거짓입니다.", "한 번은 맞혔군요. 조건이 거짓이 되는 순간을 다시 확인하세요.", "정확합니다. 조건이 거짓이 되는 순간을 잘 파악했군요." },
        new[] { "괜찮아요! 10이 되면 조건이 거짓이 된다는 것을 기억해요.", "한 번은 딱 잡았네요! 한 번씩 차근차근 살펴봐요.", "와, 반복이 끝나는 순간을 정확히 잡았네요!" }
    };
    float Interval(int n) => round == 0 ? 1f / 3f : round == 1 ? (n < 6 ? .28f : n < 9 ? .20f : .15f) : .25f;
    void BeginCountdown() { state = State.Countdown; stateAt = Now; }
    void Stop(int n)
    {
        stopped = n;
        if (n == 10) { verdict = hit; if (attempt == 1) hits++; }
        else if (n == 9 || n == 11) verdict = near;
        else verdict = miss;
        state = State.Result; stateAt = Now;
        NovelUI.Sound?.Invoke(n == 10 ? "Up" : "Down");
    }
    void Next()
    {
        if (attempt == 0) attempt = 1;
        else round++;
        if (round >= 3) Finish(); else BeginCountdown();
    }
    void Finish()
    {
        state = State.Completed; Done = true; Delta = hits >= 2 ? 2 : hits;
        Summary = "본 도전 3번 중 " + hits + "번 정확히 멈춤";
    }
    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        float now = Now;
        if (state == State.Countdown && now - stateAt >= 3f) { state = State.Running; value = 0; nextStepAt = now + Interval(0); }
        if (state == State.Running)
        {
            while (now >= nextStepAt && state == State.Running)
            {
                value++; NovelUI.Sound?.Invoke("LoopTick");
                if (value >= 12) Stop(value); else nextStepAt += Interval(value);
            }
        }
        float shake = state == State.Running && value >= 8 && GameSettings.Effects ? Mathf.Sin(now * 45f) * 1.5f : 0;
        NovelUI.Card(new Rect(130 + shake, 150, 1020, 560), new Color(.08f,.1f,.15f), 18, NovelUI.Pink, 2);
        GUI.Label(new Rect(155, 170, 950, 35), header + "  ·  " + (Done ? "완료" : (round + 1) + "/3  " + levelNames[round]) + "  ·  " + (attempt == 0 ? "연습" : "본 도전"), body);
        GUI.Label(new Rect(155, 220, 960, 38), rule, body);
        if (state == State.Instructions)
        {
            GUI.Label(new Rect(175, 300, 930, 130), instructions, body);
            if (GUI.Button(new Rect(440, 550, 400, 75), "시작하기", button)) BeginCountdown();
        }
        else if (state == State.Countdown)
            GUI.Label(new Rect(170, 330, 940, 100), "준비  " + Mathf.Max(1, 3 - Mathf.FloorToInt(now - stateAt)), title);
        else if (state == State.Running)
        {
            bool blind = round == 2 && value >= 8 && value <= 11;
            GUI.Label(new Rect(170, 310, 940, 100), string.Format(counter, blind ? "??" : value.ToString()), title);
            GUI.Label(new Rect(170, 415, 940, 45), (value % 2 == 0 ? "●" : "○") + " " + running, Face(body));
            if (GUI.Button(new Rect(440, 535, 400, 85), stopButton, button)) Stop(value);
        }
        else if (state == State.Result)
        {
            GUI.Label(new Rect(170, 300, 940, 90), string.Format(counter, stopped), title);
            GUI.Label(new Rect(170, 415, 940, 80), verdict, Face(body));
            // The verdict stays until the player moves on, so there is time to read why.
            string next = attempt == 0 ? "본 도전 시작" : round >= 2 ? "결과 보기" : "다음 단계";
            if (GUI.Button(new Rect(440, 535, 400, 85), next, button)) Next();
        }
        else GUI.Label(new Rect(170, 330, 940, 100), Summary, title);
    }
}
