# Veriqan VEC — Continuation Handoff (Epic 12 DONE; next = Epic 13, gated)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Latest commit:** `906f303b` (all pushed to `origin/Liv`)
**This is the canonical resume point.** Supersedes `ORCHESTRATION-HANDOFF-2026-06-18d-E12.md` (which
remains accurate for the E1–E11 done-state). The original 55-item MVP (**E1–E8**), **E9 (seam)**,
**E10 (structural & textual)**, **E11 (computation — the moat)**, and now **E12 (Legal Form &
Typography)** are all COMPLETE, adversarially reviewed, and remediated on `Liv`.

**NEXT = Epic 13 (Multi-Tenant Productization & Pipeline-Gate) — ROADMAP-GATED.** Do NOT build E13
until the economic buyer is identified (GitHub issue #17, business/human discovery). The seam (E9) was
built compatibly so E13 can be executed when the gate opens. **With E12 done, the entire technical
build roadmap for Veriqan is complete; the only remaining work is corpus acquisition (below) and the
gated E13.**

---

## 1. How to resume
Re-invoke `bmad-orchestrator`. There is no ungated build epic left. The two highest-value moves are:
1. **Acquire a real CONDUSEF-statement corpus** (see §3) — this unblocks calibration of E11 + E12 and is
   the single biggest lever on production credibility.
2. **E13** only once issue #17 (economic buyer) resolves. Its stories (13.1 embeddable pipeline-gate,
   13.2 per-tenant onboarding-as-config, 13.3 traceability-matrix export, 13.4 regulatory-version
   provenance) are specified in `epics.md` §13.
The owner approves epic-by-epic and is remote — surface genuine design/legal forks via AskUserQuestion;
resolve mechanical choices yourself. Verify every chunk from ground truth, one commit per story, push to `Liv`.

## 2. What just landed — Epic 12, commits `c2ed785d`→`906f303b` (8 commits, all pushed)
Owner ruling for E12: **"build full now, abstain-safe"** (same posture as E11 §6/§16). All rules are
`internal sealed IVecValidationRule` in **`Veriqan.Infrastructure.Visual/Rules/`**, auto-registered by
Scrutor, **BaselineLocked + Deterministic, no tolerance registered** (so the DofNumeral-coverage /
tolerance-vs-BaselineLocked registry tests stay green). CARDINAL RULE honored throughout: a preventive
gate must NEVER false-Fail — abstain (`InsufficientData`) on any rendering/measurement uncertainty.

- **`#12.A` (`c2ed785d`) — extraction primitive.** New Domain `TextTypographySample(Text, PointSize,
  FontName, IsBold, PageNumber, Locator)` + `StatementModel.TypographySamples` + `TypographyExtractionStatus`.
  Populated by `PdfPigStatementFieldExtractor.ExtractTypographySamples` via `page.GetWords()` using the
  **CTM-accounted `letter.PointSize`**. Raw `FontName` retained (subset prefix + style suffix) so rules
  can judge weight-indeterminacy. (Extractor + StatementModel are the shared-write hotspots — ONE agent
  owned them, per the recurring race gotcha.)
- **`#12.1` (`b0cdb36b`, remediated in `5e27e2c3`) — `LAW-TYPO-MINSIZE`.** Body ≥8pt floor +
  *fecha límite de pago* ≥10pt. Real-word filter (len≥2), 0.25pt epsilon. Fecha located via the
  extracted `PeriodSummary.PaymentDueDate` locator (proximity join, 5pt Y-band + X-column window) or a
  phrase scan. **Abstains (InsufficientData) — never false-Passes — when no real-word body samples OR the
  fecha label can't be located** (does not hide an unverified ≥10pt floor).
