# Phase-2 Gap-Closure Tracker

**Source of truth for findings:** `docs/qa/phase2-review/PHASE2-FINAL-REPORT.md` (+ R1–R5 + VERIFICATION-NOTES).
**Branch:** `Liv`. **Created:** 2026-06-20. **Status legend:** ☐ pending · ◐ in-progress · ☑ done · ⛔ blocked (owner decision).
**For the executing agent:** each task is self-contained — file path, what to change, Definition of Done (DoD) with the *evidence to produce*. Verify from ground truth (build the touched project + a test); do NOT mark ☑ without the evidence. Build single projects (E: drive slow). xUnit v3 / MTP: `dotnet test <proj>` (NO `--nologo`), filter `--filter-query "/Asm/Ns/Class/Method"`, capture `--report-trx`. Result<T> + CancellationToken conventions apply to all new code.

> **Sequencing rule:** resolve the ⛔ owner-decision items (Section E) FIRST where a code task depends on them (noted per task). Then do Section A (Critical) → B (High) → C (Medium/Low) → D (coverage/NOT-TESTED). Re-run the capstone E2E + harness integration after A/B to prove closure.

---

## Section A — CRITICAL (block staging)

### G-C1 ☐ — Enforce non-notification (FR31 / INV-5)
- **Finding:** No code gates client notification on legal-directive permission; `ProcessingHub`/SLA escalation broadcast unconditionally. (Report CRIT-1; R2; R4 INV-5.)
- **Where:** SignalR/notification emit paths — `04 Services/.../ProcessingHub*`, SLA escalation publisher, any `Notify*`/`Broadcast*`. (Grep `Notify`, `Broadcast`, `Publish.*Escalation`.)
- **Do:** Introduce a non-notification guard: before any *client-facing* notification, check the case's legal-directive `AllowsClientNotification` (add to the directive/compliance-action model if absent; default DENY). Block + audit-log when not allowed.
- **DoD:** new unit test proving a directive that forbids notification ⇒ no client broadcast emitted (NSubstitute on the hub/notifier) AND an audit row records the suppression; build 0/0; `--report-trx` captured. Evidence path: `docs/qa/phase2-review/closure-evidence/G-C1.trx`.
- **Depends on:** none (default-deny is safe regardless of HRQ outcomes).

### G-C2 ☐ — Gate Stage-5 export on review/confidence (FR14 / FR20 / INV-6)
- **Finding:** Low-confidence classification publishes `DocumentFlaggedForReviewEvent` then `return result;` and **proceeds to export**. Fusion `Revisión manual requerida` also does not block. Unreviewed/low-confidence records produce regulatory output. (Report CRIT-2 + EX-06/EX-07; live `e2e-rerun.log`.)
- **Where:** `04 Services/Athena/Prisma.Athena.Processing/ReconciliationOrchestrator.cs:243-279` (low-confidence branch) and `ExecuteStage5ExportAsync` (line ~286). Threshold const `ClassificationConfidenceThreshold = 70`.
- **Do:** When `RequiresManualReview` is true (confidence < threshold) OR fusion `NextAction == "Revisión manual requerida"`/has unresolved conflicts, **do NOT run Stage 5**; route the case to the manual-review queue and emit a "held for review" state instead of `ExportCompletedEvent`. Only export after a review decision marks it approved. Make the policy explicit + configurable.
- **DoD:** test: a sub-threshold/conflicted case ⇒ no `ExportCompletedEvent`, a ReviewCase persisted, case status = held; an approved case ⇒ export proceeds. Build 0/0; TRX at `closure-evidence/G-C2.trx`.
- **Depends on:** HRQ-11 (confirm "block on conflict" is the desired policy — owner). If owner says flag-only-by-design, downgrade to documentation instead of a block.

---

## Section B — HIGH

