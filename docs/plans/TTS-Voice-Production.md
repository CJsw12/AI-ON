# 교수 음성(TTS) 제작 기술 문서

작성 2026-11-27 · 작성 sh-com-claude · 요청서 `docs/plans/TTS-Documentation-Request.md`

게임의 교수 32명이 말하는 대사 2,282개를 어떤 기술로 어떻게 만들었는지 정리한다. 발표·보고서용 요약(0절)과 팀원이 같은 목소리로 새 대사를 만드는 방법(8절)을 포함한다.

**읽는 법.** 모든 수치는 2026-11-27에 다시 계산한 값이고 7절에 계산 명령이 있다. 확인하지 못한 값은 "미측정" 또는 "미확인"으로 적었다. 설치 절차는 [TTS-Local-Setup.md](TTS-Local-Setup.md)를 본다(이 문서에서 다시 쓰지 않는다).

---

## 0. 한눈에 보기 (발표 슬라이드 1장)

| 항목 | 값 |
|---|---|
| 교수 / 학과 | 32명 / 16개 학과(학과당 2명) |
| 대사 / 클립 | 2,282줄 = `.ogg` 2,282개, **전부 게임 대사와 연결**(`Tests/VoiceCoverage.Tests.ps1`: PASS, 2282/2282) |
| 총 재생 시간 | **233.4분(약 3시간 53분)**, 평균 6.14초, 최장 12.48초 |
| 용량 | `.ogg` 89.8MiB, 폴더 전체(`.meta` 포함) 약 99MB |
| 음성 만든 방법 | **Qwen3-TTS 30명**(녹음 없이 설계한 목소리) · **CosyVoice3 1명**(심한뫼, 사용자 본인 녹음 복제) · **ElevenLabs 1명**(이명희, 2026-11-06 외부 서비스) |
| 장비 | NVIDIA GeForce RTX 5060 Ti(VRAM 8GB), AMD Ryzen 5 5600, RAM 15.9GB, Windows 11 |
| 속도(실측) | Qwen3-TTS 클립당 **7.5초(배치 6)**, 실시간 배율 1.35. CPU는 3절 표 |
| 총 생성 시간 | 약 6.0시간(Qwen 29명 로그 합계 5.44시간 + 정도윤 약 22분 + 목소리 설계 약 11분) |
| 품질(자동 검사) | Whisper 받아쓰기 기준 클립의 **88.3%가 글자 오류율 0.10 이하**, 중앙값 0.000. 사람이 듣고 검수한 수는 **미확인**(9절) |
| 비용 | 로컬 생성이라 API 비용 0원. 전기·장비만 사용. 이명희 80줄만 ElevenLabs **무료 플랜**(비상업·출처 표기 조건, 9절) |
| 인터넷 | 패키지·모델 내려받을 때 필요. 기본 설정은 실행할 때마다 Hugging Face에 접속한다. **모델을 로컬 폴더로 지정하면 오프라인으로 생성된다**(직접 확인, 8절) |

---

## 1. 전체 흐름

```
스토리 코드(대사)
  └ export_lines.ps1 ─→ Tools/Voice/<Id>.lines.txt (교수가 하는 말, 중복 제거)
        └ generate_qwen.py (Qwen3-TTS) 또는 generate.py (CosyVoice3)
              └ Assets/Resources/Voice/<Id>/<MD5 앞 12자리>.ogg + lines.txt(파일<TAB>대사)
                    └ Unity: ProfessorVoice 가 화면 대사와 lines.txt 를 맞춰 클립을 찾고 NovelAudio 가 재생
                          └ Tests/VoiceCoverage.Tests.ps1 로 전수 검사(대사 2,282개 모두 녹음이 있는가)
```

| 단계 | 입력 | 출력 | 실행 |
|---|---|---|---|
| ① 대사 추출 | 스토리 코드 | `Tools/Voice/<Id>.lines.txt` | `.\Tools\Voice\export_lines.ps1 -Voice <Id>` |
| ② 읽기용 문장 | 화면 대사 | `Tools/Voice/qwen/spoken.tsv`(화면 대사 → 읽기용, 375행) | 손질한 파일을 커밋 |
| ③ 목소리 설계(새 교수만) | `voices.json`의 설명·시드 | 기준 음성 `qwen/refs/<Id>.flac` | `python Tools/Voice/design_qwen.py <Id>` |
| ④ 생성 | ①, ②, 기준 음성 | `Assets/Resources/Voice/<Id>/*.ogg`, `lines.txt` | `python Tools/Voice/generate_qwen.py <Id>` |
| ⑤ 품질 검사 | 생성된 `.ogg` | 글자 오류율(CER) JSON | `python Tools/Voice/check_asr.py Tools/Voice/<Id>.asr_in.tsv 결과.json` |
| ⑥ 재생성 | CER이 높은 줄 | 같은 이름으로 덮어씀 | `generate_qwen.py <Id> --only 다시.txt --seed 1` |
| ⑦ 연결 검사 | 저장소 전체 | PASS/FAIL | `powershell -File Tests/VoiceCoverage.Tests.ps1` |

심한뫼는 ④ 대신 `Tools/Voice/generate.py --model v3`(CosyVoice3)를 쓴다(AGENTS.md 교수 목소리 절).

---

## 2. 모델과 선택 이유

### 2.1 쓴 모델

| | Qwen3-TTS | CosyVoice3 |
|---|---|---|
| 쉬운 설명 | 목소리를 **글로 설명하면 새 목소리를 만들어 주는** 음성합성 AI. 만든 목소리를 기준 삼아 다른 대사도 그 목소리로 읽는다 | **짧은 녹음 하나만 있으면 그 사람 목소리로** 새 문장을 읽는 음성합성 AI |
| 기술 설명 | Alibaba Qwen 팀 모델. 음성을 이산 토큰으로 압축하는 자체 토크나이저(Qwen3-TTS-Tokenizer-12Hz)와 다중 코드북 언어 모델 구조. 별도 학습 없이 참조 음성(제로샷)으로 복제한다. 모델 카드: 한국어를 포함한 10개 언어 지원 | FunAudioLLM의 0.5B(5억 매개변수) 규모 TTS. 참조 음성과 그 대본을 주면 같은 목소리로 읽는 제로샷 복제(`inference_zero_shot`) |
| 규모 | VoiceDesign 1.7B, Base 1.7B(이 프로젝트에서 사용), Base 0.6B(미사용). 로컬 캐시 용량 각 4.3GB / 4.3GB / 2.4GB | Fun-CosyVoice3-0.5B(`fp16=True`로 실행) |
| 쓴 기능 | **VoiceDesign**(설명 → 목소리)으로 기준 음성 1개 설계, **Base**(참조 복제)로 모든 대사 생성 | 사용자 녹음 참조(`best.json`의 `v3`, 참조 `pron3`) 복제 |
| 대상 | 정도윤 포함 30명 | 심한뫼 1명 |
| 공식 | https://github.com/QwenLM/Qwen3-TTS · https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-Base | https://github.com/FunAudioLLM/CosyVoice |

