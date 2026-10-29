using UnityEngine;

// A minigame that replaces one of the twelve fixed choices. Majors and non-majors play the same game.
// Drawn on the 1280x900 canvas; GameController shows the professor's reply once Done.
public abstract class MiniGame
{
    public bool Done;
    public int Delta;            // Affection change, 0 to 2.
    public string Summary;
    public abstract string Title { get; }
    public abstract string[][] Replies { get; }   // [professor][Delta]
    // Whether the result counts toward the chapter's ending stat.
    public virtual bool CountsForStat => Delta > 0;
    public abstract void Draw(GUIStyle title, GUIStyle body, GUIStyle button);

    static GUIStyle face;
    protected static GUIStyle Face(GUIStyle body)
    {
        if (face == null) face = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        return face;
    }
    protected static void Box(Rect rect, Color color)
    {
        GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
    }
}

public static class MiniGames
{
    public static bool Has(int chapter, int stage) => stage == 1 && (chapter == 2 || chapter == 3 || chapter == 5);
    public static string Title(int chapter) => chapter == 2 ? "용어 카드 짝맞추기" : chapter == 3 ? "반복문 타이밍 맞추기" : "연구실 커피 제조";
    public static MiniGame Create(int chapter, int professor)
    {
        switch (chapter) {
            case 2: return new CardMatch();
            case 3: return new LoopTiming();
            default: return new CoffeeOrder(professor);
        }
    }
}
