# Architecture

The solution separates domain contracts from OS operations. `HDRPilot.Core` holds DTOs, profiles, interfaces and configuration validation. `Windows` implements display configuration, process snapshots, window events and per-user startup. `Engine` implements pure deterministic rules and a serialized ownership/transition state machine. `Infrastructure` owns atomic JSON persistence and bounded asynchronous logging. `GameDetection`, `VideoDetection`, and `BrowserBridge` isolate optional integrations. `App` is WPF/MVVM with dependency injection, tray integration and the background coordinator. `NativeHost` is a separate console executable for Chromium framing. `Diagnostics` provides explicit real-hardware probes.

## Evaluation and transitions

A bounded, coalescing channel combines foreground/location WinEvents with a one-second timer. There is one evaluation consumer; inputs cannot create unbounded work. Process snapshots run at most once every two seconds; executable paths are read with limited-information process handles. Enumeration handles are always closed. No WMI service dependency, continuous game-library scanning, or keyboard hook is used.

Rule order is suspended/global override, highest-priority eligible executable profile (GUID tie break), then optional browser video. Exact full paths avoid basename collisions. Window bounds are compared with physical monitor bounds; DWM cloaked windows are excluded. Fullscreen and borderless fullscreen are geometric classifications, not game-engine metadata. WhileRunning does not require focus. Minimized rules and missing windows are handled explicitly.

The engine stores a baseline, last observed state, ownership, manual override, pending target/deadline, release delay, and cooldown timestamp per stable display ID. Waiting uses deadlines rather than a blocking delay: another evaluation can cancel a pending transition. One semaphore serializes evaluations, manual requests, resets, and shutdown restoration. Native operations have a second gate. Pending → Activating/Deactivating → verified HDRActive/Idle is explicit; failures yield control rather than retrying continuously.

A normal manual button change deliberately drops ownership. Automatic resume is explicit. Force SDR only relinquishes a known HDR Pilot change. NeverModify suppresses changes while its rule wins. Topology signatures include stable monitor path, source name and adapter association. Missing/rebound paths discard leases. Suspend/resume uncertainty and query errors discard them too. No ownership record survives process death.

## Native API boundary

`GetDisplayConfigBufferSizes` + `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)` use bounded retries for insufficient buffers. Structures use Win32 32-bit BOOLs and bitfields, explicit mode union size, Unicode fixed buffers and tested native offsets. All P/Invoke DLL lookups are restricted to System32.

Windows 11 build 26100+ uses device info type 15, HDR capability bit 4, and `activeColorMode == 2`; writes use type 16. Legacy type 9/10 is limited to older builds and rejects WCG-enforced outputs. Do not fall back merely because a new API call fails: that could conflate WCG and HDR. Target IDs/LUIDs are re-resolved from a stable path immediately before writing. A precondition re-read rejects observed intervening changes. The native write is followed by bounded read-back polling even when shutdown cancellation arrives, allowing ownership to be recorded before cleanup.

Windows does not provide an atomic HDR compare-and-set or a writer identity. There remains a narrow external-change race; identical-state external writes cannot be detected. Conservative ownership reduces risk but cannot eliminate OS observability limits.

References: [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig), [Microsoft SDK header definitions](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/wingdi.h), [DisplayConfigSetDeviceInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfigsetdeviceinfo).

## Persistence and shutdown

Configuration writes use unique temporary files, flush-to-disk, and same-volume replace with backup. Inputs are bounded to 2 MiB, schema-checked and validated. Version 0 migrates to 1 with an explicit backup. Future schema files are never overwritten. Profiles can be missing on disk while disabled. Only a new matched process session updates LastPlayed.

The background log channel is bounded to 512 entries, the UI to 300, and disk logs to four approximately 1 MiB files. Log writer failures cannot prevent restoration. Graceful exit cancels/coalesces work, waits for in-flight writes, reads current displays, restores only owned and unchanged states, then disposes tray/hotkeys/hooks/services. A forced termination or Windows shutdown timeout may prevent restoration; recovery is conservative.
