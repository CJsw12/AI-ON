# AI-ON 프로젝트 진행도 및 작업 맥락 (PROGRESS.md)

이 문서는 프로젝트 세션 진행도, 팀 협업 가이드, 그리고 개발 맥락을 일원화하여 관리하는 단일 문서입니다.

> ### 📌 팀원 공통 세션 및 협업 3원칙
> 1. **작업 시작 전**: git pull 후 본 문서를 가장 먼저 확인하여 다른 팀원의 최신 작업 현황(Done)과 할 일(Next Steps)을 확인합니다.
> 2. **작업 종료 시 1회만 갱신**: 자잘한 커밋마다 수정하면 병합 충돌이 발생하므로, 작업 세션이 일단락되어 푸시하기 직전에만 1. 완료된 작업 (Done)과 5. 다음 진행 작업 (Next Steps)을 간결한 불릿 포인트로 갱신합니다.
> 3. **핵심 맥락 일원화 유지**: 불필요한 장황한 설명을 배제하고 형상, 완료 및 검증, 결정 사항, 결함/부채, 다음 진행 작업 5대 핵심 항목을 명확하게 유지합니다.

---

## 1. 형상 (Baseline)
- **프로젝트 루트**: `AI-ON` (Repository Root)
- **Git 브랜치**: `main`
- **병합 기준**: 원격 main `5f967d0`, 시선 진단 `a8b0bd8`, 로컬 카드게임 병합 기록 `a5b3a49` 통합 점검 (2026-11-05)
- **주요 변경 대상**: `GameController.GazeTest.cs`, `GameController.IPhoneGaze.cs`, `GazeMapper.cs`, 시선 추적 인수인계 문서

---

## 2. 완료 및 검증 (Done)
- **[Codex] 2026-11-06 컴퓨터공학과 주제 선택형 강의·AI 전공 퀴즈 시범**: 4장 오피스아워 `slots[6]`을 관심도(`i`)를 올리는 강의 미니게임으로 바꾸고, 자료구조·알고리즘·컴퓨터 기초 중 하나를 골라 강의 대화와 정답 검증형 퀴즈를 진행한다. AI 문제 문장과 오답 맞춤 해설은 실패·지연 시 오프라인으로 이어진다.
  - **플레이 방법**: 타이틀 → 공학부 → 컴퓨터공학과 → 교수 선택 → 4장 오피스아워에서 강의 도전. 또는 타이틀 → 미니게임 연습 → 공학부 → 컴퓨터공학과 → 강의. AI 모드를 켜고 키를 사용할 수 있으면 교수 말투 문제와 맞춤 오답 해설이 나온다.
  - 검증: 생성기 3주제×2난이도×1,000회(6,000회)·응답 검증 사례 9종 통과, 기존 회귀 테스트 5종 및 신규 테스트 모두 exit 0, Unity 6000.3.23f1 배치 컴파일 exit 0. 기존 `AiProfessor.cs` CS0162와 `CoffeeRecipeSO.cs` CS0649 경고가 있음. 실제 AI 응답과 플레이 화면 조작은 미검증.
- **[Codex] 2026-11-06 AI 모드 심한뫼 교수의 헛소리 응답 반응**: AI 채점에서 무례하지 않은 딴소리 또는 성의 낮음(4글자 미만 포함)이면 고정 대사 `헛소리 하지 마!`와 심한뫼 교수의 화난 표정·녹음 음성을 보여 주고 음절 맞춤 표시를 적용한다. 다른 교수와 기본 모름 단독은 기존 AI 대답을 유지한다.
  - **플레이 방법**: 타이틀 → 공학부 → 컴퓨터공학과 → 심한뫼 교수, AI 모드 켜고 자유 답변에 "오늘 점심 뭐 먹지" 또는 "ㅇㅇ"을 입력한다.
  - 음성 크기는 설정 화면의 `교수님 목소리` 슬라이더로 조절한다.
  - 검증: 회귀 테스트 5종 exit 0, 사용자 Unity 실행에서 실제 AI 응답 → 삿대질 사진·녹음 재생·음절 맞춤 표시·원래 사진 복귀 확인. `ProfessorPersonality`에서 기존 `AiProfessor.cs` CS0162 및 `CoffeeRecipeSO.cs` CS0649 경고가 출력됨.
