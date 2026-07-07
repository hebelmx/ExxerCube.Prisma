# HANDOFF — Veriqan progressive fallback chain implementation (next orchestrator)

**For:** the next agent running `/bmad-orchestrator` on branch `Liv`.
**Date:** 2026-07-05. **Status:** design complete + pushed. **E1 DONE + reviewed + pushed (4 commits).**

---

## ⏩⏩⏩⏩⏩⏩⏩ LATEST (2026-07-07 session 3) — **E6.S6.2.5 (populated DESGLOSE movements + totals) DONE + reviewed + pushed `03197966` → NEXT = S6.2.6 (batch + CI)**

**Resumed a prior session's uncommitted-but-complete S6.2.5 work, verified it from ground truth, stripped byte-churn, adversarial-reviewed it, fixed one honesty defect, and shipped it on `Liv`.** The prior session had authored all of S6.2.5 (generator `_write_s6211_desglose` + `s6211-desglose.pdf`/`.manifest.json` + golden `[Fact]` + csproj includes) but never verified/reviewed/committed — sitting dirty. It had also run the FULL generator profile, byte-churning all 12 committed s6211/s622 fixtures (PDF-container non-determinism; the generator diff is purely additive so their content is unchanged). Verified from ground truth: build 0/0; `Veriqan.Infrastructure.Extraction.Tests` **279/279** (278 + 1 new `SyntheticGolden_S6211Desglose` golden round-trip). Reverted the 12 churned fixtures and re-proved 279/279 → churn was content-neutral. Committed only the real payload (desglose PDF+manifest + additive generator + golden fact + csproj); desglose regenerated via a one-off `_write_s6211_desglose(OUTPUT_DIR)` call (NOT `--profile all`) to keep the other fixtures byte-untouched. No production code touched.

**What S6.2.5 delivers (`03197966`):** page 1 reuses `PAGE1_TOKENS` verbatim (unchanged canary — 14 baseline fields still `Extracted` to baseline values, byte-identical to `s6211-baseline.manifest.json`); page 2 adds a populated "DESGLOSE DE MOVIMIENTOS DEL PERIODO" table (3 data rows + Total cargos + Total abonos summary rows). **Closes the `TotalCargos`/`TotalAbonos` gap** every prior s6211 manifest deferred as `NotExtracted` — both now `Extracted` and tie to the exact RESUMEN figures the baseline's CL-21/CL-22 arithmeticChecks assert (32,446.69 = CargosRegulares+CargosMeses = PagoParaNoGenerarIntereses; 67,796.35 = PagosYAbonos = AdeudoPeriodoAnterior). Coordinates calibrated to the REAL extractor: `DesgloseBandTolerance=4pt` → rows 14pt apart; sign at Left 425 (in [423,480] but <436, so the amount filter `Left>=436` ignores it) + amount at Left 485 (>=436, so the sign filter [423,480] ignores it) → no column collision; Total rows are sign-less with `Total`→`cargos`/`abonos` adjacency.

**GROUND-TRUTH CATCH (adversarial review, 1 skeptic, 6 angles — the point of verifying):** the credit-row sign was authored as `−` U+2212 MINUS SIGN, but the base-14 `helv` font has **no U+2212 glyph** → PyMuPDF silently substitutes `·` U+00B7 MIDDLE DOT, which `IsSignToken` (`PdfPigStatementFieldExtractor.cs:1969` accepts only `+`/`-`/`−`U+2212) **rejects** → the real extractor DROPS the PAGO RECIBIDO credit movement, making the god's-eye `movements[]` array **irreproducible** (2-of-3 parse). The golden `[Fact]` passed anyway (it asserts only the sign-less totals + canary), so this was a LATENT TRAP for any future movements-level assertion, not a live failure. **Fixed to ASCII `-` (U+002D — in WinAnsi, survives `helv`, accepted by `IsSignToken`/`IsCreditToken` via `SignCreditAscii`)** + corrected two now-false generator comments. Verified the emitted glyph is now `0x2d`. **LESSON reaffirmed: base-14 font glyph coverage is part of the ground truth — a Unicode codepoint you place is NOT necessarily the codepoint the extractor sees; verify the emitted glyph, not your source literal.**

