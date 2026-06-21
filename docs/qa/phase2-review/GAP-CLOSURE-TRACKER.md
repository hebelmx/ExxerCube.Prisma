# Phase-2 Gap-Closure Tracker

> **▶ Orchestration brief (start here):** `docs/qa/phase2-review/REMEDIATION-ORCHESTRATION-KICKOFF.md` —
> the master plan that groups these tasks into EPIC-0…8, the BMAD agent/skill mapping, sequencing,
> owner rulings, and env gotchas. Launch with `/bmad-orchestrator docs/qa/phase2-review/REMEDIATION-ORCHESTRATION-KICKOFF.md`.
> **This tracker is the task ledger** the orchestrator updates as it executes.

**Source of truth for findings:** `docs/qa/phase2-review/PHASE2-FINAL-REPORT.md` (+ R1–R5 + VERIFICATION-NOTES).
**▶ Re-issued deployment recommendation (2026-06-20):** `DEPLOYMENT-RECOMMENDATION-ADDENDUM-2026-06-20.md` — **CONDITIONALLY READY FOR STAGING**; all code blockers closed + verified, one live-runtime verification pass remains.
**Branch:** `Liv`. **Created:** 2026-06-20. **Status legend:** ☐ pending · ◐ in-progress · ☑ done · ⛔ blocked (owner decision).
**For the executing agent:** each task is self-contained — file path, what to change, Definition of Done (DoD) with the *evidence to produce*. Verify from ground truth (build the touched project + a test); do NOT mark ☑ without the evidence. Build single projects (E: drive slow). xUnit v3 / MTP: `dotnet test <proj>` (NO `--nologo`), filter `--filter-query "/Asm/Ns/Class/Method"`, capture `--report-trx`. Result<T> + CancellationToken conventions apply to all new code.

> **Sequencing rule:** resolve the ⛔ owner-decision items (Section E) FIRST where a code task depends on them (noted per task). Then do Section A (Critical) → B (High) → C (Medium/Low) → D (coverage/NOT-TESTED). Re-run the capstone E2E + harness integration after A/B to prove closure.

---

## Section A — CRITICAL (block staging)

### G-C1 ☑ — Enforce non-notification (FR31 / INV-5)
> **CLOSED 2026-06-20** (commit fb95b1fb). HRQ-12 = (A): regression guard only. `FR31NonNotificationRegressionGuardTests` scans 21 production assemblies; FAILS if any external-notification SDK is referenced outside an explicit allow-list. Re-grep confirmed zero real sinks. Guard 1/1; build 0/0. TRX: closure-evidence/G-C1.trx.
- **Finding:** No code gates client notification on legal-directive permission; `ProcessingHub`/SLA escalation broadcast unconditionally. (Report CRIT-1; R2; R4 INV-5.)
- **Where:** SignalR/notification emit paths — `04 Services/.../ProcessingHub*`, SLA escalation publisher, any `Notify*`/`Broadcast*`. (Grep `Notify`, `Broadcast`, `Publish.*Escalation`.)
- **Do:** Introduce a non-notification guard: before any *client-facing* notification, check the case's legal-directive `AllowsClientNotification` (add to the directive/compliance-action model if absent; default DENY). Block + audit-log when not allowed.
- **DoD:** new unit test proving a directive that forbids notification ⇒ no client broadcast emitted (NSubstitute on the hub/notifier) AND an audit row records the suppression; build 0/0; `--report-trx` captured. Evidence path: `docs/qa/phase2-review/closure-evidence/G-C1.trx`.
- **Depends on:** **HRQ-12** (owner 2026-06-20 leans "no external-notification path exists ⇒ FR31 satisfied by absence"). **If HRQ-12 = (A):** scope shrinks to a *regression guard* — a test that FAILS if any external-notification sink is added without a legal gate (no full gate needed now). **If HRQ-12 = (B):** implement the full gate as described. Verify first: grep notification sinks + confirm SignalR audiences are operators-only.

