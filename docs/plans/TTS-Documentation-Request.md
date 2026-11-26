# 요청서: 교수 음성(TTS) 제작 기술 문서화

작성 2026-11-27 · 요청 home-claude-opus55(사용자 승인) → 담당 sh-com-claude · 상태: **완료(2026-11-27)** — 산출물: [TTS-Voice-Production.md](TTS-Voice-Production.md), 생성·설계·검사·측정 스크립트와 설정 `Tools/Voice/generate_qwen.py`·`design_qwen.py`·`check_asr.py`·`bench_qwen.py`·`voice_stats.py`·`voice_similarity.py`·`asr_summary.py`·`qwen/`. 사용자 결정 대기: 문서 9.1절

교수 32명 음성을 어떤 기술로 어떻게 만들었는지 정리해 발표와 보고서에 쓰려고 합니다. 이 요청서를 읽고 아래 "산출물"을 만들어 주세요.

## 1. 목적
- 팀원과 심사위원이 다음을 이해할 수 있어야 합니다.
  - 무엇을 썼나
  - 왜 그걸 골랐나
  - 그래픽카드(GPU)를 어떻게 썼나
  - AI를 어떻게 활용했나
  - 결과가 어땠나(숫자로)
- 새 대사가 생겼을 때 **다른 팀원이 같은 목소리를 다시 만들 수 있어야** 합니다.

## 2. 현재 저장소에서 확인된 사실 (2026-11-27, home-claude-opus55 확인)

| 항목 | 값 | 근거 |
|---|---|---|
| 음성이 있는 교수 | 32명 (16개 학과 × 2) | `Professor.Voice`, `Tests/VoiceCoverage.Tests.ps1` |
| 대사와 녹음 연결 | 2282 / 2282 | `Tests/VoiceCoverage.Tests.ps1` |
| 클립 수·용량 | `.ogg` 2,282개, `Assets/Resources/Voice` 약 100MB | `find`, `du` |
| Qwen3-TTS 음성 | 30명 (컴공 제외 29명 + 정도윤) | 커밋 `ff53565`, `a4ec9f8` |
| CosyVoice3 복제 음성 | 심한뫼 (사용자 본인 녹음 복제) | 커밋 `486a715`, `797b356` |
| 이명희 음성 | 생성 방식 **미확인** | 커밋 `88ae068` |
| 복제 모델 선정 실험 | 3종 비교 → CosyVoice3 1위 | `VoiceTTS/결과보고서.txt` |
| 설치 안내 | Qwen3-TTS 로컬 설치, Intel GPU·CPU 경로, VoiceTTS 부록 | `docs/plans/TTS-Local-Setup.md` |
| 게임 연동 | 해시 파일명, `lines.txt`, `ProfessorVoice` 매칭 | `Tools/Voice/generate.py`, `Assets/Scripts/ProfessorVoice.cs` |

- **복제 모델 선정 실험의 세부:**
  - 비교한 모델: CosyVoice3, OmniVoice, CosyVoice2
  - 채점 방식: 화자 유사도(SpeechBrain ECAPA)와 발음 오류율(Whisper 받아쓰기 CER)
  - 생성 규모: 참조 음성 6개 × 문장 3개 × 모델 3종 = 54개
- **저장소에 없는 것 (이번 요청의 핵심 공백):**
  - 30명을 만든 **Qwen3-TTS 생성 스크립트와 교수별 목소리 설정**이 없습니다. `Tools/Voice/generate.py`는 CosyVoice3 전용입니다.
  - **실제로 쓴 GPU와 생성 속도 기록**이 없습니다. `TTS-Local-Setup.md`에는 "Intel Arc B370에서 Qwen3-TTS 미검증"이라고만 적혀 있고, VoiceTTS 부록은 NVIDIA GPU 기준입니다.
- **확인 필요:** `VoiceTTS/rec/`, `VoiceTTS/voice/`, `VoiceTTS/work/clips/`에 사용자 본인 녹음 원본과 화자 임베딩(`me.pt`)이 Git에 올라가 있습니다. 문서의 윤리 절에서 이 파일들의 공개 범위를 사용자에게 확인받으세요. 삭제는 사용자 결정 없이 하지 않습니다.

## 3. 산출물
1. **`docs/plans/TTS-Voice-Production.md`** (신규, 한국어)
   - 설치 절차는 새로 쓰지 말고 `TTS-Local-Setup.md` 링크로 대신합니다.
