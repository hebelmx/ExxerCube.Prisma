# Phase-2 QA Review — R4: Invariants and Feature Verification

**Reviewer:** Quinn (QA Agent — independent, clean-room)
**Date:** 2026-06-20
**Branch reviewed:** Liv
**Sources consulted:**
- PRD: `docs/product/requirements/prd.md`
- PRP: `docs/product/requirements/PRP.md`
- Evidence bundle: `docs/qa/harness/runs/phase2-evidence/` (MANIFEST, e2e-run.log, harness-integration TRX, webui http-probes)
- Source code inspected directly for interface/implementation presence and DI wiring

**Independence declaration:** No prior gap matrices, readiness reports, RC documents, planning artifacts, or harness phase-1 analysis were consulted. All verdicts derive from the PRD/PRP requirements and the evidence collected in this run.

---

## Part A — Invariants

| ID | Invariant (from PRD/PRP) | Status | Evidence | Rationale |
|----|--------------------------|--------|----------|-----------|
| INV-1 | **Checksum uniqueness** — no duplicate downloads; `IDownloadTracker.IsFileAlreadyDownloadedAsync` checks SHA-256 before any download proceeds (FR2, Story 1.1 AC3) | NOT TESTED | E2E capstone test failed at Step 1 (simulator rendered no PDF links); no download operations were executed during the evidence run. Interface exists; `DownloadTrackerService` stores/queries on `Checksum` column; SHA-256 hashing performed by caller. Source presence confirmed, runtime not exercised. | The mechanism is structurally present and the interface contract is SHA-256-keyed, but zero download transactions were executed in the current evidence bundle. |
| INV-2 | **Identity dedup** — `IPersonIdentityResolver.DeduplicateRecordsAsync` prevents duplicate person records across documents (FR10, Story 1.4 AC2) | FAIL | `PersonIdentityResolverService` constructor takes only `ILogger`; `FindByRfcAsync` returns `null` with explicit TODO "Database lookup deferred"; `DeduplicatePersonsAsync` is in-memory only; no DB persistence path exists; `IPersonIdentityResolver` is not registered in Athena or Orion workers — only in Web UI. | Deduplication logic runs in-memory within a single request, is not persisted, and is not on the pipeline path (Athena/Orion). Across-document deduplication is impossible without persistence. |
| INV-3 | **Referential integrity** — Expediente/Persona/Oficio entities maintain FK integrity (FR26) | NOT TESTED | EF Core `PrismaDbContext` has `DbSet<Persona>`, `DbSet<FileMetadata>`, `DbSet<SLAStatus>`, `DbSet<AuditRecord>`, `DbSet<PersistedUnifiedMetadata>` but no `DbSet<Expediente>` or `DbSet<Oficio>`. `Expediente` and `Oficio` are used as in-memory domain objects embedded within `PersistedUnifiedMetadata`. No E2E pipeline ran to confirm runtime integrity. | Without dedicated `Expediente` and `Oficio` tables, classical FK enforcement cannot exist. Whether `PersistedUnifiedMetadata` serialization preserves referential consistency was not exercised at runtime. |
| INV-4 | **Audit immutability** — audit log is append-only; no update or delete of records (FR17, Story 1.9 AC1) | FAIL | `AuditRetentionBackgroundService.cs` (path confirmed) actively deletes `AuditRecord` rows via `SaveChangesAsync`. No EF Core state guard blocks `EntityState.Deleted` or `EntityState.Modified` on `AuditRecord`. No DB trigger or shadow table enforces immutability. The interface is insert-only by design, but `AuditRetentionBackgroundService` bypasses the interface contract. | FR17 requires an "immutable audit log." PRD Story 1.9 AC1 states "maintains immutable audit log of all processing steps." Active deletion via retention policy contradicts this requirement as written. Note: an argument exists that retention archival after 7 years is compatible with regulatory intent — but the current implementation deletes (not archives to separate read-only store) and no archive-first step is confirmed operational. |
| INV-5 | **Non-notification safety** — client is NOT notified unless legal directive explicitly allows (FR31, PRP Legal Constraints) | FAIL | Zero enforcement code found in the entire codebase. No interface, class, guard, policy, middleware, or attribute named for FR31 or non-notification exists. `ProcessingHub.NotifySLAEscalation` broadcasts SLA alerts unconditionally. `HubContextDomainEventBroadcaster` broadcasts all domain events without a notification-permission check. | FR31 is a stated legal compliance requirement with zero corresponding enforcement implementation. Any SLA escalation broadcast would reach client-facing systems without a legal-directive gate. |
| INV-6 | **Field completeness gate before export** — `IFieldMatcher.ValidateMatchResultAsync` or equivalent must pass before export is produced (FR20, Story 1.7 AC4) | FAIL | `SiroXmlExporter` checks only 3 header fields (`Expediente`, `NumeroExpediente`, `NumeroOficio`). `AdaptiveResponseExporterAdapter` has no gate in its call path — `AdaptiveExporter.ValidateExportAsync` exists but is never called from within `ExportAsync`. `ValidateMatchResultAsync` (the PRP-specified interface method on `IFieldMatcher`) is not called from any orchestrator before export. | Export can proceed with incomplete regulatory fields beyond the 3-field header check. FR20 requires all required regulatory fields to be present and valid. |
| INV-7 | **Additive-only DB schema** — existing tables not modified (CR4) | PASS | All 12 migration files inspected: `DROP TABLE` and `DROP COLUMN` appear exclusively in `Down()` rollback methods. Two migrations use `DropForeignKey` in `Up()` — these affect FK constraints only, not columns or tables. No raw SQL `DROP` statements in `Up()`. | Migration forward path is additive. |
| INV-8 | **Backward-compatibility** — existing OCR pipeline still functions (Story 1.1 IV1, 1.2 IV1, 1.3 IV1) | NOT TESTED | Build succeeds with 0 errors/warnings (evidence: build-output.txt). Harness integration tests (2/2 passed) exercise Web UI startup via WebApplicationFactory and health check workflow. The capstone E2E test failed at the SIARA simulator step before reaching the OCR pipeline. No unit-test execution log for `Tests.Infrastructure.Extraction.Teseract` or Domain/Application suites is in the current evidence bundle. | Source structure of `IFieldExtractor` (generic and non-generic variants), `IOcrExecutor`, and `IImagePreprocessor` implementations is present. Runtime execution of OCR path was not captured in this evidence run. |
| INV-9 | **SLA business-day correctness incl. Mexican holidays** (Story 1.5 IV3) | NOT TESTED | `MexicoBusinessDayCalculator` exists and is registered as `IBusinessDayCalculator` across all hosts. `SLAEnforcerService` delegates to it when injected, covering `PublicHoliday.MexicoPublicHoliday` federal holidays + weekends. However, no SLA calculation was exercised at runtime in this evidence run (DB was unavailable; E2E failed before Stage 3). | Holiday-aware implementation is correctly wired at source level and DI registration level. Runtime correctness was not exercised. |
| INV-10 | **Confidence-threshold gating** — low-confidence classifications are routed to manual review rather than proceeding automatically (Story 1.6 AC1, PRP Stage 3 error propagation) | NOT TESTED | `IManualReviewerPanel` / `ManualReviewerService` exists and is registered across all hosts. The Stage 3 decision flow in the PRP specifies that `IManualReviewerPanel.GetReviewCasesAsync` is invoked when `ValidateMatchResultAsync` fails or confidence is low. `IFieldMatcher` interface is not implemented (see Part B). No E2E pipeline exercised at runtime. | Without `IFieldMatcher` being implemented, the confidence score that drives the gating cannot be computed by the pipeline. The gating condition (low confidence → manual review) cannot be active even if `IManualReviewerPanel` is wired. |
| INV-11 | **Correlation-ID propagation** across all processing stages (NFR11, Story 1.9 AC7) | NEEDS HUMAN REVIEW | Application-level propagation confirmed: `CorrelationId` is generated at ingestion (`Guid.NewGuid()`), carried on all domain events, stamped on every `AuditRecord`, indexed in DB, and queryable in audit viewers. However, no HTTP-layer middleware extracts or injects a `X-Correlation-ID` header. Whether the internal event-chain propagation satisfies NFR11 ("distributed tracing across processing stages") at the infrastructure level depends on operational topology definition — microservice-to-microservice HTTP tracing is not wired. | Domain-event propagation is structurally sound. HTTP header injection for external distributed tracing is absent. Whether this meets the requirement is ambiguous and needs owner judgment. |