### G-C2 ☑ — Gate Stage-5 export on review/confidence (FR14 / FR20 / INV-6)
> **CLOSED 2026-06-20** (commit 8db5e781; real-path hardening 692e4608). `ExportGatePolicy` (configurable, all gates default ON) + `ExportHeldForReviewEvent`; `ReconciliationOrchestrator.EvaluateExportGate` blocks Stage 5 when confidence<70 OR fusion=ManualReviewRequired OR unresolved conflicts. **G-C2c CLOSED (692e4608):** adversarial review found the gate was bypassed in the real 3-process path (handoff discarded fusion decision; FusionResult.NextAction defaults to ManualReviewRequired); fixed by carrying RequiresManualReview across the ExtractionCompletedEvent + reconstructing a faithful FusionResult in ReconciliationPipelineService. Athena.Processing 112/112 (incl. real-pipeline ProcessAsync test); TRX G-C2.trx + G-C2c.trx. **Remaining follow-on:** G-C2b (review-approval re-trigger — release-after-approval path; ExportHeldForReviewEvent has no subscriber yet).
- **Finding:** Low-confidence classification publishes `DocumentFlaggedForReviewEvent` then `return result;` and **proceeds to export**. Fusion `Revisión manual requerida` also does not block. Unreviewed/low-confidence records produce regulatory output. (Report CRIT-2 + EX-06/EX-07; live `e2e-rerun.log`.)
- **Where:** `04 Services/Athena/Prisma.Athena.Processing/ReconciliationOrchestrator.cs:243-279` (low-confidence branch) and `ExecuteStage5ExportAsync` (line ~286). Threshold const `ClassificationConfidenceThreshold = 70`.
- **Do:** When `RequiresManualReview` is true (confidence < threshold) OR fusion `NextAction == "Revisión manual requerida"`/has unresolved conflicts, **do NOT run Stage 5**; route the case to the manual-review queue and emit a "held for review" state instead of `ExportCompletedEvent`. Only export after a review decision marks it approved. Make the policy explicit + configurable.
- **DoD:** test: a sub-threshold/conflicted case ⇒ no `ExportCompletedEvent`, a ReviewCase persisted, case status = held; an approved case ⇒ export proceeds. Build 0/0; TRX at `closure-evidence/G-C2.trx`.
- **Depends on:** ✔ **HRQ-11 RESOLVED** (owner 2026-06-20: *export must block; release only after human review*) → implement the blocking gate; no longer downgradeable. **This is the #1 remaining staging blocker.**

---

## Section B — HIGH

### G-H1 ☑ — Excel/DatosCargaOficio export never completes (FR18)
> **CLOSED 2026-06-20** (commit d0211f46). Root cause: `DatosCargaOficioLayoutGenerator` wrapped synchronous `ClosedXML.SaveAs` in `Task.Run`, which starved under E2E thread-pool saturation (SIRO's async-I/O leg didn't). Fix: inline `SaveAs`. Tests 210/210 (TC-15 thread-capture red→green) + Athena 111/111. TRX: closure-evidence/G-H1.trx.
- **Finding:** Stage 5 logs `Starting DatosCargaOficio layout generation` then no completion + no `ExportCompletedEvent(DatosCargaOficioXlsx)` within 60 s; SIRO XML leg on the same run succeeded. (Report HIGH-1; 3 reviewers; `e2e-rerun.log:426`.)
- **Where:** `DatosCargaOficioLayoutGenerator` + `AdaptiveExporter` + the Stage-5 export wiring in the Reconciliator (`AddDatosCargaOficioExportServices`).
- **Do:** Reproduce in isolation (unit/integration test invoking the layout generator on the live-run expediente `EXP-9626-2021` shape). Find why it hangs / never emits (likely: ClosedXML exception swallowed, await never completing, or event not published). Fix so it completes and emits the event, or fails loudly with an audited error.
- **DoD:** the capstone E2E `MaxFidelityGateFullPipelineE2ETests` passes (both export events arrive); OR a dedicated test proves the generator emits the xlsx event. TRX at `closure-evidence/G-H1.trx`.

