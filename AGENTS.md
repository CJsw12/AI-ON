# AI-ON — Codex 작업 규칙

응답과 문서는 한국어로 작성한다. Unity `6000.3.23f1` 프로젝트.

## 시작할 때
- `PROGRESS.md`를 먼저 읽고 Done / Blockers / Next Steps를 확인한다.
- 시선 추적 작업이면 `docs/AI/iPhone-Gaze-Debug-Handoff.md` 최신 절을 읽는다.

## Orca 워커로 실행될 때 (Claude 코디네이터)
프롬프트에 Task / Dispatch ID가 있으면 Orca 워커다. 이때는 아래 규칙을 따른다.
- 명세에 적힌 범위와 수정 허용 파일만 바꾼다. 범위 밖 수정이 필요하면 직접 하지 말고 `orca orchestration ask`로 코디네이터에게 묻고 턴을 끝낸다.
- 명세가 모호하거나 서로 충돌하면 추측하지 말고 `ask`로 묻는다.
- 커밋·푸시·브랜치 생성·stash 조작은 명세에 명시돼 있을 때만 한다.
- `PROGRESS.md`와 인수인계 문서는 명세에 명시돼 있을 때만 수정한다.
- 끝나면 `worker_done`으로 보고한다. 보고에는 다음을 포함한다:
  1. 변경한 파일 목록과 변경 요지
  2. 실행한 검증 명령과 실제 결과(exit code, 실패 출력)
  3. 검증하지 못한 항목과 남은 위험
- 실패했으면 실패로 보고한다. 테스트를 건너뛰었거나 일부만 통과했으면 그대로 적는다.

## 단독으로 실행될 때
- 커밋·푸시는 사용자가 요청할 때만 한다.
- Claude 세션이 같은 작업 폴더를 쓸 수 있다. 시작 전에 `git status`로 다른 세션의 미커밋 변경을 확인하고, 내 작업이 아닌 변경은 되돌리거나 덮어쓰지 않는다.

## 검증
- 회귀 테스트: `Tests/*.ps1` 5종 (ImpactLabIntegration, PhysicsExperiencedDialogue, PhysicsImpactRules, PhysicsStory, ProfessorPersonality). 모두 exit 0이어야 한다.
- 변경 후 `git diff --check`를 통과해야 한다.
- AiProfessor/CoffeeRecipeSO 경고는 기존 경고다. 새 경고와 구분해서 보고한다.
- PS 테스트 통과는 Unity 전체 컴파일이나 실기기 동작을 보장하지 않는다. 검증하지 않은 항목은 "미검증"이라고 적는다.
- 시선 추적 수치(보정 오차 px)는 자세·표본이 다르면 전후 개선율로 해석하지 않는다.

## 코드 규칙
- `OnGUI`에서 매 프레임 `new GUIStyle` 등 할당하지 않는다. 정적 필드 캐싱을 쓴다.
- AI/네트워크 실패 시 스토리 진행이 막히지 않도록 오프라인 선택지 fallback을 유지한다.
- Unity 에셋을 추가하면 `.meta` 파일도 함께 포함되는지 확인한다.

## 환경 주의
- 방화벽 규칙, 네트워크 설정 같은 PC 설정은 Git에 포함되지 않는다. 다른 PC에 그대로 적용된다고 가정하지 않는다.
- 학교/관리 PC의 보안 정책을 임의로 우회하지 않는다.
