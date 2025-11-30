# Mission 3 — Missing/Conflicting Data → Manual Review Path

Context: System test for reconciliation and routing to manual review when data is incomplete/conflicting. Reference: `docs/Legal/LegalRequirements_Summary.md`, `docs/Legal/ClassificationRules.md`, `docs/SYSTEM_FLOW_DIAGRAM.md`.

Objective: Prove conflicts trigger manual review with SLA tagging and full traceability.

Scope (3–4 services): Ingestion → OCR/Extraction → Reconciliation → Classification → Manual Review queue → Audit.

Fixtures: XML missing key fields (e.g., `NumeroOficio`, `AutoridadNombre`) + PDF with partial data; optional DOCX free-form variant from `Prisma/Fixtures`.

Test contract:
- Reconciliation flags required-field gaps per `MandatoryFields_CNBV` and conflicting values.
- Classification confidence below threshold yields `RequiresReviewReason = LowConfidence` or `ExtractionError`.
- Manual Review queue entry created with SLA tag (High/Urgent per legal timelines); includes reconciliation report link.
- Audit trail records conflict detection and enqueue with shared correlation id.

Deliverables:
- System test asserting review case creation, SLA tag, audit sequence, and stored reconciliation report artifact. No UI assertions.

Stories (parallelizable):
- Story A (Reconciliation specialist): Detect missing/conflicting required fields; unit tests for rule coverage per `MandatoryFields_CNBV`.
- Story B (Classification specialist): Apply classification rules and confidence thresholds; unit tests for rule outcomes and review reasons.
- Story C (Manual Review specialist): Create queue entries with SLA tags and attach reconciliation report; unit tests for queue/enqueue behavior.
- Story D (Audit specialist): Ensure conflict + enqueue events with correlation id; unit tests for audit event ordering.
- Story E (Demo page): Razor page showing a sample flagged case with SLA badge and link to reconciliation report; driven by service outputs.

Conventions (applies to this mission; do not skip):
- Solution/projects: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`; code in Domain/Application/Infrastructure.*; UI pages under `03-UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages`; system tests in `Tests.System.*`, unit tests in `Tests.Application` or relevant `Tests.*`.
- Key interfaces/classes: reconciliation/comparison service, classification service, manual review queue service, SLA enforcer, audit logger.
- Code style: C# 10, warnings-as-errors; SmartEnums need EF converters; PascalCase types/properties, camelCase locals/params; keep methods small/pure; avoid solution churn.
- Fixtures: use `Prisma/Fixtures`; create missing-field/conflict variants as needed.
- Telemetry: emit audit for conflicts and enqueue with correlation ids; metrics for classification confidence/SLA tagging; assert behavior/metrics, not UI.
- Observability & security: structured logging with correlation ids, performance timings, metrics for review queue health; audit conflict/enqueue; honor authentication/authorization roles for reviewers.

Definition of Done:
- Build passes with warnings-as-errors, no new suppressions.
- System/unit tests pass; assertions are behavioral and stable.
- Razor demo page shows flagged case with SLA badge/report link and works end-to-end.
- Playwright/demo headed run confirms scenario works (no UI assertions).
