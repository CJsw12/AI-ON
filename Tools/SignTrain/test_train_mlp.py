import json
import pathlib
import subprocess
import sys
import tempfile
import unittest
from make_synthetic import make

HERE = pathlib.Path(__file__).parent


class TrainingTests(unittest.TestCase):
    def test_cpu_train_export_and_sealed_exclusion(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            make(root / 'data')
            cmd = [sys.executable, str(HERE/'train_mlp.py'), '--data', str(root/'data'), '--sealed', 'p04', '--device', 'cpu', '--seeds', '1', '--epochs', '50', '--out', str(root/'run'), '--time-report']
            run = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', timeout=100)
            self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
            metrics = json.loads((root/'run'/'metrics.json').read_text(encoding='utf-8'))
            self.assertGreaterEqual(metrics['seeds'][0]['mean_recall'], .8)
            self.assertTrue(all(t['peak_bytes'] > 0 for t in metrics['timings']))
            self.assertNotIn('p04', metrics['seeds'][0]['evaluated_people'])
            self.assertNotIn('p04', metrics['training_people'])
            run = subprocess.run([sys.executable, str(HERE/'export_onnx.py'), '--checkpoint', str(root/'run'/'final.pt'), '--out', str(root/'model.onnx')], capture_output=True, text=True, encoding='utf-8', timeout=30)
            self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
            self.assertTrue((root/'labels.json').exists())

    def test_one_ordinary_person_is_error(self):
        with tempfile.TemporaryDirectory() as tmp:
            make(tmp, people=2)
            run = subprocess.run([sys.executable, str(HERE/'train_mlp.py'), '--data', tmp, '--sealed', 'p02', '--epochs', '1'], capture_output=True, text=True, encoding='utf-8')
            self.assertNotEqual(run.returncode, 0)
            self.assertIn('2명', run.stderr)

    def test_identical_letters_are_confused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            make(root/'data', identical=True)
            run = subprocess.run([sys.executable, str(HERE/'train_mlp.py'), '--data', str(root/'data'), '--sealed', 'p04', '--device', 'cpu', '--seeds', '1', '--epochs', '50', '--min-margin', '0', '--out', str(root/'run')], capture_output=True, text=True, encoding='utf-8', timeout=100)
            self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
            matrix = json.loads((root/'run'/'metrics.json').read_text(encoding='utf-8'))['seeds'][0]['confusion']
            self.assertGreater(matrix.get('B', {}).get('C', 0) + matrix.get('C', {}).get('B', 0), 0)

if __name__ == '__main__':
    unittest.main()