### G-H1 ☐ — Excel/DatosCargaOficio export never completes (FR18)
- **Finding:** Stage 5 logs `Starting DatosCargaOficio layout generation` then no completion + no `ExportCompletedEvent(DatosCargaOficioXlsx)` within 60 s; SIRO XML leg on the same run succeeded. (Report HIGH-1; 3 reviewers; `e2e-rerun.log:426`.)
- **Where:** `DatosCargaOficioLayoutGenerator` + `AdaptiveExporter` + the Stage-5 export wiring in the Reconciliator (`AddDatosCargaOficioExportServices`).
- **Do:** Reproduce in isolation (unit/integration test invoking the layout generator on the live-run expediente `EXP-9626-2021` shape). Find why it hangs / never emits (likely: ClosedXML exception swallowed, await never completing, or event not published). Fix so it completes and emits the event, or fails loudly with an audited error.
- **DoD:** the capstone E2E `MaxFidelityGateFullPipelineE2ETests` passes (both export events arrive); OR a dedicated test proves the generator emits the xlsx event. TRX at `closure-evidence/G-H1.trx`.

### G-H2 ☐ — `/sla-dashboard` served to anonymous users (security)
- **Finding:** `GET /sla-dashboard` → HTTP 200 anonymous, renders real SLA dashboard (66 KB, deadline/escalation columns). No `[Authorize]`. (Report HIGH-2; live probe V2.)
- **Where:** `07 UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages/SlaDashboard.razor`.
- **Do:** Add `@attribute [Authorize(Roles = "Reviewer,Admin")]` (match the policy used on protected pages). Re-verify anon ⇒ redirect/deny.
- **DoD:** live probe `curl -o /dev/null -w '%{http_code}' /sla-dashboard` as anon ⇒ 302/401 (not 200); a `Tests.UI` test asserts the redirect. Evidence: `closure-evidence/G-H2-probe.txt`.

### G-H3 ☐ — `/dashboard` served to anonymous users
- **Finding:** `GET /dashboard` → HTTP 200 anonymous (operational processing metrics). (Report HIGH-3; live probe.)
- **Where:** the dashboard page razor under `07 UI/.../Components/Pages/` (find `@page "/dashboard"`).
- **Do:** Decide if `/dashboard` should be public; if it exposes processing metrics/case data, add `[Authorize]`. (Cross-check HRQ-4 scope.)
- **DoD:** anon probe ⇒ non-200 if protected, OR a written rationale if intentionally public. Evidence: `closure-evidence/G-H3-probe.txt`.

### G-H4 ⛔ — Seven PRP interfaces absent (decide: implement vs de-scope)
- **Finding:** `IFieldMatcher<T>`, `IRuleScorer`, `IScanDetector`, `IScanCleaner`, `IReportGenerator`, `IUIBundle`, `IFieldAgreement` — 0 declarations in production (`01-04`). `IFieldMatcher<T>` backs 7 PRP sequential-flow steps. (Report HIGH-4; grep V3.)
- **Nuance (VERIFICATION-NOTES V3):** the *capability* of field consolidation runs via `FusionExpedienteService` (fusion executed live). So this is an ITDD-inventory conformance gap, not necessarily a functional hole.
- **Do:** Per interface, owner/architect decides: (a) implement to the PRP contract, or (b) formally de-scope and update the PRP to reflect the fusion-based design. Start with `IFieldMatcher<T>` (highest impact).
- **DoD:** a decision record per interface + (if implement) interface + impl + tests; (if de-scope) PRP amended + note in report.
- **Depends on:** HRQ-8 (does `FusionExpedienteService` confidence output satisfy the matcher intent?).

### G-H5 ☐ — Cross-document identity dedup non-functional (FR10 / INV-2)
- **Finding:** `PersonIdentityResolverService.FindByRfcAsync` returns `null` (its own behavioural contract `PersonIdentityResolverContract` asserts `ReturnsSuccessWithNullValue`); no DB persistence; not registered in Athena/Orion worker pipeline hosts. (Report HIGH-5; R4 INV-2.)
- **Where:** `02 Infrastructure/Infrastructure.Classification/PersonIdentityResolverService.cs:166` + worker DI composition roots.
- **Do:** Implement DB-backed RFC/alias lookup + dedup persistence (Persona table); register the service in the worker pipeline that creates Persona records; update the behavioural contract.
- **DoD:** integration test (Testcontainers SQL): two documents with RFC variants for the same person ⇒ one deduplicated Persona row. TRX at `closure-evidence/G-H5.trx`.
- **Depends on:** FR24/FR26 persistence (already PASS).

