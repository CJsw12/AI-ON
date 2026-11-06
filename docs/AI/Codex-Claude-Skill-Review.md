# Codex·Claude 공용 스킬 통합 검토안

> 2026-11-05 정정: 아래 최초 조사에서는 하위 폴더의 연결 속성을 확인하지 못했습니다. 재확인 결과 Claude의 개인 스킬 39개는 이미 각각 `~/.agents/skills`의 같은 이름 폴더를 가리키는 Windows Junction입니다. 따라서 아래에 적힌 사본 교체 작업은 필요하지 않습니다. `.trash`와 `synced`는 일반 폴더로 유지되어 있습니다. 연결의 Claude 실행 중 인식 여부는 별도로 확인해야 합니다.

작성일: 2026-11-05

## 확인한 현재 상태

- Codex에서 실제로 쓰는 개인 스킬 폴더는 `C:\Users\asdgg\.agents\skills`입니다. `~/.codex/skills`에는 `.system`만 있습니다.
- `~/.agents/skills`에는 개인 스킬 37개가 있습니다.
- `~/.claude/skills`에는 같은 이름의 스킬 37개가 이미 있으며, 각 스킬의 모든 파일을 SHA-256으로 비교했을 때 Codex 쪽과 차이가 없었습니다.
- Claude 폴더에만 `.trash`와 `synced`가 있습니다. 이 두 폴더는 Claude 전용 자료로 보존해야 합니다.
- 따라서 지금 필요한 작업은 Codex 스킬을 Claude에 다시 복사하는 작업이 아니라, 두 곳에 있는 동일한 사본을 하나의 원본으로 연결하는 작업입니다.
- 스킬 파일은 이번 조사에서 수정하거나 이동하지 않았습니다.

## 제안하는 구조

`~/.agents/skills`를 공용 원본으로 유지합니다. Codex는 지금처럼 이 폴더를 읽습니다. `~/.claude/skills` 안의 37개 스킬 폴더는 각각 공용 원본의 같은 이름 폴더를 가리키는 링크로 바꿉니다. 그러면 Codex 스킬을 한 번 수정했을 때 Claude도 같은 내용을 읽습니다.

`~/.claude/skills/.trash`와 `~/.claude/skills/synced`는 그대로 둡니다. `synced`는 Claude 계정 동기화 영역이며 예약 이름이므로 공유 원본으로 옮기지 않습니다.

## 적용 범위

공유 대상은 `~/.agents/skills`에 있는 개인 스킬 37개입니다. Codex 플러그인에 포함된 기능, MCP 도구, 앱 연결, 이미지 생성 기능, 문서 작업 기능과 자동 실행 훅은 스킬 파일만 복사해도 Claude에서 동작하지 않을 수 있으므로 이번 공용 스킬 폴더에 합치지 않습니다. 각 기능은 해당 Claude 플러그인이나 도구가 실제로 설치되어 있는지 별도로 확인해야 합니다.

Codex 전용 메타데이터나 도구 이름에 기대는 내용이 공유 스킬에 들어 있는지도 적용 전에 확인합니다. 필요한 경우 공용 `SKILL.md`는 중립적인 절차만 담고, Codex·Claude 전용 도구 호출은 각 에이전트의 연결 설정에서 따로 둡니다. 기존 Codex 원본을 Claude용으로 옮기거나 덮어쓰지 않습니다.

## 승인 후 진행할 작업

1. 현재 두 폴더를 다시 비교하고, 링크 변환 전 Claude 사본 37개를 날짜가 붙은 별도 백업 폴더에 복사합니다.
2. 먼저 영향이 작은 스킬 하나를 링크로 연결하고 Claude와 Codex에서 파일을 같은 경로로 읽는지 확인합니다.
3. 문제가 없으면 나머지 36개도 같은 방식으로 연결합니다. Windows에서 사용할 링크 종류는 첫 항목 적용 전에 확인합니다.
4. Claude 전용 `.trash`와 `synced`가 그대로 있고, 양쪽에서 공용 스킬을 발견하는지 확인합니다.
5. 설치가 정상인 것을 확인한 뒤에도 백업은 별도 승인 없이 삭제하지 않습니다.

이 절차는 Claude의 동일 내용 사본 37개를 링크로 교체하므로, 전환 전에 백업하고 한 항목씩 확인합니다. 설치 전 검토를 요청한 사용자 지시에 따라 이 문서는 제안만 정리했으며, 글로벌 스킬 폴더는 변경하지 않았습니다.

## 참고한 공식 문서

- [Codex CLI 설치 안내의 스킬 경로](https://developers.openai.com/api/reference/cli#use-the-cli-from-a-coding-agent): Codex는 `~/.agents/skills`, Claude Code는 `~/.claude/skills`를 사용합니다.
- [Claude Code 스킬 문서](https://code.claude.com/docs/en/skills#choose-where-skills-load): 개인 스킬 위치와 스킬 폴더 링크 지원을 설명합니다.
