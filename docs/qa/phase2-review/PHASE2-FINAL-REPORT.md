# ExxerCube.Prisma — Phase-2 Independent QA Final Report

**Product:** ExxerCube.Prisma — Regulatory Compliance Automation System  
**PRD Version:** 1.0 (2025-01-12)  
**Review Date:** 2026-06-20  
**Branch:** Liv  
**Synthesizer:** Quinn (QA Agent — SYNTHESIZER role)  
**Reviewers:** R1 (FR1–16), R2 (FR17–32), R3 (NFR1–17, CR1–8), R4 (Invariants + Features), R5 (Exploratory + Usability), plus Orchestrator Verification Notes V1–V5  

---

## 1. Executive Summary

ExxerCube.Prisma is an enterprise OCR document-processing system for Spanish-language Mexican
regulatory (UIF/CNBV) legal documents, operated under a five-stage pipeline:
Quality Analysis → OCR → Fusion/Reconciliation → Classification → Export.

This independent Phase-2 review evaluated the product against its PRD (v1.0, 2025-01-12)
across 32 Functional Requirements, 17 Non-Functional Requirements, 8 Compatibility Requirements,
11 Invariants, and 31 Features. Evidence was drawn exclusively from a capstone E2E pipeline
re-run, harness integration tests, and live Web UI probes performed on 2026-06-20, all on
branch Liv (build: 0 errors, 0 warnings; .NET 10.0.301; Docker 29.5.3).

**Key result:** The core ingestion-through-SIRO-export pipeline ran end-to-end and produced
live database-backed audit evidence, demonstrating meaningful functional maturity. However,
the product carries three zero-implementation legal/compliance gaps (non-notification enforcement,
export completeness gate, identity deduplication), a live pipeline failure (Excel export), two
unauthenticated route exposures, and seven absent PRP-specified interfaces that collectively
block staging readiness.

**Deployment Recommendation: NOT READY FOR STAGING** (see Section 9 for full rationale).

---

## 2. Requirement Summary

### Functional Requirements (32 total)

Post-reconciliation with VERIFICATION-NOTES V1 (pipeline did execute in the re-run):

| Status | Count | IDs |
|--------|-------|-----|
| PASS | 13 | FR1, FR3, FR4 (partial), FR5, FR6 (partial), FR7, FR9, FR15, FR17, FR20, FR24, FR25, FR26 |
| FAIL | 2 | FR18, FR31 |
| NEEDS HUMAN REVIEW | 2 | FR14, FR30 |
| NOT TESTED | 15 | FR2, FR8, FR10, FR11, FR12, FR13, FR16, FR19, FR21, FR22, FR23, FR27, FR28, FR29, FR32 |

> FR32 (data retention) reclassified PASS by R2 based on live `AuditRetentionBackgroundService`
> execution evidence. FR30 (RBAC) remains NEEDS HUMAN REVIEW: structural enforcement confirmed by
> source; runtime authenticated exercise absent.

> FR31 is classified FAIL (not NOT TESTED): the PRP explicitly names non-notification
> as a mandatory legal compliance constraint and zero enforcement code exists — absence
> of implementation for a stated legal requirement is a failure.

**FR PASS rate (confirmed): 13/32 (41%). Fail: 2/32. NHR: 2/32. NT: 15/32.**

---

### Non-Functional Requirements (17 total)

| Status | Count | IDs |
|--------|-------|-----|
| PASS | 8 | NFR2, NFR9, NFR10, NFR11, NFR12, NFR13, NFR15 (partial — see below), NFR32 (see FR32) |
| FAIL | 0 | — |
| NEEDS HUMAN REVIEW | 2 | NFR14, NFR15 |
| NOT TESTED | 7 | NFR1, NFR3, NFR4, NFR5, NFR6, NFR7, NFR8, NFR16, NFR17 |

> Corrected counts: NFR PASS=8 (NFR2, NFR9, NFR10, NFR11, NFR12, NFR13 confirmed; NFR15 is
> NEEDS HUMAN REVIEW because the primary pipeline is event-driven per-document, not a
> bulk-input batch method). NFR16 and NFR17 are NOT TESTED due to PRD scope ambiguity
> (Reasoning Path 9 owner decision; no Azure AD or field-level PII encryption implemented).
> NFR8 has active risk but no measurement obtained, so NOT TESTED. All performance NFRs
> (NFR1, NFR3, NFR4, NFR5) are NOT TESTED: no timing data was collected.

**NFR PASS: 6/17 confirmed. NHR: 2/17. NT: 9/17 (includes 2 scope-ambiguous). Fail: 0.**

---

### Compatibility Requirements (8 total)

| Status | Count | IDs |
|--------|-------|-----|
| PASS | 5 | CR1, CR2, CR5, CR6, CR7 |
| FAIL | 1 | CR8 |
| NEEDS HUMAN REVIEW | 2 | CR3, CR4 |
| NOT TESTED | 0 | — |

**CR FAIL: CR8 (Azure Blob Storage adapter absent — confirmed by two independent search passes).**

---

## 3. Feature Summary

Based on R4 Part B (31 features evaluated across 4 stages + cross-cutting), post-reconciliation
with V1 (pipeline executed):

| Status | Count | Notes |
|--------|-------|-------|
| PASS | 2 | MudBlazor UI shell (Story 1.6 AC7); SIRO XML export (F25 — runtime evidence from re-run) |
| FAIL | 9 | F12 (IRuleScorer absent), F13 (IScanDetector absent), F14 (IScanCleaner absent), F18 (IReportGenerator absent), F21 (IPersonIdentityResolver not operational across docs), F24 (IUIBundle absent), F28/29/33 (IFieldMatcher<T> absent), F31 (IFieldAgreement absent), F33 export gate (ValidateExportAsync not called) |
| NEEDS HUMAN REVIEW | 2 | NFR6 (horizontal scaling), INV-11 (correlation-ID propagation depth) |
| NOT TESTED | 18 | All remaining features — browser automation, duplicate detection, file persistence, type ID, metadata extraction, safe naming, classification L1/L2, file organization, audit logging (not exercised in this run), XML parsing, field extraction DOCX/PDF, SLA management, legal directive classification, manual review panel, Excel layout generation, matching policy, PDF summarization, semantic label mapping |

