"""ECAPA(VoxCeleb) 화자 유사도로 재채점 + 기존 CER 결합."""
import json, os, glob, warnings
from collections import defaultdict
from pathlib import Path
warnings.filterwarnings("ignore"); os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
import numpy as np, torch, librosa, soundfile as sf
from speechbrain.inference.speaker import EncoderClassifier
from speechbrain.utils.fetching import LocalStrategy

W = Path(__file__).resolve().parent
clips = json.load(open(W / "clips.json", encoding="utf-8"))
old = json.load(open(W / "scores.json", encoding="utf-8"))
cer = {(r["model"], r["ref"]): r["cer"] for r in old["results"]}
SRC = {"daily_A": "rec_13", "casual": "rec_14", "pron3": "rec_15", "emotion3": "rec_17",
       "narration": "rec_19", "long_breath": "rec_20"}
enc = EncoderClassifier.from_hparams("speechbrain/spkrec-ecapa-voxceleb", savedir=str(W / "ecapa"),
                                     run_opts={"device": "cuda"}, local_strategy=LocalStrategy.COPY)
def emb(p):
    y, sr = sf.read(p, dtype="float32")
    if y.ndim > 1: y = y.mean(1)
    y = librosa.resample(y, orig_sr=sr, target_sr=16000)
    with torch.no_grad(): e = enc.encode_batch(torch.tensor(y)[None].cuda())[0, 0]
    e = e.cpu().numpy(); return e / np.linalg.norm(e)
ce = {c["file"]: emb(c["file"]) for c in clips}
def cen(ex=None):
    v = np.stack([e for f, e in ce.items() if not ex or ex not in Path(f).stem]).mean(0); return v / np.linalg.norm(v)
human = [float(e @ cen(Path(f).stem[:6])) for f, e in ce.items()]
print(f"[기준] 실제 녹음 ↔ 내 목소리 평균: {np.mean(human):.3f} (최저 {np.min(human):.3f})\n")
rows = defaultdict(list)
for f in sorted(glob.glob(str(W / "gen" / "*.wav"))):
    m, r, _ = Path(f).stem.split("__"); rows[(m, r)].append(float(emb(f) @ cen(SRC[r])))
res = []
print(f"{'model':8s} {'ref':12s} {'SIM':>6s} {'CER':>6s} score")
for (m, r), v in sorted(rows.items(), key=lambda x: (x[0][0], -np.mean(x[1]))):
    s = float(np.mean(v)); c = cer[(m, r)]; sc = s - 0.5 * c
    res.append({"model": m, "ref": r, "sim": round(s, 4), "cer": c, "score": round(sc, 4)})
    print(f"{m:8s} {r:12s} {s:6.3f} {c:6.3f} {sc:.3f}")
best = {}
for x in res:
    if x["model"] not in best or x["score"] > best[x["model"]]["score"]: best[x["model"]] = x
print("\n[모델별 최적]"); [print(f"  {m}: {x['ref']} SIM {x['sim']:.3f} CER {x['cer']:.3f}") for m, x in best.items()]
json.dump({"human_sim": float(np.mean(human)), "results": res, "best": best},
          open(W / "scores_ecapa.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
