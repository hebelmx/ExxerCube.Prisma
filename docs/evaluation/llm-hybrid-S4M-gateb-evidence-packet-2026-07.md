# S4-M Gate-B Evidence Packet — LLM Fallback for `NumeroExpediente`

**For:** owner S4-C go/no-go · **Date:** 2026-07-19 · **Branch:** `Liv` · **Status:** measurement paused by owner ruling; this packet IS the deliverable.
**Sources:** `docs/planning-artifacts/SCOPING-llm-hybrid-S4M-gateb-measurement.md` (full trail), `docs/architecture/adr/ADR-024-llm-hybrid-graduation-criteria.md` (Gate-B), `docs/planning-artifacts/TRACKER-llm-hybrid-S4A.md` (S4-B baselines).

---

## 1. What S4-M set out to produce

S4-C (un-dark the LLM-text fallback in the Athena worker) is owner-gated on one number that did not exist:
**`P(LLM-text NumeroExpediente correct | deterministic extractor returned NULL)`** — the precision of the fallback
on the *only* slice it ever runs on. S4-M was scoped to produce that number, measurement-only, zero production `.cs`.

## 2. What we found (honest, ground-truth-verified)

| Step | Result |
|------|--------|
| **M.0** — natural deterministic-NULL rate on PRP1-golden (20 docs, real .NET OCR pipeline) | **3/20** null `NumeroExpediente` — far below the N≥30 decidability floor (ADR-024 D6). All 3 fail by ONE mechanism: Tesseract reads capital `I`→lowercase `l` in the `FI1` token; the extractor regex has no tolerance for it (it handles `O↔0`, `I→1`, but not `I→l`). |
| **M.1.0** — diversification spike | Only **one** grounded failure mode exists in the corpus (the `FI1` glyph bug). To reach N≥30 we constructed 2 more mechanisms — em-dash delimiter (dropped by the OCR char-whitelist) and spaced-hyphen delimiter (regex has zero whitespace tolerance). |
| **M.1** — built a 36-fixture NULL corpus (`Prisma/Fixtures/PRP1-golden-nullslice/`) | Verified via the live .NET pipeline: 36/36 null, even 12/12/12 split, source-contained. **Generator change is additive** (RNG-equivalence tested 200 seeds — PRP1-golden reproducibility preserved). |
| **Adversarial review** (2 reviewers) | **Corpus methodology partially refuted.** Only **mode1 (`FI1`, 12 fixtures)** is grounded in an *observed* failure. **mode3 (spaced, 12)** is a whitespace-regex bug — OCR is byte-perfect, a one-line `\s*[-–]\s*` fix recovers all 12 with no LLM. **mode2 (em-dash, 12)** is invented formatting (no real-doc citation) and re-segmentable only because the corpus uses one fixed width template. The three modes are three different difficulties (trivial copy / one-glyph fix / template re-segmentation) — a single blended precision number describes none of them. |

## 3. The honest conclusion

**A single defensible `P(LLM correct | deterministic NULL)` number for `NumeroExpediente` cannot be produced from this
corpus.** The field's deterministic failures are (a) narrow, (b) mostly *un-grounded* once diversified to reach N≥30, and
(c) **largely deterministically fixable** (a whitespace-tolerant delimiter + an `I↔l` OCR-cleanup entry would recover
mode3 + mode1 with no LLM at all). Measuring "LLM fallback precision" over a corpus dominated by cases the deterministic
extractor could itself be patched to survive would overstate the fallback's marginal value — the exact synthetic-corpus
trap ADR-024 / the scoping doc warned against.

## 4. Scope caveat (important — do NOT over-read this)

This finding is **specific to `NumeroExpediente`**, the field the S4-C fallback *gates on*. It does **not** refute the
broader S4-B thesis: LLM-text still beats deterministic **81% vs 0% on `AutoridadNombre`** and 94% overall. The contrast
is itself the signal — `NumeroExpediente` is a *poor vehicle* for demonstrating fallback value precisely because the
deterministic extractor is already good at it and its failures are cheap to patch. The fallback's real value lives in the
fields where deterministic extraction is genuinely weak.

## 5. Recommended owner decisions (pick a direction for S4-C)

1. **Harden the extractor for `NumeroExpediente`** (cheapest, ~1-line each): add whitespace-tolerant delimiter and `I↔l`
   OCR cleanup to `AdaptiveTxtFieldExtractor`. Likely collapses the real NULL slice toward zero → Gate-B closes as
   *"LLM fallback not warranted for this field."* Touches production `.cs` (a real, but small, change).
2. **Re-target the Gate-B measurement onto `AutoridadNombre`** (or another field where deterministic is demonstrably weak,
   per S4-B). This is the honest version of the conditional-precision question — measure the fallback where it actually
   fires with value. Reuses all the S4-M harness/probe/generator machinery.
3. **Proceed to S4-C on the broader S4-B evidence**, explicitly accepting that per-field `NumeroExpediente`-NULL precision
   is *not* separately established, and rely on the S4-C control-discard gate (plausibility + source-containment) +
   provenance-in-review-UI to bound risk.

## 6. What is built and reusable (regardless of direction)

- `Prisma/Fixtures/PRP1-golden-nullslice/` — 36 source-contained deterministic-NULL fixtures (retained as a **robustness**
  corpus with the caveats above; NOT a grounded precision oracle as-is). Add segment-width variance before trusting any
  precision number off it.
- Generator knobs (`AAAV2_refactored`: `--expediente-area-codes` / `--expediente-delimiter`) — additive, reproducible.
- Skip-gated live-pipeline probes `S4M0_…` / `S4M1_…Probe.cs` — reusable NULL-slice measurement harness.
- The M.0 finding (3/20, single grounded mode) — the load-bearing input to any of the three decisions above.

## 7. Gate-B checklist status

- **Item 1 (the missing conditional number):** intentionally NOT produced — this packet documents *why* a single number
  on `NumeroExpediente` is not defensible, and hands the owner three grounded directions instead.
- **Item 2 (N≥30 corpus):** built (36), but only 12 grounded → does not satisfy the *spirit* (grounded variance).
- **Items 3/4/5/6/7/8:** unchanged / still owned by S4-C + the owner ruling.
