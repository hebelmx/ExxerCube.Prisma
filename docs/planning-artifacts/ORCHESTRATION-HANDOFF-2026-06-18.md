# Veriqan VEC — MVP-Complete Continuation Handoff (Tranche 2 resume point)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Latest commit:** `217ced7d` (all pushed to `origin/Liv`)
**Supersedes** `ORCHESTRATION-HANDOFF-2026-06-17.md` (Epic-4-era). This is the canonical resume point.

The **original 55-item-checklist MVP (Epics 1–8) is COMPLETE, end-to-end proven, and adversarially
reviewed.** Remaining work = the **Tranche 2 regulatory tranche (E9–E13)**. Read this, then
`docs/planning-artifacts/epics-tranche2-regulatory.md` + `epics.md` + `architecture.md`.

> **UPDATE 2026-06-18 — Epic 9 (the seam) is COMPLETE + adversarially reviewed + remediated** on `Liv`
> (commits `da72eb49`→`0314045b`, all pushed). Stories 9.1–9.6 delivered: dual legal/tenant verdict on
> `RuleFinding`, required `DofNumeral` (NFR-7) on all 35 rules + coverage map, `RuleClassification`
> (BaselineLocked/TenantTightenableOnly/TenantOverridable), typed range-bounded `Tolerance` with legal
> defaults in an **encrypted read-only SQL store** (AES column converter, `VeriqanLegalBaselineStartupService`
> seeds/initialises + fails loud — owner ruling), `TenantProfile` resolution (tighten applies; sub-legal
> loosening → reject + loud `TenantDeviation` + fall back to legal floor), confidence-driven abstain
> (`MinFieldConfidence` floor 0.8). A 2-skeptic review found and a remediation closed: the SQL store was
> registered-but-unwired, the confidence floor was droppable, the pipeline path was dead, and the legal
> verdict wasn't statement-level-separable. Whole Veriqan footprint green (~510 tests; e2e still RED).
> **Carry-forwards:** persist `LegalBaselineVerdict` for the E13 traceability export; FiscalBlock
> (CL-50..53) extraction-confidence so a garbled QR abstains, not false-Fail; tolerance ranges + CL-46
> numeral pending legal review (tighten in E10). **NEXT = Epic 10 (Structural & Textual Completeness).**

---

## 1. How to resume

Re-invoke the `bmad-orchestrator` skill (arg `docs/planning-artifacts/HANDOFF.md`) or continue the loop.
**Next = Tranche 2, starting at E9 (the rule-contract seam) — it MUST precede any new rule epic because it
retrofits the existing ~31 rules.** The owner approves epic-by-epic and is remote — surface design forks via
AskUserQuestion. Tranche-2 gates (from the planning agent): E9 seam first; E11 needs a table-extraction
spike + a ground-truth corpus; E12 gated on E5 (done); **E13 gated on issue #17** (economic-buyer discovery
— human intel, not a code task).

## 2. What is DONE — the full MVP (Epics 1–8) on `Liv`, pushed

All additive except ONE corrective Solution-1 `<Compile Remove>` (see §6 gotchas). Each epic was
ground-truth-verified + adversarially reviewed + findings closed. Commit range `83e5701f`…`217ced7d` (~50 commits).

| Epic | Delivered | Anchor commits |
|---|---|---|
| 1 Foundation | 10 `Veriqan.*` projects, `Veriqan→Prisma` NetArchTest rule, isolated `VeriqanDbContext`/`veriqan` schema, net-new pkgs + smoke | `83e5701f`→`f5ef52c0` |
| 2 Ingestion & Ref Data | idempotent ingest→Job; `IVecReferenceDataProvider` + `VecReferenceBundle` + draft-2020-12 schema gate + CSV adapter (all sections); product resolution + graceful degradation | `1f3ce25d`→`70da5f4d` |
| 3 Field Extraction | PdfPig text-layer → `StatementModel` (header + `PeriodSummary` + movements) w/ confidence + bbox locators; Spanish dates; `TasaMatcher` | `346614cf`,`c9d8cf45` |
| 4 **Financial Engine** | `IVecValidationRule` + `VecValidationEngine` (Scrutor DI, deterministic, batch-isolated) + `RuleFinding`; real PASS/FAIL CL-10/17/18/19/20/21/22/24/25/42/44/45/item-58 | `6cf5683d`→`f51d20ff` |
| 5 Visual | font/Aptos CL-35, overlap+headers CL-28/29, pagination/blank/per-page CL-31/33/34/48 (PdfPig geometry). 5.4 image-presence DEFERRED | →`424351d0` |
| 6 Regulatory & Fiscal | legends+COMPARA CL-32/46 (`VecTextNormalizer`), fiscal QR/RFC CL-50..53 (ZXing+PDFtoImage), promotions CL-49 — never false-FAIL | →`e88249de` |
| 7 Reporting/QA | `VerdictAggregator` (BLOCKED>RED>GREEN, InsufficientData never alone→RED), color-marked PDF (PdfSharp), RED email (Polly retry), append-only `Disposition` audit (actor required) | `19246bb7`→`5bed45be` |
| 8 Batch & Observability | end-to-end `VerificationPipeline` + bounded-concurrency `BatchProcessor` + exception queue; resume/reprocess idempotent; metrics (Meter, p95, throughput) + correlation ids | `067a58b4`→`80acd33e` |

