# PRP1-golden-nullslice — deterministic-NULL(`NumeroExpediente`) corpus for S4-M.1

A **golden** synthetic corpus of **36 fixtures**, each engineered to make the deterministic extraction
track (real Tesseract OCR → `AdaptiveTxtFieldExtractor`, via `PdfOcrFieldExtractor`) return **NULL** for
`NumeroExpediente` — while the true value **is still rendered, source-contained, in the document body**.

This is the population the S4-C LLM-text fallback would ever run on. It exists to make the fallback's
conditional precision (`P(LLM-text correct | deterministic NULL)`) decidable (ADR-024 D6: N&gt;=30). See
`docs/planning-artifacts/SCOPING-llm-hybrid-S4M-gateb-measurement.md` (S4-M.0 / S4-M.1.0 / S4-M.1).

Sibling to `Prisma/Fixtures/PRP1-golden/` (do NOT mutate that corpus — this is a separate, additive corpus
for the deterministic-NULL slice specifically). Same gold discipline: the oracle is ALWAYS the
generator-stamped `ground_truth.json` manifest, never a delivered companion file
(`[[prisma-domain-3-docs-unreliable]]`).

## Why NULL-by-construction, not by chance

S4-M.0 measured the *natural* deterministic-NULL rate on PRP1-golden: only **3/20** fixtures nulled
`NumeroExpediente` — far below the N&gt;=30 decidability floor. Growing the slice required *deliberately
constructing* fixtures that null, without inventing an unrealistic failure — see the S4-M.1.0 diversification
spike (`docs/planning-artifacts/SCOPING-llm-hybrid-S4M-gateb-measurement.md`), which empirically confirmed 3
DISTINCT, grounded, source-contained deterministic-NULL mechanisms via CLI probing (real
`tesseract -l spa --oem 1 --psm 6` with the prod char-whitelist, `pdftoppm -r 300`, and the real
`AdaptiveTxtFieldExtractor.ExtractExpediente` regex).

**Owner ruling: diversify, don't monoculture.** Over-sampling one bug 30x would make the eventual precision
number measure that one bug, not real fallback precision — hence the even 3-way split below.

## The 3 grounded modes (12 fixtures each)

| # | Mode | Layer | Mechanism | Generator knobs used |
|---|------|-------|-----------|----------------------|
| 1 | `mode1-FI1` — `FI1`→`Fl1` glyph | OCR glyph | Tesseract reads capital `I` as lowercase `l` in the `FI1` area code → `AdaptiveTxtFieldExtractor`'s `[A-Z]{1,4}` segment breaks (lowercase `l` is not `[A-Z]`) for BOTH the primary and the fuzzy (O→0/I→1-tolerant) pattern. **Must exclude `judicial`-type docs** — they additionally embed the Expediente in Motivación prose (`legal_catalog.py:180-183`), which would rescue a dedicated-line misread. | `--expediente-area-codes FI1`, `--types fiscal pld aseguramiento informacion` (excludes judicial) |
| 2 | `mode2-emdash` — em-dash `—` delimiter | OCR char-whitelist | The prod `TesseractOcrExecutor` character whitelist (`ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,;:!?¿¡()[]{}"'-/$%&ñÑáéíóúÁÉÍÓÚüÜ `) has no em-dash → it's dropped or misread inconsistently, digits fuse (e.g. `...436896IMX` with no delimiter before the suffix) → no delimiter for the regex to match on. | `--expediente-delimiter '—'`, `--expediente-area-codes AS1 IN1 PL1 JU1 AS2 IN2` (excludes FI1, to keep this mode single-mechanism) |
| 3 | `mode3-spaced` — spaced hyphen `" - "` delimiter | pure regex-strictness | Tesseract reads the spaced hyphen perfectly (confirmed via CLI OCR) — but the extractor's regex `[-–]` has zero whitespace tolerance, so `\d+[-–]\d+` can't span the space-hyphen-space run → null despite flawless OCR. | `--expediente-delimiter " - "`, `--expediente-area-codes AS1 IN1 PL1 JU1 AS2 IN2` (excludes FI1) |

**Rejected in the spike (not reproduced here):** 6/7 non-FI1 area codes; suffix-position `I` misreads
(`UIF`/`IMX`); en-dash `–` (OCR normalizes back to `-`, and the regex already accepts `–`); narrow-container
line-wrap (fails literal source-containment; also would require undoing the generator's load-bearing
"EXPEDIENTE CONTIGUITY" fix).

## Generator changes (additive, backward-compatible)

`Prisma/PRP/PRP1/research/generators/AAAV2_refactored/` — the SAME generator that produced `PRP1-golden`.
Two small additive knobs were added (default behavior unchanged; omitting either flag reproduces the
original `random.choice` over the full area-code pool + hardcoded `'-'` delimiter exactly):

- `core/data_generator.py` — `generate_numero_expediente(area_codes=None, delimiter='-')`: optional
  area-code-pool override and field-delimiter override.
