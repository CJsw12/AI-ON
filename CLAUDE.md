# AI-ON — Claude 작업 규칙

응답과 문서는 한국어로 작성한다. Unity `6000.3.23f1` 프로젝트.

## 시작할 때
- `PROGRESS.md`를 먼저 읽고 Done / Blockers / Next Steps를 확인한다.
- 시선 추적 작업이면 `docs/AI/iPhone-Gaze-Debug-Handoff.md` 최신 절을 읽는다.

## 역할 분담: Claude = 코디네이터, Codex = 구현 워커
- Claude는 요구 분석, 설계, 작업 명세 작성, 결과 검토, 사용자 보고를 맡는다.
- 여러 파일에 걸친 구현, 반복적인 수정, 독립 리뷰는 `orchestration` 스킬로 Codex 워커에게 맡긴다:
  `orca orchestration worker-start --spec "<명세>" --agent codex --worktree <current|new-child>`
- 한두 줄 수정, 파일 읽기, 짧은 조사는 Claude가 직접 처리한다. 위임 비용이 더 크다.
- 워커를 시작하면 턴을 끝낸다. 폴링하지 않는다. Orca가 `worker_done`/질문으로 깨운다.
- 워커 결과는 그대로 믿지 않는다. diff를 직접 읽고, 근거가 빠졌거나 모순이 있을 때만 검증을 다시 돌린다.

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