모델 규모의 근거는 모델 이름과 로컬 캐시 용량이다. 구조 설명은 모델 카드에 적힌 내용만 옮겼고, 매개변수 상세 비교는 하지 않았다.

### 2.2 둘로 나눠 쓴 이유

- 심한뫼는 **실제 사람(사용자 본인)의 목소리**를 닮게 해야 해서 녹음을 복제하는 모델이 필요했다.
- 나머지 교수는 가상의 인물이라 **복제할 녹음이 없다.** 설명으로 목소리를 만드는 기능(VoiceDesign)이 있는 모델이 필요했다. 아래 비교 실험의 3종(CosyVoice3, OmniVoice, CosyVoice2)은 복제 모델이라 이 용도에 맞지 않는다.
- VoiceDesign을 대사마다 다시 돌리면 매번 음색이 달라진다(TTS-Local-Setup.md 4절). 그래서 **기준 음성 1개를 한 번만 설계**하고, 모든 대사는 그 음성을 복제하는 Base 모델로 만들었다.

### 2.3 검토했지만 쓰지 않은 대안

복제 모델 선정 실험(`VoiceTTS/결과보고서.txt`, 2026-11-05)의 결과다. 같은 녹음(약 3분)에서 참조 6개를 만들고 모델 3종 × 참조 6 × 새 문장 3 = 54개를 생성해 자동 채점했다.

| 모델 | 최고 참조 | 화자 유사도 | 발음 오류율(CER) | 속도(문장 1개) |
|---|---|---|---|---|
| **CosyVoice3** | `pron3` | **0.850** | 0% | 약 7~10초 |
| OmniVoice | `narration` | 0.836 | 0% | 약 3초 |
| CosyVoice2 | `pron3` | 0.834 | 0.9% | 약 7~10초 |

- 화자 유사도는 SpeechBrain ECAPA, 발음 오류율은 Whisper 받아쓰기로 쟀다. 실제 녹음끼리의 평균 유사도는 0.79다.
- CosyVoice3가 1위라 심한뫼에 썼다. OmniVoice는 가장 빠르지만 유사도가 약간 낮았다.
- 미세학습은 하지 않았다. 8GB 그래픽카드로는 메모리가 모자라고, 학습 기본 설정이 Windows에서 지원되지 않으며, 녹음 3분은 학습하기에 적어 음질이 나빠질 위험이 크다(결과보고서).
- **클라우드 TTS(ElevenLabs 등)**: 이명희 80줄은 실제로 ElevenLabs로 만들었다(AUDIO-CREDITS.md). 무료 플랜은 비상업 이용과 출처 표기가 조건이다. 로컬 생성은 외부 서비스의 생성 크레딧을 쓰지 않는다(TTS-Local-Setup.md 목적 절). 다른 클라우드 서비스의 가격과 약관은 **미조사**.

### 2.4 라이선스

| 구성 | 라이선스 | 근거 |
|---|---|---|
| Qwen3-TTS 모델 | Apache-2.0 | 로컬 캐시 모델 카드(`license: apache-2.0`) |
| CosyVoice 코드·Fun-CosyVoice3 | Apache-2.0 | 저장소 `LICENSE`, 모델 카드 |
| Whisper large-v3-turbo(검사용) | MIT | 모델 카드(`license: mit`) |
| OmniVoice, SpeechBrain ECAPA | **미확인** | 로컬에서 라이선스 표기를 찾지 못함 |
| ElevenLabs 무료 플랜 음성(이명희) | 비상업·출처 표기 | `AUDIO-CREDITS.md`가 인용한 ElevenLabs 안내 |

Apache-2.0은 상업적 사용을 허용하지만, 대회·발표에서 요구하는 고지 조건과 생성 음성의 이용 조건은 **법적 판단이 아니므로 사람이 확인**해야 한다. 현재 `AUDIO-CREDITS.md`에는 Qwen3-TTS·CosyVoice3 항목이 없다(9절).

---

## 3. 그래픽카드(GPU) 활용

### 3.1 실제 생성 환경

| 항목 | 값 |
|---|---|
| GPU | NVIDIA GeForce RTX 5060 Ti, VRAM 8,151MiB, 컴퓨트 능력 12.0(Blackwell) |
| 드라이버 / CUDA | 610.88 / PyTorch가 쓰는 CUDA 13.0 |
| Python / PyTorch | 3.12.16 / 2.14.1+cu130 |
| 주요 패키지 | qwen-tts 0.1.1, transformers 4.57.3, soundfile 0.14.0 |
| 정밀도 / attention | bfloat16 / `sdpa`(PyTorch 기본 어텐션). **FlashAttention은 쓰지 않음**(미설치) |
| 배치 | 한 번에 6줄(`--batch 6`). 8GB에서 VRAM이 부족하면 4로 낮춘다 |
| 검사용 환경 | Python 3.13.15, PyTorch 2.11.0+cu128, transformers 5.22.1(Whisper large-v3-turbo) |
| 이 PC의 CPU / RAM | AMD Ryzen 5 5600(6코어) / 15.9GB |

(공식 설치 안내의 Intel Arc B370 경로는 이 PC에서 쓰지 않았고, Qwen3-TTS의 Intel XPU 동작은 **미검증**이다.)

### 3.2 실측: GPU 대 CPU

조건: 교수 `SeoJinHo`의 첫 대사 11줄 중 1줄은 워밍업(측정 제외), 나머지 **10줄**을 측정. 같은 기준 음성·같은 대사·같은 시드, 오프라인(로컬 스냅샷)에서 실행. `Tools/Voice/bench_qwen.py`.