**END-TO-END PROVEN:** `VerificationPipelineEndToEndTests` runs the real pipeline over
`Prisma/Fixtures/PRP2/01+Dummie+VEC+jul_ago+20252.pdf` → **verdict RED, 35 findings** (CL-10/17/18/19/20…).

**Test inventory (all green; ~430+):** Application.Tests 40 · Extraction.Tests 73 · Validation.Tests 147 ·
Visual.Tests 48 · ReferenceData.Tests 38 · Reporting.Tests 21 · Tests.Smoke 4 ·
Persistence.IntegrationTests 1 (Docker) · Orchestration.Tests 31 · Architecture (Tests.Architecture) 26
(incl. the `Veriqan→Prisma` rule). Full solution `dotnet build` = 0/0. (Re-run for exact current counts.)

## 3. The building blocks (the whole pipeline is composed — reuse it)

`AddVeriqan(IServiceCollection, IConfiguration)` (`Veriqan.Orchestration/DependencyInjection`) is the
composition root. The per-statement `IVerificationPipeline` threads: ingest → extract `StatementModel` →
bind bundle/product/availability → build final `VerificationContext` (with the model) → `IVecValidationEngine`
→ `IVerdictAggregator` → `VerificationOutcome` (Job, VerdictSummary, Findings, ProcessingDuration).
`IBatchProcessor` runs it with bounded concurrency + exception queue + resume + metrics. Rules are
`IVecValidationRule` (CheckId, TechniqueClass, `Result<RuleFinding> Evaluate(VerificationContext, ct)`),
auto-discovered by Scrutor in the Validation + Visual assemblies.

## 4. Tranche 2 plan (E9–E13) — see `epics-tranche2-regulatory.md` + epics.md FR-28..39

The CONDUSEF *Acuerdo* (28 mandatory sections) is a superset of the 55-item checklist; ~12 NEW
law-mandated checks + ~10 PARTIAL (bold/typography/exact-text precision) + the multi-tenant productization.
Most NEW checks are **intra-statement self-contained** (recompute from the statement's own reported
figures) → reuse the Epic-4 engine + Epic-5 geometry; not blocked on external data. Highlights:
- **E9 — the seam (DO FIRST):** generalize the shared rule contract + retrofit the existing ~31 rules
  (e.g. per-finding DOF-numeral traceability / NFR-7, a tenant-profile selector / FR-39, the legal-floor vs
  client-brand layering). Because it changes `IVecValidationRule`/`RuleFinding` and touches every rule,
  land it before adding new rules.
- **E10–E11:** the NEW deterministic checks — §20 payment-distribution waterfall, §6 simulation, §19
  interest recompute, §26 (13 verbatim notas) / §27 (15-term glosario) / §24 legends, §8/§16 sections,
  typography legal floor, advertising placement. **E11 prereq:** a table-extraction spike + a ground-truth
  corpus from the PRP2 Dummie fixtures (the recompute rules are only synthetically testable without it).
- **E12:** visual/typography completeness (gated on E5 — done).
- **E13:** productization/traceability deck — **gated on issue #17 buyer discovery** (human intel).

## 5. Carry-forwards (all honest InsufficientData — never a false PASS/FAIL)

- **Reference-data product-alias alignment (NEW, from the e2e run):** extraction yields the product as
  `"Tarjeta de Crédito BSSB"` but the CSV bundle aliases only `"BSSB"` → binding BLOCKs on real fixtures.
  Fix the reference-data aliases to cover the extracted token (or make `ProductResolver` fuzzier). The e2e
  test works around it with a matching bundle.
