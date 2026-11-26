using System;
using System.Collections.Generic;
using System.Globalization;

// 순수 C# 3D 분자 기하·회전·원근 투영·깊이 정렬 로직 (UnityEngine 비의존, PS 5.1 Add-Type 호환).
// 원자 순서 규약(0번 기준 원자)은 기획서 v0.4 3절·4.1절 및 BondPuzzle과 공유한다.
//
// 실측 결합 길이·결합각 출처 (NIST CCCBDB, https://cccbdb.nist.gov/):
// - H2O  : r(O-H)=0.9578 A (표 0.958 A), a(H-O-H)=104.4776 deg (1979Hoy/Bun:1 equilibrium)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=7732185&charge=0
// - NH3  : r(N-H)=1.0124 A (표 1.012 A), a(H-N-H)=106.67 deg, a(X-N-H)=112.15 deg (1966Herzberg)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=7664417&charge=0
// - CH4  : r(C-H)=1.0870 A, a(H-C-H)=109.4712 deg (정사면체 acos(-1/3), 1966Herzberg)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=74828&charge=0
// - CO2  : r(C=O)=1.1621 A (표 1.162 A), a(O-C-O)=180 deg (1966Herzberg)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=124389&charge=0
// - N2   : r(N#N)=1.0977 A (표 1.098 A, re, 1979HUB/HER)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=7727379&charge=0
// - O2   : r(O=O)=1.2075 A (표 1.208 A, re, 1979HUB/HER)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=7782447&charge=0
// - HCN  : r(C-H)=1.0640 A, r(C#N)=1.1560 A, a(H-C-N)=180 deg (1966Herzberg)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=74908&charge=0
// - CH2O : r(C=O)=1.2050 A, r(C-H)=1.1110 A, 직교좌표 O(0,0,1.2050) C(0,0,0) H(0,+-0.9429,-0.5876)
//          -> a(H-C-H)=116.14 deg, a(H-C-O)=121.93 deg (Gurvich/1966Herzberg)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=50000&charge=0
// - C2H4 : r(C=C)=1.3390 A, r(C-H)=1.0860 A, 직교좌표 C(0,0,+-0.6695) H(0,+-0.9289,+-1.2321)
//          -> a(H-C-H)=117.60 deg, a(H-C-C)=121.20 deg
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=74851&charge=0
// - C2H2 : r(C#C)=1.2026 A (표 1.203 A, 1998Kuc rm), r(C-H)=1.0631 A, a(H-C-C)=180 deg (1966Herzberg)
//          https://cccbdb.nist.gov/expgeom2x.asp?casno=74862&charge=0
public static class MoleculeGeometry
{
    public const double DefaultTiltDegrees = 18.0;
    public static double InitialYawDegrees(string id) { return id == "NH3" || id == "CH4" ? 30.0 : 0.0; }
    // 자동 회전(세로축 한 바퀴) 중 원자가 가장 덜 겹치는 카메라 기울기. 직선형은 축이 화면을 향해도 원자가 겹치지 않는다(MoleculeGeometry 테스트에서 전 각도 검사).
    public static double InitialTiltDegrees(string id)
    {
        switch (id)
        {
            case "CO2": case "N2": case "O2": case "HCN": case "C2H2": return 35.0;
            case "H2O": case "C2H4": return 15.0;
            case "NH3": return 60.0;
            case "CH4": return 45.0;
            case "CH2O": return 10.0;
            default: return 0.0;
        }
    }
    public const double DefaultCameraDistance = 6.5;
    public const double MinCameraDepth = 0.25;
    public const double LonePairOffsetAngstrom = 0.56;
    public const double LonePairRadiusFactor = 0.78;
    public const string TrigonalPlanarAngleNote = "이상적 120°에 가깝지만 실제로는 달라요";

    public struct Vec3
    {
        public double X;
        public double Y;
        public double Z;

        public static readonly Vec3 Zero = new Vec3(0.0, 0.0, 0.0);

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double LengthSquared()
        {
            return X * X + Y * Y + Z * Z;
        }

        public double Length()
        {
            return Math.Sqrt(LengthSquared());
        }

        public Vec3 Normalized()
        {
            double len = Length();
            if (len <= 1e-12) return Zero;
            return new Vec3(X / len, Y / len, Z / len);
        }

        public static double Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        public static double Distance(Vec3 a, Vec3 b)
        {
            return (a - b).Length();
        }

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vec3 operator *(Vec3 v, double s)
        {
            return new Vec3(v.X * s, v.Y * s, v.Z * s);
        }

        public static Vec3 operator *(double s, Vec3 v)
        {
            return new Vec3(v.X * s, v.Y * s, v.Z * s);
        }
    }

    public enum AngleKind
    {
        None = 0,
        Arc = 1,
        Straight = 2
    }

    public enum DrawItemKind
    {
        Atom = 0,
        Bond = 1,
        LonePair = 2
    }

    public sealed class AtomDef
    {
        public readonly string Element;
        public readonly Vec3 Position;
        public readonly double RelativeRadius;

        public AtomDef(string element, Vec3 position)
        {
            Element = element;
            Position = position;
            RelativeRadius = RelativeRadiusFor(element);
        }
    }

    public sealed class BondDef
    {
        public readonly int AtomA;
        public readonly int AtomB;
        public readonly int DefaultOrder;

        public BondDef(int atomA, int atomB, int defaultOrder)
        {
            AtomA = atomA;
            AtomB = atomB;
            DefaultOrder = defaultOrder;
        }
    }

