# AI-ON 작업 규칙 (모든 에이전트 공통)

응답과 문서는 한국어로 작성한다. Unity `6000.3.23f1` 프로젝트. 이 파일이 규칙과 도구 사용법의 정본이다. Claude만 따르는 코디네이터 규칙은 `CLAUDE.md`에 있다.

## 1. 문서 정책
- 마크다운은 세 곳에만 쓴다. 규칙·도구 사용법은 이 파일, 현재 상태는 `PROGRESS.md`, 진행 중인 계획은 `docs/plans/`.
- 예외: `README.md`(저장소 소개), `CLAUDE.md`, 라이선스·고지(`AUDIO-CREDITS.md`, `Packages/*/LICENSE.md` 등), 제출용 보고서(`docs/reports/`, `docs/report/`, `docs/voice/`), 에셋 폴더 안내(`Assets/StreamingAssets/SignData/README.md`).
- 새 도구는 7절에 3~8줄로 사용법을 추가한다. 별도 README나 사용법 문서를 만들지 않는다.
- 인수인계·리뷰·조사 결과는 새 파일 대신 `PROGRESS.md` 항목이나 해당 계획 문서에 적는다. 끝난 계획 문서는 지운다(git 기록에 남는다).

## 2. 시작할 때
- `PROGRESS.md`를 먼저 읽고 Done / Blockers / Next Steps를 확인한다. 시선 추적 작업이면 `docs/plans/iPhone-Gaze-Handoff.md` 최신 절도 읽는다.
- `bash Tools/agent-sync.sh status`(PowerShell만 쓰면 `pwsh -NoProfile -File Tools/agent-sync.ps1 status`)로 새 커밋과 다른 사람의 선점을 확인한다. Claude는 SessionStart 훅이 대신 실행한다.
- `NOT SET UP` 경고가 보이면 사용자에게 에이전트 ID를 받아 `Tools/setup-git-unity.ps1` 실행을 요청하고, 설정이 끝날 때까지 편집하지 않는다(5절).
- `git pull`/병합으로 팀원의 커밋을 받았으면 `PROGRESS.md` Done에 새로 생긴 항목을 읽고 사용자에게 **새 기능 요약 + 플레이 방법(어디서 시작해 무엇을 누르는지) + 아직 미검증인 부분**을 짧게 알린다. 항목에 `플레이 방법`이 있으면 그대로 전달한다. Orca 워커로 실행 중이면 생략한다.

## 3. 실행 방식
### Orca 워커로 실행될 때 (프롬프트에 Task / Dispatch ID가 있음)
- 명세에 적힌 범위와 수정 허용 파일만 바꾼다. 범위 밖 수정이 필요하거나 명세가 모호·모순이면 추측하지 말고 `orca orchestration ask`로 묻고 턴을 끝낸다.
- 커밋·푸시·브랜치 생성·stash 조작·선점(`claim`)은 명세에 있을 때만 한다. `PROGRESS.md`와 계획 문서도 명세에 있을 때만 고친다. Unity는 실행하지 않는다.
- 끝나면 `worker_done`으로 보고한다: (1) 변경 파일과 요지 (2) 실행한 검증 명령과 실제 결과(exit code, 실패 출력) (3) 검증하지 못한 항목과 남은 위험. 실패·건너뜀·일부 통과는 그대로 적는다.

### 단독으로 실행될 때
- 커밋·푸시는 사용자가 요청할 때만 한다.
- 푸시 요청을 받으면 먼저 `git fetch`로 원격을 확인한다. 팀원의 새 커밋이 같은 파일·기능을 건드렸으면 사용자에게 알리고 방법을 묻는다. 겹치지 않으면 최신 `origin/main`에 합치고 4절 검증을 다시 통과시킨 뒤 푸시한다.
- 같은 폴더를 다른 세션이 쓸 수 있다. 시작 전에 `git status`로 미커밋 변경을 확인하고, 내 작업이 아닌 변경과 stash는 적용·되돌리기·덮어쓰기를 하지 않는다.
- `PROGRESS.md`는 작업을 마치거나 푸시하기 직전에 한 번 갱신한다(Done / Next Steps). 기능을 추가했으면 팀원이 바로 해 볼 수 있게 `플레이 방법` 한 줄을 함께 적는다.