2. **재현용 Qwen3-TTS 생성 스크립트와 설정** (`Tools/Voice/` 아래)
   - 교수별 목소리 설정: Voice ID, 목소리 설명(디자인 프롬프트) 또는 참조 음성, 시드, 생성 파라미터
   - 입력은 `export_lines.ps1` 출력, 출력은 기존 규칙(MD5 앞 12자리 `.ogg` + `lines.txt`)과 같게 합니다.
   - 기존 `generate.py`처럼 **바뀐 대사만 생성**합니다. 목록에 없는 클립을 지운다면 그 동작을 문서에 적습니다.
   - 커밋하지 않는 것: 모델 가중치, 파이썬 환경, 대용량 파일, API 키, 개인 경로. "어디서 받는지"만 적습니다.
3. **이 요청서의 상태 갱신**: 맨 위 상태를 "완료"로 바꾸고 산출물 링크를 답니다.

## 4. `TTS-Voice-Production.md` 절 구성

### 0. 한눈에 보기 (발표 슬라이드 1장)
- 교수 32명, 대사 2,282개, 총 재생 시간, 총 용량
- 모델: Qwen3-TTS / CosyVoice3
- 장비: GPU 모델명·VRAM
- 총 생성 시간
- 비용: 로컬이면 API 비용 0원. 전기·장비만 사용
- 인터넷 사용: 모델 다운로드 때만인지

### 1. 전체 흐름
- 스토리 코드 → `export_lines.ps1` → 생성 스크립트 → `.ogg` + `lines.txt` → Unity `Resources/Voice/<Voice>/` → `ProfessorVoice`가 대사와 맞춰 재생 → `Tests/VoiceCoverage.Tests.ps1`로 전수 검사
- 단계마다 입력 파일, 출력 파일, 실행 명령을 표로 정리합니다.

### 2. 모델과 선택 이유
- Qwen3-TTS와 CosyVoice3를 각각 설명합니다. 쉬운 설명 한 줄과 기술 설명(구조, 파라미터 규모, 제로샷 복제·목소리 설계 방식)을 함께 씁니다.
- 두 모델을 나눠 쓴 이유: 녹음 없이 설계한 목소리(29명) 대 본인 녹음 복제(1명)
- 검토했지만 안 쓴 대안과 이유: 클라우드 TTS(ElevenLabs 등), OmniVoice, CosyVoice2
  - VoiceTTS 실험 점수를 인용합니다. 화자 유사도 CosyVoice3 0.850, OmniVoice 0.836 등
- 라이선스와 대회·발표 사용 가능 여부. 공식 저장소나 모델 카드 링크를 답니다.

### 3. 그래픽카드(GPU) 활용 — 핵심
- 실제 생성에 쓴 장치: GPU 모델, VRAM, 드라이버, CUDA·XPU, PyTorch 버전
- 정밀도(fp16·bf16·fp32), 배치 여부, 어텐션 구현(FlashAttention 등)
- **실측 표 (GPU 대 CPU)**
  - 같은 대사 10개, 워밍업 1회 뒤 측정합니다.
  - 항목: 클립당 생성 시간, 실시간 배율(RTF = 생성 시간 ÷ 음성 길이), 최대 VRAM·RAM
  - 측정 명령과 조건을 함께 적습니다.
  - 측정할 수 없으면 "미측정"과 이유를 씁니다.
- 30명 전체 생성에 걸린 총 시간. 로그가 없으면 "추정"이라고 표시하고 추정 근거를 씁니다.
- 겪은 문제와 해결: 메모리 부족, 첫 실행 지연, 장치 호환 등

### 4. 목소리 설계
- 교수 설명(스토리의 `Description`)을 목소리 특성(나이·성별·톤·속도·말투)으로 어떻게 옮겼는지
- **교수별 설정표 32행**: 학과, 교수, Voice ID, 모델, 목소리 설명 또는 참조, 시드
- 심한뫼: 녹음 9개(약 3분) → Whisper로 받아쓰기 → 클립 23개 → 참조 6개 → 자동 채점 → `best.json` 선정. `VoiceTTS/결과보고서.txt`를 요약합니다.

### 5. AI 활용 — 사람과 AI의 역할을 나눠서
| 단계 | AI가 한 일 | 사람이 결정·확인한 일 |
|---|---|---|
| 대사 추출 | | |
| 목소리 설계 | | |
| 생성 | | |
| 품질 검사 | | |
| 게임 연동·테스트 | | |

- 사용한 AI를 모두 적습니다.
  - 생성 모델: Qwen3-TTS, CosyVoice3
  - 평가 모델: Whisper, SpeechBrain ECAPA
  - 코딩 에이전트: sh-com-claude, home-claude-opus55 등
  - 각각의 역할을 함께 씁니다.
- 품질 관리
  - 말 건너뜀·반복·영어 철자 읽기를 어떻게 찾았는지. 청취는 전수인지 표본인지, 몇 개 들었는지
  - 몇 개를 다시 만들었는지
  - `SAY` 읽기 규칙(LIFO→라이포 등)과 `--seed` 재생성
- 실패 사례와 교훈 1~3개

