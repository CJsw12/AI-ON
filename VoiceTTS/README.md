# VoiceTTS — 내 목소리 TTS (로컬 GPU)

아이폰으로 녹음한 목소리(약 3분)로 OmniVoice, CosyVoice2, Fun-CosyVoice3 세 모델을 맞춰 둔 TTS 도구입니다.
결과와 판단 근거는 [`결과보고서.txt`](결과보고서.txt)에 있습니다.

## 결과 요약

| 순위 | 모델 | 참조 녹음 | 화자 유사도(ECAPA) | 발음 오류(CER) |
|---|---|---|---|---|
| 1 | Fun-CosyVoice3-0.5B | 발음 파트(pron3) | 0.850 | 0% |
| 2 | OmniVoice | 내레이션(narration) | 0.836 | 0% |
| 3 | CosyVoice2-0.5B | 발음 파트(pron3) | 0.834 | 0.9% |

샘플 음성은 `output/최종샘플/`에 있습니다.

## 폴더 구성

| 경로 | 내용 |
|---|---|
| `tts.py`, `speak.bat`, `speak_all.bat`, `script_to_voice.bat` | 실행기. `voice/best.json`에 저장된 모델별 최적 참조를 자동으로 사용 |
| `clone.py`, `cosy.py`, `compare.py`, `register_voice.bat`, `compare.bat`, `web_ui.bat` | 모델별 단독 실행, 새 녹음 등록, 비교 |
| `voice/` | 모델별 최적 참조 음성, `best.json`, OmniVoice 목소리 프롬프트(`me.pt`) |
| `rec/` | 원본 녹음(무손실 m4a), 받아쓰기 결과 |
| `work/` | 문장 클립, 참조 후보, 생성/채점 스크립트, 채점 결과(`scores_ecapa.json`) |
| `녹음대본_핵심.txt`, `녹음대본_확장.txt` | 녹음 대본 |
| `setup/` | CosyVoice를 Windows에 설치할 때 쓰는 의존성 목록 |

모델 파일과 파이썬 환경(약 15GB)은 저장소에 포함하지 않았습니다. 아래 순서로 다시 설치하세요.

## 설치 (Windows, NVIDIA GPU)

RTX 50 시리즈는 CUDA 12.8용 PyTorch가 필요합니다. 아래 명령은 PowerShell에서 `VoiceTTS` 폴더 안에서 실행하세요.
경로 길이 제한(260자)에 걸리지 않도록 짧은 경로(예: `C:\Users\<이름>\voice-clone`)에 두는 것을 권장합니다.

### 1. OmniVoice 환경 (Python 3.13)

```bash
python -m venv .venv
.venv\Scripts\python.exe -m pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu128
.venv\Scripts\python.exe -m pip install omnivoice imageio-ffmpeg soundfile speechbrain uv
.venv\Scripts\python.exe -c "import imageio_ffmpeg,shutil;shutil.copy(imageio_ffmpeg.get_ffmpeg_exe(),r'.venv\Scripts\ffmpeg.exe')"
```

### 2. CosyVoice 환경 (Python 3.10)

```bash
git clone --recursive --depth 1 https://github.com/FunAudioLLM/CosyVoice.git cosyvoice
$env:UV_PYTHON_INSTALL_DIR="$PWD\py"; .venv\Scripts\uv.exe python install 3.10
.venv\Scripts\uv.exe venv --python (Get-ChildItem py -Recurse -Filter python.exe | Select-Object -First 1).FullName cosyvoice\.venv
.venv\Scripts\uv.exe pip install --python cosyvoice\.venv\Scripts\python.exe torch==2.8.0 torchaudio==2.8.0 --index-url https://download.pytorch.org/whl/cu128
.venv\Scripts\uv.exe pip install --python cosyvoice\.venv\Scripts\python.exe -r setup\req-win.txt --override setup\override.txt --build-constraint setup\build-c.txt --extra-index-url https://download.pytorch.org/whl/cu128 --index-strategy unsafe-best-match
.venv\Scripts\uv.exe pip install --python cosyvoice\.venv\Scripts\python.exe onnxruntime==1.20.1 "setuptools<70"
```

### 3. CosyVoice 모델 받기 (약 13.6GB)

```bash
cd cosyvoice
.venv\Scripts\python.exe -c "from huggingface_hub import snapshot_download as s; s('FunAudioLLM/CosyVoice2-0.5B', local_dir='pretrained_models/CosyVoice2-0.5B'); s('FunAudioLLM/Fun-CosyVoice3-0.5B-2512', local_dir='pretrained_models/Fun-CosyVoice3-0.5B')"
```

OmniVoice와 Whisper 모델은 처음 실행할 때 자동으로 내려받습니다.

## 사용법

```bash
.venv\Scripts\python.exe tts.py "읽힐 문장"
```

- `--model v3|omni|v2|all`: 모델 선택. 기본값은 1위인 CosyVoice3입니다.
- `--file 대본.txt`: 대본 파일을 한 줄씩 읽어서 줄마다 음성 파일을 만듭니다.
- 결과는 `output/<날짜_시간>/`에 저장됩니다.

본인 목소리이거나 동의를 받은 목소리만 사용하세요.
