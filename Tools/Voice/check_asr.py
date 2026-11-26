"""Whisper 받아쓰기로 WAV/OGG의 글자 오류율(CER)을 잰다. 품질 검사용(게임에는 들어가지 않는다).
사용: python Tools/Voice/check_asr.py Tools/Voice/<Voice>.asr_in.tsv 결과.json    (generate_qwen.py 가 asr_in.tsv 를 만든다)
  입력 한 줄 = 파일경로<TAB>기대문장(읽기용). 필요: torch, transformers, librosa. 모델: openai/whisper-large-v3-turbo(MIT).
읽는 법: CER 0.25 넘는 줄만 듣는다. Whisper 가 "이십"을 "20"으로 적는 숫자 표기 차이는 오류가 아니다.
  실제 오독(단어 빠짐·바뀜)만 --seed 를 바꿔 다시 만든다. 짧은 감탄사("음…")는 Whisper 가 적지 않는 경우가 많아 CER 로 못 잡는다.
"""
import json, re, sys, warnings
warnings.filterwarnings("ignore")
import torch, librosa
from transformers import pipeline

def norm(s):
    return re.sub(r"[^0-9A-Za-z가-힣]", "", s)

def cer(ref, hyp):
    r, h = norm(ref), norm(hyp)
    d = list(range(len(h) + 1))
    for i in range(1, len(r) + 1):
        prev, d[0] = d[0], i
        for j in range(1, len(h) + 1):
            prev, d[j] = d[j], min(d[j] + 1, d[j - 1] + 1, prev + (r[i - 1] != h[j - 1]))
    return d[len(h)] / max(1, len(r))

asr = pipeline("automatic-speech-recognition", model="openai/whisper-large-v3-turbo",
               dtype=torch.float16, device="cuda:0")
rows = [l.split("\t", 1) for l in open(sys.argv[1], encoding="utf-8-sig").read().splitlines() if "\t" in l]
res = []
for path, expect in rows:
    y, _ = librosa.load(path, sr=16000, mono=True)
    hyp = asr({"raw": y, "sampling_rate": 16000}, return_timestamps=True,
              generate_kwargs={"language": "ko", "task": "transcribe"})["text"].strip()
    c = cer(expect, hyp)
    res.append({"file": path, "sec": round(len(y) / 16000, 1), "cer": round(c, 3), "expect": expect, "heard": hyp})
    print(f"{c:6.3f} {len(y)/16000:5.1f}s {path}\n       heard: {hyp}", flush=True)
json.dump(res, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