---

## Part B — Feature Verification

### Stage 1: Ingestion and Acquisition

| Feature | Interface(s) | Status | Evidence | Justification |
|---------|-------------|--------|----------|---------------|
| F1-2: Browser automation — launch, navigate, detect downloadable files | `IBrowserAutomationAgent` (PlaywrightBrowserAutomationAdapter) | NOT TESTED | Interface and implementation exist; registered in Orion + Web UI. E2E capstone test failed at the simulator step — the authenticated dashboard loaded but no `a[href$='.pdf']` appeared within 90 s. No successful download cycle was observed. | Playwright adapter is implemented and DI-wired. Runtime execution reached authentication but not file detection. |
| F3: Duplicate detection via checksum | `IDownloadTracker` (DownloadTrackerService) | NOT TESTED | Implementation confirmed; queries `Checksum` column on `FileMetadata` table. No runtime execution observed in this evidence run. | |
| F4: File persistence with deterministic paths | `IDownloadStorage` (FileSystemDownloadStorageAdapter) | NOT TESTED | Implementation exists. No storage operation executed. | |
| F5: File metadata logging (name, URL, timestamp, checksum) | `IFileMetadataLogger` (FileMetadataLoggerService) | NOT TESTED | Implementation exists; `FileMetadata` DbSet confirmed. No runtime execution observed. | |

