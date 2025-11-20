# Style & Conventions
- .NET 10 everywhere (`Directory.Build.props` sets `<TargetFramework>net10.0</TargetFramework>`); nullable reference types enabled and `TreatWarningsAsErrors` forces clean builds.
- `.editorconfig` currently relaxes IDE0058 (expression-value unused) to suggestions, but otherwise rely on default C# conventions; new code should follow idiomatic C# 12 naming plus XML doc comments for public APIs per README guidance.
- Domain/application layers follow Hexagonal Architecture and Railway-Oriented Programming: propagate errors via `Result<T>` instead of exceptions, keep side effects at the boundaries, and preserve async/await flows.
- Logging/telemetry expected to be structured with correlation IDs; new features should extend existing instrumentation rather than ad-hoc logging.
- Testing culture: maintain 80%+ unit coverage, add integration/e2e coverage when touching infrastructure or UI, and keep warnings clean (warnings-as-errors enforced).
- When interacting with Python modules, respect the established integration helpers in `Infrastructure.*` rather than adding direct `Process` calls or unmanaged interop.