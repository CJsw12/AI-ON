"""사람 단위 교차 검증으로 57차원 수어 MLP를 학습한다."""
import argparse
from collections import Counter, defaultdict
from datetime import datetime
import hashlib
import json
import math
import os
from pathlib import Path
import random
import sys
import time

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
import torch
from torch import nn

import dataset
from sign_features import compute
from sign_vote import SignVote, NO_HAND, UNKNOWN

torch.set_num_threads(1)


def model_for(hidden, dropout, classes):
    layers = []
    previous = 57
    for size in hidden:
        layers.extend((nn.Linear(previous, size), nn.ReLU(), nn.Dropout(dropout)))
        previous = size
    layers.append(nn.Linear(previous, classes))
    return nn.Sequential(*layers)


def augmented(frame, rng):
    # 픽셀 좌표에서 회전·배율·잡음을 적용한 다음 동일한 compute를 호출한다.
    p = np.array(frame.landmarks, dtype=np.float64).reshape(21, 3).copy()
    xy = p[:, :2] * [frame.width, frame.height]
    origin = xy[0].copy()
    angle = rng.uniform(-math.pi/12, math.pi/12)
    scale = rng.uniform(.9, 1.1)
    rot = np.array([[math.cos(angle), -math.sin(angle)], [math.sin(angle), math.cos(angle)]])
    palm = np.linalg.norm(xy[9] - origin)
    xy = (xy - origin) @ rot.T * scale + origin + rng.normal(0, palm*.003, (21, 2))
    p[:, :2] = xy / [frame.width, frame.height]
    result = compute(p.ravel().tolist(), frame.width, frame.height, frame.handedness == 'Left')
    return frame.features if result is None else result


def peak_rss():
    if sys.platform == 'win32':
        import ctypes
        from ctypes import wintypes
        class Counters(ctypes.Structure):
            _fields_ = [('cb', wintypes.DWORD), ('PageFaultCount', wintypes.DWORD)] + [(x, ctypes.c_size_t) for x in ('PeakWorkingSetSize', 'WorkingSetSize', 'QuotaPeakPagedPoolUsage', 'QuotaPagedPoolUsage', 'QuotaPeakNonPagedPoolUsage', 'QuotaNonPagedPoolUsage', 'PagefileUsage', 'PeakPagefileUsage')]
        c = Counters()
        c.cb = ctypes.sizeof(c)
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        psapi = ctypes.WinDLL('psapi', use_last_error=True)
        kernel.GetCurrentProcess.restype = wintypes.HANDLE
        psapi.GetProcessMemoryInfo.argtypes = [wintypes.HANDLE, ctypes.POINTER(Counters), wintypes.DWORD]
        psapi.GetProcessMemoryInfo.restype = wintypes.BOOL
        process = kernel.GetCurrentProcess()
        if psapi.GetProcessMemoryInfo(process, ctypes.byref(c), c.cb):
            return c.PeakWorkingSetSize
        return None
    import resource
    value = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    return value if sys.platform == 'darwin' else value * 1024


