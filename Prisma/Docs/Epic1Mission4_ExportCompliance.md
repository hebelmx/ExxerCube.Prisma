# Mission 4 — Export & Compliance Evidence (End-to-End Delivery)

Context: System test for delivery artifacts and compliance evidence generation. Reference: `docs/Legal/LegalRequirements_Summary.md`, `docs/Legal/ClassificationRules.md`.

Objective: Validate export readiness and evidence for SIRO XML, Excel FR18, and signed PDF (or explicit skip).

Scope (3–4 services): Storage (processed case) → Export Service (XML/Excel/PDF) → Audit → Observability.

Fixtures: Unified record from Missions 1–3 or seeded storage fixture.

Test contract:
- Exports produce SIRO XML and Excel layouts matching schema; signed PDF generated or skipped with rationale logged.
- Audit entries: ExportRequested → ExportGenerated → ExportSigned/Skipped with correlation id.
- Metrics capture export duration; no failures in happy path.
- Artifacts pass basic schema validation (XML) and non-empty checks (Excel/PDF).

Deliverables:
- System test invoking export, asserting artifacts, audit events, metrics counters, and stored evidence (export files + audit ids). No UI assertions.

Stories (parallelizable):
- Story A (Export specialist): Generate SIRO XML/Excel/PDF; unit tests for schema validation and signed-PDF optional path.
- Story B (Storage/Audit specialist): Record export artifacts and audit sequence (Requested/Generated/Signed); unit tests for audit persistence.
- Story C (Metrics specialist): Emit export duration/success counters; unit tests for metrics emission.
- Story D (Validation specialist): Basic schema/non-empty validation helpers; unit tests for validators.
- Story E (Demo page): Razor page listing generated exports with download links and audit ids; uses real artifacts.

Conventions (applies to this mission; do not skip):
- Solution/projects: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`; code in Domain/Application/Infrastructure.*; UI pages under `03-UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages`; system tests in `Tests.System.*`, unit tests in `Tests.Application` or relevant `Tests.*`.
- Key interfaces/classes: ExportService (XML/Excel/PDF), storage repo for export artifacts, audit logger, metrics service.
- Code style: C# 10, warnings-as-errors; SmartEnums need EF converters; PascalCase types/properties, camelCase locals/params; keep methods small/pure; avoid solution churn.
- Fixtures: use processed records from fixtures or prior missions; store under `Prisma/Fixtures` if adding.
- Telemetry: audit Requested/Generated/Signed, metrics for duration/success; assert artifacts/metrics, not UI.
- Observability & security: structured logging with correlation ids, performance timings, metrics for export throughput/failures; audit every export step; honor authentication/authorization for export actions.

Definition of Done:
- Build passes with warnings-as-errors, no new suppressions.
- System/unit tests pass; assertions are behavioral and stable.
- Razor demo page lists generated exports with downloads/audit ids and works end-to-end.
- Playwright/demo headed run confirms exports scenario (no UI assertions).