| 장치 | 정밀도 | 배치 | 클립당 생성 | 10개 합계 | 음성 길이 합 | **실시간 배율(RTF)** | 최대 VRAM | 최대 RAM |
|---|---|---|---|---|---|---|---|---|
| RTX 5060 Ti | bf16 | 1 | 14.1초 | 141.1초 | 57.5초 | 2.45 | 4,591MB | 4,399MB |
| RTX 5060 Ti | bf16 | 6 | **7.5초** | 75.0초 | 55.6초 | **1.35** | 7,038MB | 4,319MB |
| CPU(Ryzen 5 5600, 6코어) | fp32 | 1 | 36.7초 | 366.6초 | 55.3초 | **6.63** | — | 11,744MB |

- 같은 배치 1끼리 비교하면 GPU가 CPU보다 약 2.6배 빠르고, GPU 배치 6은 CPU 배치 1보다 약 4.9배 빠르다. CPU로 교수 1명(약 70줄)을 만들면 약 43분, GPU(배치 6)는 약 9분이다(생성 시간만, 검사 제외). CPU 실행은 RAM을 최대 11,744MB 쓴다(이 PC RAM 15.9GB).
- RTF = 생성 시간 ÷ 음성 길이. **1보다 크면 실시간보다 느리다.** 이 모델은 GPU에서도 실시간보다 느려서, 게임이 실행 중에 만들지 않고 **미리 만들어 `.ogg`로 넣는** 방식이 맞다.
- 배치를 6으로 올리면 클립당 시간이 거의 절반이 된다. VRAM은 7.0GB까지 올라가 8GB에 거의 가득 찬다.
- 이전 세션에서 배치 생성 중 GPU 사용률이 약 16%로 관찰됐다(기록 근거는 당시 작업 노트이며 이번에 다시 재지 않았다). 병목이 GPU 연산량이 아니라 순차 생성임을 시사하지만 **원인은 미확인**이다.
- 모델 불러오는 시간은 약 4초(캐시가 따뜻한 상태). 첫 실행 시 다운로드 시간은 **미측정**이다.

### 3.3 전체 생성에 걸린 시간

| 구간 | 시간 | 근거 |
|---|---|---|
| 29명 생성+받아쓰기+재시도 | 5.44시간(교수당 평균 11.3분) | `pipeline.log`의 교수별 `DONE` 합계(로컬 로그, 저장소 밖) |
| 그중 1차 생성만 | 4.64시간(평균 8.1초/줄, 모델 불러오기 포함) | 같은 로그의 `generated` 합계 |
| 정도윤 71줄 | 약 22분(하한) | 로그의 배치 시간 합계. 중간에 끊겨 이어 돌린 시간은 포함 안 됨 |
| 목소리 설계 35회(29명 + 재설계 6명) | 약 11분 | `design.log` 합계 |
| 심한뫼 13줄(CosyVoice3) | 88초(클립당 평균 6.8초) | 2026-11-27 생성 로그. 앞서 만든 66줄의 시간은 **미확인**(로그 없음) |
| **합계(Qwen)** | **약 6.0시간** | |

### 3.4 겪은 문제와 해결

| 문제 | 해결 |
|---|---|
| 세션이 재시작되어 생성 프로세스가 죽음(정도윤 42/71에서 중단) | 이미 있는 `.ogg`는 건너뛰므로 같은 명령으로 이어서 실행 |
| 백그라운드 작업 시간 제한으로 전체 생성이 3번 중단 | 교수 단위로 끝 표시(`DONE`)를 남겨 이어 달리게 설계 |
| 8GB에서 배치가 크면 메모리 부족 우려 | 배치 6(VRAM 7.0GB)로 고정, 부족하면 4 |
| Windows 콘솔·PowerShell 로그가 UTF-16/cp949로 깨짐 | 파이썬 UTF-8 모드(`-X utf8`, `PYTHONIOENCODING`)와 bash 리다이렉트 사용 |
| `HF_HUB_OFFLINE=1`로 오프라인 실행 시 `qwen-tts`가 Hugging Face API를 불러 실패 | 모델 ID 대신 **로컬 스냅샷 폴더**를 `--model`로 지정하면 오프라인 성공(직접 확인) |

---

## 4. 목소리 설계

### 4.1 교수 설명을 목소리로 옮기는 방법

1. 스토리 코드의 `Professor.Description`(예: "군대식 열혈 코치 · 구령과 근성, 한 세트 더")과 **교수 이미지**를 함께 봤다. 이미지에서는 성별·나이대·분위기를 읽었다. 29명 이미지를 한 장에 모아 확인했다.
2. 성별 · 나이대 · 음높이 · 음색 · 속도 · 말투를 영어 문장 하나로 적었다. 예:
   - 서진호(군대식 코치): "early 40s, muscular military-style fitness coach. Powerful deep chest voice, loud and commanding, short barking cadence, fast firm rhythm."
   - 강하늘(힐링 요가): "early 30s, calm yoga instructor. Soft low-mid voice, serene and steady, slow gentle rhythm, soothing and kind."
3. 모든 설명 뒤에 공통 문장을 붙였다: `Native Korean speaker, polite honorifics, clear consonants, natural. Not breathy, not whispery, not nasal.` (정도윤 작업에서 사용자가 "비음·숨소리 섞인 느끼한 톤은 피하라"고 해서 전체에 적용)
4. **기준 문장**은 그 교수의 실제 대사 중 숫자·영문 없이 35~70자 되는 줄을 골랐다(해당하는 줄이 없으면 25~90자로 넓히고, 그래도 없으면 `…`·`~`가 든 줄도 허용). 목소리가 대사 말투에 맞게 나오도록 하기 위해서다.
5. VoiceDesign으로 **교수당 1개**만 만들어 기준 음성으로 고정했다(후보 여러 개를 만들지 않음). 시드는 `101 + 번호`.

### 4.2 겹침 점검과 재설계

Qwen3-TTS 복제 모델이 쓰는 화자 임베딩의 코사인 유사도(평균 제거 후)로 목소리끼리 겹치는지 점검했다(`Tools/Voice/voice_similarity.py`).

