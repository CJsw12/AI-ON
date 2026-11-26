"""생성 음성 채점: 화자 유사도(SIM, WavLM-SV) + 발음 정확도(CER, Whisper)."""
import json, os, re, sys, warnings, glob
from collections import defaultdict
from pathlib import Path
warnings.filterwarnings("ignore"); os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
import numpy as np, torch, librosa, soundfile as sf
from transformers import AutoFeatureExtractor, WavLMForXVector, pipeline

W = Path(__file__).resolve().parent
gen_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else W / "gen"
texts = json.load(open(W / "eval_texts.json", encoding="utf-8-sig"))
clips = json.load(open(W / "clips.json", encoding="utf-8"))
SRC = {"daily_A": "rec_13", "casual": "rec_14", "pron3": "rec_15", "emotion3": "rec_17",
       "narration": "rec_19", "long_breath": "rec_20"}

fe = AutoFeatureExtractor.from_pretrained("microsoft/wavlm-base-plus-sv")
sv = WavLMForXVector.from_pretrained("microsoft/wavlm-base-plus-sv").cuda().eval()
def emb(path):
    y, sr = sf.read(path, dtype="float32")
    if y.ndim > 1: y = y.mean(1)
    y = librosa.resample(y, orig_sr=sr, target_sr=16000)
    x = fe(y, sampling_rate=16000, return_tensors="pt").to("cuda")
    with torch.no_grad(): e = sv(**x).embeddings[0]
    return torch.nn.functional.normalize(e, dim=-1).cpu().numpy()

asr = pipeline("automatic-speech-recognition", model="openai/whisper-large-v3-turbo", dtype=torch.float16, device="cuda:0")
def norm(s): return re.sub(r"[^\w]", "", s)
def cer(ref, hyp):
    r, h = norm(ref), norm(hyp); d = list(range(len(h) + 1))
    for i in range(1, len(r) + 1):
        p, d[0] = d[0], i
        for j in range(1, len(h) + 1):
            p, d[j] = d[j], min(d[j] + 1, d[j - 1] + 1, p + (r[i - 1] != h[j - 1]))
    return d[len(h)] / max(1, len(r))
def transcribe(path):
    y, sr = sf.read(path, dtype="float32")
    if y.ndim > 1: y = y.mean(1)
    y = librosa.resample(y, orig_sr=sr, target_sr=16000)
    return asr({"raw": y, "sampling_rate": 16000}, generate_kwargs={"language": "ko", "task": "transcribe"})["text"]

clip_emb = {c["file"]: emb(c["file"]) for c in clips}
def centroid(exclude=None):
    v = np.stack([e for f, e in clip_emb.items() if not exclude or exclude not in Path(f).stem])
    c = v.mean(0); return c / np.linalg.norm(c)
allc = centroid()
human = np.mean([float(e @ centroid(Path(f).stem[:6])) for f, e in clip_emb.items()])
print(f"[기준] 실제 녹음끼리 유사도(사람 상한선): {human:.3f}\n")

rows = defaultdict(list)
for f in sorted(glob.glob(str(gen_dir / "*.wav"))):
    model, ref, idx = Path(f).stem.split("__")
    c = centroid(SRC.get(ref))
    sim = float(emb(f) @ c)
    hyp = transcribe(f); er = cer(texts[int(idx) - 1], hyp)
    rows[(model, ref)].append((sim, er, hyp))

result = []
print(f"{'model':8s} {'ref':12s} {'SIM':>6s} {'CER':>6s}  score")
for (model, ref), v in sorted(rows.items()):
    sim = float(np.mean([x[0] for x in v])); er = float(np.mean([x[1] for x in v]))
    score = sim - 0.5 * er
    result.append({"model": model, "ref": ref, "sim": round(sim, 4), "cer": round(er, 4), "score": round(score, 4),
                   "hyp": [x[2] for x in v]})
    print(f"{model:8s} {ref:12s} {sim:6.3f} {er:6.3f}  {score:.3f}")

best = {}
for r in result:
    if r["model"] not in best or r["score"] > best[r["model"]]["score"]: best[r["model"]] = r
print("\n[모델별 최적 참조]")
for m, r in best.items(): print(f"  {m}: {r['ref']}  SIM {r['sim']:.3f}  CER {r['cer']:.3f}")
json.dump({"human_sim": human, "results": result, "best": best}, open(W / "scores.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
