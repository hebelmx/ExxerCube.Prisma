# Veriqan Calibration Harness — Reference Guide

**Branch:** `Liv` · **Date:** 2026-06-19 · **Status:** Seed corpus (0 KnownGood, 3 KnownSynthetic, 0 KnownBroken)

---

## Purpose

The calibration harness evaluates the Veriqan VEC rule engine against a labelled corpus of
statement PDFs, computing:

- A **confusion matrix per CheckId** (Pass / Fail / InsufficientData counts + detection /
  false-positive rates).
- **Threshold-evidence tables** showing the measured quantity from each specimen alongside
  the current rule threshold, so a human reviewer can see how much margin exists.
- A **honesty banner** that loudly warns when the corpus lacks KnownBroken specimens and
  thresholds are therefore uncalibrated.

It does NOT auto-apply any threshold change. Threshold changes are legal/compliance decisions.

---

## Running the harness

```powershell
dotnet test "Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/ExxerCube.Prisma.Veriqan.Orchestration.Tests.csproj"
```

The driver test (`Calibration_SyntheticCorpus_NoNewFalseFails`) runs automatically. Output:

- **Console:** pass/fail count + assertion details if any NewFails emerge.
- **Report:** `docs/qa/calibration/calibration-report.md` (overwritten each run).

The test skips gracefully if the PDF fixtures are absent (CI without binaries).

---

## Manifest schema (`corpus-manifest.json`)

Location: `Prisma/Fixtures/PRP2/corpus-manifest.json`.

```jsonc
{
  "corpusDir": ".",              // dir containing PDF fixtures, relative to manifest
  "specimens": [
    {
      "fileName": "...",         // PDF file name
      "label": "KnownSynthetic", // "KnownGood" | "KnownSynthetic" | "KnownBroken"
      "description": "...",
      "notes": "...",
      "bundle": {                // reference-data bundle params
        "institution": "...",
        "periodLabel": "...",
        "periodStart": "YYYY-MM-DD",
        "periodEnd": "YYYY-MM-DD",
        "productId": "...",
        "productName": "...",
        "productToken": "...",   // must match what the extractor reads from the PDF
        "aliases": ["..."],
        "annualOrdinaryRate": 0.28,
        "creditLine": 100000.00,
        "annualCommissionMxn": 1500.00,
        "requiredFontFamily": "Aptos",
        "bankingYearDays": 360
      },
      "intendedDefects": [       // KnownBroken only
        { "checkId": "LAW-TYPO-MINSIZE", "expectedVerdict": "Fail", "defectNote": "..." }
      ],
      "allowedFails": [          // CheckIds suppressed ONLY for omitted reference-data gaps
        { "checkId": "CL-42", "reason": "Omitted reference-data: movement dates vs. period" }
      ],
      "knownFixtureDefects": [   // Genuine non-compliance properties of the fixture PDF itself
        { "checkId": "CL-35", "reason": "Fixture PDF uses non-Aptos embedded fonts — genuine typography non-compliance" }
      ]
    }
  ]
}
```

### Specimen labels

| Label | Meaning | No-new-Fail guard applies? |
|-------|---------|---------------------------|
| `KnownGood` | Statement believed CONDUSEF-compliant. Must NOT have genuine format/typography/structure violations. | Yes |
| `KnownSynthetic` | Synthetic/placeholder PDF known to violate rules. Exercise the pipeline; never used as compliance evidence. | Yes (same guard) |
| `KnownBroken` | Deliberately-defective specimen. Must carry `intendedDefects`. | N/A (defects expected) |

### Two suppression buckets — cardinal rule

**Neither bucket is a carpet to sweep false-Fails under.**

- **`allowedFails`** — Fails permitted ONLY because *optional reference data was omitted from
  the synthetic bundle* (faked RFC/rate/period). These say nothing about PDF compliance.
  Acceptable reasons: omitted `PriorStatements`, `ExpectedTransactions`, `MandatoryLegends`, RFC entries, period/rate mismatch in faked data.

- **`knownFixtureDefects`** — Fails that are *genuine non-compliance properties of the fixture
  PDF itself* (sub-floor typography, non-Aptos font, missing mandatory sections, absent verbatim
  legends, text overlap, pagination). **A green test with these suppressed MUST NOT be read as
  proof that the statement is compliant.** Valid ONLY on `KnownSynthetic` or `KnownBroken` specimens.

No-new-Fail guard: `NewFails = Fails \ (allowedFails ∪ knownFixtureDefects ∪ intendedDefects)` must be empty.

---

## How to add a KnownGood specimen

1. Place the PDF in `Prisma/Fixtures/PRP2/`.
2. Add an entry to `corpus-manifest.json` with `"label": "KnownGood"`.
3. Run the harness with empty `allowedFails`. The test will fail and list actual Fail CheckIds.
4. For each Fail: decide whether it is an acceptable AllowedFail (reference-data gap or
   fixture limitation) and document the reason in the manifest.