## 4. 검증
- 회귀 테스트: `Tests/*.ps1` 전부(2026-11-26 기준 10종). 각각 `powershell -NoProfile -ExecutionPolicy Bypass -File <파일>`로 실행해 모두 exit 0이어야 한다. 변경 후 `git diff --check`도 통과해야 한다.
- AiProfessor/CoffeeRecipeSO 경고는 기존 경고다. 새 경고와 구분해서 보고한다.
- PS 테스트 통과는 Unity 전체 컴파일이나 실기기 동작을 보장하지 않는다. 검증하지 않은 항목은 "미검증"이라고 적는다. 실패한 검사를 약하게 바꾸지 말고 실제 동작부터 확인한다.
- 시선 추적 수치(보정 오차 px)는 자세·표본이 다르면 전후 개선율로 해석하지 않는다.
- Unity 에디터 검사 메뉴: `Tools → Checks → Bug Hunt Guidance`, `Minigame Failure Lessons`, `Gaze Lab`(Play 모드를 끈 상태).

## 5. 협업 (agent-sync, 모든 PC가 `main`에서 동시 작업)
**처음 한 번 (PC마다, 저장소 폴더마다)**
```powershell
git pull
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-git-unity.ps1 -AgentId <이름>-<도구>-<모델>
```
- ID 예: `dohun-claude-opus55`, `taehwan-codex-sol`(영문·숫자·`.`·`_`·`-`). 이 폴더의 `.git` 설정만 바꾼다: 커밋·푸시 훅, 에이전트 ID, `pull.rebase=true`, Unity Smart Merge(`git mergetool --tool=unityyamlmerge`).
- Unity가 기본 위치(`C:\Program Files\Unity\Hub\Editor\6000.3.23f1`)가 아니면 `-EditorPath "<경로>\Editor\Unity.exe"`를 준다.
- 한 폴더에서 Claude와 Codex를 함께 쓰면 환경 변수 `AION_AGENT_ID`로 ID를 따로 준다(Claude `~/.claude/settings.json`의 `env`, Codex `~/.codex/config.toml`의 `[shell_environment_policy.set]`).
- 유료 UI 팩은 Git에 없다. 팀 안에서 받은 파일을 `Assets/Resources/UI/`에 넣는다. 없어도 단순 도형으로 실행된다.

**매번 작업할 때**
```bash
bash Tools/agent-sync.sh status
bash Tools/agent-sync.sh claim "예체능학부 스토리" Assets/Scripts/Stories/ArtsSports/
bash Tools/agent-sync.sh finish "Add arts faculty story" Assets/Scripts/Stories/ArtsSports/
```
- `claim`은 경로를 선점하고 바로 푸시한다. 폴더는 끝에 `/`. 학과·기능 단위로 크게 잡는다. `finish`는 커밋·선점 해제·푸시를 한 번에 한다. 둘 다 `main`에서만 동작한다.
- 접을 때 `release`, 오래 걸리면 `heartbeat`(8시간 갱신이 없으면 STALE). 미푸시 커밋이 있으면 `claim`·`release`·`heartbeat`가 거절된다.
- Unity Play Mode·에디터 검사 전후로 `editor acquire "<목적>"` / `editor release`(로컬 사용권, 커밋 없음).
- 선점이 겹치면 `bash Tools/agent-sync.sh send <상대 ID> "<메시지>"`. 상대는 다음 `status`에서 본다. 타이머로 반복 확인하지 않는다.
- 도구 자체 확인: `bash Tools/test_agent_sync.sh`(exit 0 = 통과).

