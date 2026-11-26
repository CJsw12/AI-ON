# 여러 PC·여러 에이전트 협업 (agent-sync)

네 명이 각자 Claude·Codex·Antigravity로 `main`에서 동시에 작업할 때 생기는 세 가지 사고를 막는다. 첫째는 같은 파일을 동시에 고쳐 충돌하는 일이고, 둘째는 컴파일이 깨진 코드를 푸시하는 일이며, 셋째는 유료 에셋이나 대화 로그를 실수로 커밋하는 일이다. surviving-game에서 쓰던 도구를 AI-ON에 맞게 옮겼다.

## 1. 처음 한 번만 (PC마다, 저장소 폴더마다)
```powershell
git pull
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-git-unity.ps1 -AgentId <이름>-<도구>-<모델>
```
- 에이전트 ID 예시는 `dohun-claude-opus55`, `taehwan-codex-sol`이다. 영문·숫자·`.`·`_`·`-`만 쓴다.
- 이 스크립트는 이 폴더의 `.git` 설정만 바꾼다. 하는 일은 커밋·푸시 훅 설치, 에이전트 ID 저장, `pull.rebase=true`, Unity Smart Merge(수동 `git mergetool --tool=unityyamlmerge`) 등록이다.
- Unity를 기본 위치(`C:\Program Files\Unity\Hub\Editor\6000.3.23f1`)가 아닌 곳에 설치했으면 `-EditorPath "<경로>\Editor\Unity.exe"`를 함께 준다. 푸시 전 컴파일·테스트 검사가 이 경로를 쓴다.
- 한 폴더에서 Claude와 Codex를 함께 쓰면 도구마다 환경 변수 `AION_AGENT_ID`로 ID를 따로 준다. Claude는 `~/.claude/settings.json`의 `env`에, Codex는 `~/.codex/config.toml`의 `[shell_environment_policy.set]`에 넣는다.
- 유료 UI 팩(Modern UI Pack)은 Git에 없다. 팀 안에서 비공개로 받은 파일을 `Assets/Resources/UI/`에 넣는다. 없어도 게임은 단순한 도형으로 대신 그려서 실행된다.
- 설정이 잘 됐는지는 `bash Tools/agent-sync.sh status`로 확인한다. 맨 위에 `NOT SET UP` 경고가 없으면 된다.

## 2. 매번 작업할 때
```bash
bash Tools/agent-sync.sh status                                  # 새 커밋, 누가 어디를 잡고 있는지
bash Tools/agent-sync.sh claim "예체능학부 스토리" Assets/Scripts/Stories/ArtsSports/
# ... 작업 ...
bash Tools/agent-sync.sh finish "Add arts faculty story" Assets/Scripts/Stories/ArtsSports/
```
- `claim`은 경로를 선점하고 바로 푸시한다. 폴더는 끝에 `/`를 붙인다. 학과나 기능 단위로 크게 잡는다(잦은 선점 커밋은 히스토리를 어지럽힌다).
- `finish`는 지정한 경로를 커밋하면서 선점을 해제하고 한 번에 푸시한다. 다른 사람이 먼저 푸시했으면 자동으로 rebase한 뒤 다시 시도한다.
- 작업을 접을 때는 `release`, 오래 걸리면 `heartbeat`로 선점을 갱신한다. 8시간 동안 갱신이 없으면 STALE로 표시된다.
- PowerShell만 쓰는 도구는 `pwsh -NoProfile -File Tools/agent-sync.ps1 <명령>`을 쓴다(Git Bash를 자동으로 찾는다).
- `PROGRESS.md`는 선점하지 않는다. 짧게 고치고, 고치기 직전에 pull하고 고친 직후에 푸시한다.
- `claim`과 `finish`는 `main` 브랜치에서만 동작한다.
- 아직 푸시하지 않은 커밋이 있으면 `claim`·`release`·`heartbeat`가 거절된다. 선점 커밋과 함께 그 커밋까지 공개되는 일을 막기 위해서다. 먼저 `git push`하거나 `finish`로 마무리한다.
- Unity Play Mode나 에디터 검사를 하기 전에는 `bash Tools/agent-sync.sh editor acquire "<목적>"`을, 끝나면 `editor release`를 실행한다. 같은 PC에서 두 에이전트가 에디터를 동시에 쓰지 않게 하는 로컬 사용권이며 커밋은 생기지 않는다. Orca 워커는 Unity를 실행하지 않는다.