- **[Claude] 2026-11-06 심한뫼 교수 목소리 (TTS)**: 사용자 본인 녹음(9개, 약 3분)으로 복제한 목소리(CosyVoice3, 로컬 `voice-clone` 환경)로 심한뫼 교수 대사 72줄 전부를 음성으로 만들어 `Assets/Resources/Voice/ShimHanmoe/`(ogg, 2.4MB)에 넣고, 대사가 나올 때 재생한다.
  - 대상: 본문·후속 대사, 선택지/문제/미니게임 반응(Fond/Wary가 붙으면 조각을 이어 재생), 엔딩 대사, 시선 이탈 끼어들기. AI 모드 실시간 대사·힌트와 엔딩 도감은 목소리 없음.
  - 코드: [`ProfessorVoice.cs`](Assets/Scripts/ProfessorVoice.cs)(대사→음성), `NovelAudio.Speak`, `GameController.SpeakOnce`, `Professor.Voice`. 설정에 `교수님 목소리` 슬라이더 추가, 자동 진행은 목소리가 끝날 때까지 대기.
  - 대사를 고치면 [`Tools/Voice/README.md`](Tools/Voice/README.md)의 두 명령으로 바뀐 줄만 다시 생성.
  - 검증: Unity 배치 컴파일 오류 0, 테스트 5종 exit 0, Whisper 받아쓰기 평균 글자 오류 약 3%(말을 빠뜨리거나 반복한 3개는 다른 시드로 재생성). 게임 안 실제 재생은 미검증.
- **[Claude] 2026-11-06 웹캠 화면 맞춤: 고정점 + 따라보기 혼합 (Saxena 외 ETRA 2022)**: 3×3 고정점 9개 → 8자 궤적을 12초간 눈으로 따라보기(1초 예고, 0.12초 지연 보정, 0.5초 단위로 묶어 보정점 ~23개 추가) → 확인용 점 3개(실측 오차). 총 약 31초. iPhone 16점 보정은 그대로. [`GameController.WebcamFit.cs`](Assets/Scripts/GameController.WebcamFit.cs)
  - **플레이 방법**: 시선 테스트 또는 `시선 추적 준비`에서 웹캠 `화면 맞춤 (약 30초)` → 점 9개 보기 → 움직이는 점을 고개는 고정하고 눈으로 따라가기 → 점 3개 더 보기 → 완료 문구의 `검증점 실측 오차` 확인.
  - 검증: Unity 컴파일 오류 0, 테스트 5종 exit 0. 미검증: 실기 정확도(예전 16점 대비), 따라보기 속도 체감.
- **[Claude] 2026-11-06 클릭으로 시선 보정 학습 (WebGazer/Tri-Cam 방식)**: 보정이 끝난 뒤 플레이 중 마우스 클릭을 "그 지점을 보고 있었다"는 보정점으로 추가 학습한다. 클릭 직전 0.05~0.45초 시선이 60px 안에 머물렀고 예측 시선과 클릭이 250px 이내일 때만 받아들이며, 클릭 1개는 보정점 0.35개 무게, 최근 30개만 유지. 포인터를 커서 쪽으로 옮기지 않는다. [`GameController.ClickCalibration.cs`](Assets/Scripts/GameController.ClickCalibration.cs), [`GazeMapper.AddClick`](Assets/Scripts/GazeMapper.cs)
  - **플레이 방법**: 웹캠/iPhone 보정 후 스토리나 시선 테스트에서 평소처럼 대사·선택지를 클릭한다. Console의 `[gaze click]` 로그로 학습된 클릭 수와 클릭 위치·당시 시선을 확인.
- **[Claude] 2026-11-06 시선 보정 안정화 (Codex 리뷰 반영)**: (1) 움직이는 점 보정 라벨을 카메라 촬영 시각(CaptureTimestamp) 기준으로 정렬, 고정 지연은 눈 추종분 0.05초만. (2) 클릭 학습은 버튼·선택지 위 클릭만, 라벨은 클릭점과 버튼 중심의 중간, 학습 뒤 검증점+보정점 오차가 5%(또는 3px) 넘게 커지면 되돌림. (3) 웹캠 포인터 고정 반경 30→12px. (4) 정확도 문구에 최악 5%(P95) 오차와 클릭 학습/거절 수 표시. 컴파일·테스트 통과, 실기기 미검증. 남은 일: 모서리·고개 움직임 검증, 설치 모델(MobileOne/MobileNetV2) 비교.
  - **플레이 방법**: 화면 맞춤 후 완료 문구에서 "최악 5%" 값 확인 → 시선 테스트에서 선택지·버튼을 클릭하면 문구의 "클릭 학습 N개(거절 M)"가 바뀐다.
  - 검증: Unity 컴파일 오류 0, 테스트 5종 exit 0. 미검증: 실기에서 정확도 개선 폭, 저장된 iPhone 보정(재학습 대신 오프셋만 보정) 경로.
