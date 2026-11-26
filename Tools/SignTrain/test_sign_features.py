import json
import pathlib
import unittest
from sign_features import compute, FEATURE_COUNT

class FeatureFixtureTest(unittest.TestCase):
    def test_small_palm(self):
        points = [0.0] * 63
        points[27] = 0.01
        self.assertIsNone(compute(points, 640, 480, False))
        self.assertIsNotNone(compute(points, 640, 480, False, 5.0))

    def test_boundaries(self):
        points = [0.0] * 63
        points[27] = 0.1
        original = compute(points, 640, 480, False)
        self.assertAlmostEqual(original[42], 3.141592653589793)
        points[2] = 100.0
        self.assertEqual(original, compute(points, 640, 480, False))
        self.assertIsNone(compute(points[:-1], 640, 480, False))
        points[1] = float("nan")
        self.assertIsNone(compute(points, 640, 480, False))
        self.assertIsNone(compute([0.0] * 63, 640, 480, False, float("inf")))
    def test_fixtures(self):
        path = pathlib.Path(__file__).resolve().parents[2] / "Tests" / "Fixtures" / "sign-features.json"
        cases = json.loads(path.read_text(encoding="utf-8-sig"))
        self.assertEqual(len(cases), 10)
        for case in cases:
            actual = compute(case["landmarks"], case["width"], case["height"], case["isLeft"])
            self.assertEqual(len(actual), FEATURE_COUNT)
            for a, b in zip(actual, case["features"]):
                self.assertLessEqual(abs(a-b), 1e-5)

if __name__ == "__main__":
    unittest.main()
