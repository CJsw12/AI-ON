"""CosyVoice2 / Fun-CosyVoice3 로 녹음 목소리 TTS (로컬 GPU)

사용 예:
  cosyvoice\\.venv\\Scripts\\python.exe cosy.py --model v3 --ref "녹음.m4a" --ref-text "녹음에서 말한 문장" "읽힐 문장"
  cosyvoice\\.venv\\Scripts\\python.exe cosy.py --model v2 --ref voice\\me_ref.wav --ref-text "..." --file 대본.txt
"""
import argparse
import os
import subprocess
import sys
import time
import warnings
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CV = ROOT / "cosyvoice"
OUT_DIR = ROOT / "output"
VOICE_DIR = ROOT / "voice"
MODELS = {"v2": CV / "pretrained_models" / "CosyVoice2-0.5B",
          "v3": CV / "pretrained_models" / "Fun-CosyVoice3-0.5B"}
V3_PREFIX = "You are a helpful assistant.<|endofprompt|>"

warnings.filterwarnings("ignore")
os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
sys.path.insert(0, str(CV))
sys.path.insert(0, str(CV / "third_party" / "Matcha-TTS"))
START_CWD = Path.cwd()
os.chdir(CV)  # CosyVoice 내부 상대경로 대비

import torch
import torchaudio
from cosyvoice.cli.cosyvoice import AutoModel

FFMPEG = ROOT / ".venv" / "Scripts" / "ffmpeg.exe"


def clean_reference(src: Path, dst: Path, start=0.0, seconds=20.0):
    af = ("highpass=f=70,afftdn=nf=-25,"
          "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1,areverse,"
          "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1,areverse,"
          "loudnorm=I=-18:TP=-1.5:LRA=11")
    subprocess.run([str(FFMPEG), "-y", "-hide_banner", "-loglevel", "error", "-ss", str(start), "-i", str(src),
                    "-t", str(seconds), "-ac", "1", "-ar", "24000", "-af", af, str(dst)], check=True)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("text", nargs="?")
    p.add_argument("--file")
    p.add_argument("--model", choices=["v2", "v3"], default="v3")
    p.add_argument("--ref", required=True, help="참조 녹음 (m4a/wav 등)")
    p.add_argument("--ref-text", required=True, help="참조 녹음에서 말한 문장 그대로")
    p.add_argument("--seconds", type=float, default=20)
    p.add_argument("--speed", type=float, default=1.0)
    p.add_argument("-o", "--output")
    a = p.parse_args()

    if a.file:
        lines = [l.strip() for l in (START_CWD / a.file).read_text(encoding="utf-8-sig").splitlines() if l.strip()]
    elif a.text:
        lines = [a.text]
    else:
        sys.exit("읽힐 문장 또는 --file 을 주세요.")

    src = (START_CWD / a.ref).resolve()
    VOICE_DIR.mkdir(exist_ok=True)
    OUT_DIR.mkdir(exist_ok=True)
    ref_wav = VOICE_DIR / f"cosy_ref_{src.stem}.wav"
    clean_reference(src, ref_wav, seconds=a.seconds)

    print(f"CosyVoice {a.model} 불러오는 중 (GPU)...", flush=True)
    model = AutoModel(model_dir=str(MODELS[a.model]), fp16=True)
    ref_text = (V3_PREFIX + a.ref_text) if a.model == "v3" else a.ref_text

    stamp = time.strftime("%Y%m%d_%H%M%S")
    for i, line in enumerate(lines, 1):
        t = time.time()
        chunks = [j["tts_speech"] for j in model.inference_zero_shot(
            line, ref_text, str(ref_wav), stream=False, speed=a.speed)]
        audio = torch.cat(chunks, dim=1)
        out = (START_CWD / a.output).resolve() if (a.output and len(lines) == 1) else OUT_DIR / f"cosy{a.model}_{stamp}_{i:03d}.wav"
        torchaudio.save(str(out), audio, model.sample_rate)
        print(f"[{i}/{len(lines)}] {time.time() - t:.1f}초 → {out}", flush=True)


if __name__ == "__main__":
    main()
