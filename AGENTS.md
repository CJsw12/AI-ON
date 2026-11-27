# AI-ON 작업 규칙 (모든 에이전트 공통)

한국어로 답하고 쓴다. Unity `6000.3.23f1`. 이 파일이 규칙·도구 사용법의 정본이고, Claude 전용 코디네이터 규칙만 `CLAUDE.md`에 있다.

## 1. 문서 정책
- 마크다운은 이 파일(규칙·도구), `PROGRESS.md`(현재 상태), `docs/plans/`(진행 중 계획)에만 쓴다. 예외: `README.md`, `CLAUDE.md`, 라이선스·고지, 제출용 보고서(`docs/reports/`, `docs/report/`, `docs/voice/`), `Assets/StreamingAssets/SignData/README.md`.
- 새 도구는 7절에 3~8줄로 추가한다. 인수인계·리뷰·조사는 `PROGRESS.md`나 해당 계획 문서에 적고, 끝난 계획은 지운다(git 기록에 남음).

## 2. 시작할 때
- `PROGRESS.md`의 Done / Blockers / Next Steps를 읽는다. 시선 추적 작업이면 `docs/plans/iPhone-Gaze-Handoff.md` 최신 절도 읽는다.
- `bash Tools/agent-sync.sh status`로 새 커밋과 선점을 확인한다(Claude는 SessionStart 훅이 실행). `NOT SET UP`이면 사용자에게 에이전트 ID를 받아 `Tools/setup-git-unity.ps1` 실행을 요청하고, 끝날 때까지 편집하지 않는다.
- 팀원 커밋을 받았으면 Done의 새 항목으로 **새 기능 요약 + 플레이 방법 + 미검증 부분**을 사용자에게 짧게 알린다(Orca 워커면 생략).

## 3. 실행 방식
**Orca 워커** (프롬프트에 Task / Dispatch ID): 명세의 범위와 허용 파일만 바꾼다. 범위 밖·모호·모순이면 추측하지 말고 `orca orchestration ask` 후 턴을 끝낸다. 커밋·푸시·브랜치·stash·선점·`PROGRESS.md`·계획 문서 수정은 명세에 있을 때만, Unity는 실행하지 않는다. 끝나면 `worker_done`으로 (1) 변경 파일·요지 (2) 검증 명령과 실제 결과(exit code, 실패 출력) (3) 미검증 항목·위험을 보고한다. 실패·건너뜀·부분 통과는 그대로 적는다.

**단독 실행**
- 커밋·푸시는 사용자가 요청할 때만. 푸시 전 `git fetch`로 원격을 보고, 팀원 커밋이 같은 파일·기능을 건드렸으면 사용자에게 묻는다. 아니면 최신 `origin/main`에 합치고 4절 검증을 다시 통과시킨 뒤 푸시한다.
- 시작 전 `git status`를 본다. 내 작업이 아닌 미커밋 변경과 stash는 적용·되돌리기·덮어쓰기를 하지 않는다.
- `PROGRESS.md`는 작업을 마치거나 푸시하기 직전에 한 번 갱신하고, 기능을 추가했으면 `플레이 방법` 한 줄을 적는다.

## 4. 검증
- `Tests/*.ps1` 전부(현재 15종)를 `powershell -NoProfile -ExecutionPolicy Bypass -File <파일>`로 실행해 모두 exit 0, 그리고 `git diff --check` 통과.
- `.cs`를 바꿨고 에디터가 열려 있으면 `Invoke-UnityRecompile`(7절)로 컴파일 오류 0을 확인한다. AiProfessor/CoffeeRecipeSO 경고는 기존 경고다.
- PS 테스트 통과는 Unity 컴파일·실기기 동작을 보장하지 않는다. 확인하지 않은 것은 "미검증"으로 적고, 실패한 검사를 약하게 바꾸지 않는다.
- 시선 추적 오차(px)는 자세·표본이 다르면 전후 개선율로 해석하지 않는다.
- 에디터 검사 메뉴(Play 끈 상태): `Tools → Checks → Bug Hunt Guidance`, `Minigame Failure Lessons`, `Gaze Lab`.