> V1 correction applied: features exercised in the re-run (F25 SIRO export, F16 audit logging,
> F7/F9 classification and fusion) were upgraded from NOT TESTED where runtime evidence is direct.
> F7 (classification L1) upgraded: `Type: Aseguramiento, Confidence: 10` logged at Stage 4.

---

## 4. Invariant Summary

Based on R4 Part A (11 invariants) plus VERIFICATION-NOTES V4:

| Status | Count | IDs |
|--------|-------|-----|
| PASS | 1 | INV-7 (additive-only DB schema) |
| FAIL | 3 | INV-2 (identity dedup — in-memory only, no pipeline wiring), INV-5 (non-notification — zero code), INV-6 (export completeness gate bypassed — confirmed by re-run evidence) |
| NEEDS HUMAN REVIEW | 3 | INV-4 (audit immutability — V4 adjudication: append-only write convention + sanctioned 7-yr retention deletion; no hard DB-level guard; owner must rule), INV-10 (confidence-threshold gating — IFieldMatcher<T> absent, so gate signal cannot be computed), INV-11 (correlation-ID propagation depth) |
| NOT TESTED | 4 | INV-1 (checksum uniqueness), INV-3 (referential integrity Expediente/Oficio), INV-8 (OCR backward-compat), INV-9 (SLA holiday correctness) |

> V4 adjudication: INV-4 is NEEDS HUMAN REVIEW (not PASS and not FAIL). Both R2's PASS (append-only
> logger interface) and R4's FAIL (AuditRetentionBackgroundService deletes rows) are factually
> correct. Owner must rule on whether sanctioned 7-year deletion satisfies "immutable."

---

## 5. Findings

### Critical Severity

#### CRIT-1: Non-Notification Enforcement is Entirely Absent (FR31, INV-5)

**What:** The PRP explicitly identifies "Legal constraints prohibiting the notification of involved
clients unless expressly allowed by legal directive" as a mandatory legal compliance constraint
(FR31). No enforcement code exists anywhere in the production codebase — no interface, class,
guard, policy, middleware, or attribute for non-notification enforcement was located across two
independent search passes (R2 and R4). `ProcessingHub.NotifySLAEscalation` broadcasts SLA alerts
unconditionally. `HubContextDomainEventBroadcaster` broadcasts all domain events without a
notification-permission check.

**Evidence:** R2 grep pass: `NonNotification`, `non.notification`, `IsNotificationAllowed`,
`NotifyClient`, `legal.*constraint.*notif`, `prohibit.*notif` — zero matches in production CSharp.
R4 independent confirmation. V5 corroboration.

**Affected requirements:** FR31, INV-5, PRP Legal Constraints  
**Risk:** Regulatory non-compliance with Mexican UIF/CNBV requirements. Client notification of
financial investigations without legal authorisation is a legal breach, not merely a software gap.

---

#### CRIT-2: Export Proceeds Without Full Field-Completeness Gate (INV-6, FR20, EX-06)

**What:** The live pipeline re-run demonstrates that a document classified at confidence level 10
(critically low) had two ReviewCase rows created (CASE-8bb5be3c, CASE-23cb2ac7), yet Stage 5
SIRO XML export proceeded immediately thereafter with no gate on review resolution. The PRP
specifies `IFieldMatcher<T>.ValidateMatchResultAsync` must pass before export. `IFieldMatcher<T>`
does not exist as a named interface in the codebase. `AdaptiveExporter.ValidateExportAsync` exists
but is not called from within the export call chain. `SiroXmlExporter` checks only 3 header fields
(Expediente, NumeroExpediente, NumeroOficio).

Additionally, fusion returned `NextAction: Revisión manual requerida, Conflicts: 1` (line 353),
yet Stage 5 proceeded without awaiting resolution (EX-07).

**Evidence:** `e2e-rerun.log` lines 353, 395–421 (review-case creation followed immediately by
Stage 5 SIRO XML export); R4 D2; V5 corroboration; R4 INV-6 analysis.

**Affected requirements:** INV-6, FR20 (field completeness validation before export),
FR14 (manual review gate), Story 1.7 AC4  
**Risk:** Unvalidated, low-confidence regulatory XML submitted to CNBV/SIRO — direct regulatory
compliance exposure.

---

### High Severity

#### HIGH-1: Excel (DatosCargaOficio) Export Does Not Complete (FR18, EX-01)

**What:** The live pipeline re-run logged "Starting DatosCargaOficio layout generation for
expediente: EXP-9626-2021" at line 426 of `e2e-rerun.log`. No completion log, no
`ExportCompletedEvent(DatosCargaOficioXlsx)`, and no written file appeared within the 60-second
test window. The test gate failed at `MaxFidelityGateFullPipelineE2ETests.cs:141`. SIRO XML
export (FR15) completed successfully in the same run; the asymmetry is specific to the
DatosCargaOficio generation path, not a stage-level failure.

**Evidence:** `e2e-rerun.log` lines 426–450; `RERUN-SUMMARY.md` section "The failing assertion";
independently confirmed by R1, R2, R5, V5.

**Affected requirements:** FR18 (Excel layout generation)  
**Risk:** SIRO registration workflow is incomplete without the DatosCargaOficio Excel file; the
system cannot complete the mandatory submission bundle.

---

#### HIGH-2: SLA Dashboard Accessible Without Authentication (EX-05, V2)

**What:** `GET /sla-dashboard` returns HTTP 200 (body: 66,569 bytes of SLA deadline/escalation
content) to unauthenticated requests. `SlaDashboard.razor` has no `@attribute [Authorize]`. By
contrast, `/manual-review` and `/audit/viewer` correctly enforce authentication (HTTP 302 to
login). V2 directly confirmed this: live unauthenticated probe returned full SLA Dashboard content.