    public sealed class LonePairDef
    {
        public readonly int AtomIndex;
        public readonly Vec3 Direction;

        public LonePairDef(int atomIndex, Vec3 direction)
        {
            AtomIndex = atomIndex;
            Direction = direction.Normalized();
        }
    }

    public sealed class AngleDef
    {
        public readonly int AtomA;
        public readonly int Vertex;
        public readonly int AtomB;
        public readonly AngleKind Kind;
        public readonly string Label;

        public AngleDef(int atomA, int vertex, int atomB, AngleKind kind, string label)
        {
            AtomA = atomA;
            Vertex = vertex;
            AtomB = atomB;
            Kind = kind;
            Label = label;
        }
    }

    public sealed class MoleculeModel
    {
        public readonly string Id;
        public readonly AtomDef[] Atoms;
        public readonly BondDef[] Bonds;
        public readonly LonePairDef[] LonePairs;
        public readonly AngleDef[] Angles;
        public readonly string AngleNote;
        public readonly string SourceUrl;
        public readonly string SourceNote;
        public readonly Vec3 Center;
        public readonly double BoundingRadius;
        internal readonly double[] AngleDegreesCache;
        internal readonly string[] AngleTextsCache;

        public MoleculeModel(
            string id,
            AtomDef[] atoms,
            BondDef[] bonds,
            LonePairDef[] lonePairs,
            AngleDef[] angles,
            string angleNote,
            string sourceUrl,
            string sourceNote)
        {
            Id = id;
            Atoms = atoms;
            Bonds = bonds;
            LonePairs = lonePairs;
            Angles = angles;
            AngleNote = angleNote;
            SourceUrl = sourceUrl;
            SourceNote = sourceNote;

            Vec3 sum = Vec3.Zero;
            for (int i = 0; i < atoms.Length; i++)
            {
                sum = sum + atoms[i].Position;
            }
            Center = atoms.Length > 0 ? sum * (1.0 / atoms.Length) : Vec3.Zero;

            double maxRadius = 0.0;
            for (int i = 0; i < atoms.Length; i++)
            {
                double d = Vec3.Distance(atoms[i].Position, Center);
                if (d > maxRadius) maxRadius = d;
            }
            BoundingRadius = maxRadius;

            AngleDegreesCache = new double[angles.Length];
            AngleTextsCache = new string[angles.Length];
            for (int i = 0; i < angles.Length; i++)
            {
                AngleDef a = angles[i];
                double deg = MoleculeGeometry.AngleDegrees(
                    atoms[a.AtomA].Position,
                    atoms[a.Vertex].Position,
                    atoms[a.AtomB].Position);
                AngleDegreesCache[i] = deg;
                AngleTextsCache[i] = a.Kind == AngleKind.Straight
                    ? "180°"
                    : deg.ToString("F1", CultureInfo.InvariantCulture) + "°";
            }
        }

        public double BondLength(int bondIndex)
        {
            BondDef bond = Bonds[bondIndex];
            return Vec3.Distance(Atoms[bond.AtomA].Position, Atoms[bond.AtomB].Position);
        }

        public double AngleDegrees(int angleIndex)
        {
            return AngleDegreesCache[angleIndex];
        }

        public int LonePairCountForAtom(int atomIndex)
        {
            int count = 0;
            for (int i = 0; i < LonePairs.Length; i++)
            {
                if (LonePairs[i].AtomIndex == atomIndex) count++;
            }
            return count;
        }
    }

    public struct ProjectedPoint
    {
        public bool Visible;
        public double X;
        public double Y;
        public double Depth;
        public double PerspectiveScale;
    }

    public sealed class ProjectedAtom
    {
        public bool Visible;
        public int Index;
        public string Element;
        public Vec3 Rotated;
        public double X;
        public double Y;
        public double Depth;
        public double PerspectiveScale;
        public double RadiusPx;
        public double Brightness;
    }

    public sealed class DrawItem
    {
        public DrawItemKind Kind;
        public double Depth;
        public double PerspectiveScale;
        public double Brightness;

        // Atom / LonePair
        public int AtomIndex;
        public int LonePairIndex;
        public string Element;
        public double X;
        public double Y;
        public double RadiusPx;
        public double AnchorX;
        public double AnchorY;
        public double DirX;
        public double DirY;

        // Bond
        public int BondIndex;
        public int AtomA;
        public int AtomB;
        public int Order;
        public double StartX;
        public double StartY;
        public double EndX;
        public double EndY;
        public double ClippedLength;
    }

    public sealed class ProjectedAngle
    {
        public bool Visible;
        public int AtomA;
        public int Vertex;
        public int AtomB;
        public AngleKind Kind;
        public string Label;
        public double Degrees;
        public string DegreesText;
        public double VertexX;
        public double VertexY;
        public double VertexRadiusPx;
        public double Ax;
        public double Ay;
        public double Bx;
        public double By;
        public double PerspectiveScale;
    }

    public sealed class ProjectedScene
    {
        public MoleculeModel Model;
        public ProjectedAtom[] Atoms;
        public DrawItem[] Items;
        public ProjectedAngle[] Angles;
    }

    // 매 Repaint마다 배열·객체를 새로 할당하지 않고 재사용하기 위한 분자별 투영 버퍼.
    public sealed class SceneBuffer
    {
        public readonly MoleculeModel Model;
        public readonly ProjectedAtom[] Atoms;
        public readonly List<DrawItem> Items;
        public readonly ProjectedAngle[] Angles;

