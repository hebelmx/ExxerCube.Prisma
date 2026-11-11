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

---

## 8. Addendum — owner picked **Direction #1 (harden the extractor)**  ·  2026-07-20  ·  `Liv`

The owner selected §5 **Direction #1**. The `NumeroExpediente` deterministic failures were patched directly in
`AdaptiveTxtFieldExtractor.ExtractExpediente` rather than measuring an LLM fallback over a slice the deterministic
extractor could itself survive.

**What shipped** (production `.cs`, working tree on `Liv`):
1. **Whitespace-tolerant delimiter (recovers mode3-spaced).** Both the primary and fuzzy expediente regexes now
   accept horizontal whitespace around each delimiter (`[ \t]*[-–][ \t]*`, newlines deliberately excluded to avoid
   cross-line matches); a new `CanonicalizeDelimiters` helper collapses the match back to the canonical bare-hyphen
   form. `A/AS1 - 2025 - 436896 - IMX` → `A/AS1-2025-436896-IMX`.
2. **`I↔l` OCR cleanup (recovers mode1-FI1 glyph).** The *fuzzy* pattern's letter class widened `[A-Z]{1,4}` →
   `[A-Zl]{1,4}` (primary untouched), and `CleanOcrErrors` gained `l → I` alongside the existing `O → 0` — scoped to
   the fuzzy-matched expediente token only. `A/Fl1-2025-436896-IMX` → `A/FI1-2025-436896-IMX`.
- **mode2-emdash deliberately NOT handled** — the adversarial review (§2) refuted it as un-grounded/invented, and its
  mechanism (delimiter dropped by the OCR char-whitelist, digits fuse) is not delimiter-recoverable in any case.

**Verified from ground truth:** `Infrastructure.Extraction.Txt` builds 0 warn / 0 err; `Tests.Infrastructure.Extraction.Txt`
**214/214** green (210 baseline + 4 new: 2 mode-positive, 2 regression guards incl. a lowercase-`l`-in-prose guard).
ITDD red phase confirmed (the 2 positive cases returned `null` before the fix).

**Scope of the claim (honest):** this is verified at the **unit level** — the regex now recovers the mode1 and mode3
*token shapes* from representative OCR text strings. Because the natural M.0 slice (§2, **3/20**) was **100% mode1
(`FI1`)**, the naturally-occurring `NumeroExpediente` deterministic-NULL slice is expected to collapse toward zero on
real documents. **Not yet re-measured end-to-end:** re-running the skip-gated live-pipeline probes (`S4M0_…` /
`S4M1_…Probe.cs`) over `PRP1-golden-nullslice/` (24/36 fixtures = mode1+mode3 should now extract; the 12 mode2 remain
NULL by design) is the confirming measurement. That step needs the live Tesseract pipeline (and may hit the known
Linux OCR-native-coexistence SIGSEGV — see `ocr-segfault-troubleshooter`), so it is called out as the recommended
follow-up, **not** silently assumed done here.

**Gate-B consequence:** with the extractor hardened, the `NumeroExpediente` LLM-fallback slice shrinks to (at most)
the un-grounded mode2 residual. This closes the field-level Gate-B question for `NumeroExpediente` as
*"LLM fallback not warranted for this field"* (pending the live re-measurement above). The fallback's real value — if
S4-C is pursued — lives on a field where deterministic is genuinely weak (§4: `AutoridadNombre`, §5 Direction #2), not
on the identity key. **No S4-C production wiring was authorized or written by this addendum.**

---

## 9. Addendum — live re-measurement CONFIRMS the closure + owner PARKS S4-C  ·  2026-07-21  ·  `Liv`

The §8 follow-up was executed (owner ruled "run it now"). `S4M1_DeterministicNullDiversifiedCorpusProbe`
was temporarily un-skipped and run through the LIVE .NET pipeline (real Tesseract 5.5 + spa → `PdfOcrFieldExtractor`
→ hardened `AdaptiveTxtFieldExtractor`) over all 36 `PRP1-golden-nullslice/` fixtures. Run: 2m49s–3m34s, **no OCR
SIGSEGV** (two full runs), probe 1/1 green.

**Measured result — deterministic-NULL slice collapsed 36/36 → 13/36:**

| Mode | Pre-hardening NULL | Post-hardening NULL | Reading |
|------|-------------------|---------------------|---------|
| mode1-FI1 (the only *grounded* mode) | 12/12 | **0/12** | fully recovered — the natural real-doc slice (M.0 was 100% mode1) is now empty |
| mode3-spaced | 12/12 | **1/12** | 11 recovered; the residual (`INFONAVIT-2024-389098`, gold `H/AS2 - 2108 - 458945 - FGR`) is fixture-specific OCR noise in the production render path — CLI `tesseract` at 300 dpi reads the line verbatim, and identical-shape siblings (`A/AS2 - …`, `H/AS2 - …`) recovered |
| mode2-emdash | 12/12 | **12/12** | unchanged **by design** — un-grounded/invented mode, deliberately not handled (§8) |

D7.1 pdftotext source-containment: still 36/36. The probe's `[Skip]` annotation now carries both the
pre- and post-hardening results.

**Gate-B verdict, now measured (not just expected):** the grounded `NumeroExpediente` deterministic-NULL slice is
**zero** post-hardening; the only residual NULLs are the invented mode2 corpus artifacts plus one OCR-noise
one-off. Field-level Gate-B for `NumeroExpediente` is **CLOSED: "LLM fallback not warranted for this field"** —
no longer pending.

**Owner ruling (same sitting): S4-C is PARKED.** With `NumeroExpediente` closed and no *currently-grounded*
weakness on `AutoridadNombre` (the S4-B "81% vs 0%" headline predates fix `5a4d0b86`; deterministic is ~100% on
PRP1-golden by construction), the epic has no grounded target field. Reopen condition: real-document evidence of
a genuinely weak field → §5 Direction #2 (grounded measurement FIRST; build stays gated on the result). Ruling
record: `docs/planning-artifacts/DECISIONS-2026-07-21-owner-rulings.md`; spec + tracker banners updated.
