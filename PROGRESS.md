# AI-ON 진행 상황 (PROGRESS.md)

현재 상태만 적는다. 작성 규칙은 `AGENTS.md` 3·5절(푸시 직전 1회 갱신, 덧붙이기, 항목 5줄·줄당 200자 안쪽, `플레이 방법` 한 줄).
2026-11-06 이전 Done 기록은 `git log -p -- PROGRESS.md`로 본다.

## 1. 형상 (Baseline)
- 브랜치 `main` 하나에서 agent-sync 선점으로 협업한다(2026-11-26 원격 브랜치 전부 병합).
- 회귀 테스트 `Tests/*.ps1` 10종. 문서는 `AGENTS.md`(규칙·도구), `PROGRESS.md`, `docs/plans/`(계획)만 쓴다.

## 2. 완료 및 검증 (Done)
- **[Claude] 2026-11-26 문서 정리**: 마크다운 48개 → 21개. 규칙·도구 사용법은 `AGENTS.md`로 합치고 `CLAUDE.md`는 `@AGENTS.md` + 코디네이터 규칙만 남겼다.
  - 진행 중 계획 5개는 `docs/plans/`로 옮겼고, 완료된 기획·리뷰·사용법 문서와 `CHANGELOG.md`는 지웠다(git 기록에 남음).
  - **플레이 방법**: 규칙·도구가 궁금하면 `AGENTS.md`의 목차(1 문서 정책 ~ 8 환경 주의)를 본다. 미니게임·스토리 제작 규칙은 6.2·6.3절.
  - 검증: `Tests/*.ps1` 10종 exit 0, `git diff --check` 통과. 문서만 바뀌어 Unity 동작 변화 없음.
- **[Claude] 2026-11-26 원격 브랜치 전체 main 병합 + 협업 규칙 적용**: agent-sync, 수어 지문자, voice-tts, wip/mjpg-camera, wip/posture-fit 병합.
  - posture-fit 충돌은 `WebcamFeatures()` 구조를 따르고 Gaze Lab 기록 값을 그 결과로 채웠다.
  - **플레이 방법(설치)**: pull 후 `Tools/setup-git-unity.ps1 -AgentId <이름>-<도구>-<모델>` → `bash Tools/agent-sync.sh status`(`AGENTS.md` 5절).
  - 검증: 테스트 10종 exit 0, `test_agent_sync.sh` 23항목 통과. **Unity 컴파일·웹캠 실기 미검증.** `VoiceTTS/` WAV 30MB+가 LFS 밖.
- **2026-11-26 Qwen3-TTS 로컬 설치 안내**: `docs/plans/Qwen3-TTS-Local-Setup.md`(Intel XPU·CPU 경로, 박태준 70대 음색 설계). 실제 설치·추론 미검증.
- **[Claude+Codex+Antigravity] 2026-11-26 수어 지문자 미니게임 1차 구현**: MediaPipe 손 관절 21개 → k-NN → 0.6초 유지 확정 → 하이라이트 미니게임.
  - 계획 `docs/plans/Sign-Fingerspelling-Plan.md`, 사용법 `AGENTS.md` 7절. 세 AI 교차 리뷰로 27건 수정.
  - **플레이 방법**: Play → `Tools → Sign → Recorder`로 임시 손 모양 녹화 → CSV를 `Assets/StreamingAssets/SignData/`로 복사 → 미니게임 → 하이라이트 → 수어 지문자.
  - 검증: 배치 컴파일 오류 0, 테스트 10종 exit 0. **웹캠 실기·녹화 도구·학교 PC 성능 미검증.** 정식 글자 5개와 녹화 데이터 없음.
- **시선 추적 선택 적용**: 새 스토리·이어하기를 시선 추적 없이 진행할 수 있고, 이때 눈맞춤 기록과 시선 전용 이벤트는 빠진다.
  - **플레이 방법**: 교수 선택 또는 세이브 불러오기 후 `시선 추적 없이 계속`. 쓰려면 같은 화면에서 `웹캠 시작`/`iPhone 추적` → 보정.
  - 검증: Windows 빌드 성공, 카메라 없이 새 게임·불러오기 진입 확인. 실제 장치 보정은 재검증하지 않음.