### G-H2 ◐ — `/sla-dashboard` served to anonymous users (security)
> **AUTH ADDED 2026-06-20** (commit 749f287e, via G-I1): `@attribute [Authorize]` now on SlaDashboard.razor; Web.UI 0/0. Live anon-probe evidence (=>302/401) pending app-running-against-SQL2025 → folded into EPIC-0/G-D2.
- **Finding:** `GET /sla-dashboard` → HTTP 200 anonymous, renders real SLA dashboard (66 KB, deadline/escalation columns). No `[Authorize]`. (Report HIGH-2; live probe V2.)
- **Where:** `07 UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages/SlaDashboard.razor`.
- **Do:** Add `@attribute [Authorize(Roles = "Reviewer,Admin")]` (match the policy used on protected pages). Re-verify anon ⇒ redirect/deny.
- **DoD:** live probe `curl -o /dev/null -w '%{http_code}' /sla-dashboard` as anon ⇒ 302/401 (not 200); a `Tests.UI` test asserts the redirect. Evidence: `closure-evidence/G-H2-probe.txt`.

### G-H3 ◐ — `/dashboard` served to anonymous users
> **AUTH ADDED 2026-06-20** (commit 749f287e, via G-I1): `@attribute [Authorize]` now on Dashboard.razor; Web.UI 0/0. Live anon-probe evidence pending runtime → folded into EPIC-0/G-D2.
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

### G-H5 ☑ — Cross-document identity dedup non-functional (FR10 / INV-2)
> **CLOSED 2026-06-20** (commit 7a3703d0). New DB-backed `DbPersonIdentityResolverService` (in Infrastructure.Database) implements `IPersonIdentityResolver`: RFC + variant-set lookup, find-or-create one Persona, unique-index concurrency guard; registered (single, no shadowing) in `AddDatabaseServices`. Contract updated off the null-stub. Tests.System.Storage 54/54 (Testcontainers SQL) incl. two-RFC-variants⇒one-Persona. TRX: G-H5.trx. **Scope (narrowed per final-review RV-4):** the resolver + its consumer `DecisionLogicService` serve the **Web.UI / manual-review** path. The Athena/Reconciliator pipeline workers do **not** create Persona records / resolve identity during processing, so they correctly do not register it (verified — not a gap).
- **Finding:** `PersonIdentityResolverService.FindByRfcAsync` returns `null` (its own behavioural contract `PersonIdentityResolverContract` asserts `ReturnsSuccessWithNullValue`); no DB persistence; not registered in Athena/Orion worker pipeline hosts. (Report HIGH-5; R4 INV-2.)
- **Where:** `02 Infrastructure/Infrastructure.Classification/PersonIdentityResolverService.cs:166` + worker DI composition roots.
- **Do:** Implement DB-backed RFC/alias lookup + dedup persistence (Persona table); register the service in the worker pipeline that creates Persona records; update the behavioural contract.
- **DoD:** integration test (Testcontainers SQL): two documents with RFC variants for the same person ⇒ one deduplicated Persona row. TRX at `closure-evidence/G-H5.trx`.
- **Depends on:** FR24/FR26 persistence (already PASS).

### G-H6 ☑(decision) — Storage stays vendor-agnostic; Azure Blob NOT a release blocker (CR8)
- **Finding:** No `BlobServiceClient`/`BlobContainerClient`; only local FS adapter. CR8 (as written) requires both. (Report HIGH-6; R3.)
- **✔ Owner decision (2026-06-20, HRQ-13 = A):** Azure Blob is **over-specification**. Keep storage **vendor-agnostic** behind `IDownloadStorage`; local FS adapter now; cloud adapters added **per deployment**. **CR8 reclassified FAIL → met via vendor-agnostic abstraction.** No release-blocking work.
- **Remaining (optional, low priority):** ensure `IDownloadStorage` is genuinely provider-agnostic (no FS-specific leakage in the contract); document the adapter extension point. Update the PRD/PRP wording for CR8 to "vendor-agnostic storage interface."
- **DoD:** PRD/PRP CR8 wording updated; a one-paragraph design note on the storage extension point. No Azure SDK dependency required.

---

## Section C — MEDIUM / LOW (from R5 exploratory)