**Evidence:** Live probe `GET http://localhost:5172/sla-dashboard` → 200 (V2); `SlaDashboard.razor`
line 2: `@page "/sla-dashboard"` with no Authorize attribute (R5 EX-05); contrast with
`ManualReviewDashboard.razor` line 8: `@attribute [Authorize(Roles = "Reviewer,Admin")]`.

**Affected requirements:** FR30 (RBAC for operations), NFR8 (security), general financial
compliance security posture  
**Risk:** Case IDs, escalation levels, SLA deadlines, and timing data for active financial
investigations exposed to unauthenticated users.

---

#### HIGH-3: Analytics Dashboard Accessible Without Authentication (EX-12)

**What:** `GET /dashboard` returns HTTP 200 (78 KB rendered MudBlazor page) to unauthenticated
requests. Processing metrics (Total Documents, Success Rate, Queue state) are operational
information for a compliance platform.

**Evidence:** Live probe `GET http://localhost:5172/dashboard` → HTTP 200; `screen-dashboard.png`
(78,134 B full render) (R5 EX-12).

**Affected requirements:** FR30 (RBAC), NFR8 (security)  
**Risk:** Operational state of financial document processing system exposed publicly.

---

#### HIGH-4: Seven PRP-Specified Interfaces Absent (R4 D1, V3)

**What:** The PRP interface inventory defines 28 interfaces required across the processing stages.
Seven are absent from the production CSharp tree with zero declarations: `IFieldMatcher<T>`,
`IRuleScorer`, `IScanDetector`, `IScanCleaner`, `IReportGenerator`, `IUIBundle`, `IFieldAgreement`.
Of these, `IFieldMatcher<T>` is the most consequential — it is cited 7 times across the PRP
sequential flow as the mechanism for field matching, unified record generation, export validation,
and confidence-gated manual review routing.

**Evidence:** V3 grep: `interface IXxx` — 0 declarations each for all 7 names across
`01 Core / 02 Infrastructure / 03 Orchestration / 04 Services`. Confirmed by R4 Part B.

**V3 nuance:** The absence of named interfaces does not mean all capabilities are absent.
`FusionExpedienteService` provides partial Stage-2 consolidation (live evidence: fusion handoff
produced 1,075 B JSON in re-run). However, the PRP-specified interface contracts — which back
the export validation chain, confidence gating, and cross-format field agreement — are not
realized as designed.

**Affected requirements:** FR9 (field matching), FR20 (export completeness), FR14 (confidence
gating), INV-6, INV-10, Features F12/F13/F14/F18/F24/F28/F31/F33  
**Risk:** The architectural contract described in the PRP is partially unimplemented; future
development and test coverage cannot reference these contracts.

---

#### HIGH-5: Person Identity Deduplication Not Operational Across Documents (INV-2, FR10, F21)

**What:** `PersonIdentityResolverService.FindByRfcAsync` returns `null` with an explicit TODO:
"Database lookup deferred." `DeduplicatePersonsAsync` is in-memory only. The service is
registered only in Web UI — not in Athena or Orion workers. Cross-document person deduplication
(the stated requirement) is structurally impossible: the worker pipeline cannot invoke the service,
and even if it did, there is no persistent identity store.

**Evidence:** R4 INV-2 analysis; R4 F21; no `PersonIdentityResolver` log entry in `e2e-rerun.log`
(confirmed by R1 note 4).

**Affected requirements:** FR10 (RFC variant/alias dedup), INV-2, Story 1.4 AC2  
**Risk:** Duplicate person records for the same RFC across documents — downstream compliance
reporting would contain inflated or conflicting person counts.

---

#### HIGH-6: Azure Blob Storage Adapter Absent (CR8)

**What:** The PRD Technical Constraints section and CR8 require both a local filesystem adapter
and an Azure Blob Storage adapter. `FileSystemDownloadStorageAdapter` implements `IDownloadStorage`
for local FS and is present. No `BlobServiceClient`, `BlobContainerClient`, or Azure Storage blob
implementation exists anywhere in the production source. Two independent search passes by R3
confirmed this. No TODO or placeholder for a future Azure Blob adapter was found.

**Evidence:** R3 CR8: two independent grep passes for `AzureBlob`, `BlobServiceClient`,
`BlobContainerClient`, `Azure.Storage` returned zero production matches.

**Affected requirements:** CR8  
**Risk:** Cloud deployment (which requires Azure Blob) is blocked; the product cannot satisfy
its stated technical constraint.

---

### Medium Severity

#### MED-1: `/health/live` Returns 404 (EX-04, V2)

**What:** CLAUDE.md documents `/health/live` and `/health/ready` as the expected liveness and
readiness probes. Live probe confirms `/health/live` = 404 Not Found (endpoint not registered).
A Kubernetes liveness probe configured per documentation would receive 404 and may trigger
incorrect restart action.

**Evidence:** V2 live probe: `GET http://localhost:5172/health/live` → HTTP 404.

**Affected requirements:** NFR7 (uptime/SLA services)  

---

#### MED-2: PDF Page-Iteration Off-By-One (FR6, EX-02)

**What:** During PDF rasterization, the conversion loop iterates `page = 0..3` for a 3-page PDF
(should stop at `page = 0..2`). `ArgumentOutOfRangeException` is caught and logged as WRN at
line 288 of `e2e-rerun.log`. The pipeline continues (page 1 was successfully used), but for
PDFs where the last page has not already been processed, this error could mask content loss.

**Evidence:** `e2e-rerun.log` line 288: `[14:46:36 WRN] Stopping PDF conversion at page 3:
ArgumentOutOfRangeException — The page number must be between 0 and 2.`

**Affected requirements:** FR6 (scanned-PDF preprocessing)  

---

#### MED-3: Low-Confidence Classification Does Not Block Export (EX-06, EX-07)

