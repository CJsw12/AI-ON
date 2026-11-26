"""교수 대사를 Qwen3-TTS(Base, 기준 음성 복제)로 만든다. 출력 규칙은 generate.py(CosyVoice3)와 같다.
  <대사 MD5 앞 12자리>.ogg + lines.txt(파일<TAB>대사). 새 대사·바뀐 대사만 만들고, 목록에 없는 클립은 지운다.

사용 (Qwen3-TTS 환경의 python, docs/plans/TTS-Local-Setup.md 와 docs/plans/TTS-Voice-Production.md 8절):
  .\\Tools\\Voice\\export_lines.ps1 -Voice SeoJinHo                       # Tools\\Voice\\SeoJinHo.lines.txt
  python Tools/Voice/generate_qwen.py SeoJinHo                           # Assets/Resources/Voice/SeoJinHo 에 바뀐 것만
  python Tools/Voice/generate_qwen.py SeoJinHo --dry-run                 # 몇 개 만들지 세기만
  python Tools/Voice/generate_qwen.py SeoJinHo --line "문장 그대로"       # 대사 1개만 다시(같은 이름으로 덮어씀)
  python Tools/Voice/generate_qwen.py SeoJinHo --only 다시.txt --seed 2   # 목록의 대사만 시드를 바꿔 다시

- 목소리는 Tools/Voice/qwen/voices.json 의 기준 음성(refs/<Id>.flac)과 기준 문장으로 정해진다.
- 읽는 법은 Tools/Voice/qwen/spoken.tsv(화면 대사<TAB>읽기용 문장). 없는 대사는 … → 쉼 정리만 한다. 화면 글자는 바뀌지 않는다.
- --force 는 전부 다시 만든다. 같은 기준 음성이라 같은 목소리지만 억양은 조금 달라진다(생성은 확률적).
- 검사용 Tools/Voice/<Id>.asr_in.tsv 를 함께 쓴다(Tools/Voice/check_asr.py 의 입력). 커밋하지 않는다.
"""
import argparse, hashlib, json, re, time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]


def spoken(text):
    text = text.replace("…", "... ").strip()
    text = re.sub(r"^(\.\.\.\s*)+", "", text)  # 앞쪽 쉼표는 무음이라 뺀다.
    return re.sub(r"\s+", " ", text).strip()


def name(text):
    return hashlib.md5(text.encode("utf-8")).hexdigest()[:12]


