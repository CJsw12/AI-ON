import sys, glob, os, warnings, json
warnings.filterwarnings("ignore")
import torch, librosa
from transformers import pipeline
asr = pipeline("automatic-speech-recognition", model="openai/whisper-large-v3-turbo",
               dtype=torch.float16, device="cuda:0")
res = {}
for f in sorted(glob.glob(os.path.join(sys.argv[1], "*.wav"))):
    y, sr = librosa.load(f, sr=16000, mono=True)
    out = asr({"raw": y, "sampling_rate": 16000}, return_timestamps=True,
              generate_kwargs={"language": "ko", "task": "transcribe"})
    name = os.path.basename(f)
    res[name] = [{"t": c["timestamp"], "text": c["text"].strip()} for c in out["chunks"]]
    print(f"\n### {name}  ({len(y)/16000:.1f}s)")
    for c in res[name]:
        print(f"  [{c['t'][0]:6.2f}-{(c['t'][1] or 0):6.2f}] {c['text']}")
json.dump(res, open(os.path.join(sys.argv[1], "transcripts.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

