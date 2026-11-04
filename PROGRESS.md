# AI-ON 세션 진행도 및 작업 맥락 (PROGRESS.md)

## 1. 완료된 작업 (Done)
- **원격 저장소 동기화 완료**: `origin/main`의 최신 커밋들(`817ac2f` 교수 상반신/프레이밍 개편, iPhone 시선 추적 UDP 및 캘리브레이션/별잡기 미니게임, 웹캠 시선 추적, Cloudflare 릴레이 기반 AI 모드, 자연과학부/예체능학부 신규 비주얼 에셋 등) 패치 및 로컬 병합 완료.
- **`.gitignore` 충돌 해결 및 정리**: 로컬의 `.serena/` 항목과 원격의 AI key/Cloudflare worker 캐시 무시 설정을 통합.
- **단위 테스트 스크립트 빌드 복구 (`Tests/ProfessorPersonality.Tests.ps1`)**:
  - 원격에서 추가된 외부 패키지(UnitEye, Newtonsoft.Json) 의존 스크립트(`GameController.GazeTest.cs`, `VTubeStudioUsbApiBridge.cs` 등)가 독립 .NET 컴파일 환경에 잡혀 빌드가 실패하던 문제를 제외 컴파일 설정으로 수정.
  - 16개 학과 대사, 32명 교수 성격, 호감도 및 엔딩 유효성 검증 단위 테스트 5종 전수 PASS 확인 (`PASS: 16 faculties, 32 personalities, 3524 dialogue calls`).
- **웹캠 시선 추적 화면 왜곡 및 테두리/하단 인식 버그 수정**:
  - `GameController.cs`의 `OnGUI` 종료 시 1280×900 레터박스용 `Canvas` 행렬이 리셋되지 않고 누수되어, 전체 화면 픽셀 기준으로 동작하는 `HomulerGazeCalibration`의 보정 점과 시선 좌표가 왜곡되던 현상 해결 (`GUI.matrix = Matrix4x4.identity`).
  - `GameController.GazeTest.cs`에서 웹캠 보정 화면 활성화 시(`GazeCalibrationActive`) 기존 메뉴 캔버스 중복 드로우를 방지하고 정상적인 화면 좌표계를 제공하도록 분기 처리.
- **원격 푸시 완료**: 작업 내용 `origin/main`에 푸시 완료 (`27f4ea5`).

## 2. 핵심 결정 사항 (Decisions)
- **독립 검증 도구 분리 원칙**: `Tests/*.ps1`은 Unity Editor 없이 CLI 환경에서 스토리, 미니게임 결과, 호감도 정합성을 즉시 검증하는 핵심 파이프라인이므로, Unity 런타임/외부 네이티브 라이브러리(MediaPipe, VTS 브리지 등) 의존 스크립트는 컴파일 대상에서 명시적으로 제외하여 언제든 100% 통과 가능하도록 유지합니다.
- **IMGUI 매트릭스 복원 필수**: 비주얼 노벨 뷰는 고정 1280×900 캔버스 스케일링을 사용하지만, 웹캠 캘리브레이션 등 전체 화면 해상도를 직접 다루는 외부 컴포넌트가 활성화될 때는 반드시 `GUI.matrix`를 `Matrix4x4.identity`로 복원해야 왜곡이 발생하지 않습니다.

## 3. 다음 진행 작업 (Next Steps)
- 팀원(태환, 김도훈, 김병구)의 추가 푸시 및 실시간 피드백 모니터링 (특히 웹캠/아이폰 시선 보정 테두리 추적 개선 여부 확인).
- Cloudflare AI 프록시 서버 및 Gemini 응답 지연/타임아웃(504) 관련 안정성 지속 확인.
- 16개 학과별 스토리 및 미니게임 인게임 플레이 밸런스 유지보수.
