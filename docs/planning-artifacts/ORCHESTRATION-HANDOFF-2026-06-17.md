# Veriqan VEC — Orchestration Continuation Handoff

**Date:** 2026-06-17 · **Branch:** `Liv` · **Latest commit:** `6c6107c2` (all pushed to `origin/Liv`)
**For:** the next agent resuming the `bmad-orchestrator` run on `docs/planning-artifacts/HANDOFF.md`.

This is a *resume point* so you don't inherit a huge context. Read this, then
`docs/planning-artifacts/epics.md` (Epic 4 onward) + `architecture.md`. The original planning handoff is
`docs/planning-artifacts/HANDOFF.md` (its "§0 Implementation status" is kept current).

---

## 1. How to resume

Re-invoke the `bmad-orchestrator` skill with arg `docs/planning-artifacts/HANDOFF.md` (or just continue the
loop manually). **Epic 4 is now DONE (2026-06-17).** **Next work = Epic 5 (Visual & Print-Quality).** The
user approves epic-by-epic; check in at boundaries. The owner is remote — surface design forks via
AskUserQuestion. **E5 partially needs the image-catalog open client answer (§6)** — font (CL-35),
text-overlap (CL-28/29), pagination/blank/per-page (CL-31/33/34/48) are buildable now from PdfPig
geometry + render; the catalog IMAGE-PRESENCE checks (CL-27/30/47, pHash) need the catalog delivery/keying
answer.

**Epic 4 (Financial Consistency Engine) — DONE** (commits `6cf5683d`,`eb8d34c4`,`c0609b62`,`f47a9042`,
`f9d189ae`,`77360987`, findings `f51d20ff`), adversarially reviewed; every checklist formula confirmed
correct + two semantics points owner-adjudicated. `IVecValidationRule`+`VecValidationEngine` (Scrutor DI,
deterministic, batch-isolated); RuleFinding (Domain.Verification). Real PASS/FAIL: CL-10 (CAT,
percentage-point tolerance), CL-17, CL-18, CL-19, CL-20 (credits-only — confirmed via MX cargo/abono
semantics + CONDUSEF), CL-21, CL-22, CL-24, CL-25, CL-42, CL-44 (credits side), CL-45, item-58. Validation.Tests 96, Extraction.Tests 54.

**Legal context (NEW, authoritative):** `docs/legal/regulations/` holds the CONDUSEF
`Acuerdo_estado_de_cuenta.pdf` (+ SIARA/DGAAC docs). VEC terminology + regulatory checks (esp. Epic 6
legends/sections, and cargo/abono semantics) MUST accord with it.

**Epic 4 carry-forwards (P2, deliberate InsufficientData — honest degradation, never false PASS/FAIL):**
- `TotalCargos` not extracted on the fixtures → CL-44 charge side InsufficientData (credits validated
  exactly). Needs extractor tuning to find the "Total de cargos" footer.
- CL-26 needs a separately-extracted "crédito disponible para disposiciones de efectivo" value (now
  InsufficientData, was vacuously always-Pass).
- COMPRAS-A-MESES installment-table extraction is UNSCHEDULED → CL-23/40/41 InsufficientData.
- Rewards-section extraction + a rewards-bearing fixture (all 3 fixtures are BSSB/no-rewards) → CL-36/37/39.
- CL-43 per-page DESGLOSE range header not captured → InsufficientData.

## 2. What is DONE (Epics 1–3 + P2 hardening) — all on `Liv`, pushed

| Epic | Stories | Status | Key tests |
|---|---|---|---|
| **1 Foundation & Isolation** | 1.1–1.4 | ✅ reviewed | arch 26/26, smoke 4/4 |
| **2 Ingestion & Reference Data** | 2.1–2.3 | ✅ reviewed, findings closed | App 13/13, RefData 38/38 |
| **3 Field Extraction** | 3.1–3.2 | ✅ reviewed, findings closed | Extraction 33/33 |
| **P2 hardening** | DB-coexistence + full CSV adapter | ✅ done | Persistence.IntegrationTests 1/1 (Docker) |

