using UnityEngine;

// Chapter 2: match each term from the first lecture with its meaning.
public class CardMatch : MiniGame
{
    static readonly string[,] Pairs = {
        { "변수", "값을 담아 두는 이름표" }, { "반복문", "같은 일을 여러 번 실행" }, { "조건문", "참·거짓에 따라 다른 길" },
        { "자료구조", "데이터를 정리하는 방법" }, { "알고리즘", "문제를 푸는 순서" }, { "Git", "코드 변경 기록 관리" }
    };
    readonly string[] faces = new string[12];
    readonly int[] pairOf = new int[12];
    readonly bool[] matched = new bool[12];
    int first = -1, second = -1, mistakes, found;
    float hideAt;

    public CardMatch()
    {
        for (int i = 0; i < 12; i++) { pairOf[i] = i / 2; faces[i] = Pairs[i / 2, i % 2]; }
        for (int i = 11; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (faces[i], faces[j]) = (faces[j], faces[i]);
            (pairOf[i], pairOf[j]) = (pairOf[j], pairOf[i]);
        }
    }
    public override string Title => MiniGames.Title(2);
    public override string[][] Replies => new[] {
        new[] { "용어가 아직 섞여 있습니다. 정의만 외우지 말고 예제 코드에서 직접 찾아보세요.", "대부분 맞혔군요. 헷갈린 용어는 예제와 함께 다시 정리하세요.", "다음 수업 퀴즈에서 용어를 정확히 구분하더군요. 정의를 말로 설명할 수 있으면 코드도 읽힙니다." },
        new[] { "디저트 샀다면서요? 괜찮아요, 그만큼 확실히 기억나겠죠. 다음엔 이길 거예요!", "카드 게임으로 복습이라니 좋네요! 헷갈린 건 다음 수업 때 저한테 물어봐요.", "학식에서 용어 카드 했다면서요? 동기가 디저트 사게 됐다고 하던데요!" }
    };

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (second >= 0 && Time.unscaledTime >= hideAt) first = second = -1;
        GUI.Label(new Rect(100, 120, 1080, 35), "같은 개념의 용어와 뜻을 짝지으세요.   틀린 횟수 " + mistakes + "   ·   찾은 짝 " + found + "/6", body);
        for (int i = 0; i < 12; i++)
        {
            Rect rect = new Rect(110 + (i % 4) * 268, 175 + (i / 4) * 175, 250, 155);
            if (matched[i] || i == first || i == second)
            {
                Box(rect, matched[i] ? new Color(.35f, .55f, .45f, .95f) : new Color(.55f, .14f, .34f, .95f));
                GUI.Label(rect, faces[i], Face(body));
            }
            else if (GUI.Button(rect, "?", button) && !Done && second < 0) Flip(i);
        }
    }
    void Flip(int card)
    {
        if (first < 0) { first = card; return; }
        if (pairOf[first] == pairOf[card])
        {
            matched[first] = matched[card] = true; first = -1;
            if (++found == 6) Finish();
            return;
        }
        second = card; mistakes++; hideAt = Time.unscaledTime + .8f;
    }
    void Finish()
    {
        Done = true;
        Delta = mistakes <= 3 ? 2 : mistakes <= 7 ? 1 : 0;
        Summary = "6쌍 완성 · 틀린 횟수 " + mistakes;
    }
}