## 5. 협업 (agent-sync: 모든 PC가 `main`에서 동시 작업)
- 처음 한 번(PC·폴더마다): `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-git-unity.ps1 -AgentId <이름>-<도구>-<모델>`. 훅·ID·`pull.rebase`·Unity Smart Merge를 이 폴더 `.git`에 설정하고 개발 도구를 설치한다(7절, `-SkipTools`로 생략). Unity가 기본 위치가 아니면 `-EditorPath`. 한 폴더에서 여러 도구를 쓰면 환경 변수 `AION_AGENT_ID`로 ID를 나눈다.
- 매번: `status` → `claim "<작업>" <경로>...`(폴더는 끝에 `/`, 학과·기능 단위로 크게) → 작업 → `finish "<메시지>" <경로>...`(커밋·해제·푸시). 접으면 `release`, 오래 걸리면 `heartbeat`(8시간 무갱신은 STALE). 미푸시 커밋이 있으면 거절된다. 겹치면 `send <상대 ID> "<메시지>"`, 타이머로 반복 확인하지 않는다. Play Mode·에디터 검사 전후로 `editor acquire "<목적>"` / `editor release`.
- 훅이 막는 것: 남이 선점한 경로, 유료 UI 팩(`Assets/Resources/UI/`, Git 밖)·카톡 로그, `.meta` 짝 불일치, LFS 밖 10MB 초과, 푸시 시 테스트·Unity 컴파일 실패. 우회 변수(`ALLOW_*`, `AION_SKIP_*`)는 사람이 동의했을 때만 쓰고 커밋 메시지나 `PROGRESS.md`에 적는다. 훅은 pwsh·Unity·`Library/`가 없으면 경고만 하고 통과하므로 직접 확인한다.
- 커밋은 경로를 지정한다(`git add -A`/`.`/`-f`, `main` 강제 푸시 금지). 검사를 통과한 단위마다 작게 커밋·푸시한다.
- 병합이 어려운 파일(`.unity`, `.prefab`, `.controller`, `ProjectSettings/*`, `Packages/manifest.json`, `packages-lock.json`)은 파일 단위로 선점하고 바로 푸시·해제한다. 내 선점 밖 충돌은 풀지 말고 `git rebase --abort` 후 `send`.
- 큰 바이너리는 커밋 전에 팀에 확인하고, 계속 둘 파일이면 `.gitattributes` LFS에 먼저 등록한다.
- `PROGRESS.md`는 선점하지 않으며 `merge=union`이다. 덧붙여 쓰고, 항목 5줄·줄당 200자 안쪽, 긴 로그는 경로만.

## 6. 코드 규칙
### 6.1 공통
- `Assets/Scenes/Main.unity` 하나의 `GameController`(partial 여러 파일)가 `OnGUI` IMGUI로 모든 화면을 그린다. 논리 캔버스 1280×900, `Screen.width` 직접 사용 금지.
- `OnGUI`에서 매 프레임 `new GUIStyle` 등 할당 금지(정적 캐싱). AI/네트워크 실패 시 오프라인 fallback 유지.
- 에셋을 추가하면 `.meta`도 커밋한다. 리소스는 `Assets/Resources/`에서 `Resources.Load` 키(파일 이름)로 읽는다.

