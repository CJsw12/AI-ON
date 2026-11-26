"""check_asr.py 결과(JSON)를 모아 받아쓰기 오류율(CER)을 요약한다. 같은 파일이 여러 JSON 에 있으면(재생성) 가장 낮은 CER 를 쓴다.
  python Tools/Voice/asr_summary.py 결과1.json 결과2.json ...   [--threshold 0.25] [--list]
  --list: 기준 넘는 줄을 (CER, 길이, 기대문장, 들린 문장) 으로 보여 준다. 숫자 표기 차이("이십"→"20")인지 눈으로 확인한다.
"""
import json
import re
import statistics
import sys
from pathlib import Path


def main():
    args = sys.argv[1:]
    thr = 0.25
    if "--threshold" in args:
        i = args.index("--threshold")
        thr = float(args[i + 1])
        del args[i:i + 2]
    show = "--list" in args
    files = [a for a in args if not a.startswith("--")]
    best = {}
    for f in files:
        folder = str(Path(f).resolve().parent)   # 같은 폴더의 JSON 은 같은 목소리의 같은 대사(파일명이 같음)로 본다.
        for r in json.load(open(f, encoding="utf-8")):
            key = (folder, Path(r["file"]).name)
            if key not in best or r["cer"] < best[key]["cer"]:
                best[key] = r
    rows = list(best.values())
    cers = [r["cer"] for r in rows]
    over = [r for r in rows if r["cer"] > thr]
    digit = [r for r in over if re.search(r"[0-9A-Za-z]", r["heard"])]
    print(f"clips {len(rows)}  mean CER {statistics.mean(cers):.3f}  median {statistics.median(cers):.3f}  "
          f"<=0.10: {sum(c <= 0.10 for c in cers)} ({sum(c <= 0.10 for c in cers) / len(cers):.1%})  "
          f">{thr}: {len(over)} ({len(over) / len(cers):.1%}), 그중 들린 문장에 숫자·영문 있음 {len(digit)}")
    if show:
        for r in sorted(over, key=lambda r: -r["cer"]):
            print(f"{r['cer']:.2f} {r['sec']:.1f}s\n  E: {r['expect']}\n  H: {r['heard']}")


if __name__ == "__main__":
    main()