- 1차에서 가까운 쌍은 백소담↔강하늘 0.80, 오명환↔신도윤 0.79, 남궁현↔신도윤 0.78, 여성 5명(오하람·류보라·하은채·윤소희·임초롱) 0.69~0.72.
- 신도윤·남궁현·백소담·오하람·임초롱·류보라 6명의 설명을 더 뚜렷하게 바꿔 다시 설계했다(예: 신도윤 "얇고 팽팽한 테너", 백소담 "낮고 허스키한 콘트랄토").
- **현재(정도윤 포함 30명, 435쌍)**: 중앙값 -0.098, 최대 **0.796(정도윤↔오명환)**, 0.7 넘는 쌍 4개, 0.6 넘는 쌍 17개. 같은 성별·연령대끼리는 비슷한 게 자연스럽다. 가장 가까운 쌍과 오명환↔남궁현(0.762), 임초롱↔오하람(0.778)은 **사람이 들어서 구분되는지 확인이 필요**하다(9절).
- 임베딩 점수는 소리 자체가 아니라 모델이 보는 값이다. 청취 확인을 대신하지 못한다.

### 4.3 정도윤만 후보를 만들어 사용자가 고름

- 1차(60대 초반)는 "너무 젊다"는 사용자 의견으로 반려됐다.
- 2차는 "늙은 목소리" 설명 2종 × 시드 3개 = 6개 후보를 만들고 **사용자가 `old70` 시드 21을 직접 골랐다.** 이 파일이 기준 음성(`qwen/refs/JungDoyun.flac`)이다.
- 이후 사용자 요구에 따라 나머지 29명은 후보 없이 교수당 1개만 만들었다.

### 4.4 교수별 설정표 (32행)

설정의 원본은 `Tools/Voice/qwen/voices.json`이다(설명 전체, 기준 문장, 시드). 아래는 요약이다. 목소리 설명은 영어 원문에서 공통 문장을 뺀 것이다.

| 학과 | 교수 | Voice ID | 모델 | 목소리 설명 | 설계 시드 | 클립 수 |
|---|---|---|---|---|---|---|
| 화학과 | 정도윤 교수 | `JungDoyun` | Qwen3-TTS | grandfather-aged man around seventy, a retired-age chemistry professor. Clearly elderly voice: aged, rough, slightly gravelly timbre, low and thick, a bit of natural vocal wear from decades of lecturing. Slow, deliberate pace with pauses. | 21 | 71 |
| 체육학과 | 서진호 교수 | `SeoJinHo` | Qwen3-TTS | man in his early 40s, a muscular military-style fitness coach. Powerful deep chest voice, loud and commanding, short barking cadence, fast firm rhythm. | 101 | 67 |
| 실용음악과 | 신도윤 교수 | `ShinDoYun` | Qwen3-TTS | man in his early 60s, a fussy music producer. Tense, thin and dry tenor voice with a slightly higher pitch, brisk clipped pace, exacting and irritable edge, light and reedy rather than deep. | 102 | 67 |
| 연극영화과 | 엄태오 교수 | `EomTaeO` | Qwen3-TTS | man in his mid 50s, a flamboyant stage actor. Rich resonant baritone, grand and dramatic, wide pitch swings, theatrical flourish on every phrase, slow showy delivery. | 103 | 67 |
| 시각디자인학과 | 남궁현 교수 | `NamgungHyun` | Qwen3-TTS | man in his mid 60s, an aristocratic art historian. Light, clear and refined mid-pitch voice with a gentle floating elegance, unhurried, old-fashioned formal diction, courteous and dignified, not deep. | 104 | 68 |
| 건축학과 | 진서한 교수 | `JinSeoHan` | Qwen3-TTS | man around 40, a cynical sharp-tongued architect. Flat deadpan mid-pitch voice, lazy sarcastic drawl, medium-slow pace, dry understatement. | 105 | 70 |
| 기계공학과 | 탁기훈 교수 | `TakGiHun` | Qwen3-TTS | man around 48, a hearty clumsy mechanical engineer. Loud husky warm voice, cheerful and booming, laughs easily, fast enthusiastic pace. | 106 | 71 |
| 스마트시스템공학과 | 박태준 교수 | `ParkTaeJun` | Qwen3-TTS | man in his early 60s, a veteran field craftsman. Gruff gravelly low voice, plain and matter-of-fact, slow and sturdy, no frills. | 107 | 75 |
| 경영학과 | 한석진 교수 | `HanSeokJin` | Qwen3-TTS | man in his early 40s, a cold calculating negotiator. Crisp controlled even tone, mid-low pitch, precise articulation, calm and unemotional, deliberate pace. | 108 | 67 |
| 미디어커뮤니케이션학과 | 조민혁 교수 | `JoMinHyuk` | Qwen3-TTS | man in his mid 30s, a rushed news reporter. Higher-mid tenor, very fast clipped rapid-fire speech, urgent and busy, short breaths between phrases. | 109 | 65 |
| 심리학과 | 배준우 교수 | `BaeJunWoo` | Qwen3-TTS | man around 30, a shy timid researcher. Soft light high-mid voice, quiet and hesitant, trailing off at ends, becomes a little brighter when excited. | 110 | 67 |
| 수학과 | 구민석 교수 | `GuMinSeok` | Qwen3-TTS | man in his mid 40s, a kindly dad-joke math teacher. Warm friendly mid-pitch voice, jovial with a smile in the voice, playful, medium pace. | 111 | 69 |
| 물리학과 | 오명환 교수 | `OhMyeongHwan` | Qwen3-TTS | man in his early 60s, a contemplative philosopher with a moustache. Deep soft slightly raspy voice, very slow, thoughtful pauses, reflective and gentle. | 112 | 101 |
| 물리학과 | 임재하 교수 | `LimJaeHa` | Qwen3-TTS | man around 40, a mischievous experiment-loving prankster. Bright mid-high voice, quick and excitable, playful mischievous energy, grins while talking. | 113 | 101 |
| 체육학과 | 강하늘 교수 | `KangHaNeul` | Qwen3-TTS | woman in her early 30s, a calm yoga instructor. Soft low-mid voice, serene and steady, slow gentle rhythm, soothing and kind. | 114 | 67 |
| 실용음악과 | 류보라 교수 | `RyuBoRa` | Qwen3-TTS | woman in her mid 40s, a free-spirited jazz musician. Very husky smoky low alto, lazy loose rhythm with elongated vowels, laughs while speaking, relaxed and bluesy. | 115 | 68 |
| 연극영화과 | 차유리 교수 | `ChaYuRi` | Qwen3-TTS | woman in her early 60s, a cool detached film director. Low dry voice, terse and flat, slow, few words, unbothered. | 116 | 67 |
| 시각디자인학과 | 하은채 교수 | `HaEunChae` | Qwen3-TTS | woman around 48, a trendy blunt designer. Bright mid-pitch voice, quick, confident and direct, casual but sharp. | 117 | 68 |
| 건축학과 | 나윤슬 교수 | `NaYunSeul` | Qwen3-TTS | woman in her late 30s, a gentle worrywart who mothers her students. Warm soft mid-high voice, caring and concerned, soft-spoken, slightly worried tone. | 118 | 70 |
| 기계공학과 | 하은별 교수 | `HaEunByeol` | Qwen3-TTS | woman in her late 30s, a cold efficient engineer. Crisp precise mid-low voice, firm and businesslike, fast and exact, no warmth wasted. | 119 | 71 |
| 스마트시스템공학과 | 최유나 교수 | `ChoiYuNa` | Qwen3-TTS | woman in her early 30s, a calm Socratic teacher. Even clear mid-pitch voice, patient and curious, measured pace, rising gently at questions. | 120 | 75 |
| 경영학과 | 윤소희 교수 | `YoonSoHee` | Qwen3-TTS | woman in her mid 40s, an energetic startup founder. High-energy bright voice, loud and fast, passionate and persuasive, big enthusiasm. | 121 | 67 |
| 미디어커뮤니케이션학과 | 송다온 교수 | `SongDaOn` | Qwen3-TTS | woman in her early 30s, a lyrical poet. Soft mellow voice, gentle and slow, musical intonation, emotion that swells quietly. | 122 | 65 |
| 심리학과 | 오세린 교수 | `OhSeRin` | Qwen3-TTS | woman in her early 40s, a mysterious psychologist who sees through people. Low quiet calm voice, slow and measured, observant, a little enigmatic. | 123 | 67 |
| 사회복지학과 | 권도경 교수 | `KwonDoKyung` | Qwen3-TTS | woman in her mid 60s, a stern old-school teacher with a hidden soft heart. Firm slightly gravelly older woman's voice, scolding but warm, strong and steady. | 124 | 65 |
| 사회복지학과 | 백소담 교수 | `BaekSoDam` | Qwen3-TTS | woman in her early 40s, a reserved good listener. Low husky contralto, almost monotone, very quiet and sparing with long pauses, plain and grounded, warm only at the very end of a sentence. | 125 | 65 |
| 화학과 | 오하람 교수 | `OhHaRam` | Qwen3-TTS | woman in her mid 50s, a showy experiment magician. Deep, rich, full-bodied theatrical voice with big pitch swings, grand and smiling, mature showwoman, expansive and animated. | 126 | 71 |
| 생명과학과 | 문혜원 교수 | `MoonHyeWon` | Qwen3-TTS | woman in her early 60s, a relaxed naturalist. Gentle slow warm older voice, unhurried and calm, kind grandmotherly softness. | 127 | 71 |
| 생명과학과 | 임초롱 교수 | `LimChoRong` | Qwen3-TTS | woman around 28, a competitive athletic biologist. Young strong sporty voice, firm and punchy with short bursts, confident, slightly lower than a typical young woman, cheers herself on. | 128 | 71 |
| 수학과 | 설아라 교수 | `SeolAhRa` | Qwen3-TTS | woman in her early 40s, a cold perfectionist genius. Low crisp sharp voice, short clipped sentences, direct and icy, rarely warm. | 129 | 69 |
| 컴퓨터공학과 | 심한뫼 교수 | `ShimHanmoe` | CosyVoice3 | 사용자 본인 녹음 복제 | — | 79 |
| 컴퓨터공학과 | 이명희 교수 | `LeeMyeonghee` | ElevenLabs | ElevenLabs Nieve - Argentinian Grandmother / Eleven v4 (2026-11-06, 무료 플랜) | — | 80 |