**What:** Classification at confidence 10 created two ReviewCases but the pipeline proceeded
immediately to SIRO XML export. Fusion also flagged `NextAction: Revisión manual requerida`
with 1 conflict, which was similarly ignored. A compliance officer would not know whether the
exported SIRO XML incorporated a reviewed record or an unreviewed one.

**Evidence:** `e2e-rerun.log` lines 353, 395–421; V5 corroboration. (Note: this partly overlaps
with CRIT-2; MED-3 focuses on the pipeline behaviour gap, CRIT-2 on the missing architectural gate.)

**Affected requirements:** FR14, FR20, INV-10  

---

#### MED-4: TXT Extractor Returns Null for Critical Fields Despite 94.93% OCR Confidence (EX-08)

**What:** `AdaptiveTxtExtractor` returned `Expediente: null, Causa: null` with
`AdditionalFields count: 7` and logged "Successfully extracted fields" — claiming success while
failing the two most critical fields. Tesseract achieved 94.93% average confidence on the same
document. The XML companion correctly yielded `EXP-9626-2021`. Silent null extraction reduces
fusion quality.

**Evidence:** `e2e-rerun.log` lines 335–336: `AdaptiveTxtExtractor: Successfully extracted
fields - Expediente: null, Causa: null`.

**Affected requirements:** FR3, FR9  

---

#### MED-5: Health Endpoint Returns Bare "Unhealthy" with No Component Detail (EX-03)

**What:** `/health` returns HTTP 503 with body "Unhealthy" — no JSON breakdown, no component
detail, no machine-readable structure. NFR11 requires structured logging; the health response
does not meet even a basic structured-output bar.

**Evidence:** Live probe: `GET http://localhost:5172/health` → HTTP 503, body: "Unhealthy"
(R5 EX-03; MANIFEST Task 3).

**Affected requirements:** NFR7, NFR11  

---

#### MED-6: Development-Mode Nav Exposure Without Config Guard (R5 Usability)

**What:** `NavMenu.razor` line 143: `AllowDevNavigation => HostEnvironment.IsDevelopment() ||
Debugger.IsAttached`. In Development mode, all pages including auth-gated ones are listed with
"Every page is temporarily accessible while debugging." No config-guard prevents this being
inadvertently active in staging/production.

**Evidence:** R5 usability observation; `NavMenu.razor` line 143.

**Affected requirements:** NFR8, FR30  

---

### Low Severity

#### LOW-1: Login Page Displays Raw ASP.NET Scaffolding Text (EX-11)

**What:** `screen-login.png` shows "There are no external authentication services configured.
See this article about setting up this ASP.NET application…" with a hyperlink to an external
article — the default ASP.NET Identity scaffold text, exposing implementation details to
end-users in a professional financial compliance context.

**Evidence:** `screen-login.png` right panel; R5 EX-11.

**Affected requirements:** CR3 (UI/UX consistency)  

---

#### LOW-2: DOCX Embedded Image OCR Returns Zero-Length Text Silently (EX-09)

**What:** Tesseract OCR on a DOCX embedded image (`/word/media/image1.jpg`) produced Text
length: 0, Confidence avg: 0.00% with no fallback, retry, or user-visible warning. DOCX
Expediente was built with FieldsExtracted: 1 (empty expediente number).

**Evidence:** `e2e-rerun.log` lines 338–340.

**Affected requirements:** FR4 (DOCX extraction)  

---

#### LOW-3: Auth-Gated Nav Items Show Warning Icons to Unauthenticated Users (EX-10)

**What:** `screen-home.png` shows Manual Review, Export Management, and Audit Trail with warning
triangle badges in the sidebar to unauthenticated sessions. Visual treatment communicates
"broken" rather than "requires login."

**Evidence:** `screen-home.png`; `NavMenu.razor` AuthorizeView guard implementation (R5 EX-10).

**Affected requirements:** CR3  

---

## 6. Human Review Queue

Each item requires a specific human decision before it can be classified PASS or FAIL.

| ID | Item | Question for Human Reviewer |
|----|------|-----------------------------|
| HRQ-1 | INV-4 / FR17 — Audit immutability vs. 7-year retention deletion | `AuditRetentionBackgroundService` deletes rows (no archive-first step to an immutable store). FR17 says "immutable audit log." NFR9 requires 7-year retention with deletion. Are these reconcilable? Does the deletion path satisfy or violate the immutability requirement? Does regulatory compliance (UIF/CNBV) require tamper-evident storage (e.g., WORM) or is deletion after retention period acceptable? |
| HRQ-2 | NFR14 — Graceful file-error handling | `ProcessingOrchestrator`'s exception handler publishes `ProcessingErrorEvent` rather than directly chaining audit-write and manual-review-queue. Is the downstream event-subscription chain sufficient to satisfy NFR14, or must the exception path directly invoke `IAuditLogger` and `IManualReviewerPanel`? A human code trace through the event subscription graph is required. |
| HRQ-3 | NFR15 — Batch processing | The primary pipeline is event-driven (one document per `DocumentDownloadedEvent`). `IOcrProcessingService.ProcessDocumentsAsync` accepts `IEnumerable<ImageData>` with `maxConcurrency`. Does concurrent event-driven processing satisfy the PRD's "batch processing for high-volume regulatory periods" intent? |
| HRQ-4 | CR3 — MudBlazor UI consistency | Auth-protected pages (Manual Review, SLA Dashboard authenticated view, Export Management, Audit Trail, Processing) were not rendered in an authenticated session. Visual consistency of these screens against MudBlazor design standards requires a logged-in human reviewer. |
| HRQ-5 | CR4 — Additive-only DB schema | Two EF Core migrations drop FK constraints in their `Up()` methods (`DropAuditFileMetadataFk`, `DropReviewCaseFileMetadataFk`). CR4 requires "adding new tables without modifying existing table structures." Does dropping a FK constraint (not a column or table) constitute a CR4 violation? Owner judgment required. |
| HRQ-6 | FR14 — Manual review UI (authenticated) | The back-end correctly created ReviewCases for low-confidence results (confirmed live). The UI route `/manual-review` exists and redirects to login as expected. Authenticated UI behaviour — does the Blazor component correctly display, allow decision entry, and update the unified metadata record per Story 1.6 AC5? Requires login credentials. |
| HRQ-7 | FR30 — RBAC runtime enforcement | `[Authorize(Roles = "Reviewer,Admin")]` is applied structurally. Requires an authenticated session to verify runtime enforcement of role gates and that non-Reviewer/Admin roles are correctly denied. |
| HRQ-8 | INV-10 — Confidence-threshold gating | `IFieldMatcher<T>` is absent as a named interface. However, fusion produces a confidence score (0.83 in the live run). Does the existing `FusionExpedienteService` confidence output satisfy the PRP's confidence-gated routing intent, even without the named interface? Owner/architect judgment on design conformance. |
| HRQ-9 | INV-11 — Correlation-ID propagation depth | Application-level propagation via domain events and AuditRecord correlation IDs is confirmed. Is HTTP-layer `X-Correlation-ID` header injection required for the distributed tracing requirement, or does in-process event-chain propagation satisfy it in the current 3-process-split architecture? |
| HRQ-10 | NFR16 / NFR17 scope | PRD Reasoning Path 9 identifies Azure AD (NFR16) and field-level PII encryption (NFR17) as "Missing Requirements" but the synthesis states "Keep original requirements only." Are NFR16 and NFR17 binding for this release? If yes, both are unimplemented and would be FAIL. |
| HRQ-11 | EX-07 — Fusion conflict not blocking export | Fusion returned `NextAction: Revisión manual requerida` with 1 conflict. Is it a design decision or defect that the Reconciliator proceeds to export despite this flag? If it is a defect, it should be promoted to HIGH severity finding. |

