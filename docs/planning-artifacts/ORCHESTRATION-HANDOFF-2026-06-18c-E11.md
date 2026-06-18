# Veriqan VEC — Continuation Handoff (resume at Epic 11)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Latest commit:** `4a9d39bc` (all pushed to `origin/Liv`)
**This is the canonical resume point.** Supersedes the "next = E10" guidance in
`ORCHESTRATION-HANDOFF-2026-06-18.md` (which remains accurate for E1–E10 done-state + gotchas).

The original 55-item MVP (**E1–E8**) and Tranche-2 **E9 (the seam)** and **E10 (structural & textual
completeness)** are all COMPLETE, adversarially reviewed, and remediated on `Liv`. **NEXT = Epic 11
(Regulatory Computation Verification — the moat).** Read this, then `epics.md` (E11 stories 11.1–11.6),
`epics-tranche2-regulatory.md` (tranche rationale), and `architecture.md`.

---

## 1. How to resume

Re-invoke the `bmad-orchestrator` skill (or continue the loop). Pick up at **Epic 11**. The owner approves
epic-by-epic and is remote — surface genuine design forks via AskUserQuestion; resolve mechanical choices
yourself and note them. Verify every delegated chunk from ground truth (build/test/git), one commit per
story, push to `Liv` after each, adversarial review at the epic boundary.

## 2. Done-state (E1–E10 on `Liv`, pushed)

- **E1–E8** (original MVP): foundation/isolation, ingestion + reference data, PdfPig field extraction,
  the financial validation engine, visual, regulatory/fiscal, reporting/QA, batch+observability.
  End-to-end proven over a real Dummie fixture. See `ORCHESTRATION-HANDOFF-2026-06-18.md` §2.
- **E9 (Compliance Verdict Core & Traceability Seam):** the rule contract every rule is authored against —
  dual legal/tenant verdict, `DofNumeral` (NFR-7), `RuleClassification`, typed range-bounded `Tolerance`
  in an encrypted SQL store, `TenantProfile` resolution, confidence-driven abstain (`MinFieldConfidence`).
  Commits `da72eb49`→`0314045b`.
- **E10 (Structural & Textual Completeness):** §1–28 heading detection + per-page geometry, order + >2 cm
  blank-gap, the `VecTextMatcher` tolerant primitive, 5 verbatim rules (§11/§17/§24/§26/§27), conditional
  §23/§25, §18/§13 completeness. Commits `d00784c8`→`4a9d39bc` (incl. the R1/R2/R3 review remediation).

**Veriqan test footprint, all green, build 0/0 (re-run for exact counts):** Application 115 · Extraction 93
· Validation 359 · Orchestration 36 (e2e scenario still RED — data-alignment carry-forward) · Visual 48 ·
ReferenceData 38 · Reporting 21 · Smoke 4 · Persistence-int (Docker, not re-run this session).

## 3. Building blocks E11 reuses (do NOT reinvent)

- **The rule seam** — `IVecValidationRule` (`01 Core/Veriqan.Application/Validation/IVecValidationRule.cs`):
  `CheckId`, `DofNumeral`, `RuleClassification`, `TechniqueClass Technique`,
  `Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct)`. Findings via
  `RuleFinding.Pass/Fail/InsufficientData` (`01 Core/Veriqan.Domain/Verification/RuleFinding.cs`). Rules are
  Scrutor-discovered in the Validation (and Visual) assemblies — just drop a class in
  `02 Infrastructure/Veriqan.Infrastructure.Validation/Rules/`. The Orchestration registry/coverage-map test
  asserts every rule has a non-empty `DofNumeral` + `Classification`.
- **Tolerances** — `Tolerance(LegalDefault, Min, Max).Resolve(override)` + `ILegalToleranceProvider`
  (`01 Core/Veriqan.Domain/Tolerances/`). Use typed per-rule tolerances for the recompute comparisons; do
  NOT invent a free k/v bag.
- **Abstain on low confidence** — `ConfidenceGuard` + `MinFieldConfidence` (default 0.8). Every recompute
  rule MUST abstain (`InsufficientData`) when a needed input is missing/low-confidence. A misread digit must
  never produce a false "bank non-compliant" — this is a preventive pipeline gate; a false Fail halts a
  bank's billing run (cardinal sin).
- **VerificationContext** (`01 Core/Veriqan.Application/Binding/VerificationContext.cs`): exposes
  `StatementModel`, `Bundle`, `ResolvedProduct`, `Availability`, `TenantProfile`.
