# Mission 2 — Degraded PDF Resilience (Filters + OCR Adaptation)

Context: System test chaining imaging and OCR on noisy/blurred PDFs to prove adaptive filters improve outcomes. Reference: `docs/Legal/LegalRequirements_Summary.md`, `docs/SYSTEM_FLOW_DIAGRAM.md`.

Objective: Validate adaptive imaging raises OCR quality and preserves mandatory fields under degraded input.

Scope (3–4 services): Ingestion → Image Quality Analyzer (filters) → OCR → Metrics/Storage.

Fixtures: Low-quality PDF (noise/blur/watermark) + baseline XML from `Prisma/Fixtures` (degraded variant).

Test contract:
- Quality analyzer selects non-default filter; selection recorded in metrics.
- OCR confidence improves vs. unfiltered baseline by ≥ 10% relative or hits ≥ 0.7 absolute.
- Mandatory fields present or explicitly flagged (warnings, not fatal).
- Storage keeps raw and enhanced images; audit notes filter choice.

Deliverables:
- System test running filtered vs. unfiltered (sequential) asserting confidence delta and flag behavior.
- Metrics snapshot showing filter selection and uplift. No UI assertions.

Stories (parallelizable):
- Story A (Imaging specialist): Ensure quality analyzer picks non-default filters; unit tests for filter selection logic.
- Story B (OCR specialist): Validate confidence uplift vs baseline; unit tests for confidence calculation/thresholding.
- Story C (Storage/Audit specialist): Persist raw/enhanced images and audit filter choice; unit tests for dual artifact persistence.
- Story D (Metrics specialist): Emit filter selection and confidence delta; unit tests for metric emission.
- Story E (Demo page): Razor page showing before/after confidence and selected filter name; uses real run output, not asserted by tests.

Conventions (applies to this mission; do not skip):
- Solution/projects: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`; code in Domain/Application/Infrastructure.*; UI pages under `03-UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages`; system tests in `Tests.System.*`, unit tests in `Tests.Application` or relevant `Tests.*`.
- Key interfaces/classes: image quality analyzer/filter selector, OCR service, metrics service, storage repo for artifacts, audit logger.
- Code style: C# 10, warnings-as-errors; SmartEnums need EF converters; PascalCase types/properties, camelCase locals/params; keep methods small/pure; avoid solution churn.
- Fixtures: use `Prisma/Fixtures`; add degraded variants in clear subfolders.
- Telemetry: emit filter choice, confidence delta, correlation-aligned audit; assert behavior/metrics, not UI.
- Observability & security: structured logging with correlation ids, performance timings, metrics for success/error rates; audit filter selection; honor authentication/authorization where applicable.

Definition of Done:
- Build passes with warnings-as-errors, no new suppressions.
- System/unit tests pass; assertions are behavioral and stable (non-flaky).
- Razor demo page shows before/after confidence and filter; works end-to-end.
- Playwright/demo run in headed mode confirms scenario works (no UI assertions).
- Structured logging/metrics emitted (filter choice, improvement %, timings) and verified in tests; audit entries recorded for filter selection if available.