**자동으로 막히는 것** (우회 변수는 사람이 동의했을 때만 쓰고, 사용 사실을 커밋 메시지나 `PROGRESS.md`에 적는다)
- 커밋: 다른 사람이 선점한 경로(`ALLOW_FOREIGN_PATHS=1`), 유료 UI 팩·카톡 로그(`ALLOW_PRIVATE_FILES=1`), `Assets/`와 `.meta` 짝 불일치(`ALLOW_MISSING_META=1`), LFS가 아닌 10MB 초과 파일(`ALLOW_LARGE_NON_LFS=1`).
- 푸시(`.cs` 변경 시): 작업 폴더 `.cs`가 푸시할 커밋과 다름, `Tests/*.ps1` 실패(`AION_SKIP_TESTS=1`), Unity 컴파일 실패(`AION_SKIP_COMPILE_CHECK=1`). Unity·PowerShell 7을 못 찾거나 `Library/`가 없으면 경고만 하고 통과하므로 직접 확인한다.
- Claude 편집: 다른 사람이 선점한 파일.

**팀 규칙**
- 커밋할 경로를 직접 지정한다. `git add -A`/`.`/`-f`와 `main` 강제 푸시는 금지. 검사를 통과한 단위마다 작게 커밋·푸시한다.
- 병합이 어려운 파일(`.unity`, `.prefab`, `.controller`, `ProjectSettings/*`, `Packages/manifest.json`, `packages-lock.json`)은 파일 단위로 선점하고 끝나면 바로 푸시·해제한다.
- 내 선점 밖 충돌은 직접 풀지 않는다. `git rebase --abort` 후 `send`로 알린다.
- 큰 바이너리(음성·이미지·영상·모델)는 커밋 전에 팀에 확인하고, 계속 둘 파일이면 `.gitattributes`의 LFS에 먼저 등록한다.
- `PROGRESS.md`는 선점하지 않고 `merge=union`이다. 기존 줄을 고치기보다 덧붙이고, 항목은 5줄·줄당 200자 안쪽, 긴 로그는 경로만 남긴다.

## 6. 코드 규칙
### 6.1 공통
- 씬 하나(`Assets/Scenes/Main.unity`)의 `GameController`(partial class 여러 파일)가 `OnGUI` IMGUI로 모든 화면을 그린다. 논리 캔버스는 1280×900이며 `Screen.width`를 직접 쓰지 않는다.
- `OnGUI`에서 매 프레임 `new GUIStyle` 등을 할당하지 않는다. 정적 필드 캐싱을 쓴다.
- AI/네트워크 실패 시 스토리가 막히지 않도록 오프라인 fallback을 유지한다.
- Unity 에셋을 추가하면 `.meta`도 함께 커밋한다. 리소스는 `Assets/Resources/`에서 `Resources.Load` 키(파일 이름)로 읽는다.

### 6.2 미니게임 (`MiniGame` 상속 C# 클래스 1개)
- 기본 클래스·테마는 `Assets/Scripts/MiniGames.cs`(`MiniGame`, `MiniGameTheme`, `MiniGames.Create`). 기존 게임(`CardMatch`, `LoopTiming`, `CoffeeOrder`, `BugHunt` 등)을 예시로 본다. 클래스 이름과 생성자 형태(`: base(theme)`)는 `MiniGames.Create`와 맞춘다.
- `MonoBehaviour`·씬·프리팹·Canvas·UI Toolkit 금지. IMGUI(`GUI.Label/Button/DrawTexture`)만 쓰고 `Draw()`는 매 프레임 `OnGUI`에서 불린다. 호스트는 `Render`를 호출한다.
- 그릴 수 있는 영역은 x 100~1180, y 115~770. 호스트가 패널 `(60,40,1160,830)`, 제목(y 58~102), 결과 버튼 `(980,790,210,50)`, 설정 아이콘 `(1228,10)`·연습 닫기 `(1228,62)`를 그린다.
- 시간은 `MiniGame.Now`만 쓴다(설정 창이 열리면 멈춤). `Time.*`, `Update`, 코루틴 금지. 입력은 `GUI.Button`이나 `Event.current`.
- 외부 에셋 없이 `Box(rect, color)`나 `NovelUI` 도우미(`Fill`, `Card`, `Heart`, `Pill`, `Button`)로 그린다. 분위기는 귀엽고 청춘물: 분홍 `#ff8fb1`, 진분홍 `#e0457b`, 크림 `#fffaf5`, 연분홍 `#ffe3ec`, 하늘 `#8ec9ff`, 민트 `#8fdcc0`, 레몬 `#ffe08a`.
- 30초~1분 안에 반드시 끝난다. 끝나면 `Done=true`, `Delta` 0~2, `Summary` 한 줄, `Replies`는 `Theme.Replies ?? new[]{...}`(교수 2명×점수 3개), `CountsForStat`(기본 `Delta>0`). `Title`은 덮어쓰지 않는다. 끝난 뒤에도 결과를 그리고 입력은 받지 않는다.
- 학과별 글자는 `Theme`에서 읽고 null이면 기본(컴퓨터공학) 글자를 쓴다. 게임 안에 학과 용어를 박지 않는다. 새 글자가 필요하면 `MiniGameTheme`에 필드를 추가한다.
- 교수 0번은 엄격한 원칙파("~합니다/~하세요"), 1번은 유쾌한 소통파("~해요/~죠!").
- 전공 경험 여부는 `MiniGames.Create(..., experienced:)`로 들어온다(버그 사냥·멀티미터는 입문자에게 설명 추가). 실패하면 `ExplainFailure(원인, 해결)`로 복습을 띄우고, 복습 중(`Reviewing`) 시계와 결과 버튼은 호스트가 멈춘다.
- 확인 경로: 타이틀 → 미니게임 → 학과 탭 → 게임(교수도 선택 가능). 모든 학과 탭에서 글자가 바뀌는지 본다.