        internal readonly DrawItem[] AtomPool;
        internal readonly DrawItem[] BondPool;
        internal readonly DrawItem[] LonePairPool;
        internal readonly int[] CachedOrders;

        internal bool HasCache;
        internal double CachedYaw;
        internal double CachedTilt;
        internal double CachedCenterX;
        internal double CachedCenterY;
        internal double CachedScale;
        internal double CachedCameraDistance;
        internal double CachedBaseAtomRadiusPx;
        internal bool CachedIncludeLonePairs;

        public int RebuildCount { get; internal set; }

        public SceneBuffer(MoleculeModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            Model = model;

            int atomCount = model.Atoms.Length;
            int bondCount = model.Bonds.Length;
            int lpCount = model.LonePairs.Length;
            int angleCount = model.Angles.Length;

            Atoms = new ProjectedAtom[atomCount];
            AtomPool = new DrawItem[atomCount];
            for (int i = 0; i < atomCount; i++)
            {
                ProjectedAtom pa = new ProjectedAtom();
                pa.Index = i;
                pa.Element = model.Atoms[i].Element;
                Atoms[i] = pa;

                DrawItem di = new DrawItem();
                di.Kind = DrawItemKind.Atom;
                di.AtomIndex = i;
                di.Element = model.Atoms[i].Element;
                AtomPool[i] = di;
            }

            BondPool = new DrawItem[bondCount];
            CachedOrders = new int[bondCount];
            for (int i = 0; i < bondCount; i++)
            {
                DrawItem bi = new DrawItem();
                bi.Kind = DrawItemKind.Bond;
                bi.BondIndex = i;
                bi.AtomA = model.Bonds[i].AtomA;
                bi.AtomB = model.Bonds[i].AtomB;
                BondPool[i] = bi;
            }

            LonePairPool = new DrawItem[lpCount];
            for (int i = 0; i < lpCount; i++)
            {
                DrawItem li = new DrawItem();
                li.Kind = DrawItemKind.LonePair;
                li.AtomIndex = model.LonePairs[i].AtomIndex;
                li.LonePairIndex = i;
                LonePairPool[i] = li;
            }

            Angles = new ProjectedAngle[angleCount];
            for (int i = 0; i < angleCount; i++)
            {
                AngleDef def = model.Angles[i];
                ProjectedAngle ang = new ProjectedAngle();
                ang.AtomA = def.AtomA;
                ang.Vertex = def.Vertex;
                ang.AtomB = def.AtomB;
                ang.Kind = def.Kind;
                ang.Label = def.Label;
                ang.Degrees = model.AngleDegreesCache[i];
                ang.DegreesText = model.AngleTextsCache[i];
                Angles[i] = ang;
            }

            Items = new List<DrawItem>(atomCount + bondCount + lpCount);
        }

        public void Invalidate()
        {
            HasCache = false;
        }
    }

    static readonly Comparison<DrawItem> s_drawItemComparer = CompareDrawItemsFarToNear;

    static readonly string[] s_allIds = new string[]
    {
        "H2O", "NH3", "CH4", "CO2", "N2", "O2", "HCN", "CH2O", "C2H4", "C2H2"
    };

    static readonly Dictionary<string, MoleculeModel> s_models = BuildModels();

    public static string[] AllIds
    {
        get
        {
            string[] copy = new string[s_allIds.Length];
            Array.Copy(s_allIds, copy, s_allIds.Length);
            return copy;
        }
    }

    public static double RelativeRadiusFor(string element)
    {
        if (element == "H") return 0.74;
        if (element == "C") return 1.00;
        if (element == "N") return 0.96;
        if (element == "O") return 0.94;
        return 0.95;
    }

    public static bool TryGet(string moleculeId, out MoleculeModel model)
    {
        model = null;
        if (string.IsNullOrEmpty(moleculeId)) return false;
        return s_models.TryGetValue(moleculeId, out model);
    }

    public static MoleculeModel Get(string moleculeId)
    {
        MoleculeModel model;
        if (!TryGet(moleculeId, out model))
        {
            throw new ArgumentException("알 수 없는 분자 ID입니다: " + (moleculeId ?? "(null)"));
        }
        return model;
    }

    public static double AngleDegrees(Vec3 a, Vec3 vertex, Vec3 b)
    {
        Vec3 va = a - vertex;
        Vec3 vb = b - vertex;
        double la = va.Length();
        double lb = vb.Length();
        if (la <= 1e-12 || lb <= 1e-12) return 0.0;
        double cos = Vec3.Dot(va, vb) / (la * lb);
        if (cos > 1.0) cos = 1.0;
        if (cos < -1.0) cos = -1.0;
        return Math.Acos(cos) * (180.0 / Math.PI);
    }

    // Y축 회전(yaw) 후 고정 X축 기울기(tilt)를 적용한다. 직교 변환이므로 3D 거리를 보존한다.
    public static Vec3 Rotate(Vec3 point, double yawDegrees, double tiltDegrees)
    {
        double yaw = yawDegrees * (Math.PI / 180.0);
        double cy = Math.Cos(yaw);
        double sy = Math.Sin(yaw);
        double x1 = point.X * cy + point.Z * sy;
        double y1 = point.Y;
        double z1 = -point.X * sy + point.Z * cy;

        double tilt = tiltDegrees * (Math.PI / 180.0);
        double cx = Math.Cos(tilt);
        double sx = Math.Sin(tilt);
        double x2 = x1;
        double y2 = y1 * cx - z1 * sx;
        double z2 = y1 * sx + z1 * cx;

        return new Vec3(x2, y2, z2);
    }