### 6.2 미니게임 (`MiniGame` 상속 클래스 1개)
- 기준: `Assets/Scripts/MiniGames.cs`(`MiniGame`, `MiniGameTheme`, `MiniGames.Create`), 예시 `CardMatch`·`LoopTiming`·`CoffeeOrder`·`BugHunt`. 클래스 이름·생성자(`: base(theme)`)는 `MiniGames.Create`와 맞춘다.
- `MonoBehaviour`·씬·프리팹·Canvas·UI Toolkit 금지, IMGUI만. 그릴 영역 x 100~1180, y 115~770(호스트가 패널 `(60,40,1160,830)`, 제목 y 58~102, 결과 버튼 `(980,790,210,50)`, 아이콘 `(1228,10)`·`(1228,62)`을 그림).
- 시간은 `MiniGame.Now`만(설정 창이 열리면 멈춤). `Time.*`·`Update`·코루틴 금지. 입력은 `GUI.Button`/`Event.current`. 외부 에셋 없이 `Box`·`NovelUI`(`Fill`, `Card`, `Heart`, `Pill`, `Button`). 색: `#ff8fb1` `#e0457b` `#fffaf5` `#ffe3ec` `#8ec9ff` `#8fdcc0` `#ffe08a`.
- 30초~1분 안에 반드시 끝나고 `Done=true`, `Delta` 0~2, `Summary` 한 줄, `Replies = Theme.Replies ?? new[]{...}`(교수 2×점수 3), `CountsForStat`(기본 `Delta>0`)을 채운다. `Title`은 덮어쓰지 않는다. 끝난 뒤엔 결과만 그리고 입력을 받지 않는다.
- 학과별 글자는 `Theme`에서 읽고 null이면 컴퓨터공학 기본값. 새 글자는 `MiniGameTheme`에 필드 추가. 교수 0번 엄격("~합니다"), 1번 유쾌("~해요/~죠!").
- 전공 경험은 `MiniGames.Create(..., experienced:)`. 실패 복습은 `ExplainFailure(원인, 해결)`. 확인: 타이틀 → 미니게임 → 학과 탭 → 게임(모든 학과 탭에서 글자 확인).

### 6.3 학과 스토리 (`Assets/Scripts/Stories/<학부>/○○Story.cs`)
- 틀 `Stories/Story.cs`, 예시 `Stories/Engineering/SmartSystemsStory.cs`. 학부 폴더 `Engineering`/`Natural`/`HumanSocial`/`ArtsSports`. 새 학과는 `Stories.All` **맨 끝에만** 추가(순서가 바뀌면 저장·도감이 어긋남).
- 6챕터×2 = 12자리(`(챕터-1)×2+단계`; 단계 0은 `Dialogue` 뒤, 1은 결과 대사와 `FollowUp` 뒤). 구성: 선택지 6·문제 3·미니게임 3(일반 학과 전용 1+공용 2, 물리학과 `ImpactLab` 3), 배치는 다른 학과와 다르게.
- 선택지·문제 직전 줄에 교수가 있어야 한다. 교수 없는 줄은 `HiddenDialogue`/`HiddenFollowUp`. 대사 60자, 선택지 30자 안팎. `Experienced*` 대사는 챕터별 줄 수를 기본과 같게.
- 형식: `"화자|대사"`, `"교수|엄격|유쾌"`, `"신입생 단톡방|a / b / c"`. `FollowUp`은 나레이션 또는 `동기:`/`나:`/`선배:`/`사장님:`/`교수님: 엄격|유쾌`.
- 스탯 `q` 질문·`t` 협업·`i` 관심·`s` 혼자: 각 3곳 이상, 음수 점수 선택지 2곳 이상. 선택지 `Options` 2~3, `Scores[교수][선택]` -2~+2, `Stats`, `Replies`. AI 모드가 태도로 판정하므로 선택지 태도를 분명히, 장면은 답할 수 있는 질문으로 끝낸다.
- 문제: `Major` 입력형(`Accepted` 소문자·공백 없이 모든 표기), `Minor` 객관식(`Answer` 위치를 바꿈). 정답 +2, 오답 감점 없음, 오답 반응에 정답과 이유. 미니게임 자리는 `MiniGameTheme{ Kind, Title, Place, Note("챕터 N · 설명"), Pairs(카드 6쌍), Replies }`, 커피 첫 잔은 교수 것.
- 엔딩(위부터): 1 호감도≤0 / 8 ≥10·q,t,i 각 2↑ / 7 ≥7·i 2↑ / 6 ≥6·t 2↑·q 1↑ / 5 q 3↑ / 4 s 3↑ / 3 t 2↑ / 2 그 밖. `EndingNames` 8(12자), `EndingText` 8(`교수님:` 줄 정확히 하나), `EndingLines` 2×8. 엔딩 조건은 플레이어에게 공유하지 않는다.
- 전개·배경·교수 이름·사진 키는 기존 학과와 겹치지 않게(기존 `*Story.cs`로 확인). `Mood`는 `Campus`/`Study`/`Crisis`, `chapter == 7`은 엔딩.

