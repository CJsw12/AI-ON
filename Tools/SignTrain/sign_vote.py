"""SignVote.cs의 시간 창, 과반, 끊김 및 유지 규칙."""
from dataclasses import dataclass
import math

UNKNOWN = -1
NO_HAND = -2


@dataclass
class Result:
    label: int = UNKNOWN
    confirmed: bool = False
    hold_progress: float = 0.0


class SignVote:
    def __init__(self, window=0.33, gap=0.2, hold=0.6):
        if not all(math.isfinite(x) for x in (window, gap, hold)) or window <= 0 or gap <= 0 or hold < 0:
            raise ValueError('시간 설정이 잘못되었습니다')
        self.window, self.gap, self.hold = window * 1000, gap * 1000, hold * 1000
        self.reset()

    def clear(self):
        self.entries = []
        self.held = UNKNOWN
        self.confirmed = False

    def reset(self):
        self.clear()
        self.last = None
        self.hold_start = 0

    def add(self, timestamp, label):
        if label < NO_HAND:
            raise ValueError('결과가 잘못되었습니다')
        if self.last is not None and (timestamp < self.last or timestamp - self.last >= self.gap):
            self.clear()
        self.last = timestamp
        result = Result()
        if label == NO_HAND:
            self.clear()
            return result
        self.entries.append((timestamp, label))
        self.entries = [e for e in self.entries if timestamp - e[0] <= self.window]
        counts = {}
        for _, item in self.entries:
            if item >= 0:
                counts[item] = counts.get(item, 0) + 1
        result.label = next((k for k, v in counts.items() if v > len(self.entries) // 2), UNKNOWN)
        if result.label == UNKNOWN:
            self.held, self.confirmed = UNKNOWN, False
            return result
        if self.held != result.label:
            self.held, self.hold_start, self.confirmed = result.label, timestamp, False
        result.hold_progress = 1.0 if self.hold <= 0 else min(1.0, max(0.0, (timestamp-self.hold_start)/self.hold))
        if not self.confirmed and timestamp-self.hold_start >= self.hold:
            self.confirmed = result.confirmed = True
        return result