5. Re-run until the driver test is green.

## How to add a KnownBroken specimen

1. Inject a deliberate defect into a copy of a real statement (or ask the owner to provide one).
2. Add an entry with `"label": "KnownBroken"` and populate `"intendedDefects"` with the
   CheckId that must Fail to detect the defect.
3. Run the harness. The harness will report DetectionRate per intendedDefect.
4. Only after both classes exist can threshold-evidence tables show a distribution split.

---

## Threshold → CheckId → DOF-numeral calibration map

### Typography floors (Acuerdo Anexo / Guía de llenado — Tipografía)

| CheckId | Quantity | Current threshold | Epsilon | DOF ref |
|---------|----------|------------------|---------|---------|
| `LAW-TYPO-MINSIZE` | Min body word PointSize (real words, len ≥ 2) | 8.0 pt | −0.25 pt | Acuerdo Anexo §Tipografía |
| `LAW-TYPO-MINSIZE` | Fecha-límite de pago word PointSize | 10.0 pt | −0.25 pt | Acuerdo Anexo §Tipografía |

**⚠️ Current measured min body size on ALL 3 synthetic specimens: 5.04 pt** (word `'11'`, page 1).
This is a confirmed rule Fail on known-good synthetic fixtures. The root cause is that the
synthetic PDF generator uses small-size numbers in page headers/footers. This is a fixture
authoring issue, not a false rule. See AllowedFails legend in `calibration-report.md`.

### Advertising placement (Acuerdo §12)

| CheckId | Quantity | Current threshold | Epsilon | DOF ref |
|---------|----------|------------------|---------|---------|
| `LAW-ADS-PLACEMENT` | §12 section char length | ≤ 700 chars (legal floor) / 805 chars (Fail gate) | n/a | Acuerdo §12 |

**Synthetic corpus probe:** §12 section text = **47 chars** across all 3 specimens → +758 chars margin.

### Section size cap (Acuerdo §17/§21/§28)

| CheckId | Quantity | Current threshold | Epsilon | DOF ref |
|---------|----------|------------------|---------|---------|
| `LAW-SEC-SIZECAP` | §17 page-fraction | ≤ 25 % | +0.02 (2 %) | Acuerdo §17 |
| `LAW-SEC-SIZECAP` | §21 page-fraction (optional) | ≤ 33 % | +0.02 (2 %) | Acuerdo §21 |
| `LAW-SEC-SIZECAP` | §28 page-fraction (optional) | ≤ 33 % | +0.02 (2 %) | Acuerdo §28 |

**Synthetic corpus probe:** §17 measured at **10.1 %** → well within cap.

### Interest rate (Acuerdo §19)

| CheckId | Quantity | Current threshold | Epsilon | DOF ref |
|---------|----------|------------------|---------|---------|
| `LAW-§19-INTERES` (or CL-19) | |Reported rate − expected rate| | ≤ rate tolerance (1.5 / 1.0 pp) | ±0.0005 | Acuerdo §19 |
| Currency checks | |Reported amount − computed amount| | ≤ 0.50 MXN | n/a | Acuerdo §8, §20 |

**Synthetic corpus probe (CL-19):** Observed ≈ 985–965 MXN across specimens → Pass with tolerance 0.50.

---

## Current carry-forward

| Issue | Priority | Owner action |
|-------|----------|-------------|
| **All thresholds uncalibrated** — 0 KnownBroken specimens | P0 | Acquire or synthesize at least one deliberately-broken fixture per calibratable rule |
| **LAW-TYPO-MINSIZE Fails on all 3 synthetic fixtures** (5.04 pt < 8 pt floor) | P1 | Investigate the synthetic PDF generator — the small-size text is real. Either fix the fixture or confirm the fixture is not representative of real statements |
| **CL-35 Fails on all 3 synthetic fixtures** (non-Aptos fonts) | P1 | Synthetic PDFs use a different font family. Either regenerate with Aptos or confirm non-Aptos is expected for these demos |
| **CL-31 Fails on all 3 synthetic fixtures** (pagination mismatch) | P1 | Pagination labels do not match document page count in synthetic PDFs |
| **CL-34 Fails on all 3 synthetic fixtures** (card number not on all pages) | P2 | Card number missing from some pages in synthetic PDFs |
| **LAW-§17-LEGENDS / LAW-§26-NOTAS / LAW-§27-GLOSARIO** low similarity (0.3–0.7) | P1 | Verbatim-match catalog text differs from what the synthetic PDF generator produces — update catalog or update fixtures |
| **LAW-SEC-PRESENCE / LAW-SEC-ORDER-GAP** on all specimens | P1 | Synthetic PDFs missing mandatory sections (§2–§5, §9, §10, §14, §15) and have non-standard section order |