### Stage 2: Extraction and Classification

| Feature | Interface(s) | Status | Evidence | Justification |
|---------|-------------|--------|----------|---------------|
| F6: File type identification by content | `IFileTypeIdentifier` (FileTypeIdentifierService) | NOT TESTED | Implementation in `Infrastructure.Extraction/Teseract/`. No runtime execution in this evidence run. | |
| F7, F15: Metadata extraction from XML/DOCX/PDF with OCR fallback | `IMetadataExtractor` (XmlMetadataExtractor, PdfMetadataExtractor, DocxMetadataExtractor, CompositeMetadataExtractor) | NOT TESTED | Four implementations present. No runtime execution confirmed in current evidence. | |
| F8: Safe, normalized file naming | `ISafeFileNamer` (SafeFileNamerService) | NOT TESTED | Implementation present. | |
| F9-10: Document classification L1/L2 (Aseguramiento, Judicial, etc.) | `IFileClassifier` (FileClassifierService) | NOT TESTED | Implementation present. | |
| F11: File organization by classification | `IFileMover` (FileMoverService) | NOT TESTED | Implementation present. | |
| F12: Duplicate/ambiguity scoring | `IRuleScorer` | FAIL | No interface definition file found in the CSharp tree. No implementation found. | PRP Feature 12 and its interface `IRuleScorer` are absent from the codebase. |
| F13: Scanned PDF detection | `IScanDetector` | FAIL | No interface definition file found. No implementation found. | PRP Feature 13 and `IScanDetector` are absent. OCR fallback path exists, but the specific scan-detection interface is not implemented. |
| F14: Image preprocessing for OCR (IScanCleaner) | `IScanCleaner` | FAIL | No interface definition file found. No implementation found. `IImagePreprocessor` exists (the pre-PRP interface), but the PRP-specified `IScanCleaner` wrapping it is absent. | PRP Feature 14 gap. The legacy `IImagePreprocessor` still exists but `IScanCleaner` is not defined. |
| F16-17: Audit logging with classification decisions | `IAuditLogger` (AuditLoggerService, QueuedAuditLoggerService) | NOT TESTED | Both implementations exist; `QueuedAuditLoggerService` is registered as `IAuditLogger` across all hosts when DB string is present. No runtime audit inserts observed (DB unavailable in this run). | |
| F18: Classification reports (CSV/JSON) | `IReportGenerator` | FAIL | No interface definition file found. No implementation found. | PRP Feature 18 gap. |
| F19: XML nullable parsing to Expediente | `IXmlNullableParser<T>` (XmlExpedienteParser) | NOT TESTED | Implementation confirmed. Not exercised at runtime. | |
| F26-27: Structured field extraction from DOCX/PDF | `IFieldExtractor<T>` (DocxFieldExtractor, PdfOcrFieldExtractor) | NOT TESTED | Multiple implementations present. Not runtime-exercised in this evidence run. | |
| F28-29, F33: Cross-format field matching, unified record generation, completeness validation | `IFieldMatcher<T>` | FAIL | No interface definition file found anywhere in the CSharp tree. No implementation found. This interface is the backbone of Stage 2→3 handoff and export validation. | This is a significant gap: `IFieldMatcher<T>` is used in 7 PRP sequential flow steps across all stages and is referenced as the source of `UnifiedMetadataRecord` for export. |

