"""교수 기준 음성끼리 얼마나 비슷한지(화자 임베딩 코사인 유사도)를 잰다. 새 목소리가 기존 목소리와 겹치는지 볼 때 쓴다.

Qwen3-TTS Base 모델이 복제할 때 쓰는 화자 임베딩(ref_spk_embedding)을 쓴다. 원시 값은 모두 0.89 이상으로 몰려 있어서
전체 평균 임베딩을 뺀 뒤 코사인을 잰다(평균 제거). 값이 클수록 비슷하다. 같은 성별·연령대끼리는 높게 나오는 게 자연스럽다.
  python Tools/Voice/voice_similarity.py [--top 10] [--model <로컬 스냅샷 폴더>]
"""
import argparse
import json
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--top", type=int, default=10)
    p.add_argument("--model")
    a = p.parse_args()
    import torch
    from qwen_tts import Qwen3TTSModel

    cfg = json.loads((HERE / "qwen" / "voices.json").read_text(encoding="utf-8"))
    dev = "cuda:0" if torch.cuda.is_available() else "cpu"
    m = Qwen3TTSModel.from_pretrained(a.model or cfg["models"]["clone"], device_map=dev,
                                      dtype=torch.bfloat16 if dev.startswith("cuda") else torch.float32, attn_implementation="sdpa")
    ids, emb = [], []
    for v in cfg["voices"]:
        if v["engine"] != "qwen3-tts":
            continue
        pr = m.create_voice_clone_prompt(ref_audio=str(HERE / "qwen" / v["ref"]), ref_text=v["ref_text"])
        pr = pr[0] if isinstance(pr, (list, tuple)) else pr
        e = getattr(pr, "ref_spk_embedding", None)
        if e is None:
            e = pr["ref_spk_embedding"]
        ids.append(v["id"])
        emb.append(e.float().flatten().cpu().numpy())
    R = np.stack(emb)
    R = R - R.mean(0)
    X = R / np.linalg.norm(R, axis=1, keepdims=True)
    S = X @ X.T
    pairs = sorted(((S[i, j], ids[i], ids[j]) for i in range(len(ids)) for j in range(i)), reverse=True)
    vals = np.array([p[0] for p in pairs])
    print(f"voices {len(ids)}  pairs {len(pairs)}  median {np.median(vals):.3f}  max {vals.max():.3f}  "
          f"pairs>0.7: {(vals > 0.7).sum()}  pairs>0.6: {(vals > 0.6).sum()}")
    for s, x, y in pairs[: a.top]:
        print(f"{s:.3f}  {x} - {y}")


if __name__ == "__main__":
    main()