### G-M1 ☑ / G-M3 ☑ / G-M4 ☑ — health endpoints + login scaffolding (commit a2447225)
> **CLOSED 2026-06-20.** G-M1: `/health/live` mapped (Predicate=>false, AllowAnonymous) ⇒ 200 no-DB; `/health/ready` keeps DB gate. G-M4: `HealthCheckResponseWriter` emits per-check JSON. G-M3: removed ASP.NET external-auth scaffolding from Login.razor + ExternalLoginPicker. Web.UI 0/0; auth middleware preserved. Live-probe evidence ⇒ EPIC-0/G-D2. (Original G-M1 entry retained below.)

### G-M1 ☑ — `/health/live` returns 404 (NFR-ops)
- **Finding:** `/health/live` → 404 (liveness probe not registered); `/health` → 503, `/health/ready` → 503. (Report; live probe; R5 EX-04.)
- **Where:** Web UI `Program.cs` health-check endpoint mapping.
- **Do:** Register `/health/live` (liveness, no DB dependency) + keep `/health/ready` (readiness incl. DB). Liveness should be 200 when the process is up.
- **DoD:** anon `/health/live` ⇒ 200; `/health/ready` ⇒ 503 when DB down. Evidence: `closure-evidence/G-M1-probe.txt`.

### G-M2 ☑ — PDF page-index off-by-one (FR6)
> **CLOSED 2026-06-20** (commit 266e1e50). Replaced exception-as-sentinel `while(true)` in `PdfToImageConverter.ConvertToImagesAsync` with `GetPageCount` + bounded `for` (0..pageCount-1). Infrastructure.Extraction.Ocr 0/0; test project 164/164 (5 new). TRX in closure-evidence/G-M2.trx.
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

> Full, easy-to-answer versions of these questions live in **PHASE2-FINAL-REPORT.md §6** (decision worksheet with options + fill-in lines). This table is the index + current owner answers. ✔ = owner answered 2026-06-20; ⛔ = still open.

**ALL 13 answered by owner 2026-06-20.** ✔ = resolved/decided.

| ID | Decision | Owner answer (2026-06-20) | Unblocks / task |
|----|----------|---------------------------|-----------------|
| HRQ-1 ✔ | Audit "immutable"? | **SQL Server Append-Only Ledger Table** for tamper-evidence; Serilog → SEQ + SQL Server. Deploy/infra concern. INV-4 → met-by-design. | **G-S2** |
| HRQ-2 ✔ | NFR14 event-error handling? | **PASS** — strengthen with a dedicated background worker (outbox/retry: unprocessed event → persist + re-raise; periodic delayed task). | **G-S3** |
| HRQ-3 ✔ | NFR15 batch? | **PASS (A)** — ingestion is async; just pack the 1–3 docs per case. | — |
| HRQ-4 ✔ | CR3 UI consistency? | Implement **Identity** first, then decide. CR3 stays NHR. | **G-I1** → G-D2 |
| HRQ-5 ✔ | CR4 FK-drop? | **OK for now (dev, EF migrations).** Later: DDL trigger blocking DROP/ALTER on critical tables, or recreate DB with fresh migrations (nothing to preserve yet). | **G-S4** |
| HRQ-6 ✔ | FR14 manual-review UI? | After Identity, re-check. FR14 stays NHR. | **G-I1** → G-D2 |
| HRQ-7 ✔ | FR30 RBAC runtime? | After Identity, re-check. FR30 stays NHR. | **G-I1** → G-D2 |
| HRQ-8 ✔ | 7 interfaces needed? | Agree research — needs additional evidence; **draft an ADR per interface** (implement vs de-scope). | **G-H4** |
| HRQ-9 ✔ | Correlation-ID depth? | **PASS (A)** — in-process propagation is sufficient. INV-11 → PASS. | — |
| HRQ-10 ✔ | NFR16/17 binding? | NFR16: Azure AD **over-spec → de-scope** (Identity + Windows auth). NFR17: **defer** to a later release. | **G-I1**; NFR17 backlog |
| HRQ-11 ✔ | Block export on low-conf/conflict? | **Must BLOCK; release only after human review.** CRIT-2 confirmed. | **G-C2 (unblocked)** |
| HRQ-12 ✔ | External-notification path? | **None — grep-confirmed** (`closure-evidence/HRQ-12-notification-sinks.txt`): Identity email no-op, email infra is Veriqan's, slack=false-positive, SignalR=operators. FR31/INV-5 → PASS-by-absence; **CRIT-1 downgraded.** | **G-C1** (regression guard) |
| HRQ-13 ✔ | CR8 Azure binding? | **Vendor-agnostic `IDownloadStorage`; Azure over-spec.** Local FS now; Azure/AWS later; **storage MUST be encrypted (local + cloud), E2E.** | CR8 → met; **G-S1** (encryption) |