---

## 7. Coverage Analysis

### Requirement Coverage

| Category | Total | Exercised at Runtime | Pass | Fail | NHR | NT |
|----------|-------|---------------------|------|------|-----|-----|
| FR | 32 | 16 (re-run pipeline) | 13 | 2 | 2 | 15 |
| NFR | 17 | 3 (retention, signing, correlation) | 6 | 0 | 2 | 9 |
| CR | 8 | 5 (source inspection) | 5 | 1 | 2 | 0 |
| **Total** | **57** | **24** | **24** | **3** | **6** | **24** |

### Feature Coverage

| Category | Total | Pass | Fail | NHR | NT |
|----------|-------|------|------|-----|-----|
| Features | 31 | 2 | 9 | 2 | 18 |
| Invariants | 11 | 1 | 3 | 3 | 4 |

### NOT TESTED Drivers

The following gaps drove the large NOT TESTED count and represent the primary evidence limitations
of this review:

1. **Corpus gap (primary):** The SIARA simulator's `bulk_generated_documents_all_formats/` corpus
   was unavailable or not wired in `appsettings.json` for the first E2E run. A manual simulator
   reset enabled the re-run. Without a stable, pre-seeded corpus, Stage 1 ingestion cannot be
   reliably triggered for most test paths.

2. **No authenticated Web UI session:** Five of eight screenshotted routes redirected to login.
   All authenticated UI flows (manual review, SLA dashboard authenticated view, audit trail,
   export management, processing) were not verified. An authenticated session with Reviewer and
   Admin roles is required.

3. **DB disconnected for live Web UI:** `appsettings.json` hardcodes a non-available SQL Server.
   All DB-backed screens showed empty data. The `/health` endpoint returned 503, which also
   prevents health-check probes from confirming readiness.

4. **No performance instrumentation:** All latency/throughput NFRs (NFR1, NFR3, NFR4, NFR5, NFR6,
   NFR7) are NOT TESTED. No timing data was collected from the E2E re-run.

5. **Live SIARA portal not exercised:** FR1 was tested against the simulator (not the production
   SIARA portal). Real portal authentication, TLS certificate chains, and network timeouts were
   not validated.

6. **Signed PDF excluded from DI:** FR16 (`DigitalPdfSigner`) is implemented and tested but
   deliberately excluded from the Reconciliator's DI composition (only `AddSiroExportServices`
   called). Runtime exercise requires DI wiring change.

7. **CSnakes/Python dormant by design:** `Extraction.Python` tests (0 executed) and all
   VLM/GOT-OCR2 paths are intentionally dormant per ADR-001. NOT TESTED is expected and correct.

### Confidence Level

**Medium-Low.** The pipeline execution evidence (re-run) is real and valuable — 9 confirmed
`INSERT INTO [AuditRecords]`, a 643-byte SIRO XML file written, a 1,075-byte fusion handoff JSON,
and a live classification result. However, the corpus dependency for E2E runs is fragile (one
successful run from a specific simulator state). The majority of requirements could not be
exercised: 15/32 FRs and 9/17 NFRs are NOT TESTED. The 58 Human Review items (HRQ-1 through
HRQ-11) require resolution before confidence can be upgraded.

---

## 8. Risk Assessment

### Critical Risks (block staging)

| Risk | Evidence | Affected Requirements |
|------|----------|-----------------------|
| Legal compliance breach: non-notification enforcement absent | CRIT-1; R2 FR31; R4 INV-5; V5 | FR31, INV-5 |
| Unvalidated documents exported to SIRO without full field gate | CRIT-2; EX-06; re-run lines 395–421 | FR20, INV-6 |
| Audit immutability unresolved (owner decision required) | INV-4; V4 HRQ-1 | FR17, NFR9 |

### High Risks (must resolve before staging)