    // 오른손 모델 좌표(+X 오른쪽, +Y 위, +Z 카메라 방향)를 화면 좌표(+X 오른쪽, +Y 아래)로 원근 투영한다.
    // Depth = 카메라로부터의 거리(클수록 멀고, 작을수록 가깝다). near plane(MinCameraDepth) 뒤 점은 false를 반환한다.
    public static bool TryProjectPoint(
        Vec3 rotatedPoint,
        double centerX,
        double centerY,
        double scale,
        double cameraDistance,
        out ProjectedPoint result)
    {
        double cam = cameraDistance > 0.0 ? cameraDistance : DefaultCameraDistance;
        double depth = cam - rotatedPoint.Z;
        if (depth < MinCameraDepth)
        {
            result.Visible = false;
            result.X = 0.0;
            result.Y = 0.0;
            result.Depth = depth;
            result.PerspectiveScale = 0.0;
            return false;
        }
        double perspective = cam / depth;

        result.Visible = true;
        result.X = centerX + rotatedPoint.X * scale * perspective;
        result.Y = centerY - rotatedPoint.Y * scale * perspective;
        result.Depth = depth;
        result.PerspectiveScale = perspective;
        return true;
    }

    public static ProjectedPoint ProjectPoint(
        Vec3 rotatedPoint,
        double centerX,
        double centerY,
        double scale,
        double cameraDistance)
    {
        ProjectedPoint result;
        TryProjectPoint(rotatedPoint, centerX, centerY, scale, cameraDistance, out result);
        return result;
    }

    static double ComputeBrightness(double depth, double cameraDistance, double halfSpan)
    {
        double span = halfSpan > 0.5 ? halfSpan : 0.5;
        // 가까울수록(depth < cameraDistance) 값이 커져 밝아진다.
        double t = (cameraDistance - depth) / span;
        if (t > 1.0) t = 1.0;
        if (t < -1.0) t = -1.0;
        return 0.80 + 0.20 * t;
    }

    public static SceneBuffer CreateBuffer(string moleculeId)
    {
        return new SceneBuffer(Get(moleculeId));
    }

    static int ResolveBondOrder(BondDef bond, int[] bondOrders, int bondIndex)
    {
        int order = bond.DefaultOrder;
        if (bondOrders != null && bondIndex < bondOrders.Length)
        {
            order = bondOrders[bondIndex];
        }
        if (order <= 0) return 0;
        if (order > 3) return 3;
        return order;
    }

    static bool MatchesCache(
        SceneBuffer buffer,
        int[] bondOrders,
        double yawDegrees,
        double tiltDegrees,
        double centerX,
        double centerY,
        double scale,
        double cameraDistance,
        double baseAtomRadiusPx,
        bool includeLonePairs)
    {
        if (!buffer.HasCache) return false;
        if (buffer.CachedYaw != yawDegrees ||
            buffer.CachedTilt != tiltDegrees ||
            buffer.CachedCenterX != centerX ||
            buffer.CachedCenterY != centerY ||
            buffer.CachedScale != scale ||
            buffer.CachedCameraDistance != cameraDistance ||
            buffer.CachedBaseAtomRadiusPx != baseAtomRadiusPx ||
            buffer.CachedIncludeLonePairs != includeLonePairs)
        {
            return false;
        }

        BondDef[] bonds = buffer.Model.Bonds;
        for (int i = 0; i < bonds.Length; i++)
        {
            if (buffer.CachedOrders[i] != ResolveBondOrder(bonds[i], bondOrders, i))
            {
                return false;
            }
        }
        return true;
    }