### 6. 게임 연동
- 해시 파일명(대사 MD5 앞 12자리)을 쓰는 이유: 바뀐 대사만 다시 생성
- `lines.txt` 형식과 `ProfessorVoice` 매칭: 한 줄 전체, 또는 이어 붙인 줄을 쪼개서 찾기
- 음성 볼륨 설정, 오토 모드가 음성을 기다리는 동작
- `Tests/VoiceCoverage.Tests.ps1`이 막는 실수: 대사만 고치고 녹음을 다시 안 만든 경우

### 7. 수치 요약표
- 항목: 교수 수, 대사 수, 클립 수, 총 재생 시간, 총 용량(MB), 평균·최장 클립 길이, 총 생성 시간, 재생성 수, 청취 검수 수
- **각 수치 옆에 계산 방법**(명령이나 스크립트)을 적습니다. 예: `.ogg` 길이 합계를 구하는 스크립트

### 8. 재현 방법
- 새 대사가 생겼을 때: `export_lines.ps1 -Voice <ID>` → 생성 스크립트 → `Tests/VoiceCoverage.Tests.ps1`
- 다른 PC에서 다시 만들 때 필요한 것: 모델 받는 곳, Python 버전, GPU 요구 사항, 예상 소요 시간

### 9. 한계와 윤리
- 합성 음성이라는 고지를 할지, 한다면 어떤 방식으로 할지. **사람이 결정할 항목으로 표시합니다.**
- 본인 목소리 복제 동의(심한뫼), 녹음 원본과 화자 임베딩이 Git에 올라가 있는 문제 → 사용자 확인
- 발음·억양 한계, 미검증 항목

## 5. 작성 규칙
- **확인한 사실만 씁니다.** 모르는 값은 "미측정" 또는 "미확인"으로 표시하고, 추측한 숫자로 칸을 채우지 않습니다.
- 숫자는 가능하면 실제 명령으로 다시 계산하고, 그 명령을 문서에 남깁니다.
- 모델·라이선스·논문 정보에는 공식 출처 링크를 답니다.
- 개인 경로, 계정, 키는 쓰지 않습니다. 경로는 `C:\Users\<이름>\...` 형태로 씁니다.

## 6. 진행과 커밋
```powershell
git pull
bash Tools/agent-sync.sh claim "TTS 제작 기술 문서 + Qwen3 생성 스크립트" docs/plans/TTS-Voice-Production.md docs/plans/TTS-Documentation-Request.md Tools/Voice/
# 작성과 스크립트 정리
Get-ChildItem Tests/*.ps1 | ForEach-Object { powershell.exe -NoProfile -ExecutionPolicy Bypass -File $_.FullName; "$($_.Name): $LASTEXITCODE" }   # 전부 0
git diff --check
bash Tools/agent-sync.sh finish "TTS 제작 기술 문서와 Qwen3-TTS 생성 스크립트 추가" docs/plans/TTS-Voice-Production.md docs/plans/TTS-Documentation-Request.md Tools/Voice/
```
- `git add -A` 금지. 기존 `generate.py`·`export_lines.ps1`·`ExportLines.cs`의 동작은 바꾸지 않습니다.
- 새 PowerShell·C# 파일에 한국어가 있으면 **UTF-8 BOM**으로 저장합니다. Windows PowerShell 5.1이 BOM 없는 한국어 파일을 잘못 읽습니다.
- `PROGRESS.md` Done에 한 줄을 **추가**합니다(기존 줄 수정 금지). 플레이 방법 자리에는 "문서 위치와 재현 명령"을 적습니다.

## 7. 완료 체크리스트
- [ ] `docs/plans/TTS-Voice-Production.md`에 4절의 0~9절이 모두 있고, 빈칸 없이 값 또는 "미측정/미확인"이 적혀 있다
- [ ] GPU 대 CPU 실측 표가 있다(또는 미측정 사유)
- [ ] 교수별 설정표 32행
- [ ] AI와 사람의 역할 표
- [ ] 수치마다 계산 명령이 있다
- [ ] Qwen3-TTS 생성 스크립트·설정이 `Tools/Voice/`에 있고, 문서의 재현 명령으로 한 교수의 대사 1개를 다시 만들어 기존 파일과 같은 이름으로 생성되는지 확인했다
- [ ] 비밀값, 개인 경로, 모델 가중치가 커밋되지 않았다
- [ ] `Tests/*.ps1` 전부 exit 0, `git diff --check` 통과
- [ ] 이 요청서의 상태를 "완료"로 바꿨다
- [ ] 사용자에게 보고: 문서 경로, 절 목록, 추가 스크립트, 실측 수치, 미측정 항목, **사람이 결정할 항목**(합성 음성 고지, VoiceTTS 녹음 원본 공개 범위)
