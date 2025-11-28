# Repository Guidelines

## Project Structure & Modules
- Core domain models and contracts: `Prisma/Code/Src/CSharp/Domain` (entities, value objects, interfaces).
- Application layer: `Prisma/Code/Src/CSharp/Application` orchestrates use cases and pipelines.
- Infrastructure: `Prisma/Code/Src/CSharp/Infrastructure.*` (Classification, Extraction, Imaging, Database, Export, FileStorage, Metrics, Python adapters).
- UI: `Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI`.
- Tests: `Prisma/Code/Src/CSharp/Tests.*` (unit, integration, system). OCR fixtures live under `Tests.Infrastructure.Extraction.Teseract/Fixtures`.
- Docs and due‑diligence notes: `docs/qa` (e.g., `MustDoTask.md`, `Domain_Legal_CodeReview.md`).

## Build, Test, and Development Commands
- Restore/build solution: `dotnet restore` then `dotnet build Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`.
- Build a single project: `dotnet build <project>.csproj` (useful for fast inner loops).
- Run focused tests: `dotnet test <project>.csproj --filter "<Trait|FullyQualifiedName>"` to avoid long E2E suites; system tests can take ~1 hour.
- Frontend dev server: `dotnet run --project Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI`.

## Coding Style & Naming
- C# 10, `Nullable` enabled, `TreatWarningsAsErrors=true`. Prefer explicit null checks over nullable annotations when in doubt.
- Use SmartEnum/typed identifiers for domain enums; avoid magic numbers/strings.
- Method/prop names in PascalCase, locals/params in camelCase; async methods suffixed with `Async`.
- Keep XML documentation on public APIs and meaningful inline comments only where intent is non‑obvious.

## Testing Guidelines
- Unit tests live beside feature area projects; system fixtures under `Tests.System` and OCR fixtures under `Tests.Infrastructure.Extraction.*`.
- Follow Arrange/Act/Assert; name tests `<Method>_<Scenario>_<Outcome>`.
- Add positive, negative, and edge cases for validation rules (e.g., RFC/CURP, account formats, name conflicts).
- Prefer deterministic data; when using OCR fixtures, keep originals + sanitized outputs for traceability.

## Commit & Pull Request Guidelines
- Commit messages: short imperative summary (e.g., `Add name matching policy`, `Fix OCR sanitization logging`).
- Pull requests should include: scope/intent, key changes, testing performed (`dotnet test ...`), and any schema/config migrations.
- Link to related docs in `docs/qa` and cite legal/spec sources when changes are driven by regulation.
- Screenshots/GIFs for UI changes; sample payloads for API/ingestion changes.

## Security & Configuration Tips
- Keep secrets out of repo; use user secrets or environment variables.
- Validate untrusted OCR/XML inputs; flag conflicts rather than dropping data.
- Configuration: `appsettings.*.json` for options (e.g., `MatchingPolicy`, `NameMatching`, polynomial model); default-safe values should allow local runs without external services.
