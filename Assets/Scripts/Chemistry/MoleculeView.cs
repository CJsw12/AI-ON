using UnityEngine;

// IMGUI 3D 분자 렌더러: MoleculeGeometry의 투영·깊이 정렬 결과를 NovelUI 위에 그린다.
// OnGUI 프레임마다 new GUIStyle을 할당하지 않으며, 모든 선 회전은 NovelUI.RotateAround만 사용한다.
public static class MoleculeView
{
    public const float AutoRotateSpeedDegrees = 28f;
    public const float DefaultTiltDegrees = (float)MoleculeGeometry.DefaultTiltDegrees;
    public const float DragDegreesPerPixel = 0.65f;
    public const string LonePairNotice = "전자쌍의 대략적인 영역(VSEPR 모형), 실제 전자 위치가 아님";

    static readonly Color AtomHColor = new Color(0.96f, 0.97f, 0.99f, 1f);
    static readonly Color AtomCColor = new Color(0.38f, 0.40f, 0.46f, 1f);
    static readonly Color AtomNColor = new Color(0.24f, 0.50f, 0.92f, 1f);
    static readonly Color AtomOColor = new Color(0.88f, 0.24f, 0.26f, 1f);
    static readonly Color AtomFallbackColor = new Color(0.55f, 0.55f, 0.60f, 1f);
    static readonly Color AtomDarkText = new Color(0.16f, 0.18f, 0.24f, 1f);
    static readonly Color AtomDarkRing = new Color(0.24f, 0.26f, 0.34f, 0.85f);
    static readonly Color BondBaseColor = new Color(0.76f, 0.80f, 0.88f, 1f);
    static readonly Color LonePairLobeColor = new Color(1.00f, 0.88f, 0.45f, 0.34f);
    static readonly Color LonePairDotColor = new Color(1.00f, 0.95f, 0.72f, 0.92f);
    static readonly Color AngleArcColor = new Color(0.56f, 0.90f, 0.80f, 0.95f);
    static readonly Color AngleTextBg = new Color(0.10f, 0.12f, 0.18f, 0.82f);

    static float s_dragLastX;

    // 분자·영역별 재사용 버퍼 슬롯(결과 갤러리 정지 썸네일 5개 + 메인 회전 뷰 등 GC 없는 재사용).
    sealed class CachedSlot
    {
        public string MoleculeId;
        public Rect Area;
        public MoleculeGeometry.SceneBuffer Buffer;
        public int LastUsedFrame;
    }

    const int MaxCacheSlots = 16;
    static readonly CachedSlot[] s_cache = new CachedSlot[MaxCacheSlots];
    static int s_cacheCount;
    static readonly System.Collections.Generic.Dictionary<(string, int, bool), double[]> s_bounds = new System.Collections.Generic.Dictionary<(string, int, bool), double[]>();

    static MoleculeGeometry.SceneBuffer AcquireBuffer(MoleculeGeometry.MoleculeModel model, Rect area)
    {
        int frame = Time.frameCount;
        int oldestIndex = 0;
        int oldestFrame = int.MaxValue;

        for (int i = 0; i < s_cacheCount; i++)
        {
            CachedSlot slot = s_cache[i];
            if (slot.MoleculeId == model.Id && slot.Area == area)
            {
                slot.LastUsedFrame = frame;
                return slot.Buffer;
            }
            if (slot.LastUsedFrame < oldestFrame)
            {
                oldestFrame = slot.LastUsedFrame;
                oldestIndex = i;
            }
        }

        CachedSlot target;
        if (s_cacheCount < MaxCacheSlots)
        {
            target = new CachedSlot();
            s_cache[s_cacheCount++] = target;
        }
        else
        {
            target = s_cache[oldestIndex];
        }

        target.MoleculeId = model.Id;
        target.Area = area;
        if (target.Buffer == null || target.Buffer.Model != model)
        {
            target.Buffer = new MoleculeGeometry.SceneBuffer(model);
        }
        else
        {
            target.Buffer.Invalidate();
        }
        target.LastUsedFrame = frame;
        return target.Buffer;
    }

