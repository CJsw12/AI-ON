"""Compares webcam gaze calculations on a multi-posture recording (GameController.WebcamRecord.cs).

  python analyze_record.py <record-*.jsonl>

For every calculation it fits on all postures but one and scores the left-out posture
("cross-posture", what real play looks like), and also scores a fit on posture 0 alone tested on the
others (what a single-posture calibration gives). Errors are mean canvas px (1280 x 900) per point
median. Needs numpy.
"""
import json, sys
import numpy as np

# Composite feature layout (UnitEye CompositeGazeBackbone: EyeMU 36 + ResNet-34 angle terms 7).
EMB = slice(0, 4); EYEMU_X, EYEMU_Y = 4, 5; HEAD4 = slice(11, 15); IRIS = slice(15, 19)
TX, TY, DIST = 19, 20, 21; LOOK = slice(22, 30); RES_YAW, RES_PITCH = 36, 37


def load(path):
    rows = [json.loads(l) for l in open(path, encoding="utf-8")]
    use = [r for r in rows if r["measuring"] and r["fresh"] and r["present"] and r.get("feat") and not r["blink"]]
    pts = {}
    for r in use:
        pts.setdefault(r["point"], []).append(r)
    return pts


def signals(r):
    f = np.array([np.nan if v is None else v for v in r["feat"]], float)
    head = np.array(r["head"], float)            # pitch, yaw, roll (radians)
    shapes = np.array(r.get("shapes") or [0] * 10, float)
    lm = np.array(r.get("lm") or [0] * 39, float).reshape(-1, 3)
    return f, head, shapes, lm


def plane(d, offset, angle):
    return offset + d * np.tan(np.clip(angle, -1.2, 1.2))


def calc(name, r):
    """One calculation = a feature vector per sample; a linear (ridge) map to the screen is fitted on top."""
    f, head, shapes, lm = signals(r)
    tx, ty, d = f[TX], f[TY], f[DIST]
    yaw, pitch = f[RES_YAW], f[RES_PITCH]
    iris = f[IRIS]                                       # left dx, dy, right dx, dy (image, / corner distance)
    ix, iy = (iris[0] + iris[2]) / 2, (iris[1] + iris[3]) / 2
    lookx = (shapes[1] - shapes[0] + shapes[4] - shapes[5]) / 2
    looky = (shapes[2] + shapes[6] - shapes[3] - shapes[7]) / 2
    hyaw, hpitch = head[1], head[0]
    raw = np.array(r["raw"], float) / [1280, 900]
    if name == "UnitEye point":
        return raw
    if name.startswith("ResNet plane g="):
        g = float(name.split("=")[1])
        return np.array([plane(d, tx, yaw / g), plane(d, ty, pitch / g)])
    if name == "ResNet angles + head xyz":
        return np.array([yaw, pitch, tx, ty, d, yaw * d, pitch * d])
    if name == "iris + head pose + xyz":
        return np.array([ix, iy, hyaw, hpitch, tx, ty, d, ix * d, iy * d, hyaw * d, hpitch * d])
    if name == "blendshape + head pose + xyz":
        return np.array([lookx, looky, hyaw, hpitch, tx, ty, d, lookx * d, looky * d, hyaw * d, hpitch * d])
    if name == "EyeMU + head pose + xyz":
        return np.array([f[EYEMU_X], f[EYEMU_Y], hyaw, hpitch, tx, ty, d, f[EYEMU_X] * d, f[EYEMU_Y] * d])
    if name == "ResNet + iris + head":
        return np.array([yaw, pitch, ix, iy, hyaw, hpitch, tx, ty, d, yaw * d, pitch * d, ix * d, iy * d])
    if name == "everything (43 + shapes)":
        return np.r_[np.nan_to_num(f), shapes, hyaw, hpitch]
    raise KeyError(name)


NAMES = ["UnitEye point", "ResNet plane g=1", "ResNet plane g=0.37", "ResNet angles + head xyz",
         "iris + head pose + xyz", "blendshape + head pose + xyz", "EyeMU + head pose + xyz",
         "ResNet + iris + head", "everything (43 + shapes)"]


def ridge(X, y, lam):
    mu, sd = X.mean(0), X.std(0) + 1e-9
    Z = (X - mu) / sd
    A = Z.T @ Z + lam * np.eye(Z.shape[1])
    w = np.linalg.solve(A, Z.T @ (y - y.mean()))
    return lambda X2: ((X2 - mu) / sd) @ w + y.mean()


def evaluate(pts, name, lam):
    items = []
    for p, rs in sorted(pts.items()):
        feats = np.array([np.atleast_1d(calc(name, r)) for r in rs])
        items.append((rs[0]["posture"], np.array(rs[0]["target"]), np.nanmedian(feats, 0), feats))
    postures = sorted({i[0] for i in items})
    def score(train, test):
        X = np.array([i[2] for i in train]); T = np.array([i[1] for i in train])
        errs, noise = [], []
        fx = [ridge(X, T[:, k], lam) for k in (0, 1)]
        for _, t, m, raw in test:
            pred = np.array([fx[0](m[None])[0], fx[1](m[None])[0]])
            errs.append(np.hypot((pred[0] - t[0]) * 1280, (pred[1] - t[1]) * 900))
            per = np.c_[fx[0](raw), fx[1](raw)]
            noise.append(np.hypot(per[:, 0].std() * 1280, per[:, 1].std() * 900))
        return np.mean(errs), np.mean(noise)
    cross = [score([i for i in items if i[0] != p], [i for i in items if i[0] == p]) for p in postures]
    single = score([i for i in items if i[0] == 0], [i for i in items if i[0] != 0])
    same = score([i for i in items if i[0] == 0], [i for i in items if i[0] == 0])
    return same[0], single[0], [c[0] for c in cross], np.mean([c[1] for c in cross])


def main():
    pts = load(sys.argv[1])
    print(f"{len(pts)} points, {sum(len(v) for v in pts.values())} fresh samples")
    print(f"{'calculation':32} {'fit pose':>9} {'1-pose fit':>11} {'cross-posture (left out 0/1/2/3)':>40} {'frame jitter':>13}")
    for name in NAMES:
        for lam in (.1, 1.0):
            same, single, cross, jitter = evaluate(pts, name, lam)
            print(f"{name:32} {same:9.0f} {single:11.0f}   mean {np.mean(cross):5.0f}  ({' / '.join(f'{c:4.0f}' for c in cross)})  {jitter:10.0f}   ridge={lam}")


if __name__ == "__main__":
    main()