- **[Codex] 2026-11-05 시선 이탈 끼어들기**: 눈맞춤 추적 중 일반 대사에서 교수 얼굴을 7초 이상 벗어나면 25% 확률로 교수 성격별 한 줄이 끼어든다. 장면 시작 후 최소 90초, 실패 후 60초 쿨다운을 적용하며 판당 최대 1회, 호감도 -1, 저장·이어하기 발동 상태 유지.
  - **플레이 방법**: 보정 완료 후 90초 이상 플레이하고 교수 얼굴이 보이는 일반 대사 중 시선을 얼굴 바깥에 7초 이상 둔다. 에디터/개발 빌드는 F8로 상태를 보고 F9로 즉시 확인한다.
  - 검증: 테스트 5종 exit 0, Unity 컴파일 오류 0, 사용자 실기(웹캠)에서 F9 끼어들기·화난 표정·호감도 -1 팝업·닫은 뒤 원래 대사와 원래 표정 복귀·판당 1회·결과 대사 중 눈맞춤 카운트 확인. 미검증: 실제 7초 이탈 + 25% 확률 자연 발동, 이어하기 후 발동 상태 유지, iPhone 경로.
- **[Claude+Codex] 2026-11-05 교수 눈맞춤 카운터 (엔딩 통계)**: 스토리 내내 시선 추적을 켜 두고, 화면의 교수 얼굴 주변(보이지 않는 타원 영역)을 0.45초 이상 보면 "눈맞춤 1회"로 센다. 상세: [Eye-Contact-Counter.md](docs/AI/Eye-Contact-Counter.md)
  - **플레이 방법**: 타이틀 → 학과·교수 선택 → `시선 추적 준비` 화면에서 웹캠 또는 iPhone 선택 → 웹캠은 `웹캠 시작` 후 `화면 맞춤 (16점)`(1단계 시선 보정 없음), iPhone은 연결 후 16점 보정 → 끝나면 자동으로 스토리 시작. 플레이 중 교수 얼굴을 바라보면 횟수가 쌓이고 엔딩 통계 아래 `눈 마주침 N회`로 표시. 이어하기도 매번 보정 필요, `준비 취소`는 새 게임이면 교수 선택, 이어하기면 타이틀로.
  - **개발 확인**: 에디터/개발 빌드에서 F8 → 판정 타원(분홍 진입·보라 이탈), 시선 점, 타원 위 `눈맞춤 N`, 증가 시 `+1`. Console `[eye contact]` 로그. 플레이어 빌드에는 표시 없음.
  - 호감도·엔딩 분기에는 아직 미반영(`EyeContactCount` 등 읽기 전용 속성만 노출). 웹캠/iPhone이 없으면 스토리를 시작할 수 없음(필수 보정).
  - 같은 묶음: `FitAndCheck`의 `requireVerticalEye`를 iPhone 전용으로 분리(웹캠 화면 맞춤은 false), 인수인계 문서 정정. `눈빛 피하기` 병합 시 `GameController.GazeTest.cs` 충돌은 양쪽 유지로 해결.
  - 검증: 테스트 5종 exit 0, Unity 컴파일 오류 0, 사용자 실기(웹캠) 준비→플레이→카운트 확인. iPhone 경로와 엔딩·이어하기 실기는 미검증.
