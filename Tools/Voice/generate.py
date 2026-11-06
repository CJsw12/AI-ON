"""Speaks every line in <lines.txt> with a cloned voice (CosyVoice3) into Assets/Resources/Voice/<Voice>/.

Run with the CosyVoice environment's Python (see README.md):
  python generate.py <lines.txt> <out_dir> --clone C:\\Users\\PC12\\voice-clone [--force]

Each clip is named after a hash of its text, so unchanged lines keep their audio and only new or edited
lines are generated. lines.txt in out_dir ("file<TAB>text") is what the game reads; clips no longer
listed are deleted.
"""
import argparse, hashlib, json, os, re, sys, time, warnings
from pathlib import Path

warnings.filterwarnings("ignore")
os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")

# Read-aloud forms for what the model would otherwise spell out or skip. The game text is unchanged.
SAY = [
    (r"i\+\+", "아이 플러스 플러스"),
    (r"\bGit\b", "깃"),
    (r"\bCount\b", "카운트"),
    (r"\bOT\b", "오티"),
]


def spoken(text):
    for pattern, word in SAY:
        text = re.sub(pattern, word, text)
    text = text.replace("…", "... ").strip()
    text = re.sub(r"^(\.\.\.\s*)+", "", text)          # A leading pause is just silence; drop it.
    return re.sub(r"\s+", " ", text).strip()


def name(text):
    return hashlib.md5(text.encode("utf-8")).hexdigest()[:12]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("lines")
    p.add_argument("out")
    p.add_argument("--clone", default=r"C:\Users\PC12\voice-clone")
    p.add_argument("--model", default="v3", choices=["v2", "v3"])
    p.add_argument("--force", action="store_true", help="regenerate clips that already exist")
    p.add_argument("--seed", type=int, default=0, help="another seed often fixes a clip that skips or repeats words")
    a = p.parse_args()

    texts = [l.strip() for l in Path(a.lines).read_text(encoding="utf-8-sig").splitlines() if l.strip()]
    out = Path(a.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    clone = Path(a.clone).resolve()
    ref = json.load(open(clone / "voice" / "best.json", encoding="utf-8"))["models"][a.model]
    ref_wav = str((clone / ref["wav"]).resolve())

    todo = [t for t in texts if a.force or not (out / (name(t) + ".ogg")).exists()]
    print(f"{len(texts)} lines, {len(todo)} to generate", flush=True)
    if todo:
        cv = clone / "cosyvoice"
        sys.path.insert(0, str(cv))
        sys.path.insert(0, str(cv / "third_party" / "Matcha-TTS"))
        here = os.getcwd()
        os.chdir(cv)
        import torch, soundfile
        from cosyvoice.cli.cosyvoice import AutoModel
        folder = {"v2": "CosyVoice2-0.5B", "v3": "Fun-CosyVoice3-0.5B"}[a.model]
        model = AutoModel(model_dir=str(cv / "pretrained_models" / folder), fp16=True)
        prompt = ("You are a helpful assistant.<|endofprompt|>" if a.model == "v3" else "") + ref["text"]
        os.chdir(here)
        for i, text in enumerate(todo, 1):
            torch.manual_seed(a.seed)
            t0 = time.time()
            parts = [j["tts_speech"] for j in model.inference_zero_shot(spoken(text), prompt, ref_wav, stream=False)]
            audio = torch.cat(parts, 1).squeeze(0).float().cpu().numpy()
            soundfile.write(str(out / (name(text) + ".ogg")), audio, model.sample_rate, format="OGG", subtype="VORBIS")
            print(f"[{i}/{len(todo)}] {time.time() - t0:.1f}s  {text}", flush=True)

    keep = {name(t) + ".ogg" for t in texts}
    for clip in out.glob("*.ogg"):
        if clip.name not in keep:
            clip.unlink()
            meta = clip.with_name(clip.name + ".meta")
            if meta.exists():
                meta.unlink()
    (out / "lines.txt").write_text("".join(f"{name(t)}\t{t}\n" for t in texts), encoding="utf-8")
    print(f"done -> {out}")


if __name__ == "__main__":
    main()