### 4.5 심한뫼: 사용자 본인 목소리 복제

`VoiceTTS/결과보고서.txt`의 요약이다.

1. 녹음 9개(무손실, 약 3분)를 Whisper로 받아써 문장 단위 **23개 클립**으로 잘랐다.
2. 참조 후보 6개(7~22초): `daily_A`, `casual`, `pron3`, `emotion3`, `narration`, `long_breath`.
3. 모델 3종 × 참조 6 × 새 문장 3 = 54개를 생성해 자동 채점(2.3절).
4. 모델별 최고 점수 참조를 `voice/best.json`에 저장. 심한뫼는 **CosyVoice3 + `pron3`(유사도 0.850, 오류 0%)**.
5. `Tools/Voice/generate.py --model v3`가 이 참조로 대사를 생성한다.

가장 나쁜 조합은 CosyVoice3 + `casual`(유사도 0.530, 오류율 34%)이었다. 짧고 빠른 반말 참조는 이 모델과 궁합이 나쁘다.

---

## 5. AI 활용 — 사람과 AI의 역할

### 5.1 역할 표

| 단계 | AI가 한 일 | 사람이 결정·확인한 일 |
|---|---|---|
| 대사 추출 | `export_lines.ps1`로 30명 대사 2,052줄을 뽑고, 숫자·영문·수식·자음 약어(`ㄷㄷ`, `ㅇㅈ` 등)·지문 `(한숨)`이 든 348줄을 찾아 읽기용 문장으로 바꿈(Claude 하위 에이전트 4개가 분담, 스크립트로 원문 일치·남은 기호 0 검증) | 범위(컴공과 제외, 교수당 목소리 1개), **화면 대사는 바꾸지 않는다**는 원칙 |
| 목소리 설계 | 교수 이미지·설명을 보고 설계 문장 작성, VoiceDesign 모델이 음성 생성, 화자 유사도로 겹침 점검 후 6명 재설계 | 정도윤 후보 6개 중 선택, "늙은 목소리"·"비음·숨소리 금지" 요구. 나머지 29명 목소리가 캐릭터에 맞는지는 **청취 확인 진행 중(미확인)** |
| 생성 | Qwen3-TTS Base 모델이 기준 음성을 복제해 대사 2,052개 생성(심한뫼는 CosyVoice3) | 생성 시작 지시 |
| 품질 검사 | Whisper가 받아쓰기, CER 0.25 초과 줄을 시드 바꿔 자동 재생성, 남은 줄을 분류(숫자 표기 차이 / 무음 / 실제 오독) | **청취 검수: 미확인**(AI는 소리를 들을 수 없음) |
| 게임 연동·테스트 | `Professor.Voice` 속성 연결, 폴더·`.meta` 생성, 푸시 | `Tests/VoiceCoverage.Tests.ps1` 통과 확인(AI·사람 모두 실행), Unity에서 실제 재생 확인(사용자) |

### 5.2 사용한 AI

