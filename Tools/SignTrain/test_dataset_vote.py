import csv
import json
import pathlib
import tempfile
import unittest

import dataset
from sign_vote import SignVote


class DatasetVoteTests(unittest.TestCase):
    def test_dataset_and_invalid_person(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = pathlib.Path(tmp) / 'p1' / 's.csv'
            path.parent.mkdir()
            points = [0.0] * 63
            points[27] = 0.1
            with path.open('w', encoding='utf-8-sig', newline='') as f:
                writer = csv.writer(f)
                writer.writerow(dataset.HEADER)
                writer.writerow(['p1', 's', '1', 'A', 1, 0, 640, 480, 'Left', 1] + points)
                writer.writerow(['p1', 's', '1', 'A', 2, 50, 640, 480, 'Right', 1] + [0]*63)
            frames = dataset.load(tmp)
            self.assertEqual(len(frames[0].features), 57)
            self.assertIsNone(frames[1].features)
            text = path.read_text(encoding='utf-8-sig').replace('p1,s,', 'wrong,s,')
            path.write_text(text, encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'person'):
                dataset.load(tmp)

    def test_vote_against_csharp(self):
        path = pathlib.Path(__file__).parent / 'fixtures' / 'sign_vote_cases.json'
        cases = json.loads(path.read_text(encoding='utf-8-sig'))
        for case in cases:
            vote = SignVote()
            for entry in case['entries']:
                result = vote.add(entry['time'], entry['input'])
                self.assertEqual(result.label, entry['label'], case['name'])
                self.assertEqual(result.confirmed, entry['confirmed'], case['name'])
                self.assertAlmostEqual(result.hold_progress, entry['progress'], places=6)

if __name__ == '__main__':
    unittest.main()