| Risk | Evidence | Affected Requirements |
|------|----------|-----------------------|
| Excel export does not complete — submission bundle incomplete | HIGH-1; FR18 FAIL | FR18 |
| SLA dashboard and analytics dashboard exposed unauthenticated | HIGH-2, HIGH-3; V2 | FR30, NFR8 |
| 7 PRP interfaces absent — architectural contracts not fulfilled | HIGH-4; V3 | FR9, FR20, F12–F33 |
| Person dedup not operational — duplicate RFC records inevitable | HIGH-5; INV-2 | FR10 |
| Azure Blob adapter absent — cloud deployment blocked | HIGH-6; CR8 FAIL | CR8 |

### Medium Risks (should resolve before staging)

| Risk | Evidence |
|------|----------|
| /health/live 404 — liveness probe misconfigured for K8s | MED-1; V2 |
| PDF page-iteration off-by-one — could mask content loss | MED-2; e2e-rerun.log:288 |
| Low-confidence export proceeds without review gate | MED-3; EX-06/07 |
| TXT extractor silent null for critical fields | MED-4; e2e-rerun.log:335 |
| Health endpoint provides no triage signal | MED-5; EX-03 |
| Dev-mode nav exposure without staging guard | MED-6; NavMenu.razor:143 |
| 15 FRs and 9 NFRs untested — coverage gap for staging | Coverage Analysis §7 |

### Low Risks (address in next sprint)

| Risk | Evidence |
|------|----------|
| Login page raw scaffold text — professional presentation | LOW-1; EX-11 |
| DOCX image OCR silent zero-result — degraded extraction | LOW-2; EX-09 |
| Auth-gated nav warning icons — UX confusion | LOW-3; EX-10 |

---

## 9. Deployment Recommendation

**NOT READY FOR STAGING**

**Rationale:**

Three Critical findings independently block staging readiness:

1. **CRIT-1 (non-notification enforcement absent):** Deploying to staging with zero enforcement
   of the "no client notification without legal permission" rule risks live violation of Mexican
   UIF/CNBV legal constraints. SignalR broadcasts are unconditional. This is not a UX gap — it
   is a legal compliance gap with no code path to close it at runtime.

2. **CRIT-2 (export completeness gate bypassed):** The live pipeline emits SIRO XML for
   documents with classification confidence of 10 (out of 100), with unresolved fusion conflicts,
   without awaiting manual review resolution. A staging environment connected to any downstream
   SIRO ingestion system could submit invalid regulatory filings.

3. **HIGH-1 (Excel export fails):** The DatosCargaOficio Excel file — the second mandatory
   component of the SIRO submission bundle — does not complete in the E2E gate. The SIRO
   registration workflow cannot be completed end-to-end.

Beyond these three blockers, the combination of two unauthenticated route exposures (SLA
dashboard, analytics dashboard) serving financial operational data, seven absent PRP-specified
interfaces, and non-operational person identity deduplication across documents represents
material risk for a financial regulatory compliance system processing Mexican legal documents
under UIF/CNBV supervision.

**Minimum bar for re-evaluation:**
- CRIT-1: Implement non-notification enforcement gate in the broadcast path.
- CRIT-2: Wire a low-confidence/conflict block before Stage 5 export; resolve IFieldMatcher
  gap or equivalent completeness gate.
- HIGH-1: Fix `DatosCargaOficioLayoutGenerator` to emit `ExportCompletedEvent(DatosCargaOficioXlsx)`.
- HIGH-2/3: Add `@attribute [Authorize]` to `SlaDashboard.razor` and `/dashboard`.
- HRQ-1 (INV-4): Owner ruling on audit immutability required before staging with live audit data.

---

## 10. Audit Statement

All findings in this report were produced from an independent evaluation of the current system
version against the Product Requirements Document. No previous reports, issue trackers, release
notes, or historical assessments were used to determine PASS, FAIL, NEEDS HUMAN REVIEW, or NOT
TESTED outcomes. All conclusions are based exclusively on evidence collected during the current
review.

---

## 11. Independence Limitations

**CLAUDE.md context injection (disclosed honestly).** The owner's ruling was *strict* independence.
To enforce it, a **sanitized** `CLAUDE.md` (release-status, "Done/Partial/Planned", and known-issues
commentary stripped; only neutral build/test/architecture facts retained) was prepared and written to
disk **before** the reviewers ran. **Ground-truth check shows the substitution did not take effect for
the reviewer subagents:** the harness injects the *session-cached* (original) `CLAUDE.md` into subagents
regardless of the on-disk swap. Evidence — reviewer files `R3-NFR-CR.md` and `R4-invariants-features.md`
cite "CLAUDE.md documents this as optionality-by-design (ADR-001)" and "dormant by design per CLAUDE.md /
per owner decision," language that exists **only in the original** CLAUDE.md. The reviewers therefore
received the full CLAUDE.md, including its release-status and owner-decision commentary. **The strict-isolation
goal was NOT fully achieved.**

