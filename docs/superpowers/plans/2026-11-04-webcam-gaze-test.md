# Webcam gaze test implementation plan

**Goal:** Offer a temporary webcam gaze visualizer from the title screen.

**Architecture:** A GameController partial owns an opt-in UnitEye prefab instance, calibration and screen-space marker. Destroying the instance releases the camera. The existing IMGUI canvas draws the test screen.

**Tech Stack:** Unity 6000.3, UnitEye 1.1.0, MediaPipe Unity plugin, Unity Inference Engine.

**Spec:** `docs/superpowers/specs/2026-11-04-dual-gaze-minigame-design.md`

1. Install the pinned UnitEye and MediaPipe packages, their runtime dependencies, and the required face landmark model. Preserve third-party notices. Keep the existing legacy input handler; UnitEye calibration supports legacy input as a fallback.
2. Add an opt-in title-screen test with startup errors, face status, calibration, a pixel-to-canvas gaze marker, and explicit teardown. Disable UnitEye's CSV logger and debug overlays.
3. Validate package resolution and compile in Unity, review changes, and report whether real webcam calibration was exercised.
