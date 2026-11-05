"""녹음을 문장 클립으로 자르고, 참조(ref) 후보를 만든다."""
import json, subprocess
from pathlib import Path
import numpy as np, soundfile as sf

ROOT = Path(__file__).resolve().parent.parent
REC = ROOT / "rec" / "wav"
W = ROOT / "work"
FF = ROOT / ".venv" / "Scripts" / "ffmpeg.exe"
tr = json.load(open(REC / "transcripts.json", encoding="utf-8"))

def load(name):
    y, sr = sf.read(REC / name, dtype="float32")
    return y, sr

def refine(y, sr, t, win=0.4):
    """t 근처(±win초)에서 에너지가 가장 낮은 지점으로 경계 보정"""
    hop = int(sr * 0.01); a = max(0, int((t - win) * sr)); b = min(len(y), int((t + win) * sr))
    seg = y[a:b]
    if len(seg) < hop * 3: return t
    frames = np.lib.stride_tricks.sliding_window_view(seg, hop * 2)[::hop]
    e = (frames ** 2).mean(1)
    return (a + int(np.argmin(e)) * hop + hop) / sr

def clean(src_wav, dst, start, end):
    af = ("highpass=f=70,afftdn=nf=-25,"
          "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1,areverse,"
          "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1,areverse,"
          "loudnorm=I=-18:TP=-1.5:LRA=11")
    subprocess.run([str(FF), "-y", "-hide_banner", "-loglevel", "error", "-ss", f"{start:.3f}", "-to", f"{end:.3f}",
                    "-i", str(src_wav), "-ac", "1", "-ar", "24000", "-af", af, str(dst)], check=True)

# 1) 문장 클립
(W / "clips").mkdir(parents=True, exist_ok=True)
bounds = {}
clips = []
for name, chunks in tr.items():
    y, sr = load(name)
    dur = len(y) / sr
    b = [0.0] + [refine(y, sr, c["t"][1]) for c in chunks[:-1]] + [dur]
    bounds[name] = b
    for i, c in enumerate(chunks):
        out = W / "clips" / f"{Path(name).stem}_{i+1:02d}.wav"
        clean(REC / name, out, b[i], b[i + 1])
        clips.append({"file": str(out), "text": c["text"]})
json.dump(clips, open(W / "clips.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)

# 2) 참조 후보 (실제 말한 대로, 문장부호 보정)
REFS = {
 "daily_A": ("rec_13.wav", 0, None,
   "안녕하세요, 오늘은 제 목소리를 천천히 들려드릴게요. 창밖에는 꽃잎이 흩날리고, 따뜻한 햇살이 의자 위로 쏟아지네요. "
   "혹시 요즘 읽고 계신 책이 있으신가요? 저는 얼마 전까지 끝까지 읽은 소설이 아직도 맘에 남아 있어요. "
   "왠지 쉬운 결말은 아니었지만, 정말 괜찮았거든요!"),
 "casual": ("rec_14.wav", 0, None,
   "주말에 시간 되면 같이 영화 보러 갈래? 그 카페 케이크 진짜 맛있던데, 다음에 또 가자. "
   "냉장고에 우유 다 떨어졌으니까 오는 길에 하나만 사다 줄래?"),
 "pron3": ("rec_15.wav", 0, 3,
   "깜짝 놀라서 뒤를 돌아보니, 꼬마가 씩 웃고 있었어요. 칠판 앞에서 키 큰 친구가 퀴즈 정답을 큰 소리로 외쳤어요. "
   "닭볶음탕을 끓이려면 감자를 먼저 넣어야 돼요."),
 "emotion3": ("rec_17.wav", 0, 3,
   "정말요? 와, 진짜 잘됐다! 축하해요! 어, 깜짝이야! 언제 거기 와 있었어요? 조금만 일찍 왔으면 볼 수 있었을 텐데."),
 "narration": ("rec_19.wav", 0, None,
   "지금부터 간단하게 설명해 드리겠습니다. 첫째, 전원 버튼을 3초 동안 길게 눌러 주세요. 둘째, 화면에 파란색 불빛이 "
   "깜빡이면 연결이 시작됩니다. 만약 연결이 되지 않는다면, 기기를 껐다가 다시 켜 보시기 바랍니다."),
 "long_breath": ("rec_20.wav", 0, None,
   "비가 그치고 나면, 우리는 강가를 따라 천천히 걸으면서 이런저런 이야기를 나누곤 했어요."),
}
(W / "refs").mkdir(exist_ok=True)
refs = {}
for key, (name, si, ei, text) in REFS.items():
    b = bounds[name]
    s, e = b[si], (b[ei] if ei is not None else b[-1])
    out = W / "refs" / f"{key}.wav"
    clean(REC / name, out, s, e)
    refs[key] = {"wav": str(out), "text": text, "sec": round(sf.info(str(out)).duration, 2)}
    print(f"{key:12s} {refs[key]['sec']:5.1f}s")
json.dump(refs, open(W / "refs.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"clips: {len(clips)}")
