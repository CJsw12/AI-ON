"""Qwen3-TTS VoiceDesign 으로 교수 기준 음성을 새로 설계한다(교수당 1개). 새 교수를 추가하거나 목소리를 바꿀 때만 쓴다.

기존 교수의 목소리는 이미 Tools/Voice/qwen/refs/<Id>.flac 에 고정되어 있다. 이 스크립트를 다시 돌리면
같은 설명·시드여도 장치와 버전에 따라 다른 목소리가 나올 수 있으므로, 기존 파일을 덮어쓰지 않고 --out 폴더에 만든다.

  python Tools/Voice/design_qwen.py SeoJinHo --out Tools/Voice/qwen/refs_new
  (새 교수는 voices.json 에 id, style, ref_text, design_seed 를 먼저 적는다. 결과를 듣고 마음에 들면 refs/ 로 옮겨 커밋한다.)
"""
import argparse, json
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("voices", nargs="+")
    p.add_argument("--config", default=str(HERE / "qwen" / "voices.json"))
    p.add_argument("--out", default=str(HERE / "qwen" / "refs_new"))
    a = p.parse_args()

    import torch
    import soundfile as sf
    from qwen_tts import Qwen3TTSModel

    cfg = json.loads(Path(a.config).read_text(encoding="utf-8"))
    device = "cuda:0" if torch.cuda.is_available() else "cpu"
    model = Qwen3TTSModel.from_pretrained(cfg["models"]["design"], device_map=device,
                                          dtype=torch.bfloat16 if device.startswith("cuda") else torch.float32,
                                          attn_implementation=cfg["generation"]["attn_implementation"])
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    for vid in a.voices:
        e = next(v for v in cfg["voices"] if v["id"] == vid and v["engine"] == "qwen3-tts")
        torch.manual_seed(e["design_seed"])
        wavs, sr = model.generate_voice_design(text=e["ref_text"], language=cfg["generation"]["language"], instruct=e["style"])
        sf.write(str(out / f"{vid}.flac"), wavs[0], sr, format="FLAC")
        print(f"{vid}: {len(wavs[0]) / sr:.1f}s -> {out / (vid + '.flac')}", flush=True)


if __name__ == "__main__":
    main()
