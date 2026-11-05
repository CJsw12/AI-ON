using UnityEngine;

// Two-person dialogue staging, used when the player chose "내 사진 사용하기": while the professor is on stage the
// professor stands right of centre and the player's character, wearing the webcam photo as its face, stands on the
// left. Whoever is speaking is lit; the listener dims a little. Without a photo the professor stays centred.
public partial class GameController
{
    const float DuoShift = .19f;        // share of the screen width each character moves away from the centre
    const float DuoListenerDim = .58f;  // brightness of whoever is listening
    const float DuoProfessorScale = .9f; // a little smaller off-centre, so a tall portrait's head clears the HUD at the top right
    float duoFocus;                     // -1 player speaking … +1 professor speaking, eased

    // Only in the story's dialogue: minigames, the ending card and the memorial keep the single centred portrait.
    bool DuoActive => PlayerPhotoReady && miniGame == null && memorial <= 0 && scene >= 1 && scene <= 6;

    /// <summary>Moves the professor right and scales it about the bottom centre (feet stay on the floor).</summary>
    Matrix4x4 DuoProfessorMatrix
    {
        get
        {
            Vector3 pivot = new Vector3(Screen.width * .5f, Screen.height, 0);
            return Matrix4x4.Translate(new Vector3(Screen.width * DuoShift, 0, 0)) * Matrix4x4.Translate(pivot)
                * Matrix4x4.Scale(new Vector3(DuoProfessorScale, DuoProfessorScale, 1)) * Matrix4x4.Translate(-pivot);
        }
    }

    /// <summary>Eases the spotlight toward whoever is talking. The player counts as speaking while choosing an answer.</summary>
    void UpdateDuoFocus()
    {
        if (!NovelUI.Repaint) return;
        CurrentLine(out string speaker, out _);
        float target = AiHintShown ? 1f : speaker == "나" || AtChoice ? -1f : speaker == ProfessorName ? 1f : 0f;
        duoFocus = Mathf.MoveTowards(duoFocus, target, Time.unscaledDeltaTime * 5f);
    }

    float DuoBrightness(bool professorSide)
    {
        float lit = professorSide ? Mathf.Clamp01(duoFocus + 1f) : Mathf.Clamp01(1f - duoFocus);   // 1 when speaking or neutral
        return Mathf.Lerp(DuoListenerDim, 1f, lit);
    }

    // The player's body: Resources/PlayerBody.png, made by Tools/make_player_body.py, which also prints these numbers
    // (face centre, face height, lowest pixel and shape, as in PortraitFaces) for the head it cut off.
    const string PlayerBodyPicture = "PlayerBody";
    static readonly PortraitFaces.Entry PlayerBodyFace = new PortraitFaces.Entry(.5005f, .2230f, .2467f, 1f, .6667f);

    float DuoPlayerX => Canvas.inverse.MultiplyPoint3x4(new Vector3(Screen.width * (.5f - DuoShift), 0, 0)).x;

    /// <summary>The player's bust on the left (body shape plus the webcam face), drawn in canvas space; mirrors the professor's distance from the centre.</summary>
    void DrawPlayerCharacter(float alpha)
    {
        Matrix4x4 saved = GUI.matrix;
        GUI.matrix = Canvas;
        float centreX = DuoPlayerX;
        float b = DuoBrightness(false);
        float breathe = GameSettings.Effects ? Mathf.Sin(Time.unscaledTime * 1.7f) * 3f : 0f;
        Texture2D bodyPicture = Load(PlayerBodyPicture);
        if (bodyPicture != null)
        {
            // A real body (Tools/make_player_body.py) at the professors' scale, the webcam face on its neck.
            Rect rect = FaceLayout(PlayerBodyFace, StageFaceHeight, new Vector2(centreX, StageFaceY + breathe));
            DrawFigure(bodyPicture, PlayerBodyFace, rect, NovelUI.Window.yMax, StageFadeHeight, new Color(b, b, b, alpha));
            if (playerPhoto != null) DrawPlayerPhoto(centreX, b, alpha, breathe);
        }
        else
        {
            // Stand-in shapes when the body picture is missing.
            Color body = new Color(.93f * b, .64f * b, .74f * b, .92f * alpha);
            Color edge = NovelUI.Alpha(Color.white, .55f * alpha * b);
            Rect shoulders = new Rect(centreX - 175, 470 + breathe, 350, 520);
            NovelUI.Fill(new Rect(shoulders.x - 4, shoulders.y - 4, shoulders.width + 8, shoulders.height + 8), edge, 150);
            NovelUI.Fill(shoulders, body, 146);
            NovelUI.Fill(new Rect(centreX - 34, 400 + breathe, 68, 110), body, 30);
            if (playerPhoto != null) DrawPlayerPhoto(centreX, b, alpha, breathe * .6f);
            else
            {
                Rect head = new Rect(centreX - 82, 245 + breathe * .6f, 164, 180);
                NovelUI.Fill(new Rect(head.x - 4, head.y - 4, head.width + 8, head.height + 8), edge, 88);
                NovelUI.Fill(head, body, 84);
            }
        }
        float pill = NovelUI.Width("나", NovelUI.Round, 20) + 40;
        NovelUI.Pill(centreX - pill / 2, 560 + breathe, 40, "나", NovelUI.Alpha(NovelUI.Sky, alpha), NovelUI.Alpha(Color.white, alpha), 20);
        GUI.matrix = saved;
    }
}