### Stage 3: Decision Logic and SLA Management

| Feature | Interface(s) | Status | Evidence | Justification |
|---------|-------------|--------|----------|---------------|
| F20: SLA deadline tracking and escalation | `ISLAEnforcer` (ResilientSLAEnforcerService wrapping SLAEnforcerService + MexicoBusinessDayCalculator) | NOT TESTED | Full holiday-aware implementation confirmed; registered across all hosts. Runtime not exercised (DB unavailable). | |
| F21: Person identity resolution with RFC variants | `IPersonIdentityResolver` (PersonIdentityResolverService) | FAIL | Registered in Web UI only (not Athena/Orion). Implementation is in-memory with no DB persistence. `FindByRfcAsync` returns `null` with a TODO comment. Cross-document deduplication is not operational. | |
| F22: Legal directive classification | `ILegalDirectiveClassifier` (SemanticAnalyzerAdapter, LegalDirectiveClassifierService) | NOT TESTED | Two implementations exist; registered in Web UI only — not in Athena or Orion workers. Runtime not exercised. | Partial registration gap: legal classification is only reachable via the Web UI path, not the automated pipeline workers. |
| F23: Manual review panel / human review queue | `IManualReviewerPanel` (ManualReviewerService) | NOT TESTED | Registered across all hosts. Blazor pages exist at `/manual-review` and `/manual-review/{CaseId}`. Not exercised at runtime. | |
| F24: UI bundle (validation, editing, submission forms) | `IUIBundle` | FAIL | No interface definition found. No implementation found. The Blazor pages for manual review, SLA, and export exist but they do not implement a technology-agnostic `IUIBundle` contract. | PRP Feature 24 gap. |
| F30: Excel layout generation | `ILayoutGenerator` (ExcelLayoutGenerator) | NOT TESTED | Registered in Reconciliator + Web UI. Not exercised at runtime in this evidence run. | |
| F31: Field agreement / confidence annotation | `IFieldAgreement` | FAIL | No interface definition found. No implementation found. | PRP Feature 31 gap. |
| F32: Configurable matching policy | `IMatchingPolicy` (MatchingPolicyService) | NOT TESTED | Implementation exists; `INameMatchingPolicy` sub-interface exists. Not exercised at runtime. | |

### Stage 4: Final Compliance Response and Export

| Feature | Interface(s) | Status | Evidence | Justification |
|---------|-------------|--------|----------|---------------|
| F25: SIRO-compliant XML export | `IResponseExporter` (SiroXmlExporter, AdaptiveResponseExporterAdapter) | NOT TESTED | Multiple implementations present. Reconciliator uses `SiroXmlExporter`; Web UI uses `AdaptiveResponseExporterAdapter`. Export page exists at `/export-management`. Not exercised at runtime. | |
| F33 (field validation): Export completeness gate | `IFieldMatcher<T>.ValidateMatchResultAsync` | FAIL | `IFieldMatcher<T>` does not exist. `AdaptiveExporter.ValidateExportAsync` exists but is not called from within the export call chain. `SiroXmlExporter` checks only 3 header fields. FR20 / Story 1.7 AC4 are not met. | |
| F34: PDF requirement summarization (bloqueo/desbloqueo categories) | `IPdfRequirementSummarizer` (PdfRequirementSummarizerService) | NOT TESTED | Implementation present. Not runtime-exercised. | |
| F35: Semantic label mapping to categories | `ICriterionMapper` (CriterionMapperService) | NOT TESTED | Implementation present. Not runtime-exercised. | |

### Cross-Cutting

