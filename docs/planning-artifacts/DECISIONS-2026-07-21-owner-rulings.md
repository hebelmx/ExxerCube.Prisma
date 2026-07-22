# Owner rulings — 2026-07-21 (branch `Liv`)

Three pending gates were put to the owner in one sitting; all three were ruled. This note is the
canonical record; per-artifact banners/trackers were updated to match (links below).

## 1. S4-M live re-measurement — RUN NOW ✅ (executed this session)

The Direction-#1 extractor hardening (`2aaf1b2b`) was only unit-verified. Owner ruled: run the
confirming live-pipeline measurement immediately. The skip-gated probe
`S4M1_DeterministicNullDiversifiedCorpusProbe` was temporarily un-skipped and run over
`Prisma/Fixtures/PRP1-golden-nullslice/` (36 fixtures, real Tesseract 5.5 + spa).

**Measured result (probe 1/1 green, ~3 min, NO OCR SIGSEGV, two full runs):** NULL slice collapsed
**36/36 → 13/36**. mode1-FI1 (the only grounded mode) **0/12** — fully recovered; mode3-spaced 1/12
(single fixture-specific OCR-noise residual, `INFONAVIT-2024-389098`; CLI tesseract reads the line
verbatim, identical-shape siblings recovered); mode2-emdash 12/12 unchanged by design. D7.1
source-containment still 36/36. **Field-level Gate-B for `NumeroExpediente` is now CLOSED on
measurement, not expectation.** Full table: evidence packet §9.

## 2. S4-C (un-dark the LLM-text fallback in the Athena worker) — **PARKED**

**Ruling:** park the epic. Rationale accepted by the owner:
- `NumeroExpediente` (the D9 trigger anchor) is closed field-level as *"LLM fallback not warranted"*
  — the deterministic extractor was hardened instead (evidence packet §8, now §9-confirmed).
- The remaining candidate field, `AutoridadNombre`, has **no currently-grounded weakness**: the S4-B
  "81% vs 0%" headline is stale (fix `5a4d0b86`-era generator-stamped label took deterministic to
  ~100% on PRP1-golden *by construction*). Any honest measurement first needs a grounding-firmed,
  real-doc-shaped corpus (no generator-stamped label) — which does not exist today.

**Reopen condition (explicit):** real-document evidence of a field where deterministic extraction is
genuinely weak. If that surfaces, the path is evidence-packet §5 Direction #2 (measure
`P(LLM correct | det wrong-or-empty)` on the grounded corpus first; the build stays gated on the
result). The D9-reconciled design in `spec-llm-hybrid-extractor-S4C.md` remains valid as the
anti-drift reference for that future build — nothing is deleted.

**No S4-C production wiring exists; none was authorized.** The worker still calls the deterministic
`ExtractionOrchestrator` path only.

## 3. Veriqan next lever — **real CONDUSEF corpus** (owner-sourced material)

**Ruling:** the next Veriqan investment is the standing "top lever" — a real/realistic CONDUSEF
statement corpus + reference bundle, to calibrate the recompute rules (Epic 11 residual) and
validators against ground truth. E4 (semantic/embeddings) and E5 (LLM stage) stay parked per the
program's own sequencing rule ("only after residual gaps are proven on real material").

**What the owner needs to supply (blocking input — no code proceeds without it):**
- Real (or bank-provided realistic) estados de cuenta PDFs — ideally several banks/layouts, both
  compliant and known-defective examples.
- The corresponding per-tenant reference bundle material (checklist CSV verified against the bank's
  actual list, products catalog, brand/image catalog).
- Any available CONDUSEF adverse findings / dictámenes to use as gold labels for the law tier.

Until that material lands, the Veriqan lane is idle by design (not blocked on code).

---

*Session artifacts: probe run + evidence-packet §9; spec-S4C banner PARKED update;
TRACKER-llm-hybrid-S4A successor #3 updated. Memory updated
(`prisma-s4c-fallback-trigger-mismatch`, `prisma-llm-hybrid-extractor`).*