Mitigations that *did* hold (verified):
- **Deny-list held.** No reviewer read `docs/planning*`, `docs/planning-artifacts*`, the gap matrices, the
  RC0–RC6 readiness challenge, prior QA reports, or git history (verified by inspecting every citation in the
  five reviewer files — all point to the PRD, the PRP, source, the live app, or this run's evidence bundle).
- **Verdicts were evidence-driven and frequently CONTRADICTED CLAUDE.md's prior "DONE" claims** — e.g. the Excel
  export (FR18), non-notification enforcement (FR31), Azure Blob adapter (CR8), and cross-document identity
  deduplication were all judged FAIL/absent against a CLAUDE.md that presents the system as largely complete.
  That divergence is positive evidence the prior framing did not drive the conclusions.

Residual risk (disclosed): dispositions that *leaned on* CLAUDE.md's stated intent rather than first-hand
evidence are the least independent and should be re-checked by a human if independence is paramount — specifically
**CR2** (Python/CSnakes "optionality-by-design / ADR-001") and the related "dormant by design" Python items.
The **deployment recommendation rests on evidence-backed Critical/High findings** (live route probes, source
state, runtime logs), **not** on CLAUDE.md framing, and is therefore unaffected by this contamination.

**Adversarial verification provenance.** The planned independent adversarial-refutation pass was attempted but
its subagent terminated on a transient API connection error before returning. The two Critical and six High
findings were instead re-verified **first-hand by the orchestrator**: CRIT-1 (no legal gate before client
notification) and CRIT-2 (low-confidence classification publishes `DocumentFlaggedForReviewEvent`, then
`return result;` and proceeds to Stage-5 export — `ReconciliationOrchestrator.cs:243-279`) by source reading;
HIGH-2/HIGH-3 (`/sla-dashboard` and `/dashboard` → HTTP 200 anonymous) by live probe; HIGH-4 (seven PRP
interfaces — 0 declarations in production), HIGH-5 (`FindByRfcAsync` returns null per its own behavioural
contract) and HIGH-6 (no Azure Blob client) by source grep; HIGH-1 (Excel export) by the live run log plus
three concurring reviewers.

**Environment limitations:**

- **DB disconnected for live Web UI:** `appsettings.json` hardcodes `Server=DESKTOP-FB2ES22\SQL2022`,
  which was not available. All DB-backed Web UI screens (SLA dashboard, manual review case list,
  audit trail, export history) showed empty data. Auth-protected pages could not be navigated.
- **Capstone E2E required simulator-state reset:** The first run failed because the SIARA simulator
  served 0 document links — root cause established: the simulator's `cases.json` (its served-cases
  ledger) already listed all 500 corpus cases with `ResetCasesOnStartup:false`, so `GetNextAvailableCaseId`
  returned null and no arrivals were scheduled. After resetting that ledger (`ResetCasesOnStartup:true` +
  removing the stale ledger — a *test-environment* change only; no product/repo code touched), a re-run
  produced the live pipeline evidence used in this report. Single-run evidence for end-to-end behaviour is noted.
- **Signed PDF (FR16) excluded from DI:** `DigitalPdfSigner` is intentionally excluded from
  the Reconciliator's DI composition per the source comment. Runtime exercise was not possible.
- **Live SIARA portal not exercised:** All ingestion evidence uses the local SIARA simulator
  (port 5001/5002). Production portal authentication, network latency, and real-world certificate
  chains were not validated.
- **No performance instrumentation:** Zero timing measurements were collected. All latency,
  throughput, and uptime NFRs are NOT TESTED.
- **No authenticated Web UI session:** No test credentials were available, so authenticated UI flows are
  unverified. (Note: live probing showed several routes named in the Phase-1 screenshots — `/processing`,
  `/review`, `/export`, `/audit`, `/sla` — actually return HTTP 404, i.e. they are route-name mismatches,
  not auth redirects; the genuinely reachable-anonymous routes are `/`, `/dashboard`, and `/sla-dashboard`.)

---

## Appendix A — Full Traceability Table

### FR Traceability

| ID | Requirement | Status | Primary Evidence |
|----|-------------|--------|-----------------|
| FR1 | Browser-automated download (PDF/XML/DOCX) | PASS | `e2e-rerun.log` lines 138–229 |
| FR2 | Checksum dedup + download history | NOT TESTED | Journal creation noted; dedup path not exercised |
| FR3 | XML metadata extraction | PASS | `e2e-rerun.log` line 337: EXP-9626-2021, 3 fields |
| FR4 | DOCX extraction | PASS (partial) | `e2e-rerun.log` line 340: 1 field, blank expediente |
| FR5 | PDF extraction with OCR fallback | PASS | `e2e-rerun.log` lines 300–328: 1386 chars, 94.93% |
| FR6 | Scanned-PDF detect + preprocessing | PASS (partial) | `e2e-rerun.log` lines 267–299; off-by-one WRN:288 |
| FR7 | Level-1 classification | PASS | `e2e-rerun.log` line 396: Type=Aseguramiento |
| FR8 | Level-2/3 subclassification | NOT TESTED | No L2/L3 logged in live run |
| FR9 | Multi-source field matching + confidence | PASS | `e2e-rerun.log` line 353: Confidence=0.83, Conflicts=1 |
| FR10 | Identity resolution (RFC dedup) | NOT TESTED | No PersonIdentityResolver log in live run |
| FR11 | Legal-directive classification | NOT TESTED | No LegalDirectiveClassifier log in live run |
| FR12 | SLA deadline calculation | NOT TESTED | 0 active SLA records; no INSERT INTO [SLAStatus] |
| FR13 | SLA breach escalation | NOT TESTED | Depends on FR12; never triggered |
| FR14 | Manual review interface | NHR (back-end PASS) | `e2e-rerun.log` lines 407–420: 2 ReviewCases inserted |
| FR15 | SIRO XML export + schema validation | PASS | `e2e-rerun.log` lines 422–425: 643 B written |
| FR16 | Digitally-signed PDF export | NOT TESTED | Excluded from Reconciliator DI composition |
| FR17 | Immutable audit log | PASS | `e2e-rerun.log`: 9 INSERT INTO [AuditRecords] confirmed |
| FR18 | Excel (DatosCargaOficio) export | FAIL | `e2e-rerun.log` lines 426–450: started, never completed |
| FR19 | PDF content summarization to requirement categories | NOT TESTED | No PdfRequirementSummarizer log in live run |
| FR20 | Field completeness validation before export | FAIL (structural) | INV-6: only 3-header check; gate not in call chain |
| FR21 | Retry transient failures (exponential backoff) | NOT TESTED | No transient failures induced in run |
| FR22 | Queue failed documents for retry | NOT TESTED | No IFailedDocQueue found in production source |
| FR23 | Error-recovery workflows per stage | NOT TESTED | No per-stage recovery class found |
| FR24 | Persist domain entities via EF Core | PASS | Live DB inserts; migrations applied to Testcontainer |
| FR25 | Data migration scripts for schema evolution | PASS | 5 incremental EF Core migrations confirmed |
| FR26 | Referential integrity between entities | PASS | FK configurations in EF Core entities confirmed |
| FR27 | Notification integration (email/SMS/Slack) | NOT TESTED | No INotificationSender in production source |
| FR28 | REST API for manual review UI | NOT TESTED | Blazor direct injection used; REST surface for review absent |
| FR29 | Webhook callbacks on export completion | NOT TESTED | No webhook infrastructure found |
| FR30 | RBAC for manual review operations | NHR | `[Authorize(Roles="Reviewer,Admin")]` confirmed; runtime unverified |
| FR31 | Non-notification enforcement | FAIL | Zero enforcement code in production source (R2, R4, V5) |
| FR32 | Data retention with automated archival/deletion | PASS | `AuditRetentionBackgroundService` ran live; 7-yr cutoff confirmed |