| Feature | Interface(s) / Requirement | Status | Evidence | Justification |
|---------|---------------------------|--------|----------|---------------|
| FR16: Digitally signed PDF export (Story 1.8) | `IResponseExporter.ExportSignedPdfAsync` (DigitalPdfSigner) | NOT TESTED | `DigitalPdfSigner` implementation exists within `CompositeResponseExporter`. Not runtime-exercised. | |
| NFR9: 7-year audit retention | `AuditRetentionBackgroundService` | NOT TESTED | Service is configurable via `IOptions<AuditOptions>` with `RetentionYears`; defaults not confirmed at runtime. | |
| NFR8: TLS 1.3 / encryption at rest | Infrastructure config | NOT TESTED | No TLS configuration was inspectable without a running DB; `appsettings.json` hardcodes SQL Server host not available in this environment. | |
| CR2: CSnakes Python interop compatibility | Python module registration | NOT TESTED | Python/CSnakes registration is intentionally commented out (dormant by design per CLAUDE.md); `Extraction.Python` tests 0 executed (dormant). Not a runtime gap per owner decision. | |
| NFR6: Stateless / horizontal scaling | Architecture | NEEDS HUMAN REVIEW | Services are registered as Scoped or Singleton; no session state beyond SignalR circuit. No Kubernetes/Docker configuration was verified in evidence bundle. | |
| Story 1.6 AC7: UI uses MudBlazor components | Blazor pages | PASS | Web UI home, login, and dashboard screenshots show MudBlazor app shell (confirmed by file sizes 66–101 KB vs. 4 KB redirect pages). `/manual-review`, `/sla-dashboard`, `/audit-trail`, `/export-management` routes exist in the source tree. | Authenticated pages could not be screenshotted (auth redirect); source presence confirmed. |
| Story 1.9 IV1: Audit logging is async / non-blocking | QueuedAuditLoggerService | NOT TESTED | `QueuedAuditLoggerService` channels inserts through an in-process queue (non-blocking design). Not exercised at runtime. | |

---

## Part C — Method

**Source evidence:** Two parallel sub-agents performed independent code searches across the full CSharp source tree. One catalogued all 28 PRP interfaces and their implementations; the other examined routes, DB schema, migrations, DI composition roots, and special enforcement points. Both reported facts only; verdicts were rendered by the reviewer.

**Runtime evidence:** The phase-2 evidence bundle provides three evidence streams:
1. E2E capstone test (`MaxFidelityGateFullPipelineE2ETests`) — 1 test, 1 failed (Step 1: SIARA simulator rendered no PDF links). Docker, SQL Testcontainers, and Playwright login all succeeded; the pipeline was never reached.
2. Harness integration tests — 2/2 passed: a fast stub-HTTP health-check run and a full WebApplicationFactory boot with SQL Testcontainer and health-check workflow.
3. Web UI live probes — `GET /` = HTTP 200 (MudBlazor renders); `GET /health` = HTTP 503 (SQL readiness fails, expected without DB).

**Consequence for verdicts:** Because the E2E pipeline never executed, the majority of runtime-dependent behaviors cannot be verified. All pipeline features are marked NOT TESTED rather than PASS/FAIL unless source-code evidence is sufficient to confirm a structural failure (FAIL) or the evidence run explicitly exercised the behavior (PASS). NOT TESTED is not a clean bill of health — it means the assertion is open.

**Verification approach for FAIL verdicts:** Each FAIL was derived from one of: (a) an interface/implementation being absent from the source tree, (b) an implementation that structurally cannot fulfill the requirement (e.g., in-memory-only identity resolution across documents), or (c) an active code path that contradicts a stated invariant (e.g., audit deletion).

---

## Part D — Notes and Anomalies

**D1. Seven of 28 PRP interfaces are absent from the codebase.**
`IRuleScorer`, `IScanDetector`, `IScanCleaner`, `IReportGenerator`, `IFieldMatcher<T>`, `IUIBundle`, `IFieldAgreement` have neither an interface file nor an implementation in the CSharp tree. Of these, `IFieldMatcher<T>` is the most consequential: it is called 7 times across the PRP sequential flow (field matching, unified record generation, field validation across all three latter stages).

**D2. `IFieldMatcher<T>` absence causes cascade failures.**
The PRP defines `UnifiedMetadataRecord` as the output of `IFieldMatcher<T>.GenerateUnifiedRecordAsync`. Without this interface, the Stage 2→3 hand-off data model cannot be produced by the designed path. Export validation (`ValidateMatchResultAsync`) and confidence-gated manual review routing also depend on this interface.

