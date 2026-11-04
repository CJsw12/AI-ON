"""Regenerates Assets/Scripts/PortraitFaces.cs from the professor portraits.

Run from the repository root after adding or replacing any Assets/Resources/Professor*Transparent.png:

    pip install "opencv-python-headless<5" "numpy<2.3"
    python Tools/generate_portrait_faces.py            # writes the table
    python Tools/generate_portrait_faces.py --sheet    # also writes Temp/portrait_faces_sheet*.jpg to check by eye

The Unity Editor warns in the Console when a portrait is missing from the table or changed since it was generated
(Assets/Editor/PortraitFacesCheck.cs). Until the table is regenerated, such a picture falls back to the old framing.

How it works: OpenCV's Haar face detector finds the face (trying small rotations for tilted heads, and keeping only
a face near the top of the figure so printed shirts and hands are ignored). The detector's box size wobbles between
the moods of one professor, so per professor the face/head-width ratio and the face's drop below the top of the
figure are taken as medians and applied to every mood. OVERRIDES fixes pictures the detector gets wrong.
"""
import glob, hashlib, os, re, statistics, sys

import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESOURCES = os.path.join(ROOT, "Assets", "Resources")
OUTPUT = os.path.join(ROOT, "Assets", "Scripts", "PortraitFaces.cs")

# Face boxes set by hand (x, y, size in source pixels) where detection fails.
OVERRIDES = {
    "ProfessorGuMath": (381, 128, 221),   # tilted head: Haar only finds one eye
}
FACE, FACE_Y, REACH, BOOST_CAP = 140, 345, 660, 1.35   # must match DuoFaceHeight / DuoFaceY in GameController.Duo.cs

CASCADES = [cv2.CascadeClassifier(cv2.data.haarcascades + n)
            for n in ("haarcascade_frontalface_default.xml", "haarcascade_frontalface_alt2.xml")]


def professor_of(name):
    return re.sub(r"(Happy|Frown)?Transparent$", "", name)