### 6.3 학과 스토리 (`Assets/Scripts/Stories/<학부>/○○Story.cs`)
- 틀은 `Assets/Scripts/Stories/Story.cs`(`Story`, `Slot`, `Quiz`, `Professor`, `Stories.Colleges`, 엔딩 판정). 완성 예시는 `Stories/Engineering/SmartSystemsStory.cs`. 학부 폴더는 `Engineering`, `Natural`, `HumanSocial`, `ArtsSports`.
- 새 학과는 `Stories.All` 배열의 **맨 끝에만** 추가한다. 순서가 바뀌면 기존 저장과 엔딩 도감이 다른 학과로 연결된다.
- 6챕터 × 선택 자리 2개 = 12자리(`(챕터-1)×2+단계`). 단계 0은 `Dialogue[챕터]` 뒤, 단계 1은 결과 대사와 `FollowUp[챕터]` 뒤.
- 12자리 = 선택지 6 · 문제 3 · 미니게임 3(일반 학과는 전용 1 + 공용 카드/타이밍/커피 중 2, 물리학과는 `ImpactLab` 3). 배치는 다른 학과와 다르게 한다.
- 선택지·문제 직전 마지막 줄에는 교수가 있어야 한다. 교수가 없는 줄 번호는 `HiddenDialogue`/`HiddenFollowUp`에 넣는다. 대사 한 줄 60자, 선택지 30자 안팎.
- 형식: `Dialogue`는 `"화자|대사"`, 교수는 `"교수|엄격|유쾌"`, 단톡방은 `"신입생 단톡방|a / b / c"`. `FollowUp`은 나레이션 문장 또는 `동기:`, `나:`, `선배:`, `사장님:`, `교수님: 엄격|유쾌`.
- `ExperiencedDialogue`/`ExperiencedFollowUp`을 쓰면 챕터별 줄 수를 기본 대사와 같게 맞춘다(저장된 줄 번호 유지).
- 스탯 글자 `q` 질문, `t` 협업, `i` 관심, `s` 혼자 해결. 12자리 전체에서 각 스탯이 3곳 이상 오르고, 음수 점수가 있는 선택지가 2곳 이상이어야 한다.
- 선택지: `Options` 2~3개, `Scores[교수][선택]` -2~+2, `Stats`, `Replies[교수][선택]`. AI 모드는 대답을 태도로 판정해 가장 비슷한 선택지 점수를 쓰므로 선택지의 태도가 한눈에 보이게 쓰고, 장면은 플레이어가 답할 수 있는 질문으로 끝낸다.
- 문제: 경험자 `Major`(입력형, `Accepted`는 소문자·공백 없이 모든 표기), 입문자 `Minor`(객관식, `Answer` 위치를 문제마다 바꿈). 정답 +2, 오답 감점 없음, 오답 반응에 정답과 이유. `Prompt`만 읽어도 무엇을 묻는지 알 수 있게 쓴다.
- 미니게임 자리: `Game = new MiniGameTheme{ Kind, Title, Place, Note("챕터 N · 설명"), Pairs(카드 6쌍), Replies }`. 커피 첫 잔은 교수 것(0번 아메리카노, 1번 바닐라 라떼).
- 엔딩 판정(위부터): 1 호감도≤0 / 8 호감도≥10·q,t,i 각 2↑ / 7 호감도≥7·i 2↑ / 6 호감도≥6·t 2↑·q 1↑ / 5 q 3↑ / 4 s 3↑ / 3 t 2↑ / 2 그 밖. `EndingNames` 8개(12자), `EndingText` 8개(정확히 한 줄이 `교수님:`), `EndingLines[교수][엔딩]` 2×8. 엔딩 조건은 플레이어에게 공유하지 않는다.
- 이야기 전개·배경·교수 이름·사진 키는 기존 학과와 겹치지 않게 한다(기존 `*Story.cs`의 `Background`·`Professors`로 확인). `Mood`는 `Campus`/`Study`/`Crisis`, `chapter == 7`은 엔딩 화면.

