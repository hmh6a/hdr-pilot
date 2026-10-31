# Reproducible hardware validation

Back up settings. Close other HDR automation tools. Use the local console session. Record Windows build, GPU/driver, monitor, connector, display topology and initial HDR state. Changing HDR can briefly blank the display; do not run during critical work.

1. Run `HDRPilot.Diagnostics.exe --abi`. Expected x64 sizes: Header=20, Path=72, Mode=64, Color2=36, SetHdr=24, TargetName=420.
2. Run `HDRPilot.Diagnostics.exe` to enumerate active displays. Compare capability, state, resolution, refresh and primary monitor with Windows Settings. Device IDs must remain stable across application restarts.
3. Explicit round trip: `HDRPilot.Diagnostics.exe --roundtrip 'EXACT_DISPLAY_ID'`. It flips state, verifies it, waits, then restores in a finally block using an expected-state precondition. Observe the image and Windows Settings. It will not overwrite an observed different external state during cleanup. The reported ActualState is an API read-back, not photometric validation.
4. With a harmless, visible application running on an initially SDR display, use `HDRPilot.Diagnostics.exe --profile-roundtrip 'C:\Exact\TestApplication.exe'`. This uses real process/window snapshots and the production rule/ownership engine, then simulates removal of the process from the next snapshot and verifies restoration. It does not terminate the test application.
5. For full lifecycle testing, create a profile in the GUI for a harmless test program, start/exit it, and verify delays, active rule, event log, and restored state. Repeat with HDR initially on, manual changes mid-session, competing priorities and minimized/foreground/fullscreen windows.
6. On two physical displays, move the window between them, configure a preferred display, unplug/reconnect, sleep/wake, and switch topology in Windows. Old ownership should be dropped when uncertain. Do not expect old crash ownership to be restored.
7. Use Windows' normal driver recovery procedures only if needed in a controlled test. Do not intentionally crash a production GPU driver. Unit tests simulate failures; this is not physical-reset evidence.
8. Test explicit tray Exit during pending and active transitions. Confirm that closing the window only hides it. Force termination is a separate crash test: next launch must preserve current state.
9. Install the companion manually in each target browser and exercise playing, paused, ended, navigation, inactive tabs, window focus, browser closure and both range classifications. Unknown classification must never enable HDR.
10. Sample total process CPU time delta / wall time / logical processor count, working set, private memory and handles over at least 60 seconds after warm-up. Include tray-only and visible-window samples. Short development measurements are not a soak test.

Stop and retain redacted diagnostics on any unexpected state. Do not claim multi-vendor compatibility from a mocked test suite or one physical monitor.
