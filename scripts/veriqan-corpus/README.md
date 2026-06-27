# Veriqan VEC Demo Corpus — Anonymization Script

## Purpose

`anonymize.py` replaces all PII in real Banamex/Citibanamex PDF bank statements
with deterministic fictitious values, producing a demo corpus safe for Veriqan
integration testing and demos.

## Hard PII Rules (non-negotiable)

| What | Where |
|------|-------|
| This script (code only) | **In the repo** — `scripts/veriqan-corpus/` |
| Real source statements | **Outside the repo** — `~/Downloads/bank-statements-sorted/` |
| Real→fake mapping JSON | **Outside the repo** — `~/Downloads/vec-anonymization-map.json` |
| Anonymized output PDFs | **Outside the repo** — `~/Downloads/vec-corpus-staging/` |

**Never** `git add` PDFs, the mapping JSON, or any file from `~/Downloads/`.

## Requirements

```bash
pip install pymupdf pillow
# or inside a venv:
python3 -m venv .venv && source .venv/bin/activate
pip install pymupdf pillow
```

PyMuPDF ≥ 1.24, Pillow ≥ 10.

## Usage

### Single file

```bash
python anonymize.py \
  --in ~/Downloads/bank-statements-sorted/account-B-visa/2026-04.pdf \
  --out ~/Downloads/vec-corpus-staging/account-B-visa/2026-04.pdf
```

### Defect injection (from an anonymized master)

```bash
# Break a CL-21 arithmetic identity (alter a balance figure)
python anonymize.py \
  --in ~/Downloads/bank-statements-sorted/account-B-visa/2026-04.pdf \
  --out ~/Downloads/vec-corpus-staging/defects/bad-math-cl21.pdf \
  --inject math

# Wrong font on the "Estado de Cuenta" title → CL-35 fails
python anonymize.py \
  --in ~/Downloads/bank-statements-sorted/account-B-visa/2026-04.pdf \
  --out ~/Downloads/vec-corpus-staging/defects/bad-font-cl35.pdf \
  --inject font

# Rasterize all pages to images (no text layer) → text-density guard BLOCKED
python anonymize.py \
  --in ~/Downloads/bank-statements-sorted/account-B-visa/2026-04.pdf \
  --out ~/Downloads/vec-corpus-staging/defects/scanned.pdf \
  --inject scanned
```

### Batch (all 3 products × 4 months + SUT defect variants)

```bash
python anonymize.py --batch
```

This produces:
```
~/Downloads/vec-corpus-staging/
├── account-A-priority/
│   ├── 2026-02.pdf   # anonymized master
│   ├── 2026-03.pdf
│   ├── 2026-04.pdf
│   └── 2026-05.pdf
├── account-B-visa/         # folder name includes the last-4 from ~/Downloads (not in repo)
│   ├── 2026-03.pdf
│   ├── 2026-04.pdf   ← SUT intermediate month master
│   ├── 2026-05.pdf
│   └── 2026-06.pdf
├── account-C-mc/           # folder name includes the last-4 from ~/Downloads (not in repo)
│   ├── 2026-02.pdf
│   ├── 2026-03.pdf
│   ├── 2026-04.pdf
│   └── 2026-05.pdf
└── defects/
    ├── good.pdf          # copy of SUT master (baseline)
    ├── bad-math-cl21.pdf      # CL-21 arithmetic identity broken
    ├── bad-font-cl35.pdf      # CL-35 wrong-font injection
    └── scanned.pdf       # text-density BLOCKED (image-only, no text layer)
```

### Force mapping rebuild

If you update the source PDFs or add an account, rebuild the mapping:

```bash
python anonymize.py --batch --rebuild-mapping
```

## What gets replaced

| PII category | Method | Scope |
|---|---|---|
| Holder full name | Text-layer search+redact | All pages / all products |
| Personal RFC (AAAA######XX#) | Text-layer search+redact | All pages |
| Bank RFC (BNM840515VB1) | Text-layer search+redact | CFDI pages |
| 16-digit card number | Text-layer search+redact | Pages 2+ (CC) |
| 18-digit CLABE | Text-layer search+redact | All pages (checking) |
| Contract / checking account # | Text-layer search+redact | All pages (checking) |
| Debit card 16-digit | Text-layer search+redact | All pages (checking) |
| Branch / client # | Text-layer search+redact | All pages |
| Street address (3 lines) | Text-layer search+redact | Page 1 (checking) |
| Bank name / "Banamex" brand | Text-layer search+redact | All pages |
| Bank email / URLs | Text-layer search+redact | Footer pages |
| CC page-1 field values (card#, RFC, sucursal, cliente, CLABE) | Image-layer white-rect + overlay | Page 1 of CC statements only |
| Bank logo (top-left image) | Image-layer white-rect + placeholder | Page 1 |

### Known limitations

1. **AFP character images on CC page 1**: The values next to "Número de tarjeta",
   "RFC", "Número de sucursal", "Número de cliente", and "CLABE Interbancaria" on
   page 1 of credit-card statements are rendered character-by-character as raster
   image glyphs (AFP→PDF conversion artefact). The script covers these with a
   white rectangle and overlays fake values, but the coverage relies on hardcoded
   coordinate regions.  If Banamex ever changes page layout, these regions need
   recalibration.

2. **Transaction descriptions containing the holder name**: Lines such as
   `TRANSFERENCIA A ABEL BRIONES` contain a partial name. The script replaces
   full name and partial first+second-surname combinations but may miss
   non-standard truncations (e.g., `ABEL B.RAMIREZ`).

3. **Embedded digital signatures / QR / barcodes**: The CFDI pages contain XML
   digital signatures that encode the card number and RFC.  These appear in the
   text layer and are redacted; however, the CFDI digital signature integrity
   (SAT seal) will be broken. This is acceptable for demo corpus use.

4. **Logo fidelity**: The bank logo is covered with a plain white rectangle and
   text.  The placeholder does not match the visual design of the original form;
   it is sufficient for machine-readable tests but not for visual mockups.

5. **Non-text visual content**: Graphical elements (coloured background bands,
   table borders) are not altered. The form layout and visual identity remain
   Banamex-derived; use this corpus only for internal testing, not for publishing.

## Defect injection reference

| Variant | Veriqan rule broken | What changes |
|---|---|---|
| `bad-math-cl21.pdf` | CL-21 arithmetic identity | "El pago para no generar intereses" altered by +$0.44 — balance table and payment table disagree |
| `bad-font-cl35.pdf` | CL-35 font consistency | "Estado de Cuenta Mensual" title re-rendered in Courier (wrong font) |
| `scanned.pdf` | Text-density guard | All pages rasterized to 150-dpi images; text layer removed → `pdftotext` yields ~0 chars |

## SUT product

Default SUT for defect injection: **account-B (Visa)**, month **2026-04**
(second of four = intermediate month).  Override `SUT_PRODUCT_PREFIX` / `SUT_MONTH`
constants in the script if needed.  The prefix match (`account-B-visa`) avoids
embedding the real account last-4 in committed source.

## Library choices

| Library | Version | Role |
|---|---|---|
| PyMuPDF (`fitz`) | ≥ 1.24 | PDF text extraction, redaction annotation, image overlay, rasterisation |
| Pillow | ≥ 10 | Pixmap → PNG byte conversion for scanned-inject |

No network calls, no external services.  All processing is local.
