# Mission 1 — Happy Path Intake → Extraction → Reconciliation → Storage

Context: System testing with real fixtures, chaining ingestion through storage with full telemetry. Reference: `docs/Legal/LegalRequirements_Summary.md`, `docs/Legal/ClassificationRules.md`, `docs/SYSTEM_FLOW_DIAGRAM.md`, `docs/qa/Requirements.md`.

Objective: Prove the core pipeline completes on well-formed CNBV XML + clean PDF, yielding unified metadata with audit/traceability.

Scope (3–4 services): Ingestion (XML+PDF fetch) → OCR/Extraction → Reconciliation → Storage/Audit.

Fixtures: PRP1 XML + matching clean PDF from `Prisma/Fixtures` (e.g., `222AAA-44444444442025.xml` + corresponding PDF).

Test contract:
- Ingestion emits correlation id, persists raw XML/PDF.
- OCR/Extraction confidence ≥ 0.9 for mandatory fields (`MandatoryFields_CNBV`).
- Reconciliation match rate ≥ 95% with zero critical conflicts.
- Storage writes unified record; audit entries include IntakeFetched, OcrCompleted, ReconcileCompleted, Stored (ordered, same correlation).

Deliverables:
- System test asserting correlation continuity, confidence/match thresholds, audit sequence, storage presence.
- Metrics/log evidence captured (Serilog/AuditLogger output). No UI assertions.

Stories (parallelizable):
- Story A (Ingestion specialist): Verify ingestion emits correlation id and persists raw artifacts; add unit tests for correlation propagation and storage writes.
- Story B (OCR/Extraction specialist): Ensure confidence thresholds on mandatory fields; add unit tests for parser/mapper; expose metrics hook.
- Story C (Reconciliation specialist): Enforce 95%+ match and zero critical conflicts; add unit tests for Levenshtein scoring and conflict classification.
- Story D (Storage/Audit specialist): Persist unified record and ordered audit events; add unit tests for audit pipeline.
- Story E (Demo page): Razor page to display run result summary (correlation id, match %, confidence, audit count) using service outputs; no assertions in tests.

Conventions (applies to this mission; do not skip):
- Solution/projects: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`; code in Domain/Application/Infrastructure.*; UI pages under `03-UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages`; system tests in `Tests.System.*`, unit tests in `Tests.Application` or relevant `Tests.*`.
- Key interfaces/classes to hook: ingestion/browser automation agents, `IOcrProcessingService`, `IProcessingMetricsService`, reconciliation/comparison services, storage repositories, `IAuditLogger`/`AuditLogger`, `MetricsService`.
- Code style: C# 10, warnings-as-errors; SmartEnums require EF converters; PascalCase for types/properties, camelCase for locals/params; keep methods small/pure where possible; no solution churn.
- Fixtures: use `Prisma/Fixtures` (add variants if needed under clear subfolders).
- Telemetry: emit Serilog, metrics, and audit with consistent correlation ids; tests assert behavior/payloads, not UI.
- Observability & security: structured logging with correlation ids, performance timings, metrics for success/error rates; audit every step; honor authentication/authorization where applicable.

Definition of Done (mission-specific):
- Build passes with warnings-as-errors, no new suppressions.
- System and unit tests pass; assertions target behavior, not implementation details; no flakiness.
- Razor demo page works for this mission scenario.
- Playwright/demo run in headed mode shows the flow working (no UI assertions needed).
