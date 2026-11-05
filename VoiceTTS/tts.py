"""내 목소리 TTS 통합 실행기 (모델별 최적 참조 자동 사용)

  .venv\\Scripts\\python.exe tts.py "읽힐 문장"                 → 1등 모델로 생성
  .venv\\Scripts\\python.exe tts.py "읽힐 문장" --model all     → 세 모델 모두
  .venv\\Scripts\\python.exe tts.py --file 대본.txt --model v3  → 여러 줄
  모델: omni / v2 / v3 / all   (voice\\best.json 에 모델별 최적 참조가 저장되어 있음)
"""
import argparse, json, os, subprocess, sys, tempfile, time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
BEST = json.load(open(ROOT / "voice" / "best.json", encoding="utf-8"))
PY_OMNI = ROOT / ".venv" / "Scripts" / "python.exe"
PY_COSY = ROOT / "cosyvoice" / ".venv" / "Scripts" / "python.exe"

p = argparse.ArgumentParser()
p.add_argument("text", nargs="?")
p.add_argument("--file")
p.add_argument("--model", default=BEST["ranking"][0], choices=["omni", "v2", "v3", "all"])
p.add_argument("--out", default=str(ROOT / "output"))
a = p.parse_args()
if a.file:
    lines = [l.strip() for l in Path(a.file).read_text(encoding="utf-8-sig").splitlines() if l.strip()]
elif a.text:
    lines = [a.text]
else:
    sys.exit("읽힐 문장 또는 --file 을 주세요.")

stamp = time.strftime("%Y%m%d_%H%M%S")
out = Path(a.out) / stamp
out.mkdir(parents=True, exist_ok=True)
tmp = Path(tempfile.mkdtemp())
(tmp / "texts.json").write_text(json.dumps(lines, ensure_ascii=False), encoding="utf-8")
env = dict(os.environ, PYTHONIOENCODING="utf-8", HF_HUB_DISABLE_SYMLINKS_WARNING="1")
models = ["omni", "v2", "v3"] if a.model == "all" else [a.model]
for m in models:
    ref = dict(BEST["models"][m]); ref["wav"] = str((ROOT / ref["wav"]).resolve())
    (tmp / "refs.json").write_text(json.dumps({"best": ref}, ensure_ascii=False), encoding="utf-8")
    if m == "omni":
        cmd = [str(PY_OMNI), str(ROOT / "work" / "gen_omni.py"), str(tmp / "refs.json"), str(tmp / "texts.json"), str(out)]
    else:
        cmd = [str(PY_COSY), str(ROOT / "work" / "gen_cosy.py"), m, str(tmp / "refs.json"), str(tmp / "texts.json"), str(out)]
    print(f"== {m} 생성 중 ({len(lines)}문장) ==", flush=True)
    subprocess.run(cmd, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
print(f"완료 → {out}")
if not os.environ.get("TTS_NO_OPEN"): os.startfile(out)
