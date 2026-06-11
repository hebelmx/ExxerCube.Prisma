# Gap Matrix — MVP Ground-Truth (2026-06-11)

**Phase 3–4 of the MVP Audit** (`MVP-AUDIT-PLAN-2026-06-11.md`) · **Branch:** Kt2
**Method:** 6 parallel subsystem traces + per-gap adversarial verification + completeness critic (21 agents, file:line evidence). Refreshes `GAP-MATRIX-2026-06-dual-ground-truth.md` (2026-06-07) and measures against the agreed MVP bar (`MVP-DEFINITION-2026-06.md`, checklist A–F).

> **How to read.** Status = current state vs the MVP acceptance criterion. **Done** (real + wired from a composition root + meets criterion) · **Partial** (real but unwired/single-source/stubbed-telemetry/half-scope) · **Planned** (stub/placeholder/missing on critical path) · **Out-of-MVP** (deferred by owner decision; not counted as a gap). Every verdict is adversarially verified — agents tried to *refute* each gap before recording it. `Δ` = change vs 2026-06-07 matrix.

---

## Headline

Against the **MVP bar**, the system splits cleanly:

- **Already there (keep green):** the whole core *processing* surface — Quality, OCR, Classification, SIRO-XML/Excel export — **and**, newly confirmed, a **substantially complete manual-review dashboard** (list+filters, field annotations, override/notes, reviewer-identity audit) and a **real SLA dashboard** (reads DB directly).
- **The MVP-blocking work is concentrated in one place:** the **ingestion + 3-process security spine (A1–A6)**. Plus a short tail of pipeline/cleanup partials (B1, B2, C3-persistence, D2-telemetry, E1, E2) and the verification gate (F).

The "small but disqualifying" picture is exactly right: ~6 blocking items in section A, then a handful of S/M finishing tasks.

---

## A. Ingestion + 3-process split + security  — *the MVP-blocking cluster*

| # | Criterion | Status | Size | Evidence (file:line) | Notes / dependencies |
|---|---|---|---|---|---|
| **A1** | Real SIARA downloader wired into Orion `IDocumentDownloader` | **Planned** | M | `Orion.Worker/Program.cs:17` (Stub only); `StubDocumentDownloader.cs:26-30` (returns `Array.Empty<byte>()`); `SiaraNavigationTarget.cs:59-80` (real scraper, **never referenced in prod**) | Scraper exists but disconnected. Only 1 `IDocumentDownloader` impl in tree (the stub). **Decision pending:** SIARA auth (session-passthrough vs one-time login). |
| **A2** | `IngestionOrchestrator.StartAsync()` poll/watch loop | **Planned** | L | `IngestionOrchestrator.cs:268-273` (`Task.CompletedTask`, comment "implement SIARA polling…"); runs in prod via `OrionWorkerService.cs:31-35` | No-op runs in production. Per-doc `IngestDocumentAsync` ROP + SHA-256 journal are real but never driven. **Root defect shared with A1 + E1.** |
| **A3** | `StubExxerHub` → real Ember transport **in workers** | **Planned** | M | `Orion.Worker/Program.cs:19-20` (stub, "replace in production"); `Athena.Worker` wires **no** `IExxerHub`; UI is the only real hub (`Web.UI/Program.cs:166-169`) | Δ **downgraded Partial→Planned** (critic): criterion scopes to *workers*, where it's 0%. Real Ember exists in UI only. Backbone for A4. |
| **A4** | 3 separately-hosted Ember processes (Downloader/Extractor/Reconciliator) | **Partial** | L | Only 2 monolithic workers: `Orion.Worker` + `Athena.Worker`; `ProcessingOrchestrator.cs:19,88` runs Quality→OCR→Fusion→Classification→Export in **one** process | **Lynchpin.** Athena must split into Extractor (quality→OCR→fusion) + Reconciliator (classification→export). Depends on A3. |
| **A5** | Per-stage authorization + data minimization | **Planned** | L | Workers register **zero** auth (`Athena.Worker/Program.cs`, `Orion.Worker/Program.cs`); `EfCoreIdentityAdapter.cs:11` real but **instantiated in no DI root**; orchestration threads no identity | **Blocked-by-A4** (no inter-process boundary to minimize across yet). UI page-level `[Authorize(Roles=…)]` exists but is not pipeline authz. |
| **A6** | Audit trail of who/which-process accessed which doc | **Partial** | M | `IAuditLogger` real + queued persistence; audit calls only in Web.UI services (`DecisionLogicService`, `DocumentIngestionService`…); **no audit in Orion/Athena workers**; `LogAuditAsync` has `userId` but **no process-identity** param | **Blocked-by-A4.** Needs process identity + worker-side audit calls. |

