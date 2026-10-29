using System.Collections.Generic;
using UnityEngine;

// Cute, youthful look for the IMGUI screens: rounded panels, pill buttons, a heart gauge and soft Korean fonts.
// Everything is drawn on the 1280x900 canvas that GameController sets up with GUI.matrix.
public static class NovelUI
{
    public static readonly Color Pink = Hex(0xff8fb1), DeepPink = Hex(0xe0457b), Blush = Hex(0xffe3ec), Cream = Hex(0xfffaf5),
        Berry = Hex(0x6b2a45), Plum = Hex(0x8a6a78), Sky = Hex(0x8ec9ff), Mint = Hex(0x8fdcc0), Lemon = Hex(0xffe08a);
    public const string AffectionColor = "#e0457b";

    public static Font Round, Soft, Code;   // Jua for labels, Gowun Dodum for story text, Consolas for code.
    static Texture2D heart, heartOutline, glow;
    static readonly Dictionary<int, GUIStyle> shapes = new Dictionary<int, GUIStyle>();
    static GUIStyle text;

    public static void Load()
    {
        if (text != null) return;
        Round = Resources.Load<Font>("Fonts/Jua-Regular");
        Soft = Resources.Load<Font>("Fonts/GowunDodum-Regular");
        // WebGL has no OS fonts to borrow, so code falls back to the story font there.
        Code = Application.platform == RuntimePlatform.WebGLPlayer ? Soft : Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Malgun Gothic" }, 22);
        heart = Resources.Load<Texture2D>("UI/HeartFilled");
        heartOutline = Resources.Load<Texture2D>("UI/HeartOutline");
        glow = Resources.Load<Texture2D>("UI/SoftGlow");
        text = new GUIStyle { richText = true, wordWrap = true, clipping = TextClipping.Overflow };
    }
    // Uniform scale with the 1280x900 canvas centered, so circles and text never stretch on wide screens.
    static float Scale => Mathf.Min(Screen.width / 1280f, Screen.height / 900f);
    public static Matrix4x4 Canvas => Matrix4x4.TRS(new Vector3((Screen.width - 1280 * Scale) / 2, (Screen.height - 900 * Scale) / 2, 0), Quaternion.identity, new Vector3(Scale, Scale, 1));
    // The whole window in canvas coordinates (wider or taller than the canvas when letterboxed).
    public static Rect Window => new Rect(-(Screen.width - 1280 * Scale) / 2 / Scale, -(Screen.height - 900 * Scale) / 2 / Scale, Screen.width / Scale, Screen.height / Scale);
    public static void Tint(Color color) { GUI.color = color; GUI.DrawTexture(Window, Texture2D.whiteTexture); GUI.color = Color.white; }

