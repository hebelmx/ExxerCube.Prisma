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
- **Depends on:** **HRQ-12** (owner 2026-06-20 leans "no external-notification path exists ⇒ FR31 satisfied by absence"). **If HRQ-12 = (A):** scope shrinks to a *regression guard* — a test that FAILS if any external-notification sink is added without a legal gate (no full gate needed now). **If HRQ-12 = (B):** implement the full gate as described. Verify first: grep notification sinks + confirm SignalR audiences are operators-only.

### G-C2 ☐ — Gate Stage-5 export on review/confidence (FR14 / FR20 / INV-6)
- **Finding:** Low-confidence classification publishes `DocumentFlaggedForReviewEvent` then `return result;` and **proceeds to export**. Fusion `Revisión manual requerida` also does not block. Unreviewed/low-confidence records produce regulatory output. (Report CRIT-2 + EX-06/EX-07; live `e2e-rerun.log`.)
- **Where:** `04 Services/Athena/Prisma.Athena.Processing/ReconciliationOrchestrator.cs:243-279` (low-confidence branch) and `ExecuteStage5ExportAsync` (line ~286). Threshold const `ClassificationConfidenceThreshold = 70`.
- **Do:** When `RequiresManualReview` is true (confidence < threshold) OR fusion `NextAction == "Revisión manual requerida"`/has unresolved conflicts, **do NOT run Stage 5**; route the case to the manual-review queue and emit a "held for review" state instead of `ExportCompletedEvent`. Only export after a review decision marks it approved. Make the policy explicit + configurable.
- **DoD:** test: a sub-threshold/conflicted case ⇒ no `ExportCompletedEvent`, a ReviewCase persisted, case status = held; an approved case ⇒ export proceeds. Build 0/0; TRX at `closure-evidence/G-C2.trx`.
- **Depends on:** ✔ **HRQ-11 RESOLVED** (owner 2026-06-20: *export must block; release only after human review*) → implement the blocking gate; no longer downgradeable. **This is the #1 remaining staging blocker.**

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

### G-H6 ☑(decision) — Storage stays vendor-agnostic; Azure Blob NOT a release blocker (CR8)
- **Finding:** No `BlobServiceClient`/`BlobContainerClient`; only local FS adapter. CR8 (as written) requires both. (Report HIGH-6; R3.)
- **✔ Owner decision (2026-06-20, HRQ-13 = A):** Azure Blob is **over-specification**. Keep storage **vendor-agnostic** behind `IDownloadStorage`; local FS adapter now; cloud adapters added **per deployment**. **CR8 reclassified FAIL → met via vendor-agnostic abstraction.** No release-blocking work.
- **Remaining (optional, low priority):** ensure `IDownloadStorage` is genuinely provider-agnostic (no FS-specific leakage in the contract); document the adapter extension point. Update the PRD/PRP wording for CR8 to "vendor-agnostic storage interface."
- **DoD:** PRD/PRP CR8 wording updated; a one-paragraph design note on the storage extension point. No Azure SDK dependency required.

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

### G-I1 ☐ — Implement ASP.NET Identity as infrastructure  **(KEYSTONE)**
- **Why:** unblocks CR3, FR14, FR30 (all NHR pending login), closes HIGH-2/3 (route auth), and satisfies NFR16 (Identity + Windows auth in place of Azure AD).
- **Do (owner direction):** define identity/auth interfaces in **Domain**; implement in an **Infrastructure adapter**; keep ALL Identity scaffolding inside the adapter project. Use **SQL Server `DESKTOP-FB2ES22\SQL2025` (Windows authentication)**. Seed a Reviewer + an Admin user for testing.
- **DoD:** login works against live SQL; anonymous hitting `/sla-dashboard`, `/dashboard`, `/manual-review`, `/audit`, `/export` ⇒ redirect/deny; a Reviewer session opens the protected screens. Evidence: authenticated screenshots in `closure-evidence/ui/`.

### G-S1 ☐ — Storage encryption (local + cloud), E2E  [HRQ-13 · NFR8/NFR17-adjacent]
- **Owner ruling:** storage stays vendor-agnostic behind `IDownloadStorage`; **encrypt at rest — the local FS adapter must be encrypted too**, not just future cloud adapters.
- **DoD:** stored document bytes are ciphertext at rest; a test reads the on-disk file and asserts it is not plaintext; key handling documented.

### G-S2 ☐ — Audit tamper-evidence via SQL Ledger  [HRQ-1 · INV-4/FR17]
- **Owner ruling:** use **SQL Server Append-Only Ledger Table** for audit rows; Serilog → SEQ + SQL Server sinks. Deploy/infra.
- **DoD:** audit table is a ledger (append-only/verifiable); a delete/update attempt is blocked or ledger-detectable; Serilog SEQ+SQL sinks configured.

### G-S3 ☐ — Event-processing reliability worker (outbox/retry)  [HRQ-2 · NFR14 enhancement]
- **Owner ruling:** NFR14 PASS; add a dedicated background worker — if an event wasn't processed → persist + re-raise; periodic delayed task.
- **DoD:** a simulated dropped/failed event is detected, persisted, and re-raised; test proves recovery.

### G-S4 ☐ — Production schema-protection  [HRQ-5 · CR4 hardening]
- **Owner ruling:** dev EF migrations fine now; for prod add a **DDL trigger** blocking DROP/ALTER on critical tables (audit, review, …), or adopt a migration-squash/recreate policy.
- **DoD:** a DDL trigger blocks a DROP on a protected table in a test DB (or a documented migration policy).

### G-H4 ☐ (updated) — Research + ADR per absent PRP interface  [HRQ-8]
- **Owner ruling:** per interface, gather additional evidence and **draft an ADR** recording: fulfilled elsewhere (e.g. fusion) → de-scope, or genuinely needed → implement. Start with `IFieldMatcher<T>`.
- **DoD:** one ADR per interface (7) under `docs/architecture/adr/`, each with a decision.

---

## Progress log
- 2026-06-20 — tracker created from PHASE2-FINAL-REPORT.md. All tasks pending; owner-decision items blocked pending rulings.
- 2026-06-20 (later) — owner answered **all 13** HR items (see report §1a + §6). Resolved/accepted: CRIT-1 (non-notification satisfied by absence — grep evidence in closure-evidence/), CR8 (vendor-agnostic), INV-4 (SQL ledger, G-S2), NFR14 (PASS + G-S3), NFR15/INV-11 (PASS), NFR16 (de-scoped), NFR17 (deferred), CR4 (dev-OK + G-S4). New owner work items: **G-I1 Identity (keystone), G-S1 storage encryption, G-S2 audit ledger, G-S3 outbox worker, G-S4 DDL trigger, G-H4 ADR-per-interface.** **G-C2 unblocked (owner: export must block).** Remaining staging blockers: G-C2, G-H1, G-H2/G-H3 (via G-I1).
