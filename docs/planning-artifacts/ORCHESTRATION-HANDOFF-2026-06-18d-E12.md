# Veriqan VEC — Continuation Handoff (resume at Epic 12)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Latest commit:** `6c24f882` (all pushed to `origin/Liv`)
**This is the canonical resume point.** Supersedes `ORCHESTRATION-HANDOFF-2026-06-18c-E11.md` (which
remains accurate for the E1–E10 done-state). The original 55-item MVP (**E1–E8**), **E9 (seam)**,
**E10 (structural & textual)**, and **E11 (Regulatory Computation Verification — the moat)** are all
COMPLETE, adversarially reviewed, and remediated on `Liv`. **NEXT = Epic 12 (Legal Form & Typography).**

---

## 1. How to resume
Re-invoke the `bmad-orchestrator` skill. Pick up at **Epic 12** (`epics.md` stories 12.1–12.x). The owner
approves epic-by-epic and is remote — surface genuine design/legal forks via AskUserQuestion; resolve
mechanical choices yourself and note them. Verify every delegated chunk from ground truth (build/test/git),
one commit per story, push to `Liv` after each, adversarial review at the epic boundary.

## 2. What just landed (E11 + the fixtures/PRP reorg), commits `298a00af`→`6c24f882`
- **Fixtures/PRP SRP reorg** (`298a00af`→`734b63b6`): the owner's half-done "Rearranged the PRP and Fixtures
  folders" was finished. **`Prisma/Fixtures/`** = test data only (csproj-consumed: PRP1, PRP1_Degraded(_v*),
  PRP1_Enhanced(_*), PRP2 Dummie PDFs+checklists, pristine_baseline_ocr.json). **`Prisma/PRP/PRP2/`** = Veriqan
  specs+generators (PRP.md, architecture.md, stories/, python/, reference-data/). **`Prisma/PRP/PRP1/research/`**
  = Banamex OCR ML research outputs. ~10 csprojs in BOTH solutions consume `Prisma/Fixtures/…`; nothing
  referenced was moved (one ConsoleApp python ref repointed to `PRP/PRP2`). Verified: Ocr.Pipeline + Teseract +
  Veriqan.Extraction.Tests build 0/0.
- **E11.1 spike** (`f93fc8f0`): single-pass PdfPig table extraction → `StatementModel.FinancialTables`
  (**5 entries §8/§19/§20/§16/§6**). New Domain types `FinancialTable` / `TableRow` /
  `TableCell{RawText, ParsedValue, CellKind, Confidence, Locator}` / `CellKind` / `TableExtractionStatus`.
  Unreconstructable table → SectionNotFound/Indeterminate, never guessed rows. §20 `=` separator dropped so
  all 7 semantic columns map (incl. Saldo a favor[6]).
- **5 recompute rules** (`745e2a3a`, `fb0a3099`, `64c385a2`, + §8/§16): **§20** waterfall, **§19** per-row
  interest + §10 cross-check, **§6** Banxico-13/2011 recursion, **§8** indicators, **§16** per-column. All
  `LAW-§N-…` CheckIds, Deterministic, **TenantTightenableOnly** (NOT BaselineLocked — the DofNumeral coverage
  test forbids tolerance-bearing BaselineLocked rules). Tolerances pre-registered in BOTH
  `DefaultLegalToleranceProvider` + `LegalBaselineSeeder` (they must match) as CurrencyMxn.
- **Adversarial review remediation** (`7fa7007c`): §8 SectionNotFound→InsufficientData (heuristic detection
  miss must not Fail); §8 Empty cell only Fails when Confidence≥threshold; §19 rate-scale from `%`/`CellKind.Rate`
  not the `>1.5` magnitude heuristic; ALL rules guard `ILegalToleranceProvider.Has()`→InsufficientData before
  `For()` (which throws on a missing key).
- **Owner rulings, then built full** (`bd35aa9d`/`#11.6b`, `6c24f882`/`#11.4b`): §16 full per-column rule;
  §6 table extractor added.

**Green footprint (re-run for exact counts):** Validation **468**, Extraction **130**, Orchestration **36**
(engine over all 40 rules + e2e), Application/Visual/ReferenceData/Reporting/Smoke unchanged from E10.

## 3. E11 carry-forwards (all corpus-gated, abstain-safe — none false-block)
- **NO real CONDUSEF-statement corpus.** The 3 PRP2 `Dummie VEC` fixtures are SYNTHETIC and internally
  inconsistent (e.g. §20 jul_ago components sum 61,033.35 vs pagos 67,796.35, Δ 6,763). So the recompute rules
  are only **synthetically validated**. **Acquiring a corpus (known-good + deliberately-broken computed values)
  is the single highest-value next step for E11 production credibility** — it would calibrate the §6/§16
  extractor geometry, settle the §20 sign, and move validation from synthetic to ground-truth.