- **[Claude] 2026-11-05 눈빛 피하기 시선 게임 + 연습 난이도 통일**:
  - [`GameController.GazeEyeDodge.cs`](Assets/Scripts/GameController.GazeEyeDodge.cs)/[`GazeEyeDodgeFx.cs`](Assets/Scripts/GameController.GazeEyeDodgeFx.cs): 심한뫼 교수 두 눈의 빨간 레이저가 시선(파란 점, 기존 `StarGaze` 웹캠/iPhone)을 쫓음. 3번 잡히면 끝, 0.1초당 10점, 점점 빨라짐. 기본/찡그림/기쁨 사진 전환과 대사, 스프라이트 효과·효과음(`Resources/EyeDodge`, `Resources/Audio/EyeDodge`), 게임 중 `Darkness` 음악. `화면 효과 끔` 적용, 실제 시간 기준 점수(1/60초 분할 계산), 시선을 놓치면 플레이·무적 시간·말풍선 함께 멈춤.
  - 타이틀 `눈빛 피하기` 버튼: 시선 추적 테스트를 열고 웹캠을 켠 뒤 보정이 끝나면 자동 시작. 웹캠/iPhone 패널에도 시작 버튼.
  - 회전은 캔버스 좌표 행렬 사용(`RotateAroundPivot`은 창이 1280×900이 아니면 위치가 어긋남).
  - [`GameMenus.cs`](Assets/Scripts/GameMenus.cs) 연습 메뉴: 모든 학과에서 `연습 난이도` 표시(MiniGame.Easy가 모든 미니게임에 적용되므로). `Assets/_Recovery` 삭제 및 `.gitignore` 추가.
  - 검증: 전체 스크립트 컴파일 오류 0, 테스트 5종 exit 0, 스크래치 빌드 캡처(1600×900 레이저 위치, 타이틀→보정→자동 시작, 5FPS 10초→생존 10.00초, 시선 끊김 3초 동안 무적·말풍선 유지, 효과 끔 시 흔들림 0), 16개 학과 연습 메뉴 캡처. 미검증: 실제 웹캠/iPhone 플레이 감각, 소리 균형.
- **[Codex] 2026-11-05 심한뫼 교수 사진 교체**: IMG_6931 기본, IMG_6939 기쁨, IMG_6950 화남 사진을 배경 제거 PNG로 교체하고, IMG_6937 근접 사진을 교수 선택 카드 마우스 오버 전용 `ProfessorKimHoverTransparent`로 연결. 얼굴 위치 표는 심한뫼 4장만 재생성하고 기존 에셋 GUID와 다른 교수 데이터를 보존. Unity 6000.3.23f1 배치 가져오기·스크립트 컴파일 완료(로그 return code 0), 회귀 테스트 5종 및 `git diff --check` 통과. 기존 AiProfessor 경고와 다른 교수 로컬 초상 9장의 얼굴 위치 표 미등록 경고는 남아 있음. 선택 화면 실제 마우스 오버 및 플레이 화면 시각 검증은 미검증.
- **[Claude] 2026-11-05 AI 서버 GPT-6.1 Terra 전환 + 채점·입력 버그 수정**:
  - [`worker.js`](Server/ai-proxy/worker.js): `gpt-6.1-terra`(12초) 우선, 실패·시간 초과·빈 응답·일일 한도(하루 500회, `TERRA_DAILY_LIMIT`) 초과 시 Gemini(22초)로 자동 전환. OpenAI 401/403/429면 10분간 Terra 건너뜀. 응답 헤더 `X-AION-Model`. 배포 완료(버전 2a9046dd, 시크릿 `OPENAI_API_KEY`). 빌린 OpenAI 계정이라 계정 쪽 한도를 못 걸어 일일 횟수가 비용 상한.
  - [`AiProfessor.cs`](Assets/Scripts/AiProfessor.cs) `GradeAnswer`: 판정 값은 허용 단어와 정확히 일치, 선택지는 0~N 정수만 인정(나머지는 형식 오류).
  - [`GameController.cs`](Assets/Scripts/GameController.cs): 단답형 퀴즈에서 AI 질문 칸이 열려 있거나 힌트 대기 중이면 Enter로 정답이 제출되지 않음.
  - [`GameMenus.cs`](Assets/Scripts/GameMenus.cs)/[`GameAiMode.cs`](Assets/Scripts/GameAiMode.cs): 다시 보낸 AI 대답·질문이 대화 기록의 예전 줄을 대체(다음 AI 요청 맥락 오류 수정).
  - 검증: 실제 게임 프롬프트 4장면×9답안 채점 실험 Terra 107/108 정답(중간값 2.7초), 형식 오류 0, Gemini 전환 0. 채점 검사 오프라인 19/19, 대화 기록 반사 테스트 4/4, 전체 스크립트 컴파일 오류 0, 테스트 5종 exit 0, 에디터 실제 채점 2건 확인. 미검증: Play 모드 Enter 동작, Gemini 동일 조건 비교.