---

## B. Pipeline completeness

| # | Criterion | Status | Size | Evidence | Notes |
|---|---|---|---|---|---|
| **B1** | Multi-source fusion (XML+DOCX into worker path) | **Partial** | M | `ProcessingOrchestrator.cs:535-542` & `:819-823` — `FuseAsync(null, pdf, null, …)`, XML/DOCX hardcoded null + empty metadata. UI does 2-way XML+PDF via **fixtures** (`DocumentComparisonCoordinator.cs:88-95`); DOCX universally null | Fusion engine is genuinely multi-source; the *worker wiring* feeds one source. |
| **B2** | Native (searchable) PDF text extraction | **Planned** | S | `PdfMetadataExtractor.cs:203-217` returns `string.Empty` unconditionally ("use iTextSharp or PdfSharp"); `PdfSharp`/`PdfPig` declared in `Directory.Packages.props` but **unused** for text | OCR fallback works for scanned; searchable-PDF text path absent. Small, isolated. |
| **B3** | Quality / OCR / Classification / SIRO-XML / Excel (keep green) | **Done** | — | Worker registrations `Athena.Worker/Program.cs:23,24,26,27`; analyzers/executors real | 5 of 6 B3 slices Done + wired. |
| B3-pdf | *Signed* PDF export | **Out-of-MVP** | — | `AdaptiveResponseExporterAdapter.cs:90-100` returns "implemented in Story 1.8"; real `DigitalPdfSigner` exists but is **shadowed** by last-wins DI (`Web.UI/Program.cs:294-295`) | Owner decision (2026-06-11): signing = **P2/bank-side**. Recorded as deferred, **not a gap**. (Note the DI-shadowing bug for whenever it's revived.) |

---

## C. Manual-review dashboard — *mostly Done (Δ new positive)*

| # | Criterion | Status | Size | Evidence | Notes |
|---|---|---|---|---|---|
| **C1** | Flagged cases listed + filters | **Done** | — | `ManualReviewDashboard.razor` (route `/manual-review`, quick-stats, full filter UI, MudBlazor table); `ManualReviewerService.cs:29-117` (DB-backed filters) | Only bulk-action buttons are TODO stubs; individual review workflow complete. |
| **C2** | Field-level annotations (source/confidence/conflicts) | **Partial** | M | `ReviewCaseDetail.razor:94-244` (annotation table + source-comparison grid real); but `ManualReviewerService.cs:325-343` hydrates only ConfidenceLevel — multi-source per-field values are placeholder | UI is built; the multi-source field *values* need the unified record (ties to C3). |
| **C3** | Override/correct/notes **persisted to unified record** | **Partial** | M | Decision form + persistence real (`ReviewCaseDetail.razor:247-280`, `ManualReviewerService.cs:120-280`); **but** `DecisionLogicService.cs:760` "we *would* update the unified metadata record here… for now the decision is saved" | Δ **trace said Done; critic corrected to Partial** — the "persisted to unified record" clause is literally unmet (saved to review table, not merged). |
| **C4** | Review actions audited with reviewer identity | **Done** | — | `DecisionLogicService.cs:709-756` logs `AuditActionType.Review` with `ReviewerId` at cancel/fail/success; queued persistence | Complete. |

---

## D. SLA tracking

| # | Criterion | Status | Size | Evidence | Notes |
|---|---|---|---|---|---|
| **D1** | Deadline calc from intake + días plazo (business days) | **Done** | — | `SLAEnforcerService.cs:66-67,453-469` (`AddBusinessDays` skips weekends); wired via `AddDatabaseServices` (`Web.UI/Program.cs:245`); tested (`SLAEnforcerServiceTests.cs:77-91`) | Δ **refuted Partial→Done**: weekend-only is the criterion's permitted "explicitly simplified". **Action: document the simplification** (Mexican holidays = P1). |
| **D2** | At-risk surfaced + telemetry returns real values | **Partial** | S | Dashboard real, reads DB directly (`SlaDashboard.razor:456-472`, `SLAEnforcerService.cs:259-262`); **but** `AddMeter` has **0 matches** repo-wide → SLA meter never exported; `SLAMetricsCollector.cs:246-251` `GetCurrentGaugeValue` returns hardcoded `0` → gauges accumulate wrong | User-facing at-risk flagging works (DB-direct). The **telemetry/metrics-export** half is the gap. Small fix. |

---

## E. Ops / readiness / config + Sentinel

| # | Criterion | Status | Size | Evidence | Notes |
|---|---|---|---|---|---|
| **E1** | Real readiness probe per process | **Partial** | S | `Athena/Orion HealthCheckService.cs:55-57` — `isReady = _orchestrator != null` + `// TODO: orchestrator.IsStarted`; no `IsStarted` exists | Orion's truthful readiness is **blocked-by-A2** (StartAsync is a no-op). |
| **E2** | Config externalized + template pages removed | **Partial** | S | `appsettings.json:3-4` hardcodes `Server=DESKTOP-FB2ES22\SQL2022`; `Counter.razor`/`Weather.razor` orphaned in `Components/Pages` | Quick cleanup; also a **demo-blocker**. No env-var/user-secrets/Production override layer. |
| **E3** | Health via `/health` per process | **Done** | — | `/health(/live|/ready)` wired in all 3 roots (`Athena.Worker:64-84`, `Orion.Worker:40-60`, `Web.UI:114`) | `IServiceHealth<T>` not used (`/health` is the accepted alternative). Hollow until E1 readiness is real. |
| **Sentinel** | Monitoring hosted | **Planned (P1)** | M | Real lib (`SentinelService.cs`, `HeartbeatMonitor.cs`) but **no Worker host**, wired in **no** root; **no `IProcessRestarter` impl** | Not MVP-blocking; deploy as host in P1 (or rely on Ember `IServiceHealth`). |

---

## F. Verification gate — *(Δ critic-added; was unscored)*

| # | Criterion | Status | Size | Notes |
|---|---|---|---|---|
| **F1** | One real E2E run, **no stub on critical path** | **Planned (blocked)** | M | Transitively gated: A1 (stub downloader), A2 (no-op watcher), A4 (no split), B1 (single-source) each place a stub on the path. Cannot pass until those close. The criterion that proves A–E integrate. |
| **F2** | Suite green + new integration tests + flaky OCR stabilized | **Partial** | M | `System.Ocr.Pipeline` flaky (the exact test F2 names); **no** ingestion-chain / multi-source-fusion / review-dashboard *integration* tests (current tests are NSubstitute mocks). |

---

## Dependency spine (drives the path ordering)

```
A1 (downloader) ─┐
                 ├─► A2 (watch loop) ─► E1-Orion (readiness truthful)
                 │      └─ shares root defect IngestionOrchestrator.cs:268-273 with A1, E1
A3 (Ember in workers) ─► A4 (3-process split) ─┬─► A5 (per-stage authz + data-min)
                                               └─► A6 (per-process audit identity)
B1 (multi-source) ── independent of A ──┐
B2 (native PDF)   ── independent of A ──┴─ both touch pre-fusion extraction wiring
C2/C3 (unified-record persistence) ── independent
D2 (SLA telemetry) ── independent ── E2 (config cleanup) ── independent
ALL ─► F1 (E2E gate) ◄─ F2 (integration tests + de-flake)
```

**Lynchpins:** A4 (everything security depends on it) and the `IngestionOrchestrator` placeholder (A1/A2/E1 share it). A5/A6 are **not** parallelizable with A4 — they're downstream.

---

## Diff vs 2026-06-07 matrix

| Area | Then (06-07) | Now (06-11) |
|---|---|---|
| Manual review | Not deeply assessed | **C1/C4 Done, C2/C3 Partial** — dashboard largely built (new positive) |
| SLA | Listed `return 0` telemetry stub | **D1 Done; D2 Partial** with precise telemetry blockers (meter never `AddMeter`'d, gauge hardcoded 0) |
| Signed PDF | Not flagged | **DI-shadowing bug** found (`AdaptiveResponseExporterAdapter` shadows real `DigitalPdfSigner`) — but **Out-of-MVP** now |
| A3 Ember | "Planned/Partial" | **Planned** (workers 0%; UI-only real) |
| A5/A6 security | Auth = "Med" priority, real-but-unwired | **MVP-required** (owner: 3-process security split IN) — elevated + framed as blocked-by-A4 |
| A1/A2/A4/B1/B2 | Planned/Partial | **Unchanged** — consistent, re-verified with fresh evidence |
| Verification gate | — | **F1/F2 added** (the missing acceptance gate) |