- **StatementModel** (`01 Core/Veriqan.Domain/Extraction/StatementModel.cs`): `PeriodSummary`, `Movements`
  (DESGLOSE rows w/ `FieldLocator`), **`Sections`** (`DetectedSection`: number, name, IsPresent,
  IsApplicable, `SectionDetectionStatus`, **`SectionText`**, heading locator), **`SectionGaps`**, per-page
  `Width`/`Height`, `NormalizedFullText`, `FontRuns`, `FiscalBlock`. The single-pass extractor is
  `02 Infrastructure/Veriqan.Infrastructure.Extraction/PdfPigStatementFieldExtractor.cs` — **extend this
  pass for table extraction (Story 11.1); do NOT open the PDF a second time.**
- **Tolerant matching** — `VecTextMatcher` (Domain) for any label/heading matching; never exact-equality.

## 4. Epic 11 plan + the GATE (read carefully)

E11 recomputes the law's mandated calculations from the statement's OWN reported figures and confirms the
printed values reconcile (FR-29..33, NFR-8). **Stories (sequence as written — 11.1 first, it's the
load-bearing prerequisite, then the cheapest/highest-confidence identity 11.2, outward):**

- **11.1 — table-row/cell extraction (THE SPIKE — biggest hidden cost).** Reconstruct §19/§20/§8/§16 grids
  from PdfPig geometry into typed rows of cells (labels + decimal amounts + rates/days) with **per-cell
  confidence**. A table that can't be reconstructed → `InsufficientData`, never guessed rows. Lives in the
  extractor pass → new typed model on `StatementModel`. **Schedule this before any recompute rule.**
- **11.2 — §20 payment-distribution waterfall** (7-col identity; cheapest, highest-confidence — do first
  after 11.1).
- **11.3 — §19 per-row interest** `monto ≈ saldo_base × (tasa/360) × días`; §19 ordinary-rate row must equal
  the §10 tasa.
- **11.4 — §6 payment simulation** via the Acuerdo revolving-balance recursion for pago-mínimo / 2× / 5×
  (references **Banxico Circular 13/2011** for the pago-mínimo method). Abstain if a needed input is
  indeterminable.
- **11.5 — §8 12-month annual-cost indicators** (presence + coherence).
- **11.6 — §16 other credit lines** (conditional; per-row arithmetic + ties to summary).

> **GATE — do this before the recompute rules (11.2–11.6) can be trusted:**
> 1. **Story 11.1 spike** delivers the structured numeric inputs. Without it, 11.2–11.6 have nothing to
>    recompute from. It is a spike, not a one-liner — budget for it.
> 2. **Ground-truth corpus.** The 3 PRP2 Dummie fixtures (`Prisma/Fixtures/PRP2/01|02|03 Dummie VEC*.pdf`)
>    are SYNTHETIC and omit several mandatory sections (§6/§8/§19/§20/§21/§28 were absent in detection). So
>    the recompute rules are only **synthetically testable** until a corpus of real/representative statements
>    (with known-good AND deliberately-broken computed values) is acquired. **Flag this to the owner** — it
>    is a data-acquisition dependency, not a code task. Build the rules + synthetic round-trip tests now;
>    note that production-grade validation is corpus-gated.

E12 (Legal Form & Typography) is gated on E5 (done) and reuses Epic-10 section geometry. E13
(productization) is roadmap-gated on GitHub issue #17 (economic-buyer discovery — human intel).

## 5. Orchestration mechanics + gotchas (hard-won — heed these)

- **Verify from ground truth every story:** `dotnet build` the touched project(s) + `dotnet test` the test
  project + `git status`/`git diff`. Believe those, NOT subagent prose. Subagents have misreported test
  counts and "tool_uses: 1" with real files.
- **`dotnet test` flags:** run `dotnet test <csproj>` plain. Do NOT pass `--nologo` (the MTP runner treats
  it as an unknown arg → "Zero tests ran", exit 5). The repo uses xunit.v3 + Microsoft.Testing.Platform.
- **Parallel subagents must edit DISJOINT files.** In E10 Wave-A, three `dev` agents editing the SAME
  Validation project raced — patched each other's files (missing usings, a truncated brace) and reported
  CONFLICTING counts (328/284/254). Either give each agent its own files, or serialize edits to shared
  files (the extractor / `StatementModel` are the classic shared-write hotspots — give them to ONE agent).