| 종류 | 이름 | 역할 |
|---|---|---|
| 생성 모델 | Qwen3-TTS(1.7B VoiceDesign, 1.7B Base) | 목소리 설계, 대사 생성 30명 |
| 생성 모델 | CosyVoice3 | 심한뫼 |
| 외부 서비스 | ElevenLabs(Eleven v4) | 이명희 |
| 평가 모델 | Whisper large-v3-turbo | 받아쓰기 오류율(CER) |
| 평가 모델 | SpeechBrain ECAPA | 복제 모델 선정 실험의 화자 유사도 |
| 코딩 에이전트 | sh-com-claude(Claude Sonnet 5.5) | 대사 추출, Qwen 생성 파이프라인, 연동, 이 문서 |
| 코딩 에이전트 | home-claude-opus55(요청서 기준) | 연결 전수 점검, 이 문서 요청 |
| 하위 에이전트 | Claude Sonnet 4개 | 읽기용 문장 348줄 변환 |

커밋 작성자는 계정 이름이라 사람이 쓴 것과 AI가 쓴 것을 구분할 수 없다. 심한뫼·이명희·`VoiceTTS` 작업의 실제 주체는 **미확인**이다.

### 5.3 품질 관리

- **전수 자동 검사**: 생성한 클립 2,052개(+정도윤 71개 별도)를 모두 Whisper로 받아써 기대 문장과 글자 오류율을 비교했다. 청취는 AI가 할 수 없어 **0개**, 사람의 청취 검수는 **미확인**이다.
- **재생성 규칙**: CER 0.25 초과 줄만 시드 1, 2로 다시 만들어 가장 낮은 것을 채택. 말을 건너뛰거나 반복하면 `--seed`를 바꿔 그 줄만 다시.
- **결과(29명 2,052줄, `Tools/Voice/asr_summary.py`)**

| 시점 | 평균 CER | 중앙값 | CER ≤ 0.10 | CER > 0.25 |
|---|---|---|---|---|
| 1차 생성 | 0.062 | 0.000 | 87.6% | 70줄(3.4%) |
| 시드 1, 2 재시도 후 | 0.052 | 0.000 | 87.9% | 50줄(2.4%) |
| 2차 보정 후(최종) | 0.051 | 0.000 | 88.3% | 48줄(2.3%) |

- 평균에는 백소담의 **말 없는 무음 줄**("…….", 0.2초)이 CER 32로 들어가 있다. 이 줄을 빼면 최종 평균은 약 0.035다.
- 남은 48줄: **45줄은 숫자 표기 차이**(Whisper가 "이십"을 `20`으로 적음), 1줄은 위 무음 줄, **실제 오독은 2줄**이다(차유리 "괄호는 안 읽어." → "과로는", 하은채 "정답 거터…" → "걷어").
- 재생성 시도: 1차 재시도 70줄, 2차 보정 14줄(자음 약어 11줄 + 오독 3줄). 정도윤은 오독 5줄과 "음…" 대사를 따로 재생성했다. 심한뫼 13줄 중 7줄과 3줄을 읽기 규칙 추가 후 다시 만들었다.
- 읽기 규칙: 영어·숫자가 이상하게 읽히면 소리만 바꾸는 규칙을 둔다. 29명은 `Tools/Voice/qwen/spoken.tsv`(대사별 읽기용 문장), 심한뫼는 `generate.py`의 `SAY` 표(LIFO→라이포, FIFO→파이포, target→타깃, k번째→케이번째, 2진수, 10진수, 1이). **화면 글자와 파일 이름은 그대로**다.

### 5.4 실패 사례와 교훈

1. **검사가 놓친 자음 약어**: 읽기용 문장 변환의 남은 문자 검사 목록에 `ㄷ`, `ㅇ`이 없어서 하은채 10줄과 진서한 1줄에 `ㄷㄷ`, `ㅇㅈ`, `ㄹㅇ`이 그대로 들어가 이상하게 읽혔다. 사람이 들으면 바로 알 문제였지만 Whisper 점수로는 중간 정도로 나와 눈에 띄지 않았다. → 검사 문자 범위를 한글 자모 전체로 넓히고(`[ㄱ-ㅣ]`), 해당 줄을 읽기용 문장으로 고쳐 다시 생성했다. **교훈: 점수만 믿지 말고 입력 텍스트를 먼저 전수 검사한다.**
2. **`\b`가 한글 앞에서 안 먹음**: 심한뫼 읽기 규칙 `\bLIFO\b`가 "LIFO예요"에 적용되지 않아 소리가 안 바뀌었다(한글도 단어 문자라 경계로 인식되지 않음). 소리 길이가 이전과 같다는 점으로 알아챘다. → 영문자 앞뒤 검사(`(?<![A-Za-z])…(?![A-Za-z])`)로 바꿨다.
3. **푸시에서 스토리 파일 누락**: 처음 푸시에는 `Voice = "…"`을 넣은 스토리 파일 15개가 빠져 게임이 음성을 연결하지 못했다(경로 필터가 이미 스테이징된 파일을 놓침). 푸시 직후 상태를 확인해 두 번째 커밋으로 바로잡았다. **교훈: 푸시 뒤 원격에서 결과를 다시 확인한다.**
4. 그 밖에: 짧은 감탄사("음…")는 Whisper가 적지 않아 CER로 못 잡는다. 읽기용 문장을 "으음…"으로 늘려 앞소리가 생기는 것을 파형으로 확인했지만, 그 소리가 "음"으로 들리는지는 **청취 미확인**이다.

---

## 6. 게임 연동

### 6.1 해시 파일명

- 클립 이름 = **대사 원문의 UTF-8 MD5 앞 12자리**(`86950fd4a084.ogg`). `lines.txt`는 `파일<TAB>대사` 한 줄씩이다.
- 이유: 대사를 고치면 이름이 바뀌어 **바뀐 대사만 다시 생성**하면 된다. 안 바뀐 클립은 목소리도 그대로 유지된다.
- 생성기는 **목록에 없는 클립을 지운다**(`.ogg`와 `.meta`). 대사가 스토리에서 빠졌을 때 옛 클립이 쌓이지 않게 하기 위해서다. 그래서 일부 대사만 넣고 돌리면 안 되고, `export_lines.ps1`이 뽑은 **전체 목록**을 넣어야 한다.

### 6.2 `ProfessorVoice` 매칭 (`Assets/Scripts/ProfessorVoice.cs`)

