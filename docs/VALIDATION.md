# Validation record — 2026-10-29

## Windows installer verification — 2026-10-31

- Version 0.1.2 builds with 0 warnings and 0 errors; 62 unit tests passed and the opt-in hardware query was skipped.
- The Setup EXE installed into a custom path containing spaces and Arabic characters. The installed application hash matched the standalone executable; browser/native-host files, diagnostics, Start menu shortcut and uninstall registration were present.
- Reinstalling without a destination override reused the selected folder. Uninstall removed the installed program and registration while preserving an extra user-created file and the existing application settings.
- The Arabic wizard was opened and its editable destination field and Browse button were visually verified. The interactive preview was cancelled; no additional installation was made.

## HDR Pilot rename verification — 2026-10-31

- Release build: 0 warnings and 0 errors. Tests: 62 passed, 0 failed, 1 opt-in hardware test skipped.
- Locked dependency restore and self-contained packaging succeeded. Packaged executable product metadata identifies HDR Pilot 0.1.0; packaged Diagnostics reports the expected ABI layout.
- The published application launched successfully. Window title, dashboard branding and P icon were inspected; the repository dashboard image was refreshed from the running application.
- The earlier hardware results below are retained from the implementation before the branding change. No additional HDR-write hardware certification is implied by the rename.

## Earlier implementation checks

Environment: Windows 11 Pro build 26300, x64. .NET SDK 10.0.403. NVIDIA GeForce RTX 5070 driver 32.0.16.1742; an AMD Radeon integrated adapter is also installed. One active LC32G7xT display, 2560 × 1440 at 239.958 Hz, driven by NVIDIA. Initial display state was SDR.

## Completed checks

- Release solution build: **0 warnings, 0 errors**.
- xUnit: **63 passed, 0 failed** when the opt-in real-display query is enabled: Core 36, Engine 24, Windows 3. Without that environment variable, 62 pass and the single hardware query is explicitly skipped.
- Guided game setup tests cover helper exclusion, directory boundaries, deep live executable discovery, ambiguous candidates, detached profile edits, defaults, duplicate prevention, atomic save/reload, recently played preservation, and optional automation resumption.
- The packaged Games page automatically discovered 14 installations. The Arabic setup dialog selected `Game\DarkSoulsIII.exe` without a file picker and displayed the automatic screen option. The dialog was cancelled without creating a user profile. Save/reload and duplicate prevention were exercised using isolated test settings; no game was launched for this UI check.
- Notification regressions cover independent secondary-display changes while primary HDR stays enabled, simultaneous changes, mode changes, startup/reconnect/reorder suppression, discarded notifications, and deduplicated errors.
- After the second display was connected, live discovery and the running UI showed MPG321UX OLED (3840 × 2160, 239.99 Hz, primary) and LC32G7xT (2560 × 1440, 239.958 Hz). This verifies two-display enumeration, not full multi-monitor transition or hotplug certification.
- Native ABI sizes: Header 20, Path 72, Mode 64, ColorInfo2 36, SetHdr 24, TargetName 420; matched expected SDK layouts.
- Real manual round trip: Windows HDR **OFF → ON → OFF**, both writes returned success and matching read-back.
- Real production-engine profile round trip: matched the running HDR Pilot executable and its real visible window, enabled HDR, then simulated process removal from the rule snapshot and restored SDR. Each final state was queried from Windows. This is not a game-specific native-HDR validation or an actual game-exit event test.
- Desktop UI launched, dark dashboard/display data inspected, navigation and read-only discovery exercised, running-application list populated, native file picker opened/cancelled, close-to-tray and About-page graceful exit exercised. Snapshot captured in assets/dashboard.png. A duplicate Steam path representation and an initial layout/style defect were found and corrected.
- Steam and accessible Xbox installations were discovered on the development PC. No launcher or game files were changed.
- Chromium companion JavaScript syntax checks passed. Strict schema, duplicates/oversize/truncation, framed roundtrip, and same-user pipe disconnect tests passed. The extension itself was **not installed or end-to-end browser-tested**.
- NuGet's vulnerability check, including transitive packages, reported no known vulnerable packages at check time. This is not a comprehensive security audit.
- No TCP connection was found for the sampled app process. Source review found no application network client or shell command execution; the NVIDIA help action opens a fixed user-selected HTTPS URL.
- The default Win+Alt+P shortcut conflicted on this PC and produced a visible log warning. Other available commands remain usable. Global key injection was not used to test shortcut execution.
- Published `HDRPilot.exe` ran for the 20-second smoke test and exited with code 0 with `DOTNET_ROOT` pointed to a nonexistent runtime directory. Published Diagnostics enumerated the real display and reported the expected ABI without using the development SDK. A second invocation reactivated the existing tray instance instead of starting a second engine.

## Resource measurements

Measured from process CPU-time deltas divided by wall time and logical processor count. These are short local samples, not benchmarks or leak/soak certification.

| Sample | Duration | Total CPU | Working set | Private memory |
| --- | ---: | ---: | ---: | ---: |
| Visible window, early build | 20.01 s | 0.039% | 182.5 MiB | 110.8 MiB |
| Tray only, after UI/discovery/picker exercises | 30.02 s | 0.036% | 244.9 MiB | 158.3 MiB |
| Self-contained portable, tray only after 10 s warm-up | 60.02 s | 0.081% | 169.4 MiB | 112.8 MiB |

The first tray sample's handles changed from 1126 to 1129. The fresh portable sample changed from 761 to 755 with 20 threads. Active UI inspection produced higher transient CPU values (up to approximately 0.85% in the dashboard's ten-second samples). Memory includes WPF UI/native resources loaded during browsing. These measurements should not be presented as a universal low-memory guarantee.

## Not validated physically

Full multi-monitor transition coverage, AMD/Intel-driven HDR transitions, Windows 11 legacy APIs, repeated hotplug, sleep/wake, GPU resets, protected Store discovery, full browser installation, advanced video metadata, multi-day memory stability, high-DPI/accessibility coverage, or signed release/update infrastructure. Engine simulations cover many corresponding policy cases but do not establish hardware compatibility. Follow HARDWARE-TESTING.md before expanding support claims.

The application executable is approximately 165.6 MiB; the ZIP is approximately 131.6 MiB including two self-contained helper executables. `SHA256SUMS.txt` and the adjacent ZIP digest cover the delivered files. Authenticode status is NotSigned. The application retains the limitations listed in README.md.