def detect(img, bbox):
    h, w = img.shape[:2]
    alpha = img[:, :, 3:4].astype(np.float32) / 255
    rgb = (img[:, :, :3].astype(np.float32) * alpha + 128 * (1 - alpha)).astype(np.uint8)
    gray = cv2.equalizeHist(cv2.cvtColor(rgb, cv2.COLOR_BGR2GRAY))
    figure_h = bbox[3] - bbox[1]
    for angle in (0, 12, -12, 22, -22):
        m = cv2.getRotationMatrix2D((w / 2, h / 2), angle, 1.0)
        g = cv2.warpAffine(gray, m, (w, h), borderValue=128) if angle else gray
        mi = cv2.invertAffineTransform(m)
        found = []
        for c in CASCADES:
            for (x, y, fw, fh) in c.detectMultiScale(g, 1.05, 5, minSize=(max(24, figure_h // 22),) * 2):
                px, py = mi @ np.array([x + fw / 2, y + fh / 2, 1.0])
                fx, fy = px - fw / 2, py - fh / 2
                if fy - bbox[1] < 1.3 * fh + .04 * figure_h:   # only hair sits above a face
                    found.append((fx, fy, fw))
        if found:
            return min(found, key=lambda f: f[1])
    return None


def head_width(img, cx, cy):
    row = img[int(cy), :, 3] > 40
    l = r = int(cx)
    while l > 0 and row[l - 1]: l -= 1
    while r < len(row) - 1 and row[r + 1]: r += 1
    return r - l + 1


def main():
    data = {}
    for path in sorted(glob.glob(os.path.join(RESOURCES, "Professor*Transparent.png"))):
        name = os.path.basename(path)[:-4]
        img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
        if img is None or img.ndim != 3 or img.shape[2] != 4:
            print("skipped (no alpha):", name); continue
        ys, xs = np.nonzero(img[:, :, 3] > 20)
        bbox = [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]
        if professor_of(name) in OVERRIDES:
            x, y, s = OVERRIDES[professor_of(name)]
            face = (x, y, s)
        else:
            face = detect(img, bbox)
        if face is None:
            print("NO FACE FOUND, add an override:", name); continue
        x, y, s = face
        data[name] = dict(img=img, size=img.shape[1::-1], bbox=bbox, face=face,
                          head=head_width(img, x + s / 2, y + s / 2),
                          hash=hashlib.md5(open(path, "rb").read()).hexdigest()[:12])

    groups = {}
    for name, v in data.items(): groups.setdefault(professor_of(name), []).append(v)
    rows = []
    for prof, vs in groups.items():
        ratio = statistics.median(v["face"][2] / v["head"] for v in vs)
        drop = statistics.median((v["face"][1] + v["face"][2] / 2 - v["bbox"][1]) / v["head"] for v in vs)
        for v in vs:
            w, h = v["size"]
            size = ratio * v["head"]
            v["cx"], v["cy"], v["s"] = (v["face"][0] + v["face"][2] / 2) / w, (v["bbox"][1] + drop * v["head"]) / h, size / h
            v["bottom"] = (v["bbox"][3] + 1) / h
            # A close-up whose cut-off torso would end above the dialogue box is drawn a little larger.
            k = FACE / (v["s"] * h)
            reach = (v["bottom"] - v["cy"]) * h
            v["boost"] = max(1.0, min(BOOST_CAP, (REACH - FACE_Y) / (reach * k)))
        boost = max(v["boost"] for v in vs)
        for v in vs: v["s"] /= boost
        if boost > 1.01: print(f"{prof}: close-up, drawn {boost:.2f}x larger so it reaches the dialogue box")

    lines = ["using System.Collections.Generic;", "",
             "// GENERATED by Tools/generate_portrait_faces.py - do not edit by hand; rerun it when portraits change.",
             "// Per image: X = face centre as a share of the width; Y = face centre, Size = face height used for scaling",
             "// (smaller than the real face for a close-up whose cut-off torso would otherwise end above the dialogue box),",
             "// Bottom = lowest pixel of the figure; Y, Size, Bottom as shares of the height from the top. Aspect = source",
             "// width / height: Unity imports these pictures stretched to power-of-two sizes, so the texture's own size is",
             "// not the picture's shape. Every professor is drawn from this with the same face size and eye line.",
             "public static class PortraitFaces", "{",
             "    public readonly struct Entry", "    {",
             "        public readonly float X, Y, Size, Bottom, Aspect;",
             "        public Entry(float x, float y, float size, float bottom, float aspect) { X = x; Y = y; Size = size; Bottom = bottom; Aspect = aspect; }",
             "    }", "",
             "    public static readonly Dictionary<string, Entry> Face = new Dictionary<string, Entry>", "    {"]
    for name in sorted(data):
        v = data[name]; w, h = v["size"]
        lines.append(f'        ["{name}"] = new Entry({v["cx"]:.4f}f, {v["cy"]:.4f}f, {v["s"]:.4f}f, {v["bottom"]:.4f}f, {w / h:.4f}f),')
    lines += ["    };", "",
              "    // MD5 (first 12 hex digits) of each source file, so the Editor can tell when a picture changed.",
              "    public static readonly Dictionary<string, string> SourceHash = new Dictionary<string, string>", "    {"]
    for name in sorted(data):
        lines.append(f'        ["{name}"] = "{data[name]["hash"]}",')
    lines += ["    };", "}", ""]
    with open(OUTPUT, "w", encoding="utf-8-sig", newline="\r\n") as f:
        f.write("\n".join(lines))
    print(f"wrote {len(data)} portraits to {os.path.relpath(OUTPUT, ROOT)}")

    if "--sheet" in sys.argv:
        tiles = []
        for name in sorted(data):
            v = data[name]; img = v["img"]; w, h = v["size"]
            a = img[:, :, 3:4].astype(np.float32) / 255
            rgb = (img[:, :, :3] * a + 200 * (1 - a)).astype(np.uint8)
            scale = 180 / h
            t = cv2.resize(rgb, (max(1, int(w * scale)), 180))
            s = v["s"] * h * scale; cx = v["cx"] * w * scale; cy = v["cy"] * h * scale
            cv2.rectangle(t, (int(cx - s / 2), int(cy - s / 2)), (int(cx + s / 2), int(cy + s / 2)), (0, 0, 255), 2)
            cv2.putText(t, name[9:-11][:16], (2, 12), cv2.FONT_HERSHEY_SIMPLEX, .38, (0, 140, 0), 1)
            tiles.append(t)
        out_dir = os.path.join(ROOT, "Temp"); os.makedirs(out_dir, exist_ok=True)
        rows, row, width = [], [], 0
        for t in tiles:
            if width + t.shape[1] > 1800: rows.append(row); row, width = [], 0
            row.append(t); width += t.shape[1]
        rows.append(row)
        rows = [np.hstack(r + [np.zeros((180, 1800 - sum(x.shape[1] for x in r), 3), np.uint8)]) for r in rows]
        for i in range(0, len(rows), 5):
            p = os.path.join(out_dir, f"portrait_faces_sheet{i // 5}.jpg")
            cv2.imwrite(p, np.vstack(rows[i:i + 5])); print("sheet:", os.path.relpath(p, ROOT))


if __name__ == "__main__":
    main()
