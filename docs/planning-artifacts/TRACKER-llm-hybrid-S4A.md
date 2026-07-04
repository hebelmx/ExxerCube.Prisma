# TRACKER — S4-A/S4-B: Calibrate + Extend + Graduation Criteria (LLM/Hybrid extractor)

Branch `Liv`. Spec: `docs/implementation-artifacts/spec-llm-hybrid-extractor-S4A.md`.
Orchestrated 2026-07-03. Owner ruling: S4 = Option A (measure before graduating).

## ✅✅ S4-B EPIC CLOSED 2026-07-04 — verified DONE from ground truth (fresh orchestrator re-verification).
## The extractor extension is landed, VALIDATED on a source-contained corpus, and its lone residual gap is a
## deliberate WONTFIX. Nothing un-gated remains under S4-B. Evidence (code + eval, not commit prose):
## - Code present: `LlmExpedienteDto.cs:17-18` (numeroOficio/autoridadNombre fields); `LlmExpedienteMapper.cs:55-65`
##   (field-level abstention via `LlmExtractionGate.IsPlausibleNumeroOficio/IsPlausibleAutoridadNombre`).
## - Source-contained-gold BLOCKER (the old "top lever") is CLOSED: P1 generator commits `d6b7dfb7`→`313d2210`
##   emit a source-contained gold manifest + the `Prisma/Fixtures/PRP1-golden` 20-doc corpus; `69ce5c71` added the
##   trustworthy gold loader. So accuracy numbers from the harness ARE now meaningful.
## - VALIDATED (`97b77be7`, ~11m live Ollama llama3.1:8b/gemma3:12b on the 20-doc golden set): AutoridadNombre
##   LLM-text 81% (13/16) vs deterministic 0% — S4-B's CENTRAL claim proven; NumeroExpediente LLM-text 94% > 85%.
## - Deterministic-authority production bug (deterministic returned the constant CNBV *recipient*, not the
##   *requesting* authority) FIXED `5a4d0b86` — 0/20→~20/20 via the `Autoridad solicitante:` label rule.
## - Lone residual gap LLM-text NumeroOficio 0/16 → DIAGNOSED + prompt fix REJECTED as net-negative (`149756f4`,
##   ADR-024 D3-golden-v3): a controlled temp-0 paired run showed the fix (0/16→14/15) REGRESSES expediente
##   (15→12) and authority (13→9) to patch a REDUNDANT field — deterministic oficio is 100% and the reconciler
##   is deterministic-wins, so the LLM oficio value never reaches the output. Correct call: do not ship.
## - Vision FROZEN for the demo by owner (`2c93d06c` gate vision UI on VisionExtractorEnabled, `37c2d4d1` dynamic
##   N-Way label) — no visible vision failure in the shareholder demo.
##
## SUCCESSORS (open, NOT part of S4-B): (design) gate-coupling — all-or-nothing DTO rejection vs per-field
## abstention; owner steer 2026-07-04 = return a FAILED Result<Expediente> carrying the partial T + metadata +
## error list. (S4-C, separate epic) un-dark the PIPELINE — the Athena worker never calls HybridExtractionService;
## the flags gate only the /hybrid-extraction demo page. Both tracked separately from this closed tracker.

## PIVOT 2026-07-03 (owner ruled B after adversarial gate): the measure-only S4-A surfaced that the DARK
## LLM extractor only attempts partes(+wrong-format expediente) — it never produces NumeroOficio/AutoridadNombre
## (C1/C2, confirmed from code + gold). Owner chose to EXTEND the extractor (functional, S4-B) THEN measure,
## rather than baseline the gap. New sequence: [design panel → S4-B spec] → S4-B extractor extension (#6) +
## eval bug fixes (#7) → live baseline (#3) → ADR-024 finalize (#4 done, refresh numbers) → smoke (#5).
## S4-B is a functional change to the dark path (flags default false → zero production runtime impact).

| # | Story | Status | Verified by | Commit |
|---|-------|--------|-------------|--------|
| S4A-1 | Harness build-out: invoke 3 tracks + compute per-field metrics + emit JSON/MD artifact (D1) | TODO | build 0/0 + diff | |
| S4A-2 | Deterministic metric-computation unit test, mocked ILlmProvider (D2) — the verification anchor | TODO | test green | |
| S4A-3 | Live run on box → commit baseline artifact `docs/evaluation/llm-hybrid-extraction-baseline-2026-07.md` (+JSON) (D3) | ✅ DONE | 2m06s live run, artifact written w/ real numbers + model tags; surfaced silent-green-pass bug + gold-not-source-contained finding | (pending) |
| S4A-4 | Graduation-criteria ADR-024 (D4) | ✅ DONE | D3 table filled + measurement-validity finding added | (pending) |
| S4A-5 | (optional) Live-provider smoke test, skip-gated (D5) | TODO | test present + skips clean | |

## DONE this session (2026-07-03, all pushed to Liv): 79b0da91 (S4-A harness+metrics+17 tests),
## c4522def (eval bug-fixes C3/C4/M1/M2/M3, metrics 20), 9496c1c3 (S4-B extractor extension + prompt
## honesty fix), 24efd638 (S4-B spec + ADR-024 D7 + tracker). Two adversarial gates run + acted on.