---

## Section F — Owner-added work items (2026-06-20 rulings)

**Staging-path priority:** **G-I1 (Identity) → G-C2 (export gate) → G-H1 (Excel) → G-H2/G-H3 (route auth).** The rest are accepted/hardening.

### G-I1 ◐ — Implement ASP.NET Identity as infrastructure  **(KEYSTONE)**
> **CODE + STATIC AUTH-ENFORCEMENT DONE 2026-06-20** (commits 749f287e + dcf77713, per ADR-014). New `02 Infrastructure/Infrastructure.Identity` adapter (DbContext, user, roles, adapter, seeder, migrator, AddPrismaIdentity, InitialIdentitySchema migration); Web.UI rewired (~40 files); [Authorize] on Dashboard+SlaDashboard. **Adversarial review then REFUTED runtime enforcement** — [Authorize] was inert because Routes.razor used bare RouteView (AuthorizeRouteView commented out) AND Program.cs lacked UseAuthentication/UseAuthorization. **Fixed (dcf77713):** AuthorizeRouteView+RedirectToLogin restored, both middleware added + ordered, explicit AddAuthorization. Infrastructure.Identity 0/0; Web.UI 0/0 (orchestrator-rebuilt twice); Tests.Infrastructure.Identity 7/7. **◐ because:** live SQL2025 unreachable ⇒ runtime boot/login/anon-redirect screenshots NOT yet captured (⇒ EPIC-0/G-D2). appsettings DefaultConnection still SQL2022 — reconcile to SQL2025 at runtime. Old unregistered Web.UI Data/ context kept as dead code (G-I1-cleanup).
- **Why:** unblocks CR3, FR14, FR30 (all NHR pending login), closes HIGH-2/3 (route auth), and satisfies NFR16 (Identity + Windows auth in place of Azure AD).
- **Do (owner direction):** define identity/auth interfaces in **Domain**; implement in an **Infrastructure adapter**; keep ALL Identity scaffolding inside the adapter project. Use **SQL Server `DESKTOP-FB2ES22\SQL2025` (Windows authentication)**. Seed a Reviewer + an Admin user for testing.
- **DoD:** login works against live SQL; anonymous hitting `/sla-dashboard`, `/dashboard`, `/manual-review`, `/audit`, `/export` ⇒ redirect/deny; a Reviewer session opens the protected screens. Evidence: authenticated screenshots in `closure-evidence/ui/`.

### G-S1 ☑ — Storage encryption (local + cloud), E2E  [HRQ-13 · NFR8/NFR17-adjacent]
> **CLOSED 2026-06-20** (commit 4367ad26). Owner key-mgmt ruling = AES-256 + config/env key + protect purpose strings. `IStorageEncryptor` seam + AES-256-GCM; master key from config/env (fail-fast), per-purpose HKDF subkeys, purpose string never persisted (HKDF info only). FS adapter encrypts on write / decrypts on read. Tests 53/53 incl. raw-on-disk-ciphertext + plaintext-purpose-absent. Key rotation = documented follow-up. TRX: G-S1.trx.
- **Owner ruling:** storage stays vendor-agnostic behind `IDownloadStorage`; **encrypt at rest — the local FS adapter must be encrypted too**, not just future cloud adapters.
- **DoD:** stored document bytes are ciphertext at rest; a test reads the on-disk file and asserts it is not plaintext; key handling documented.