def read_lines(path):
    return [l.strip() for l in Path(path).read_text(encoding="utf-8-sig").splitlines() if l.strip()]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("voice", help="Professor.Voice 값 (예: SeoJinHo)")
    p.add_argument("--lines", help="export_lines.ps1 출력 (기본 Tools/Voice/<Voice>.lines.txt)")
    p.add_argument("--out", help="출력 폴더 (기본 Assets/Resources/Voice/<Voice>)")
    p.add_argument("--config", default=str(HERE / "qwen" / "voices.json"))
    p.add_argument("--spoken", default=str(HERE / "qwen" / "spoken.tsv"))
    p.add_argument("--only", help="이 파일의 화면 대사만 다시 만든다(이미 있어도 덮어씀)")
    p.add_argument("--line", action="append", help="화면 대사 1개를 다시 만든다(여러 번 가능)")
    p.add_argument("--seed", type=int, help="기본은 voices.json 의 generation.seed")
    p.add_argument("--force", action="store_true", help="있는 클립도 전부 다시 만든다")
    p.add_argument("--batch", type=int, help="GPU 한 번에 만들 대사 수 (8GB VRAM 은 4~6)")
    p.add_argument("--device", help="기본: cuda:0 이 있으면 cuda:0, 없으면 cpu")
    p.add_argument("--dtype", choices=["bfloat16", "float16", "float32"], help="기본: GPU bfloat16, CPU float32")
    p.add_argument("--attn", help="attention 구현 (기본 voices.json 의 sdpa)")
    p.add_argument("--model", help="모델 ID 또는 로컬 폴더(기본 voices.json 의 clone 모델). 오프라인이면 캐시 스냅샷 폴더를 준다")
    p.add_argument("--dry-run", action="store_true", help="만들 개수만 세고 끝낸다")
    a = p.parse_args()

    cfg = json.loads(Path(a.config).read_text(encoding="utf-8"))
    entry = next((v for v in cfg["voices"] if v["id"] == a.voice), None)
    if entry is None or entry["engine"] != "qwen3-tts":
        raise SystemExit(f"{a.voice}: voices.json 에 Qwen3-TTS 목소리로 등록되어 있지 않다. "
                         f"(심한뫼는 generate.py, 이명희는 ElevenLabs 로 만들었다)")
    gen = cfg["generation"]
    lines_path = Path(a.lines) if a.lines else HERE / f"{a.voice}.lines.txt"
    out = Path(a.out).resolve() if a.out else REPO / "Assets" / "Resources" / "Voice" / a.voice
    texts = read_lines(lines_path)
    say = {}
    spoken_path = Path(a.spoken)
    if spoken_path.exists():
        for row in read_lines(spoken_path):
            shown, voiced = row.split("\t", 1)
            say[shown] = voiced

    redo = set(read_lines(a.only)) if a.only else set()
    redo |= set(a.line or [])
    unknown = [t for t in redo if t not in texts]
    if unknown:
        raise SystemExit(f"목록에 없는 대사: {unknown[:3]}  (export_lines.ps1 출력과 글자가 같아야 한다)")
    out.mkdir(parents=True, exist_ok=True)
    todo = [t for t in texts if (t in redo if redo else a.force or not (out / (name(t) + ".ogg")).exists())]
    print(f"{len(texts)} lines, {len(todo)} to generate", flush=True)
    if a.dry_run:
        return

    if todo:
        import torch
        import soundfile as sf
        from qwen_tts import Qwen3TTSModel

        device = a.device or ("cuda:0" if torch.cuda.is_available() else "cpu")
        dtype_name = a.dtype or (gen["dtype"] if device.startswith("cuda") else "float32")
        dtype = getattr(torch, dtype_name)
        batch = a.batch or gen["batch"]
        seed = gen["seed"] if a.seed is None else a.seed
        model = Qwen3TTSModel.from_pretrained(a.model or cfg["models"]["clone"], device_map=device, dtype=dtype,
                                              attn_implementation=a.attn or gen["attn_implementation"])
        prompt = model.create_voice_clone_prompt(ref_audio=str(HERE / "qwen" / entry["ref"]), ref_text=entry["ref_text"])
        done = 0
        for start in range(0, len(todo), batch):
            chunk = todo[start:start + batch]
            torch.manual_seed(seed + start)
            t0 = time.time()
            voiced = [spoken(say.get(t, t)) for t in chunk]
            wavs, sr = model.generate_voice_clone(text=voiced, language=[gen["language"]] * len(chunk),
                                                  voice_clone_prompt=prompt * len(chunk))
            for text, v, w in zip(chunk, voiced, wavs):
                sf.write(str(out / (name(text) + ".ogg")), w, sr, format="OGG", subtype="VORBIS")
                done += 1
                print(f"[{done}/{len(todo)}] {len(w) / sr:4.1f}s audio  {v}", flush=True)
            print(f"  batch of {len(chunk)}: {time.time() - t0:.1f}s", flush=True)

    keep = {name(t) + ".ogg" for t in texts}
    for clip in out.glob("*.ogg"):
        if clip.name not in keep:
            print(f"delete (not in lines): {clip.name}")
            clip.unlink()
            meta = clip.with_name(clip.name + ".meta")
            if meta.exists():
                meta.unlink()
    (out / "lines.txt").write_text("".join(f"{name(t)}\t{t}\n" for t in texts), encoding="utf-8")
    (HERE / f"{a.voice}.asr_in.tsv").write_text(
        "".join(f"{out / (name(t) + '.ogg')}\t{spoken(say.get(t, t))}\n" for t in texts), encoding="utf-8")
    print(f"done -> {out}")


if __name__ == "__main__":
    main()