def fit(train, valid, labels, args, seed, device):
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if device.type == 'cuda':
        torch.cuda.manual_seed_all(seed)
        torch.cuda.reset_peak_memory_stats(device)
    rng = np.random.default_rng(seed)
    samples = [f for f in train if f.features is not None]
    if not samples or set(f.label for f in samples) != set(labels):
        raise ValueError('각 학습 분할에 모든 클래스의 유효 프레임이 필요합니다')
    index = {label: i for i, label in enumerate(labels)}
    net = model_for(args.hidden, args.dropout, len(labels)).to(device)
    opt = torch.optim.Adam(net.parameters(), lr=args.lr)
    history, times = [], []
    for epoch in range(args.epochs):
        start = time.perf_counter()
        net.train()
        order = rng.permutation(len(samples))
        loss_sum = 0.0
        for start_index in range(0, len(order), args.batch):
            batch = [samples[i] for i in order[start_index:start_index+args.batch]]
            x = np.asarray([augmented(f, rng) if args.augment else f.features for f in batch], dtype=np.float32)
            y = torch.tensor([index[f.label] for f in batch], dtype=torch.long, device=device)
            opt.zero_grad()
            loss = nn.functional.cross_entropy(net(torch.from_numpy(x).to(device)), y)
            loss.backward()
            opt.step()
            loss_sum += loss.item() * len(batch)
        if device.type == 'cuda':
            torch.cuda.synchronize(device)
        elapsed = time.perf_counter() - start
        if epoch > 0:
            times.append(elapsed)
        net.eval()
        usable = [f for f in valid if f.features is not None]
        accuracy = None
        if usable:
            with torch.no_grad():
                x = torch.tensor(np.asarray([f.features for f in usable], dtype=np.float32), device=device)
                predicted = net(x).argmax(1).cpu().tolist()
            accuracy = sum(labels[i] == f.label for i, f in zip(predicted, usable)) / len(usable)
        history.append({'loss': loss_sum/len(samples), 'validation_accuracy': accuracy})
    memory = torch.cuda.max_memory_allocated(device) if device.type == 'cuda' else peak_rss()
    return net, history, {'epochs_measured': len(times), 'training_seconds': sum(times),
                          'seconds_per_epoch': sum(times)/len(times) if times else None,
                          'peak_bytes': memory, 'memory_kind': 'cuda_allocated' if device.type == 'cuda' else 'process_peak_rss'}


