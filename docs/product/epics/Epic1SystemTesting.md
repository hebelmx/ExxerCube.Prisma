# Epic 1 — System Testing Missions (N+1 Service Chains)

Context: we are in system-test mode, chaining real services with real fixtures to validate legal compliance, resilience, and observability. Guidance sources: `docs/Legal/LegalRequirements_Summary.md`, `docs/Legal/ClassificationRules.md`, `docs/SYSTEM_FLOW_DIAGRAM.md`, `docs/qa/Requirements.md`. Goal: prove end-to-end behavior (no UI assertions) across 3–4 services per mission, covering happy paths, extremes, classic failures, storage, observability, traceability, and compliance.

## Mission 1 — Happy Path Intake → Extraction → Reconciliation → Storage
- **Objective**: Demonstrate core pipeline completeness on well-formed CNBV XML + clean PDF, producing unified metadata stored with audit trail.
- **Services chained**: Ingestion (XML+PDF fetch), OCR/Extraction, Reconciliation, Storage/Audit.
- **Fixtures**: PRP1 XML + matching clean PDF from `Prisma/Fixtures` (e.g., `222AAA-44444444442025.xml` + corresponding PDF).
- **Test contract**:
  - Ingestion pulls XML/PDF, emits correlation id, and persists raw artifacts.
  - OCR/Extraction returns confidence >= 0.9 for required fields (per `MandatoryFields_CNBV`).
  - Reconciliation match rate >= 95% (Levenshtein) with zero critical conflicts.
  - Storage writes unified record and audit entries (action types: IntakeFetched, OcrCompleted, ReconcileCompleted, Stored).
- **Deliverables**:
  - System test that runs end-to-end and asserts: correlation continuity, match rate, confidence thresholds, audit entries count/order, storage record presence.
  - Metrics/log evidence captured (Serilog/AuditLogger output).

## Mission 2 — Degraded PDF Resilience (Filters + OCR Adaptation)
- **Objective**: Validate adaptive imaging on noisy/blurred PDFs and ensure best-effort extraction meets minimum thresholds.
- **Services chained**: Ingestion, Image Quality Analyzer (filters), OCR, Metrics, Storage.
- **Fixtures**: Low-quality PDF (noise/blur/watermark) + XML baseline from `Prisma/Fixtures` (degraded variant).
- **Test contract**:
  - Quality analyzer selects non-default filter set; record chosen filter in metrics.
  - OCR confidence improves vs. unfiltered baseline (>= +10% or absolute >= 0.7).
  - Fields marked “mandatory” in `MandatoryFields_CNBV` are present; missing fields are flagged (not fatal).
  - Storage keeps both raw and enhanced images; audit log notes filter choice.
- **Deliverables**:
  - System test comparing filtered vs. unfiltered run (can be sequential in one test) asserting confidence delta and flag handling.
  - Metrics snapshot showing filter selection and confidence uplift.

## Mission 3 — Missing/Conflicting Data → Manual Review Path
- **Objective**: Prove reconciliation detects gaps/conflicts and routes to manual review while preserving traceability.
- **Services chained**: Ingestion, OCR/Extraction, Reconciliation, Classification, Manual Review queue, Audit.
- **Fixtures**: XML missing key fields (e.g., `NumeroOficio`, `AutoridadNombre`) + PDF with partial data; DOCX free-form variant.
- **Test contract**:
  - Reconciliation flags conflicts/missing required fields per `MandatoryFields_CNBV`.
  - Classification applies rule set per `ClassificationRules.md` with confidence < threshold → `RequiresReviewReason = LowConfidence` or `ExtractionError`.
  - Manual Review queue receives case with SLA tag (High/Urgent based on `LegalRequirements_Summary.md` timelines).
  - Audit trail records conflict detection and enqueue action with correlation id.
- **Deliverables**:
  - System test that asserts review case creation, SLA tag, and audit events sequence.
  - Artifact: serialized reconciliation report attached to the case (stored or downloadable).

