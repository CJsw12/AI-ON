"""Assets/Resources/Voice 의 교수 음성 통계 (클립 수, 총 재생 시간, 용량, 평균·최장 길이).

표준 라이브러리만 쓴다. Ogg Vorbis 길이 = 마지막 페이지의 granule position / 샘플레이트.
  python Tools/Voice/voice_stats.py [Assets/Resources/Voice]        # 교수별 표 + 합계
  python Tools/Voice/voice_stats.py --json                          # 같은 내용을 JSON으로
"""
import json
import struct
import sys
from pathlib import Path


def ogg_seconds(path):
    data = path.read_bytes()
    # 첫 페이지의 Vorbis identification header: 'vorbis' 앞 1바이트(0x01) 뒤에 version(4), channels(1), rate(4).
    i = data.find(b"\x01vorbis")
    if i < 0:
        raise ValueError(f"Vorbis 헤더 없음: {path}")
    rate = struct.unpack_from("<I", data, i + 7 + 4 + 1)[0]
    j = data.rfind(b"OggS")
    granule = struct.unpack_from("<q", data, j + 6)[0]
    return granule / rate


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    root = Path(args[0]) if args else Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Voice"
    rows = []
    for d in sorted(p for p in root.iterdir() if p.is_dir()):
        clips = sorted(d.glob("*.ogg"))
        if not clips:
            continue
        secs = [ogg_seconds(c) for c in clips]
        rows.append({
            "voice": d.name, "clips": len(clips), "seconds": round(sum(secs), 1),
            "mb": round(sum(c.stat().st_size for c in clips) / 1048576, 2),
            "mean_s": round(sum(secs) / len(secs), 2), "max_s": round(max(secs), 2),
        })
    total = {
        "voices": len(rows), "clips": sum(r["clips"] for r in rows), "seconds": round(sum(r["seconds"] for r in rows), 1),
        "mb": round(sum(r["mb"] for r in rows), 1),
        "mean_s": round(sum(r["seconds"] for r in rows) / max(1, sum(r["clips"] for r in rows)), 2),
        "max_s": max(r["max_s"] for r in rows),
    }
    if "--json" in sys.argv:
        print(json.dumps({"voices": rows, "total": total}, ensure_ascii=False, indent=1))
        return
    print(f"{'voice':<16}{'clips':>6}{'minutes':>9}{'MB':>8}{'mean s':>8}{'max s':>8}")
    for r in rows:
        print(f"{r['voice']:<16}{r['clips']:>6}{r['seconds']/60:>9.1f}{r['mb']:>8.2f}{r['mean_s']:>8.2f}{r['max_s']:>8.2f}")
    print(f"{'TOTAL':<16}{total['clips']:>6}{total['seconds']/60:>9.1f}{total['mb']:>8.1f}{total['mean_s']:>8.2f}{total['max_s']:>8.2f}")


if __name__ == "__main__":
    main()