---

### NFR Traceability

| ID | Requirement | Status | Primary Evidence |
|----|-------------|--------|-----------------|
| NFR1 | OCR memory usage baseline ≤20% | NOT TESTED | No memory profiling data collected |
| NFR2 | Async concurrent processing | PASS | `IOcrProcessingService` contract; concurrent Testcontainers spin-up |
| NFR3 | Browser ops < 5 s | NOT TESTED | No browser timing captured (90s failure first run) |
| NFR4 | Extraction < 2 s (XML/DOCX), < 30 s (PDF OCR) | NOT TESTED | No timing logged in evidence |
| NFR5 | Classification < 500 ms | NOT TESTED | No per-doc timing logged |
| NFR6 | Horizontal scaling via stateless microservices | NHR | 4 worker hosts present; load test not performed |
| NFR7 | 99.9% uptime for SLA services | NOT TESTED | Single cold-boot probe insufficient; 503 = expected env issue |
| NFR8 | Encryption at rest + TLS 1.3 in transit | NOT TESTED | No SslProtocols.Tls13 found; no DB encryption config |
| NFR9 | Audit retention ≥ 7 years | PASS | `AuditOptions.RetentionYears=7`; live enforcement run confirmed |
| NFR10 | Digital signature X.509 | PASS | `DigitalPdfSigner.cs`: X509Certificates + Azure KeyVault; DI-wired |
| NFR11 | Structured logging + correlation IDs | PASS | Serilog on all 4 hosts; CorrelationId on all AuditRecords |
| NFR12 | Backward compatibility with OCR interfaces | PASS | `IFieldExtractor`, `IFieldExtractor<T>`, `IOcrExecutor`, `IImagePreprocessor` all present |
| NFR13 | Config-driven matching policies | PASS | `MatchingPolicyOptions` bound from `configuration.GetSection("MatchingPolicy")` |
| NFR14 | Graceful file-error handling | NHR | Orchestrator publishes event, not direct chain; V2 human trace needed |
| NFR15 | Batch processing | NHR | Interface supports bulk; primary pipeline is event-driven per-doc |
| NFR16 | Azure AD / Identity Server auth | NOT TESTED | ASP.NET Core Identity used; PRD scope ambiguous (Reasoning Path 9) |
| NFR17 | Field-level PII encryption | NOT TESTED | Not implemented; PRD scope ambiguous (Reasoning Path 9) |

---

### CR Traceability

| ID | Requirement | Status | Primary Evidence |
|----|-------------|--------|-----------------|
| CR1 | Retain IFieldExtractor, IOcrExecutor, IImagePreprocessor | PASS | Glob of Domain/Interfaces: all 3 files present |
| CR2 | Retain Python/CSnakes modules | PASS | `AddPrismaPythonEnvironment()` commented (dormant-by-design, ADR-001) |
| CR3 | MudBlazor UI/UX consistency | NHR | Shell renders; auth-protected pages not visually verified |
| CR4 | Additive-only DB schema changes | NHR | `DropForeignKey` in 2 migrations' `Up()`; owner must rule |
| CR5 | Result<T> for all new interface methods | PASS | All inspected interfaces return `Task<Result<T>>`; build 0/0 |
| CR6 | Hexagonal boundaries (interfaces in Domain, impls in Infrastructure) | PASS | Structure confirmed; architecture tests 19/19 green (prior run) |
| CR7 | .NET 10 + Python 3.9+ runtimes | PASS | .NET 10.0.301; Python pinned at 3.12.4 |
| CR8 | Local FS + Azure Blob Storage adapters | FAIL | `FileSystemDownloadStorageAdapter` present; no BlobServiceClient found |

---

### Invariant Traceability

| ID | Invariant | Status | Primary Evidence |
|----|-----------|--------|-----------------|
| INV-1 | Checksum uniqueness (no dup downloads) | NOT TESTED | `IDownloadTracker` present; zero download ops in re-run |
| INV-2 | Identity dedup across documents | FAIL | `FindByRfcAsync` returns null (TODO); in-memory only; not on pipeline |
| INV-3 | Referential integrity (Expediente/Persona/Oficio) | NOT TESTED | No DbSet<Expediente/Oficio>; entities embedded in JSON |
| INV-4 | Audit immutability (append-only) | NHR | V4: append-only logger + sanctioned 7-yr deletion; no DB guard |
| INV-5 | Non-notification safety | FAIL | Zero enforcement code; broadcasts unconditional (R2, R4, V5) |
| INV-6 | Field completeness gate before export | FAIL | `e2e-rerun.log`: CF=10 → ReviewCases → SIRO XML (no gate) |
| INV-7 | Additive-only DB schema (no DROP in Up()) | PASS | All 12 migrations inspected; DROP only in Down() |
| INV-8 | OCR backward-compatibility | NOT TESTED | Build 0/0; OCR path not exercised in evidence run |
| INV-9 | SLA business-day correctness incl. Mexican holidays | NOT TESTED | `MexicoBusinessDayCalculator` wired; no SLA calc exercised |
| INV-10 | Confidence-threshold gating | NHR | IFieldMatcher<T> absent; gate signal source unclear |
| INV-11 | Correlation-ID propagation | NHR | App-level event propagation confirmed; HTTP-layer header not wired |

---

*End of Phase-2 Final Report*