    static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f);
    public static Color Alpha(Color c, float a) { c.a *= a; return c; }
    public static bool Repaint => Event.current.type == EventType.Repaint;

    // ---- Shapes -------------------------------------------------------------------------------

    // A 9-sliced rounded rectangle (thickness 0 = filled, otherwise an outline ring).
    public static GUIStyle Shape(int radius, int thickness = 0)
    {
        int key = radius * 100 + thickness;
        if (shapes.TryGetValue(key, out GUIStyle style)) return style;
        int size = radius * 2 + 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                Vector2 inner = new Vector2(Mathf.Clamp(p.x, radius, size - radius), Mathf.Clamp(p.y, radius, size - radius));
                float distance = Vector2.Distance(p, inner);
                float alpha = Mathf.Clamp01(radius - distance + .5f);
                if (thickness > 0) alpha *= Mathf.Clamp01(distance - (radius - thickness) + .5f);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
        texture.SetPixels(pixels);
        texture.Apply();
        style = new GUIStyle { normal = { background = texture }, border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1) };
        shapes[key] = style;
        return style;
    }
    // The 9-slice corners (radius + 1 each side) must fit inside the rect, or the slices overlap.
    static int Fit(Rect rect, float radius) => Mathf.FloorToInt(Mathf.Min(radius, (Mathf.Min(rect.width, rect.height) - 2) / 2));
    public static void Fill(Rect rect, Color color, float radius)
    {
        if (!Repaint) return;
        GUI.color = color; Shape(Mathf.Max(1, Fit(rect, radius))).Draw(rect, false, false, false, false); GUI.color = Color.white;
    }
    public static void Ring(Rect rect, Color color, float radius, int thickness)
    {
        if (!Repaint) return;
        GUI.color = color; Shape(Mathf.Max(thickness + 1, Fit(rect, radius)), thickness).Draw(rect, false, false, false, false); GUI.color = Color.white;
    }
    // Soft drop shadow plus a filled card with an optional outline.
    public static void Card(Rect rect, Color fill, float radius, Color outline = default, int thickness = 3)
    {
        Fill(new Rect(rect.x, rect.y + 6, rect.width, rect.height), new Color(.3f, .05f, .15f, .18f), radius);
        Fill(rect, fill, radius);
        if (outline.a > 0) Ring(rect, outline, radius, thickness);
    }
    public static void Heart(Rect rect, Color color, bool filled = true)
    {
        if (!Repaint) return;
        GUI.color = color; GUI.DrawTexture(rect, filled ? heart : heartOutline, ScaleMode.ScaleToFit); GUI.color = Color.white;
    }
    public static void Glow(Rect rect, Color color)
    {
        if (!Repaint) return;
        GUI.color = color; GUI.DrawTexture(rect, glow, ScaleMode.StretchToFill); GUI.color = Color.white;
    }

    // ---- Text ---------------------------------------------------------------------------------

    public static GUIStyle Text(Font font, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
    {
        text.font = font; text.fontSize = size; text.normal.textColor = color; text.alignment = anchor; text.fontStyle = style;
        return text;
    }
    public static void Label(Rect rect, string content, Font font, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
    {
        GUI.Label(rect, content, Text(font, size, color, anchor, style));
    }
    // Big title with a thick candy-coloured outline.
    public static void Outlined(Rect rect, string content, Font font, int size, Color color, Color outline, int width = 4)
    {
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8;
            Label(new Rect(rect.x + Mathf.Cos(angle) * width, rect.y + Mathf.Sin(angle) * width, rect.width, rect.height), content, font, size, outline, TextAnchor.MiddleCenter);
        }
        Label(rect, content, font, size, color, TextAnchor.MiddleCenter);
    }
    public static float Width(string content, Font font, int size) => Text(font, size, Color.white).CalcSize(new GUIContent(content)).x;
    public static float Height(string content, Font font, int size, float width) => Text(font, size, Color.white).CalcHeight(new GUIContent(content), width);

    // A rounded tag with centered text; returns its width so tags can be chained.
    public static float Pill(float x, float y, float height, string content, Color fill, Color color, int size, Font font = null)
    {
        font = font ?? Round;
        float width = Width(content, font, size) + height * .9f;
        Fill(new Rect(x, y, width, height), fill, height / 2);
        Label(new Rect(x, y, width, height), content, font, size, color, TextAnchor.MiddleCenter);
        return width;
    }

    // ---- Controls -----------------------------------------------------------------------------

    // While a menu is open the screen underneath still draws, but must not light up or play hover sounds.
    public static bool Blocked;
    public static System.Action<string> Sound;
    static Rect hovered, lastHovered;

    public static bool Hover(Rect rect)
    {
        bool hover = GUI.enabled && !Blocked && rect.Contains(Event.current.mousePosition);
        if (hover && Repaint) hovered = rect;
        return hover;
    }
    // Call once at the end of OnGUI: plays the hover sound when the pointer moves onto a new control.
    public static void EndFrame()
    {
        if (!Repaint) return;
        if (hovered != lastHovered && hovered != default) Sound?.Invoke("Hover");
        lastHovered = hovered;
        hovered = default;
    }

    static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
    public static Texture2D Icon(string name)
    {
        if (!icons.TryGetValue(name, out Texture2D icon)) icons[name] = icon = Resources.Load<Texture2D>("UI/" + name);
        return icon;
    }
    public static void DrawIcon(Rect rect, string name, Color color)
    {
        Texture2D icon = Icon(name);
        if (icon == null || !Repaint) return;
        GUI.color = color; GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit); GUI.color = Color.white;
    }

    // Round icon button with a tooltip underneath on hover; `active` shows it switched on.
    public static bool IconButton(Rect rect, string icon, string tip, bool active = false)
    {
        bool hover = Hover(rect);
        Rect face = hover ? new Rect(rect.x, rect.y - 2, rect.width, rect.height) : rect;
        Card(face, active ? DeepPink : hover ? Pink : Alpha(Cream, .95f), rect.height / 2, active || hover ? default : Pink);
        float pad = rect.height * .25f;
        DrawIcon(new Rect(face.x + pad, face.y + pad, face.width - pad * 2, face.height - pad * 2), icon, active || hover ? Color.white : DeepPink);
        if (hover && tip != null)
        {
            float width = Width(tip, Round, 16) + 28 * .9f;
            Pill(Mathf.Min(rect.center.x - width / 2, 1272 - width), rect.yMax + 6, 28, tip, Alpha(Berry, .92f), Color.white, 16);
        }
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    // A row of pill options; returns the selected index.
    public static int Segmented(Rect rect, string[] options, int value, int size = 19)
    {
        Fill(rect, Blush, rect.height / 2);
        float width = rect.width / options.Length;
        for (int i = 0; i < options.Length; i++)
        {
            Rect cell = new Rect(rect.x + i * width, rect.y, width, rect.height);
            Rect inner = new Rect(cell.x + 4, cell.y + 4, cell.width - 8, cell.height - 8);
            bool hover = Hover(cell);
            if (i == value) Card(inner, DeepPink, inner.height / 2);
            else if (hover) Fill(inner, Alpha(Pink, .35f), inner.height / 2);
            Label(cell, options[i], Round, size, i == value ? Color.white : GUI.enabled ? Berry : Alpha(Plum, .6f), TextAnchor.MiddleCenter);
            if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) value = i;
        }
        return value;
    }

    // Horizontal 0..1 slider with a heart knob; drag or click anywhere on it.
    public static float Slider(Rect rect, float value)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;
        Rect track = new Rect(rect.x + 14, rect.center.y - 7, rect.width - 28, 14);
        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (!rect.Contains(e.mousePosition) || Blocked) break;
                GUIUtility.hotControl = id;
                value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                e.Use();
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id) break;
                value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                e.Use();
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                e.Use();
                Sound?.Invoke("Toggle");   // Lets the player hear the new effects volume.
                break;
        }
        bool hover = Hover(rect) || GUIUtility.hotControl == id;
        Fill(track, Blush, 7);
        if (value > 0) Fill(new Rect(track.x, track.y, Mathf.Max(14, track.width * value), track.height), Pink, 7);
        float knob = hover ? 40 : 36;
        Rect handle = new Rect(track.x + track.width * value - knob / 2, rect.center.y - knob / 2, knob, knob);
        Card(handle, Color.white, knob / 2, DeepPink);
        Heart(new Rect(handle.x + 9, handle.y + 9, knob - 18, knob - 18), DeepPink);
        return value;
    }

    // Pink pill button (primary) or cream pill with a pink outline (secondary).
    public static bool Button(Rect rect, string content, bool primary = true, int size = 22, Font font = null)
    {
        bool hover = Hover(rect);
        Rect face = hover ? new Rect(rect.x, rect.y - 2, rect.width, rect.height) : rect;
        float radius = rect.height / 2;
        if (primary)
        {
            if (GUI.enabled) Card(face, hover ? DeepPink : Pink, radius);
            else Fill(face, new Color(.96f, .8f, .86f, .95f), radius);
            Label(face, content, font ?? Round, size, Alpha(Color.white, GUI.enabled ? 1 : .85f), TextAnchor.MiddleCenter);
        }
        else if (GUI.enabled)
        {
            Card(face, hover ? Blush : Cream, radius, Pink);
            Label(face, content, font ?? Round, size, Berry, TextAnchor.MiddleCenter);
        }
        else
        {
            Fill(face, new Color(.93f, .89f, .9f, .9f), radius);
            Label(face, content, font ?? Round, size, Alpha(Plum, .6f), TextAnchor.MiddleCenter);
        }
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    // A wide answer button with a round number badge on the left.
    public static bool Choice(Rect rect, int number, string content)
    {
        bool hover = Hover(rect);
        Rect face = hover ? new Rect(rect.x - 4, rect.y - 2, rect.width + 8, rect.height) : rect;
        float radius = rect.height / 2;
        Card(face, hover ? Pink : Alpha(Cream, .97f), radius, hover ? DeepPink : Pink);
        Rect badge = new Rect(face.x + 12, face.y + (face.height - 40) / 2, 40, 40);
        Fill(badge, hover ? Color.white : Pink, 20);
        Label(badge, number.ToString(), Round, 22, hover ? DeepPink : Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(face.x + 68, face.y, face.width - 96, face.height), content, Soft, 22, hover ? Color.white : Berry, TextAnchor.MiddleLeft, FontStyle.Bold);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    // Heart icon with a pill-shaped gauge; pulse (0..1) makes the heart pop after a change.
    public static void HeartGauge(Rect rect, int value, int max, float pulse)
    {
        Card(rect, Alpha(Cream, .95f), rect.height / 2, Pink);
        float size = rect.height + 10 + pulse * 14;
        Rect icon = new Rect(rect.x - 10 - pulse * 7, rect.y + (rect.height - size) / 2, size, size);
        Glow(new Rect(icon.x - 12, icon.y - 12, icon.width + 24, icon.height + 24), Alpha(Pink, .35f + pulse * .4f));
        Heart(icon, value < 0 ? Plum : DeepPink);
        Rect track = new Rect(rect.x + rect.height + 8, rect.y + rect.height / 2 - 7, rect.width - rect.height - 64, 14);
        Fill(track, Blush, 7);
        float ratio = Mathf.Clamp01(value / (float)max);
        if (ratio > 0) Fill(new Rect(track.x, track.y, Mathf.Max(14, track.width * ratio), track.height), DeepPink, 7);
        Label(new Rect(track.xMax + 6, rect.y, 46, rect.height), value.ToString(), Round, 22, value < 0 ? Plum : DeepPink, TextAnchor.MiddleCenter);
    }

    // Styles handed to minigames (see MINIGAME_GUIDE.md): white text on the dark minigame panel and pink buttons.
    public static GUIStyle MiniTitle, MiniBody, MiniButton, Input;
    public static void MakeStyles()
    {
        if (MiniTitle != null) return;
        MiniTitle = new GUIStyle { font = Soft, fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = Color.white } };
        MiniBody = new GUIStyle { font = Soft, fontSize = 21, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true, normal = { textColor = Color.white } };
        MiniButton = new GUIStyle { font = Soft, fontSize = 18, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true, border = new RectOffset(19, 19, 19, 19) };
        MiniButton.normal.background = Tinted(18, Pink);
        MiniButton.hover.background = MiniButton.active.background = Tinted(18, Blush);
        MiniButton.normal.textColor = MiniButton.hover.textColor = MiniButton.active.textColor = Berry;
        Input = new GUIStyle { font = Code, fontSize = 26, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(28, 28, 0, 0), border = new RectOffset(31, 31, 31, 31), clipping = TextClipping.Clip };
        Input.normal.background = Input.focused.background = Input.hover.background = Tinted(30, Cream);
        Input.normal.textColor = Input.focused.textColor = Input.hover.textColor = Berry;
    }
    // A rounded texture baked in one colour, for GUIStyles that can't be tinted with GUI.color.
    static Texture2D Tinted(int radius, Color color)
    {
        Texture2D source = Shape(radius).normal.background;
        Color[] pixels = source.GetPixels();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(color.r, color.g, color.b, pixels[i].a);
        Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