- **[Claude] 2026-11-24 agent-sync 협업 도구 도입**: 경로 선점, 커밋·푸시 훅, Claude 편집 차단 훅(`AION_*` 변수, `aion.agentId`). Codex 리뷰 2회 반영.
  - 검증: `test_agent_sync.sh` 23항목, PowerShell 5.1·7 설치 확인. 팀원 PC 설치와 열린 에디터 CLI 경로는 미검증.
- **[Claude] 2026-11-24 교수 사진 떨림 + 캔버스 회전 중심 수정**: `GameEffects` 효과 경로 정수 반올림 제거, `NovelUI.RotateAround` 헬퍼로 4곳 교체.
  - **플레이 방법**: 스토리 대사 화면에서 사진이 부드럽게 움직이는지, 창 크기를 바꾼 뒤 선택지 하트·별이 교수 주변에서 도는지 본다.
- **[Claude] 2026-11-24 클릭 보정 컴파일 오류(CS0103) 수정 + 유료 UI 팩 추적 제외**: UI 팩이 없으면 도형·글자로 대신 그린다.
  - 과거 히스토리와 `Builds/AI-ON`에는 UI 팩이 남아 있어 저장소 공개 여부는 팀 결정이 필요하다.

## 3. 핵심 결정 사항 (Decisions)
- `OnGUI` 정적 스타일 캐싱, AI 실패 시 오프라인 선택지 fallback(`AGENTS.md` 6.1절).
- 기능 브랜치 대신 `main` + agent-sync 선점. `PROGRESS.md`는 `merge=union`.
- 마크다운은 `AGENTS.md`·`PROGRESS.md`·`docs/plans/`만(2026-11-26).

## 4. 결함 및 부채 (Blockers)
- **iPhone 머리 위치 영향 미해결**: 같은 지점을 봐도 머리 위치에 따라 포인터가 움직인다. 후보 제한은 임시 대응. 보정 오차 294→157→269px는 자세·표본이 달라 비교 불가.
- **웹캠 실기 검증**: 얼굴 자동 크롭·텍스처 합성, 2026-11-26 병합한 MJPG 읽기·자세 기반 보정은 Unity Play에서 확인해야 한다.
- **외부 패키지 의존 스크립트**: UnitEye와 하드웨어 브리지는 CLI 빌드에서 제외되어 있어 최종 패키징 때 의존성을 점검해야 한다.
- **`MfCameraCapture.cs.meta` 누락**: `wip/mjpg-camera`에서 `.cs`만 들어왔다. Unity가 만든 `.meta`를 커밋해야 한다.

## 5. 다음 진행 작업 (Next Steps)
- **병합분 Unity 확인**: `wip/mjpg-camera`·`wip/posture-fit` 병합분을 컴파일하고 웹캠 보정·Gaze Lab 표시를 확인한다. 팀원 PC마다 `Tools/setup-git-unity.ps1` 1회 실행.
- **수어 지문자**: 실기 확인(관절 정렬·거울 기준), P0 글자 5개 확정, P2 녹화·정확도, 학교 PC 성능. 계획서 10절 결정 8개를 팀이 정한다.
- **Claude Max 활용**: `docs/plans/Claude-Max-Usage-Plan.md` 초안. 대회 AI 사용 고지 방식과 지원 계정 규칙부터 확인.
- **iPhone 시선**: 같은 자세에서 독립 검증 오차와 목표 응시 오차 측정. 고개 회전/평행이동/거리를 분리 측정하고 머리 위치 정보 도입 여부 판단(`docs/plans/iPhone-Gaze-Handoff.md`).
- 눈맞춤 카운터: iPhone 경로·엔딩 표시·이어하기 실기 확인. 판정 상수(`GameController.EyeContact.cs`)는 F8로 보며 조정.
- 눈빛 피하기: 실제 장치로 난이도·소리 균형 확인. `GameController.cs` 별 회전과 `GameEffects.cs`의 `RotateAroundPivot` 문제 여부 확인.
- AI 모드: 단답형 퀴즈 힌트 대기 중 Enter 동작 확인. Terra 확인 시 설정의 개인 Gemini 키를 지운다.
- 플레이어 웹캠 아바타 촬영·2인 구도 실기 점검. 16개 학과 스토리·미니게임·엔딩 분기 밸런스 점검.
