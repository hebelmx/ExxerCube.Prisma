# Mission 6 — Extreme Case: Schema Drift & Template Adaptation

Context: System test for adaptive behavior when CNBV XML schema or bank template changes without code changes. Reference: `docs/SYSTEM_FLOW_DIAGRAM.md`, `docs/Legal/ClassificationRules.md`.

Objective: Validate schema drift handling and template adapter resilience while preserving required fields and traceability.

Scope (3–4 services): Ingestion (schema drift) → Template Adapter → Reconciliation → Storage → Audit.

Fixtures: XML with added/renamed field (schema drift) + bank template variant (layout change) from `Prisma/Fixtures` extensions.

Test contract:
- Template adapter detects schema change, maps known fields, logs adapter event.
- Non-mapped fields logged as warnings; required fields still populated or explicitly flagged (not silent).
- Audit entries: SchemaChangeDetected + TemplateAdapterApplied (correlated).
- Storage persists adapted record with warning flags.

Deliverables:
- System test applying drifted XML and template variant, asserting adapter events, warnings, and successful storage with flags. No UI assertions.

Stories (parallelizable):
- Story A (Adapter specialist): Detect and map schema drift; unit tests for field mapping and warning generation.
- Story B (Reconciliation specialist): Handle adapted fields and flag non-mapped required fields; unit tests for warning/flag propagation.
- Story C (Storage/Audit specialist): Persist adapted record with warning flags; audit SchemaChangeDetected + TemplateAdapterApplied; unit tests for audit sequence.
- Story D (Fixtures specialist): Create drifted XML + template variant fixtures; unit tests to ensure fixtures load and differ as expected.
- Story E (Demo page): Razor page showing adapter event log and flags for a drifted case; purely informational.

Conventions (applies to this mission; do not skip):
- Solution/projects: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`; code in Domain/Application/Infrastructure.*; UI pages under `03-UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages`; system tests in `Tests.System.*`, unit tests in `Tests.Application` or relevant `Tests.*`.
- Key interfaces/classes: schema/template adapter, reconciliation service, storage repo, audit logger.
- Code style: C# 10, warnings-as-errors; SmartEnums need EF converters; PascalCase types/properties, camelCase locals/params; keep methods small/pure; avoid solution churn.
- Fixtures: place drifted XML/template variants under `Prisma/Fixtures` with clear naming.
- Telemetry: audit SchemaChangeDetected and TemplateAdapterApplied with correlation; metrics/log warnings for non-mapped fields; assert behavior/telemetry, not UI.
- Observability & security: structured logging with correlation ids, performance timings, metrics for adapter hits/warnings; audit drift detection/apply; honor authentication/authorization where applicable.

Definition of Done:
- Build passes with warnings-as-errors, no new suppressions.
- System/unit tests pass; assertions are behavioral and stable.
- Razor demo page shows adapter events/flags and works end-to-end.
- Playwright/demo headed run confirms drift scenario (no UI assertions).
