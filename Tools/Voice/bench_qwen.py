"""Qwen3-TTS 생성 속도 측정 (GPU 대 CPU). 같은 대사 N개, 워밍업 1회 뒤 클립마다 시간을 잰다.

  python Tools/Voice/bench_qwen.py --voice SeoJinHo --device cuda:0 --dtype bfloat16 --batch 1
  python Tools/Voice/bench_qwen.py --voice SeoJinHo --device cuda:0 --dtype bfloat16 --batch 6
  python Tools/Voice/bench_qwen.py --voice SeoJinHo --device cpu --dtype float32 --batch 1

출력: 한 줄 JSON(--json 파일로도 저장). 항목
  load_s        모델 불러오는 시간(첫 실행은 디스크 캐시 상태에 따라 다름)
  gen_s         대사 N개를 만드는 데 걸린 시간(워밍업 제외)
  audio_s       만들어진 음성 길이 합
  rtf           gen_s / audio_s  (1보다 작으면 실시간보다 빠름)
  s_per_clip    클립당 평균 생성 시간
  peak_vram_mb  torch.cuda.max_memory_allocated (CPU 는 null)
  peak_rss_mb   프로세스 최대 메모리(psutil 가 있을 때 0.1초 간격 표본)
필요: qwen-tts, torch, soundfile, psutil(선택). 모델은 로컬 캐시에서 읽는다(HF_HUB_OFFLINE=1 로 오프라인 동작 확인 가능).
"""
import argparse, json, threading, time
from pathlib import Path

HERE = Path(__file__).resolve().parent


def spoken(text):
    import re
    text = text.replace("…", "... ").strip()
    text = re.sub(r"^(\.\.\.\s*)+", "", text)
    return re.sub(r"\s+", " ", text).strip()


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--voice", default="SeoJinHo")
    p.add_argument("--device", default="cuda:0")
    p.add_argument("--dtype", default="bfloat16")
    p.add_argument("--attn", default="sdpa")
    p.add_argument("--batch", type=int, default=1)
    p.add_argument("--n", type=int, default=10)
    p.add_argument("--model", help="모델 ID 또는 로컬 폴더(기본 voices.json 의 clone 모델)")
    p.add_argument("--json")
    a = p.parse_args()

    import torch
    from qwen_tts import Qwen3TTSModel
    try:
        import psutil
        proc = psutil.Process()
    except ImportError:
        psutil = proc = None

    cfg = json.loads((HERE / "qwen" / "voices.json").read_text(encoding="utf-8"))
    e = next(v for v in cfg["voices"] if v["id"] == a.voice)
    say = {}
    for row in (HERE / "qwen" / "spoken.tsv").read_text(encoding="utf-8").splitlines():
        if "\t" in row:
            k, v = row.split("\t", 1)
            say[k] = v
    lines = [l.strip() for l in (HERE / f"{a.voice}.lines.txt").read_text(encoding="utf-8-sig").splitlines() if l.strip()]
    texts = [spoken(say.get(t, t)) for t in lines[: a.n + 1]]   # 첫 줄은 워밍업, 나머지 N개를 잰다.
    warm, texts = texts[0], texts[1:]

    peak = {"rss": 0}
    stop = threading.Event()

    def sample():
        while not stop.is_set():
            peak["rss"] = max(peak["rss"], proc.memory_info().rss)
            time.sleep(0.1)

    if proc:
        threading.Thread(target=sample, daemon=True).start()
    if a.device.startswith("cuda"):
        torch.cuda.reset_peak_memory_stats()

    t0 = time.time()
    model = Qwen3TTSModel.from_pretrained(a.model or cfg["models"]["clone"], device_map=a.device, dtype=getattr(torch, a.dtype),
                                          attn_implementation=a.attn)
    prompt = model.create_voice_clone_prompt(ref_audio=str(HERE / "qwen" / e["ref"]), ref_text=e["ref_text"])
    load_s = time.time() - t0

    def run(chunk):
        torch.manual_seed(0)
        wavs, sr = model.generate_voice_clone(text=chunk, language=["Korean"] * len(chunk), voice_clone_prompt=prompt * len(chunk))
        if a.device.startswith("cuda"):
            torch.cuda.synchronize()
        return sum(len(w) / sr for w in wavs)

    run([warm] * 1)   # 워밍업(커널 준비·캐시)
    t1 = time.time()
    audio = 0.0
    for s in range(0, len(texts), a.batch):
        audio += run(texts[s:s + a.batch])
    gen_s = time.time() - t1
    stop.set()
    res = {
        "device": a.device, "dtype": a.dtype, "attn": a.attn, "batch": a.batch, "clips": len(texts),
        "load_s": round(load_s, 1), "gen_s": round(gen_s, 1), "audio_s": round(audio, 1),
        "rtf": round(gen_s / audio, 2), "s_per_clip": round(gen_s / len(texts), 1),
        "peak_vram_mb": round(torch.cuda.max_memory_allocated() / 1048576) if a.device.startswith("cuda") else None,
        "peak_rss_mb": round(peak["rss"] / 1048576) if proc else None,
        "torch": torch.__version__,
    }
    print(json.dumps(res, ensure_ascii=False))
    if a.json:
        Path(a.json).write_text(json.dumps(res, ensure_ascii=False, indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