- **[Codex] 2026-11-05 브랜치 통합 및 iPhone 진단 인수인계**: 원격 모든 브랜치 및 로컬 카드게임 브랜치의 main 미반영 커밋을 확인해 통합. PROGRESS 충돌은 main의 최신 5개 섹션 구조에 시선 진단 기록을 합치는 방식으로 해결. 상하 자동 진단 UI·요약 로그, 입력→포인터 연속 기록, iPhone 세로 모델의 눈 신호 포함 조건 추가. 상세 실측값과 재현 순서는 [상하 추적 인수인계](docs/AI/iPhone-Gaze-Debug-Handoff.md) 최신 절 참고. stash는 미커밋 보관본이므로 자동 적용하지 않음.
  - 로컬 카드게임 `a5b3a49`의 충돌은 main의 최신 시간 표시(`Real`), 교수 주문 랜덤화/실패 해설을 보존해 해결. Unity Cloud 설정 유지. 통합 후 모든 로컬/원격 브랜치의 main 미반영 커밋 0개 확인. 기존 테스트 5종 모두 exit 0, `git diff --check` 통과. 기존 AiProfessor/CoffeeRecipeSO 경고 있음. Unity 전체 빌드 및 하드웨어 동작은 이번 병합에서 별도 검증하지 않음.
- **플레이어 사진 아바타 및 2인 대화 구도 브랜치 무충돌 병합 (`origin/feature/player-photo-avatar` -> `main`)**:
  - 교수 102종 얼굴 정규화 표 ([`PortraitFaces.cs`](Assets/Scripts/PortraitFaces.cs)), 자동 생성 스크립트([`generate_portrait_faces.py`](Tools/generate_portrait_faces.py)), 에디터 검증기([`PortraitFacesCheck.cs`](Assets/Editor/PortraitFacesCheck.cs)) 통합.
  - 플레이어 몸체 에셋([`PlayerBody.png`](Assets/Resources/PlayerBody.png)) 및 웹캠 사진 촬영·배경 제거·얼굴 합성 시스템([`GameController.PlayerPhoto.cs`](Assets/Scripts/GameController.PlayerPhoto.cs)) 통합.
  - 교수 우측/플레이어 아바타 좌측의 2인 대화 구도([`GameController.Duo.cs`](Assets/Scripts/GameController.Duo.cs)) 연동 완료.
  - 신규 에셋 및 스크립트의 `.meta` 파일 누락 없음 확인.
