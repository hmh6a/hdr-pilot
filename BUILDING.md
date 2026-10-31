# Build and package

Use Windows 11 x64 with the .NET 10 SDK from Microsoft. `global.json` selects 10.0.403 with latest-feature roll-forward. No trimming or Native AOT is enabled. Do not bypass PowerShell execution policy or Windows protections.

```powershell
dotnet restore HDRPilot.sln
dotnet build HDRPilot.sln -c Release
dotnet test HDRPilot.sln -c Release --no-build
dotnet publish src/HDRPilot.App/HDRPilot.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o artifacts/app
```

The development environment had no SDK, so a Microsoft-hosted SDK ZIP was SHA-512 verified against Microsoft's release metadata and extracted to `%LocalAppData%\HDRPilot\sdk`. `scripts/dotnet.ps1` uses this location when present and otherwise uses `dotnet` from PATH. It opts out of CLI telemetry. No PATH or machine-wide installation changes are needed. The application itself never emits telemetry.

Build a complete portable distribution with tests, native host, diagnostics, browser companion, docs and checksums:

```powershell
.\scripts\Package.ps1
```

Output: a fresh staging folder under `artifacts/`, plus `artifacts/HDRPilot-0.1.0-win-x64.zip` and `.zip.sha256`. `SHA256SUMS.txt` in the folder covers the executables. Keep the native host executable at the path registered with Chrome/Edge. Single-file runtime extraction is handled by .NET when necessary; do not enable trimming for WPF.

Run tests separately with a read-only real-hardware query enabled:

```powershell
$env:HDRPILOT_HW_READONLY = '1'
dotnet test tests/HDRPilot.Windows.Tests -c Release
```

HDR-write integration is intentionally an explicit Diagnostics invocation; it can blank the display briefly. Follow [hardware testing](docs/HARDWARE-TESTING.md). CI never changes a physical display. CI uploads unsigned build artifacts. Version tags publish tested unsigned portable releases. Authenticode signing and MSIX are not configured.

## GitHub Actions and releases

`.github/workflows/build.yml` restores locked dependencies, builds, tests, packages and verifies executable metadata and the packaged diagnostics ABI on every branch push, pull request and manual run. Artifacts are retained for seven days.

`.github/workflows/windows-release.yml` follows the separate build/publish pattern used by sahl-video. Push a version tag such as `v0.1.0` to build and test on Windows, verify the downloaded ZIP checksum, and publish a GitHub release with the ZIP and checksum. Tags with a suffix such as `v0.2.0-beta.1` create prereleases. Only the publish job has repository write permission; the built-in GitHub token is sufficient.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The project targets Windows display APIs and WPF, so the workflow builds Windows x64 only. macOS builds from the reference project do not apply.