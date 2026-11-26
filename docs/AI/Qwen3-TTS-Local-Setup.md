# Qwen3-TTS 로컬 AI 성우 설치 안내

작성일: 2026-11-26

## 목적과 확인 범위

녹음 샘플 없이 가상 교수의 목소리를 설계하고, 생성한 샘플을 기준으로 새 대본을 WAV로 합성한다.
로컬 추론은 Hugging Face ZeroGPU 할당량과 ElevenLabs 생성 크레딧을 소비하지 않는다.
최초 패키지·모델 다운로드에는 인터넷이 필요하고 저장 공간·메모리·전력은 PC에서 사용한다.

현재 PC에서 확인한 그래픽 장치는 Intel(R) Arc(TM) B370 GPU다.
아래 설치와 실제 로컬 합성은 아직 실행 검증하지 않았다.
특히 Qwen3-TTS의 Intel XPU 호환성·속도·메모리 용량은 미검증이다.
공식 예제의 NVIDIA CUDA 및 FlashAttention 설정을 Intel GPU에 그대로 사용하면 안 된다.
드라이버 및 PyTorch의 해당 장치 지원 여부를 먼저 확인한다.

## 1. 독립된 Python 환경 만들기

Unity 프로젝트 밖에서 Windows PowerShell로 실행한다. 이 PC에는 uv가 있다.
다른 PC에서 uv가 없다면 https://docs.astral.sh/uv/getting-started/installation/ 을 참고한다.

~~~powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE/Qwen3-TTS-Local"
Set-Location "$env:USERPROFILE/Qwen3-TTS-Local"
uv venv --python 3.12 .venv
uv pip install --python .venv/Scripts/python.exe qwen-tts soundfile
~~~

가상환경 활성화 없이 전용 python.exe를 사용하므로 PowerShell 실행 정책을 바꿀 필요가 없다.
모델 캐시는 기본적으로 사용자 Hugging Face 캐시 아래에 저장된다. 모델을 Git에 올리지 않는다.

## 2. 실행 장치 선택

### Intel GPU: 실험 경로

먼저 Intel 공식 드라이버와 PyTorch Intel GPU 지원 문서를 확인한다.
아래는 XPU 패키지 설치와 감지 명령이다. 모델 전체가 실행된다는 보장은 아니다.

~~~powershell
uv pip install --python .venv/Scripts/python.exe --upgrade torch torchaudio --index-url https://download.pytorch.org/whl/xpu
.venv/Scripts/python.exe -c "import torch; print(torch.__version__); print('XPU:', torch.xpu.is_available()); print(torch.xpu.get_device_name(0) if torch.xpu.is_available() else 'XPU unavailable')"
uv pip check --python .venv/Scripts/python.exe
~~~

XPU가 False이면 GPU 경로를 진행하지 말고 드라이버·지원 장치·패키지 버전을 확인한다.
True여도 Qwen3-TTS 내부 연산이 지원되지 않을 수 있다. 실행 오류가 나면 CPU 경로로 확인한다.
Intel 환경에는 NVIDIA용 flash-attn을 설치하지 않는다.

### CPU: GPU를 사용할 수 없을 때

CPU 전용 환경은 별도 폴더에 만들거나 위 환경에 CPU 패키지를 설치한다.
아래 명령은 현재 가상환경의 PyTorch를 CPU 버전으로 교체한다.

~~~powershell
uv pip install --python .venv/Scripts/python.exe --upgrade torch torchaudio --index-url https://download.pytorch.org/whl/cpu
uv pip check --python .venv/Scripts/python.exe
~~~

CPU는 생성이 오래 걸릴 수 있다. 실행 시간과 메모리 사용량은 이 PC에서 미측정이다.

## 3. 박태준 교수의 70대 남성 목소리 만들기

아래 코드를 UTF-8로 generate_park.py에 저장한다.
DEVICE는 Intel 감지가 성공하면 "xpu:0", CPU는 "cpu"로 지정한다.
이는 공식 VoiceDesign 예제에서 장치·정밀도·attention을 조정한 실험용 예제다.

~~~python
import torch
import soundfile as sf
from qwen_tts import Qwen3TTSModel

DEVICE = "cpu"  # Intel GPU 실험 시 "xpu:0"
if DEVICE.startswith("xpu") and not torch.xpu.is_available():
    raise RuntimeError("Intel XPU를 사용할 수 없습니다.")

model = Qwen3TTSModel.from_pretrained(
    "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign",
    device_map=DEVICE,
    dtype=torch.float32,
    attn_implementation="eager",
)
text = (
    "어, 거기. 불빛 이쪽 분전함에 비춰. 손은 대지 말고. "
    "멀티미터 받아. 뜯기 전에 재. 감으로 뜯으면 멀쩡한 놈만 죽는다. "
    "못 찾았네. 괜찮아, 나도 처음엔 다 태워 먹었어. 다시 재 봐."
)
style = (
    "A native Korean man aged 75, a veteran engineering professor. "
    "Naturally elderly, low, weathered, slightly breathy voice. "
    "Deliberate pauses, clear Korean diction, short blunt informal phrases. "
    "Firm safety warning, practical instruction, understated warmth at the end."
)
wavs, sr = model.generate_voice_design(
    text=text, language="Korean", instruct=style,
)
sf.write("ParkTaejun_70s_reference.wav", wavs[0], sr)
with open("ParkTaejun_70s_reference.txt", "w", encoding="utf-8") as f:
    f.write(text)