- **Tell subagents:** do NOT `git commit`/`git add`; do NOT touch the `.sln` or `Directory.Packages.props`
  (no new NuGet without approval); build ONLY their project(s); no git worktrees. YOU (orchestrator) commit
  one-per-story (`feat(veriqan #X.Y): …` + a Verification line) and push.
- **Never false-block.** Recompute rules abstain on missing/low-confidence inputs and use typed tolerances;
  never throw for control flow; `Result<T>` everywhere; `CancellationToken` checked early.
- **Adversarial review at the epic boundary** (`plan-completion-reviewer` + a correctness skeptic vs the ACs
  + `LAW-VS-CHECKLIST-GAP-2026-06-17.md` + the Acuerdo PDF). In E10 it caught a systemic whole-doc false-Pass
  and three false-FAILs that synthetic tests missed. Run it, triage → tracker, close in focused remediation,
  then re-review.
- **PdfPig coordinates** are bottom-left origin; PdfSharp is top-left (flip Y when marking PDFs). Watch
  two-column layouts (some fixtures put two section headings on one Y-band — it truncated `SectionText` in
  E10 and caused a false-FAIL until fixed).
- **Docker** (SQL Server 2022) is available; Testcontainers DB tests run (~35s).
- **The owner edits in the SAME local repo** — fetch/reconcile before assuming divergence.

## 6. Open carry-forwards / residuals (all honest, none false-block)

- **E10 residuals:** §9/§10 presence assumes legal print-order (§9/§10 before §27 glossary) — false-Pass
  only on a malformed statement, never false-Fail; §18 `SectionText` can over-extend when §19/§20 are absent
  (synthetic-fixture artifact — generic labels like "CONTACTO" could match foreign text; bounded by §19 on
  real all-section statements); §23/§25 use section/document-level token presence (no per-ROW extraction —
  11.1 may help §23); verbatim similarity threshold 0.82 is a constant (`ResolveThreshold` is the per-tenant
  extension point — Epic-9 tolerance-seam integration deferred).
- **E9 carry-forwards:** persist `LegalBaselineVerdict` for the E13 traceability export; FiscalBlock
  (CL-50..53) extraction-confidence so a garbled QR abstains; CL-46 numeral is a §14/§17/§24 basket (tighten);
  tolerance [Min,Max] ceilings are engineering estimates pending legal review.
- **Reference-data alias (from the e2e run):** extraction yields product `"Tarjeta de Crédito BSSB"` but the
  CSV bundle aliases only `"BSSB"` → binding BLOCKs on real fixtures (the e2e test uses a matching bundle).
- **Extraction gaps (honest InsufficientData):** TotalCargos (CL-44 charge side), CL-26 efectivo field,
  COMPRAS-A-MESES installments (CL-23/40/41), rewards section + fixture (CL-36/37/39), CL-43 page-range
  header, 5.4 catalog image-presence (client image-catalog answer).
- **`Veriqan.Orchestration` co-location** under the Solution-1 `03 Orchestration/` root — a `<Compile Remove>`
  in the Prisma csproj stops the glob; a new file dropped directly in that folder (not the project) is
  silently excluded. Relocating the folder would remove the trap.

## 7. Authoritative artifacts + legal context

`docs/planning-artifacts/`: HANDOFF.md (entry point), epics.md (E1–E13 + FR-1..39, NFR-1..8),
architecture.md (ADRs), epics-tranche2-regulatory.md, LAW-VS-CHECKLIST-GAP-2026-06-17.md,
CERTIFICATION-RESEARCH-2026-06-17.md, ORCHESTRATION-HANDOFF-2026-06-18.md (E1–E10 done-state + gotchas).
**Legal (authoritative):** `docs/legal/regulations/` — CONDUSEF `Acuerdo_estado_de_cuenta.pdf` (the
28-section format + guía de llenado) + SIARA/DGAAC. Cite DOF numerals for traceability (NFR-7); §6
references Banxico Circular 13/2011.

## 8. Tracker / memory

Tracker: E10 stories + R1/R2/R3 remediation + the boundary review are all `completed`. Create E11 tasks
(11.1 spike first; then 11.2–11.6; then a boundary review) when starting. Memory: `veriqan-vec-progress.md`
+ `MEMORY.md` index are current (E1–E10 done, next = E11 with the spike+corpus gate). Update both after each
milestone.