    // 재사용 버퍼에 투영·깊이 정렬 결과를 갱신한다. 입력이 이전과 완전히 같으면 재계산을 건너뛰고 false를 반환한다.
    public static bool BuildSceneInto(
        SceneBuffer buffer,
        int[] bondOrders,
        double yawDegrees,
        double tiltDegrees,
        double centerX,
        double centerY,
        double scale,
        double cameraDistance,
        double baseAtomRadiusPx,
        bool includeLonePairs)
    {
        if (buffer == null) throw new ArgumentNullException("buffer");
        double cam = cameraDistance > 0.0 ? cameraDistance : DefaultCameraDistance;

        if (MatchesCache(buffer, bondOrders, yawDegrees, tiltDegrees, centerX, centerY, scale, cam, baseAtomRadiusPx, includeLonePairs))
        {
            return false;
        }

        MoleculeModel model = buffer.Model;
        double depthHalfSpan = Math.Max(1.2, model.BoundingRadius + 0.5);
        ProjectedAtom[] projectedAtoms = buffer.Atoms;
        List<DrawItem> items = buffer.Items;
        items.Clear();

        int atomCount = model.Atoms.Length;
        for (int i = 0; i < atomCount; i++)
        {
            AtomDef def = model.Atoms[i];
            Vec3 centered = def.Position - model.Center;
            Vec3 rotated = Rotate(centered, yawDegrees, tiltDegrees);

            ProjectedPoint p;
            bool visible = TryProjectPoint(rotated, centerX, centerY, scale, cam, out p);

            ProjectedAtom pa = projectedAtoms[i];
            pa.Visible = visible;
            pa.Rotated = rotated;
            pa.X = p.X;
            pa.Y = p.Y;
            pa.Depth = p.Depth;
            pa.PerspectiveScale = p.PerspectiveScale;

            if (!visible)
            {
                pa.RadiusPx = 0.0;
                pa.Brightness = 0.0;
                continue;
            }

            double radiusPx = baseAtomRadiusPx * def.RelativeRadius * p.PerspectiveScale;
            double brightness = ComputeBrightness(p.Depth, cam, depthHalfSpan);
            pa.RadiusPx = radiusPx;
            pa.Brightness = brightness;

            DrawItem atomItem = buffer.AtomPool[i];
            atomItem.Depth = p.Depth;
            atomItem.PerspectiveScale = p.PerspectiveScale;
            atomItem.Brightness = brightness;
            atomItem.X = p.X;
            atomItem.Y = p.Y;
            atomItem.RadiusPx = radiusPx;
            items.Add(atomItem);
        }

        for (int i = 0; i < model.Bonds.Length; i++)
        {
            BondDef bond = model.Bonds[i];
            int order = ResolveBondOrder(bond, bondOrders, i);
            buffer.CachedOrders[i] = order;
            if (order <= 0) continue;

            ProjectedAtom a = projectedAtoms[bond.AtomA];
            ProjectedAtom b = projectedAtoms[bond.AtomB];
            if (!a.Visible && !b.Visible) continue;

            double ax, ay, aDepth, aScale, trimA;
            double bx, by, bDepth, bScale, trimB;

            if (a.Visible && b.Visible)
            {
                ax = a.X; ay = a.Y; aDepth = a.Depth; aScale = a.PerspectiveScale; trimA = a.RadiusPx;
                bx = b.X; by = b.Y; bDepth = b.Depth; bScale = b.PerspectiveScale; trimB = b.RadiusPx;
            }
            else
            {
                // 한쪽 원자가 near plane 뒤에 있으면 3D 선분을 near plane(depth == MinCameraDepth)에서 클리핑한다.
                double denom = b.Depth - a.Depth;
                if (Math.Abs(denom) <= 1e-12) continue;
                double t = (MinCameraDepth - a.Depth) / denom;
                if (t <= 0.0 || t >= 1.0) continue;
                Vec3 clipped3D = a.Rotated + (b.Rotated - a.Rotated) * t;
                // 부동소수점 오차로 depth가 MinCameraDepth보다 미세하게 작아지지 않도록 보정.
                double nearZ = cam - MinCameraDepth;
                if (clipped3D.Z > nearZ) clipped3D.Z = nearZ;

                ProjectedPoint pc;
                if (!TryProjectPoint(clipped3D, centerX, centerY, scale, cam, out pc)) continue;

                if (a.Visible)
                {
                    ax = a.X; ay = a.Y; aDepth = a.Depth; aScale = a.PerspectiveScale; trimA = a.RadiusPx;
                    bx = pc.X; by = pc.Y; bDepth = pc.Depth; bScale = pc.PerspectiveScale; trimB = 0.0;
                }
                else
                {
                    ax = pc.X; ay = pc.Y; aDepth = pc.Depth; aScale = pc.PerspectiveScale; trimA = 0.0;
                    bx = b.X; by = b.Y; bDepth = b.Depth; bScale = b.PerspectiveScale; trimB = b.RadiusPx;
                }
            }

            double dx = bx - ax;
            double dy = by - ay;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double clippedLen = dist - trimA - trimB;
            if (dist <= 1e-6 || clippedLen <= 0.0) continue;

            double ux = dx / dist;
            double uy = dy / dist;
            double midDepth = (aDepth + bDepth) * 0.5;
            double midScale = (aScale + bScale) * 0.5;

            DrawItem bondItem = buffer.BondPool[i];
            bondItem.Depth = midDepth;
            bondItem.PerspectiveScale = midScale;
            bondItem.Brightness = ComputeBrightness(midDepth, cam, depthHalfSpan);
            bondItem.Order = order;
            bondItem.StartX = ax + ux * trimA;
            bondItem.StartY = ay + uy * trimA;
            bondItem.EndX = bx - ux * trimB;
            bondItem.EndY = by - uy * trimB;
            bondItem.ClippedLength = clippedLen;
            items.Add(bondItem);
        }

        if (includeLonePairs)
        {
            for (int i = 0; i < model.LonePairs.Length; i++)
            {
                LonePairDef lp = model.LonePairs[i];
                AtomDef hostDef = model.Atoms[lp.AtomIndex];
                ProjectedAtom host = projectedAtoms[lp.AtomIndex];

                Vec3 lobeWorld = (hostDef.Position - model.Center) + lp.Direction * LonePairOffsetAngstrom;
                Vec3 lobeRotated = Rotate(lobeWorld, yawDegrees, tiltDegrees);
                ProjectedPoint p;
                if (!TryProjectPoint(lobeRotated, centerX, centerY, scale, cam, out p)) continue;

                double anchorX = host.Visible ? host.X : p.X;
                double anchorY = host.Visible ? host.Y : p.Y;
                double ddx = p.X - anchorX;
                double ddy = p.Y - anchorY;
                double dlen = Math.Sqrt(ddx * ddx + ddy * ddy);
                double dirX = dlen > 1e-6 ? ddx / dlen : 0.0;
                double dirY = dlen > 1e-6 ? ddy / dlen : -1.0;

                DrawItem lpItem = buffer.LonePairPool[i];
                lpItem.Depth = p.Depth;
                lpItem.PerspectiveScale = p.PerspectiveScale;
                lpItem.Brightness = ComputeBrightness(p.Depth, cam, depthHalfSpan);
                lpItem.AnchorX = anchorX;
                lpItem.AnchorY = anchorY;
                lpItem.X = p.X;
                lpItem.Y = p.Y;
                lpItem.DirX = dirX;
                lpItem.DirY = dirY;
                lpItem.RadiusPx = baseAtomRadiusPx * LonePairRadiusFactor * p.PerspectiveScale;
                items.Add(lpItem);
            }
        }

        // 먼 것(Depth가 큰 것)부터 먼저 그리도록 내림차순 정렬(캐시된 비교자 사용).
        items.Sort(s_drawItemComparer);

        for (int i = 0; i < model.Angles.Length; i++)
        {
            AngleDef def = model.Angles[i];
            ProjectedAtom pa = projectedAtoms[def.AtomA];
            ProjectedAtom pv = projectedAtoms[def.Vertex];
            ProjectedAtom pb = projectedAtoms[def.AtomB];

            ProjectedAngle ang = buffer.Angles[i];
            ang.Visible = pa.Visible && pv.Visible && pb.Visible;
            ang.VertexX = pv.X;
            ang.VertexY = pv.Y;
            ang.VertexRadiusPx = pv.RadiusPx;
            ang.Ax = pa.X;
            ang.Ay = pa.Y;
            ang.Bx = pb.X;
            ang.By = pb.Y;
            ang.PerspectiveScale = pv.PerspectiveScale;
        }

        buffer.HasCache = true;
        buffer.CachedYaw = yawDegrees;
        buffer.CachedTilt = tiltDegrees;
        buffer.CachedCenterX = centerX;
        buffer.CachedCenterY = centerY;
        buffer.CachedScale = scale;
        buffer.CachedCameraDistance = cam;
        buffer.CachedBaseAtomRadiusPx = baseAtomRadiusPx;
        buffer.CachedIncludeLonePairs = includeLonePairs;
        buffer.RebuildCount++;
        return true;
    }

