# iPhone Calibration Screen Implementation Plan

> **For agentic workers:** Follow this plan task-by-task in the current Unity project.

**Goal:** Give iPhone gaze setup a dedicated full-screen calibration view with automatic longer sampling at all nine targets.

**Architecture:** Keep calibration state in `GameController.IPhoneGaze.cs` and switch presentation from `GameController.GazeTest.cs` when calibration is active. Both iPhone transports continue to feed the same calibration and affine-fit path.

**Tech Stack:** Unity 6000.3.23f1, C#, existing IMGUI/NovelUI.

**Spec:** `docs/superpowers/specs/2026-11-04-iphone-calibration-screen-design.md`

## Global Constraints

- Preserve the fixed 1280x900 canvas and single-scene IMGUI flow.
- Keep Wi-Fi UDP and USB/API on the shared iPhone calibration path.
- Keep nine existing target coordinates and the existing affine transform.
- Do not add a package, scene, prefab, or serialized setting.
- Do not claim device accuracy improvement until checked on the actual iPhone.

## Review Focus

- Tracking disconnects during a target must not reuse stale samples.
- A calibration retry or cancel must stop timed capture and not leave the calibration overlay stuck.
- Both Wi-Fi and USB/API selections must show the same calibration view.
- The target and progress controls must remain visible at small game-window sizes.
- Recalibration after changing display or seating setup must be clear in team documentation.

---

### Task 1: Dedicated calibration state and view

**Files:**
- Modify: `Assets/Scripts/GameController.IPhoneGaze.cs`
- Modify: `Assets/Scripts/GameController.GazeTest.cs`

- [x] Route iPhone calibration to a full-canvas calibration renderer before the connection/test panel is drawn.
- [x] Use a 0.9-second settle period followed by 1.5 seconds of fresh sequence samples at each target; automatically advance after sampling.
- [x] Render a prominent target, 3x3 progress, capture progress, status, and cancel action; remove per-point capture clicks.
- [x] Preserve shared calibration flow for Wi-Fi UDP and USB/API, and keep cancel/retry lifecycle state consistent.

### Task 2: Update documentation and verify

**Files:**
- Modify: `docs/AI/iPhone-Gaze-Tracking-Setup.md`
- Modify: `docs/AI/UnityProjectContext.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/AI/iPhone-Gaze-Tracking-Setup.docx`
- Modify: `docs/AI/iPhone-Gaze-Tracking-Setup.pdf`

- [x] Explain automatic dwell sampling and keeping the same display, viewing distance, and window size after calibration.
- [x] Render and inspect the five-page Word/PDF deliverables after editing.
- [x] Run `git diff --check` (no whitespace errors); a fresh Unity compilation and physical-device accuracy check remain outstanding.