### G-H6 ⛔ — Azure Blob Storage adapter absent (CR8)
- **Finding:** No `BlobServiceClient`/`BlobContainerClient` in production; only local FS adapter. PRD CR8 requires both local FS + Azure Blob. (Report HIGH-6; R3.)
- **Where:** `02 Infrastructure/Infrastructure.FileStorage*` (find the `IDownloadStorage`/storage adapter abstraction).
- **Do:** Owner decides if Azure Blob is in-scope for this release. If yes: implement an `AzureBlobStorageAdapter` behind the existing storage interface + config + tests. If no: formally de-scope CR8 for this release.
- **DoD:** (implement) adapter + test against Azurite/Testcontainers; OR (de-scope) decision recorded + CR8 reclassified.
- **Depends on:** owner scope decision.

---

## Section C — MEDIUM / LOW (from R5 exploratory)

### G-M1 ☐ — `/health/live` returns 404 (NFR-ops)
- **Finding:** `/health/live` → 404 (liveness probe not registered); `/health` → 503, `/health/ready` → 503. (Report; live probe; R5 EX-04.)
- **Where:** Web UI `Program.cs` health-check endpoint mapping.
- **Do:** Register `/health/live` (liveness, no DB dependency) + keep `/health/ready` (readiness incl. DB). Liveness should be 200 when the process is up.
- **DoD:** anon `/health/live` ⇒ 200; `/health/ready` ⇒ 503 when DB down. Evidence: `closure-evidence/G-M1-probe.txt`.

### G-M2 ☐ — PDF page-index off-by-one (FR6)
- **Finding:** `Stopping PDF conversion at page 3: ArgumentOutOfRangeException — page must be between 0 and 2` on a 3-page PDF; loop iterates one past the last page. (Report; `e2e-rerun.log:288`; R1/R5.)
- **Where:** the PDF→image rasterisation loop in the imaging/OCR-preprocess path (grep `page number must be between` / the PDF conversion loop).
- **Do:** Fix loop bound (`< pageCount`, not `<= pageCount`). Verify no truncation on >3-page PDFs.
- **DoD:** unit test on a multi-page PDF ⇒ no exception, all pages converted. TRX at `closure-evidence/G-M2.trx`.

### G-M3 ☐ — Login page shows raw ASP.NET scaffolding text (usability)
- **Finding:** Login page renders "There are no external authentication services configured. See this article…" — scaffolding boilerplate on a compliance product. (Report §5; R5.)
- **Where:** `07 UI/.../Components/.../Login*.razor` (Identity UI).
- **Do:** Remove/replace the external-auth scaffolding block with product-appropriate copy.
- **DoD:** login page no longer contains the scaffolding sentence (curl/grep). Evidence: `closure-evidence/G-M3.txt`.

### G-M4 ☐ — `/health` body unstructured (NFR11 operability)
- **Finding:** `/health` returns the single word `Unhealthy` with no per-check detail. (Report §5; R5.)
- **Where:** health-check `ResponseWriter` in Web UI `Program.cs`.
- **Do:** Emit structured JSON (per-check name/status/duration) via a custom `ResponseWriter`.
- **DoD:** `/health` returns JSON with per-check entries. Evidence: `closure-evidence/G-M4.txt`.

---

## Section D — NOT TESTED coverage closure (raise confidence)