### 6.4 이미지
- 교수 사진 `Image = "ProfessorPark"` → `ProfessorPark{,Happy,Frown}Transparent.png`. 투명 PNG 1698×926, 상반신 가운데, 머리 끝 y 60, 690px쯤부터 흐림, 세 표정 위치 동일. 배경도 1698×926 영문 이름. 얼굴 크롭표는 `Tools/generate_portrait_faces.py`.

## 7. 도구 사용법
**개발 도구 설치** (근거 `docs/plans/Dev-Tooling.md`): `Tools/setup-dev.ps1`(setup-git-unity가 호출)이 빠진 필수 도구 Git·Git LFS·PowerShell 7·rg·uv·.NET SDK 10·Unity CLI·Serena를 winget/uv로 설치하고 Unity 스킬을 에이전트에 넣는다. `-IncludeOptional`은 gh·Node·Unity Hub·RTK 추가, `-CheckOnly`는 점검만(exit 2 = 필수 누락). UAC와 로그인(`gh auth login`, `unity auth login`)은 본인이 한다.

**Unity CLI 코드모드** (에디터가 열려 있어야 함): MCP 대신 스크립트로 호출을 묶고 요약만 출력한다.
```powershell
Import-Module ./Tools/codemode/Unity.psm1
Wait-UnityReady; $r = Invoke-UnityRecompile; if ($r.failed) { $r.errors | Select-Object -First 5 }
Get-UnityConsole -Level error -Tail 20; Invoke-UnityEval 'return UnityEditor.EditorBuildSettings.scenes.Length;'
```
함수 목록은 `Get-Command -Module Unity`, 나머지 약 190개 명령은 `Invoke-UnityCommand '<group> <name>' @{...}`(목록 `unity command`, 인자 `--help`). 캡처는 `Save-UnityGameView`(→ `Logs/codemode/`). `confirm=true`가 필요한 파괴적 명령은 사용자 지시가 있을 때만.

