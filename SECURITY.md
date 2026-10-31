# Security model

HDR Pilot runs as the current user. It has no telemetry, analytics SDK, advertising, remote command API, cloud backend, silent updater, service, driver, or elevation request. The application makes no network requests during normal operation. Opening the explicitly selected NVIDIA help link uses the user's browser; developer restore/build uses NuGet/Microsoft distribution endpoints.

## Input and OS boundaries

- Browser messages are data only: protocol version, playback flag, fullscreen flag, range enum, browser enum. No URLs, titles, cookies, authentication data, file paths, shell commands, or executable names are accepted from the companion.
- Chromium's native host manifest lists exact extension origins. The native host rechecks its caller origin against the per-user manifest. Installation is explicit and per-user. No broad-site host permissions are requested.
- The named pipe uses `PipeOptions.CurrentUserOnly` on server and client, a user- and Windows-session-specific name, four bounded listeners, a 1 KiB message cap before allocation, strict fields/duplicate rejection, max JSON depth, a 400 ms rate floor, connection timeouts, and 12-second signal expiry. No localhost TCP/HTTP endpoint exists.
- **Same-user code is within the trust boundary.** A malicious process running as this Windows user can imitate video signals or alter user configuration/host registration. Caller-origin arguments are not a cryptographic identity. CurrentUserOnly isolates users, not mutually hostile applications in one account. Browser requests can only request conservative Windows HDR policies; they cannot execute code. Four-slot denial-of-service by same-user malware is not prevented.
- Every native HDR write is serialized, precondition-checked and read back. Display addresses are re-resolved from stable IDs; unknown identity disables writes. Native DLL search is restricted to System32. No DLL plugins or launcher executables are loaded.
- JSON, manifests, and browser messages are bounded. Configuration imports validate types, modes, absolute executable paths, delays, priorities and shortcut masks. No imported path is executed.
- Logs omit window titles and URLs. They can contain application/profile names, error messages and monitor names. Settings contain local executable paths. Diagnostics exports are local; review before sharing.

## Registry and files

Optional startup uses only `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\HDRPilot`. Optional native host registration uses `HKCU\Software\Google\Chrome\NativeMessagingHosts\org.hdrpilot.video` and `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\org.hdrpilot.video`. Launcher/uninstall registry access is read-only. No NVIDIA registry values are written. Config/log/host-manifest paths are documented in the README; Windows user-profile ACLs provide their baseline isolation.

## Releases and updates

No official repository or publisher certificate was supplied. The update feature is deliberately disabled, including periodic checks. Releases are unsigned local artifacts. SHA-256 verifies integrity against a known digest, not publisher identity; a co-hosted checksum cannot authenticate a compromised release.

Before enabling future updates, maintainers must configure a fixed official HTTPS repository and trusted signing identity, verify Authenticode chain/revocation and publisher identity (or an authenticated signed manifest with a pinned key), compare versions, require explicit approval, stage/verify before activation, and retain a rollback copy. Do not build an auto-installer around release-page checksums. Signing keys must not be stored in the repository or embedded in the application.

## Reporting

This local project has no configured public security contact or hosting repository. Report findings privately to its maintainer through an established trusted channel. Do not publish secrets or personal logs. Include version, Windows build, affected component, and minimal steps with redacted diagnostics. No promise of a supported public release channel is implied.
