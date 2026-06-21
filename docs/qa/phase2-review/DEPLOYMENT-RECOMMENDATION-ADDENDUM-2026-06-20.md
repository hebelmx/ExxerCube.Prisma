# Phase-2 Remediation — Re-issued Deployment Recommendation (Addendum)

**Date:** 2026-06-20 · **Branch:** `Liv` (HEAD `d3677838`) · **Supersedes** the verdict in
`PHASE2-FINAL-REPORT.md §8/§9` (which was *NOT READY FOR STAGING*).
**Method:** BMAD orchestration of the kickoff brief
(`REMEDIATION-ORCHESTRATION-KICKOFF.md`); every item delegated to an isolated subagent and
**verified by the orchestrator from ground truth** (single-project + full-solution builds,
test runs, `git diff`), not from subagent prose. Two mid-execution adversarial-review gates
and one final completeness review were run; the gaps they found were fixed, not papered over.

---

## Recommendation: **CONDITIONALLY READY FOR STAGING**

The independent Phase-2 review's blockers (**2 Critical + 6 High**) plus the owner-added
hardening are **closed in code and verified at build + unit/integration level** (full
solution builds **0 errors / 0 warnings**; all touched test projects green). **Two latent
staging blockers and four residual findings that the adversarial gates uncovered were also
closed.**

**The one remaining gate is RUNTIME verification on the live box.** The headless
orchestration environment could **not** reach live SQL Server `DESKTOP-FB2ES22\SQL2025`, the
SIARA Docker simulator, or a running Web.UI for Playwright. Therefore the live proofs
(authenticated UI walkthrough, capstone E2E with both export events, anon-probe HTTP codes,
migration apply) are **designed and code-ready but not yet executed**. **Staging sign-off
should follow a single live verification pass** (recipe below). Nothing in the code is
believed to block it; the condition is *evidence*, not *implementation*.

---

## What is CODE-COMPLETE + VERIFIED (build 0/0 + tests green)

| Item | What landed | Commit | Evidence |
|------|-------------|--------|----------|
| **G-C2 + G-C2c** export-block gate (Critical) | Stage-5 blocked on low-conf/fusion-conflict; **3-process real-path** hole (fusion `ManualReviewRequired` discarded at handoff) caught by adversarial review + fixed | 8db5e781, 692e4608 | Athena 117/117; `G-C2.trx`, `G-C2c.trx` |
| **G-C2b** release-after-approval | Held case exports after reviewer Approve; handler **registered + started**, event published on Approve, gate honors approval override | e7481462, 8d95c5fc | solution 0/0; `G-C2b*.trx` (Athena 117, App 576, DB 172, Domain 342) |
| **G-H1** Excel/DatosCargaOficio (High) | Root-caused `Task.Run`-over-sync-`SaveAs` thread-pool starvation; inline fix + thread-capture regression | d0211f46 | 210/210; `G-H1.trx` |
| **G-I1** Identity-as-infra keystone | Domain interfaces reused; `Infrastructure.Identity` adapter; seeder (Reviewer/Admin/Administrator); SQL2025 target | 749f287e | Web.UI 0/0; 7/7; `G-I1.trx` |
| **G-I1 auth enforcement** (High, caught by adversarial review) | `[Authorize]` was **inert** (bare `RouteView` + no auth middleware) → restored `AuthorizeRouteView` + `UseAuthentication/UseAuthorization` + `AddAuthorization` | dcf77713 | Web.UI 0/0 + static proof |
| **G-H2 / G-H3** route auth | `[Authorize]` on Dashboard + SlaDashboard (live via the enforcement fix) | 749f287e, dcf77713 | Web.UI 0/0 |
| **G-C1** non-notification guard (FR31) | Assembly-scanning architecture guard over 21 production assemblies | fb95b1fb | 1/1; `G-C1.trx` |
| **G-H5** identity dedup (FR10) | DB-backed `IPersonIdentityResolver` (RFC + variants, dedup-to-one) for the Web.UI/manual-review path | 7a3703d0 | 54/54 Testcontainers; `G-H5.trx` |
| **G-S1** storage encryption (owner) | AES-256-GCM, config/env key, **per-purpose HKDF subkeys, purpose not persisted**; FS adapter + **Orion download path** (RV-1) | 4367ad26, d3677838 | 53/53 + Orion 57/57; `G-S1.trx`, `RV-fixes.trx` |
| **G-S2 / G-S4** audit ledger + DDL trigger (owner) | AuditRecords → append-only ledger; `TR_ProtectCriticalSchema` DROP/ALTER block; Serilog SEQ+SQL sinks (4 hosts) | f2e257d0, d3677838 | 7/7 Testcontainers; `G-S2-S4.trx`; ADR-022 |
| **G-S3** outbox/retry worker (owner) | `OutboxRetryWorker` re-raises unprocessed events with retry cap → dead-letter; **registered + hosted** | 79799c61 | 62/62; `G-S3.trx` |
| **G-M1/M3/M4** ops | `/health/live` (200, no DB), structured `/health` JSON, login scaffolding removed | a2447225 | Web.UI 0/0 |
| **G-M2** PDF off-by-one (FR6) | Exception-sentinel loop → bounded loop | 266e1e50 | 164/164; `G-M2.trx` |
| **G-H4** 7 PRP interfaces | ADR-015..021 — all DE-SCOPE (capability fulfilled elsewhere; cited) | 8ea15725 | ADRs |
| **RV-2 / RV-3** | `[Authorize]`+`RequireAuthorization()` on ProcessingHub; Serilog env-var binding fixed across 4 hosts | d3677838 | solution 0/0 |