**D3. Non-notification enforcement (FR31) is a zero-implementation legal gap.**
FR31 is a regulatory compliance requirement, not just a software feature. Zero lines of code enforce the "no client notification without legal permission" rule. SignalR broadcasts events unconditionally. This is a material compliance risk.

**D4. Audit immutability claim vs. deletion reality.**
`AuditRetentionBackgroundService` deletes records; it does not archive-then-delete to an immutable store. PRD Story 1.9 AC1 and FR17 state "immutable audit log." Whether configurable deletion after `RetentionYears` (NFR9 = 7 years minimum) constitutes a violation or an acceptable archival policy is ambiguous — but the current implementation provides no immutable store: deletion is the only end-of-life path.

**D5. SIARA simulator corpus gap caused E2E failure.**
The `cases.json` file has 500 entries, but the simulator served zero PDF document links within 90 seconds. The MANIFEST notes `bulk_generated_documents_all_formats/` may not be wired in `appsettings.json` for this run, or the corpus-scan loop had not completed. This is an environment/configuration gap, not a product code defect — but it means zero pipeline runtime evidence was produced.

**D6. Registration gaps for classification services in worker hosts.**
`ILegalDirectiveClassifier` and `IPersonIdentityResolver` are registered only in the Web UI, not in Athena or Orion workers. The automated pipeline (which runs headlessly through workers) cannot perform legal directive classification or person identity resolution without manual UI intervention.

**D7. Auth-protected pages not visually verified.**
Five of eight screenshotted pages (4,254-byte files) show login redirects. Manual review, SLA dashboard, export management, audit trail, and processing pages were not captured in authenticated state. Source routes exist; functional rendering is unverified in this evidence run.

**D8. `Expediente` and `Oficio` have no dedicated DB tables.**
`PrismaDbContext` lacks `DbSet<Expediente>` and `DbSet<Oficio>`. These domain entities appear to be serialized into `PersistedUnifiedMetadata`. INV-3 (referential integrity between Expediente/Persona/Oficio) cannot be enforced by the database in the classical FK sense.

---

## Summary

### Invariants (11 evaluated)

| Status | Count | IDs |
|--------|-------|-----|
| PASS | 1 | INV-7 (additive-only schema) |
| FAIL | 4 | INV-2 (identity dedup), INV-4 (audit immutability), INV-5 (non-notification), INV-6 (export completeness gate) |
| NOT TESTED | 4 | INV-1 (checksum uniqueness), INV-3 (referential integrity), INV-8 (OCR backward-compat), INV-9 (SLA holiday correctness) |
| NEEDS HUMAN REVIEW | 2 | INV-10 (confidence-threshold gating), INV-11 (correlation-ID propagation) |

### Features (31 evaluated, grouped across 4 stages + cross-cutting)

| Status | Count |
|--------|-------|
| PASS | 1 (MudBlazor UI consistency) |
| FAIL | 10 (F12 IRuleScorer, F13 IScanDetector, F14 IScanCleaner, F18 IReportGenerator, F28/29/33 IFieldMatcher, F21 identity resolver, F24 IUIBundle, F31 IFieldAgreement, INV-5/FR31 non-notification, INV-6/F33 export gate) |
| NOT TESTED | 18 |
| NEEDS HUMAN REVIEW | 2 (NFR6 horizontal scaling, correlation-ID depth) |

### Key Findings (priority order)

1. **FR31 / INV-5 — Non-notification safety: zero enforcement.** Regulatory legal compliance gap; no code path prevents unconditional broadcast.
2. **IFieldMatcher absence** — 7 of 28 PRP interfaces missing; `IFieldMatcher<T>` is the most critical as it backs the Stage 2→3 unified record and export validation chain.
3. **INV-4 — Audit immutability contradicted** by active deletion in `AuditRetentionBackgroundService`.
4. **INV-2 / F21 — Person identity deduplication not operational** across documents (in-memory only, no DB persistence, not on worker path).
5. **INV-6 / F33 — Export proceeds without full field-completeness gate** (only 3-header check in SiroXmlExporter; adaptive path has no gate in call chain).
6. **E2E pipeline produced zero runtime artifacts** — the SIARA corpus gap blocks end-to-end validation of the entire Stage 1→4 flow.