- **`#12.2` (`cba6f159`, remediated in `5e27e2c3`) — `LAW-TYPO-BOLD`.** ~9 mandated-negrillas fields
  proximity-joined from their `PeriodSummary` `ExtractedField` locators. Tri-state `ClassifyWeight`:
  **Bold** (name contains bold/black/heavy/semibold) / **NotBold** (only on a delimiter-bounded explicit
  non-bold token like `-Regular`/`-Light`) / **Indeterminate** (everything else, incl. a bare family
  name like `"Aptos"` — synthetic-bold can't be ruled out from the name). Coverage quorum: Pass requires
  ≥3 located-bold fields and zero NotBold, else InsufficientData. Added `InternalsVisibleTo` (repo pattern).
- **`#12.3` (`7e6582a0`, remediated in `c7edbe5f`) — `LAW-ADS-PLACEMENT`.** §12 over-length + advertising
  placement over Epic-10 section geometry. §12 length: legal cap **700** chars, but only **Fails beyond
  805** (700×1.15 extraction-uncertainty margin — E10 SectionText can over-extend in two-column layouts).
  Advertising: a pruned **7-phrase** promotional allowlist (normalized via `VecTextNormalizer`), scanned
  in §12 and all non-permitted sections (permitted free zones = §21/§28). Legit terms (meses sin
  intereses, CAT, tasa…) never flagged.
- **`#12.4` (`189151c6`, remediated in `906f303b`) — `LAW-SEC-SIZECAP`.** §17 > ¼ page, §21/§28 > ⅓ page.
  Section vertical extent = gap from the target heading to the **immediate physically-below present
  heading on the same page** (largest `Bottom` < target `Bottom`, **no section-number filter** — matches
  the extractor's reading-order boundary), ÷ page Height, 0.02 epsilon. Cross-page / null geometry / zero
  page height / no same-page successor → that section abstains; all abstained → InsufficientData.

**Adversarial review (2-skeptic, epic boundary) — what it caught + fixed (3 remediation commits):**
- **`5e27e2c3` (12.1+12.2):** band tolerance was 3pt < the extractor's 5pt and Y-only (cross-column in the
  two-column fixtures) → aligned to 5pt + added X-column proximity; 12.1 verdict no longer false-Passes an
  unverified fecha floor; 12.2 `ClassifyWeight` no longer false-Fails synthetic-bold (bare family →
  Indeterminate) + coverage quorum added.
- **`c7edbe5f` (12.3):** markers `SOLICITA TU`/`ADQUIERE TU`/`TASA PREFERENCIAL` substring-collided with
  legit mandated text → pruned; §12 hard 700 cap → +15% margin (805).
- **`906f303b` (12.4):** **both reviewers independently** caught that the successor was chosen by lowest
  section *number*, not physical adjacency → false-Fail (inflated extent) AND false-Pass (under-measure)
  in two-column layouts → switched to positional selection + added two-column regression tests.

**Green footprint (re-run for exact counts):** Visual **150**, Extraction **133**, Validation **468**,
Application **115**, Orchestration **36** (registry over all 40+ rules + e2e). Build 0/0.

## 3. E12 carry-forwards (all honest, corpus-gated, abstain-safe — none false-block)
- **NO real CONDUSEF-statement corpus** — the 3 PRP2 `Dummie VEC` fixtures are SYNTHETIC. So the E12
  typography/geometry rules are only **synthetically validated** and their thresholds are spec/AC-derived
  estimates, not calibrated: the **8pt/10pt point-size floors**, the **§12 700-char cap + 15% margin**,
  the **¼/⅓ page section caps**, and the **7-phrase advertising allowlist**. **Acquiring a corpus
  (known-good + deliberately-broken FORM/typography defects) remains the single highest-value next step**
  — it also still gates E11's §6/§16 extractor geometry and the §20 saldo-a-favor sign (see prior handoff).
- **12.2 bold is font-NAME-based only.** Synthetic-bold (bold faked via glyph stroke width while the base
  font name stays e.g. `"Aptos"`) is NOT detectable from the name → such fields are `Indeterminate`
  (abstain). True stroke-width bold detection needs per-glyph data (carry-forward).
- **12.2 last-page fiscal block** (an AC-named mandated-bold field) has **no extractable locator** → it is
  documented-as-not-assessed in the rule's Observed output (abstain, never silent).
- **12.3 página-cero** is named in the AC as a permitted advertising zone but is **not modeled** anywhere
  (no §0 concept in the codebase) → documented gap; currently inert (cannot false-Fail).
- **12.1 fecha-límite sub-check is best-effort**: if the label can't be located by the PaymentDueDate
  locator or the phrase scan, the whole rule abstains (InsufficientData) rather than Pass — honest, not a
  false-Pass, but means a genuinely <10pt fecha on a statement we can't locate the label on is not caught.
- **12.1 numeral** now cites `"Acuerdo Anexo / Guía de llenado — Tipografía (puntaje mínimo)"` (the AC
  asked for the *guía* numeral; the exact DOF guía sub-numeral can be tightened with the legal text).

## 4. Orchestration mechanics + gotchas (heed these — relived again in E12)
- **Verify from ground truth every story** (`dotnet build`/`dotnet test` the touched project + `git status`/diff);
  believe those, NOT subagent prose (subagents misreport counts).
- `dotnet test <csproj>` plain — do NOT pass `--nologo` (MTP → "Zero tests ran", exit 5).
- **Parallel `dev` agents on the SAME project RACE.** E12 SERIALIZED every story + remediation agent, and
  gave the extractor + `StatementModel` (shared-write hotspots) to ONE agent. Each rule lives in its own
  new file; remediations edited only their own rule+test files. This avoided the race entirely.
- **The cross-assembly registry test** `DofNumeralRegistryTests` (in `Veriqan.Orchestration.Tests`) loads
  BOTH Validation + Visual and asserts: rule count `>=35` (now 39), unique CheckIds, non-empty DofNumeral,
  defined Classification, and **tolerance-bearing ⇒ NOT BaselineLocked**. Any new rule must satisfy these.
  Run it after adding/changing any rule.
- **Changed-test expectations are where made-to-pass bugs hide** — when a remediation flips an existing
  test's expectation, scrutinize the new expectation against the cardinal rule, don't just accept green.
- **The E: filesystem is SLOW** (builds 2–4 min; a bare `ls` can time out). Prefer `git ls-files`. Be patient.
- **The owner edits in the SAME local repo** — `git status`/fetch and reconcile before assuming divergence.

## 5. Authoritative artifacts + legal context
`docs/planning-artifacts/`: HANDOFF.md, epics.md (E1–E13, FR-1..39, NFR-1..8), architecture.md,
epics-tranche2-regulatory.md, LAW-VS-CHECKLIST-GAP-2026-06-17.md, and the prior handoffs
`ORCHESTRATION-HANDOFF-2026-06-18d-E12.md` (E1–E11 detail) / `-18c-E11.md` (E1–E10 detail).
**Legal (authoritative):** `docs/legal/regulations/` — CONDUSEF `Acuerdo_estado_de_cuenta.pdf`
(28-section format + guía de llenado) + SIARA/DGAAC. Cite DOF numerals (NFR-7). Memory:
`veriqan-vec-progress.md` + `MEMORY.md` are current (E1–E12 done, next = E13 gated, corpus = top priority).