    // 마우스 좌클릭 드래그로 Y축 회전 각(yawDegrees)을 조절한다. 드래그 중이면 true를 반환한다.
    public static bool HandleDrag(Rect area, ref float yawDegrees, float sensitivity = DragDegreesPerPixel)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;
        if (e == null) return false;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button != 0 || !GUI.enabled || NovelUI.Blocked || !area.Contains(e.mousePosition)) break;
                GUIUtility.hotControl = id;
                s_dragLastX = e.mousePosition.x;
                e.Use();
                return true;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id) break;
                float dx = e.mousePosition.x - s_dragLastX;
                s_dragLastX = e.mousePosition.x;
                yawDegrees += dx * sensitivity;
                e.Use();
                return true;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                e.Use();
                return false;
        }

        return GUIUtility.hotControl == id;
    }

    public static void Draw(Rect area, string moleculeId, int[] bondOrders, float yawDegrees, bool showLonePairs, bool showAngle)
    {
        Draw(area, moleculeId, bondOrders, yawDegrees, (float)MoleculeGeometry.InitialTiltDegrees(moleculeId), showLonePairs, showAngle);
    }

    public static void Draw(Rect area, string moleculeId, int[] bondOrders, float yawDegrees, float tiltDegrees, bool showLonePairs, bool showAngle)
    {
        // 장면 투영·정렬은 Repaint 이벤트에서만 실행한다(Layout·마우스 이벤트에서 불필요한 계산·할당 방지).
        if (!NovelUI.Repaint) return;
        if (area.width <= 4f || area.height <= 4f) return;
        MoleculeGeometry.MoleculeModel model;
        if (!MoleculeGeometry.TryGet(moleculeId, out model)) return;

        bool compact = area.height < 180f || area.width < 180f;
        float footerReserve = (showAngle && !compact && !string.IsNullOrEmpty(model.AngleNote)) ? 24f : 0f;
        float usableW = area.width;
        float usableH = Mathf.Max(20f, area.height - footerReserve);
        yawDegrees += (float)MoleculeGeometry.InitialYawDegrees(moleculeId);
        float centerX = area.x + usableW * 0.5f;
        float centerY = area.y + usableH * 0.5f;

        MoleculeGeometry.SceneBuffer buffer = AcquireBuffer(model, area);
        // 회전 중 크기·위치가 출렁이지 않게, 한 바퀴(15° 간격) 전체의 원자·전자쌍 경계로 배율을 정하고 캐시한다.
        // 경계는 scale=1 투영 단위라 area와 무관하고, 결합 차수는 원자 좌표를 바꾸지 않는다. 기울기는 정수로 묶어 캐시가 늘지 않게 한다.
        var boundsKey = (moleculeId, Mathf.RoundToInt(tiltDegrees), showLonePairs);
        double[] b;
        if (!s_bounds.TryGetValue(boundsKey, out b))
        {
            b = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue };
            for (int step = 0; step < 24; step++)
            {
                MoleculeGeometry.BuildSceneInto(buffer, bondOrders, step * 15.0, tiltDegrees, 0, 0, 1,
                    MoleculeGeometry.DefaultCameraDistance, 0.20, showLonePairs);
                for (int i = 0; i < buffer.Items.Count; i++)
                {
                    MoleculeGeometry.DrawItem item = buffer.Items[i];
                    if (item.Kind == MoleculeGeometry.DrawItemKind.Bond) continue;
                    b[0] = System.Math.Min(b[0], item.X - item.RadiusPx); b[1] = System.Math.Max(b[1], item.X + item.RadiusPx);
                    b[2] = System.Math.Min(b[2], item.Y - item.RadiusPx); b[3] = System.Math.Max(b[3], item.Y + item.RadiusPx);
                }
            }
            s_bounds[boundsKey] = b;
        }
        double minX = b[0], maxX = b[1], minY = b[2], maxY = b[3];
        double scale = System.Math.Min(usableW * 0.75 / (maxX - minX), usableH * 0.75 / (maxY - minY));
        double baseAtomRadius = scale * 0.20;
        centerX -= (float)((minX + maxX) * 0.5 * scale);
        centerY -= (float)((minY + maxY) * 0.5 * scale);
        MoleculeGeometry.BuildSceneInto(
            buffer,
            bondOrders,
            yawDegrees,
            tiltDegrees,
            centerX,
            centerY,
            scale,
            MoleculeGeometry.DefaultCameraDistance,
            baseAtomRadius,
            showLonePairs);

        for (int i = 0; i < buffer.Items.Count; i++)
        {
            MoleculeGeometry.DrawItem item = buffer.Items[i];
            if (item.Kind == MoleculeGeometry.DrawItemKind.Bond)
            {
                DrawBond(item);
            }
            else if (item.Kind == MoleculeGeometry.DrawItemKind.LonePair)
            {
                DrawLonePair(item);
            }
            else
            {
                DrawAtom(item, compact);
            }
        }

        if (showAngle && buffer.Angles.Length > 0)
        {
            for (int i = 0; i < buffer.Angles.Length; i++)
            {
                DrawAngle(buffer.Angles[i], i, compact);
            }
            if (footerReserve > 0f)
            {
                NovelUI.Label(
                    new Rect(area.x + 8f, area.yMax - footerReserve, area.width - 16f, footerReserve),
                    model.AngleNote,
                    NovelUI.Soft,
                    14,
                    NovelUI.Alpha(NovelUI.Cream, 0.85f),
                    TextAnchor.MiddleCenter);
            }
        }
    }

    static Color AtomBaseColor(string element)
    {
        if (element == "H") return AtomHColor;
        if (element == "C") return AtomCColor;
        if (element == "N") return AtomNColor;
        if (element == "O") return AtomOColor;
        return AtomFallbackColor;
    }

    static Color Shade(Color baseColor, double brightness)
    {
        float b = Mathf.Clamp((float)brightness, 0.52f, 1.08f);
        return new Color(
            Mathf.Clamp01(baseColor.r * b),
            Mathf.Clamp01(baseColor.g * b),
            Mathf.Clamp01(baseColor.b * b),
            baseColor.a);
    }

    static void DrawAtom(MoleculeGeometry.DrawItem item, bool compact)
    {
        float r = (float)item.RadiusPx;
        if (r < 3f) return;
        float x = (float)item.X;
        float y = (float)item.Y;
        Rect rect = new Rect(x - r, y - r, r * 2f, r * 2f);

        Color fill = Shade(AtomBaseColor(item.Element), item.Brightness);
        NovelUI.Fill(rect, fill, r);

        // 은은한 입체 하이라이트(좌상단)
        if (r >= 9f)
        {
            float hr = r * 0.36f;
            float hx = x - r * 0.32f;
            float hy = y - r * 0.32f;
            NovelUI.Fill(new Rect(hx - hr, hy - hr, hr * 2f, hr * 2f), NovelUI.Alpha(Color.white, item.Element == "H" ? 0.35f : 0.24f), hr);
        }

        Color ringColor = item.Element == "H" ? AtomDarkRing : NovelUI.Alpha(Color.black, 0.32f);
        NovelUI.Ring(rect, ringColor, r, r >= 16f ? 2 : 1);
        if (item.AtomIndex == 0) NovelUI.Ring(rect, NovelUI.Lemon, r, r >= 16f ? 3 : 2);

        int fontSize = Mathf.Clamp(Mathf.RoundToInt(r * (compact ? 1.05f : 1.12f)), 10, 32);
        Color textColor = item.Element == "H" ? AtomDarkText : Color.white;
        NovelUI.Label(rect, item.Element, NovelUI.Round, fontSize, textColor, TextAnchor.MiddleCenter);
    }

    static void DrawBond(MoleculeGeometry.DrawItem item)
    {
        Vector2 start = new Vector2((float)item.StartX, (float)item.StartY);
        Vector2 end = new Vector2((float)item.EndX, (float)item.EndY);
        Vector2 delta = end - start;
        float len = delta.magnitude;
        if (len < 1f) return;

        Vector2 perp = new Vector2(-delta.y / len, delta.x / len);
        float pScale = (float)item.PerspectiveScale;
        float thickness = item.Order >= 3
            ? Mathf.Max(1.8f, 3.0f * pScale)
            : Mathf.Max(2.2f, 4.0f * pScale);
        float gap = item.Order >= 3
            ? Mathf.Max(3.6f, 5.2f * pScale)
            : Mathf.Max(4.2f, 6.2f * pScale);
        Color color = Shade(BondBaseColor, item.Brightness);

        if (item.Order <= 1)
        {
            DrawSegment(start, end, thickness, color);
        }
        else if (item.Order == 2)
        {
            Vector2 offset = perp * (gap * 0.5f);
            DrawSegment(start - offset, end - offset, thickness, color);
            DrawSegment(start + offset, end + offset, thickness, color);
        }
        else
        {
            Vector2 offset = perp * gap;
            DrawSegment(start - offset, end - offset, thickness, color);
            DrawSegment(start, end, thickness, color);
            DrawSegment(start + offset, end + offset, thickness, color);
        }
    }

    static void DrawLonePair(MoleculeGeometry.DrawItem item)
    {
        float r = (float)item.RadiusPx;
        if (r < 4f) return;
        float x = (float)item.X;
        float y = (float)item.Y;
        float b = Mathf.Clamp((float)item.Brightness, 0.55f, 1.0f);

        // 반투명 전자쌍 로브
        Color lobe = new Color(LonePairLobeColor.r, LonePairLobeColor.g, LonePairLobeColor.b, LonePairLobeColor.a * b);
        NovelUI.Fill(new Rect(x - r, y - r, r * 2f, r * 2f), lobe, r);
        NovelUI.Ring(new Rect(x - r, y - r, r * 2f, r * 2f), NovelUI.Alpha(LonePairDotColor, 0.42f * b), r, 1);

        // 로브 내부 전자쌍 점 2개 (축에 수직 배치)
        float px = -(float)item.DirY;
        float py = (float)item.DirX;
        float sep = r * 0.34f;
        float dotR = Mathf.Max(2f, r * 0.17f);
        Color dotColor = NovelUI.Alpha(LonePairDotColor, b);
        NovelUI.Fill(new Rect(x - px * sep - dotR, y - py * sep - dotR, dotR * 2f, dotR * 2f), dotColor, dotR);
        NovelUI.Fill(new Rect(x + px * sep - dotR, y + py * sep - dotR, dotR * 2f, dotR * 2f), dotColor, dotR);
    }

    static void DrawAngle(MoleculeGeometry.ProjectedAngle angle, int angleIndex, bool compact)
    {
        if (!angle.Visible || angle.Kind == MoleculeGeometry.AngleKind.None) return;

        Vector2 v = new Vector2((float)angle.VertexX, (float)angle.VertexY);
        Vector2 a = new Vector2((float)angle.Ax, (float)angle.Ay);
        Vector2 b = new Vector2((float)angle.Bx, (float)angle.By);
        Vector2 va = a - v;
        Vector2 vb = b - v;
        float lenA = va.magnitude;
        float lenB = vb.magnitude;

        if (angle.Kind == MoleculeGeometry.AngleKind.Straight)
        {
            DrawStraightAngle(v, a, b, (float)angle.VertexRadiusPx, compact);
            return;
        }

        string text = angle.DegreesText;
        if (lenA < 8f || lenB < 8f)
        {
            DrawAngleBadge(v + new Vector2(0f, -(float)angle.VertexRadiusPx - 18f), text, compact);
            return;
        }

        float angA = Mathf.Atan2(va.y, va.x);
        float angB = Mathf.Atan2(vb.y, vb.x);
        float sweep = ShortestAngleDelta(angA, angB);
        float minArm = Mathf.Min(lenA, lenB);
        float baseR = Mathf.Max((float)angle.VertexRadiusPx + 7f, minArm * (angleIndex == 0 ? 0.46f : 0.40f));
        float arcR = Mathf.Min(baseR, minArm * 0.72f);

        const int segments = 14;
        float thickness = compact ? 1.5f : 2.2f;
        Vector2 prev = v + new Vector2(Mathf.Cos(angA), Mathf.Sin(angA)) * arcR;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float theta = angA + sweep * t;
            Vector2 next = v + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * arcR;
            DrawSegment(prev, next, thickness, AngleArcColor);
            prev = next;
        }

        float midTheta = angA + sweep * 0.5f;
        float labelDist = Mathf.Max(arcR + (compact ? 14f : 20f), (float)angle.VertexRadiusPx + (compact ? 28f : 38f));
        Vector2 labelPos = v + new Vector2(Mathf.Cos(midTheta), Mathf.Sin(midTheta)) * labelDist;
        DrawAngleBadge(labelPos, text, compact);
    }

    static void DrawStraightAngle(Vector2 v, Vector2 a, Vector2 b, float vertexRadius, bool compact)
    {
        Vector2 axis = b - a;
        float len = axis.magnitude;
        if (len < 12f)
        {
            DrawAngleBadge(v + new Vector2(0f, -vertexRadius - 18f), "180°", compact);
            return;
        }

        Vector2 dir = axis / len;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        // 화면 위쪽(-Y)에 보조선이 놓이도록 방향을 맞춘다.
        if (perp.y > 0f) perp = -perp;

        float offset = vertexRadius + (compact ? 12f : 18f);
        float halfGuide = Mathf.Clamp(len * 0.34f, 24f, 88f);
        Vector2 mid = v + perp * offset;
        Vector2 p1 = mid - dir * halfGuide;
        Vector2 p2 = mid + dir * halfGuide;
        float thickness = compact ? 1.5f : 2.2f;

        DrawSegment(p1, p2, thickness, AngleArcColor);
        float tick = compact ? 4f : 6f;
        DrawSegment(p1 - perp * tick, p1 + perp * tick, thickness, AngleArcColor);
        DrawSegment(p2 - perp * tick, p2 + perp * tick, thickness, AngleArcColor);
        DrawAngleBadge(mid + perp * (compact ? 12f : 16f), "180°", compact);
    }

    static void DrawAngleBadge(Vector2 center, string text, bool compact)
    {
        int size = compact ? 12 : 16;
        float w = compact ? 44f : 58f;
        float h = compact ? 18f : 24f;
        Rect rect = new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
        NovelUI.Fill(rect, AngleTextBg, h * 0.5f);
        NovelUI.Label(rect, text, NovelUI.Round, size, AngleArcColor, TextAnchor.MiddleCenter);
    }

    static float ShortestAngleDelta(float fromRad, float toRad)
    {
        float delta = toRad - fromRad;
        while (delta > Mathf.PI) delta -= Mathf.PI * 2f;
        while (delta < -Mathf.PI) delta += Mathf.PI * 2f;
        return delta;
    }

    // 얇은 사각형을 NovelUI.RotateAround로 회전해 선분을 그린다. GUIUtility.RotateAroundPivot은 사용하지 않는다.
    static void DrawSegment(Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.5f || !NovelUI.Repaint) return;

        Matrix4x4 savedMatrix = GUI.matrix;
        Color savedColor = GUI.color;
        NovelUI.RotateAround(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
        GUI.color = color;
        GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.color = savedColor;
        GUI.matrix = savedMatrix;
    }
}