1. 화면 대사에서 서식 태그(`<…>`)를 지우고 공백을 하나로 줄여(`Normalize`) `lines.txt`의 같은 정규화 문장과 맞춘다.
2. 한 줄 전체가 없으면, **알려진 대사를 공백으로 이어 붙인 줄**로 보고 앞에서부터 쪼개 찾는다(`Split`). 게임이 호감도 반응 대사(`Fond`, `Wary`)를 답 앞뒤에 붙이기 때문이다.
3. 교수 대사가 아닌 줄(퀴즈 피드백, 미니게임 요약 등)은 건너뛴다.
4. 찾은 클립을 `Resources.Load`로 읽어 순서대로 돌려준다.

### 6.3 재생 (`Assets/Scripts/NovelAudio.cs`)

- 클립을 **차례로** 재생하고 사이에 0.15초를 둔다. 새 대사가 시작되면 이전 재생을 끊는다.
- 음성 볼륨은 설정의 `Voice`(기본 1.0, 0~1, `PlayerPrefs`의 `AION.Voice`에 저장)다. 0이면 재생하지 않는다.
- **오토 모드**는 음성이 끝나야(`!novelAudio.Speaking`) 다음 대사로 넘어간다(`GameController.cs`).

### 6.4 연결 전수 검사 (`Tests/VoiceCoverage.Tests.ps1`)

막는 실수: **대사만 고치고 녹음을 다시 안 만든 경우**, 그리고 `Voice` 미설정, 폴더·`lines.txt`·`.meta` 누락, 파일 이름 누락, `.ogg` 헤더가 `OggS`가 아닌 파일, 어느 대사에도 안 쓰이는 고아 클립, 대사와 녹음 연결 누락. 2026-11-27 실행 결과: **PASS, 교수 32명, 2282/2282 대사 연결, 알려진 누락 허용 0건**.

---

## 7. 수치 요약표

| 항목 | 값 | 계산 방법 |
|---|---|---|
| 교수 수 | 32 | `Professor.Voice`가 있는 교수, 테스트 요약 줄 |
| 학과 수 | 16 | `voices.json`의 학과(중복 제거) |
| 대사 수 / 연결 | 2,282 / 2,282 | `powershell -File Tests/VoiceCoverage.Tests.ps1` |
| 클립 수 | 2,282 | `python Tools/Voice/voice_stats.py` |
| 총 재생 시간 | 233.4분 | 같은 명령. 각 `.ogg`의 길이(마지막 페이지 granule ÷ 샘플레이트) 합. `soundfile`로 40개 대조해 오차 0.0000초 |
| 평균 / 최장 클립 | 6.14초 / 12.48초(정도윤) | 같은 명령 |
| 용량 | `.ogg` 89.8MiB, 폴더 약 99MB | 같은 명령 / `du -sh Assets/Resources/Voice` |
| 총 생성 시간 | 약 6.0시간 | `Select-String "DONE" voices\pipeline.log` 합계 + 정도윤 로그 + `design.log`(로컬 로그, **저장소 밖이라 재현 불가**) |
| 클립당 생성(GPU) | 7.5초(배치 6), 14.1초(배치 1) | `python Tools/Voice/bench_qwen.py --device cuda:0 --batch 6` |
| 재생성 수 | 1차 70줄 + 2차 14줄(29명), 정도윤 6줄 이상, 심한뫼 10줄 | 위 로그와 `asr_summary.py`(재생성 JSON은 로컬) |
| 오류율 | 평균 0.051, 중앙값 0.000, ≤0.10 88.3% | `python Tools/Voice/asr_summary.py 결과*.json`(JSON은 로컬) |
| 목소리 겹침 | 최대 0.796, 0.7 초과 4쌍 | `python Tools/Voice/voice_similarity.py` |
| **청취 검수 수** | **미확인** | AI 청취 0개. 사람 청취는 사용자 확인 중 |

---

## 8. 재현 방법

### 8.1 새 대사가 생겼을 때 (Qwen3-TTS 30명)

```powershell
# 1) 대사 추출 (프로젝트 루트)
.\Tools\Voice\export_lines.ps1 -Voice SeoJinHo
# 2) 바뀐 대사만 생성 (Qwen3-TTS 환경의 python)
python Tools/Voice/generate_qwen.py SeoJinHo --dry-run    # 몇 개 만들지 먼저 확인
python Tools/Voice/generate_qwen.py SeoJinHo
# 3) 품질 검사 (선택)
python Tools/Voice/check_asr.py Tools/Voice/SeoJinHo.asr_in.tsv 결과.json
# 4) 연결 검사
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/VoiceCoverage.Tests.ps1
```

- 이상한 줄은 `generate_qwen.py SeoJinHo --line "그 줄 그대로" --seed 2`로 그 줄만 다시 만든다(같은 이름으로 덮어씀).
- 읽는 법이 필요하면 `Tools/Voice/qwen/spoken.tsv`에 `화면 대사<TAB>읽기용 문장`을 추가한다.
- 작업 뒤에는 `.ogg.meta`까지 커밋한다(Unity가 만든다). `Tools/Voice/<Id>.lines.txt`와 `<Id>.asr_in.tsv`는 중간 산출물이라 커밋하지 않는다.
- **같은 기준 음성이므로 같은 목소리**가 나온다. 다만 생성은 확률적이라 같은 대사도 억양과 길이가 조금 다르다(직접 확인: 같은 이름 `86950fd4a084.ogg`, 6.88초 대 기존 7.20초).

### 8.2 새 교수를 추가할 때

1. `Tools/Voice/qwen/voices.json`에 항목 추가(`id`, `style`, `ref_text`, `design_seed`, `ref: "refs/<Id>.flac"`, `engine: "qwen3-tts"`).
2. `python Tools/Voice/design_qwen.py <Id>` → `qwen/refs_new/<Id>.flac`을 **직접 듣고** 마음에 들면 `qwen/refs/`로 옮긴다.
3. `python Tools/Voice/voice_similarity.py`로 기존 목소리와 겹치지 않는지 본다.
4. 스토리 코드에 `Voice = "<Id>"`를 달고 8.1을 실행한다.

### 8.3 다른 PC에서 다시 만들 때