### 🎯 NEXT SESSION — E6.S6.2.6 (batch runner + CI), design §10.6
Batch/regen runner + CI gate for the synthetic corpus. ⚠️ **Gate on WORD-GEOMETRY (PdfPig `(Left,Bottom,Right,Text)` per token) NOT PDF bytes** — the PDF container is non-deterministic across PyMuPDF runs (this session's churn is the proof), so a byte-diff CI check would false-positive every regen. Manifests ARE byte-deterministic (fixed seed + hardcoded `generatedAtUtc`), so those can be byte-gated. **Forward item from S6.2.5:** the `movements[]` array is now honest (reproducible) but NOT asserted — the golden loader (`AssertGoldenRoundTripAsync`) only reads the `fields` map, and `StatementModelFieldAccessors.Map` has no `movements` accessor. Wiring a movements-level assertion is a model-widening slice (needs the accessor) — decide in S6.2.6+ whether it's worth it or the totals-level proof suffices. Verify the full pipeline; calibrate from PdfPig coords.

---

## ⏩⏩⏩⏩⏩⏩ (2026-07-07 session 2) — **E6.S6.2.4 (seeded variance + tolerance edges) DONE + reviewed + pushed `e1a67c59`**

**Orchestrated S6.2.4 recon-first + shipped on `Liv`.** Verified from ground truth: build 0/0; `Veriqan.Infrastructure.Extraction.Tests` **278/278** (273 + 3 variance theory cases + 2 edge facts); demo E2E `VecChecklistDemoE2ETests` **5/5** (no regression). No production code touched.

**Recon FIRST (E2.3-avoidance), and it paid off twice.** An `Explore` recon of `PdfPigStatementFieldExtractor.cs` established the real constants: **`YBandTolerance=5.0pt` (:113)**; left/right split X=280; header Y-window 530–700; TASA scan ceiling Bottom≤350; and the pivotal finding — **positional labels are matched by EXACT `OrdinalIgnoreCase` equality, NOT fuzzy, NOT accent-folded (`MatchesLabel` :2478)**. This REFINES the design's "phrasing-alias list" idea: a label phrasing/accent change doesn't test robustness, it BREAKS the field — so it belongs in the EDGE variants, not the in-tolerance ones.

**S6.2.4 shipped (`e1a67c59`):** `synth_gen.py --profile dummievec` now also emits (additive; committed baseline/defect fixtures untouched — generated via a one-off `_write_s6211_variance(OUTPUT_DIR)` call, not `--profile all`, to avoid byte-churn):
- **`s6211-var-{a,b,c}`** — 3 seeded personas re-emit the baseline layout with NOVEL arithmetic-consistent values (product/holder/amounts/percents, all differing from baseline AND each other → non-vacuous) + a **RIGID whole-page Y-shift** (`|shift|≤3pt`). Rigid, NOT independent per-band jitter, is deliberate + documented: the baseline interleaves left/right rows ~2pt apart and packs same-column rows ~10.8pt apart, so independent ±3pt jitter could merge previously-separate bands (topology change = the trap). Every field still resolves `Extracted` to the varied value.
- **`s6211-edge-band` / `s6211-edge-label`** — the two edges land on the extractor's **two distinct honest failure modes**: displacing the Adeudo amount +7pt off its label band → **`ExtractedInvalidFormat`** (matched label, no in-band amount — the Epic-5 F1 implied-zero distinction); dropping the accent from the "Crédito" label → **`NotExtracted`** (never-matched label). Manifests are god's-eye (one persona value drives BOTH the printed token AND the declared field). Extraction-only slice; values kept CL-21/CL-22-consistent for a later slice; deterministic (manifests byte-identical across regen).

**GROUND-TRUTH CATCH (the point of verifying):** the initial edge-band manifest asserted `NotExtracted`; the golden round-trip REFUTED it — real behavior is `ExtractedInvalidFormat` (matched-label/missing-amount → implied-zero, per Epic-5 F1). Corrected manifest + generator + test doc to the real behavior. **LESSON reaffirmed: assume nothing about extractor edge behavior — the test is the oracle, not your model of it.**

**Adversarial review (1 skeptic, 8 angles): SOUND, 0 defects.** One latent non-defect noted + guard-commented: the tightest boundary clearance is the RFC band (533.6, only 3.6pt above HeaderYMin=530), so the `|shift|≤3pt` cap is load-bearing — a comment now warns against widening it toward the 5pt tolerance.

### 🎯 NEXT SESSION — E6.S6.2.5 (DESGLOSE movements), design §10.5
Populate a real movements/transactions table (design §5.6 columns) with N rows + totals, closing the `TotalCargos`/`TotalAbonos` gap left `NotExtracted` in every s6211 manifest so far. ⚠️ **DESGLOSE has a TIGHTER band tolerance = 4.0pt (`DesgloseBandTolerance` :1582)** and its own X columns (OpDateXMax 95, ChargeDate 96–157, Desc 145–422, Sign 423–480, Amount min 436 — recon 2026-07-07) — re-verify these from source before placing rows (row spacing must clear 4pt, not 5pt). Then S6.2.6 (batch + CI; gate on word-geometry NOT PDF bytes). Verify the full pipeline; calibrate from PdfPig coords.

---

## ⏩⏩⏩⏩⏩ (2026-07-07) — **E6.S6.2.3 (defect injection + VERDICT-LEVEL proof) DONE + reviewed + pushed `cadd086e`**

**Resumed a prior session's uncommitted-but-complete S6.2.3 work, verified it from ground truth, adversarial-reviewed it, fixed one honesty defect, and shipped it on `Liv`.** The prior session had authored all of S6.2.3 (generator + 4 defect fixtures + golden facts + the verdict-level E2E) but never verified/reviewed/committed — it was sitting dirty in the tree.

**Verified from ground truth (not summaries):** build 0/0; `Veriqan.Infrastructure.Extraction.Tests` **273/273** (269 + 4 defect-variant golden facts); `Veriqan.Orchestration.Tests` **128/128** (122 + 6 new verdict-level, incl. the demo E2E `VecChecklistDemoE2ETests` regression gate — no regression).

**S6.2.3 shipped (`cadd086e`):**
- `synth_gen.py` emits `s6211-{math,font,scanned,abstain}.{pdf,manifest.json}` from the baseline layout (design §8). **math** = printed Pago **+$11.00** (> $0.50 legal tolerance — respects the Epic-5 "defect must exceed tolerance" lesson) → CL-21/CL-22 fire on a genuine over-tolerance defect; **font** = injected Courier run (baseline all-Helvetica) → CL-35; **scanned** = image-only raster (0 extractable fields); **abstain** = TASA/CAT block omitted → Tasa/Cat honestly `NotExtracted`.
- Manifests reintroduce `arithmeticChecks` (design §6.2) as **declared god's-eye literals** (not derived from tool output). `SyntheticGoldManifest` gains `ArithmeticChecks`.
- `SyntheticDefectVerdictE2ETests` (Orchestration) drives the **REAL `VerificationPipeline`**: CL-21/CL-22 RED on math + PASS on baseline (non-vacuous — Pass gated on `ShouldNotContain` in BOTH Fail and InsufficientData); CL-35 on font AND **not** on baseline (discriminator locked); scanned halts on the specific `BlockReason.InsufficientExtractionCoverage`.
- **No production code touched** — test project + fixtures + generator script only.

**Adversarial review (1 skeptic, 8 angles): SOUND TO COMMIT.** Found + FIXED one **MEDIUM honesty defect** pre-commit: the scanned manifest/comments/test misattributed the ExtractionGap to the **text-layer floor** (Stage 2c, `MinTextLayerWordCount`), but the **extraction-coverage floor** (Stage 2b, `MinExtractionCoverageCount`, default 10) fires *first* on a 0-field raster → `InsufficientExtractionCoverage`. The signal-only assertion papered over the wrong mechanism. Fixed: manifest reason strings + `synth_gen.py` + the test now asserts the exact `BlockReason` (verified empirically — the reason IS `InsufficientExtractionCoverage`). Also closed the LOW one-sided CL-35 test (added baseline-negative assertion). One LOW latent note left un-fixed: the two independent manifest parsers (Extraction.Tests `SyntheticGoldManifest` loader vs the Orchestration test's local `ArithmeticCheckEntry` DTO) disagree on null-`checkId` handling (loader tolerates → `""`, DTO throws) — not triggered (only `knownFixtureDefects` carries a null checkId, which the Orchestration parser never reads).

⚠️ **PRE-EXISTING UNRELATED DIRT left in the working tree (NOT mine, NOT S6.2.3, deliberately NOT committed):** `.gitignore` was gutted from 711 lines to just `secrets/` on 2026-07-04 (un-ignores build dirs + `.env`/`.env.veriqan` secrets), `secrets/.gitignore` deleted, `docker-compose.staging.override.yml` (Ollama host-gateway, from the LLM demo), `docs/qa/calibration/calibration-report.md` (a test side-effect — Epic-3 memory says revert before commit). **The gutted `.gitignore` is destructive and should be restored** — flag to owner; I did not touch it (not my change, and restoring/deleting is owner-gated).

### 🎯 NEXT SESSION — E6.S6.2.4 (variance), design §10
Parameterize layout/phrasing/position/values across the s6211 baseline to prove the extractor's robustness isn't overfit to one token table (design §"Variance"). Keep the god's-eye manifest per variant. Then S6.2.5 (DESGLOSE movements table) and S6.2.6 (batch generation + CI gate — remember: gate on **word-geometry**, NOT PDF bytes; PyMuPDF metadata churns bytes on every regen). Verify EVERY slice against the full verdict pipeline (Extraction + Orchestration demo E2E), calibrate from PdfPig coords.

---

## ⏩⏩⏩⏩ (2026-07-06 session 3) — **E6.S6.2.2 (real-Banamex left-column) DONE, pushed `03e87223`**

**Orchestrated S6.2.2 recon-first + shipped the slice on `Liv`.** Verified from ground truth
(build 0/0, `Veriqan.Infrastructure.Extraction.Tests` **269/269**, 0 skipped — 268 baseline + 1 new s622 `[Fact]`).

**Recon-first discipline honored (the whole point of the slice):** ran the PdfPig-coords recon + PyMuPDF
token-frag spike against the real `good.pdf` 612×792 **left-column** band geometry BEFORE any generator
code → **GO** (I independently re-verified every load-bearing claim from source, not the agent summary).
Key verified facts:
- `good.pdf` = **612×792, 9 pages**. Its left-column RESUMEN rows (`label Left≈25.5, amount Left≈207.7`,
  combined `$` token) already round-trip today via `ExtractResumenField` **pass-2** (`labelMinX:0/labelMaxX:280/amtMaxX:280`).
- **HARD CONSTRAINT:** `ExtractNivelDeUsoField` hardcodes `if (Left<280) continue` (`:1507`) — NIVEL DE USO +
  Saldo deudor total + Crédito disponible have **NO left-column pass**, so even in the "left-column" profile
  they MUST be placed **right-column** (label L≈303, split-`$` L≈460) exactly as real good.pdf does. Placing
  them left-column → silent Missing.
- Token-fragmentation (the E2.3 killer) does **not** reproduce at 612×792 — one `insert_text` per token → one
  un-fragmented PdfPig word (percents match `^\d+(?:\.\d+)?%$`, split-dollar `$`+digits as 2 tokens).

**S6.2.2 shipped (`03e87223`):** `scripts/veriqan-corpus/synth_gen.py` now has `--profile {dummievec,realbanamex,all}`
(default `all`); s6211 token table/geometry **unchanged** (word-geometry byte-identical — verified). New fixture
`Prisma/Fixtures/PRP2/synthetic/s622-realbanamex-baseline.{pdf,manifest.json}` (612×792, `sourceProvenance:synthetic`,
`defect:null`, **no arithmeticChecks** per owner extraction-only ruling; RESUMEN/Saldo/Crédito identities kept
arithmetic-consistent so S6.2.3 can turn CL-21 on without re-authoring — new value persona Tasa 0.1975/Cat 0.2610).
`SyntheticGoldenRoundTripTests` refactored to a shared `AssertGoldenRoundTripAsync` helper + two `[Fact]`s
(s6211 unchanged + new s622), reusing `SyntheticGoldManifestLoader` + `StatementModelFieldAccessors` unchanged
(all 14 Extracted fields already had accessors). **No production extractor code touched.**

**Adversarial review DONE (single skeptic, 7 refutation angles, all CONFIRMED-clean):** non-vacuous (every
manifest field asserted or recorded as mismatch, no silent-skip); genuinely exercises the left-column pass-2
(RESUMEN X=25.5 can only match pass-2); NIVEL honestly right-column; manifest is god's-eye (hardcoded literals
in `build_manifest_s622`, not reverse-engineered); word-geometry deterministic; no scope drift; s6211 no regression.
**Verdict: sound to build S6.2.3 on.** ONE latent note (not a defect): re-running the generator dirties PDF
**bytes** (PyMuPDF metadata) though word-geometry is stable → if S6.2.6 ever wires generate-then-`git diff --exit-code`
into CI, gate on word-geometry, NOT bytes.

### 🎯 NEXT SESSION — E6.S6.2.3 (defect injection + verdict-level), design §10 #3
Wire `math`/`font`/`scanned`/`abstain` (design §8) onto the generator output; **(re)introduce** the manifest's
`arithmeticChecks`/`defect` fields (design §6.2 note — the S6.2.2 values are already arithmetic-consistent);
add **verdict-level** tests (does CL-21 actually fire RED on the synthetic `math` variant, end-to-end through
`VerificationPipeline` — NOT just the extractor). This is where the deferred verdict-level proof lands. Verify
against the FULL verdict pipeline (Extraction + Validation + Orchestration `VecChecklistDemoE2ETests`), not just
Extraction units (the P1.4 lesson). Then S6.2.4 (variance), S6.2.5 (DESGLOSE movements), S6.2.6 (batch+CI).

---

## ⏩⏩⏩ (2026-07-06 session 2) — **E6.S6.2 DESIGN + S6.2.1 DONE, pushed `48fe6639`**

**Orchestrated the E6.S6.2 design-first flow + shipped the S6.2.1 slice on `Liv`.** Verified from ground truth
(build 0/0, `Veriqan.Infrastructure.Extraction.Tests` **268/268**, 0 skipped).

**Canonical docs (read these to resume):**
- `docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md` — the settled design (6 forks answered,
  owner rulings + spike result folded in, §0 non-goals, §5 field→X/Y/token placement table, §9 S6.2.1 slice,
  §10 reordered build sequence). **This is the intended-solution doc — do not drift from it.**
- `docs/planning-artifacts/E6-S6.2-extractor-geometry-map.md` — extractor band-constant reference (has a
  page-size correction: Dummie-VEC = 540×780, real good.pdf = 612×792).

**Pivotal finding (verified):** the Dummie-VEC geometry the extractor is calibrated to already resolves
TASA/CAT/RESUMEN/NIVEL-DE-USO GREEN today via a hand-built PowerPoint fixture
(`Prisma/Fixtures/PRP2/01+Dummie+VEC+jul_ago+20252.pdf`, 540×780) —
`PdfPigStatementFieldExtractorPeriodTests.cs:220` + `ResumenTests.cs`. So E6.S6.2 = turn that proof into a
reproducible, manifest-backed generator, not discover geometry.

**Make-or-break spike ran BEFORE build → GO:** PyMuPDF `insert_text` does NOT reproduce the E2.3
token-fragmentation (via PdfPig `GetWords()`: percent tokens round-trip single + regex-matching, Y-flip
`fitz_y=780−Bottom` exact). Keep one `insert_text` per full token string.

**Owner rulings (AskUserQuestion 2026-07-06):** S6.2.1 = **extraction-fidelity only** (no verdict assertion,
NO `arithmeticChecks` — deferred to S6.2.3). Layout: **Dummie-VEC now, real-Banamex 612×792 left-column layout
= the next slice S6.2.2** (ahead of variance/defects).

**S6.2.1 shipped (`48fe6639`):** `scripts/veriqan-corpus/synth_gen.py` (PyMuPDF; `requirements.txt` pins
`pymupdf==1.27.2.2`) → `Prisma/Fixtures/PRP2/synthetic/s6211-baseline.pdf` + `.manifest.json` (god's-eye;
`sourceProvenance`, NOT `provenance` — that collides with `ExtractionProvenance`). `SyntheticGoldenRoundTripTests`
+ `SyntheticGoldManifest`(loader, `Result<T>`) + `StatementModelFieldAccessors`(no-reflection) in
`Veriqan.Infrastructure.Extraction.Tests`. 15 fields resolved first-try, 0 coord tuning; deterministic
(word-geometry byte-identical). Honest gap: the DESIGN got a qa adversarial pass; the built code did not get a
separate fan-out review (green + test-covered → judged disproportionate) — optional quick skeptic next time.

### 🎯 NEXT SESSION — E6.S6.2.2 (real-Banamex layout profile), DESIGN-FORK, do NOT build blind
Add a 2nd generator layout profile targeting the real `good.pdf` **612×792 LEFT-column** geometry (RESUMEN
left-col pass `labelMinX<280`/`amtMaxX≤280`; the synthetic CAN supply the Tasa/CAT the real doc lacks →
exercises the extractor **fallback path** S6.2.1's right-column clone never touched). **RE-RUN the PdfPig-coords
recon + token-frag spike against the left-column band geometry BEFORE writing generator code** (same trap that
killed E2.3). Then S6.2.3 (defect-injection + verdict-level, reintroduce `arithmeticChecks`), S6.2.4 (variance),
S6.2.5 (movements), S6.2.6 (batch+CI) — design §10.

---

## ⏩⏩ LATEST (2026-07-06) — E2.1/E2.2 REVIEWED (clean) + **E2.3 REFUTED by corpus reality → PIVOT to E6.S6.2**

**Session summary (orchestrator):**
1. **E2.1/E2.2 adversarial review DONE + closed out** (commit `4877d438`, pushed). Two-lens skeptics (honesty +
   verdict-preservation) vs the design doc. Verdict = **sound to build on** (E2.1 byte-identical-neutral; rebuild
   path facet-faithful 26+24 members; demo 0/5 verdicts unchanged empirically; all 3 suites green — Extraction
   **267/267** after the new canary, Validation 531/531, Orchestration 122/122). Two latent findings triaged:
   (F1) the "PaymentDueDate has 0 consumers" claim was FALSE — it has **Visual-rule** consumers
   (`TypographyPointSizeFloorRule.cs:193` LAW-TYPO-MINSIZE, `MandatedBoldFieldsRule.cs:444` LAW-TYPO-BOLD,
   `VerificationPipeline.cs:1203`) → corrected + a **canary** now pins abstention on the 5 fixtures
   (`PaymentDueDateFuzzyRecoveryCanaryTests`). (F2) `FuzzyLabelStage` can fabricate a plausible-but-wrong date from
   a cross-column homonym label ("fecha de cargo"→85, "fecha ultimo pago"→82, "fecha de pago minimo"→100 all clear
   threshold-80) → folded into E2.3-if-ever-built as a discriminating-token AC.

2. **E2.3 (TASA/CAT + amounts) REFUTED — do NOT build fuzzy ladders for it.** Two PdfPig-ground-truth scouts across
   all 4 text-bearing demo fixtures proved E2.3-as-fuzzy-recovery has **no genuine win on the available corpus**:
   - 10/18 target fields are **already Extracted** on every fixture → fuzzy fallback = dead code.
   - The 8 always-missing fields are missing because the **VALUE isn't extractable text**, not because of a
     mislabel — so `FuzzyLabelStage` (find missed label → read band) can recover **none** of them:
     - **Tasa** = text-layer defect (no rate digits emitted anywhere; only footer print-shop codes + the CFDI IVA
       16% decoy). Verified across all 4 fixtures. Needs a **generator/fixture fix or OCR/raster**, not fuzzy.
     - **TotalCargos / TotalAbonos** = genuinely absent (no label+value in any text layer).
     - **PagosYAbonos / AdeudoPeriodoAnterior** = print-suppressed $0 rows (a lone "-", NO label) → **E3
       implied-zero policy** (the already-open ladder-exhaustion honesty item #5).
     - **SaldoDeudorTotal** = value not in text (only page-4 glosario prose) → **E3 computed-proxy or abstain**.
     - **PagoMinimoMasMeses** = absent + near-zero consumers → skip.
   - **CAT is out of E2.3 anyway** — the design escalation matrix (line 107) says `Cat = Positional→semantic→LLM`
     (E4), not fuzzy. The handoff task title #8 was looser than the canonical matrix.
   - **Decision #1 (Levenshtein copy) is MOOT** — `VecTextMatcher.NormalizedLevenshteinRatio`
     (`01 Core/Veriqan.Domain/Extraction/VecTextMatcher.cs:223-255`) already exists in Domain, dependency-free.
   - E2.2 infra confirmed **fully reusable** (pure data change: register ladders + stages) IF a real fuzzy win ever
     appears. No amount/decimal parser in `StatementValueParsers` yet (scoped-in when needed). Positional is
     page-1-restricted (`PdfPigStatementFieldExtractor.cs:554-560`); FuzzyLabelStage already scans all pages.

3. **OWNER RE-PLAN (AskUserQuestion 2026-07-06): PIVOT to E6.S6.2** — the demo fixtures are **anonymized REAL
   Banamex** docs (`scripts/veriqan-corpus/{anonymize,enhance}.py`, real sources live OUTSIDE the repo) whose text
   layer genuinely omits Tasa/totals. The root-cause fix is the **greenfield synthetic estado-de-cuenta generator**
   (E6.S6.2, the non-owner-gated escape valve) that emits a **complete text layer + god's-eye manifest**, so
   POSITIONAL extraction lights up CL-10/21/22/24/44 + Section19 with no fuzzy needed and the pipeline can be
   measured against known truth.

### 🎯 NEXT SESSION — E6.S6.2 scope (DESIGN-FIRST — do NOT build blind)
**Goal:** synthetic Banamex-style credit-card statement PDFs with (a) a complete, well-tokenized text layer
containing ALL verdict-gating fields (Tasa%, RESUMEN 7, NIVEL DE USO, DESGLOSE totals, non-suppressed rows,
movements), and (b) a **god's-eye JSON manifest** = source-contained gold (per-field value + expected status incl.
legitimate abstentions + provenance tag `synthetic`), per the design QA "golden corpus per-field triple" and the
[[prisma-domain-3-docs-unreliable]] lesson (gold from the generator manifest, NEVER a delivered file).

**Design forks to settle BEFORE any code (this is why it's design-first, not a blind build):**
1. **Generator tech:** HTML-template→PDF (weasyprint/Playwright — maintainable, excellent text layer; RECOMMENDED)
   vs PyMuPDF programmatic drawing (consistent w/ anonymize.py, painful for rich layout) vs reportlab.
2. **⚠️ Layout fidelity — THE CRITICAL RISK (same trap that killed E2.3):** `PdfPigStatementFieldExtractor`'s
   positional bands are calibrated to the REAL Banamex page-1 coordinate geometry. If the synthetic layout differs,
   positional MISSES fields even though they're present → defeats the pivot. Recommend **mimic the Banamex band
   geometry closely** (measure real `good.pdf` PdfPig coords, replicate X-columns/Y-bands) so the EXISTING extractor
   "just works" — proving end-to-end pipeline, not fixture-specific patching (the PI-1 bar).
3. **Manifest schema** + a C# loader for the eval harness.
4. **Committability:** synthetic = no PII → PDFs+manifests CAN be committed (unlike the anonymized real ones). Put
   under `Prisma/Fixtures/PRP2/synthetic/` (confirm with owner).
5. **Defect injection:** mirror anonymize.py `--inject {math,font,scanned}` so CL-21/CL-35/text-density get
   synthetic known-bad inputs with manifest-tagged expected verdicts.
6. **Variance:** parameterize layout/phrasing/position/values to prove robustness.

**Recommended first slice (S6.2.1):** generate ONE synthetic statement (HTML→PDF) mimicking the Banamex page-1
bands closely enough that the EXISTING `PdfPigStatementFieldExtractor` resolves Tasa + RESUMEN + DESGLOSE totals
positionally, + emit its god's-eye manifest, + a C# golden round-trip test asserting `ExtractFullAsync` resolves
those verdict-gating fields (the ones the real fixtures CAN'T) to the manifest values. That single slice proves the
escape valve end-to-end; variance + defect-injection + batch follow.

**First step:** a short **design pass** (architect + qa party, or an `architect` subagent producing the design doc)
to settle forks 1+2 BEFORE any generator code. Do not build against an unsettled layout-fidelity strategy.

---

## ⏩ RESUME STATE (updated 2026-07-05, after E1)

**Owner ruled (AskUserQuestion):** session scope = **PI-1 slice (E1+E2+E3-stub+E6.S6.2)**;
decision #3 = **new `ExtractedByInference` status** (built). Decision #1 defaulted to the
**recommended** path (FuzzySharp direct + Levenshtein copied to `Veriqan.Domain`) — not yet
owner-ratified, revisit at E2 start. Decisions #2 (embeddings) / #4 (product gate) untouched (E4/E7).

**E1 COMPLETE** — commits `30b81d83` (A: domain vocab), `df8d389b` (B: seam+decorator+DI),
`7a42d1c5` (D: behavior-neutral harness), `36994e3a` (fix: disagreement-gate defect from the
adversarial review). All on `Liv`, pushed. Behavior-neutral **proven** (E1.D harness: 0 field-status
divergence across all 5 demo fixtures) and **honesty-gated** (demo E2E 5/5 unchanged throughout).
Baselines: Extraction.Tests **199/199**, Validation **531/531**, Orchestration **122/122**, builds 0/0.

**Adversarial review of E1 ran** (general-purpose skeptic vs the design doc). Verdict: E1 sound to
build E2 on. One real defect found + **fixed** (`36994e3a`): the disagreement gate anchored on the
discarded stage-1 value, so validator/confidence-triggered recovery always abstained — now only
CREDIBLE candidates count as disagreeing peers. Three forward items tracked (see tasks #4/#5/#7):
- **E2 (task #4):** registering the first non-empty ladder activates the decorator's dormant
  `StatementModel` rebuild path → ADD a rebuild-path facet-preservation test.
- **E3/E7 (task #5):** ladder-exhaustion honesty — a final still-invalid candidate is emitted as
  `Extracted` (false-confident); decide clean-abstain policy WITHOUT breaking `ExtractedInvalidFormat`
  propagation (intersects E7 decision #4).
- **E5 (task #7):** `FieldCandidate` carries no LLM model/hash → orchestrator can't stamp full LLM
  provenance yet; extend it when building the LLM stage.

**E2.1 + E2.2 DONE** (commits `3f600633`, `19bf3071`, on `Liv`, pushed):
- **E2.1** = neutral plumbing: FuzzySharp pkgref (pinned 2.0.2 in `Prisma/Code/Src/CSharp/Directory.Packages.props`);
  extracted the Spanish-date parser family out of the 5654-line `PdfPigStatementFieldExtractor` into
  `internal static StatementValueParsers` (one parse-truth for stages to reuse); `IFieldStageProvider`
  seam injected into the orchestrator (consulted only when `higherStages`==null → tests still inject
  directly). Behavior-neutral, E1.D harness 0-divergence held.
- **E2.2** = first BEHAVIOR-CHANGING slice: `FuzzyLabelStage<DateOnly>` recovers **PaymentDueDate** when
  positional misses it (StatusGate trigger = only when positional NotExtracted). Stage SELF-ABSTAINS on
  no-match/unparseable/implausible (honesty; doesn't rely on the open exhaustion policy). `DefaultFieldStageProvider`
  now registered. **Verdict-preservation held: 0/5 demo verdicts changed** — the real Banamex layout never
  prints this field on p1 (fuzzy abstains on all 5). ⚠️ **CORRECTED by the E2.1/E2.2 adversarial review
  (2026-07-06):** PaymentDueDate is NOT consumer-free. The Validation *project* has no rule, but the **Visual**
  rule assembly does (wired into `VerificationPipeline`): `LAW-TYPO-MINSIZE` (`TypographyPointSizeFloorRule.cs:193`
  branches strategy on `Status==Extracted`), `LAW-TYPO-BOLD` (`MandatedBoldFieldsRule.cs:444`), and the extracted-
  field tally (`VerificationPipeline.cs:1203`). So 0/5 holds ONLY because fuzzy empirically abstains on this corpus,
  NOT because a recovery is verdict-inert. A canary test now pins that abstention (task #5).
  Rebuild-path facet test added (a non-empty ladder now always activates the decorator's StatementModel
  rebuild — verified facet-preserving). Baselines now: **Extraction 262/262**, Validation 531/531,
  Orchestration 122/122 (demo 5/5), full solution 0/0.

⚠️ **KEY GOTCHAS for the next pass:**
- FuzzySharp `Fuzz.PartialRatio` (0–100), threshold 80 in `FuzzyLabelStage.DefaultScoreThreshold`.
  Accent-fold via `AccentFolding` (FormD + strip NonSpacingMark).
- A non-empty ladder makes the orchestrator ALWAYS reconstruct that field (value-identical when not
  escalating) → the decorator's rebuild path is now live on every doc with a PeriodSummary. Verified safe,
  but any new init-only `StatementModel` facet MUST be added to the rebuild in `EscalatingStatementFieldExtractor`.
- **Ladder-exhaustion honesty is still OPEN (task #5):** a final validator-failing candidate is emitted as
  `Extracted`. E2.2 dodged it via stage-level self-abstention; E2.3's Tasa/CAT/amounts MUST do the same
  (they have real verdict consumers — CL-10/21/22/24/44 — so a false-confident recovery CAN flip a verdict).

**E2.1/E2.2 ADVERSARIAL REVIEW DONE (2026-07-06):** two-lens skeptics (honesty + verdict-preservation).
Verdict = **sound to build E2.3 on** — E2.1 byte-identical-neutral, rebuild path facet-faithful (26 StatementModel
+ 24 PeriodSummary members carried), demo 0/5 verdicts changed (empirically re-run), all 3 suites green.
Two latent findings triaged: (F1) false "0 consumers" claim → corrected above + canary added (task #5); (F2)
`FuzzyLabelStage` can fabricate a plausible-but-wrong date from a cross-column homonym label ("fecha de cargo"→85,
"fecha ultimo pago"→82, "fecha de pago minimo"→100 all clear threshold-80 vs the alias set; the 2020–2035 window
can't catch an in-range wrong date) → this is the SAME homonym-collision E2.3 must solve for CAT; folded into E2.3
as a hard AC (discriminating-token matching + red→green homonym tests). Blast-radius today = zero.

**NEXT (tracker):** #5 = **review close-out** (canary + record correction, IN PROGRESS) →
then #8 (**E2.3** TASA/CAT + amounts — the hard fields: CAT homonym collision, footer token-fragmentation,
real verdict consumers) → #9 (**E2.4** movements table-shape, separate seam) → #5 (E3 validators grow) →
#6 (**E6.S6.2** greenfield estado-de-cuenta generator, its own session) → #7 (E5 FieldCandidate LLM hashes).
Verify EVERY chunk against the full verdict pipeline (§4); calibrate from PdfPig coords not pdftotext.

---

### (original handoff below — still the canonical spec for E2–E7)

You are picking up a clean boundary. Phase 1 (extractor recalibration) shipped. Phase 2
(fallback-chain **design**) shipped as docs only. Your job is to **orchestrate the
implementation of epics E1–E7**, one epic at a time, verifying from ground truth.

---

## 1. Read these first (canonical, on disk + pushed)

1. **Design + program plan (your spec — do not drift from it):**
   `docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md`
   — architecture (per-field resolver pipeline behind an `EscalatingStatementFieldExtractor`
   decorator), the field escalation matrix, validators, determinism/honesty rules, the 7-epic
   plan + sequencing, and the 4 open owner decisions.
2. **Phase-1 recalibration record (what already changed + why):**
   `docs/planning-artifacts/veriqan-extractor-recalibration-2026-07-05.md`
3. **Tracker:** tasks **#8–#14** are the epic backlog (E1–E7) with `blockedBy` deps already set.
   #1–#7 (Phase 1 + design party) are `completed`. Use TaskList/TaskUpdate.
4. **Memory:** `veriqan-extractor-recalibration-2026-07-05.md` (in the auto-memory dir) has the
   condensed design + the gotchas. `[[prisma-llm-hybrid-extractor]]` is the sibling LLM-seam pattern.

Commits this session: `bf8629c2`, `8c118e35`, `00f1b3e0`, `5dee3ca0`, `f94f812e` (all on `Liv`, pushed).

---

## 2. Start here — PI-1, Epic E1 (recommended first move)

**E1 = the per-field resolver seam, behavior-neutral (field-status diff MUST be zero).** This is the
foundation everything else hangs off. Do NOT add any fuzzy/semantic/LLM behavior in E1 — only the
seam + provenance + the escalation-policy plumbing, wrapping today's positional extractor as "stage 1"
via a strangler-fig `EscalatingStatementFieldExtractor : IStatementFieldExtractor` decorator.

Pragmatic decision already recommended in the design (ratify it, don't re-litigate): E1 runs the
existing `ExtractFullAsync` once and escalates only *higher* stages per field — do **not** refactor
the 25 `private static` per-field methods now.

After E1 lands green + behavior-neutral: **E2** (fuzzy/Levenshtein — cheap wins, closes the P1.2-deferred
PaymentDueDate + TASA/CAT) and **E7** (real product resolution, retires the alias hack) are the
highest-value next epics. E3 (validators) grows *alongside* E2. E4 (semantic) only after E2 proves
residual gaps. E5 (LLM) last. E6.S6.2 (synthetic variance corpus) can start in parallel from day one.

---

## 3. Resolve with the owner BEFORE building (4 decisions — see design §"Open decisions")

Each changes what you build; get a ruling (AskUserQuestion) or proceed on the recommended default and
say so:
1. **Comparer reuse** — add `FuzzySharp` pkg-ref directly to `Veriqan.Infrastructure.Extraction` + copy
   Levenshtein into `Veriqan.Domain` (**recommended**) vs. extract a shared `IndFusion.*` lib.
   ⚠️ Do NOT reference `Infrastructure.Imaging` from Veriqan — hexagonal violation + drags in Emgu.CV.
2. **Embeddings provider (E4)** — local Ollama (**recommended**, bank-data residency) vs. hosted.
3. **Model widening** — new `ExtractedByInference` status vs. a provenance-only sibling field. Decide
   BEFORE building stages — it ripples into every `ExtractionStatus` consumer.
4. **Product gate re-spec (E7)** — how `VerificationPipeline:657`'s null/unknown-product gate behaves
   once Product has a ladder (allow inference-sourced-but-catalog-resolved; clean-abstain on exhaustion;
   NO alias hack).

---

## 4. Verification recipe — this is non-negotiable (the P1.4 lesson)

The extractor's own unit tests were **189 green while a real regression shipped** (null Product →
whole-verdict `ExtractionGap`). It was caught ONLY by the downstream verdict suites. So for EVERY chunk:

- **Run the full verdict pipeline, not just extractor units.** The regression gate is all of:
  - Extraction: `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Veriqan.Infrastructure.Extraction.Tests/*.csproj`
  - Validation: `.../08 Tests/02 Infrastructure/Veriqan.Infrastructure.Validation.Tests/*.csproj`
  - Orchestration: `.../08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/*.csproj`
    (the demo E2E lives in `VecChecklistDemoE2ETests` — 4 fixtures → known verdicts; these MUST stay green).
- **Ground-truth diagnostic (rebuild it — I deleted the temp one so it wouldn't linger):** a throwaway
  `[Fact]` in the Extraction.Tests project that (a) runs real `ExtractFullAsync` over the 5 demo fixtures
  (`Prisma/Fixtures/PRP2/demo/{good,compliant-master,bad-math-cl21,bad-font-cl35,scanned}.pdf`) and dumps
  each `ExtractedField.Status` via reflection, and (b) dumps PdfPig words `(Left,Bottom,Right,Text)` per
  page. **Calibrate from PdfPig coords, NEVER pdftotext/PyMuPDF** (tokenization + origin differ). Delete it
  before committing. (Its exact shape is described in the recalibration memory; the constructor is
  `new PdfPigStatementFieldExtractor(XUnitLogger.CreateLogger<…>(), Options.Create(new PdfExtractionOptions()), new NullPasswordProvider())`.)
- **Additive only.** Keep the Dummie VEC fixtures green; any change to an asserted Dummie value is a
  deliberate, visible decision, not a silent gold edit.

---

## 5. Hard-won gotchas (carry these forward)

- **Product is load-bearing.** `ExtractProductName` returns a wrong value (`"Número de tarjeta 4111…"`)
  that resolves ONLY because the demo bundle registers it as a `TC-BSSB` alias. Making it abstain →
  `UnknownProduct` blocks the whole verdict. E7 fixes this properly (catalog resolution + gate re-spec);
  until then, don't "clean up" Product.
- **Honesty is cardinal:** abstain, never fabricate. Every stage output is a *candidate* until it passes
  the same validator the positional path would. False-confidence rate is a first-class metric (0-ceiling
  on verdict-gating fields).
- **Determinism (NFR-5):** LLM/semantic stages need a content-hash cache (the real guarantee); temperature
  0 is already pinned in both providers; disagreement / non-determinism → abstain; no silent retries;
  synthetic-only few-shot examples (echo-leak risk).
- **Reuse map:** `ILlmProvider`/`ILlmProviderFactory`/`LlmProvidersOptions` lift cleanly; Prisma's
  `HybridExtractionService`/`ILlmExpedienteExtractor<T>`/`Expediente` do NOT (Oficio-shaped) — copy the
  pattern, rebuild the types. Fix the `CancellationToken.None` bug in `LlmVisionFieldExtractor` when porting.
- Build is `dotnet build`/`dotnet test` per project (see CLAUDE.md); global.json opts into MTP;
  warnings-as-errors + nullable are on.

---

## 6. Orchestration loop for this work

One epic at a time. For each: re-ground from the tracker + design doc → delegate cohesive chunks to
`dev` subagents with tight briefs + the PdfPig coordinate ground truth → **verify from ground truth
yourself** (build + the 3 suites above + the field-status diagnostic + `git diff`) → commit in
meaningful chunks with a verification line → push `Liv` → adversarial-review at each epic boundary
(a `general-purpose` skeptic refuting against the design doc). Stop + hand off at epic boundaries; do
not roll into the next epic without a deliberate decision. Never drive the semantic-infra (E4) or the
model-widening (decision #3) as a silent autonomous refactor — surface + scope with the owner.
