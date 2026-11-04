# iPhone Gaze Tracking Implementation Plan

> **For agentic workers:** Implement this plan task by task in the current Unity project. The project owner asked to add the iPhone path alongside the webcam test.

**Goal:** Add VTube Studio iOS Wi-Fi UDP and USB/API input modes to the existing gaze test and map iPhone eye features to a laptop-screen gaze marker through nine-point calibration.

**Architecture:** Separate `IPhoneGazeUdpReceiver` and `VTubeStudioUsbApiBridge` components own the Wi-Fi UDP and VTube Studio desktop WebSocket transports. Both expose eye features through a shared narrow source contract. `GameController` owns provider lifetime, connection UI, nine-point calibration, and marker rendering. Existing UnitEye webcam behavior stays available as a separate mode.

**Tech Stack:** Unity 6000.3.23f1, C#, `UdpClient`, `ClientWebSocket`, `Task`, `ConcurrentQueue`, Unity `JsonUtility`/Newtonsoft; no new package or iOS build.

**Spec:** `docs/superpowers/specs/2026-11-04-dual-gaze-minigame-design.md`

## Global Constraints

- Keep the Unity app as a Windows laptop build and use the existing VTube Studio iOS app; no Mac/Xcode work.
- Keep the current webcam test selectable and working.
- The iPhone and laptop must share a local Wi-Fi network.
- The iPhone app must have `3rd Party PC Clients` enabled; the phone receives requests on its configured port (default 21412).
- The laptop listens on UDP port 11125 and periodically requests 2.5-second tracking windows.
- For USB, VTube Studio's iOS and PC apps must be paired over USB, the PC API must be enabled on WebSocket port 8001, and the user must approve the AI-ON API token request.
- USB reads `EyeLeftX`, `EyeLeftY`, `EyeRightX`, and `EyeRightY` from the VTube Studio input parameter API; poll at 30 Hz, not 60+ Hz.
- iPhone eye rotation is not screen gaze. Require nine calibration targets, evenly distributed as a 3×3 grid, before showing a calibrated marker.
- Close the socket and receiver thread when leaving the gaze test or quitting the app.

## Review Focus

- Malformed or future VTube Studio JSON fields must not crash the Unity main thread.
- Socket bind errors, packet loss, face loss, and stale packets must show useful state and hide the marker.
- Repeated connect/disconnect and screen exit must not leave a listener thread or UDP port open.
- Calibration must reject too few or degenerate samples and keep the marker within the canvas.

## Files

- `Assets/Scripts/IPhoneGazeUdpReceiver.cs`: send VTS tracking requests, receive UDP packets off the Unity main thread, parse the newest compatible payload on the main thread, expose current sample and connection status.
- `Assets/Scripts/VTubeStudioUsbApiBridge.cs`: connect to VTube Studio's local WebSocket API, request/remember the in-memory plugin token, authenticate, poll eye parameters, and expose a normalized sample/status.
- `Assets/Scripts/GameController.IPhoneGaze.cs`: own connection/settings, calibration capture and affine fit, and iPhone-mode UI.
- `Assets/Scripts/GameController.GazeTest.cs`: add webcam/iPhone mode selection, route the current marker and cleanup to the selected provider.
- `Assets/Scripts/GameController.cs`: call the iPhone gaze update and stop networking on application quit.
- `docs/superpowers/specs/2026-11-04-dual-gaze-minigame-design.md`: document the verified VTS transport and calibration flow.

## Tasks

1. Add the receiver component with a background UDP loop, one-second keepalive requests, a bounded per-frame packet drain, tolerant JSON parsing, and deterministic shutdown. Check the payload DTO against the VTube Studio example fields (`Timestamp`, `FaceFound`, `EyeLeft`, `EyeRight`).
2. Add the VTube Studio PC API bridge: request an authentication token with a recognizable plugin name, wait for the user's approval response, authenticate, and poll `InputParameterListRequest` at 30 Hz. Parse the four Eye parameters and surface unavailable parameters, API-off, auth-denied, timeout, and disconnect states.
3. Add Wi-Fi/USB transport tabs. Wi-Fi keeps the IP/port settings; USB explains that both VTS apps must be open, USB-connected, and the desktop API enabled. Do not persist the plugin token.
4. Add nine target positions in a 3×3 grid. For each target, collect at least 20 fresh samples over one second, average eye features, fit two least-squares affine transforms using all targets, and reject singular fits.
5. Route both iPhone transports through the same calibrated marker. Smooth small frame noise, clamp marker coordinates, and hide it when face tracking is lost or packets are older than one second.
6. Switch providers without leaving either transport active. Confirm webcam mode still follows its existing start/calibrate/exit path.
7. Inspect the final diff and compile with Unity's generated C# references if the Editor is available. Physical iPhone USB/Wi-Fi reception, VTS API approval, Windows driver setup, and real accuracy require the owner's devices and remain manual checks.