- **§20 saldo-a-favor SIGN — owner-deferred until corpus.** Code/AC use `pagos = … + IVA − saldo a favor`. A
  reviewer reading Acuerdo §20 incisos (a–g) ("Fracción del monto del 'Pagos y abonos' destinado a…") argues all
  7 columns partition the payment ⇒ `+ saldo a favor`. Latent (saldo=0 in fixtures). **Do not flip without a
  real non-zero-saldo statement.** When flipping: change `Section20PaymentDistributionRule` `− Values[6]`→`+`
  AND correct `epics.md:798`.
- **§6 extractor X-ranges UNCALIBRATED** (no §6 PDF): `Sec6MonthsX*`/`Sec6InterestX*` in
  `PdfPigStatementFieldExtractor` are spec-derived estimates; §6 is detected on fixtures but month cells parse
  null → low-conf → the §6 rule abstains. Recalibrate when a §6 statement is in the corpus.
- **§16 column-mapping UNCALIBRATED** (no §16 fixture): the rule assumes the Acuerdo §16 9-column order
  (SaldoPendiente[3]/Intereses[4]/IVA[5]/Tasa[8]); a mis-map → InsufficientData via the <9-cells / low-conf
  guards, never a false-Fail.
- **§8** Fail-on-confident-omission depends on §8 heading detection; SectionNotFound now abstains (safe).

## 4. Epic 12 plan (Legal Form & Typography — gated on E5 = done)
E12 verifies the document's *form* obeys the law (FR-36, FR-37), reusing E10 section geometry + E5 PdfPig/render
infra. Stories in `epics.md` §12: **12.1** typography point-size floor (body ≥8pt Arial-equiv; *fecha límite de
pago* ≥10pt; account for CTM scale; abstain when rendered size indeterminable). **12.2** mandated-bold fields
(~10 legally-bold fields; font-name/weight heuristic; **abstain when weight indeterminate — never Fail**, esp.
subset-embedded/mangled font names). **12.3** advertising placement. Keep separate from the client Aptos brand
rule (CL-35, a tenant overlay). Same cardinal rule: a preventive gate never false-Fails — abstain on rendering
uncertainty. PdfPig coords are bottom-left origin; watch two-column bands (the recurring E10/E11 gotcha).

E13 (productization) is roadmap-gated on GitHub issue #17 (economic-buyer discovery — human intel).

## 5. Orchestration mechanics + gotchas (heed these)
- **Verify from ground truth every story** (`dotnet build`/`dotnet test` the touched project + `git status`/diff);
  believe those, NOT subagent prose (subagents have misreported counts + "tool_uses: 1" with real files).
- `dotnet test <csproj>` plain — do NOT pass `--nologo` (MTP treats it as unknown → "Zero tests ran", exit 5).
- **Parallel `dev` agents on the SAME project RACE** (relived again here). This epic SERIALIZED the rule agents
  AND pre-committed the shared tolerance infra (`DefaultLegalToleranceProvider` + `LegalBaselineSeeder`) so each
  rule agent touched only its own disjoint rule+test files. The **extractor + `StatementModel` are the shared-write
  hotspots — give them to ONE agent.**
- **Tell subagents:** no `git commit`/`add`; no `.sln`/`Directory.Packages.props`/NuGet; no worktrees; build ONLY
  their project(s). YOU commit one-per-story (`feat(veriqan #X.Y): …` + a Verification line) and push to `Liv`.
- **DofNumeral coverage test gotcha:** any tolerance-bearing rule MUST be `TenantTightenableOnly`/`TenantOverridable`,
  never `BaselineLocked` (`DofNumeralRegistryTests.AllToleranceBearingRules_AreNotBaselineLocked`).
- **The E: filesystem is SLOW** (a bare `ls` can time out at 2 min; builds 2–4 min). Prefer `git ls-files` over
  recursive `find`/`ls`. Be patient with builds; don't retry on slowness.
- **The owner edits in the SAME local repo** — `git status`/fetch and reconcile before assuming divergence (this
  session began mid-reorg by the owner).

## 6. Authoritative artifacts + legal context
`docs/planning-artifacts/`: HANDOFF.md, epics.md (E1–E13, FR-1..39, NFR-1..8), architecture.md,
epics-tranche2-regulatory.md, LAW-VS-CHECKLIST-GAP-2026-06-17.md, ORCHESTRATION-HANDOFF-2026-06-18c-E11.md
(E1–E10 detail). **Legal (authoritative):** `docs/legal/regulations/` — CONDUSEF `Acuerdo_estado_de_cuenta.pdf`
(28-section format + guía de llenado) + SIARA/DGAAC. Cite DOF numerals (NFR-7). Memory: `veriqan-vec-progress.md`
+ `MEMORY.md` are current (E1–E11 done, next = E12, corpus = top priority).
