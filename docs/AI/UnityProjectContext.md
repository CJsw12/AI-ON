# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:/Users/gimb4/Documents/GitHub/AI-ON`
- Summary: Korean campus romance visual novel with 19 minigames.
- Last analyzed: 2026-11-04
- Last analyzed commit: `a22440d` (the working tree contains additional changes; see [CHANGELOG.md](../../CHANGELOG.md))

## Confirmed Environment

- Unity version: 6000.3.23f1
- Render pipeline: Built-in Render Pipeline (`m_CustomRenderPipeline` is unset)
- Input system: Legacy Input Manager (`activeInputHandler: 0`); code also uses IMGUI events.
- Target platforms: No active build target confirmed from checked project files. iOS player settings exist, but no AR Foundation or ARKit package is declared.

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Runtime packages | UnitEye and MediaPipe Unity Plugin are local packages; Unity AI Inference 2.6.1, Input System 1.19.0, Newtonsoft JSON 3.2.1, TextMeshPro 3.0.6, and Unity audio modules are declared | Confirmed | `Packages/manifest.json`, `Packages/de.uniulm.uniteye`, `Packages/com.github.homuler.mediapipe` |
| Camera / vision | UnitEye and MediaPipe support the webcam gaze test. No OpenCV for Unity or AR Foundation/ARKit integration is declared | Confirmed | `Packages/manifest.json`, `Assets/Scripts/GameController.GazeTest.cs` |
| UI | IMGUI rendered by `GameController.OnGUI`; fixed 1280×900 minigame canvas | Confirmed | `Assets/Scripts/GameController.cs`, `MINIGAME_GUIDE.md` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scripts/` | Game controller, minigame classes, UI helpers, story data | Confirmed | Representative scripts and `README.md` |
| `Assets/Scenes/` | Single startup scene | Confirmed | `ProjectSettings/EditorBuildSettings.asset` |
| `Assets/Resources/` | Images, fonts, and audio loaded at runtime | Confirmed | `README.md`, `GameController.cs` |
| `Tests/` | PowerShell regression checks | Confirmed | `Tests/*.ps1` |
| `docs/superpowers/specs/` | Feature design drafts | Confirmed | Existing design documents |

## Assembly Boundaries

- No `.asmdef` or `.asmref` files found. Runtime scripts compile into Unity's default assemblies.

## Scenes And Startup Flow

- Build scene: `Assets/Scenes/Main.unity`
- Startup flow: the main scene hosts `GameController`, which draws intro, title, story, menus, and minigames.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Main application | `GameController` is a `MonoBehaviour` driving one scene and the IMGUI screen flow | Confirmed | `Assets/Scripts/GameController.cs` |
| Gaze test | Entry screen exposes webcam tracking and an iPhone test path; iPhone supports VTube Studio UDP over Wi-Fi and desktop API over USB streaming | Implemented in working tree; Wi-Fi confirmed by project owner, USB validation pending | `Assets/Scripts/GameController.GazeTest.cs`, `Assets/Scripts/GameController.IPhoneGaze.cs`, `Assets/Scripts/IPhoneGazeUdpReceiver.cs`, `Assets/Scripts/VTubeStudioUsbApiBridge.cs` |
| iPhone calibration | Both iPhone transports use nine screen targets and fit eye features to the test canvas; precision eye-tracking accuracy is not established | Implemented in working tree; nine-point UI fix not yet device-verified | `Assets/Scripts/GameController.IPhoneGaze.cs` |
| Minigames | Plain C# `MiniGame` subclasses are created through `MiniGames.Create` and rendered by the host | Confirmed | `Assets/Scripts/MiniGames.cs`, `MINIGAME_GUIDE.md` |
| Timing | `GameController.Update` advances `MiniGame.Now` only while menus/review are closed | Confirmed | `Assets/Scripts/GameController.cs`, `Assets/Scripts/MiniGames.cs` |
| Conventions | Global classes, feature-specific files, no scene/prefab/Canvas dependency for minigame classes | Confirmed | `MINIGAME_GUIDE.md`, representative scripts |

## Coding Conventions

- Namespace style: global namespace in inspected first-party scripts.
- Minigames: one `MiniGame` subclass per feature file; draw through IMGUI and use `NovelUI` helpers.
- Timing: use `MiniGame.Now`; settings pause it.
- Camera services would need a host component or equivalent bridge because minigame classes themselves are not `MonoBehaviour`.

## Testing And Validation

- EditMode / PlayMode tests: no Unity Test Framework package or test assemblies found.
- Regression checks: PowerShell scripts under `Tests/`.
- Build validation: no CI build command identified in inspected files.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity MCP/editor control | unavailable | No Unity-specific MCP tools surfaced in this session. |
| Repository inspection | available | Workspace filesystem and `rg` |

## Important Constraints

- Preserve the one-scene IMGUI flow unless a feature clearly requires an additional scene.
- Existing minigame guidance forbids direct `MonoBehaviour`, scene, prefab, Canvas, and external-asset dependencies in minigame classes.
- The gaze test uses explicit start, camera permission, calibration, and tracker cleanup. Webcam mode is implemented; iPhone USB/Wi-Fi still require device validation.
- The project contains a review-only AI room-recommender minigame design draft; treat it as unapproved unless the user approves it.

## Unknowns And Confidence

- Competition runtime target (Windows laptop, browser, Android, or iPhone) is unknown; this changes camera integration and should be confirmed before implementation.
- Exact active Build Profile was not available from the inspected serialized files.
- No Unity Editor connection/tooling was available to inspect live state.

## Source Files Inspected

- `README.md`
- `MINIGAME_GUIDE.md`
- `SESSION_HANDOFF.md`
- `Packages/manifest.json`
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `ProjectSettings/EditorBuildSettings.asset`
- `Assets/Scripts/GameController.cs`
- `Assets/Scripts/GameController.GazeTest.cs`
- `Assets/Scripts/GameController.IPhoneGaze.cs`
- `Assets/Scripts/IPhoneGazeUdpReceiver.cs`
- `Assets/Scripts/VTubeStudioUsbApiBridge.cs`
- `Assets/Scripts/MoleculeBuilder.cs`
- `Assets/Scripts/MiniGames.cs`
- `Assets/Scripts/MicroscopeFocus.cs`
- `docs/superpowers/specs/2026-11-04-ai-room-recommender-minigame-design.md`

<!-- unity-onboarding:generated:end -->
