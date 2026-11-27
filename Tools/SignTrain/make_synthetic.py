"""실제 참여자 정보가 없는 재현 가능한 CSV를 만든다."""
import argparse
import csv
import math
from pathlib import Path
import random
from dataset import HEADER


def make(root, identical=False, people=4):
    rng = random.Random(1701)
    for person in range(people):
        path = Path(root) / f'p{person+1:02}' / 's1.csv'
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open('w', encoding='utf-8', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(HEADER)
            for label_index, label in enumerate(('A', 'B', 'C', '_other')):
                shape = 1 if identical and label == 'C' else label_index
                for trial in range(3):
                    for frame in range(8):
                        points = []
                        for i in range(21):
                            finger, joint = divmod(max(0, i-1), 4)
                            if i == 0:
                                x, y = .5, .7
                            else:
                                x = .5 + (finger-2)*.035 + joint*.008*math.sin(shape + finger)
                                y = .65 - joint*.05 + .025*math.sin(shape*1.4 + finger*.8)*joint
                            points.extend([x+rng.uniform(-.0002,.0002), y+rng.uniform(-.0002,.0002), 0])
                        writer.writerow([f'p{person+1:02}', 's1', str(trial), label, frame, frame*100, 640, 480, 'Right', .99] + points)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('out')
    parser.add_argument('--identical', action='store_true')
    args = parser.parse_args()
    make(args.out, args.identical)
