# Project Change Log

> **공지**: 향후 모든 버전별 변경 사항 및 릴리스 노트는 [GitHub Releases](https://github.com/CJsw12/AI-ON/releases) 및 Git 커밋 히스토리에서 공식 관리됩니다.  
> 세션 작업 상태 및 팀원 간 실시간 인수인계 맥락은 루트의 [PROGRESS.md](PROGRESS.md)를 확인해 주세요.

---


## 2026-11-04 — Working tree changes after `a22440d`

The current branch is `main`; `a22440d` (`Add webcam gaze tracking test mode`) is the committed baseline. The following items describe the working tree at the time this entry was written. They are not a claim that the work has been committed or pushed.

### Team setup guide and iPhone verification

- Added `docs/AI/iPhone-Gaze-Tracking-Setup.md` with Windows/Unity/VTube Studio installation, Wi-Fi UDP and USB/API setup, calibration, network requirements, and troubleshooting steps for teammates.
- The project owner confirmed that the iPhone Wi-Fi UDP path now works. USB/API device-to-PC operation still needs a separate hands-on check.
- Fixed the iPhone calibration UI overlap: the ninth bottom-right target was covered by the cancel button. Moved cancel above the target area.
- Expanded iPhone calibration from five targets to a 3×3 set of nine. The existing affine least-squares fit now uses all nine measured targets, improving screen coverage and reducing the influence of any single point; actual accuracy improvement still needs device observation.

### Gaze test: add iPhone transports alongside the webcam

- Updated `Assets/Scripts/GameController.GazeTest.cs` to let the user choose the existing UnitEye laptop webcam path or an iPhone path. Leaving the screen stops the selected tracker; switching input stops the previous provider.
- Updated `Assets/Scripts/GameController.cs` to release gaze resources on application quit.
- Added `Assets/Scripts/IIPhoneGazeSource.cs` as a shared sample interface for the two iPhone transports.
- Added `Assets/Scripts/IPhoneGazeUdpReceiver.cs`: sends VTube Studio iOS tracking requests to the entered phone address/port, listens on UDP 11125, drains packets on Unity's main thread, and exposes eye rotation and face status.
- Added `Assets/Scripts/VTubeStudioUsbApiBridge.cs`: connects to the local VTube Studio WebSocket API, obtains and keeps its plugin token only in memory, authenticates after user approval, and polls the four eye parameters at 30 Hz. Face presence is queried less often.
- Added `Assets/Scripts/GameController.IPhoneGaze.cs`: offers Wi-Fi UDP and USB/API setup panels, shares a nine-target least-squares calibration and on-screen marker between them, and remembers connection addresses/ports. The API token is not written to preferences.
- Updated the design and implementation-plan documents under `docs/superpowers/` to reflect the two transport paths and their setup requirements.
- This is a gaze visualization/calibration tool, not a validated precision eye tracker. USB/API and accuracy checks are still pending.

### Molecule assembly minigame

- Updated `Assets/Scripts/MoleculeBuilder.cs` so the prompt asks for a compound name rather than exposing its formula before the answer.
- Normalized assembled-formula display order and correction feedback, and changed ethanol's target formula label to `C2H6O` while retaining its atom counts.
- These changes were present in the working tree when documented; this entry does not infer their author.

### Design and project-context documents added

- Added `docs/superpowers/specs/2026-11-04-ai-room-recommender-minigame-design.md`, a design proposal for an explainable, offline room-ranking minigame. It is explicitly a review draft and is not implemented by this entry.
- Added `docs/superpowers/specs/2026-11-04-ai-room-gaze-input-design.md`, a gaze-selection proposal for the room-choice concept. It remains a draft; gaze hardware support does not mean the room-recommender minigame has been built.
- Added and updated `docs/AI/UnityProjectContext.md` with the current package inventory and gaze-tracking architecture.
- Added `docs/superpowers/plans/2026-11-04-iphone-gaze-tracking.md` and updated `docs/superpowers/specs/2026-11-04-dual-gaze-minigame-design.md` with the implemented scope and remaining device checks.

### Verification

- C# compilation using Unity 6000.3.23f1's generated compiler references passed. The only emitted warning was the existing unreachable-code warning in `Assets/Scripts/AiProfessor.cs`.
- No automated test suite, Unity Editor play session, physical iPhone connection, or VTube Studio end-to-end session was run for this entry.

## 2026-11-04 — Committed baseline `a22440d`

- Added the opt-in webcam gaze test to the title/entry screen, including UnitEye prefab/resources, the MediaPipe face-landmarker model, and the Unity Inference Engine/MediaPipe dependencies.
- Added calibration and a gaze marker in the existing IMGUI test screen, with camera permission handling and tracker teardown.
- Preserved third-party notices for the included UnitEye and MediaPipe components.

## Ongoing documentation rule

For future project work, update this file in the same change as the code, content, assets, or project settings. Record the date, user-visible behavior, important files, dependencies or setup, verification performed, and any checks still requiring the project owner's device. Label design drafts as proposals and do not describe them as implemented. Do not claim a test or build passed unless it was actually run.

## 2026-11-04 — Team setup guide in Word and PDF

- Added editable Word and shareable PDF versions of the iPhone gaze tracking setup guide under `docs/AI/`.
- The five-page guide covers prerequisites, Unity setup, Wi-Fi UDP, USB/API, nine-point calibration, troubleshooting, official references, and the distinction between confirmed Wi-Fi operation and USB checks still pending.
- Rendered the PDF with Microsoft Word and visually inspected all five pages. The bundled LibreOffice renderer was unavailable in this environment; the Word export and Poppler page render were used for review.
- Copied both deliverables to the project owner's Desktop for direct access.

## 2026-11-04 — Full-screen iPhone calibration

- Replaced the embedded per-point calibration controls with a dedicated full-canvas calibration view, matching the webcam flow's focus on the target.
- Kept the 3x3 targets and affine fit, and changed capture to wait 0.9 seconds then automatically sample for 1.5 seconds per target. This avoids a mouse click at each target and gathers more samples than the previous one-second capture; real-device accuracy improvement still needs validation.
- Updated the Markdown, Word, and PDF team setup guides with the timed flow and monitor setup guidance. Physical screen size is not an input to the fit, but small viewing angle, short target spacing, low resolution, or changing viewing position can affect practical results.
- Unity Editor and iPhone device verification have not been run for this update.