    public static ProjectedScene BuildScene(
        string moleculeId,
        int[] bondOrders,
        double yawDegrees,
        double tiltDegrees,
        double centerX,
        double centerY,
        double scale,
        double cameraDistance,
        double baseAtomRadiusPx,
        bool includeLonePairs)
    {
        SceneBuffer buffer = CreateBuffer(moleculeId);
        BuildSceneInto(
            buffer,
            bondOrders,
            yawDegrees,
            tiltDegrees,
            centerX,
            centerY,
            scale,
            cameraDistance,
            baseAtomRadiusPx,
            includeLonePairs);

        ProjectedScene scene = new ProjectedScene();
        scene.Model = buffer.Model;
        scene.Atoms = buffer.Atoms;
        scene.Items = buffer.Items.ToArray();
        scene.Angles = buffer.Angles;
        return scene;
    }

    static int CompareDrawItemsFarToNear(DrawItem a, DrawItem b)
    {
        int cmp = b.Depth.CompareTo(a.Depth);
        if (cmp != 0) return cmp;
        // 깊이가 같으면 전자쌍 → 결합 → 원자 순서로 그려 원자가 깔끔하게 위에 오게 한다.
        return OrderRank(a.Kind).CompareTo(OrderRank(b.Kind));
    }

    static int OrderRank(DrawItemKind kind)
    {
        if (kind == DrawItemKind.LonePair) return 0;
        if (kind == DrawItemKind.Bond) return 1;
        return 2;
    }