### G-D1 ☐ — Stabilise the SIARA corpus + simulator for repeatable E2E
- **Why:** the capstone E2E depends on a corpus + simulator served-ledger state; first run served 0. (Coverage driver #1.)
- **Do:** Commit a small, fixed seed corpus path OR a deterministic CI fixture; set the simulator's `ResetCasesOnStartup:true` for test runs; document the run recipe. (Note: corpus is gitignored generated data — decide on a committed minimal fixture vs a generator step in CI.)
- **DoD:** two consecutive clean capstone runs serve documents without manual intervention.

### G-D2 ☐ — Provide a seeded auth session (Reviewer + Admin) for UI verification
- **Why:** all authenticated UI flows NOT TESTED (HRQ-4/6/7). (Coverage driver #2.)
- **Do:** Add a test-only seeded Reviewer + Admin user + a Playwright login fixture; capture authenticated screenshots of Manual Review, SLA Dashboard, Audit, Export.
- **DoD:** authenticated screenshots of all 5 protected screens in `closure-evidence/ui/`.

### G-D3 ☐ — Performance instrumentation for NFR1/3/4/5
- **Why:** all latency NFRs NOT TESTED (no timings). (Coverage driver #4.)
- **Do:** Add `Stopwatch`/OpenTelemetry timing around OCR, extraction, classification, browser ops; assert against NFR targets (<30s PDF OCR, <2s XML/DOCX, <500ms classify, <5s browser).
- **DoD:** a perf test emitting measured durations vs targets. TRX/CSV at `closure-evidence/perf/`.

### G-D4 ⛔ — Signed-PDF (FR16): wire into DI or confirm de-scope
- **Why:** `DigitalPdfSigner` implemented + tested but excluded from the Reconciliator DI graph (only `AddSiroExportServices`). NOT TESTED at runtime. (Coverage driver #6.)
- **Do:** Owner decides: in-scope ⇒ wire `AddExportServices`/signer into the Reconciliator + cert config + E2E proof; out-of-scope ⇒ confirm (PRD FR16 says "supports… PAdES") and record.
- **Depends on:** owner scope decision (signed-PDF was previously treated as P2).

---

## Section E — HUMAN REVIEW QUEUE (⛔ owner/architect decisions — resolve first where a task depends on them)

> These are open in Notepad++ alongside the relevant source files. Each is a decision, not a code task. Record the decision inline (replace ⛔ with the ruling + date), then unblock the dependent G-task.

| ID | Decision needed | File(s) to inspect | Unblocks |
|----|-----------------|--------------------|----------|
| HRQ-1 | Does 7-yr retention *deletion* violate "immutable audit log" (FR17)? Is WORM/tamper-evidence required by UIF/CNBV? | `…/Infrastructure.Database/Services/AuditRetentionBackgroundService.cs` | audit-immutability disposition |
| HRQ-2 | Does NFR14 require the exception path to *directly* chain audit-write + review-queue, or is the event-subscription chain sufficient? | `…/Athena/Prisma.Athena.Processing/ProcessingOrchestrator.cs` | NFR14 verdict |
| HRQ-3 | Does concurrent event-driven processing satisfy "batch processing" (NFR15)? | `IOcrProcessingService` impl | NFR15 verdict |
| HRQ-4 | Visual MudBlazor consistency of the 5 protected screens (needs login). | `…/Web.UI/Components/Pages/*.razor` | CR3 verdict + G-D2 |
| HRQ-5 | Does dropping FK constraints in migrations violate CR4 "additive-only"? | `…/Migrations/20260613142328_DropAuditFileMetadataFk.cs`, `20260615195417_DropReviewCaseFileMetadataFk.cs` | CR4 verdict |
| HRQ-6 | FR14 authenticated UI: does Manual Review correctly display/edit/update per Story 1.6 AC5? | `…/Components/Pages/ManualReviewDashboard.razor`, `ReviewCaseDetail.razor` | FR14 verdict |
| HRQ-7 | FR30 runtime RBAC: are non-Reviewer/Admin roles correctly denied? | review pages `@attribute [Authorize]` | FR30 verdict |
| HRQ-8 | Does `FusionExpedienteService` confidence output satisfy the PRP matcher/confidence-gating intent without `IFieldMatcher<T>`? | `…/Infrastructure.Classification/FusionExpedienteService.cs` | G-H4 |
| HRQ-9 | Is HTTP `X-Correlation-ID` header propagation required, or is in-process event-chain propagation enough (NFR11/INV-11)? | correlation-id usage | INV-11 verdict |
| HRQ-10 | Are NFR16 (Azure AD) and NFR17 (field-level PII encryption) binding for this release? If yes → both currently FAIL. | PRD Reasoning Path 9 | NFR16/17 verdicts |
| HRQ-11 | Is "proceed to export despite fusion conflict / low confidence" a design choice or a defect? | `…/ReconciliationOrchestrator.cs:243-279` | G-C2 policy |

---

## Progress log
- 2026-06-20 — tracker created from PHASE2-FINAL-REPORT.md. All tasks pending; owner-decision items (Section E + G-H4/G-H6/G-D4/G-C2) blocked pending rulings.
