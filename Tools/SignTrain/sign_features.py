"""C# 실행 경로와 같은 2차원 손 특징을 계산한다."""
import math

FEATURE_COUNT = 57
MINIMUM_PALM_SIZE = 10.0

def compute(landmarks, width, height, is_left, minimum_palm_size=MINIMUM_PALM_SIZE):
    if not math.isfinite(minimum_palm_size) or minimum_palm_size <= 0 or len(landmarks) != 63 or width <= 0 or height <= 0 or not all(math.isfinite(v) for v in landmarks):
        return None
    x = [(landmarks[3*i] - landmarks[0]) * width * (-1 if is_left else 1) for i in range(21)]
    y = [(landmarks[3*i+1] - landmarks[1]) * height for i in range(21)]
    scale = math.hypot(x[9], y[9])
    if not math.isfinite(scale) or scale < minimum_palm_size:
        return None
    x = [v / scale for v in x]
    y = [v / scale for v in y]
    result = [v for i in range(21) for v in (x[i], y[i])]
    for finger in range(5):
        start = 1 + finger * 4
        for j in range(3):
            a, b, c = (0 if j == 0 else start+j-1), start+j, start+j+1
            ax, ay, bx, by = x[a]-x[b], y[a]-y[b], x[c]-x[b], y[c]-y[b]
            la, lb = math.hypot(ax, ay), math.hypot(bx, by)
            cosine = -1 if la < 1e-6 or lb < 1e-6 else (ax*bx+ay*by)/(la*lb)
            result.append(math.acos(max(-1, min(1, cosine))))
    return result