- `main_generator.py` — threads the two new params through `CNBVFixtureGenerator.__init__` →
  `_generate_requirement_data` → `generate_numero_expediente`, and exposes them as CLI flags
  `--expediente-area-codes` / `--expediente-delimiter`.
- **No new flag was needed for "doc-type exclusion"** — the existing `--types` flag already supports a
  positive type list (e.g. `--types fiscal pld aseguramiento informacion` excludes `judicial`).

No production `.cs` changed. `AdaptiveTxtFieldExtractor` and `ExtractionReconciler` are untouched — the
measurement observes today's shipped deterministic path exactly as it runs.

## How it was generated (reproducible)

```bash
cd Prisma/PRP/PRP1/research/generators/AAAV2_refactored
DEST=Prisma/Fixtures/PRP1-golden-nullslice

# Mode 1: FI1 glyph misread (non-judicial)
python3 main_generator.py --count 12 --types fiscal pld aseguramiento informacion \
  --formats md pdf --chaos none --seed 5001 -o "$DEST" \
  --expediente-area-codes FI1

# Mode 2: em-dash delimiter (non-FI1, to keep the mode single-mechanism)
python3 main_generator.py --count 12 --formats md pdf --chaos none --seed 5002 -o "$DEST" \
  --expediente-area-codes AS1 IN1 PL1 JU1 AS2 IN2 \
  --expediente-delimiter $'—'

# Mode 3: spaced-hyphen delimiter (non-FI1)
python3 main_generator.py --count 12 --formats md pdf --chaos none --seed 5003 -o "$DEST" \
  --expediente-area-codes AS1 IN1 PL1 JU1 AS2 IN2 \
  --expediente-delimiter " - "
```

Only `md` + `pdf` formats are generated (not `xml`/`html`/`docx`) — the S4-M.0/M.1 probes and the
deterministic extraction path under test only need the PDF (+ `ground_truth.json` gold); `html` is still
produced as an unavoidable intermediate of the PDF export step. `--chaos none`, seeded, reproducible.

## Source-containment gate (guarantees the gold is real)

Same discipline as `PRP1-golden`: at generation, the `.md` body is the **hard** gate — every body-intended
gold field (including `numeroExpediente`, unconditionally rendered as its own "Número de expediente:" line
for every requirement type) must literally appear in the rendered Markdown, or generation fails loudly.
`pdfContained` (normalized `pdftotext`) is advisory at generation time — but for THIS corpus specifically it
was **also independently re-verified, per-fixture, against the actual on-disk PDF** by the S4-M.1 hard
verification gate (see below): **36/36 pdftotext-contained**.

## HARD VERIFICATION GATE — confirmed through the LIVE .NET pipeline (2026-07-19)

Probe: `Tests.Infrastructure.Extraction/S4M1_DeterministicNullDiversifiedCorpusProbe.cs` (skip-gated,
measurement-only, mirrors `S4M0_DeterministicNullExpedienteProbe`'s discipline). Runs the REAL
`TesseractOcrExecutor` + `PdfToImageConverter` + `AdaptiveTxtFieldExtractor` (via `PdfOcrFieldExtractor`)
over every fixture — NOT a regex replay.

**Result (2 runs, both green, ~2m41s each):**

- **Deterministic-NULL slice: 36/36** (every fixture nulled — no fixture recovered a value despite the
  injected mode).
- **3-way split holds exactly even:** `mode1-FI1` 12/36 (33%), `mode2-emdash` 12/36 (33%), `mode3-spaced`
  12/36 (33%) — well under the 60% single-mode cap.
- **D7.1 source-containment: 36/36** — every fixture's gold `numeroExpediente` is literally present
  (whitespace-normalized) in its own rendered PDF's `pdftotext` output, re-verified independently by the
  probe (not just at generation time).
- **No fixtures failed to null** — the corpus did not need buffering beyond the ≥30 target; all 36
  generated fixtures are usable, giving S4-M.2 more headroom than the N≥30 floor.
- OCR stable across both runs (no SIGSEGV, no native crash).

## `ground_truth.json` gold fields

Same shape as `PRP1-golden`'s manifest: `numeroExpediente`, `numeroOficio`, `autoridadNombre`,
`nombreSolicitante`, `personaNombre`, `personaRfc`, `monto`, `solicitudPartes`, `bodyContained` /
`pdfContained`. The `numeroExpediente` value is the one deliberately engineered per mode above; every other
field is generated the same way as `PRP1-golden` (unforced).

## Known deliberate limitation

This corpus targets ONLY the `NumeroExpediente` deterministic-NULL slice (the S4-C control field, D2
anchor). Other fields (`autoridadNombre`, `personaNombre`, etc.) are NOT deliberately nulled here — they
follow the same generation as `PRP1-golden` and are expected to extract normally. Do not reuse this corpus
to measure full-corpus per-field accuracy; that is `PRP1-golden`'s job.
