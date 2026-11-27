"""학습 체크포인트를 ONNX로 내보내고 100개 입력의 출력을 대조한다."""
import argparse
import json
from pathlib import Path
import numpy as np
import onnx
import onnxruntime as ort
import torch
from train_mlp import model_for

TOLERANCE = 1e-5


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkpoint', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    checkpoint = torch.load(args.checkpoint, map_location='cpu', weights_only=True)
    net = model_for(checkpoint['hidden'], checkpoint['dropout'], len(checkpoint['labels']))
    net.load_state_dict(checkpoint['state_dict'])
    net.eval()
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    inputs = np.random.default_rng(1701).normal(size=(100, 57)).astype(np.float32)
    # Unity 2.6.1은 opset 7~25 지원. 15는 2.5의 7~15 범위에도 포함된다.
    # https://docs.unity.cn/Packages/com.unity.ai.inference@2.6/manual/supported-models.html
    torch.onnx.export(net, torch.from_numpy(inputs[:1]), str(out), input_names=['features'],
                      output_names=['logits'], dynamic_axes={'features': {0: 'N'}, 'logits': {0: 'N'}},
                      opset_version=15, dynamo=False)
    onnx.checker.check_model(onnx.load(str(out)))
    with torch.no_grad():
        expected = net(torch.from_numpy(inputs)).numpy()
    actual = ort.InferenceSession(str(out), providers=['CPUExecutionProvider']).run(None, {'features': inputs})[0]
    error = float(np.max(np.abs(expected - actual)))
    if error > TOLERANCE:
        raise ValueError(f'ONNX 출력 불일치: 최대 오차 {error} > {TOLERANCE}')
    (out.parent/'labels.json').write_text(json.dumps(checkpoint['labels'], ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'ONNX 일치: 입력 100개, 최대 절대 오차 {error:.9g}, 허용 {TOLERANCE}; {out}')


if __name__ == '__main__':
    main()
