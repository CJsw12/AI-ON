# 개발 도구·MCP 도입 검토 (2026-11-26)

목표: 에이전트(Claude Code, Codex, Antigravity)가 Unity와 C# 코드를 적은 토큰으로 정확히 다루게 하고, 새 PC 설치를 스크립트 한 번으로 끝낸다. 사용법은 `AGENTS.md` 7절, 이 문서는 결정 근거와 남은 검증만 둔다.

## 1. 결정
| 도구 | 판정 | 근거 |
|---|---|---|
| Unity CLI 1.x + `com.unity.pipeline` | **도입(주 경로)** | 공식. 열린 에디터에 컴파일·콘솔·테스트·C# eval·Play·캡처 등 약 190개 명령. Pipeline은 manifest에 이미 있다(0.10.0-exp.1). Unity AI Assistant MCP는 공식 deprecated |
| ripgrep | **도입** | 셸·Codex·RTK가 쓴다(Claude Code는 내장 rg가 있음) |
| Serena 2.x (Roslyn LSP) | **시범 도입** | C# 심볼 탐색·참조·리팩터링을 Claude/Codex/Antigravity 공통으로. .NET 10 SDK와 PowerShell 7 필요. GPL-3.0(도구로만 쓰고 배포하지 않음) |
| `com.unity.ide.visualstudio` | **도입** | Unity가 `.sln/.csproj`를 만들어야 C# 언어 서버가 프로젝트를 읽는다. 생성 파일은 git 무시 |
| Claude Code `csharp-lsp` 플러그인 | 보류 | csharp-ls 0.29가 .NET 11 SDK 요구, Claude LSP 클라이언트와 호환 문제 제보. Serena 시범 결과를 보고 판단 |
| RTK | 개인 선택 | 출력 압축 효과는 크지만 `rtk init`이 공유 `AGENTS.md`/`CLAUDE.md`를 고치고, 출력이 잘려 오류를 놓칠 수 있다. 팀 공통으로는 넣지 않고 `rtk init -g`(전역 훅)만 허용 |
| coplay MCP for Unity, IvanMurzak Unity-MCP | 건너뜀 | Unity CLI와 기능이 겹친다. Unity CLI를 못 쓸 때의 대안 |
| mcporter(MCP → CLI 생성) | 보류 | Node 24 필요. 코드모드로 감쌀 MCP가 지금은 Serena뿐이라 이득이 작다 |

## 2. 코드모드 적용 방식 (Cloudflare Code Mode, Anthropic "Code execution with MCP")
- 핵심: 도구 스키마 수백 개를 컨텍스트에 싣는 대신, 에이전트가 짧은 스크립트로 여러 호출을 묶고 **요약만 출력**한다.
- Unity: CLI 자체가 코드 인터페이스다. `Tools/codemode/Unity.psm1`이 자주 쓰는 흐름(준비 대기, 재컴파일 후 오류, 콘솔 오류, 테스트, eval, Play, 캡처, csproj 생성)을 함수로 묶는다. 나머지는 `Invoke-UnityCommand '<group> <name>'`. Unity MCP 서버는 등록하지 않는다.
- Serena: MCP로 등록한다(`.mcp.json`). 2.x는 그 자체가 코드모드(REPL 도구 하나)라 래퍼를 만들지 않는다(4절).
- 샌드박스: Worker 격리는 없다. 에이전트 도구의 권한 설정을 따르고, 파괴적 Unity 명령(`asset delete` 등 `confirm=true` 필요)은 사용자 지시가 있을 때만 쓴다.

## 3. 설치 자동화
- `Tools/setup-dev.ps1`: winget으로 빠진 도구를 설치하고 `serena-agent`(uv tool), Unity 에이전트 플러그인(`unity setup`)을 준비한다. `-CheckOnly`는 점검만. `Tools/setup-git-unity.ps1`이 기본으로 호출한다.
- 관리자 권한이 필요한 설치(PowerShell 7, .NET SDK 등)는 UAC 창이 뜬다. 로그인(`gh auth login`, `unity auth login`)과 약관 동의는 본인이 한다.

## 4. 검증 결과 (2026-11-26, PC11)
- `setup-dev.ps1`: pwsh 7.7.0, rg 16.0.0, serena-agent 설치, Unity 스킬 claude-code·codex 동기화, exit 0. CLI가 없는 데스크톱 앱이라 `unity setup all`은 대상 0이고 스킬 동기화로 대체된다.
- Serena 2.x는 claude-code 컨텍스트에서 도구 2개(`initial_instructions`, `serena_repl`)만 노출한다. 즉 **Serena 자체가 코드모드**(Python REPL의 `s.lsp` 등)라 별도 래퍼가 필요 없다.
- 기본값 `ignore_all_files_in_gitignore: true`에서는 `.csproj`가 무시되어 파일 단위 분석으로 떨어졌다(`GazeMapper` 참조 0). `false` + `ignored_paths`로 바꾸자 참조 46개(5개 파일), `MiniGame` 59개, 시작 약 12초. 이 설정을 `.serena/project.yml`로 공유한다.
- `read_only: true`: 편집 기능이 사라져 agent-sync 편집 차단 훅 우회를 막고, 안내문도 9.2KB → 8.0KB로 준다. 단 안내문은 Read/Edit 대신 Serena를 쓰라고 강하게 요구하므로, 우선순위는 `AGENTS.md`(텍스트 rg, 심볼 Serena, 편집 기본 도구)를 따른다.
- Windows 한글 로캘(cp949)에서 Serena 로그 인코딩 오류가 나서 `PYTHONUTF8=1`을 준다. REPL 안의 `print`는 MCP 통신을 깨므로 쓰지 않는다.

## 5. 남은 검증
- [ ] Serena 메모리 사용량(과거 30GB 보고)을 긴 세션에서 확인
- [ ] Codex·Antigravity에서 Serena 연결(Antigravity 설정 경로 미확인)
- [ ] 팀원 PC에서 `setup-dev.ps1` 첫 실행
- [ ] Unity CLI 라이선스 조건(팀 계정)
