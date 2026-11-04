# iPhone Calibration Screen Design

## Intent

Give iPhone gaze calibration its own full-screen Unity view, like webcam calibration, so the target is the only prominent visual focus and the user does not need to click a capture button at every point.

## Goals

- Replace the embedded calibration controls with a dedicated 3x3 calibration view inside the existing Unity game window.
- Show one large, high contrast target at a time, with clear progress and cancel/restart controls.
- Let the user hold their gaze while each target settles and samples automatically for longer than the current one-second window.
- Keep both Wi-Fi UDP and USB/API inputs on the same calibration flow.
- Keep the existing 9-point affine mapping and avoid new packages or scene changes.

## Non-goals

- Open a second native Windows window or change display topology.
- Claim precision eye-tracker accuracy; the source is still VTube Studio face/eye motion data.
- Change the webcam UnitEye calibration implementation.

## Approaches

1. **Full-screen Unity calibration view with timed capture (chosen).** Reuses the current IMGUI canvas and gaze test lifecycle. It gives the calibration target the whole game view and removes per-target mouse clicks that can interrupt fixation.
2. **Native second OS window.** Rejected because the project uses a single-scene IMGUI screen flow, and a second window complicates mapping gaze to the game display.
3. **Keep calibration embedded in the connection panel.** Rejected because connection controls and status compete with calibration focus, and the user specifically asked for a separate view like webcam calibration.

## Behavior

Selecting 9-point calibration replaces the normal gaze test screen with a full-canvas calibration view. A target appears at each existing 3x3 coordinate. After a short settling interval, the app samples the active iPhone eye feature for 1.5 seconds, then advances automatically. The UI shows point count, capture progress, current status, and a cancel control. When all points are collected, the existing affine fit runs and the test screen returns with the calibrated marker.

Both iPhone transports share this state machine. Loss of usable tracking cannot contribute stale samples; insufficient fresh samples cause the current calibration attempt to restart with a visible status message.

## Monitor size guidance

Calibration maps tracking features to normalized positions on the active Unity canvas, so calibration must be done on the display and at the resolution/window size used for play. Physical display size is not a direct quality setting in this implementation, but a smaller display at the same viewing distance subtends a smaller angle and places targets closer together in the user's view. This can make target fixation and feature separation harder when the source is noisy. Keep viewing distance, device position, lighting, resolution, and window size stable after calibration; recalibrate after changing them.

## Constraints and verification

- Preserve the fixed 1280x900 IMGUI coordinate convention and the single-scene flow.
- Do not add packages, serialized fields, scenes, or prefabs.
- Document the code change, team steps, and the unverified physical-device accuracy.
- Verify source diff and Unity compilation if the project environment permits; no automated test suite is added for this UI change.