Commit range: `83e5701f` … `6c6107c2` (19 commits). **No Solution 1 production source was ever modified.**
One pre-existing break was fixed with owner approval: `7da2547f` (BrowserAutomation.E2E missing usings —
the branch was NOT green at the original handoff; verify with a full build, don't trust "green" claims).

**Test projects (all green):**
`08 Tests/01 Core/Veriqan.Application.Tests` (13) ·
`08 Tests/02 Infrastructure/Veriqan.Infrastructure.Extraction.Tests` (33) ·
`…/Veriqan.Infrastructure.ReferenceData.Tests` (38) · `…/Veriqan.Infrastructure.Tests.Smoke` (4) ·
`08 Tests/05 System/Veriqan.Infrastructure.Persistence.IntegrationTests` (1, Docker) ·
`08 Tests/09 Architecture/Tests.Architecture` (26, includes the Veriqan→Prisma rule).

## 3. Building blocks Epic 4 plugs into (already built — reuse, don't rebuild)

- **`VerificationContext`** (`ExxerCube.Prisma.Veriqan.Application.Binding`) — carries `Bundle`
  (`VecReferenceBundle`), `ResolvedProduct`, `Availability` (`ReferenceDataAvailability`),
  `PriorStatement?`, `ToleranceConfig?`, and `StatementModel?`. **This is the rule engine's input** (the
  architecture's `ctx`). Built by `BundleBinder` (`Application.Services`).
- **`StatementModel`** (`…Veriqan.Domain.Extraction`) — header identity fields + `PeriodSummary?`
  (`Product`, `PeriodStart`, `PeriodCutDate`, `PaymentDueDate`, `DayCountPrinted`,
  `PagoParaNoGenerarIntereses`, `PagoMinimo`, `PagoMinimoMasMeses`, `Cat`, `Tasa`, `SaldoDeudorTotal`,
  `CreditoDisponible`). Each field is `ExtractedField<T>` (`Value`/`Confidence`/`Locator`/`Status`).
- **`ReferenceDataAvailability`** (`…Domain.Binding`) + `ReferenceCapability` enum — per-capability
  `Available`/`InsufficientData`. **Rules must consult this** so a missing bundle section → `InsufficientData`
  Finding, not a false `Fail` (FR-20).
- **`TasaMatcher`** (`…Application.Verification`) — rate-vs-bundle-TASA comparison (Match/Mismatch/
  InsufficientData). CL-9 rule can wrap it.
- **`VecReferenceBundle.ToleranceConfig`** (`…Domain.ReferenceData`) — `currencyToleranceMxn` 0.50,
  `pointsTolerance` 1.00, `rewardsPesosToleranceMxn` 1.00, `pointsToPesosExchangeRate` 0.10. **ADR-V3:
  tolerances are DATA — no magic numbers; Findings record the applied tolerance.**
- **`Finding` entity** (`…Domain.Entities`, from Story 1.3) — currently has `Id, VerificationJobId,
  CheckId, Verdict (FindingVerdict {Pass,Fail,InsufficientData}), EngineVersion, Expected, Observed`.
  ⚠️ **Story 4.1 design point:** architecture §5 wants a richer Finding (also `ToleranceApplied`,
  `Severity`, `Locator`, `Technique`). Decide: extend the entity, or introduce an engine-side `Finding`
  value object that maps to the persistence entity. Reconcile these two — don't silently diverge.
- **`JobVerdict`** (`…Domain.Entities`) + `VerdictSignal {Green,Red,Blocked}`, `BlockReason` enum,
  `VerificationJobStatus {… Blocked}`. (Epic 7 does the verdict rollup; Epic 4 produces Findings.)

## 4. Epic 4 plan (from epics.md — 4 stories; ADR-V2/V3, NFR-2/NFR-5/NFR-6)

- **4.1 Rule-engine skeleton:** `IVecValidationRule { CheckId; TechniqueClass Technique; Result<Finding>
  Evaluate(VerificationContext ctx, CancellationToken ct) }` + `VecValidationEngine` with **DI rule
  discovery** (Scrutor is centrally available). Each rule pure/deterministic (same input → same Finding,
  NFR-5); no throwing for control flow (Result<T>, NFR-6). Add `TechniqueClass {Deterministic,
  LightweightCv, Ml}`. Resolve the Finding-shape design point above.
- **4.2 Intra-statement arithmetic** (CL-10,18..26): "pago para no generar intereses" formula (CL-21),
  category sums (CL-18..20), saldo deudor total / crédito disponible / CAT (CL-24,25,10), each within
  `ToleranceConfig` and recording the applied tolerance (FR-8).
- **4.3 Cross-period vs Prior Statement** (CL-17,36..41): uses `Bundle.PriorStatements`; absent prior →
  `InsufficientData`. Rewards/points math, exchange rate 0.1, installment carry-over.
- **4.4 Movement-detail reconciliation** (CL-42..45, item 58): printed DESGLOSE vs
  `Bundle.ExpectedTransactions` (now populated by the CSV adapter); description match (CL-45) + amount
  within tolerance (item 58); totals (CL-44); dates within period (CL-42). Absent expectedTransactions →
  description/amount `InsufficientData`, date checks still run.

Note: Epic 4 needs the printed movement rows extracted from the PDF (the DESGLOSE table). Story 3.x
extracted header+period but NOT the transaction detail table — **4.4 likely needs to extend the extractor**
(or add a movement extractor). Flag this; it may warrant its own sub-task.

## 5. Roadmap after Epic 4 (epics.md)

E5 Visual & Print-Quality (font/overlap/pagination/pHash image presence — uses ZXing/pHash from Epic 1) ·
E6 Regulatory & Fiscal (legends, QR/fiscal via ZXing, promotions) · E7 Findings/Reporting/QA Console
(verdict rollup, marked PDF via PdfSharp, email, human disposition) · E8 Batch & Observability.

## 6. Open client questions (HANDOFF §6 — still unanswered, do not guess)

Reference-data first delivery mechanism (CSV default shipped; DB/API adapters await the answer) · image
catalog delivery/keying (needed for E5 image checks) · accuracy/false-positive bar · alert recipients
(E7) · whether production PDFs are ever scanned (the 3 fixtures are text-layer; OCR stays v2).

## 7. Orchestration mechanics that WORKED (and gotchas — save yourself the rediscovery)

- **Verify from ground truth, every story:** `dotnet build` the touched project(s) + `dotnet test` the
  test project + `git status`/`git diff` for scope. Believe those, not the subagent's prose.
- **Adversarial review at each epic boundary** via `plan-completion-reviewer` against the ACs — it caught
  real test-coverage gaps in Epic 2 & 3 (closed in `f73f77c2`, `1a9ace21`). Worth it.
- **One commit per story** (`feat(veriqan #X.Y): …` + a Verification line), push to `Liv` after each.
  Keep `Directory.Packages.props` adds minimal + commented.
- **Delegation:** `dev` subagents, one cohesive story each. Brief them with exact file paths, the
  IndQuestResults API (`result.Error`, `ResultExtensions.Cancelled<T>()` in `IndQuestResults.Operations`,
  `IsCancelled()`), the xUnit-v3/MTP test stack (mirror `Tests.Architecture.csproj`), and "build ONLY
  your project."
- **Parallel subagents are fine for DISJOINT file sets** — but tell them NOT to touch the `.sln`
  (concurrent `dotnet sln add` races); add their projects to the sln yourself afterward, then full-build.
- **`dev` subagents sometimes spawn a stray git worktree** under `.claude/worktrees/agent-*`. After they
  return, clean it: `git worktree remove <path> --force; git worktree prune; git branch -D worktree-agent-*`.
  The real changes land in the main tree.
- **Test projects that reference BOTH a Veriqan project AND a Prisma project must be named
  `ExxerCube.Prisma.Veriqan.*`** — else `VeriqanDependencyDirectionTests` flags them as a Solution-1→Veriqan
  violation (it exempts Veriqan-prefixed assemblies).
- **Shell gotcha:** in Bash chains, a `grep` that matches nothing returns exit 1 and breaks `&&` before a
  following `git commit`. Don't gate a commit behind a `grep` filter in the same `&&` chain.
- **Docker is available on this machine** (SQL Server 2022); Testcontainers integration tests run (~35s).
- Build artifacts go to `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\` — concurrent full-solution
  builds can race there; have parallel agents build only their own project.

## 8. Tracker / memory

Task tracker: Epics 1–3 stories + review-remediation tasks + both P2 items are `completed`. Memory file
`veriqan-vec-progress.md` (+ `MEMORY.md` index) is current. Update both after each milestone.