| 필요한 것 | 값 |
|---|---|
| Python | 3.12(검증됨). `uv venv --python 3.12`, `uv pip install qwen-tts soundfile` (자세한 절차: [TTS-Local-Setup.md](TTS-Local-Setup.md)) |
| 모델 | `Qwen/Qwen3-TTS-12Hz-1.7B-Base`(약 4.3GB, 대사 생성), 새 목소리를 설계할 때만 `…-1.7B-VoiceDesign`(약 4.3GB), 검사용 `openai/whisper-large-v3-turbo`(약 1.6GB). 모두 Hugging Face에서 받는다. 저장소에는 올리지 않는다 |
| GPU | NVIDIA, VRAM 8GB면 배치 6까지(최대 7.0GB). 이 PC(RTX 5060 Ti)에서는 CUDA 13.0(cu130) 빌드로 생성, CUDA 12.8(cu128) 빌드로 받아쓰기 검사가 동작했다. 다른 GPU 세대의 PyTorch 요구 버전은 **미확인** |
| CPU만 있을 때 | `--device cpu`로 동작(측정됨). 클립당 36.7초(RTF 6.63), 교수 1명 약 43분, RAM 최대 11,744MB(3.2절 표) |
| 예상 소요 | 대사 N줄 ≈ N × 7.5초(RTX 5060 Ti, 배치 6) + 모델 불러오기. 교수 1명(약 70줄)은 약 9분, 검사·재시도 포함 평균 11분 |
| 오프라인 | `--model <C:\Users\<이름>\.cache\huggingface\hub\models--Qwen--Qwen3-TTS-12Hz-1.7B-Base\snapshots\<리비전>>`로 로컬 폴더 지정 |

사용한 리비전: Base 1.7B `fd4b254389122332181a7c3db7f27e918eec64e3`, VoiceDesign 1.7B `5ecdb67327fd37bb2e042aab12ff7391903235d3`, Whisper `41f01f3fe87f28c78e2fbf8b568835947dd65ed9`.

심한뫼(CosyVoice3)는 `<voice-clone>\cosyvoice\.venv\Scripts\python.exe Tools\Voice\generate.py Tools\Voice\ShimHanmoe.lines.txt Assets\Resources\Voice\ShimHanmoe --clone <voice-clone 경로> --model v3`(AGENTS.md). 이 환경(약 15GB)은 저장소 밖에 있다. 이명희(ElevenLabs)는 이 도구로 다시 만들 수 없다.

### 8.4 이 문서의 재현 검증

한 교수(`SeoJinHo`)의 대사 1개를 위 명령으로 임시 폴더에 다시 만들어 **기존과 같은 파일 이름(`86950fd4a084.ogg`)** 이 생성되는지 확인했다. 읽기용 문장("30초"→"삼십 초")도 적용됐고, `Assets`는 변경되지 않았다. 실행 환경은 오프라인(로컬 스냅샷 지정)이었다.

---

## 9. 한계와 윤리

### 9.1 사람이 결정할 항목

| # | 항목 | 현재 상태 | 결정이 필요한 점 |
|---|---|---|---|
| 1 | **합성 음성 고지** | 게임·문서에 합성 음성이라는 고지 없음 | 고지할지, 한다면 어디에(시작 화면, 설정, 크레딧, 발표) 어떤 문구로 |
| 2 | **`VoiceTTS` 녹음 원본·화자 임베딩의 공개 범위** | Git에 올라가 있음: `VoiceTTS/rec/` 녹음 9개 + 받아쓰기(약 16.8MB), `VoiceTTS/voice/` 기준 음성 4개 + **화자 임베딩 `me.pt`** + `best.json`(약 3.3MB), `VoiceTTS/work/clips/` 문장 클립 23개(약 5.9MB), `work/refs/` 참조 6개, `output/` 샘플 16개. 저장소 라이선스는 MIT. 저장소 공개 여부는 **미확인** | 계속 올려 둘지, 비공개로 돌릴지. **삭제는 사용자 결정 없이 하지 않았다.** 지워도 Git 이력에는 남으므로 이력 정리가 필요한지도 함께 결정 |
| 3 | **이명희 음성의 이용 조건** | ElevenLabs 무료 플랜: 비상업 이용·출처 표기(`AUDIO-CREDITS.md`) | 대회·발표·배포 범위가 이 조건에 맞는지. 맞지 않으면 다른 방식으로 다시 생성 |
| 4 | **출처 표기** | `AUDIO-CREDITS.md`에 Qwen3-TTS·CosyVoice3 항목 없음 | 모델 이름·라이선스(Apache-2.0)를 추가할지 |
| 5 | **목소리 구분** | 정도윤↔오명환 0.796, 임초롱↔오하람 0.778, 오명환↔남궁현 0.762 | 들어 보고 구분되면 유지, 비슷하면 재설계 |

### 9.2 본인 목소리 복제와 동의

심한뫼는 **사용자 본인의 녹음**을 복제했다. 복제 대상이 본인이고 동의 문제가 없다는 점은 사용자가 확인해야 한다. 다른 사람의 목소리로 복제하려면 본인 동의를 받은 녹음만 쓴다(`VoiceTTS/결과보고서.txt`, TTS-Local-Setup.md 4절).

### 9.3 기술적 한계

- 발음·억양: 한국어 숫자·영문 약어·자음 약어를 그대로 두면 이상하게 읽는다. 읽기용 문장으로 풀어 주지만(5.3절) 모든 경우를 잡았는지는 보장하지 못한다. 실제 오독 2줄이 남아 있다.
- 생성이 확률적이라 같은 대사도 매번 억양·길이가 조금 다르다.
- 감정 표현(웃음, 한숨, 짧은 감탄사)은 약하다. `(한숨)`은 "후…" 소리로 바꾸는 방식이라 실제 한숨과 다르다.
- 모델이 실시간보다 느려(3.2절) 게임 안에서 즉석 생성은 못 한다.
- 자동 검사의 한계: CER은 발음 정확도만 재고, 목소리가 캐릭터에 맞는지·어색하지 않은지는 재지 못한다.

### 9.4 미검증 항목

- 사람의 청취 검수(전수·표본 모두), Unity 실제 재생과 오토 모드 동작
- Intel GPU(XPU) 경로, FlashAttention 적용 효과
- 앞서 만든 심한뫼 66줄의 생성 시간, 이명희 음성 생성 방식의 세부
- OmniVoice·SpeechBrain ECAPA 라이선스, 다른 클라우드 TTS의 가격·약관
- 푸시 전 Unity 컴파일 점검(라이선스 문제로 실행되지 않음). 대신 `Tests/*.ps1` 15종이 통과했다.
