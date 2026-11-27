"""녹화 CSV를 게임과 같은 특징과 시도 메타데이터로 읽는다."""
import csv
import math
from dataclasses import dataclass
from pathlib import Path

from sign_features import compute

HEADER = ['person', 'session', 'trial', 'label', 'frame', 'timestamp_ms',
          'width', 'height', 'handedness', 'handedness_score'] + [
              f'{axis}{i}' for i in range(21) for axis in 'xyz']


@dataclass
class Frame:
    person: str
    session: str
    trial: str
    label: str
    frame: int
    timestamp_ms: int
    width: int
    height: int
    handedness: str
    landmarks: list
    features: object


def load(root):
    root = Path(root)
    if not root.is_dir():
        raise ValueError(f'데이터 폴더가 없습니다: {root}')
    files = sorted(root.glob('**/*.csv'))
    if not files:
        raise ValueError('CSV 파일이 없습니다')
    frames = []
    for path in files:
        if len(path.relative_to(root).parts) < 2:
            raise ValueError(f'사람 폴더가 필요합니다: {path}')
        person = path.relative_to(root).parts[0]
        with path.open(encoding='utf-8-sig', newline='') as f:
            reader = csv.reader(f)
            if next(reader, None) != HEADER:
                raise ValueError(f'CSV 헤더 오류: {path}')
            for n, row in enumerate(reader, 2):
                try:
                    if len(row) != 73 or not all(row[i].strip() for i in range(4)):
                        raise ValueError('필드 오류')
                    if row[0] != person:
                        raise ValueError('person과 폴더 가명이 다릅니다')
                    frame, timestamp, width, height = map(int, row[4:8])
                    if width <= 0 or height <= 0 or row[8] not in ('Left', 'Right'):
                        raise ValueError('크기 또는 손 구분 오류')
                    score = float(row[9])
                    landmarks = list(map(float, row[10:]))
                    if not math.isfinite(score) or not all(map(math.isfinite, landmarks)):
                        raise ValueError('유한하지 않은 숫자')
                    features = compute(landmarks, width, height, row[8] == 'Left')
                    frames.append(Frame(*row[:4], frame, timestamp, width, height,
                                        row[8], landmarks, features))
                except (ValueError, OverflowError) as exc:
                    raise ValueError(f'{path}:{n}: {exc}') from exc
    if not frames:
        raise ValueError('CSV에 프레임이 없습니다')
    return frames