## ✅ S4A-3 DONE 2026-07-04 (live baseline run + committed). The run itself surfaced TWO real bugs:
## (BUG-1) SILENT-GREEN-PASS: the first un-skipped run "passed" in 329ms writing NO artifact — LocateRepoRoot
##   resolved to `.../BuildArtifacts` (a sibling tree that ALSO contains a "Prisma" dir) because the test
##   binary runs out of BuildArtifacts, which is NOT under the repo root, so walking up from
##   AppContext.BaseDirectory never reaches the real repo. FIX: anchor on [CallerFilePath] (compile-time
##   source location, in the real tree) + require the gold file to exist under the candidate root; AND
##   convert the early-return preconditions to LOUD failures (throw/ShouldBeTrue) so a no-op can never
##   pass green again. (BUG-2 was the pre-existing name-only check.) 3rd run = real work, 2m06s, artifact written.
## (FINDING — the big one) BOTH deterministic AND LLM tracks score ~0% accuracy → the MEASUREMENT failed,
##   not the extractor. grep of the committed 222AAA .ocr.txt proves the eval GOLD is NOT source-contained:
##   gold expediente `A/AS1-1111-222222-AAA` (0 matches), gold oficio `222/AAA/...` (0 matches), gold
##   authority `SUBDELEGACION 8 SAN ANGEL` (0 matches) — all synthetic filename/XML-derived IDs never
##   rendered into the PDF body. The extractors correctly read the REAL strings that ARE present
##   (oficio `AGAFADAFSON2/2025/000084`, authority `Comisión Nacional Bancaria y de Valores`). So the
##   baseline certifies ONLY: (a) harness runs end-to-end, (b) honesty gate holds (5/6 LLM cases correctly
##   abstained rather than emit a wrong expediente). It does NOT yet certify/refute either LLM track.
##   This is the empirical proof of ADR-024 D7.1 (source-text containment) — now OBSERVED, not hypothesized.
## Artifacts: docs/evaluation/llm-hybrid-extraction-baseline-2026-07.{json,md} (md has a manual interpretation
##   banner — machine tables overwrite on re-run). ADR-024 D3 table filled + "D3 measurement-validity finding" added.
## Secondary finding logged (not fixed): gate rejects the WHOLE DTO on a present-but-invalid expediente,
##   discarding usable oficio/authority/partes → Gate-B design question (per-field abstention?).

## REMAINING / next units — UPDATED 2026-07-04 (S4-B itself is CLOSED; see the top banner):
## 1. [CLOSED ✅] Rebuild the eval gold source-contained (D7.1) — DONE by P1 generator commits `d6b7dfb7`→`313d2210`
##    + gold loader `69ce5c71`; the 20-doc `PRP1-golden` corpus is source-contained and the harness numbers are
##    now meaningful. (This was the old "top lever"; no longer open.)
## 2. [DESIGN — IN PROGRESS] Gate coupling: all-or-nothing DTO rejection vs per-field abstention (Gate-B question).
##    Owner steer 2026-07-04: return a FAILED Result<Expediente> that still carries the partial T + metadata +
##    error list (per-field abstention reasons). Being settled by a BMAD design party → decision doc / ADR-024 addendum.
## 3. [SEPARATE EPIC — S4-C] un-dark the PIPELINE: the Athena worker never calls HybridExtractionService today
##    (flags gate only the /hybrid-extraction demo page). Safe first slice = flag-gated wiring (still DARK, zero
##    runtime impact); production flag-flip is owner-gated + ADR-024 Gate-B honesty preconditions (D7).
## 4. [OPTIONAL] S4A-5 live-provider smoke test. 5. Gemini track = SkippedNoKey (no key on box).

## Notes / carried facts
- Models: text=`llama3.1:8b`, vision=`gemma3:12b` (spec defaults llama3.2/minicpm-v NOT installed → override).
- Only `222AAA` has committed `.ocr.txt`; harness OCRs `333BBB`/`333ccc` at run time (Tesseract 5.5 + spa).
- Gemini: no key → reported SkippedNoKey, not run.
- Oficio corpus is OUT (classification eval, separate baseline).
- NO production `.cs` changes. Deterministic path byte-identical.

## Adversarial-review gate — RAN 2026-07-03 (qa skeptic). VERDICT: baseline NOT trustworthy yet. Findings triaged:
- **C1 (confirmed):** LLM gate regex `^\d{3,6}/\d{4}$` rejects the real CNBV expediente format
  (`A/AS1-1111-222222-AAA`). LLM track cannot match gold expediente. (Deterministic AdaptiveTxt handles it.)
- **C2 (confirmed):** `LlmExpedienteDto` has NO NumeroOficio / AutoridadNombre — LLM extractor never attempts
  them → structural 0% on 2 of 4 fields. LLM DTO actually targets: Expediente, Solicitante, Monto, Cuenta,
  Rfc, Curp, Partes. Headline = Partes (deterministic path produces NONE → this is the real value-add to measure).
- **C3 (confirmed engine bug):** `AggregateAccuracy` doesn't exclude TrackSkipped from denominator. Untested.
- **C4 (confirmed harness bug):** per-fixture LLM failure recorded as Missing, not TrackSkipped → infra flakiness
  misattributed as model miss.
- **M1:** 222AAA fed committed `.ocr.txt` to LLM-text but fresh Tesseract to deterministic → not apples-to-apples.
- **M2:** no diacritic folding in authority normalize (Comisión≠Comision).
- **M3:** NormalizeCaseReference structure-blind → constructible boundary-shift false match (unguarded, untested).

### Fix plan (all measure-only, tests+docs):
Engine/harness bugs C3+C4+M1+M2(+M3 test) → fix regardless.
Field-set reframe (C1/C2) → OWNER-GATED (see below). Recommended: measure honestly what the LLM ACTUALLY
attempts (Partes headline + expediente-with-known-format-defect + Rfc/Curp/Monto where gold exists), label
NumeroOficio/AutoridadNombre as `NotAttemptedByDesign` for LLM tracks (excluded from accuracy denom), and
log the extractor-incompleteness as a discovered finding → recommend follow-up story S4-B (extractor hardening,
functional, OUT of S4-A). Do NOT change the production extractor DTO/prompt/gate in S4-A.
