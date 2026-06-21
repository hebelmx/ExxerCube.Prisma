# Phase-2 Remediation — BMAD Orchestration Kickoff

**You are the orchestrator.** This document is your complete brief to **plan and drive the full scope of
Phase-2 gap-closure + owner-added work** for ExxerCube.Prisma. Read it top to bottom, then expand it into a
BMAD sprint plan and execute via isolated subagents, verifying every result from ground truth.

> **How to start:** `/bmad-orchestrator docs/qa/phase2-review/REMEDIATION-ORCHESTRATION-KICKOFF.md`
> First action = **PLAN** (do not start coding blind): turn the epics below into BMAD epics+stories
> (`bmad-create-epics-and-stories` / `bmad-sprint-planning`), write the missing design ADRs, then execute
> story-by-story. Keep all state in files (tracker + sprint docs), commit in meaningful chunks, push to `Liv`.

---

## 1. Mission & success criteria

Close the gaps the independent Phase-2 QA review found, and add the owner-requested work, so the product
clears the **re-evaluation bar for staging**. The independent verdict was **NOT READY FOR STAGING**; the
owner has since ruled on every Human-Review item (see §3), narrowing the blocker set.

**Mission is complete when:**
1. The three staging blockers are fixed + proven live: **export-block gate (G-C2)**, **Excel export (G-H1)**,
   **unauthenticated dashboards (G-H2/G-H3 via Identity)**.
2. The **capstone E2E** (`MaxFidelityGateFullPipelineE2ETests`) passes with **both** export events
   (SIRO XML **and** DatosCargaOficio xlsx), and an **authenticated UI walkthrough** clears CR3/FR14/FR30.
3. The owner-added hardening (Identity, storage encryption, audit ledger, outbox worker, DDL-trigger,
   ADR-per-interface) is delivered or explicitly backlogged with an ADR.
4. A fresh **adversarial review** confirms the fixes against this brief, and the deployment recommendation
   is re-issued from new evidence.

---

## 2. Canonical inputs (the intended-solution anchors — review diffs against THESE, not subagent prose)

| Doc | Role |
|-----|------|
| `docs/qa/phase2-review/GAP-CLOSURE-TRACKER.md` | **The task backbone** — every G-task with file:line, remediation, Definition of Done + evidence artifact. Update status here as you go. |
| `docs/qa/phase2-review/PHASE2-FINAL-REPORT.md` **§1a** | Owner Decisions table — the binding dispositions. **§5/§8/§9** = findings, risks, recommendation. |
| `docs/qa/phase2-review/closure-evidence/` | Evidence produced during closure (e.g. `HRQ-12-notification-sinks.txt`). Put new closure evidence here. |
| `docs/product/requirements/prd.md` | FR1–32, NFR1–17, CR1–8, Epic-1 Stories 1.1–1.9 (acceptance criteria + Integration-Verification invariants). |
| `docs/product/requirements/PRP.md` | 28-interface ITDD design — the contract for the 7 absent interfaces (G-H4). |

---

## 3. Owner rulings — BINDING (do NOT re-ask these; they are decided)

All 13 Human-Review items are answered (report §1a). Summary you must honor:
- **Export must BLOCK** on low confidence / unresolved fusion conflict; release only after human review. (CRIT-2 → build G-C2.)
- **Identity** is implemented as **infrastructure**: interfaces in **Domain**, implementation in an **Infrastructure adapter**, all scaffolding inside the adapter project. Use **SQL Server `DESKTOP-FB2ES22\SQL2025` (Windows authentication)**. This replaces Azure AD (NFR16 **de-scoped**).
- **Storage** stays **vendor-agnostic** behind `IDownloadStorage` (Azure Blob = over-spec, CR8 met). **NEW requirement:** encrypt at rest — **the local FS adapter must be encrypted too** (G-S1).
- **Audit immutability** = deploy-time **SQL Server Append-Only Ledger Table**; Serilog → **SEQ + SQL Server** sinks (G-S2). Not an app-code FAIL.
- **NFR14** = PASS; add an **outbox/retry background worker** (unprocessed event → persist + re-raise) (G-S3).
- **NFR15** = PASS (async ingestion; pack the 1–3 docs per case). **INV-11** = PASS (in-process correlation is enough).
- **CR4** dev-acceptable now (EF migrations); for prod add a **DDL trigger** blocking DROP/ALTER on critical tables, or recreate the DB with fresh migrations (G-S4).
- **7 absent PRP interfaces:** research + **draft an ADR per interface** (fulfilled-elsewhere → de-scope, or needed → implement). Start with `IFieldMatcher<T>` (G-H4).
- **Non-notification (FR31)** satisfied by absence (grep-verified). Residual: a **regression guard** + scope SignalR `Clients.All` → authenticated operator groups once Identity lands (G-C1).
- **NFR17** (field-level PII encryption) **deferred** to a later release (backlog). **Performance NFRs** deferred until a stable full E2E exists. **Real SIARA portal** is legally gated until deployment — simulator only.