def evaluate(net, frames, labels, margin, device):
    usable = [f for f in frames if f.features is not None]
    predictions = {}
    net.eval()
    if usable:
        with torch.no_grad():
            x = torch.tensor(np.asarray([f.features for f in usable], dtype=np.float32), device=device)
            probs = torch.softmax(net(x), dim=1).cpu().numpy()
        for frame, p in zip(usable, probs):
            order = np.argsort(-p, kind='stable')
            winner = int(order[0])
            difference = float(p[winner]-p[order[1]])
            predictions[id(frame)] = UNKNOWN if labels[winner] == '_other' or difference < margin or difference == 0 else winner
    trials = defaultdict(list)
    for f in frames:
        trials[(f.person, f.session, f.label, f.trial)].append(f)
    matrix = defaultdict(Counter)
    positive = success = 0
    for (_, _, truth, _), trial in sorted(trials.items()):
        trial = sorted(trial, key=lambda f: (f.timestamp_ms, f.frame))
        vote = SignVote()
        result, when = '거절', None
        for f in trial:
            v = vote.add(f.timestamp_ms, predictions.get(id(f), NO_HAND))
            if v.confirmed:
                result, when = labels[v.label], f.timestamp_ms
                break
        matrix[truth][result] += 1
        if truth != '_other':
            positive += 1
            if result == truth and when <= 5000:
                success += 1
    rates = {label: counts.get(label, 0)/sum(counts.values()) for label, counts in matrix.items() if label != '_other'}
    other = matrix.get('_other', {})
    false_other = sum(v for k, v in other.items() if k != '거절')
    return {'confusion': {k: dict(v) for k, v in matrix.items()}, 'recall': rates,
            'mean_recall': float(np.mean(list(rates.values()))) if rates else None,
            'min_recall': min(rates.values()) if rates else None,
            'other_false_rate': false_other/sum(other.values()) if other else None,
            'success_5s': success/positive if positive else None,
            'evaluated_people': sorted({f.person for f in frames})}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--data', required=True)
    parser.add_argument('--sealed', required=True)
    parser.add_argument('--device', choices=['auto', 'cpu', 'cuda'], default='auto')
    parser.add_argument('--seeds', type=int, nargs='+', default=[1, 2, 3])
    parser.add_argument('--epochs', type=int, default=200)
    parser.add_argument('--hidden', type=int, nargs='+', default=[128, 64])
    parser.add_argument('--dropout', type=float, default=.2)
    parser.add_argument('--lr', type=float, default=.001)
    parser.add_argument('--batch', type=int, default=64)
    parser.add_argument('--augment', action='store_true')
    parser.add_argument('--min-margin', type=float, default=.2)
    parser.add_argument('--out', default=None)
    parser.add_argument('--final', action='store_true')
    parser.add_argument('--time-report', action='store_true')
    args = parser.parse_args()
    if args.epochs < 1 or args.batch < 1 or any(n < 1 for n in args.hidden) or not 0 <= args.dropout < 1 or not 0 <= args.min_margin <= 1 or not math.isfinite(args.lr) or args.lr <= 0:
        parser.error('학습 인수 범위가 잘못되었습니다')
    if args.final and len(args.seeds) != 1:
        parser.error('봉인 평가는 한 번만: --final과 시드 하나를 지정하세요')
    if args.device == 'cuda' and not torch.cuda.is_available():
        parser.error('CUDA를 사용할 수 없습니다')
    device = torch.device('cuda' if args.device == 'cuda' or (args.device == 'auto' and torch.cuda.is_available()) else 'cpu')
    frames = dataset.load(args.data)
    people = sorted({f.person for f in frames})
    if args.sealed not in people:
        raise ValueError('봉인 평가자 폴더가 없습니다')
    ordinary = [p for p in people if p != args.sealed]
    if len(ordinary) < 2:
        raise ValueError('봉인 평가자를 제외한 사람이 2명 이상 필요합니다')
    train_all = [f for f in frames if f.person in ordinary]
    labels = sorted({f.label for f in train_all if f.features is not None})
    if '_other' not in labels or len(labels) < 2:
        raise ValueError('_other와 하나 이상의 글자가 필요합니다')
    out = Path(args.out) if args.out else Path(__file__).parent/'runs'/datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    out.mkdir(parents=True, exist_ok=False)
    files = sorted(Path(args.data).glob('**/*.csv'))
    config = vars(args).copy()
    config.update({'resolved_device': str(device), 'labels': labels, 'training_people': ordinary,
                   'data_files': [{'path': str(p.relative_to(args.data)), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]})
    (out/'config.json').write_text(json.dumps(config, ensure_ascii=False, indent=2), encoding='utf-8')
    if args.final:
        print('경고: 봉인 최종 평가는 한 번만 수행하세요. 반복 실행 여부는 사람이 관리해야 합니다.')
    all_results, curves, timings = [], [], []
    for seed in args.seeds:
        eval_people = [args.sealed] if args.final else ordinary
        combined = []
        for person in eval_people:
            tr = [f for f in train_all if args.final or f.person != person]
            val = [f for f in frames if f.person == person]
            net, history, timing = fit(tr, val, labels, args, seed, device)
            # 모델이 평가 대상마다 다르므로 예측 결과를 결합한다.
            result = evaluate(net, val, labels, args.min_margin, device)
            curves.append((f'{seed}/{person}', history))
            timings.append({'seed': seed, 'fold': person, **timing})
            combined.append(result)
            print(f'시드 {seed} 평가자 {person}: 평균 재현율 {result["mean_recall"]:.3f}, 마지막 손실 {history[-1]["loss"]:.4f}')
        # fold 혼동표를 합쳐 시도 단위 재현율을 다시 계산한다.
        matrix = defaultdict(Counter)
        for result in combined:
            for truth, row in result['confusion'].items():
                matrix[truth].update(row)
        rates = {k: v.get(k, 0)/sum(v.values()) for k, v in matrix.items() if k != '_other'}
        other = matrix.get('_other', {})
        positives = sum(sum(v.values()) for k,v in matrix.items() if k != '_other')
        successes = sum((r['success_5s'] or 0) * sum(sum(v.values()) for k,v in r['confusion'].items() if k != '_other') for r in combined)
        avg = float(np.mean(list(rates.values()))) if rates else None
        all_results.append({'seed': seed, 'evaluated_people': eval_people, 'confusion': {k: dict(v) for k,v in matrix.items()}, 'recall': rates, 'mean_recall': avg, 'min_recall': min(rates.values()) if rates else None, 'other_false_rate': sum(v for k,v in other.items() if k != '거절')/sum(other.values()) if other else None, 'success_5s': successes/positives if positives else None})
    final_net, final_history, final_timing = fit(train_all, [], labels, args, args.seeds[0], device)
    timings.append({'seed': args.seeds[0], 'fold': 'final_training', **final_timing})
    torch.save({'state_dict': final_net.cpu().state_dict(), 'hidden': args.hidden, 'dropout': args.dropout, 'labels': labels, 'feature_count': 57, 'min_margin': args.min_margin}, out/'final.pt')
    means = [r['mean_recall'] for r in all_results if r['mean_recall'] is not None]
    timing_summary = {'training_seconds_total': sum(t['training_seconds'] for t in timings),
                      'epochs_measured_total': sum(t['epochs_measured'] for t in timings),
                      'peak_bytes': max((t['peak_bytes'] for t in timings if t['peak_bytes'] is not None), default=None)}
    timing_summary['seconds_per_epoch'] = (timing_summary['training_seconds_total']/timing_summary['epochs_measured_total'] if timing_summary['epochs_measured_total'] else None)
    metrics = {'training_people': ordinary, 'seeds': all_results, 'mean_recall_across_seeds': float(np.mean(means)) if means else None, 'std_recall_across_seeds': float(np.std(means)) if means else None, 'timings': timings if args.time_report else [], 'timing_summary': timing_summary if args.time_report else None}
    (out/'metrics.json').write_text(json.dumps(metrics, ensure_ascii=False, indent=2), encoding='utf-8')
    fig, axes = plt.subplots(1, 2, figsize=(12, 4))
    for name, history in curves:
        axes[0].plot([v['loss'] for v in history], label=name)
        axes[1].plot([v['validation_accuracy'] for v in history], label=name)
    axes[0].set_title('Training loss'); axes[1].set_title('Validation frame accuracy')
    for ax in axes:
        ax.set_xlabel('Epoch'); ax.legend(fontsize='x-small')
    fig.tight_layout(); fig.savefig(out/'learning_curves.png'); plt.close(fig)
    lines = ['# 수어 MLP 측정', '', '손 없음은 녹화에 대개 저장되지 않아 전체 오인식률은 미측정; 5초 성공률은 녹화 시간 기준 근사.', f'장치: {device}; 봉인: {args.sealed}; 모드: {"최종" if args.final else "교차 검증"}', f'시드 평균 재현율: {metrics["mean_recall_across_seeds"]}; 표준편차: {metrics["std_recall_across_seeds"]}']
    for r in all_results:
        a = r['mean_recall']
        decision = '미측정' if a is None else ('범위 축소 검토' if a < .6 else '혼동 쌍 제거 후 재측정' if a < .8 else '글자별 80% 이상 및 오인식률 10% 이하를 각각 확인')
        lines += ['', f'## 시드 {r["seed"]}', f'평균 재현율 {a}, 최저 {r["min_recall"]}: {decision}', f'그 외 오인식률 {r["other_false_rate"]}; 5초 안 성공률(근사) {r["success_5s"]}', '글자별 재현율: ' + json.dumps(r['recall'], ensure_ascii=False), '혼동표(실제 → 예측 → 시도 수): ' + json.dumps(r['confusion'], ensure_ascii=False)]
    if args.time_report:
        lines += ['', '## 시간 및 최대 메모리 (각 학습의 첫 에폭 제외)', 'CPU 메모리는 학습 시작 전 사용량도 포함한 프로세스 전체의 누적 최대 RSS 근사입니다.', json.dumps(timing_summary, ensure_ascii=False), *[json.dumps(t, ensure_ascii=False) for t in timings]]
    (out/'report.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print(f'결과: {out}; 평균 재현율 {metrics["mean_recall_across_seeds"]}, 표준편차 {metrics["std_recall_across_seeds"]}')
    if args.time_report:
        print('시간·메모리:', json.dumps(timings, ensure_ascii=False))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as exc:
        print(f'오류: {exc}', file=sys.stderr)
        sys.exit(1)