    static Dictionary<string, MoleculeModel> BuildModels()
    {
        Dictionary<string, MoleculeModel> map = new Dictionary<string, MoleculeModel>(StringComparer.Ordinal);
        double deg2rad = Math.PI / 180.0;
        double sqrt3Over2 = Math.Sqrt(3.0) * 0.5;

        // 1. H2O: [O, H, H], r(O-H)=0.9578 A, a(H-O-H)=104.4776 deg
        {
            double r = 0.9578;
            double half = 104.4776 * 0.5 * deg2rad;
            double hx = r * Math.Sin(half);
            double hy = -r * Math.Cos(half);
            double lpHalf = 54.7356 * deg2rad; // 정사면체 보완 방향 (YZ 평면)
            map["H2O"] = new MoleculeModel(
                "H2O",
                new AtomDef[]
                {
                    new AtomDef("O", Vec3.Zero),
                    new AtomDef("H", new Vec3(-hx, hy, 0.0)),
                    new AtomDef("H", new Vec3(hx, hy, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 1),
                    new BondDef(0, 2, 1)
                },
                new LonePairDef[]
                {
                    new LonePairDef(0, new Vec3(0.0, Math.Cos(lpHalf), Math.Sin(lpHalf))),
                    new LonePairDef(0, new Vec3(0.0, Math.Cos(lpHalf), -Math.Sin(lpHalf)))
                },
                new AngleDef[]
                {
                    new AngleDef(1, 0, 2, AngleKind.Arc, "H–O–H")
                },
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=7732185&charge=0",
                "NIST CCCBDB (1979Hoy/Bun:1 equilibrium): r(O-H)=0.9578 A, a(H-O-H)=104.48 deg");
        }

        // 2. NH3: [N, H, H, H], r(N-H)=1.0124 A, a(H-N-H)=106.67 deg
        {
            double r = 1.0124;
            double aHnh = 106.67 * deg2rad;
            double sinPhi = Math.Sqrt((1.0 - Math.Cos(aHnh)) / 1.5);
            double cosPhi = -Math.Sqrt(1.0 - sinPhi * sinPhi);
            double rad = r * sinPhi;
            double hy = r * cosPhi;
            map["NH3"] = new MoleculeModel(
                "NH3",
                new AtomDef[]
                {
                    new AtomDef("N", Vec3.Zero),
                    new AtomDef("H", new Vec3(rad, hy, 0.0)),
                    new AtomDef("H", new Vec3(-0.5 * rad, hy, sqrt3Over2 * rad)),
                    new AtomDef("H", new Vec3(-0.5 * rad, hy, -sqrt3Over2 * rad))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 1),
                    new BondDef(0, 2, 1),
                    new BondDef(0, 3, 1)
                },
                new LonePairDef[]
                {
                    new LonePairDef(0, new Vec3(0.0, 1.0, 0.0))
                },
                new AngleDef[]
                {
                    new AngleDef(1, 0, 2, AngleKind.Arc, "H–N–H")
                },
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=7664417&charge=0",
                "NIST CCCBDB (1966Herzberg): r(N-H)=1.0124 A, a(H-N-H)=106.67 deg");
        }

        // 3. CH4: [C, H, H, H, H], r(C-H)=1.0870 A, a(H-C-H)=109.4712 deg
        {
            double r = 1.0870;
            double yDown = -r / 3.0;
            double rad = r * Math.Sqrt(8.0) / 3.0;
            map["CH4"] = new MoleculeModel(
                "CH4",
                new AtomDef[]
                {
                    new AtomDef("C", Vec3.Zero),
                    new AtomDef("H", new Vec3(0.0, r, 0.0)),
                    new AtomDef("H", new Vec3(rad, yDown, 0.0)),
                    new AtomDef("H", new Vec3(-0.5 * rad, yDown, sqrt3Over2 * rad)),
                    new AtomDef("H", new Vec3(-0.5 * rad, yDown, -sqrt3Over2 * rad))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 1),
                    new BondDef(0, 2, 1),
                    new BondDef(0, 3, 1),
                    new BondDef(0, 4, 1)
                },
                new LonePairDef[0],
                new AngleDef[]
                {
                    new AngleDef(1, 0, 2, AngleKind.Arc, "H–C–H")
                },
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=74828&charge=0",
                "NIST CCCBDB (1966Herzberg): r(C-H)=1.0870 A, 정사면체 109.47 deg");
        }

