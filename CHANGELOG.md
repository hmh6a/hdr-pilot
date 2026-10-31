# Changelog

## 0.1.2 — 2026-10-31

Added a Windows Setup EXE with a visible installation-folder picker, Start menu shortcut, optional desktop shortcut and Apps & features uninstaller. Installation is per-user and includes the browser companion and diagnostics. Updates and uninstallations ask the user to exit a running application; uninstall preserves settings and removes only this installation's startup entry.

## 0.1.1 — 2026-10-31

Added a directly downloadable self-contained Windows EXE and its SHA-256 checksum to GitHub Releases and CI artifacts. The full ZIP remains available for the optional browser companion and diagnostics. The UI now displays the actual build version.

## 0.1.0 — 2026-10-31

First HDR Pilot release: renamed the application, assemblies, solution, browser host, storage paths and documentation; updated the P lettermark and dashboard screenshot. Added Windows validation and tag-triggered GitHub Releases with locked dependencies, packaged-executable checks and SHA-256 verification.

Initial independent implementation: native per-monitor HDR control and verification, conservative ownership engine, application profiles, read-only launcher discovery, WPF dark UI, tray/hotkeys/startup, JSON persistence, bounded logs, YouTube native messaging companion, diagnostics, xUnit tests and portable packaging.

Notifications now track each monitor's observed HDR state independently, report mode changes and failures, and avoid startup/reconnection noise. Settings includes an enable/test notification button; manual status messages identify the display and verified result.

Game setup now has an Arabic guided flow, automatic library discovery on opening Games, per-game setup buttons and profile status, and a live-window picker. Single executable candidates are selected automatically; ambiguous files require a choice or live detection. Advanced controls remain optional. Repeated setup reuses existing profiles and preserves their settings.

Known release gaps: no automatic YouTube HDR classification, advanced player metadata integration, signed updates, MSIX, or broad hardware certification. See README and validation report. Portable releases are unsigned.