- **AI 모드 GC 최적화 및 오프라인 Fallback 방어 로직 강화**:
  - [`GameAiMode.cs`](Assets/Scripts/GameAiMode.cs#L227-L255): `OnGUI` 내 `AiField`와 `AiKeyRow`에서 매 프레임 발생하던 `new GUIStyle(NovelUI.Input)` 할당을 제거하고, 정적 캐시 필드(`s_AiFieldStyle`, `s_AiKeyFieldStyle`) 지연 할당 구조로 전환하여 GC 할당을 완전히 차단.
  - [`GameAiMode.cs`](Assets/Scripts/GameAiMode.cs#L72-L82): AI 자유 응답 및 힌트 요청 실패 시 사용자 토스트 알림을 보강하고, 힌트 요청 실패 시 남은 질문 기회를 안전하게 복구.
  - [`GameAiMode.cs`](Assets/Scripts/GameAiMode.cs#L173-L182): 자유 대답 응답 지연/오류 시 즉시 오프라인 기본 선택지로 전환할 수 있는 `선택지로 전환` 버튼을 카드 내에 직관적으로 배치하여 게임 진행 막힘 방지.
  - [`AiProfessor.cs`](Assets/Scripts/AiProfessor.cs#L89-L93): 504 타임아웃, 502/503 서버 일시 장애, `HttpRequestException` 네트워크 단절 예외 발생 시 오프라인 선택지 전환 권장 문구를 명확히 반환하도록 예외 처리 보강.
- **16개 학과 19종 미니게임 및 시선 추적 별잡기 연동 점검**:
  - 19종 미니게임 전체([`MiniGames.cs`](Assets/Scripts/MiniGames.cs#L134-L177))의 비전공자/전공자 난이도(`MiniGame.Easy` 0.75x 감속) 및 연습 모드([`GameMenus.cs`](Assets/Scripts/GameMenus.cs#L488-L516)) 난이도 분기 동작 검증.
  - 웹캠(UnitEye) 및 iPhone UDP 듀얼 지원 시선 추적 별잡기 미니게임([`GameController.GazeStarGame.cs`](Assets/Scripts/GameController.GazeStarGame.cs))의 실시간 드리프트 보정 및 캔버스 좌표계 정상 바인딩 검증.
- **PowerShell 단위 테스트 5종 전수 통과 (Exit 0)**:
  - `Tests/ImpactLabIntegration.Tests.ps1`: 통과 (Exit 0)
  - `Tests/PhysicsExperiencedDialogue.Tests.ps1`: 통과 (Exit 0)
  - `Tests/PhysicsImpactRules.Tests.ps1`: 통과 (Exit 0)
  - `Tests/PhysicsStory.Tests.ps1`: 통과 (Exit 0)
  - `Tests/ProfessorPersonality.Tests.ps1`: 통과 (Exit 0, 16 faculties, 32 personalities, 3524 dialogue calls)

---

## 3. 핵심 결정 사항 (Decisions)
1. **OnGUI 성능 안정성 확보**:
   - `OnGUI` 생명주기 특성상 매 프레임 실행되므로, 스타일 객체 동적 생성을 지양하고 정적 필드 캐싱 패턴을 일관되게 적용합니다.
2. **AI 네트워크 결함 격리 (Graceful Fallback)**:
   - 외부 LLM API나 Cloudflare 릴레이 서버가 타임아웃(504)되거나 오프라인 상태일 때도, 스토리 진행이 중단되지 않고 단 한 번의 클릭으로 로컬 정적 선택지 모드로 자연스럽게 전환될 수 있도록 보장합니다.
3. **공동 작업 안전성 유지**:
   - 팀원들의 취침 시간대 완료 작업(`feature/player-photo-avatar`)을 `main`에 패스트 포워드로 안전하게 병합하고, 충돌 없는 청정 히스토리를 유지합니다.

---

## 4. 결함 및 부채 (Blockers)
- **iPhone 머리 위치 영향 미해결**: 같은 지점을 볼 때 머리 위치에 따라 포인터가 이동한다는 사용자 보고. 눈 신호를 포함하는 후보 제한은 임시 대응이며 정확도 개선을 보장하지 않음. 진단 자체는 보정 계수를 바꾸지 않는다. 보정 추정 오차 294→157→269px는 자세·표본이 달라 개선율로 해석할 수 없음. 최종 30초 추적 및 Unity 전체 컴파일/실기기 검증 필요.
- **웹캠 실기 촬영 검증**: 단위 테스트 및 스크립트 빌드는 모두 검증되었으나, 실제 물리 웹캠 디바이스를 통한 얼굴 자동 크롭 및 텍스처 합성은 Unity Editor 플레이 모드에서 확인이 필요합니다.
- **외부 패키지 의존 스크립트**: UnitEye 및 외부 하드웨어 브리지 스크립트는 독자 CLI 빌드 시 제외되어 있으므로, Editor 환경에서 최종 패키징 시 의존성을 점검해야 합니다.

---

## 5. 다음 진행 작업 (Next Steps)
- 눈맞춤 카운터: iPhone 경로·엔딩 표시·이어하기 실기 확인. 판정 상수(`GameController.EyeContact.cs`)는 F8로 보며 조정. 호감도 반영 여부는 추후 결정.
- 눈빛 피하기: 실제 웹캠/iPhone으로 난이도(속도·판정 반경)와 소리 균형 확인. `GameController.cs` 별 회전과 `GameEffects.cs`도 같은 `RotateAroundPivot` 문제 여부 확인.
- AI 모드: 에디터에서 단답형 퀴즈 힌트 대기 중 Enter 동작 확인. 설정에 내 Gemini 키가 있으면 서버(Terra)를 거치지 않으므로 Terra 확인 시 키를 지울 것.
- iPhone에서 같은 중앙 목표를 보며 고개 회전/평행이동/거리 변화를 분리 측정. `[iPhone gaze trace]`로 원시 예측·포인터·blink·fresh를 비교하고 현 인터페이스에 없는 머리 위치/거리 정보 도입 여부 판단.
- Unity Editor 상에서 플레이어 웹캠 아바타 촬영 기능 및 2인 구도 실기 점검.
- 팀원 취침 복귀 후 변경 사항(아바타 브랜치 머지, AI GC 최적화, Fallback 강화) 공유.
- 16개 학과별 스토리 미니게임 및 엔딩 분기 통합 밸런스 모니터링.
