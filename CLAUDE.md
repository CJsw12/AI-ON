# AI-ON — Claude 작업 규칙

응답과 문서는 한국어로 작성한다. Unity `6000.3.23f1` 프로젝트.

## 시작할 때
- `PROGRESS.md`를 먼저 읽고 Done / Blockers / Next Steps를 확인한다.
- 시선 추적 작업이면 `docs/AI/iPhone-Gaze-Debug-Handoff.md` 최신 절을 읽는다.
- `git pull`/병합으로 다른 팀원의 커밋을 받았으면, `PROGRESS.md` Done에 새로 생긴 항목을 읽고 사용자에게 **새 기능 요약 + 플레이 방법(어디서 시작해 무엇을 누르는지) + 아직 미검증인 부분**을 짧게 알려 준다. 항목에 `플레이 방법`이 있으면 그대로 전달한다.
- 기능을 추가해 `PROGRESS.md`를 갱신할 때는 다른 팀원이 바로 해 볼 수 있게 `플레이 방법` 한 줄을 함께 적는다.

## 역할 분담: Claude = 코디네이터, Codex = 구현 워커
Claude는 요구 분석, 설계, 작업 명세 작성, 결과 검토, 사용자 보고를 맡는다.
이 프로젝트에서는 아래 기준이 Orca `orchestration` 스킬의 기본 안내("자체 서브에이전트 우선")보다 우선한다.

### 위임 판단 (위에서부터 순서대로 첫 번째로 맞는 항목을 따른다)
1. **사용자가 실행자를 지정함** → 그대로 따른다. "Codex로/Codex 워커로" → Codex 워커, "직접 해/Claude가 해" → 직접.
2. **Unity 에디터·실기기(iPhone, 웹캠)·사용자 PC 설정이 필요함** → 위임하지 않는다. Claude가 준비하고 사용자에게 실행을 요청한다.
3. **설계 결정이나 사용자 확인이 먼저 필요함** → 위임 전에 사용자에게 묻는다. 결정이 끝난 뒤 4~6번을 적용한다.
4. **코드 변경 작업 → Codex 워커** (`--worktree new-child`). 아래 중 하나라도 해당하면:
   - 2개 이상 파일 수정, 또는 한 파일에서 30줄 이상 변경
   - 새 기능·리팩터링·버그 수정 구현
   - 테스트를 실행해 통과시키는 것이 완료 조건인 작업
5. **변경 후 독립 리뷰 → Codex 워커** (`--worktree current`, 읽기 전용). Claude가 직접 쓴 코드가 20줄을 넘으면 커밋 전에 Codex 리뷰를 받는다.
6. **나머지 → Claude가 직접 또는 Claude 서브에이전트**
   - 직접: 1개 파일 30줄 미만 수정, 문서·설정 수정, 파일 읽기, git 조회, 짧은 질문 답변
   - Claude 서브에이전트(Agent 도구): 여러 파일에 걸친 코드 검색·조사처럼 결론만 필요한 읽기 작업

### 위임 실행 규칙
- 위임 전에 "무엇을 / 누구에게 / 어느 worktree" 한 줄을 사용자에게 알린다.
- 실행: `orca orchestration worker-start --spec-file - --agent codex --worktree <new-child|current> --task-title "<제목>"`
- 워커를 시작하면 턴을 끝낸다. 폴링하지 않는다. Orca가 `worker_done`/질문으로 깨운다.
- 워커 결과는 그대로 믿지 않는다. diff를 직접 읽고, 근거가 빠졌거나 모순이 있을 때만 검증을 다시 돌린다.
- 수정 요청은 새 워커를 띄우지 말고 `orca orchestration send --to dispatch:<id>`로 같은 워커에게 보낸다.
- `worker-start`가 실패하면 그대로 재시도하지 않는다. 터미널 출력으로 원인을 확인해 사용자에게 보고하고, 사용자가 원하면 Claude가 직접 처리한다.

### worktree 선택
- 코드 수정 작업: `--worktree new-child` (같은 폴더를 Claude/Codex가 동시에 수정하는 충돌 방지).
- 읽기 전용 리뷰/분석: `--worktree current`.
- Unity 에디터 실행·실기 검증이 필요한 작업은 원본 폴더에서 한다. 새 worktree는 `Library/` 재생성으로 오래 걸린다.

### 작업 명세에 반드시 포함
1. 대상 파일/범위와 수정 가능한 파일 목록
2. 바꿀 내용과 하지 말아야 할 것 (커밋·푸시 금지 여부 포함)
3. 완료 증거: 실행할 테스트 명령과 기대 결과, `git diff --check`
4. 실패 기준 (non-zero exit가 실패인지, 그 출력을 받는 게 목적인지)

## 검증
- 회귀 테스트: `Tests/*.ps1` 5종 (ImpactLabIntegration, PhysicsExperiencedDialogue, PhysicsImpactRules, PhysicsStory, ProfessorPersonality). 모두 exit 0이어야 한다.
- AiProfessor/CoffeeRecipeSO 경고는 기존 경고다. 새 경고와 구분할 것.
- PS 테스트 통과는 Unity 전체 컴파일이나 실기기 동작을 보장하지 않는다. 검증하지 않은 항목은 "미검증"이라고 적는다.
- 시선 추적 수치(보정 오차 px)는 자세·표본이 다르면 전후 개선율로 해석하지 않는다.

## Git / 협업
- 커밋·푸시는 사용자가 요청할 때만 한다. Codex 워커에게도 명세에 명시한다.
- `PROGRESS.md`는 작업 세션 종료·푸시 직전에 한 번만 갱신한다 (Done / Next Steps).
- stash와 다른 사람의 미커밋 변경은 자동으로 적용하거나 버리지 않는다.
- Unity 에셋을 추가하면 `.meta` 파일도 함께 포함되는지 확인한다.

## 환경 주의
- 방화벽 규칙, 네트워크 설정 같은 PC 설정은 Git에 포함되지 않는다. 다른 PC에 그대로 적용된다고 가정하지 않는다.
- 학교/관리 PC의 보안 정책을 임의로 우회하지 않는다.