### G-S2 ☑ / G-S4 ☑ — Audit ledger + schema-protection DDL trigger + Serilog sinks
> **CLOSED 2026-06-20** (commit f2e257d0, ADR-022). Migration 20260621100000 converts AuditRecords to APPEND_ONLY ledger + installs TR_ProtectCriticalSchema (ON DATABASE DROP/ALTER over auditrecords/reviewcases/reviewdecisions/outboxevents/slastatus, PrismaTest_% bypass). Serilog.Sinks.MSSqlServer added to all 4 hosts + appsettings (SEQ already present). Tests 7/7 (Testcontainers SQL2022) — ledger UPDATE/DELETE blocked (37359), DDL trigger blocks DROP on protected. NOT applied to live SQL (owner-gated). TRX: G-S2-S4.trx.

### G-S2 ☐ — Audit tamper-evidence via SQL Ledger  [HRQ-1 · INV-4/FR17]
- **Owner ruling:** use **SQL Server Append-Only Ledger Table** for audit rows; Serilog → SEQ + SQL Server sinks. Deploy/infra.
- **DoD:** audit table is a ledger (append-only/verifiable); a delete/update attempt is blocked or ledger-detectable; Serilog SEQ+SQL sinks configured.

### G-S3 ☑ — Event-processing reliability worker (outbox/retry)  [HRQ-2 · NFR14 enhancement]
> **CLOSED 2026-06-20** (commit 79799c61). New OutboxEvents table; EventPersistenceWorker stamps processed/unprocessed; OutboxRetryWorker re-raises unprocessed events on an interval with a retry cap then dead-letters. Tests.System.Storage 62/62 (Testcontainers). TRX: G-S3.trx.
- **Owner ruling:** NFR14 PASS; add a dedicated background worker — if an event wasn't processed → persist + re-raise; periodic delayed task.
- **DoD:** a simulated dropped/failed event is detected, persisted, and re-raised; test proves recovery.

### G-S4 ☐ — Production schema-protection  [HRQ-5 · CR4 hardening]
- **Owner ruling:** dev EF migrations fine now; for prod add a **DDL trigger** blocking DROP/ALTER on critical tables (audit, review, …), or adopt a migration-squash/recreate policy.
- **DoD:** a DDL trigger blocks a DROP on a protected table in a test DB (or a documented migration policy).

### G-H4 ☑ (updated) — Research + ADR per absent PRP interface  [HRQ-8]
- **Owner ruling:** per interface, gather additional evidence and **draft an ADR** recording: fulfilled elsewhere (e.g. fusion) → de-scope, or genuinely needed → implement. Start with `IFieldMatcher<T>`.
- **DoD:** one ADR per interface (7) under `docs/architecture/adr/`, each with a decision.
- **CLOSED 2026-06-20** (commit 8ea15725). ADR-015..021 — **all 7 ruled DE-SCOPE**, each capability fulfilled by an existing differently-named component (IFieldMatcher/IFieldAgreement→fusion services; IRuleScorer→FileClassifierService; IScanDetector/IScanCleaner→PdfMetadataExtractor+IImagePreprocessor; IReportGenerator→AuditReportingService; IUIBundle→Blazor). No IMPLEMENT-large items. **Follow-up (minor):** amend PRP.md feature→interface mapping; optionally extract `IAuditReportingService`.

---

