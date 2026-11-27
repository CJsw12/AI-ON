"""Local Korean STT. --worker uses one model for multiple newline-JSON requests."""
import json
import os
import sys
from faster_whisper import WhisperModel

sys.stdin.reconfigure(encoding="utf-8")
sys.stdout.reconfigure(encoding="utf-8")
model_name = (sys.argv[sys.argv.index("--model") + 1] if "--model" in sys.argv
              else os.environ.get("AION_STT_MODEL", "small"))
try:
    model = WhisperModel(model_name, device="cpu", compute_type="int8", cpu_threads=4,
                         local_files_only=True)
except (OSError, ValueError):
    model = WhisperModel(model_name, device="cpu", compute_type="int8", cpu_threads=4)


def transcribe(path, hint=""):
    segments, _ = model.transcribe(
        path, language="ko", beam_size=5, temperature=0,
        condition_on_previous_text=False, vad_filter=True,
        vad_parameters={"min_silence_duration_ms": 450, "speech_pad_ms": 250},
        initial_prompt="대학교 교수와 학생의 한국어 대화입니다. " + hint[:500],
    )
    return " ".join(s.text.strip() for s in segments if s.no_speech_prob < .75)


if "--worker" in sys.argv:
    print(json.dumps({"ready": True}), flush=True)
    for line in sys.stdin:
        try:
            request = json.loads(line)
            response = {"text": transcribe(request["path"], request.get("hint", ""))}
        except Exception as error:
            print(str(error), file=sys.stderr, flush=True)
            response = {"error": "음성 변환에 실패했습니다. 마이크와 모델 상태를 확인해 주세요."}
        print(json.dumps(response, ensure_ascii=False), flush=True)
else:
    print(transcribe(sys.argv[1]), end="")