### 6.4 이미지 리소스
- 교수 사진 `Image = "ProfessorPark"`이면 `ProfessorParkTransparent.png`, `...HappyTransparent.png`, `...FrownTransparent.png` 세 장. 투명 PNG 1698×926, 상반신 가운데, 머리 끝 위에서 60px, 690px쯤부터 아래로 흐림. 세 표정의 크기·위치를 맞춘다. 표정 아트가 없으면 같은 그림을 세 이름으로 둔다.
- 배경은 1698×926 PNG, 영문 이름으로 `Assets/Resources/`에. 얼굴 크롭 위치표는 `Tools/generate_portrait_faces.py`로 만든다.

## 7. 도구 사용법
**수어 지문자** (`Assets/Scripts/Sign/`, 계획 `docs/plans/Sign-Fingerspelling-Plan.md`)
1. Play → `Tools → Sign → Recorder`. 사람 ID(가명), 세션 ID, 글자 목록(예 `주먹,보자기,검지,_other`) 입력 → 손 추적 시작 → 글자 선택 후 녹화 시작/종료(창 포커스 시 Space 누르는 동안). 글자마다 3회 이상, `_other`도 녹화. 녹화 전 참여자 동의를 받는다.
2. `Tools/SignData/raw/<사람>/<세션>.csv`(Git 제외, 관절 좌표만)를 `Assets/StreamingAssets/SignData/`로 복사한다.
3. Play → 미니게임 → 하이라이트 → 수어 지문자 → 연습 시작. 0.6초 유지하면 정답, 도전은 글자당 8초. 거리 상한은 데이터에서 자동 계산되며 디버그 보기에 나온다.
- 테스트: `Tests/HandProbe.Tests.ps1`, `SignFingerspelling.Tests.ps1`, `SignGame.Tests.ps1`. Python 전처리: `Tools/SignTrain/`.

**Gaze Lab** (시선 정확도 측정): Play → 시선 추적 테스트 → 장치 연결·보정 → 오른쪽 위 `움직이는 공 · 로그` → 경로(가로/세로/타원/8자/9점)·주기·시간 선택 → 실험 이름 입력 → 3초 후 기록 시작 → 노란 공만 본다. 로그는 `Logs/GazeLab/`(CSV, JSON, `latest.json`). 분석: `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Analyze-GazeLab.ps1 [-Iterations 10] [-Csv <파일> -WindowSeconds 10000]`. 같은 장치·자세·보정·경로로 baseline과 비교하고, `metrics_available=false`는 오차 0이 아니다.

