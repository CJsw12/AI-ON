"""OmniVoice: 참조 후보마다 voice prompt 생성 후 문장 생성. 사용: gen_omni.py refs.json texts.json outdir [refkeys,...]"""
import sys, json, os, warnings, time
warnings.filterwarnings("ignore"); os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
from pathlib import Path
import torch, soundfile as sf
from omnivoice import OmniVoice
refs = json.load(open(sys.argv[1], encoding="utf-8")); texts = json.load(open(sys.argv[2], encoding="utf-8-sig"))
out = Path(sys.argv[3]); out.mkdir(parents=True, exist_ok=True)
keys = sys.argv[4].split(",") if len(sys.argv) > 4 else list(refs)
m = OmniVoice.from_pretrained("k2-fsa/OmniVoice", device_map="cuda", dtype=torch.float16)
for k in keys:
    p = m.create_voice_clone_prompt(refs[k]["wav"], ref_text=refs[k]["text"])
    for i, t in enumerate(texts, 1):
        torch.manual_seed(0); t0 = time.time()
        a = m.generate(text=t, language="ko", voice_clone_prompt=p, num_step=48)[0]
        sf.write(str(out / f"omni__{k}__{i:02d}.wav"), a, m.sampling_rate)
        print(f"omni {k} {i} {time.time()-t0:.1f}s", flush=True)
