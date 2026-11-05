"""녹음된 목소리로 TTS 만들기 (OmniVoice, 로컬 GPU)

1) 목소리 등록 (한 번만):
   python clone.py register "C:\\경로\\녹음.m4a" --name me
2) 문장 읽히기:
   python clone.py say "읽힐 문장" --name me
   python clone.py say --file 대본.txt --name me     (한 줄 = 한 파일)
"""
import argparse
import os
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
VOICE_DIR = ROOT / "voice"
OUT_DIR = ROOT / "output"
os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
os.environ["PATH"] = str(Path(sys.executable).parent) + os.pathsep + os.environ["PATH"]  # ffmpeg.exe in venv

import warnings
warnings.filterwarnings("ignore")

import soundfile as sf
import torch
from omnivoice import OmniVoice
from omnivoice.models.omnivoice import VoiceClonePrompt

torch.backends.cuda.matmul.allow_tf32 = True
torch.backends.cudnn.allow_tf32 = True


def ffmpeg_exe():
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def clean_reference(src: Path, dst: Path, start: float, seconds: float):
    """iPhone m4a 등 어떤 형식이든 → 24kHz 모노 wav, 저역 잡음 제거, 음량 정규화, 앞뒤 무음 제거."""
    af = ",".join([
        "highpass=f=70",
        "afftdn=nf=-25",
        "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1",
        "areverse",
        "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1",
        "areverse",
        "loudnorm=I=-18:TP=-1.5:LRA=11",
    ])
    cmd = [ffmpeg_exe(), "-y", "-hide_banner", "-loglevel", "error",
           "-ss", str(start), "-i", str(src), "-t", str(seconds),
           "-ac", "1", "-ar", "24000", "-af", af, str(dst)]
    subprocess.run(cmd, check=True)


def load_model(with_asr=False):
    print("모델 불러오는 중 (GPU)...", flush=True)
    model = OmniVoice.from_pretrained("k2-fsa/OmniVoice", device_map="cuda", dtype=torch.float16)
    if with_asr:
        print("받아쓰기 모델(Whisper) 불러오는 중...", flush=True)
        model.load_asr_model()
    return model


def cmd_register(a):
    src = Path(a.audio)
    if not src.exists():
        sys.exit(f"파일이 없습니다: {src}")
    VOICE_DIR.mkdir(exist_ok=True)
    ref_wav = VOICE_DIR / f"{a.name}_ref.wav"
    clean_reference(src, ref_wav, a.start, a.seconds)
    info = sf.info(str(ref_wav))
    print(f"정리된 참조 음성: {ref_wav} ({info.duration:.1f}초)")
    if info.duration < 3:
        print("경고: 참조 음성이 3초보다 짧습니다. 5~15초 분량을 권장합니다.")

    model = load_model(with_asr=a.ref_text is None)
    prompt = model.create_voice_clone_prompt(str(ref_wav), ref_text=a.ref_text)
    prompt.save(str(VOICE_DIR / f"{a.name}.pt"))
    print(f"\n받아쓴 내용: {prompt.ref_text}")
    print("→ 틀린 부분이 있으면 --ref-text \"정확한 문장\" 으로 다시 등록하면 품질이 좋아집니다.")
    print(f"목소리 '{a.name}' 등록 완료: {VOICE_DIR / (a.name + '.pt')}")


def cmd_say(a):
    pt = VOICE_DIR / f"{a.name}.pt"
    if not pt.exists():
        sys.exit(f"등록된 목소리가 없습니다: {a.name}  (먼저 register 하세요)")
    if a.file:
        lines = [l.strip() for l in Path(a.file).read_text(encoding="utf-8-sig").splitlines() if l.strip()]
    elif a.text:
        lines = [a.text]
    else:
        sys.exit("읽힐 문장 또는 --file 을 주세요.")

    model = load_model()
    prompt = VoiceClonePrompt.load(str(pt))
    OUT_DIR.mkdir(exist_ok=True)
    stamp = time.strftime("%Y%m%d_%H%M%S")
    for i, line in enumerate(lines, 1):
        t = time.time()
        audio = model.generate(text=line, language=a.lang, voice_clone_prompt=prompt,
                               speed=a.speed, num_step=a.steps, normalize_text=True)[0]
        if a.output and len(lines) == 1:
            out = Path(a.output)
        else:
            out = OUT_DIR / f"{a.name}_{stamp}_{i:03d}.wav"
        sf.write(str(out), audio, model.sampling_rate)
        print(f"[{i}/{len(lines)}] {time.time() - t:.1f}초  → {out}")


def main():
    p = argparse.ArgumentParser(description="녹음 목소리 TTS")
    sub = p.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("register", help="녹음 파일로 목소리 등록")
    r.add_argument("audio")
    r.add_argument("--name", default="me")
    r.add_argument("--ref-text", default=None, help="녹음에서 말한 정확한 문장 (없으면 자동 받아쓰기)")
    r.add_argument("--start", type=float, default=0, help="녹음에서 사용할 시작 위치(초)")
    r.add_argument("--seconds", type=float, default=20, help="사용할 길이(초), 권장 10~20")
    r.set_defaults(func=cmd_register)

    s = sub.add_parser("say", help="등록한 목소리로 읽기")
    s.add_argument("text", nargs="?")
    s.add_argument("--file")
    s.add_argument("--name", default="me")
    s.add_argument("-o", "--output")
    s.add_argument("--lang", default="ko")
    s.add_argument("--speed", type=float, default=None)
    s.add_argument("--steps", type=int, default=48, help="품질 단계 (기본 48, 기본 모델 32보다 정교)")
    s.set_defaults(func=cmd_say)

    a = p.parse_args()
    a.func(a)


if __name__ == "__main__":
    main()