        // 4. CO2: [C, O, O], r(C=O)=1.1621 A, a(O-C-O)=180 deg
        {
            double r = 1.1621;
            map["CO2"] = new MoleculeModel(
                "CO2",
                new AtomDef[]
                {
                    new AtomDef("C", Vec3.Zero),
                    new AtomDef("O", new Vec3(-r, 0.0, 0.0)),
                    new AtomDef("O", new Vec3(r, 0.0, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 2),
                    new BondDef(0, 2, 2)
                },
                new LonePairDef[]
                {
                    new LonePairDef(1, new Vec3(-0.5, sqrt3Over2, 0.0)),
                    new LonePairDef(1, new Vec3(-0.5, -sqrt3Over2, 0.0)),
                    new LonePairDef(2, new Vec3(0.5, 0.0, sqrt3Over2)),
                    new LonePairDef(2, new Vec3(0.5, 0.0, -sqrt3Over2))
                },
                new AngleDef[]
                {
                    new AngleDef(1, 0, 2, AngleKind.Straight, "O–C–O")
                },
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=124389&charge=0",
                "NIST CCCBDB (1966Herzberg): r(C=O)=1.1621 A, a(O-C-O)=180 deg");
        }

        // 5. N2: [N, N], r(N#N)=1.0977 A, 이원자(각 없음)
        {
            double r = 1.0977;
            map["N2"] = new MoleculeModel(
                "N2",
                new AtomDef[]
                {
                    new AtomDef("N", Vec3.Zero),
                    new AtomDef("N", new Vec3(r, 0.0, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 3)
                },
                new LonePairDef[]
                {
                    new LonePairDef(0, new Vec3(-1.0, 0.0, 0.0)),
                    new LonePairDef(1, new Vec3(1.0, 0.0, 0.0))
                },
                new AngleDef[0],
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=7727379&charge=0",
                "NIST CCCBDB (1979HUB/HER re): r(N#N)=1.0977 A");
        }

        // 6. O2: [O, O], r(O=O)=1.2075 A, 이원자(각 없음)
        {
            double r = 1.2075;
            map["O2"] = new MoleculeModel(
                "O2",
                new AtomDef[]
                {
                    new AtomDef("O", Vec3.Zero),
                    new AtomDef("O", new Vec3(r, 0.0, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 2)
                },
                new LonePairDef[]
                {
                    new LonePairDef(0, new Vec3(-0.5, sqrt3Over2, 0.0)),
                    new LonePairDef(0, new Vec3(-0.5, -sqrt3Over2, 0.0)),
                    new LonePairDef(1, new Vec3(0.5, sqrt3Over2, 0.0)),
                    new LonePairDef(1, new Vec3(0.5, -sqrt3Over2, 0.0))
                },
                new AngleDef[0],
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=7782447&charge=0",
                "NIST CCCBDB (1979HUB/HER re): r(O=O)=1.2075 A");
        }

        // 7. HCN: [C, H, N], r(C-H)=1.0640 A, r(C#N)=1.1560 A, a(H-C-N)=180 deg
        {
            map["HCN"] = new MoleculeModel(
                "HCN",
                new AtomDef[]
                {
                    new AtomDef("C", Vec3.Zero),
                    new AtomDef("H", new Vec3(-1.0640, 0.0, 0.0)),
                    new AtomDef("N", new Vec3(1.1560, 0.0, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 1),
                    new BondDef(0, 2, 3)
                },
                new LonePairDef[]
                {
                    new LonePairDef(2, new Vec3(1.0, 0.0, 0.0))
                },
                new AngleDef[]
                {
                    new AngleDef(1, 0, 2, AngleKind.Straight, "H–C–N")
                },
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=74908&charge=0",
                "NIST CCCBDB (1966Herzberg): r(C-H)=1.0640 A, r(C#N)=1.1560 A, a(H-C-N)=180 deg");
        }

        // 8. CH2O: [C, O, H, H], r(C=O)=1.2050 A, r(C-H)=1.1110 A, a(H-C-O)=121.93 deg, a(H-C-H)=116.14 deg
        {
            map["CH2O"] = new MoleculeModel(
                "CH2O",
                new AtomDef[]
                {
                    new AtomDef("C", Vec3.Zero),
                    new AtomDef("O", new Vec3(0.0, 1.2050, 0.0)),
                    new AtomDef("H", new Vec3(-0.9429, -0.5876, 0.0)),
                    new AtomDef("H", new Vec3(0.9429, -0.5876, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 2),
                    new BondDef(0, 2, 1),
                    new BondDef(0, 3, 1)
                },
                new LonePairDef[]
                {
                    new LonePairDef(1, new Vec3(-sqrt3Over2, 0.5, 0.0)),
                    new LonePairDef(1, new Vec3(sqrt3Over2, 0.5, 0.0))
                },
                new AngleDef[]
                {
                    new AngleDef(2, 0, 1, AngleKind.Arc, "H–C–O"),
                    new AngleDef(2, 0, 3, AngleKind.Arc, "H–C–H")
                },
                TrigonalPlanarAngleNote,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=50000&charge=0",
                "NIST CCCBDB (Gurvich/1966Herzberg): r(C=O)=1.2050 A, r(C-H)=1.1110 A, a(H-C-O)=121.93 deg, a(H-C-H)=116.14 deg");
        }

        // 9. C2H4: [C, C, H, H, H, H] (H2,H3 -> C0; H4,H5 -> C1)
        // r(C=C)=1.3390 A, r(C-H)=1.0860 A, a(H-C-C)=121.20 deg, a(H-C-H)=117.60 deg
        {
            double rcc = 1.3390;
            double dxH = 0.5626;
            double dyH = 0.9289;
            map["C2H4"] = new MoleculeModel(
                "C2H4",
                new AtomDef[]
                {
                    new AtomDef("C", Vec3.Zero),
                    new AtomDef("C", new Vec3(rcc, 0.0, 0.0)),
                    new AtomDef("H", new Vec3(-dxH, dyH, 0.0)),
                    new AtomDef("H", new Vec3(-dxH, -dyH, 0.0)),
                    new AtomDef("H", new Vec3(rcc + dxH, dyH, 0.0)),
                    new AtomDef("H", new Vec3(rcc + dxH, -dyH, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 2),
                    new BondDef(0, 2, 1),
                    new BondDef(0, 3, 1),
                    new BondDef(1, 4, 1),
                    new BondDef(1, 5, 1)
                },
                new LonePairDef[0],
                new AngleDef[]
                {
                    new AngleDef(2, 0, 1, AngleKind.Arc, "H–C–C"),
                    new AngleDef(2, 0, 3, AngleKind.Arc, "H–C–H")
                },
                TrigonalPlanarAngleNote,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=74851&charge=0",
                "NIST CCCBDB: r(C=C)=1.3390 A, r(C-H)=1.0860 A, a(H-C-C)=121.20 deg, a(H-C-H)=117.60 deg");
        }

        // 10. C2H2: [C, C, H, H] (H2 -> C0; H3 -> C1)
        // r(C#C)=1.2026 A, r(C-H)=1.0631 A, a(H-C-C)=180 deg
        {
            double rcc = 1.2026;
            double rch = 1.0631;
            map["C2H2"] = new MoleculeModel(
                "C2H2",
                new AtomDef[]
                {
                    new AtomDef("C", Vec3.Zero),
                    new AtomDef("C", new Vec3(rcc, 0.0, 0.0)),
                    new AtomDef("H", new Vec3(-rch, 0.0, 0.0)),
                    new AtomDef("H", new Vec3(rcc + rch, 0.0, 0.0))
                },
                new BondDef[]
                {
                    new BondDef(0, 1, 3),
                    new BondDef(0, 2, 1),
                    new BondDef(1, 3, 1)
                },
                new LonePairDef[0],
                new AngleDef[]
                {
                    new AngleDef(2, 0, 1, AngleKind.Straight, "H–C–C")
                },
                null,
                "https://cccbdb.nist.gov/expgeom2x.asp?casno=74862&charge=0",
                "NIST CCCBDB (1998Kuc rm / 1966Herzberg): r(C#C)=1.2026 A, r(C-H)=1.0631 A, a(H-C-C)=180 deg");
        }

        return map;
    }
}