---

## 4. Environment ground truth & gotchas (verified; carry into every subagent brief)

- **Repo root:** `E:\Dynamic\IndFusion\ExxerCube.Prisma\ExxerCube.Prisma`. **Branch: `Liv`** (push here; never `main` without a checkpoint).
- **dotnet:** `C:/Program Files/dotnet/dotnet.exe` (10.0.301). Build artifacts land OUTSIDE the repo at `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\`.
- **E: drive is SLOW** — build/test **single projects**, never the whole `.sln`. ~10 min/build; use long timeouts + background tasks.
- **Tests:** xUnit v3 + **Microsoft.Testing.Platform**. `global.json` opts `dotnet test` into MTP. **Do NOT pass `--nologo`.** Filter: `--filter-query "/Assembly/Namespace/Class/Method"`. Capture: `--report-trx --report-trx-filename <n>.trx`.
- **Conventions (warnings-as-errors):** `Result<T>` for business logic (no exceptions for control flow), `CancellationToken cancellationToken = default` on every async method, nullable enabled, `ConfigureAwait(false)` in libraries. Tests: Shouldly + NSubstitute; naming `Method_Scenario_Expected`; use `TestContext.Current.CancellationToken`. Do NOT use Moq/FluentAssertions.
- **SQL for verification:** live **`DESKTOP-FB2ES22\SQL2025` (Windows auth)** per owner; Testcontainers SQL also available (Docker 29.5.3 live).
- **Capstone E2E recipe (so live verification is repeatable):** it resolves the **sibling** simulator at `E:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Deployments\Siara.Simulator\app\`. Its corpus is populated, but its `cases.json` is a *served-ledger* — with `ResetCasesOnStartup:false` it serves 0. **Set `ResetCasesOnStartup:true` + delete `cases.json`** there so it serves cases. (The live-repo corpus at `Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats/` is restored, 500 cases, gitignored.) Stabilising this is **G-D1**.
- **Playwright** chromium is installed (`~/AppData/Local/ms-playwright`).
- **Web UI** boots on a Kestrel port (was `:5172`); `dotnet run --project <Web.UI.csproj>`. Health: `/health` (readiness), needs `/health/live` (G-M1).

---

## 5. Work breakdown — EPICS (expand each into BMAD stories during PLAN)

> **Priority for staging = EPIC-0 → EPIC-1 → EPIC-2 → EPIC-8.** EPICs 3–7 run in parallel where independent.
> Per task: file:line, remediation, and Definition-of-Done with the evidence artifact are in `GAP-CLOSURE-TRACKER.md`.

### EPIC-0 — Test-environment enablement (do first; unblocks live verification)
- **G-D1** stable corpus + simulator reset recipe (repeatable capstone E2E).
- Point Web UI at live SQL `DESKTOP-FB2ES22\SQL2025` (Win auth); confirm `/health` ready + DB-backed screens render.
- **BMAD:** `dev` + `qa`. **Exit:** two clean consecutive capstone runs serve documents; Web UI shows DB data.

### EPIC-1 — Identity as infrastructure  **(KEYSTONE — unblocks the most)**  → G-I1
- Domain auth interfaces; Infrastructure adapter (all Identity scaffolding inside it); seed Reviewer + Admin; live SQL.
- **BMAD:** `architect` (write an **ADR: Identity-as-infra-adapter**, design the Domain↔adapter seam) → `bmad-dev-story` → `qa`.
- **Unblocks:** CR3, FR14, FR30, HIGH-2/3 route auth, NFR16. **Exit:** anon denied on protected routes; Reviewer session opens them; authenticated screenshots in `closure-evidence/ui/`.

### EPIC-2 — Staging blockers (must-fix code)
- **G-C2** export-block gate (CRIT-2): block Stage-5 when `RequiresManualReview` OR fusion conflict; release only after a review decision. (`ReconciliationOrchestrator.cs:243-279` + `ExecuteStage5ExportAsync`.)
- **G-H1** Excel/DatosCargaOficio export completes + emits `ExportCompletedEvent(DatosCargaOficioXlsx)` (or fails loudly + audited).
- **G-H2/G-H3** `[Authorize]` on `SlaDashboard.razor` + `/dashboard`; scope SignalR `Clients.All` → authenticated operator groups. (Depends on EPIC-1.)
- **G-C1** non-notification regression guard (test fails if an external-notify sink is added without a legal gate).
- **BMAD:** `dev` + `qa` per story; **adversarial gate** after the epic.
- **Exit:** capstone E2E passes both export events; sub-threshold/conflicted case ⇒ no export + held-for-review; anon ⇒ non-200 on dashboards.

### EPIC-3 — Data integrity & dedup
- **G-H5** person identity dedup: DB-backed `FindByRfcAsync`, persist Persona, register the resolver in Athena/Orion worker pipeline. (INV-2/FR10.)
- Coverage: INV-1 checksum uniqueness, INV-3 referential integrity (Expediente/Oficio) tests.
- **BMAD:** `dev` + `qa` (Testcontainers SQL integration test).

### EPIC-4 — Security & compliance hardening
- **G-S1** storage encryption at rest (local FS adapter + future cloud), E2E. (Owner: local must be encrypted too.)
- **G-S2** audit SQL **Append-Only Ledger Table** + Serilog SEQ/SQL sinks. (INV-4/FR17.)
- **G-S4** prod schema-protection: DDL trigger blocking DROP/ALTER on critical tables (or migration policy). (CR4.)
- **BMAD:** `architect` (ADR for ledger + encryption approach) → `dev` → `qa`.

### EPIC-5 — Reliability & ops
- **G-S3** outbox/retry background worker (NFR14 enhancement).
- **G-M1** register `/health/live` (liveness, no DB dep). **G-M4** structured `/health` JSON body. **G-M3** replace login-page scaffolding text.
- **BMAD:** `dev` + `qa`.

### EPIC-6 — Architecture conformance (ADRs)
- **G-H4** research + **one ADR per absent PRP interface** (7): `IFieldMatcher<T>`, `IRuleScorer`, `IScanDetector`, `IScanCleaner`, `IReportGenerator`, `IUIBundle`, `IFieldAgreement`. Each ADR: is the capability present elsewhere? PRP contract met? decision = implement / de-scope. Spawn implement-stories only for those ruled "needed."
- **BMAD:** `architect` (use `bmad-create-architecture` patterns); ADRs under `docs/architecture/adr/`.

### EPIC-7 — Bug fixes (Medium/Low)
- **G-M2** PDF page-index off-by-one (FR6). Plus any Low items from report §5 still open.
- **BMAD:** `dev` + `qa`.

### EPIC-8 — Coverage & re-verification  **(the exit gate)**
- **G-D2** seeded auth session → authenticated UI walkthrough → clear CR3/FR14/FR30 (record decisions in report §6 cards).
- **G-D3** performance instrumentation (deferred per owner — schedule once E2E is stable; then close NFR1/3/4/5).
- **Re-run** capstone E2E (both export events) + harness integration; capture TRX + artifacts in `closure-evidence/`.
- **Final adversarial review** of the whole remediation against this brief (`bmad-review-adversarial-general` / `plan-completion-reviewer`), then **re-issue the deployment recommendation** in a short addendum to the report.

**Backlog (owner-deferred, not in this mission):** NFR17 PII field encryption; real SIARA portal; perf until E2E stable.

---

## 6. Orchestration discipline (the loop)

1. **Re-ground** from the tracker; pick the next actionable story by the priority in §5.
2. **Delegate** one cohesive story to an isolated subagent (`bmad-dev-story`/`dev` for code, `qa` for tests, `architect` for ADRs/design). Give it the §4 gotchas + the story DoD. Run independent stories in parallel.
3. **Verify from ground truth** — build the touched project(s) (single project), run the relevant test project (MTP, no `--nologo`, `--report-trx`), inspect `git diff`. Red ⇒ re-delegate with the failure or fix the small gap; do not advance.
4. **Commit** in a meaningful chunk (what + why + verification line). **Push to `Liv`.**
5. **Record** status in `GAP-CLOSURE-TRACKER.md` (☐→◐→☑) + evidence path in `closure-evidence/`.
6. **Adversarial gate** at each epic boundary (or every 3 stories): fan out skeptics to refute the diff **against this brief + the tracker DoD**, not against subagent claims. Triage findings back into the tracker.
7. Loop until §1 success criteria are met, then write a completion summary + re-issue the deployment recommendation.

**Checkpoints (ask the owner before proceeding):** the §3 rulings are decided — do NOT re-ask. Gate only on *new* forks: e.g. an ADR (G-H4) concludes an interface IS needed and is a large build; the storage-encryption key-management approach (G-S1); whether NFR17 PII encryption should be pulled forward; any schema migration that isn't additive beyond the agreed FK drops. Irreversible/outward-facing actions (schema migrations on the live SQL box, anything to `main`) always gate.

---

## 7. First moves for the new orchestrator

1. Read §2 canonical inputs (tracker + report §1a) to load the full task set.
2. Run **PLAN**: `bmad-create-epics-and-stories` + `bmad-sprint-planning` to turn EPIC-0…8 into a story backlog with the priority/deps in §5; write the **Identity ADR** (EPIC-1) and queue the **7 interface ADRs** (EPIC-6).
3. Start **EPIC-0** (env enablement) so all later work is live-verifiable, then **EPIC-1 (Identity)**.
4. Keep `GAP-CLOSURE-TRACKER.md` as the single source of truth; this kickoff is the plan, the tracker is the ledger.