print("저장 완료: ParkTaejun_70s_reference.wav")
~~~

~~~powershell
.venv/Scripts/python.exe generate_park.py
~~~

처음 실행하면 모델을 내려받는다. 다운로드 완료 전에는 오프라인으로 실행할 수 없다.
70대라는 지시는 음색을 유도하지만 결과를 보장하지 않는다. 생성 후 직접 들어 보고
발음·연령감·문장 누락을 확인한다. 단순히 피치만 낮춘 파일과는 생성 방식이 다르다.
float32는 메모리를 많이 쓴다. 부족하면 다른 프로그램을 종료하거나 더 큰 메모리의 PC에서 실행한다.
정밀도 변경은 장치 지원 여부를 확인한 뒤 시도하며, 성공한 설정을 기록한다.

## 4. 같은 목소리로 새 대본 합성하기

마음에 드는 reference WAV와 정확한 대사 원문을 보관한다.
VoiceDesign을 매번 다시 실행하면 음색이 달라질 수 있으므로 이후 대사는 Base 모델로 합성한다.
별도의 가중치 학습 없이 참조 음성을 조건으로 사용하는 방식이다.
아래를 generate_script.py로 저장한다. DEVICE는 앞 단계와 동일하게 지정한다.

~~~python
import torch
import soundfile as sf
from qwen_tts import Qwen3TTSModel

DEVICE = "cpu"
model = Qwen3TTSModel.from_pretrained(
    "Qwen/Qwen3-TTS-12Hz-0.6B-Base",
    device_map=DEVICE,
    dtype=torch.float32,
    attn_implementation="eager",
)
with open("ParkTaejun_70s_reference.txt", encoding="utf-8") as f:
    reference_text = f.read()
with open("script.txt", encoding="utf-8") as f:
    target_text = f.read().strip()
if not target_text:
    raise ValueError("script.txt에 읽을 대본을 작성하세요.")
wavs, sr = model.generate_voice_clone(
    text=target_text,
    language="Korean",
    ref_audio="ParkTaejun_70s_reference.wav",
    ref_text=reference_text,
)
sf.write("ParkTaejun_script.wav", wavs[0], sr)
~~~

UTF-8 script.txt에 새 대본을 작성한 뒤 실행한다.

~~~powershell
.venv/Scripts/python.exe generate_script.py
~~~

긴 대본은 한 대사씩 나누면 재생·수정·발음 확인이 쉽다.
동일 참조를 써도 감정과 운율은 달라질 수 있다. 실제 교수 목소리를 재현할 때는
본인 동의를 받은 녹음을 사용한다.

## 5. Unity 적용과 재현 기록

완성 WAV를 먼저 들어 보고 대사 누락·잡음·발음을 확인한다.
AI-ON의 Tools/Voice/README.md와 기존 음성 매핑 규칙에 맞춰 별도로 연결한다.
이 문서 작성만으로 게임 음성이 자동 교체되지는 않는다.

성공한 환경은 다음 명령으로 기록한다.

~~~powershell
uv pip freeze --python .venv/Scripts/python.exe > requirements-local.txt
.venv/Scripts/python.exe -c "import torch; print(torch.__version__)"
~~~

함께 기록할 항목: Windows·드라이버 버전, GPU 이름, 모델 ID와 리비전,
device/dtype/attention 설정, 대본, 참조 음성, 생성 시간, 청취 결과.
모델·캐시·가상환경·개인 녹음·계정 토큰은 저장소에 커밋하지 않는다.

## 오류 해석

- ZeroGPU quota: 공개 데모를 호출한 것이다. 위 로컬 Python 코드에는 데모 호출이 없다.
- CUDA 관련 오류: Intel 환경에서 CUDA 설정을 사용하지 않았는지 확인한다.
- XPU 미지원 연산: 현재 조합의 GPU 실행 실패다. CPU에서 확인하고 오류 전문을 남긴다.
- flash_attn 오류: 위 예제의 eager 설정을 사용하고 CUDA 전용 최적화를 제거한다.
- 패키지 충돌: uv pip check 결과를 확인한다. 설치 성공과 합성 성공을 구분한다.
- 다운로드 실패: 연결과 저장 공간을 확인한다. 공개 모델은 일반적으로 로그인 없이
  다운로드할 수 있지만 접근 제한이 있으면 본인 계정의 접근 상태를 확인한다.

## 공식 참고 자료

- Qwen3-TTS 설치·VoiceDesign·Voice Clone:
  https://github.com/QwenLM/Qwen3-TTS
- PyTorch Intel GPU 설치·지원 범위:
  https://docs.pytorch.org/docs/stable/notes/get_start_xpu.html
- uv 설치:
  https://docs.astral.sh/uv/getting-started/installation/

문서 검증 범위: 공식 자료와 예제 API 대조, Python 예제 구문 검사, 저장소 회귀 검사.
실제 패키지 설치·모델 다운로드·Intel/CPU 추론·음성 품질은 미검증이다.