**Serena** (C# 심볼·참조, 읽기 전용): Claude Code는 `.mcp.json`으로 등록된다(첫 사용 시 승인). Codex는 `~/.codex/config.toml`에 `[mcp_servers.serena]` `command = "serena"`, `args = ["start-mcp-server", "--project-from-cwd", "--context=codex"]`, `env.PYTHONUTF8 = "1"`. 공유 설정 `.serena/project.yml`은 `read_only: true`(편집은 기본 도구로 해서 선점 훅을 지킨다)와 `ignore_all_files_in_gitignore: false`(git 무시된 `.csproj`를 읽기 위해)를 바꾸지 않는다. `.slnx/.csproj`가 없으면 `Sync-UnityProjectFiles`. Claude에서는 `serena_repl` 하나로 쓰며 `s.lsp.find_symbol`/`find_referencing_symbols`/`get_symbols_overview` 결과를 `print` 대신 마지막 식으로 돌려준다. 텍스트는 rg, 심볼·참조는 Serena.

**수어 지문자** (`Assets/Scripts/Sign/`, 계획·학습 가이드·팀 모임 대본·학습 PC 지시 통합 `docs/plans/Sign-Fingerspelling.md`, 맨 위 "읽는 법" 표부터): Play → `Tools → Sign → Recorder`에서 가명·세션·글자 목록(예 `주먹,보자기,검지,_other`) 입력, 손 추적 시작, 글자별 3회 이상 녹화(창 포커스 시 Space). 참여자 동의를 먼저 받는다. `Tools/SignData/raw/<사람>/<세션>.csv`(Git 제외)를 `Assets/StreamingAssets/SignData/`로 복사 → 미니게임 → 하이라이트 → 수어 지문자(0.6초 유지 = 정답). 측정 `Tools/SignTrain/Measure-SignKnn.ps1`, MLP 학습 `Tools/SignTrain/train_mlp.py`(통합 문서 2부). 녹화 점검 `Tools/SignTrain/Check-SignRecording.ps1 -Person <가명> -Labels "<글자들>,_other"`(좌표는 출력하지 않음, 종료 코드 0 정상·1 형식 오류·2 횟수 부족). 팀원 개인 녹화 방법과 에이전트 보조 규칙은 `docs/plans/Sign-Data-Recording-Guide.md`. **참여자 CSV는 열어 읽거나 채팅·Git에 올리지 않는다.**

**Gaze Lab**: Play → 시선 추적 테스트 → 연결·보정 → `움직이는 공 · 로그` → 경로·주기·시간·실험 이름 → 기록, 노란 공만 본다. 로그 `Logs/GazeLab/`, 분석 `Tools/Analyze-GazeLab.ps1 [-Iterations 10] [-Csv <파일> -WindowSeconds 10000]`. 같은 장치·자세·보정·경로의 baseline과만 비교하고, `metrics_available=false`는 오차 0이 아니다.

**iPhone 시선 연결** (VTube Studio, 장애는 `docs/plans/iPhone-Gaze-Handoff.md`): Wi-Fi는 iPhone 앱 `3rd Party PC Clients` 켜기 → Unity 시선 테스트 → iPhone 추적 → **iPhone의 Wi-Fi IPv4**와 앱 포트(보통 21412) → 보정. PC 수신은 UDP 11125, 같은 로컬망 필요(게스트 Wi-Fi·AP 격리·VPN·방화벽 Block 확인, 방화벽 전체를 끄지 않음). USB는 Apple Devices와 VTube Studio PC 앱 → 양쪽 Connect USB(iPhone 먼저) → PC 앱 `Allow Plugin API access`(보통 8001) → Unity에서 연결 후 `AI-ON Gaze Test` 허용(실기 미검증). 무료 버전은 5분 뒤 끊길 수 있다.

**교수 목소리** (`Professor.Voice`, 현재 `ShimHanmoe`): 음성 `Assets/Resources/Voice/<Voice>/<해시>.ogg`, 목록 `lines.txt`. 대사를 고치면 `.\Tools\Voice\export_lines.ps1 -Voice ShimHanmoe` → `<voice-clone>\cosyvoice\.venv\Scripts\python.exe Tools\Voice\generate.py Tools\Voice\ShimHanmoe.lines.txt Assets\Resources\Voice\ShimHanmoe`(바뀐 것만, `--force`는 전부, 읽는 법은 `SAY` 표) → `.ogg.meta`까지 커밋. TTS 환경 설치는 `docs/plans/TTS-Local-Setup.md`.

**AI 모드 서버** (`Server/ai-proxy/`, Cloudflare Worker): `gpt-6.1-terra`(12초, 하루 500회) → 실패 시 `gemini-3.1-flash-lite`. 키는 Cloudflare 비밀 설정에만 둔다. 배포: `npx wrangler login` → `npx wrangler secret put GEMINI_API_KEY`/`OPENAI_API_KEY` → `npx wrangler deploy`(주소는 `AiProfessor.ProxyEndpoint`). 설정에 개인 Gemini 키가 있으면 서버를 거치지 않는다.

## 8. 환경 주의
- 방화벽·네트워크 같은 PC 설정은 Git에 없다. 다른 PC에 그대로 적용된다고 가정하지 않는다. 학교/관리 PC 보안 정책을 우회하지 않는다.
