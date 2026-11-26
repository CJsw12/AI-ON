"""녹음 하나로 OmniVoice / CosyVoice2 / CosyVoice3 비교 샘플 생성.
사용: .venv\Scripts\python.exe compare.py "녹음.m4a" [--ref-text-file ref_text.txt]"""
import argparse, os, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parent
TEST = ("안녕하세요, 이건 제 목소리로 만든 인공지능 음성입니다. 오늘 하루도 정말 수고 많으셨어요. "
        "내일은 날씨가 맑다고 하니, 가까운 공원에 산책이라도 다녀오는 건 어떨까요?")
p = argparse.ArgumentParser(); p.add_argument("audio"); p.add_argument("--ref-text-file", default=str(ROOT / "ref_text.txt"))
p.add_argument("--text", default=TEST); a = p.parse_args()
audio = str(Path(a.audio).resolve()); ref = Path(a.ref_text_file).read_text(encoding="utf-8-sig").strip()
env = dict(os.environ, PYTHONIOENCODING="utf-8"); out = ROOT / "output"; out.mkdir(exist_ok=True)
omni = str(ROOT / ".venv" / "Scripts" / "python.exe"); cosy = str(ROOT / "cosyvoice" / ".venv" / "Scripts" / "python.exe")
steps = [("OmniVoice 등록", [omni, "clone.py", "register", audio, "--name", "me", "--ref-text", ref]),
         ("OmniVoice", [omni, "clone.py", "say", a.text, "--name", "me", "-o", str(out / "비교_1_OmniVoice.wav")]),
         ("CosyVoice2", [cosy, "cosy.py", "--model", "v2", "--ref", audio, "--ref-text", ref, a.text, "-o", str(out / "비교_2_CosyVoice2.wav")]),
         ("CosyVoice3", [cosy, "cosy.py", "--model", "v3", "--ref", audio, "--ref-text", ref, a.text, "-o", str(out / "비교_3_CosyVoice3.wav")])]
for name, cmd in steps:
    print(f"\n===== {name} =====", flush=True)
    r = subprocess.run(cmd, cwd=ROOT, env=env)
    if r.returncode: print(f"!! {name} 실패 (코드 {r.returncode})")
print(f"\n완료. 비교 파일: {out}\비교_*.wav")
