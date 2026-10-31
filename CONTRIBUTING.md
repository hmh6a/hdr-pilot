# Contributing

Keep Windows calls in `HDRPilot.Windows`, decisions in the engine, and UI state in view models. Add meaningful regression tests for safety-sensitive changes, especially ownership, shutdown and browser validation. Do not present mocks as hardware evidence. Document exact hardware and driver versions used for manual validation.

Run the Release build and all xUnit tests before submitting a change. Never add telemetry, broad browser permissions, undocumented driver writes, elevation, shell execution of external strings, or unverified update installation. New dependencies need a clear reason. Observe existing naming, nullable annotations and cancellation behavior. Preserve stable display identities and read-back verification.

Maintain docs/VALIDATION.md for measured results and known gaps. Verify UI at 100%, 150%, and 200% scaling and with keyboard navigation before calling it broadly production-ready. Do not commit private profiles, logs, local SDKs, certificates, or build output.