**Owner-ruling fidelity confirmed:** export MUST block (G-C2 honored on both the in-process
and 3-process paths); Identity interfaces in Domain / impl in Infrastructure adapter
(G-I1); AES-256 + protected purpose strings (G-S1); audit SQL ledger + Serilog SEQ/SQL
(G-S2); vendor-agnostic storage (CR8, no Azure SDK).

---

## What remains RUNTIME-GATED (execute on the live box before sign-off)

These are **not code gaps** — they need an environment the headless agents could not reach.

1. **Point Web.UI at SQL2025 + apply migrations.** `appsettings.json DefaultConnection` still
   names `SQL2022`; set it to `DESKTOP-FB2ES22\SQL2025` (Windows auth) and set
   `Storage:EncryptionKey` (base64 32-byte) + `SERILOG_SQL_CONNECTION` env vars. Apply the EF
   migrations (`AddOutboxEvents`, `AuditLedgerAndDdlTrigger`, `AddReviewCaseHandoffPath`) —
   **owner-gated** because the ledger/DDL-trigger migration alters live schema. *(G-I1, G-S2/S4)*
2. **Boot + seed.** Confirm `/health/ready` Healthy, `/health/live` 200; the seeder creates the
   Reviewer + Admin users. *(G-I1)*
3. **Anon-probe.** `curl -o /dev/null -w '%{http_code}' /dashboard /sla-dashboard /manual-review`
   anonymous ⇒ expect **302/401**. Capture to `closure-evidence/G-H2-probe.txt`, `G-H3-probe.txt`. *(G-H2/G-H3)*
4. **Authenticated UI walkthrough (G-D2).** Playwright login as Reviewer; screenshot Manual
   Review, SLA Dashboard, Audit, Export, Dashboard → `closure-evidence/ui/`. Clears CR3/FR14/FR30.
5. **Capstone E2E (G-D1).** SIARA simulator at
   `E:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Deployments\Siara.Simulator\app\` with
   `ResetCasesOnStartup:true` + `cases.json` deleted; run `MaxFidelityGateFullPipelineE2ETests`;
   assert **both** export events (SIRO XML **and** DatosCargaOficio xlsx). Two consecutive clean runs.
6. **Perf NFR1/3/4/5 (G-D3).** Owner-deferred until E2E is stable.

---

## Backlog follow-ons (tracked, non-blocking for staging)

- **G-H1b** — inline `SaveAs` blocks the Blazor circuit at `ExportManagement.razor:600` (offload when `SynchronizationContext.Current != null`).
- **G-I1-cleanup** — delete unregistered `Web.UI/Data/ApplicationDbContext`+`ApplicationUser`+migrations (dead code).
- **G-H4-debt** — 3 of 7 de-scopes carry ITDD interface-conformance debt (extract `IAuditReportingService`; `IFieldAgreement`/`IFieldMatcher<T>` contract notes); amend `PRP.md` mapping.
- **G-D4** — signed-PDF (FR16) DI wiring: owner scope decision still open (⛔).
- **SignalR operator-group scoping** — RV-2 added `[Authorize]`; fine-grained operator-group targeting is a TODO enhancement.
- **Storage key rotation** — G-S1 has no key-version header; rotation is a documented follow-up.

---

## Bottom line

The Phase-2 *NOT READY* verdict has been worked down to a **single runtime-verification
condition**. All code-level blockers (the original 2 Critical + 6 High, **plus** two
adversarially-discovered latent staging blockers and four final-review findings) are
**closed and ground-truth-verified**. Recommend: **execute the live verification pass above;
on green evidence, promote to staging.** The orchestration tracker (`GAP-CLOSURE-TRACKER.md`)
and `closure-evidence/` hold the per-item proof; TaskList in-session mirrors it.
