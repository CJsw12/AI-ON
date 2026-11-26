"""CosyVoice2/3: 사용: gen_cosy.py v2|v3 refs.json texts.json outdir [refkeys,...]"""
import sys, json, os, warnings, time
warnings.filterwarnings("ignore"); os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent; CV = ROOT / "cosyvoice"
ver = sys.argv[1]; refs = json.load(open(sys.argv[2], encoding="utf-8")); texts = json.load(open(sys.argv[3], encoding="utf-8-sig"))
out = Path(sys.argv[4]).resolve(); out.mkdir(parents=True, exist_ok=True)
keys = sys.argv[5].split(",") if len(sys.argv) > 5 else list(refs)
sys.path.insert(0, str(CV)); sys.path.insert(0, str(CV / "third_party" / "Matcha-TTS")); os.chdir(CV)
import torch, torchaudio
from cosyvoice.cli.cosyvoice import AutoModel
md = {"v2": "CosyVoice2-0.5B", "v3": "Fun-CosyVoice3-0.5B"}[ver]
m = AutoModel(model_dir=str(CV / "pretrained_models" / md), fp16=True)
pre = "You are a helpful assistant.<|endofprompt|>" if ver == "v3" else ""
for k in keys:
    for i, t in enumerate(texts, 1):
        torch.manual_seed(0); t0 = time.time()
        a = torch.cat([j["tts_speech"] for j in m.inference_zero_shot(t, pre + refs[k]["text"], refs[k]["wav"], stream=False)], 1)
        torchaudio.save(str(out / f"cosy{ver}__{k}__{i:02d}.wav"), a, m.sample_rate)
        print(f"cosy{ver} {k} {i} {time.time()-t0:.1f}s", flush=True)
