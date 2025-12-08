# Mission 5 — Observability & Traceability Regression

Context: Cross-mission guardrail to ensure telemetry coverage for compliance and diagnosability. Reference: `docs/qa/Requirements.md`, `docs/SYSTEM_FLOW_DIAGRAM.md`.

Objective: Assert every pipeline step emits required telemetry and audit with consistent correlation.

Scope (3–4 services minimum): Any pipeline chain + Audit + Metrics + Storage.

Test contract:
- All steps write audit entries with shared correlation id.
- Metrics emit: latency per step, error counts, selected filters, classification confidence, SLA tags.
- Missing telemetry (audit or metrics) fails the test.

Deliverables:
- Reusable assertion helper for audit/metric completeness.
- Applied across Missions 1–4 as part of their system tests. No UI assertions.

Stories (parallelizable):
- Story A (Audit specialist): Ensure every step writes correlation-aligned audit; unit tests for audit completeness checks.
- Story B (Metrics specialist): Ensure required metrics (latency, errors, filters, confidence, SLA tags) emit; unit tests for metric presence.
- Story C (Helper library): Build reusable assertion helper for telemetry completeness; unit tests for helper logic.
- Story D (Integration enabler): Apply helper across Missions 1–4 system tests; adjust gaps revealed.
- Story E (Demo page): Razor page showing telemetry completeness status for last run (all green/any missing); informational only.

Conventions (applies to this mission; do not skip):
- Solution/projects: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`; code in Domain/Application/Infrastructure.*; UI pages under `03-UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages`; system tests in `Tests.System.*`, unit tests in `Tests.Application` or relevant `Tests.*`.
- Key interfaces/classes: audit logger, metrics service, helper/assertion library for telemetry completeness.
- Code style: C# 10, warnings-as-errors; SmartEnums need EF converters; PascalCase types/properties, camelCase locals/params; keep methods small/pure; avoid solution churn.
- Fixtures: reuse mission fixtures; no UI assertions.
- Telemetry: strict on correlation-aligned audit and required metrics; tests fail on missing telemetry.
- Observability & security: structured logging with correlation ids, performance timings, metrics for success/error/latency; audit coverage; honor authentication/authorization as needed.

Definition of Done:
- Build passes with warnings-as-errors, no new suppressions.
- System/unit tests pass; telemetry helpers assert behavior, not implementation; stable/non-flaky.
- Razor demo page shows telemetry completeness status and works.
- Playwright/demo headed run confirms telemetry demo (no UI assertions).