- TotalCargos extraction gap (CL-44 charge side; credits validated exactly).
- CL-26 needs a separate "crédito disponible para disposiciones de efectivo" extracted value.
- COMPRAS-A-MESES installment table extraction (CL-23/40/41) — unscheduled.
- Rewards-section extraction + a rewards-bearing fixture (all 3 fixtures are BSSB/no-rewards) — CL-36/37/39.
- CL-43 per-page DESGLOSE range-header capture.
- 5.4 catalog image-presence (pHash, CL-27/30/47) — client image-catalog answer (§6).
- **`Veriqan.Orchestration` co-location:** it lives under the Solution-1 `03 Orchestration/` root; a
  `<Compile Remove>` in the Prisma csproj stops the glob, but any new file dropped directly in that folder
  (not the project) is silently excluded. Relocating the folder would remove the trap.

## 6. Orchestration mechanics + gotchas (hard-won — save yourself the rediscovery)

- **Verify from ground truth every story:** `dotnet build` the touched project(s) + `dotnet test` the test
  project + `git status`/`git diff` for scope. Believe those, not the subagent prose. A subagent reported
  `tool_uses: 1` with a full implementation once — the files were real but ALWAYS re-verify.
- **Adversarial review at each epic boundary** (`plan-completion-reviewer` vs the ACs + the checklist CSV).
  It caught: vacuous tests (the e2e capstone asserted only `IsSuccess||IsFailure`), a DI double-registration,
  the CL-29 date-contamination false-FAIL, the CL-26 always-Pass vacuity, normalizer duplication. Worth it
  every time. Triage findings → tracker → close them in a focused remediation subagent.
- **One commit per story** (`feat(veriqan #X.Y): …` + a Verification line), push to `Liv` after each.
- **Tell subagents: do NOT git-commit, do NOT touch the `.sln`, do NOT touch `Directory.Packages.props`**
  (unless adding a pre-approved pkg), build ONLY their project. Early subagents self-committed + raced the
  sln; the explicit rules fixed it. YOU add new test projects to the sln, then full-build.
- **Building the full solution right after `dotnet sln add`** can show a transient parallel-build error
  (new project outputs not ready) — re-run; but ALSO a real glob-contamination can hide here (see the
  `Veriqan.Orchestration` Compile-Remove). Always reconcile a full-solution 0/0 before committing.
- **`dev` subagents sometimes spawn a stray `.claude/worktrees/agent-*` worktree** — clean after each:
  `git worktree remove <path> --force; git worktree prune; git branch -D worktree-agent-*`.
- **Test projects referencing BOTH a Veriqan AND a Prisma project must be named `ExxerCube.Prisma.Veriqan.*`**
  or the dependency-direction arch test flags them.
- **Shell:** a `grep` that matches nothing returns exit 1 and breaks `&&` before a following `git commit` —
  don't gate a commit behind a grep in the same chain.
- **Coordinate systems:** PdfPig is bottom-left origin; PdfSharp is top-left → flip Y when marking PDFs
  (`top = pageHeight - bottom - height`).
- **Docker is available** (SQL Server 2022); Testcontainers DB tests run (~35s).
- **The owner edits in the SAME local repo** (commits/docs appear in your working tree) — fetch/reconcile
  before assuming divergence; pushes from your side sync their local commits up too.

## 7. Authoritative artifacts + legal context

`docs/planning-artifacts/`: HANDOFF.md (§0 status), epics.md (E1–E13 + FR-1..39), architecture.md (ADRs),
`epics-tranche2-regulatory.md` (E9–E13 stories), `LAW-VS-CHECKLIST-GAP-2026-06-17.md`,
`CERTIFICATION-RESEARCH-2026-06-17.md`. **Legal (authoritative):** `docs/legal/regulations/` — CONDUSEF
`Acuerdo_estado_de_cuenta.pdf` (the 28-section format) + SIARA/DGAAC. Tranche-2 terminology/checks must
accord with it; cite DOF numerals for traceability (NFR-7).

## 8. Tracker / memory

Task tracker: all E5–E8 stories + every review-remediation are `completed`; only `5.4` remains pending
(deferred, client-gated). Tranche-2 (E9–E13) tasks are NOT created yet — create them when starting E9.
Memory: `veriqan-vec-progress.md` (+ `MEMORY.md` index) is current (Epics 1–8 done, Tranche 2 next,
strategy + carry-forwards). Update both after each milestone.