**iPhone 시선 추적 연결** (VTube Studio iOS, 장애 진단은 `docs/plans/iPhone-Gaze-Handoff.md`)
- Wi-Fi UDP: iPhone VTube Studio에서 `3rd Party PC Clients` 켜기 → Unity 시선 테스트 → iPhone 추적 → Wi-Fi UDP에 **iPhone의 Wi-Fi IPv4**와 앱 포트(보통 21412) 입력 → 연결 → 화면 보정. PC 수신 포트는 UDP 11125. 같은 로컬망이어야 하며 게스트 Wi-Fi·AP 격리·VPN·방화벽 Block 규칙을 확인한다(방화벽 전체를 끄지 않는다).
- USB: Apple Devices(또는 iTunes)와 VTube Studio PC 앱 설치 → iPhone에서 Connect USB 먼저, PC 앱에서도 Connect USB → PC 앱 `Allow Plugin API access`(포트 보통 8001) → Unity에서 USB · VTube Studio 연결 → PC 앱의 `AI-ON Gaze Test` 접근 허용. USB 경로는 실기 미검증.
- 보정과 플레이는 같은 모니터·창 크기·거리에서 한다. VTube Studio 무료 버전은 PC 스트리밍이 5분 뒤 끊길 수 있다.

**교수 목소리 재생** (`Professor.Voice`, 현재 심한뫼 `ShimHanmoe`만): 음성은 `Assets/Resources/Voice/<Voice>/<해시>.ogg`, 대사 목록은 같은 폴더 `lines.txt`. 대사를 고친 뒤:
```powershell
.\Tools\Voice\export_lines.ps1 -Voice ShimHanmoe
<voice-clone 경로>\cosyvoice\.venv\Scripts\python.exe Tools\Voice\generate.py Tools\Voice\ShimHanmoe.lines.txt Assets\Resources\Voice\ShimHanmoe
```
바뀐 대사만 만들고 빠진 파일은 지운다(`--force`는 전부). 읽는 법은 `generate.py`의 `SAY` 표. Unity로 가져온 뒤 `.ogg.meta`도 커밋한다.

**VoiceTTS** (`VoiceTTS/`, 내 목소리 TTS 실험, NVIDIA GPU): 결과 근거는 `VoiceTTS/결과보고서.txt`(1위 Fun-CosyVoice3 + `pron3` 참조). 모델·파이썬 환경(약 15GB)은 저장소 밖이다. 짧은 경로(예 `C:\Users\<이름>\voice-clone`)에 두고, OmniVoice용 Python 3.13 `.venv`(torch cu128 + `omnivoice imageio-ffmpeg soundfile speechbrain uv`)와 CosyVoice용 Python 3.10 `cosyvoice\.venv`(`setup\req-win.txt`, `override.txt`, `build-c.txt`로 설치)를 만들고 `FunAudioLLM/CosyVoice2-0.5B`, `Fun-CosyVoice3-0.5B-2512`를 `cosyvoice/pretrained_models/`에 받는다. 실행: `.venv\Scripts\python.exe tts.py "문장" [--model v3|omni|v2|all] [--file 대본.txt]` → `output/<날짜_시간>/`. 본인 또는 동의한 목소리만 쓴다. 로컬 Qwen3-TTS 시험 계획은 `docs/plans/Qwen3-TTS-Local-Setup.md`.

**AI 모드 서버** (`Server/ai-proxy/`, Cloudflare Worker): 게임 → `https://aion-ai-proxy.dugeun-campus.workers.dev`(헤더 `X-AION-Client: dugeun-campus`) → OpenAI `gpt-6.1-terra`(12초, 하루 500회 `TERRA_DAILY_LIMIT`) → 실패 시 Gemini `gemini-3.1-flash-lite`(22초). 응답 모델은 헤더 `X-AION-Model`. 키는 Cloudflare 비밀 설정에만 있고 저장소·빌드에 넣지 않는다. 배포: `cd Server/ai-proxy` → `npx wrangler login` → `npx wrangler secret put GEMINI_API_KEY` / `OPENAI_API_KEY` → `npx wrangler deploy`(주소는 `AiProfessor.ProxyEndpoint`). 설정에 개인 Gemini 키가 있으면 서버를 거치지 않는다.

## 8. 환경 주의
- 방화벽 규칙, 네트워크 설정 같은 PC 설정은 Git에 포함되지 않는다. 다른 PC에 그대로 적용된다고 가정하지 않는다.
- 학교/관리 PC의 보안 정책을 임의로 우회하지 않는다.