## Progress log
- 2026-06-20 — tracker created from PHASE2-FINAL-REPORT.md. All tasks pending; owner-decision items blocked pending rulings.
- 2026-06-20 (orchestration final gate) — ran a final completeness/adversarial review (plan-completion-reviewer) over the whole remediation. Confirmed the "tested-but-unwired" mode does NOT recur (G-C2b/G-I1 already fixed); found 4 residual findings, all **closed + verified** (commit d3677838, full solution 0/0, Orion 57/57): **RV-1 [Major]** Orion download path wrote plaintext (bypassed G-S1) → now encrypted; **RV-2** ProcessingHub had no auth → [Authorize]+RequireAuthorization; **RV-3** Serilog %VAR% tokens don't bind → env-var pre-injection across 4 hosts; **RV-4** G-H5 dedup claim narrowed to Web.UI/manual-review (pipeline workers correctly omit it). **Re-issued deployment recommendation = CONDITIONALLY READY FOR STAGING** (`DEPLOYMENT-RECOMMENDATION-ADDENDUM-2026-06-20.md`): all code blockers closed+verified; one live-runtime pass (auth UI walkthrough + capstone E2E both export events + anon-probes + migration apply on SQL2025) remains — gated only by live-box access the headless agents lacked.
- 2026-06-20 (orchestration wave 5) — closed **G-S2+G-S4** (audit append-only ledger + DDL schema-protection trigger + Serilog MSSqlServer sinks, commit f2e257d0, ADR-022; 7/7 Testcontainers). **All EPIC-4/5 hardening + EPIC-1/2/3/6/7 code now complete and ground-truth-verified.** Remaining: runtime-gated (EPIC-0/G-D1 corpus+sim, G-D2 auth UI walkthrough, capstone E2E, perf — need live SQL2025+Docker+Playwright box) + small follow-ons (G-C2b, G-H1b, G-I1-cleanup) + final adversarial re-verification & deployment recommendation re-issue (task 16).
- 2026-06-20 (orchestration waves 3-4) — closed **G-H5** (DB dedup, 7a3703d0), **G-M1/M3/M4** (health+login, a2447225), **G-S1** (AES-256-GCM storage encryption, 4367ad26), **G-S3** (outbox/retry worker, 79799c61). All ground-truth-verified (Web.UI/Infra builds 0/0; FileStorage 53/53; Tests.System.Storage 62/62 Testcontainers). Owner key-mgmt ruling captured (AES-256 + protected purpose strings). Remaining code: G-S2+G-S4 (audit ledger + DDL trigger, combined — shared migration surface). Remaining runtime-gated: EPIC-0/G-D1, G-D2, capstone E2E, perf (need the live SQL2025+Docker+Playwright box).
- 2026-06-20 (orchestration adversarial gate) — fanned out 3 skeptics against waves 1+2. Caught TWO real holes, both FIXED + ground-truth-verified: **G-C2c** (3-process path discarded fusion ManualReviewRequired → conflicted cases exported; commit 692e4608, 112/112) and **G-I1 auth was inert** ([Authorize] cosmetic — RouteView not AuthorizeRouteView + no UseAuthentication/UseAuthorization; commit dcf77713, Web.UI 0/0). G-H4: 4 de-scopes sound, 3 carry ITDD interface-debt (task 21, non-blocking). New follow-ons: G-H1b (Blazor-circuit SaveAs), G-H4-debt. Remaining staging items: runtime auth proof (EPIC-0/G-D2), G-C2b release-after-approval.
- 2026-06-20 (orchestration wave 2) — **G-I1 Identity keystone CODE DONE** (749f287e; Web.UI 0/0 rebuilt, Tests 7/7) which also added [Authorize] to both dashboards (**G-H2/G-H3 auth core ◐**, live probe → EPIC-0). **G-H1 Excel** closed (d0211f46; Task.Run-over-sync-SaveAs starvation). **G-C1 non-notif guard** closed (fb95b1fb). New follow-ons: G-I1-cleanup (old Data/ dead code). Live-SQL-dependent verification deferred to EPIC-0/G-D2.
- 2026-06-20 (orchestration wave 1) — **closed G-C2** (export gate, commit 8db5e781), **G-M2** (PDF off-by-one, 266e1e50), **G-H4** (7 de-scope ADRs, 8ea15725); landed **ADR-014** Identity-as-infra design. Pushed to Liv (HEAD 266e1e50). New follow-on items: G-C2b (review-approval re-trigger), G-C2c (3-process fusion-state propagation). Next: G-I1 Identity implementation (keystone) per ADR-014.
- 2026-06-20 (later) — owner answered **all 13** HR items (see report §1a + §6). Resolved/accepted: CRIT-1 (non-notification satisfied by absence — grep evidence in closure-evidence/), CR8 (vendor-agnostic), INV-4 (SQL ledger, G-S2), NFR14 (PASS + G-S3), NFR15/INV-11 (PASS), NFR16 (de-scoped), NFR17 (deferred), CR4 (dev-OK + G-S4). New owner work items: **G-I1 Identity (keystone), G-S1 storage encryption, G-S2 audit ledger, G-S3 outbox worker, G-S4 DDL trigger, G-H4 ADR-per-interface.** **G-C2 unblocked (owner: export must block).** Remaining staging blockers: G-C2, G-H1, G-H2/G-H3 (via G-I1).