## 3. 자동으로 막히는 것
| 언제 | 검사 | 우회(합의한 경우에만) |
|---|---|---|
| 커밋 | 다른 사람이 선점한 경로 | `ALLOW_FOREIGN_PATHS=1` |
| 커밋 | `Assets/Resources/UI/`(유료 UI 팩), 카카오톡 로그 | `ALLOW_PRIVATE_FILES=1` |
| 커밋 | `Assets/` 파일과 `.meta` 짝 불일치 | `ALLOW_MISSING_META=1` |
| 커밋 | LFS가 아닌 10MB 초과 파일 | `ALLOW_LARGE_NON_LFS=1` |
| 푸시 | `.cs`가 바뀌었는데 작업 폴더의 `.cs`가 푸시할 커밋과 다름(미커밋·미추적 파일) | 커밋하거나 stash한 뒤 다시 푸시 |
| 푸시 | `.cs`가 바뀌었으면 `Tests/*.ps1` 전부 exit 0 | `AION_SKIP_TESTS=1` |
| 푸시 | `.cs`가 바뀌었으면 Unity 컴파일 성공 | `AION_SKIP_COMPILE_CHECK=1` |
| Claude 편집 | 다른 사람이 선점한 파일 편집 차단 | `ALLOW_FOREIGN_PATHS=1` |

- 컴파일 검사는 Unity 에디터가 열려 있고 Unity CLI(`unity`)가 있으면 열린 에디터에 묻는다. 에디터가 닫혀 있으면 배치 모드로 직접 컴파일한다(기존 `Library/` 사용, 약 1분). 에디터는 열려 있는데 CLI가 없으면 검사하지 못하므로, 푸시 전에 Console 오류를 직접 확인한다.
- Unity나 PowerShell 7을 찾지 못하면 푸시를 막지 않고 경고만 출력한다. 설치 위치가 다른 팀원이 막히지 않게 하려는 설계이므로, 경고가 보이면 직접 확인한다.
- 선점이 겹쳐 막히면 카톡으로 조율하거나 `bash Tools/agent-sync.sh send <상대 ID> "<메시지>"`를 쓴다. 상대는 다음 `status`에서 메시지를 본다.

## 4. 팀 규칙 (surviving-game 운영 경험 반영)
- **커밋 범위**: 커밋할 경로를 직접 지정한다. `git add -A`, `git add .`, `git add -f`는 쓰지 않는다. `main`에는 강제 푸시하지 않는다.
- **작게 자주**: 테스트나 컴파일 검사를 통과한 단위마다 커밋하고 푸시한다. 몇 시간씩 쌓아 두지 않는다.
- **병합이 어려운 파일**(`.unity`, `.prefab`, `.controller`, `ProjectSettings/*`, `Packages/manifest.json`, `packages-lock.json`)은 폴더가 아니라 파일 단위로 선점하고, 끝나면 바로 푸시해서 해제한다.
- **내 선점 밖 충돌**: pull/rebase에서 다른 사람 선점 경로가 충돌하면 직접 풀지 않는다. `git rebase --abort` 후 `send`로 상대에게 알린다. Unity YAML 충돌은 `git mergetool --tool=unityyamlmerge <파일>`을 먼저 시도한다.
- **우회 변수**(`ALLOW_*`, `AION_SKIP_*`)는 사람이 동의했을 때만 쓰고, 사용 사실을 커밋 메시지나 `PROGRESS.md`에 적는다.
- **대용량 파일**: 새 음성·이미지·영상·모델처럼 큰 바이너리는 커밋 전에 팀에 확인한다. 계속 둘 파일이면 확장자나 경로를 `.gitattributes`의 LFS에 먼저 등록한다.
- **검사 실패**: 실패한 검사를 약하게 바꾸지 말고 실제 동작부터 확인한다. 필요한 도구가 없어 검사를 못 했으면 원인과 실행하지 못한 범위를 "미검증"으로 기록한다.
- **`PROGRESS.md`**: `.gitattributes`에 `merge=union`이 설정되어 있어 양쪽 기록이 자동으로 합쳐진다. 그러니 기존 줄을 고치기보다 새 줄을 덧붙인다(같은 줄을 둘이 고치면 두 줄이 다 남는다). 항목은 5줄 안쪽, 줄당 200자 안쪽으로 쓰고, 긴 로그·수치는 파일 경로만 남긴다.
- **기다리기**: 다른 에이전트의 메시지나 선점 해제를 타이머로 반복 확인하지 않는다. 급하면 사람에게 알린다.

## 5. 도구 자체 확인
```bash
bash Tools/test_agent_sync.sh   # 임시 폴더에 PC 두 대를 흉내 내 선점·차단·해제를 검사 (exit 0 = 통과)
```