## Mission 4 — Export & Compliance Evidence (End-to-End Delivery)
- **Objective**: Validate export readiness and compliance evidence generation from processed cases.
- **Services chained**: Storage, Export Service (SIRO XML / Excel FR18 / signed PDF), Audit, Observability.
- **Fixtures**: Previously processed unified record (from Missions 1–3) or seeded fixture data.
- **Test contract**:
  - Export produces SIRO XML and Excel layouts matching schema; signed PDF generated or explicitly skipped with rationale.
  - Audit entries for ExportRequested, ExportGenerated, ExportSigned (or Skipped) with correlation to source case.
  - Observability metrics capture export duration and failures = 0 for happy path.
  - Downloaded artifacts pass basic schema validation (XML) and non-empty checks (Excel/PDF).
- **Deliverables**:
  - System test invoking export and asserting artifacts, audit events, and metrics counters.
  - Stored evidence: export files + audit ids for traceability.

## Mission 5 — Observability & Traceability Regression
- **Objective**: Ensure all missions emit mandatory telemetry for traceability/compliance.
- **Services chained**: Any 3+ pipeline steps + Audit + Metrics + Storage.
- **Test contract**:
  - Every step produces correlation-id-consistent audit entries.
  - Serilog/metrics emit: latency per step, error counts, selected filters, classification confidence, SLA tags.
  - Missing telemetry triggers test failure.
- **Deliverables**:
  - Reusable assertion helper for audit/metric completeness.
  - Applied across Missions 1–4 as part of their checks.

## Mission 6 — Extreme Case: Schema Drift & Template Adaptation
- **Objective**: Validate adaptive behavior when CNBV XML schema or bank template changes without code changes.
- **Services chained**: Ingestion (schema drift), Template Adapter, Reconciliation, Storage, Audit.
- **Fixtures**: XML with added/renamed field (per `SYSTEM_FLOW_DIAGRAM.md` schema evolution note) + bank template variant.
- **Test contract**:
  - Template adapter detects schema change, maps fields, and records adapter event.
  - No hard failure; non-mapped fields logged as warnings; required fields still populated or explicitly flagged.
  - Audit entry: SchemaChangeDetected + TemplateAdapterApplied.
- **Deliverables**:
  - System test applying drifted XML and asserting adapter events, warnings, and successful storage with flags.

## Mission 7 — Adaptive DOCX Extraction & Field Merging
- **Objective**: Implement adaptive DOCX extraction using multiple strategies and merge logic; pass contract tests and a system test with real DOCX fixtures.
- **Services chained**: Adaptive DOCX strategies → Adaptive extractor → Merge strategy → Storage/Audit/Telemetry.
- **Fixtures**: DOCX variants (structured, semi-structured, table-heavy) under `Prisma/Fixtures/DocxAdaptive/`.
- **Test contract**:
  - `IAdaptiveDocxExtractor` modes behave per contracts; confidences sorted; respects cancellation.
  - `IFieldMergeStrategy` merges without null, reports conflicts/merged fields, handles null entries.
  - System test: run on DOCX variants; mandatory fields covered or flagged; audit/metrics record strategy/merge/conflicts.
- **Deliverables**:
  - Implementation passing contract tests; system test for DOCX fixtures; Razor demo page showing strategies/confidences/merged fields/conflicts.

## Observability, Security, and Telemetry (applies to all missions)
- Emit structured logs with correlation ids; capture performance timings and metrics (success/error rates, confidence deltas, throughput).
- Record audit events for key actions (ingestion, filter selection, reconciliation, export, adapter changes) with correlation.
- Ensure authentication/authorization is honored on services/pages where applicable.
- Tests should assert behavior/telemetry, not UI; no flakiness; warnings-as-errors build.

## How to Demo (select 3–4 cases)
- Pick from Missions: 1 (happy path), 2 (degraded PDF uplift), 3 (manual review routing), 4 (export evidence).
- Demo flow: run system tests, capture artifacts (audit ids, exports), then showcase via UI pages (System Flow, Audit Trail, Manual Review, Export Management) using real outputs—no UI assertions in tests.

## Test Implementation Notes
- Follow TDD: write system test specs first, then adjust services to satisfy contracts.
- Use real fixtures from `Prisma/Fixtures`; extend with degraded/missing-field variants as needed.
- Assert behavior/payloads, not UI; validate metrics/audit/exports directly from services/storage.
- Keep correlation ids consistent across chained calls; fail fast if missing telemetry.
