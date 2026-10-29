using UnityEngine;

// Chapter 3: stop the loop counter exactly when it reaches 10; it speeds up every round.
public class LoopTiming : MiniGame
{
    static readonly float[] StepSeconds = { .16f, .11f, .08f };
    int round, hits, stopped = -1;
    float startedAt = Time.unscaledTime, resumeAt;
    string verdict = "";

    public override string Title => MiniGames.Title(3);
    public override string[][] Replies => new[] {
        new[] { "종료 조건을 놓쳤습니다. i < 10이 언제 거짓이 되는지 값을 하나씩 적어 보세요.", "한 번은 맞혔군요. 반복은 i가 10이 되는 순간 끝납니다. 다시 확인하세요.", "정확합니다. 조건이 거짓이 되는 순간을 정확히 알고 있군요." },
        new[] { "숫자가 너무 빨랐죠? 괜찮아요. 10이 결승선이라는 것만 기억하면 돼요!", "한 번은 딱 잡았네요! 숫자가 10에서 퇴근한다는 것만 기억해요.", "와, 반복문이 퇴근하는 순간을 정확히 잡았네요! 감각 최고!" }
    };
    int Current => (int)((Time.unscaledTime - startedAt) / StepSeconds[round]) % 15;

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        GUI.Label(new Rect(100, 120, 1080, 35), "for (int i = 0; i < 10; i++)      i가 10이 되는 순간 멈추세요.", body);
        GUI.Label(new Rect(100, 160, 1080, 35), "라운드 " + Mathf.Min(round + 1, 3) + "/3   ·   성공 " + hits, body);
        if (Done) { GUI.Label(new Rect(100, 300, 1080, 120), Summary, title); return; }
        if (stopped >= 0)
        {
            GUI.Label(new Rect(100, 280, 1080, 120), "i = " + stopped, title);
            GUI.Label(new Rect(100, 420, 1080, 50), verdict, Face(body));
            if (Time.unscaledTime < resumeAt) return;
            stopped = -1;
            if (++round == 3) { Finish(); return; }
            startedAt = Time.unscaledTime;
            return;
        }
        GUI.Label(new Rect(100, 280, 1080, 120), "i = " + Current, title);
        if (GUI.Button(new Rect(440, 560, 400, 90), "멈춤!", button))
        {
            stopped = Current;
            if (stopped == 10) hits++;
            verdict = stopped == 10 ? "정확히 10! 조건이 거짓이 되어 반복이 끝났어요." : stopped < 10 ? "아직 i < 10이 참이라 반복 중이에요." : "이미 10을 지나쳤어요.";
            resumeAt = Time.unscaledTime + 1.2f;
        }
    }
    void Finish()
    {
        Done = true;
        Delta = hits >= 2 ? 2 : hits;
        Summary = "3번 중 " + hits + "번 정확히 멈춤";
    }
}
