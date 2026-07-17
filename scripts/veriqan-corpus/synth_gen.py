#!/usr/bin/env python3
"""
synth_gen.py — E6.S6.2.1 synthetic Dummie-VEC estado-de-cuenta generator
==========================================================================
Builds a reproducible, code-generated clone of the "Dummie-VEC" statement
layout (the hand-built PowerPoint-exported fixture
``Prisma/Fixtures/PRP2/01+Dummie+VEC+jul_ago+20252.pdf`` that
``PdfPigStatementFieldExtractor`` is calibrated against) plus a god's-eye
JSON manifest of the expected extraction outcome for every field it covers.

Design authority (read before changing any coordinate in this file):
    docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md      (§4 coordinate
        flip formula, §5 field→(X,Y,token) placement table, §6 manifest schema,
        §9 S6.2.1 slice deliverables)
    docs/planning-artifacts/E6-S6.2-extractor-geometry-map.md          (band constants)

SCOPE (S6.2.1 — extraction-fidelity only, owner ruling 2026-07-06):
    - Targets the Dummie-VEC layout (540×780pt, right-column), NOT the real
      Banamex layout (612×792, left-column) — that is S6.2.2.
    - NO arithmeticChecks / verdict-level assertion — see the design doc §0/§9.
    - Only page-1 geometry is load-bearing; pages 2-8 are minimal filler.

Coordinate system (§4 of the design doc):
    PdfPig (read side, the extractor) uses PDF-native space: origin bottom-left,
    Y grows upward — this is what all the (X, Bottom) tuples below are expressed in.
    PyMuPDF (write side, this script) uses page space: origin top-left, Y grows
    downward — `page.insert_text` consumes this.  The flip is:

        fitz_y = PAGE_HEIGHT_PT - target_pdfpig_Bottom
        fitz_x = target_pdfpig_Left        (X is not inverted)

Token-fragmentation rule (the actual E2.3 killer — see design doc §3/§5.3):
    A token that must round-trip as ONE PdfPig word (e.g. "27.36%", "$67,796.35")
    MUST come from a single `insert_text` call with no internal characters split
    across separate calls.  Multiple SPACE-separated words in one call are safe —
    PdfPig tokenizes on whitespace exactly like any normal PDF text run (this is
    how the real proven Dummie-VEC fixture — a PowerPoint export — already works).

Usage:
    python synth_gen.py [--profile {dummievec,realbanamex,all}] [--write-index]
        Default "all" generates every profile's PDFs + manifests (S6.2.1-S6.2.5,
        13 specimens total) into Prisma/Fixtures/PRP2/synthetic/, and — because a
        "--profile all" run is a full regeneration of the standing corpus — also
        (re)writes the corpus index (see below).

        "--write-index" (E6.S6.2.6): index-only mode — write ONLY
        Prisma/Fixtures/PRP2/synthetic/corpus-manifest.json, a byte-deterministic
        index of the 13 standing specimens (id/pdf/manifest/profile/slice/defect/
        description), sourced from a hardcoded specimen table (CORPUS_SPECIMENS)
        — never by scanning the output directory (scanning would be OS/order-
        dependent and could index stale files) — and exit; no PDF/manifest is
        generated or byte-churned, and --profile is ignored:
            python synth_gen.py --write-index

Determinism: fixed seed, hardcoded period dates — no wall-clock dependency.
Re-running this script must reproduce byte-identical (or at minimum
word-geometry-identical) output — see design doc §9 acceptance criteria.

S6.2.2 (real-Banamex left-column profile) — additive, does NOT touch the
S6.2.1 Dummie-VEC token table/constants above. See design doc §10 build
sequence item #2 and docs/planning-artifacts/E6-S6.2-extractor-geometry-map.md.
Real-good.pdf coordinates were re-measured this session via PyMuPDF
(`Bottom = 792 - y1`) against
Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Extraction/PdfPigStatementFieldExtractor.cs
(ExtractResumenField's two-pass left-column fallback, ExtractNivelDeUsoField's
hard `Left >= 280` gate, ExtractTasaAndCat's label-anchored 60pt value-band scan).
"""

from __future__ import annotations

import argparse
import json
import random
import sys
from pathlib import Path
from typing import Any

try:
    import fitz  # PyMuPDF
except ImportError:
    sys.exit("PyMuPDF not found.  Run: pip install pymupdf")

# Reuse the validated fake-identity generators from anonymize.py (sibling script)
# rather than re-deriving Luhn/CLABE/RFC checksum logic — see design doc §9 deliverable 1.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from anonymize import (  # noqa: E402  (import after sys.path mutation, by design)
    _make_clabe,
    _make_pan,
    _make_rfc_personal,
    build_scanned_doc,  # generic rasterizer — reused as-is for the S6.2.3 'scanned' variant
)

# ─── Constants ────────────────────────────────────────────────────────────────

PAGE_WIDTH_PT = 540.0
PAGE_HEIGHT_PT = 780.0
PAGE_COUNT = 8

FONT_NAME = "helv"  # PyMuPDF base-14 Helvetica alias (no embedding needed)
FONT_SIZE = 8.0
COLOR_BLACK = (0.0, 0.0, 0.0)

SEED = 20260706
TOOL_VERSION = "0.1.0"
GENERATED_AT_UTC = "2026-07-06T00:00:00Z"  # hardcoded — deterministic, no wall-clock

REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = REPO_ROOT / "Prisma" / "Fixtures" / "PRP2" / "synthetic"
PDF_FILENAME = "s6211-baseline.pdf"
MANIFEST_FILENAME = "s6211-baseline.manifest.json"


# ─── Coordinate flip (design doc §4) ───────────────────────────────────────────

def fitz_point(x: float, pdfpig_bottom: float, page_height: float = PAGE_HEIGHT_PT) -> "fitz.Point":
    """Convert a PdfPig-space (Left, Bottom) target into a PyMuPDF insertion Point.

    `page_height` defaults to the S6.2.1 Dummie-VEC page height (780.0) so every
    existing call site is unaffected; the S6.2.2 profile passes 792.0 explicitly.
    """
    return fitz.Point(x, page_height - pdfpig_bottom)


def put(
    page: "fitz.Page",
    x: float,
    bottom: float,
    text: str,
    *,
    fontsize: float = FONT_SIZE,
    page_height: float = PAGE_HEIGHT_PT,
    fontname: str = FONT_NAME,
) -> None:
    """Place `text` as ONE insert_text call at PdfPig-space (x, bottom).

    `fontname` defaults to Helvetica; the S6.2.3 'font' defect variant passes a
    non-Helvetica base-14 alias (``"cour"``) for a single token so CL-35's
    font-consistency check (which scans every FontRun) fires.
    """
    page.insert_text(
        fitz_point(x, bottom, page_height),
        text,
        fontname=fontname,
        fontsize=fontsize,
        color=COLOR_BLACK,
    )


# ─── Synthetic identity values (fake, no PII — generated + checksum-valid) ─────
# Distinct from anonymize.py's own FAKE dict values (different demo persona) so the
# two synthetic corpora are never confused, but built with the SAME validated
# generator functions per the design doc's explicit reuse instruction.

_CARD_RAW = _make_pan(prefix="4567", last4="0006", length=16)
CARD_GROUPS = " ".join(_CARD_RAW[i : i + 4] for i in range(0, 16, 4))

CLABE_VALUE = _make_clabe(bank="002", plaza="180", account11="00000000077")

RFC_VALUE = _make_rfc_personal(name4="SNTH", dob_yymmdd="260101", homoclave="A1")

BRANCH_NUMBER = "0910"
CLIENT_NUMBER = "00654321"


# ─── Page 1 — field placement (design doc §5.1–§5.5) ───────────────────────────
# Each entry is (x, bottom, text).  One tuple == one insert_text call.
# Bottoms are PdfPig-space (bottom-left origin, points), copied verbatim from the
# design doc's measured placement table — DO NOT "clean up" apparent near-duplicate
# Y values between unrelated rows (e.g. 574.8 vs 576.8): those are the ACTUAL
# proven coordinates from the real hand-built Dummie-VEC fixture the tests already
# pass against today; band-tolerance interactions between them are accounted for
# by the extractor's label + regex-based token filtering, not by row separation.

PAGE1_TOKENS: list[tuple[float, float, str]] = [
    # --- §5.1 Header / identity block (right column) ---
    (295.9, 576.8, "Número de sucursal"),
    (414.5, 576.8, BRANCH_NUMBER),
    (295.9, 566.0, "Número de Tarjeta"),
    (414.5, 566.0, CARD_GROUPS),
    (295.9, 555.2, "CLABE Interbancaria"),
    (414.5, 555.2, CLABE_VALUE),
    (295.9, 544.4, "Número de cliente"),
    (414.5, 544.4, CLIENT_NUMBER),
    (295.9, 533.6, "RFC"),
    (414.5, 533.6, RFC_VALUE),
    (298.2, 597.7, "Fecha de Corte"),
    (405.7, 597.7, "04 de ago 2025"),
    # Client name + address (right block)
    (337.2, 678.2, "CARLOS MENDOZA VARGAS"),
    (337.2, 665.0, "AV REFORMA 1234 DESP 8"),
    (337.2, 651.8, "COL JUAREZ"),
    (337.2, 638.6, "06600 CIUDAD DE MEXICO, CDMX"),
    # --- §5.2 Left column ---
    (23.9, 644.4, "Tarjeta de Crédito BSSB"),
    (19.0, 585.6, "Periodo 5-jul-2025 al 04-ago-2025"),
    (19.0, 574.8, "Número de días en el periodo: 31 días"),
    (19.0, 564.0, "Fecha límite de pago lunes, 25-ago-2025"),
    (19.0, 553.2, "Pago para no generar intereses $32,446.69"),
    (19.0, 542.4, "Pago mínimo + compras $3,145.39"),
    (19.0, 520.8, "Pago mínimo: $2,160.00"),
    # --- §5.3 TASA / CAT (label-anchored, not blind-Y) ---
    (140.8, 288.3, "TASA DE INTERES ANUAL"),
    (25.7, 283.5, "CAT"),
    (140.8, 278.7, "ORDINARIA FIJA"),
    (27.6, 260.0, "28.86% sin IVA 27.36%"),
    # --- §5.4 RESUMEN DE CARGOS Y ABONOS DEL PERIODO ---
    (290.1, 373.7, "RESUMEN DE CARGOS Y ABONOS DEL PERIODO"),
    (283.6, 353.9, "Adeudo del periodo anterior"),
    (435.7, 353.9, "= $67,796.35"),
    (283.6, 343.1, "Cargos regulares (no a meses)"),
    (435.7, 343.1, "+ $31,461.30"),
    (283.6, 332.3, "Cargos compras a meses (capital)"),
    (435.7, 332.3, "+ $985.39"),
    (283.6, 321.5, "Monto de Intereses"),
    (435.7, 321.5, "+ $0.00"),
    (283.6, 310.7, "Monto de comisiones"),
    (435.7, 310.7, "+ $0.00"),
    (283.6, 299.9, "IVA de Intereses y comisiones"),
    (435.7, 299.9, "+ $0.00"),
    (283.6, 289.1, "Pagos y abonos"),
    (435.7, 289.1, "- $67,796.35"),
    # --- §5.5 NIVEL DE USO DE TU TARJETA + balances ---
    (290.1, 199.9, "NIVEL DE USO DE TU TARJETA"),
    (283.6, 180.1, "Saldo cargos regulares:"),
    (435.7, 180.1, "$ 32,446.69"),
    (283.6, 169.3, "Saldo cargos a meses:"),
    (435.7, 169.3, "$ 19,941.16"),
    (283.6, 158.5, "Saldo deudor total: $ 52,387.85"),
    (283.6, 136.9, "Crédito disponible: $ 47,612.15"),
]


# ─── Manifest (design doc §6.2) ────────────────────────────────────────────────
# Field values below are internally arithmetic-consistent (kept for a future
# S6.2.3 arithmeticChecks re-introduction — NOT asserted in S6.2.1):
#   67796.35 + 31461.30 + 985.39 + 0 + 0 + 0 - 67796.35 = 32446.69 (PagoParaNoGenerarIntereses)
#   32446.69 (SaldoCargosRegulares) + 19941.16 (SaldoCargosAMeses) = 52387.85 (SaldoDeudorTotal)
#   100000.00 (creditLine) - 52387.85 = 47612.15 (CreditoDisponible)

def build_manifest() -> dict[str, Any]:
    def extracted(value: float, clr_type: str = "decimal") -> dict[str, Any]:
        return {"value": value, "clrType": clr_type, "expectedStatus": "Extracted"}

    return {
        "schemaVersion": 1,
        "sourceProvenance": "synthetic",
        "generator": {
            "tool": "synth_gen.py",
            "toolVersion": TOOL_VERSION,
            "seed": SEED,
            "generatedAtUtc": GENERATED_AT_UTC,
        },
        "pdf": {
            "fileName": PDF_FILENAME,
            "pageWidthPt": PAGE_WIDTH_PT,
            "pageHeightPt": PAGE_HEIGHT_PT,
            "pageCount": PAGE_COUNT,
        },
        "defect": None,
        "bundle": {
            "productId": "TC-BSSB",
            "productName": "Tarjeta de Crédito BSSB",
            "periodStart": "2025-07-05",
            "periodEnd": "2025-08-04",
            "creditLine": 100000.00,
        },
        "fields": {
            "Tasa": extracted(0.2736),
            "Cat": extracted(0.2886),
            "PagoParaNoGenerarIntereses": extracted(32446.69),
            "AdeudoPeriodoAnterior": extracted(67796.35),
            "CargosRegularesNoMeses": extracted(31461.30),
            "CargosComprasAMesesCapital": extracted(985.39),
            "MontoIntereses": extracted(0.00),
            "MontoComisiones": extracted(0.00),
            "IvaInteresesYComisiones": extracted(0.00),
            "PagosYAbonos": extracted(67796.35),
            "SaldoCargosRegulares": extracted(32446.69),
            "SaldoCargosAMeses": extracted(19941.16),
            "SaldoDeudorTotal": extracted(52387.85),
            "CreditoDisponible": extracted(47612.15),
            "TotalCargos": {
                "value": None,
                "clrType": "decimal",
                "expectedStatus": "NotExtracted",
                "reason": "S6.2.1 does not populate a movements table; DESGLOSE totals deferred to a later slice",
            },
        },
        # C1.0a — baseline confidence floor (design doc "C1 intended-solution design"
        # decision 3): a clean, unambiguous positional pick must score >= 0.8 once the
        # geometric-plausibility scorer (C1.2) is armed. Recorded here now so the C1.0b
        # separation spike has a machine-readable floor for every clean specimen, not
        # just the adversarial ones.
        "confidenceExpectations": {
            "Cat": {"band": "high", "min": 0.8},
            "Tasa": {"band": "high", "min": 0.8},
        },
        "movements": [],
        # arithmeticChecks reintroduced at S6.2.3 (design §6.2): the clean baseline
        # PASSES both arithmetic identities — the verdict-level test asserts CL-21/CL-22
        # are NOT in FailCheckIds here, and ARE for the s6211-math variant.
        "arithmeticChecks": [
            {"checkId": "CL-21", "expectedOutcome": "Pass",
             "note": "5-core-sum 32446.69 == printed Pago 32446.69 (delta 0 <= $0.50)."},
            {"checkId": "CL-22", "expectedOutcome": "Pass",
             "note": "SaldoCargosRegulares 32446.69 == PagoParaNoGenerarIntereses 32446.69."},
        ],
        "knownFixtureDefects": [],
    }


# ─── PDF construction ───────────────────────────────────────────────────────────

def build_pdf() -> "fitz.Document":
    doc = fitz.open()

    page1 = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
    for x, bottom, text in PAGE1_TOKENS:
        put(page1, x, bottom, text)

    # Pages 2-8: minimal filler — only page-1 geometry is load-bearing for S6.2.1
    # (design doc §5.7 / §9).  A page label is emitted purely for human debugging
    # when eyeballing the PDF; it carries no extraction meaning.
    for page_num in range(2, PAGE_COUNT + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
        put(page, 20.0, PAGE_HEIGHT_PT - 30.0, f"S6.2.1 synthetic filler — page {page_num}")

    return doc


# ═══════════════════════════════════════════════════════════════════════════
# S6.2.2 — real-Banamex LEFT-column profile (612x792pt)
# ═══════════════════════════════════════════════════════════════════════════
# Additive to the S6.2.1 Dummie-VEC profile above — nothing in this section is
# read by build_pdf()/build_manifest()/PAGE1_TOKENS, so the S6.2.1 baseline
# geometry is byte-for-byte unaffected.
#
# Design authority: docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md
# §10 build-sequence item #2 (S6.2.2 scope), plus this session's ground-truth
# recon of Prisma/Fixtures/PRP2/demo/good.pdf (612x792, 9 pages) via PyMuPDF
# (`Bottom = 792 - y1`) cross-checked against
# PdfPigStatementFieldExtractor.cs's actual positional logic (not the design
# doc's illustrative numbers alone):
#
#   - ExtractResumenField: two-pass column scan.  Pass 1 (right column,
#     labelMinX=280) finds nothing for a left-column layout, so pass 2 (left
#     column: labelMinX=0, labelMaxX=280, amtMaxX=280) is what actually
#     resolves these 7 fields.  Real good.pdf measured: label Left=25.5,
#     amount Left=207.7 (comfortably < 280).
#   - ExtractNivelDeUsoField: HARD gate `if (Left < 280) continue;` — these 2
#     fields MUST be right-column.  Real good.pdf measured: label Left=303.3,
#     split-dollar "$" Left=460.8 + digits Left=467.5.
#   - ExtractSaldoDeudorTotal / ExtractCreditoDisponible: column-agnostic (no
#     Left restriction in the extractor), but placed right-column (Left=303.3)
#     to mirror real good.pdf's own layout for the fields it DOES carry
#     (Límite de crédito / Crédito disponible use this exact column).
#   - ExtractPagoParaNoGenerarIntereses: requires the 5-token run
#     "Pago" "para" "no" "generar" "intereses" (case-insensitive) with the
#     FIRST token's Left <= 200, then an amount within maxX=300 on the same
#     band.  Real good.pdf has this at Left=25.5 (as "...el pago para no
#     generar intereses...", amount Left≈218.7) — mirrored here without the
#     real fixture's leading "El pago mínimo" run-on sentence, for a clean
#     label boundary.
#   - ExtractTasaAndCat: label-anchored, column-agnostic, requires an
#     uppercase-or-any-case "TASA" and/or "CAT" token at Bottom<350, then scans
#     0-60pt BELOW that label row for percent tokens (1st=CAT, 2nd=TASA).
#     Real good.pdf's own TASA/CAT is a print-shop text-layer defect (absent) —
#     this is the field the S6.2.2 synthetic profile SUPPLIES that real
#     good.pdf cannot, exercising the same label-anchored scan on left-column
#     geometry (not previously exercised by the S6.2.1 Dummie-VEC clone, whose
#     TASA/CAT sits at different X but the same scan logic).
#
# Row spacing: every distinct logical row below is >=15pt apart in Bottom, well
# clear of the extractor's YBandTolerance=5pt band-merge threshold — no two
# unrelated rows can accidentally merge into one band.
#
# Field values are a NEW, internally-arithmetic-consistent set (distinct from
# the S6.2.1 baseline's numbers) so a later S6.2.3 slice can turn on
# arithmeticChecks without re-authoring:
#   45320.10 (Adeudo) + 18750.25 (CargosRegulares) + 1200.00 (CargosCompras)
#     + 350.75 (MontoIntereses) + 99.00 (MontoComisiones) + 71.96 (IVA)
#     - 45320.10 (PagosYAbonos) = 20471.96 (PagoParaNoGenerarIntereses)
#   22150.40 (SaldoCargosRegulares) + 3400.00 (SaldoCargosAMeses)
#     = 25550.40 (SaldoDeudorTotal)
#   60000.00 (creditLine) - 25550.40 (SaldoDeudorTotal) = 34449.60 (CreditoDisponible)

PAGE_WIDTH_PT_S622 = 612.0
PAGE_HEIGHT_PT_S622 = 792.0
PAGE_COUNT_S622 = 9  # mirrors real good.pdf's page count

SEED_S622 = 20260706
GENERATED_AT_UTC_S622 = "2026-07-06T00:00:00Z"  # hardcoded — deterministic, no wall-clock

PDF_FILENAME_S622 = "s622-realbanamex-baseline.pdf"
MANIFEST_FILENAME_S622 = "s622-realbanamex-baseline.manifest.json"

# ─── Page 1 — field placement (real-Banamex left-column clone) ────────────────
# Each entry is (x, bottom, text) in PdfPig space, one insert_text call per
# tuple (token-fragmentation rule — see module docstring / design doc §5.3).

PAGE1_TOKENS_S622: list[tuple[float, float, str]] = [
    # --- Pago para no generar intereses (left column, Left<=200 REQUIRED) ---
    (25.5, 450.0, "Pago para no generar intereses"),
    (220.0, 450.0, "$20,471.96"),
    # --- RESUMEN block, LEFT-COLUMN pass (label Left<280, amount Left<=280) ---
    # Sign + amount as one call each, fixed X per real good.pdf's column
    # alignment (label X=25.5, sign X=187.5, amount X=207.7 — all measured).
    (25.5, 400.0, "Adeudo del periodo anterior"),
    (187.5, 400.0, "="),
    (207.7, 400.0, "$45,320.10"),
    (25.5, 385.0, "Cargos regulares (no a meses)"),
    (187.5, 385.0, "+"),
    (207.7, 385.0, "$18,750.25"),
    (25.5, 370.0, "Cargos compras a meses (capital)"),
    (187.5, 370.0, "+"),
    (207.7, 370.0, "$1,200.00"),
    (25.5, 355.0, "Monto de Intereses"),
    (187.5, 355.0, "+"),
    (207.7, 355.0, "$350.75"),
    (25.5, 340.0, "Monto de comisiones"),
    (187.5, 340.0, "+"),
    (207.7, 340.0, "$99.00"),
    (25.5, 325.0, "IVA de Intereses y comisiones"),
    (187.5, 325.0, "+"),
    (207.7, 325.0, "$71.96"),
    (25.5, 310.0, "Pagos y abonos"),
    (187.5, 310.0, "-"),
    (207.7, 310.0, "$45,320.10"),
    # --- TASA / CAT (column-agnostic, label-anchored; Bottom<350) ---
    # Label row: "CAT" then "TASA DE INTERES ANUAL ORDINARIA FIJA" as one
    # space-separated call (both tokens land on the same Bottom).
    (100.0, 295.0, "CAT TASA DE INTERES ANUAL ORDINARIA FIJA"),
    # Value row 25pt below (within the 0-60pt scan window): CAT is the first
    # (leftmost) percent token, TASA ORDINARIA the second.
    (100.0, 270.0, "26.10% sin IVA 19.75%"),
    # --- NIVEL DE USO + balances, RIGHT column (Left>=280 REQUIRED for NIVEL) ---
    (303.3, 200.0, "Saldo cargos regulares:"),
    (460.8, 200.0, "$"),
    (467.5, 200.0, "22,150.40"),
    (303.3, 185.0, "Saldo cargos a meses:"),
    (460.8, 185.0, "$"),
    (467.5, 185.0, "3,400.00"),
    (303.3, 170.0, "Saldo deudor total:"),
    (460.8, 170.0, "$"),
    (467.5, 170.0, "25,550.40"),
    (303.3, 155.0, "Crédito disponible:"),
    (460.8, 155.0, "$"),
    (467.5, 155.0, "34,449.60"),
]


def build_manifest_s622() -> dict[str, Any]:
    def extracted(value: float, clr_type: str = "decimal") -> dict[str, Any]:
        return {"value": value, "clrType": clr_type, "expectedStatus": "Extracted"}

    return {
        "schemaVersion": 1,
        "sourceProvenance": "synthetic",
        "generator": {
            "tool": "synth_gen.py",
            "toolVersion": TOOL_VERSION,
            "seed": SEED_S622,
            "generatedAtUtc": GENERATED_AT_UTC_S622,
        },
        "pdf": {
            "fileName": PDF_FILENAME_S622,
            "pageWidthPt": PAGE_WIDTH_PT_S622,
            "pageHeightPt": PAGE_HEIGHT_PT_S622,
            "pageCount": PAGE_COUNT_S622,
        },
        "defect": None,
        "bundle": {
            "productId": "TC-BSSB-VISA",
            "productName": "Tarjeta de Crédito Banamex Visa (real-layout clone)",
            "periodStart": "2026-03-04",
            "periodEnd": "2026-04-01",
            "creditLine": 60000.00,
        },
        "fields": {
            "Tasa": extracted(0.1975),
            "Cat": extracted(0.2610),
            "PagoParaNoGenerarIntereses": extracted(20471.96),
            "AdeudoPeriodoAnterior": extracted(45320.10),
            "CargosRegularesNoMeses": extracted(18750.25),
            "CargosComprasAMesesCapital": extracted(1200.00),
            "MontoIntereses": extracted(350.75),
            "MontoComisiones": extracted(99.00),
            "IvaInteresesYComisiones": extracted(71.96),
            "PagosYAbonos": extracted(45320.10),
            "SaldoCargosRegulares": extracted(22150.40),
            "SaldoCargosAMeses": extracted(3400.00),
            "SaldoDeudorTotal": extracted(25550.40),
            "CreditoDisponible": extracted(34449.60),
            "TotalCargos": {
                "value": None,
                "clrType": "decimal",
                "expectedStatus": "NotExtracted",
                "reason": "S6.2.2 does not populate a movements table; DESGLOSE totals deferred to a later slice (same as S6.2.1)",
            },
        },
        # C1.0a — baseline confidence floor (Mary's non-negotiable: the realbanamex
        # LEFT-COLUMN code path must calibrate on its own, not only via dummievec). See
        # the matching comment on build_manifest() for the design authority.
        "confidenceExpectations": {
            "Cat": {"band": "high", "min": 0.8},
            "Tasa": {"band": "high", "min": 0.8},
        },
        "movements": [],
        "knownFixtureDefects": [],
    }


def build_pdf_s622() -> "fitz.Document":
    doc = fitz.open()

    page1 = doc.new_page(width=PAGE_WIDTH_PT_S622, height=PAGE_HEIGHT_PT_S622)
    for x, bottom, text in PAGE1_TOKENS_S622:
        put(page1, x, bottom, text, page_height=PAGE_HEIGHT_PT_S622)

    # Pages 2-9: minimal filler — only page-1 geometry is load-bearing (the
    # extractor's period/summary pass is page-1-only; see design doc §9/§5.7).
    for page_num in range(2, PAGE_COUNT_S622 + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT_S622, height=PAGE_HEIGHT_PT_S622)
        put(
            page,
            20.0,
            PAGE_HEIGHT_PT_S622 - 30.0,
            f"S6.2.2 synthetic filler — page {page_num}",
            page_height=PAGE_HEIGHT_PT_S622,
        )

    return doc


# ═══════════════════════════════════════════════════════════════════════════
# S6.2.3 — Defect-injection variants (off the S6.2.1 Dummie-VEC s6211 baseline)
# ═══════════════════════════════════════════════════════════════════════════
# Additive: nothing here mutates PAGE1_TOKENS / build_pdf() / build_manifest(),
# so the S6.2.1 baseline geometry is unaffected.  Each variant is a deterministic
# transform of the proven s6211 baseline (design authority: E6-S6.2 design doc
# §8 defect-injection + §10 build-sequence item #3 = "defect injection + verdict-level").
#
# INJECTION IS GENERATOR-NATIVE (a deliberate, documented deviation from design
# §8's "reuse anonymize.py's _inject_math/_inject_font as-is"): those helpers are
# calibrated to the REAL Banamex coordinate geometry (a $-token search box at
# x 190-280 / y 315-350) and to a title string ("Estado de Cuenta Mensual") that
# the s6211 Dummie-VEC layout does not contain — so they do not apply here.  The
# generator knows its own token coordinates exactly, so it perturbs / omits /
# re-fonts the specific token directly.  Only `build_scanned_doc` (a generic
# whole-document rasterizer) is genuinely layout-agnostic and IS reused verbatim.
#
# Why s6211 (not the s622 real-Banamex profile) carries the defect variants:
#   - s6211's Product token "Tarjeta de Crédito BSSB" is Extracted (Left=23.9<200)
#     and already resolves against the demo reference bundle's products.csv → the
#     verdict pipeline binds and runs CL-21/CL-22.  s622's Product is NotExtracted
#     (no "Tarjeta" token in its left-column layout) → UnknownProduct → the
#     pipeline short-circuits to ExtractionGap and rules never run.
#   - s6211's baseline satisfies BOTH arithmetic identities
#     (SaldoCargosRegulares == PagoParaNoGenerarIntereses == 32446.69), so a single
#     +$11.00 on the printed Pago figure cleanly trips CL-21 AND CL-22.

MATH_DELTA = 11.00  # +$11.00 fat-finger — well above the $0.50 CL-21/CL-22 legal tolerance
DEFECT_VARIANTS = ("math", "font", "scanned", "abstain")

# The one baseline token whose amount the 'math' variant perturbs (+$11.00).
_MATH_BASELINE_PAGO = "Pago para no generar intereses $32,446.69"
_MATH_DEFECT_PAGO = "Pago para no generar intereses $32,457.69"  # 32446.69 + 11.00

# The TASA/CAT block the 'abstain' variant omits entirely (design §8 abstain example):
# drops Tasa + Cat to NotExtracted while leaving every other field intact.
_ABSTAIN_DROP_TEXTS = frozenset({
    "TASA DE INTERES ANUAL",
    "CAT",
    "ORDINARIA FIJA",
    "28.86% sin IVA 27.36%",
})

# Extra Courier token for the 'font' variant — placed in an empty header zone
# (Bottom=720, above all field bands) so no field band is disturbed; its sole
# purpose is to introduce a non-Helvetica FontRun so CL-35 fails.
_FONT_DEFECT_TOKEN = (20.0, 720.0, "Estado de Cuenta")  # (x, bottom, text)


def _s6211_variant_tokens(variant: str) -> list[tuple[float, float, str]]:
    """Return the page-1 token list for a defect variant (transform of PAGE1_TOKENS)."""
    if variant == "abstain":
        return [t for t in PAGE1_TOKENS if t[2] not in _ABSTAIN_DROP_TEXTS]
    if variant == "math":
        return [
            (x, b, _MATH_DEFECT_PAGO if txt == _MATH_BASELINE_PAGO else txt)
            for (x, b, txt) in PAGE1_TOKENS
        ]
    # 'font' and 'scanned' keep the baseline tokens (font adds a Courier token in
    # build; scanned rasterizes the whole doc after building the baseline).
    return list(PAGE1_TOKENS)


def build_pdf_s6211_variant(variant: str) -> "fitz.Document":
    """Build one s6211 defect-variant PDF.  `variant` must be in DEFECT_VARIANTS."""
    if variant not in DEFECT_VARIANTS:
        raise ValueError(f"Unknown variant {variant!r}; expected one of {DEFECT_VARIANTS}")

    doc = fitz.open()
    page1 = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
    for x, bottom, text in _s6211_variant_tokens(variant):
        put(page1, x, bottom, text)

    if variant == "font":
        fx, fb, ftext = _FONT_DEFECT_TOKEN
        put(page1, fx, fb, ftext, fontname="cour")  # Courier — trips CL-35

    for page_num in range(2, PAGE_COUNT + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
        put(page, 20.0, PAGE_HEIGHT_PT - 30.0, f"S6.2.1 synthetic filler — page {page_num}")

    if variant == "scanned":
        # build_scanned_doc rasterizes every page → image-only PDF (no text layer).
        # It extracts 0 fields, so the extraction-coverage floor (Stage 2b) fires
        # ExtractionGap (InsufficientExtractionCoverage) first — before the later
        # text-layer floor is reached — halting the pipeline before any rule runs.
        scanned = build_scanned_doc(doc)
        doc.close()
        return scanned

    return doc


def _arith(check_id: str, outcome: str, note: str) -> dict[str, Any]:
    """One arithmeticChecks entry (design §6.2 note: checkId / expectedOutcome / note)."""
    return {"checkId": check_id, "expectedOutcome": outcome, "note": note}


def build_manifest_s6211_variant(variant: str) -> dict[str, Any]:
    """God's-eye manifest for an s6211 defect variant — baseline overlaid with the defect."""
    manifest = build_manifest()
    manifest["defect"] = variant
    manifest["pdf"] = dict(manifest["pdf"])
    manifest["pdf"]["fileName"] = f"s6211-{variant}.pdf"

    def not_extracted(reason: str) -> dict[str, Any]:
        return {"value": None, "clrType": "decimal", "expectedStatus": "NotExtracted", "reason": reason}

    if variant == "math":
        # The extractor STILL reads the (wrong) printed value correctly — only the
        # verdict differs.  CL-21 & CL-22 both fail (delta $11.00 > $0.50 tolerance).
        manifest["fields"]["PagoParaNoGenerarIntereses"] = {
            "value": 32457.69, "clrType": "decimal", "expectedStatus": "Extracted",
        }
        manifest["arithmeticChecks"] = [
            _arith("CL-21", "Fail", "Printed Pago +$11.00 vs 5-core-sum → delta $11.00 > $0.50."),
            _arith("CL-22", "Fail", "SaldoCargosRegulares 32446.69 vs injected Pago 32457.69 → delta $11.00."),
        ]
        manifest["knownFixtureDefects"] = []
    elif variant == "font":
        # Arithmetic untouched (CL-21/CL-22 still pass); a Courier run trips CL-35.
        manifest["arithmeticChecks"] = [
            _arith("CL-21", "Pass", "Arithmetic identity unchanged by the font defect."),
            _arith("CL-22", "Pass", "Arithmetic identity unchanged by the font defect."),
        ]
        manifest["knownFixtureDefects"] = [
            {"checkId": "CL-35", "expectedOutcome": "Fail",
             "reason": "A Courier token is injected in the header; CL-35 requires Helvetica (demo bundle)."},
        ]
    elif variant == "scanned":
        # Image-only PDF → every positional field NotExtracted; pipeline → ExtractionGap.
        for name in list(manifest["fields"].keys()):
            manifest["fields"][name] = not_extracted(
                "Image-only (rasterized) PDF extracts zero fields; the extraction-coverage floor "
                "(Stage 2b) short-circuits first with BlockReason.InsufficientExtractionCoverage "
                "before the text-layer floor is reached (ExtractionGap).")
        manifest["arithmeticChecks"] = []  # rules never run — no arithmetic outcome to assert
        # C1.0a: a "high confidence expected" floor makes no sense for a field that's
        # NotExtracted in this variant — drop it rather than carry a misleading claim.
        manifest["confidenceExpectations"] = {}
        manifest["knownFixtureDefects"] = [
            {"checkId": None, "expectedOutcome": "ExtractionGap",
             "reason": "Image-only PDF extracts 0 fields → extraction-coverage floor (Stage 2b, "
                       "default 10) not met → BlockReason.InsufficientExtractionCoverage → "
                       "VerdictSignal.ExtractionGap (fires before the text-layer floor)."},
        ]
    elif variant == "abstain":
        # TASA/CAT block omitted → Tasa+Cat NotExtracted; CL-21/CL-22 operands untouched.
        manifest["fields"]["Tasa"] = not_extracted("TASA/CAT block deliberately omitted (synthetic-only honest-abstention test).")
        manifest["fields"]["Cat"] = not_extracted("TASA/CAT block deliberately omitted (synthetic-only honest-abstention test).")
        # C1.0a: Tasa/Cat are NotExtracted here, so the baseline's "high confidence
        # expected" floor for them no longer applies — drop those two entries.
        manifest["confidenceExpectations"] = {
            k: v for k, v in manifest["confidenceExpectations"].items() if k not in ("Tasa", "Cat")
        }
        manifest["arithmeticChecks"] = [
            _arith("CL-21", "Pass", "Tasa/Cat are not CL-21 operands; arithmetic identity holds."),
            _arith("CL-22", "Pass", "Tasa/Cat are not CL-22 operands; arithmetic identity holds."),
        ]
        manifest["knownFixtureDefects"] = []

    return manifest


def _write_s6211_variants(output_dir: Path) -> None:
    for variant in DEFECT_VARIANTS:
        doc = build_pdf_s6211_variant(variant)
        pdf_path = output_dir / f"s6211-{variant}.pdf"
        doc.save(str(pdf_path), garbage=4, deflate=True)
        doc.close()
        print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

        manifest = build_manifest_s6211_variant(variant)
        manifest_path = output_dir / f"s6211-{variant}.manifest.json"
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"Wrote {manifest_path}")


# ═══════════════════════════════════════════════════════════════════════════
# S6.2.4 — Variance (seeded) over the S6.2.1 Dummie-VEC baseline
# ═══════════════════════════════════════════════════════════════════════════
# Additive. Proves the extractor is NOT overfit to the one hardcoded s6211 token
# table by re-emitting the baseline layout with (a) seeded value personas
# (product/holder/amounts/percents, kept arithmetic-consistent) so the golden
# round-trip asserts the extractor resolves NOVEL values (not the memorized
# baseline), and (b) a per-variant whole-page rigid Y-shift so absolute Y cannot
# be what the extractor keys on. Two EDGE variants deliberately cross a real
# tolerance to map the boundary.
#
# Ground-truth constants honored (recon 2026-07-07 vs PdfPigStatementFieldExtractor.cs):
#   - YBandTolerance = 5.0pt (:113). The whole-page shift is RIGID (one offset for
#     every page-1 token) so every intra- and inter-row Y-gap is preserved exactly
#     — no band ever merges or splits. |shift| <= 3pt and every baseline band is
#     > 3pt clear of every hard Y-window boundary (HeaderYMin=530, HeaderYMax=700,
#     ClientNameYMin=630, TASA scan ceiling=350), so the shift never crosses one.
#     (A rigid shift, NOT independent per-band jitter: the baseline interleaves
#     left/right-column rows only ~2pt apart in Y and packs same-column rows ~10.8pt
#     apart, so independent ±3pt jitter could merge previously-separate bands —
#     that is exactly the topology change we must NOT introduce. Rigid-shift is the
#     safe realization of the design's "±3pt Y jitter inside the 5pt tolerance".)
#   - Left/right column split = 280.0. The shift is Y-only, so no X boundary moves.
#   - Positional labels are matched by EXACT OrdinalIgnoreCase equality, NOT fuzzy,
#     NOT accent-folded (MatchesLabel :2478). So value tokens vary freely (the
#     extractor returns whatever is printed) while every label token stays verbatim
#     in the in-tolerance variants — and the EDGE variants weaponize exactly this.
#
# EDGE variants (design §10.4 "map the edges of what the extractor tolerates").
# They land on the extractor's TWO distinct honest failure modes — verified against
# the real extractor via the golden round-trip, not assumed:
#   - s6211-edge-band  — the Adeudo amount token is displaced +7pt off its label's
#     band (> YBandTolerance 5.0pt); the label IS still matched but no amount parses
#     in its band → AdeudoPeriodoAnterior honestly ExtractedInvalidFormat (implied-zero,
#     NOT NotExtracted — the Epic-5 F1 distinction), everything else intact.
#   - s6211-edge-label — the accent is dropped from the "Crédito" label token
#     ("Crédito disponible:" → "Credito disponible:"); exact-equality label match
#     never matches → CreditoDisponible honestly NotExtracted, everything else intact.

VARIANCE_SEEDS: dict[str, int] = {"a": 20260707, "b": 20260708, "c": 20260709}

# Surrounding-text pools (NOT asserted by the golden test — the manifest.fields set
# is financial-only; these vary the text the asserted amounts sit AMONG, so a real
# non-overfit resolution can be distinguished from baseline-string memorization).
_VARIANCE_PRODUCTS = (
    "Tarjeta de Crédito BSSB Oro",
    "Tarjeta de Crédito BSSB Platino",
    "Tarjeta de Crédito BSSB Clásica",
)
_VARIANCE_HOLDERS = (
    "MARIA GONZALEZ LOPEZ",
    "JOSE RAMIREZ SOTO",
    "ANA TORRES DIAZ",
)


def _money(value: float) -> str:
    """US/MX thousands-and-cents money string, e.g. 31461.3 -> '$31,461.30'."""
    return f"${value:,.2f}"


def _build_variance_persona(label: str, seed: int) -> dict[str, Any]:
    """A seeded, internally-arithmetic-consistent value persona for one variance PDF.

    The amount identities mirror the baseline's (design §6.2 note) so a later slice
    could turn CL-21/CL-22 on without re-authoring:
      pago == cargos_reg + cargos_compras (+ 0 + 0 + 0), with adeudo == pagos_abonos
      (they cancel); saldo_cargos_reg == pago (CL-22); saldo_deudor == the two saldos;
      credito_disp == credit_line - saldo_deudor.
    Interest/commission/IVA are kept at 0.00 (as the baseline) so the three identical
    '+ $0.00' tokens stay unambiguous for the full-string substitution map below.
    """
    rng = random.Random(seed)

    adeudo = round(rng.uniform(40_000, 70_000), 2)
    cargos_reg = round(rng.uniform(20_000, 40_000), 2)
    cargos_compras = round(rng.uniform(500, 2_000), 2)
    pago = round(cargos_reg + cargos_compras, 2)          # + 0 + 0 + 0; adeudo cancels pagos
    pagos_abonos = adeudo
    saldo_cargos_reg = pago                                # CL-22 identity
    saldo_cargos_meses = round(rng.uniform(10_000, 25_000), 2)
    saldo_deudor = round(saldo_cargos_reg + saldo_cargos_meses, 2)
    credit_line = round(saldo_deudor + rng.uniform(20_000, 60_000), 2)
    credito_disp = round(credit_line - saldo_deudor, 2)

    tasa_pct = round(rng.uniform(18.0, 32.0), 2)
    cat_pct = round(tasa_pct + rng.uniform(1.0, 6.0), 2)

    # HARD CAP |shift| <= 3.0pt is load-bearing: the tightest baseline clearance to a hard
    # Y-window boundary is the RFC band (533.6) at only 3.6pt above HeaderYMin=530 (Adeudo
    # 353.9 is 3.9pt above the TASA ceiling 350). Widening this range toward the 5pt
    # YBandTolerance would silently push those bands across their boundaries → dropped fields.
    y_shift = rng.choice((-3.0, -2.0, -1.0, 1.0, 2.0, 3.0))
    product_name = _VARIANCE_PRODUCTS[rng.randrange(len(_VARIANCE_PRODUCTS))]
    holder = _VARIANCE_HOLDERS[rng.randrange(len(_VARIANCE_HOLDERS))]

    # Full-token-text substitution map (baseline PAGE1_TOKENS text -> persona text).
    # Keyed on the WHOLE token string (never a substring), so it is unambiguous and
    # every unmatched token — crucially every EXACT positional label — passes through
    # verbatim. The three '+ $0.00' tokens are intentionally absent (they stay 0.00).
    token_subs = {
        "Tarjeta de Crédito BSSB": product_name,
        "CARLOS MENDOZA VARGAS": holder,
        "Pago para no generar intereses $32,446.69":
            f"Pago para no generar intereses {_money(pago)}",
        "28.86% sin IVA 27.36%": f"{cat_pct:.2f}% sin IVA {tasa_pct:.2f}%",
        "= $67,796.35": f"= {_money(adeudo)}",
        "+ $31,461.30": f"+ {_money(cargos_reg)}",
        "+ $985.39": f"+ {_money(cargos_compras)}",
        "- $67,796.35": f"- {_money(pagos_abonos)}",
        "$ 32,446.69": f"$ {saldo_cargos_reg:,.2f}",
        "$ 19,941.16": f"$ {saldo_cargos_meses:,.2f}",
        "Saldo deudor total: $ 52,387.85": f"Saldo deudor total: $ {saldo_deudor:,.2f}",
        "Crédito disponible: $ 47,612.15": f"Crédito disponible: $ {credito_disp:,.2f}",
    }

    return {
        "label": label,
        "seed": seed,
        "pdf_filename": f"s6211-var-{label}.pdf",
        "y_shift": y_shift,
        "product_name": product_name,
        "credit_line": credit_line,
        "token_subs": token_subs,
        "fields": {
            "Tasa": round(tasa_pct / 100.0, 4),
            "Cat": round(cat_pct / 100.0, 4),
            "PagoParaNoGenerarIntereses": pago,
            "AdeudoPeriodoAnterior": adeudo,
            "CargosRegularesNoMeses": cargos_reg,
            "CargosComprasAMesesCapital": cargos_compras,
            "MontoIntereses": 0.00,
            "MontoComisiones": 0.00,
            "IvaInteresesYComisiones": 0.00,
            "PagosYAbonos": pagos_abonos,
            "SaldoCargosRegulares": saldo_cargos_reg,
            "SaldoCargosAMeses": saldo_cargos_meses,
            "SaldoDeudorTotal": saldo_deudor,
            "CreditoDisponible": credito_disp,
        },
    }


def _apply_token_subs_and_shift(
    tokens: list[tuple[float, float, str]],
    token_subs: dict[str, str],
    y_shift: float,
) -> list[tuple[float, float, str]]:
    """Rebuild a page-1 token list: substitute value tokens by exact full-text match
    (labels pass through verbatim) and apply one RIGID whole-page Y-shift."""
    return [
        (x, round(bottom + y_shift, 4), token_subs.get(text, text))
        for (x, bottom, text) in tokens
    ]


def _build_s6211_doc(page1_tokens: list[tuple[float, float, str]]) -> "fitz.Document":
    """8-page Dummie-VEC-geometry doc from an explicit page-1 token list (pages 2-8
    are the same human-debug filler as build_pdf())."""
    doc = fitz.open()
    page1 = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
    for x, bottom, text in page1_tokens:
        put(page1, x, bottom, text)
    for page_num in range(2, PAGE_COUNT + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
        put(page, 20.0, PAGE_HEIGHT_PT - 30.0, f"S6.2.1 synthetic filler — page {page_num}")
    return doc


def build_manifest_variance(persona: dict[str, Any]) -> dict[str, Any]:
    """God's-eye manifest for one in-tolerance variance PDF — baseline structure with
    the 14 financial fields overridden to the persona's declared (literal) values."""
    def extracted(value: float) -> dict[str, Any]:
        return {"value": value, "clrType": "decimal", "expectedStatus": "Extracted"}

    m = build_manifest()
    m["pdf"] = dict(m["pdf"])
    m["pdf"]["fileName"] = persona["pdf_filename"]
    m["generator"] = dict(m["generator"])
    m["generator"]["seed"] = persona["seed"]
    m["bundle"] = dict(m["bundle"])
    m["bundle"]["productName"] = persona["product_name"]
    m["bundle"]["creditLine"] = persona["credit_line"]
    m["defect"] = None
    m["variance"] = {
        "label": persona["label"],
        "yShiftPt": persona["y_shift"],
        "note": "Seeded value persona + rigid whole-page Y-shift; every field must "
                "still resolve Extracted (extraction-fidelity, no verdict assertion).",
    }
    # Extraction-only slice: arithmeticChecks stay unconsumed → drop them (values ARE
    # kept consistent above, so a later slice can recompute+re-add without re-authoring).
    m["arithmeticChecks"] = []
    m["knownFixtureDefects"] = []
    for name, value in persona["fields"].items():
        m["fields"][name] = extracted(value)
    return m


def build_pdf_variance(persona: dict[str, Any]) -> "fitz.Document":
    tokens = _apply_token_subs_and_shift(PAGE1_TOKENS, persona["token_subs"], persona["y_shift"])
    return _build_s6211_doc(tokens)


# ─── Edge variants (map the tolerance boundary; baseline values, one mutation) ────

def build_pdf_edge_band() -> "fitz.Document":
    """Adeudo amount displaced +7pt off its label band (> YBandTolerance 5.0pt).
    Moved UP into the ~19.8pt empty gap below the RESUMEN title (353.9 -> 360.9),
    so it lands clear of every other row's band and is matched to no label."""
    tokens = []
    for x, bottom, text in PAGE1_TOKENS:
        if text == "= $67,796.35" and abs(bottom - 353.9) < 0.01:
            tokens.append((x, round(bottom + 7.0, 4), text))
        else:
            tokens.append((x, bottom, text))
    return _build_s6211_doc(tokens)


def build_pdf_edge_label() -> "fitz.Document":
    """Accent dropped from the 'Crédito' label token → exact-equality label match
    fails for CreditoDisponible (all other fields untouched)."""
    subs = {"Crédito disponible: $ 47,612.15": "Credito disponible: $ 47,612.15"}
    tokens = [(x, bottom, subs.get(text, text)) for (x, bottom, text) in PAGE1_TOKENS]
    return _build_s6211_doc(tokens)


def _build_manifest_edge(
    pdf_filename: str, edge_field: str, expected_status: str, reason: str
) -> dict[str, Any]:
    """Baseline manifest with exactly ONE field flipped to a non-Extracted status.

    The two edges land on DIFFERENT honest failure modes (verified against the real
    extractor, not assumed): a never-matched label → NotExtracted, but a matched
    label whose amount is missing/unparseable in-band → ExtractedInvalidFormat
    (the implied-zero/invalid-format distinction the Epic-5 F1 honesty work built)."""
    m = build_manifest()
    m["pdf"] = dict(m["pdf"])
    m["pdf"]["fileName"] = pdf_filename
    m["defect"] = "edge"
    m["arithmeticChecks"] = []
    m["fields"][edge_field] = {
        "value": None,
        "clrType": "decimal",
        "expectedStatus": expected_status,
        "reason": reason,
    }
    m["knownFixtureDefects"] = [{"field": edge_field, "reason": reason}]
    return m


def _write_s6211_variance(output_dir: Path) -> None:
    # In-tolerance robustness variants
    for label, seed in VARIANCE_SEEDS.items():
        persona = _build_variance_persona(label, seed)
        doc = build_pdf_variance(persona)
        pdf_path = output_dir / persona["pdf_filename"]
        doc.save(str(pdf_path), garbage=4, deflate=True)
        doc.close()
        print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

        manifest = build_manifest_variance(persona)
        manifest_path = output_dir / f"s6211-var-{label}.manifest.json"
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"Wrote {manifest_path}")

    # Edge variants (tolerance-boundary mapping). expected_status is the REAL extractor
    # behavior verified via the golden round-trip, not an assumption.
    edges = [
        (
            "s6211-edge-band.pdf",
            build_pdf_edge_band(),
            "AdeudoPeriodoAnterior",
            "ExtractedInvalidFormat",
            "Amount token '= $...' displaced +7pt off the 'Adeudo del periodo anterior' "
            "label band (> YBandTolerance 5.0pt); the label IS still matched but no amount "
            "parses in its band, so the extractor emits ExtractedInvalidFormat (implied-zero), "
            "NOT NotExtracted. Maps the vertical band-tolerance edge and the matched-label / "
            "missing-amount honesty distinction.",
        ),
        (
            "s6211-edge-label.pdf",
            build_pdf_edge_label(),
            "CreditoDisponible",
            "NotExtracted",
            "Accent dropped from the 'Crédito' label token ('Crédito disponible:' → "
            "'Credito disponible:'); positional labels are matched by exact "
            "OrdinalIgnoreCase equality (not accent-folded), so the label is NEVER matched "
            "→ NotExtracted. Maps the exact-label-match edge.",
        ),
    ]
    for pdf_filename, doc, field, expected_status, reason in edges:
        pdf_path = output_dir / pdf_filename
        doc.save(str(pdf_path), garbage=4, deflate=True)
        doc.close()
        print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

        manifest = _build_manifest_edge(pdf_filename, field, expected_status, reason)
        manifest_path = output_dir / f"{pdf_filename[:-4]}.manifest.json"
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"Wrote {manifest_path}")


# ═══════════════════════════════════════════════════════════════════════════
# S6.2.5 — DESGLOSE DE MOVIMIENTOS DEL PERIODO (populated movements table)
# ═══════════════════════════════════════════════════════════════════════════
# Additive: page 1 reuses PAGE1_TOKENS verbatim (unchanged canary — every S6.2.1
# baseline field must still resolve Extracted to its baseline value); the movements
# table is placed on page 2 (S6.2.1's/S6.2.4's page 2 is minimal filler, so nothing
# on that page has ever been load-bearing). Closes the TotalCargos/TotalAbonos gap
# the S6.2.1/S6.2.2/S6.2.4 manifests all explicitly deferred ("DESGLOSE totals
# deferred to a later slice").
#
# Ground truth verified this session against PdfPigStatementFieldExtractor.cs
# (ExtractMovements / TryParseMovementRow / TryParseTotalRow):
#   - DesgloseBandTolerance = 4.0pt → rows must be > 4pt apart (14pt used here).
#   - A DATA row requires BOTH an operation-date token at Left <= 95 matching
#     `^\d{1,2}-[a-záéíóúñü]+-\d{2,4}$` AND a sign token — IsSignToken accepts
#     "+" / "-" (ASCII) / "−" (U+2212) — at Left 423..480. We emit ASCII "+"/"-"
#     only: the base-14 'helv' font lacks a U+2212 glyph (PyMuPDF substitutes
#     U+00B7, which IsSignToken rejects). Amount tokens (Left >= 436) match
#     `^\$?([\d,]+(?:\.\d+)?)$`.
#   - A TOTAL row requires NO sign token in 423..480, >= 2 description-column
#     (Left 145..422) words, a "Total" token immediately followed by "cargos" or
#     "abonos" (OrdinalIgnoreCase), and an amount (Left >= 436).
#   - Sign placed at Left≈425 (inside 423..435, clear of the amount column's first
#     real token at Left≈436+) and amount at Left≈485 (unambiguously >= 436) so the
#     overlapping 436..480 sign/amount bands never collide.
#
# Totals tie to the S6.2.1 baseline's own RESUMEN values so the fixture is
# internally consistent, not just individually plausible:
#   Total cargos  31,461.30 + 985.39 = 32,446.69  (== baseline CargosRegularesNoMeses
#                                                     + CargosComprasAMesesCapital,
#                                                     == baseline PagoParaNoGenerarIntereses)
#   Total abonos  67,796.35            (== baseline PagosYAbonos == AdeudoPeriodoAnterior)

PDF_FILENAME_S6211_DESGLOSE = "s6211-desglose.pdf"
MANIFEST_FILENAME_S6211_DESGLOSE = "s6211-desglose.manifest.json"

# Page-2 DESGLOSE table tokens (PdfPig space). One tuple == one insert_text call
# (token-fragmentation rule). Rows 14pt apart — well clear of the 4pt band tolerance.
_S6211_DESGLOSE_PAGE2_TOKENS: list[tuple[float, float, str]] = [
    (18.3, 663.0, "DESGLOSE"),
    # Row 1 (charge) — Bottom=640
    (20.0, 640.0, "05-jul-2025"),
    (100.0, 640.0, "06-jul-2025"),
    (150.0, 640.0, "COMPRA REGULAR"),
    (425.0, 640.0, "+"),
    (485.0, 640.0, "$31,461.30"),
    # Row 2 (charge) — Bottom=626
    (20.0, 626.0, "05-jul-2025"),
    (100.0, 626.0, "06-jul-2025"),
    (150.0, 626.0, "COMPRA A MESES"),
    (425.0, 626.0, "+"),
    (485.0, 626.0, "$985.39"),
    # Row 3 (credit) — Bottom=612
    (20.0, 612.0, "05-jul-2025"),
    (100.0, 612.0, "06-jul-2025"),
    (150.0, 612.0, "PAGO RECIBIDO"),
    (425.0, 612.0, "-"),  # ASCII HYPHEN-MINUS U+002D — NOT U+2212: the base-14 'helv'
                          # font has no U+2212 glyph, so PyMuPDF silently substitutes
                          # U+00B7 MIDDLE DOT, which IsSignToken rejects → the credit row
                          # would be dropped. ASCII '-' is in WinAnsi (survives helv) and
                          # IsSignToken/IsCreditToken accept it (SignCreditAscii).
    (485.0, 612.0, "$67,796.35"),
    # Total cargos row — Bottom=594, no sign token
    (150.0, 594.0, "Total"),
    (195.0, 594.0, "cargos"),
    (485.0, 594.0, "$32,446.69"),
    # Total abonos row — Bottom=580, no sign token
    (150.0, 580.0, "Total"),
    (195.0, 580.0, "abonos"),
    (485.0, 580.0, "$67,796.35"),
]


def build_pdf_s6211_desglose() -> "fitz.Document":
    """S6.2.1 baseline page 1 (unchanged canary) + a populated DESGLOSE movements
    table on page 2 + plain filler pages 3-8."""
    doc = fitz.open()

    page1 = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
    for x, bottom, text in PAGE1_TOKENS:
        put(page1, x, bottom, text)

    page2 = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
    for x, bottom, text in _S6211_DESGLOSE_PAGE2_TOKENS:
        put(page2, x, bottom, text)

    for page_num in range(3, PAGE_COUNT + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
        put(page, 20.0, PAGE_HEIGHT_PT - 30.0, f"S6.2.5 synthetic filler — page {page_num}")

    return doc


def build_manifest_s6211_desglose() -> dict[str, Any]:
    """Baseline manifest (all 14 financial fields Extracted, unchanged values) with
    TotalCargos/TotalAbonos flipped from the baseline's NotExtracted stub to Extracted,
    plus a god's-eye `movements` array for documentation (the loader does not read it —
    StatementModelFieldAccessors.Map has no `movements` accessor — but it keeps the
    manifest an honest, complete record of what the PDF actually contains)."""
    def extracted(value: float, clr_type: str = "decimal") -> dict[str, Any]:
        return {"value": value, "clrType": clr_type, "expectedStatus": "Extracted"}

    manifest = build_manifest()
    manifest["pdf"] = dict(manifest["pdf"])
    manifest["pdf"]["fileName"] = PDF_FILENAME_S6211_DESGLOSE
    manifest["fields"]["TotalCargos"] = extracted(32446.69)
    manifest["fields"]["TotalAbonos"] = extracted(67796.35)
    manifest["movements"] = [
        {"opDate": "2025-07-05", "chargeDate": "2025-07-06",
         "description": "COMPRA REGULAR", "sign": "charge", "amount": 31461.30},
        {"opDate": "2025-07-05", "chargeDate": "2025-07-06",
         "description": "COMPRA A MESES", "sign": "charge", "amount": 985.39},
        {"opDate": "2025-07-05", "chargeDate": "2025-07-06",
         "description": "PAGO RECIBIDO", "sign": "credit", "amount": 67796.35},
    ]
    # arithmeticChecks unchanged from build_manifest() — CL-21/CL-22 both PASS, and
    # the DESGLOSE totals above tie to the exact same baseline figures they check.
    return manifest


def _write_s6211_desglose(output_dir: Path) -> None:
    doc = build_pdf_s6211_desglose()
    pdf_path = output_dir / PDF_FILENAME_S6211_DESGLOSE
    doc.save(str(pdf_path), garbage=4, deflate=True)
    doc.close()
    print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

    manifest = build_manifest_s6211_desglose()
    manifest_path = output_dir / MANIFEST_FILENAME_S6211_DESGLOSE
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {manifest_path}")


def _write_dummievec(output_dir: Path) -> None:
    doc = build_pdf()
    pdf_path = output_dir / PDF_FILENAME
    doc.save(str(pdf_path), garbage=4, deflate=True)
    doc.close()
    print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

    manifest = build_manifest()
    manifest_path = output_dir / MANIFEST_FILENAME
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {manifest_path}")


def _write_realbanamex(output_dir: Path) -> None:
    doc = build_pdf_s622()
    pdf_path = output_dir / PDF_FILENAME_S622
    doc.save(str(pdf_path), garbage=4, deflate=True)
    doc.close()
    print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

    manifest = build_manifest_s622()
    manifest_path = output_dir / MANIFEST_FILENAME_S622
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {manifest_path}")


# ═══════════════════════════════════════════════════════════════════════════
# S7.1 — header-image OCR product fixture (raster banner, top-30% header band)
# ═══════════════════════════════════════════════════════════════════════════
# Additive to the S6.2.2 real-Banamex profile above — reuses PAGE1_TOKENS_S622
# verbatim (unchanged canary: this specimen's 14 financial fields must resolve
# exactly like s622-realbanamex-baseline) and adds ONE new thing: a RASTERIZED
# product-name banner ("Tarjeta de Crédito COSTCO BANAMEX") inserted as a PNG
# image into the top 30%-of-page-height header band.
#
# Design authority: docs/planning-artifacts/veriqan-e7-s72-header-ocr-design-2026-07-08.md
# §4.F "S7.1 shared contract" — binding on this section:
#   - Fractional rect (x0=0.0, y0=0.0, x1=1.0, y1=0.30) of the page MediaBox,
#     expressed in page-space (top-left origin, Y grows down) at generation
#     time — this is ALREADY the native fitz/page-space convention (no
#     PdfPig-bottom flip needed for the crop rect itself; the flip in
#     fitz_point()/put() above only matters for PdfPig-space TEXT placement).
#   - The manifest's expected identity is carried in a NEW top-level
#     `headerOcrProduct` key (productId/productName/assertionRoute), NOT the
#     `fields` dict (StatementModelFieldAccessors.Map has no Product accessor
#     — adding one would break AssertGoldenRoundTripAsync's accessor lookup).
#   - Structurally distinct from `build_scanned_doc` (whole-page rasterize):
#     here only a small banner sub-image sits inside the header band; every
#     other token on the page is real, extractable PDF text.
#
# Byte-determinism: the banner PNG is produced from fixed inputs only (fixed
# text, fixed font, fixed font size, fixed render matrix) — no randomness, no
# wall-clock — so re-running the generator reproduces identical PNG bytes and
# therefore an identical PDF (verified: see module usage notes / DoD check
# "regenerate twice, git diff must be empty").
#
# Make-or-break spike (2026-07-08, throwaway script, deleted after use):
# fontsize=30 rendered at 300 DPI onto a small isolated canvas, inserted at
# (20,40)-(400,78)pt on the final 612x792 page, cropped at the production
# HeaderImageOcrStage.RenderDpi=150 / HeaderCropFraction=0.30 geometry, fed to
# `tesseract -l spa --psm 3` (PSM 3 == Tesseract .NET's PageSegMode.Auto, the
# exact mode TesseractHeaderProductOcrEngine uses) — OCR text came back
# 'Tarjeta de Crédito COSTCO BANAMEX\n', a clean match against
# HeaderImageOcrStage.cs's ProductHeadingPattern regex. GO.

PDF_FILENAME_S71 = "s71-header-ocr-costco.pdf"
MANIFEST_FILENAME_S71 = "s71-header-ocr-costco.manifest.json"

HEADER_OCR_PRODUCT_ID = "TC-COSTCO-BANAMEX"
HEADER_OCR_PRODUCT_NAME = "Tarjeta de Crédito COSTCO BANAMEX"

# Fractional header-band rect (design doc §4.F) — full page width, top 30% of
# MediaBox height. In fitz/page-space this needs no bottom-flip: (x0,y0) is
# already the top-left corner and y1 grows downward toward mid-page.
HEADER_BAND_X0_FRAC = 0.0
HEADER_BAND_Y0_FRAC = 0.0
HEADER_BAND_X1_FRAC = 1.0
HEADER_BAND_Y1_FRAC = 0.30

# Banner canvas + insertion geometry, pinned by the make-or-break spike above.
_BANNER_CANVAS_WIDTH_PT = 900.0
_BANNER_CANVAS_HEIGHT_PT = 90.0
_BANNER_RENDER_DPI = 300
_BANNER_FONT_SIZE = 30.0
_BANNER_INSERT_X0 = 20.0
_BANNER_INSERT_Y0 = 40.0
_BANNER_INSERT_WIDTH_PT = 380.0
_BANNER_INSERT_HEIGHT_PT = _BANNER_INSERT_WIDTH_PT * (_BANNER_CANVAS_HEIGHT_PT / _BANNER_CANVAS_WIDTH_PT)


def _build_header_banner_png(text: str) -> bytes:
    """Render `text` onto an isolated small canvas at high DPI and return PNG
    bytes — a genuine raster (get_pixmap -> tobytes("png")), mirroring
    anonymize.py's build_scanned_doc pixmap->PNG pattern but scoped to a single
    banner rather than a whole page. Deterministic: fixed text/font/matrix,
    no randomness, no wall-clock.
    """
    banner_doc = fitz.open()
    page = banner_doc.new_page(width=_BANNER_CANVAS_WIDTH_PT, height=_BANNER_CANVAS_HEIGHT_PT)
    page.insert_text(
        fitz.Point(20, 60),
        text,
        fontname=FONT_NAME,
        fontsize=_BANNER_FONT_SIZE,
        color=COLOR_BLACK,
    )
    pix = page.get_pixmap(matrix=fitz.Matrix(_BANNER_RENDER_DPI / 72, _BANNER_RENDER_DPI / 72), alpha=False)
    png_bytes = pix.tobytes("png")
    banner_doc.close()
    return png_bytes


def build_pdf_s71() -> "fitz.Document":
    """S6.2.2 real-Banamex page-1 tokens (unchanged canary) + a rasterized
    product-name banner inserted into the top-30% header band — the header
    band is otherwise empty (PAGE1_TOKENS_S622's topmost token sits at
    Bottom=450, i.e. fitz-y=342, well below the header band's fitz-y<=237.6
    ceiling), so the banner is isolated with no competing text at overlapping
    Y (the column-bleed risk the design doc's PSM.Auto choice guards against)."""
    doc = fitz.open()

    page1 = doc.new_page(width=PAGE_WIDTH_PT_S622, height=PAGE_HEIGHT_PT_S622)
    for x, bottom, text in PAGE1_TOKENS_S622:
        put(page1, x, bottom, text, page_height=PAGE_HEIGHT_PT_S622)

    banner_png = _build_header_banner_png(HEADER_OCR_PRODUCT_NAME)
    banner_rect = fitz.Rect(
        _BANNER_INSERT_X0,
        _BANNER_INSERT_Y0,
        _BANNER_INSERT_X0 + _BANNER_INSERT_WIDTH_PT,
        _BANNER_INSERT_Y0 + _BANNER_INSERT_HEIGHT_PT,
    )
    page1.insert_image(banner_rect, stream=banner_png)

    for page_num in range(2, PAGE_COUNT_S622 + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT_S622, height=PAGE_HEIGHT_PT_S622)
        put(
            page,
            20.0,
            PAGE_HEIGHT_PT_S622 - 30.0,
            f"S7.1 synthetic filler — page {page_num}",
            page_height=PAGE_HEIGHT_PT_S622,
        )

    return doc


def build_manifest_s71() -> dict[str, Any]:
    """God's-eye manifest: reuses the S6.2.2 baseline's 14 financial-field
    expectations verbatim (the banner is additive, not a defect — the extractor
    must still resolve every positional field exactly like s622-realbanamex-baseline),
    plus the S7.1-specific `headerOcrProduct` resolved-identity block (design
    doc §4.F). Deliberately does NOT add a `Product` key to `fields` —
    StatementModelFieldAccessors.Map has no Product accessor (see that file's
    remark); the resolved-identity assertion route is `headerOcrProduct`,
    consumed by a LiveOcr test, not the word-geometry [Theory]."""
    manifest = build_manifest_s622()
    manifest["pdf"] = dict(manifest["pdf"])
    manifest["pdf"]["fileName"] = PDF_FILENAME_S71
    manifest["bundle"] = dict(manifest["bundle"])
    manifest["bundle"]["productId"] = HEADER_OCR_PRODUCT_ID
    manifest["bundle"]["productName"] = HEADER_OCR_PRODUCT_NAME
    manifest["headerOcrProduct"] = {
        "productId": HEADER_OCR_PRODUCT_ID,
        "productName": HEADER_OCR_PRODUCT_NAME,
        "assertionRoute": "resolved-ProductId/LiveOcr",
    }
    return manifest


def _write_s71_header_ocr(output_dir: Path) -> None:
    doc = build_pdf_s71()
    pdf_path = output_dir / PDF_FILENAME_S71
    doc.save(str(pdf_path), garbage=4, deflate=True)
    doc.close()
    print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

    manifest = build_manifest_s71()
    manifest_path = output_dir / MANIFEST_FILENAME_S71
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {manifest_path}")


# ═══════════════════════════════════════════════════════════════════════════
# C1.0a — Adversarial GEOMETRY specimens for the TASA/CAT % band
# ═══════════════════════════════════════════════════════════════════════════
# Additive: nothing here mutates PAGE1_TOKENS / build_pdf() / build_manifest(), so the
# S6.2.1 Dummie-VEC baseline geometry is unaffected. Each variant is a deterministic
# single-token transform of the s6211 baseline's TASA/CAT value line (design authority:
# docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-confidence.md, "C1
# intended-solution design", story C1.0a).
#
# WHY these specimens exist: ExtractTasaAndCat (PdfPigStatementFieldExtractor.cs
# ~1245-1288) disambiguates CAT vs TASA PURELY by X-order — the leftmost '%' token in
# the value band below the labels is CAT, the next is TASA. There is no label/column
# cross-check. On the text layer a digit can't be garbled (PdfPig reads exact glyphs),
# so the only way this heuristic fails is GEOMETRIC: the wrong token sits leftmost.
#
# GOD'S-EYE `fields` CONTRACT (load-bearing — read before touching `fields` below):
#   For 'missing-order-marker' and 'decoy-percent' the extractor's positional read is
#   STILL CORRECT (Cat=0.2886 / Tasa=0.2736, matching the s6211 baseline) — these two
#   specimens only make the read geometrically LESS TRUSTWORTHY (a future confidence
#   scorer should mark them low-band); they do NOT flip today's extracted value.
#   `fields.Cat` / `fields.Tasa` therefore hold the TRUE (== actually-extracted) value,
#   exactly like every other specimen in this corpus.
#
#   For 'swap' the extractor's positional read is WRONG: it returns Cat=0.2736 /
#   Tasa=0.2886 (transposed) at confidence 1.0 — a genuine extraction-fidelity defect of
#   the CURRENT, unmodified extractor, NOT a printed-on-the-page data error (contrast
#   with the S6.2.3 'math' variant, where the wrong number really is what's printed).
#   `SyntheticGoldenRoundTripTests.AssertGoldenRoundTripAsync` (Extraction.Tests)
#   asserts `fields.<name>.value` against the extractor's ACTUAL output for every
#   specimen indexed in corpus-manifest.json — so `fields.Cat` / `fields.Tasa` here
#   MUST hold the actually-extracted (transposed, WRONG) values, or registering this
#   specimen in CORPUS_SPECIMENS would break that unrelated, already-green suite. The
#   GOD'S-EYE TRUE value (what the document's real CAT/TASA are — matching the s6211
#   baseline: Cat=0.2886 / Tasa=0.2736) is instead carried in the new
#   `geometryDefect.trueValue` block below — never silently conflated with `fields`.
#   The live-pipeline proof that this transposition is a CONFIDENT-WRONG verdict (not
#   just a wrong number) lives in Orchestration.Tests
#   (`SyntheticDefectVerdictE2ETests.Synthetic_C1Swap_FlipsCl10ToConfidentWrongFail`) —
#   the golden round-trip only proves extraction fidelity, deliberately including its
#   bugs; it is not, and cannot be, the make-or-break proof for this specimen.

_C1_TASACAT_BASELINE_TEXT = "28.86% sin IVA 27.36%"

# C1.0b — 'marker-displaced-swap' (the separation-spike's answer to the C1.0a CRITICAL
# FINDING). The central-'sin IVA' 'swap' variant above is GEOMETRICALLY INVISIBLE — 'sin
# IVA' sits equidistant between the two transposed tokens in both the clean baseline and
# the swap, so no positional signal can tell them apart (proven in the C1.0b spike). This
# variant is the REALISTIC counterpart: CONDUSEF/Banamex statements print 'sin IVA' as a
# legal qualifier bound to CAT specifically ("Costo Anual Total, sin IVA" — the marker
# always immediately follows CAT's printed value, see both the s6211 baseline "28.86% sin
# IVA 27.36%" and the real-Banamex s622 baseline "26.10% sin IVA 19.75%": in BOTH profiles
# the marker sits right after the FIRST (CAT) percent token, never after the second). A
# realistic swap is therefore a statement TEMPLATE that reorders which figure prints first
# on the value line (TASA before CAT) while the 'sin IVA' qualifier keeps tracking the true
# CAT (now second/rightmost) — e.g. a different template revision, not a random shuffle.
# ExtractTasaAndCat's fixed 'leftmost=CAT' heuristic misreads this exactly like 'swap'
# (Cat/Tasa transposed at confidence 1.0), but UNLIKE 'swap' the marker is now displaced
# off the extractor's CAT pick (pctTokens[0]) onto its TASA pick (pctTokens[1]) — a
# geometric difference C1.0b's sibling-adjacency signal can and does detect (see the
# GeometricPlausibilityScorerPrototype spike test).
C1_GEOMETRY_VARIANTS = ("swap", "missing-order-marker", "decoy-percent", "marker-displaced-swap")

_C1_TASACAT_VARIANT_TEXT: dict[str, str] = {
    # True Cat=28.86% / Tasa=27.36% (unchanged from baseline) but the two percent
    # tokens are transposed in X-order with 'sin IVA' kept between them —
    # ExtractTasaAndCat's pure X-order pick reads Cat=27.36% / Tasa=28.86% (WRONG),
    # both at confidence 1.0.
    "swap": "27.36% sin IVA 28.86%",
    # 'sin IVA' removed entirely; the two percent values stay in their TRUE X-order
    # (Cat still leftmost, Tasa still rightmost) so extraction is UNCHANGED/correct —
    # proves the sibling-marker signal is independent of the token-count signal
    # (design decision 1: signal #1 sin IVA presence != signal #2 token count).
    "missing-order-marker": "28.86% 27.36%",
    # A spurious 3rd '%' token appended after the true pair (a plausible on-statement
    # "IVA 16.00%" mention). pctTokens sorted by X are [28.86, 27.36, 16.00] — the
    # extractor still picks index 0/1 correctly (Cat/Tasa unchanged), but a 3-way
    # token competition is exactly what the competition-count signal should flag.
    "decoy-percent": "28.86% sin IVA 27.36% IVA 16.00%",
    # True Cat=28.86% / Tasa=27.36%, TASA printed FIRST (leftmost) with CAT second —
    # 'sin IVA' stays glued to the true CAT value (now rightmost), NOT centered between
    # the two tokens. ExtractTasaAndCat reads Cat=27.36% / Tasa=28.86% (WRONG, transposed
    # — same failure magnitude as 'swap'), but here the marker no longer sits between the
    # extractor's CAT pick (pctTokens[0]=27.36%, the true TASA) and its TASA pick
    # (pctTokens[1]=28.86%, the true CAT): it trails AFTER pctTokens[1] instead. That
    # displacement is the geometric tell.
    "marker-displaced-swap": "27.36% 28.86% sin IVA",
}

_C1_DECOY_TOKEN: dict[str, str | None] = {
    "swap": "sin IVA",
    "missing-order-marker": None,
    "decoy-percent": "16.00%",
    "marker-displaced-swap": "sin IVA",
}

_C1_GEOMETRY_DEFECT_TYPE: dict[str, str] = {
    "swap": "cat-tasa-swap",
    "missing-order-marker": "missing-order-marker",
    "decoy-percent": "decoy-percent",
    "marker-displaced-swap": "cat-tasa-swap-marker-displaced",
}

# Specimen id == pdf/manifest filename stem. 's-c1-swap' matches the id the C1 tracker
# and the make-or-break Orchestration.Tests case both name explicitly; the other two
# variants use their variant slug verbatim (no extra prefix needed — they are
# unambiguous within the standing corpus index).
_C1_VARIANT_ID: dict[str, str] = {
    "swap": "s-c1-swap",
    "missing-order-marker": "missing-order-marker",
    "decoy-percent": "decoy-percent",
    "marker-displaced-swap": "s-c1-swap-displaced",
}

# Variants whose extractor read is ACTUALLY WRONG (transposed Cat/Tasa at confidence
# 1.0) — as opposed to 'missing-order-marker'/'decoy-percent', where extraction stays
# correct and only the (future) confidence score should dip.
_C1_TRANSPOSED_VARIANTS = frozenset({"swap", "marker-displaced-swap"})


def _c1_variant_tokens(variant: str) -> list[tuple[float, float, str]]:
    """Return the page-1 token list for a C1 geometry variant (transform of
    PAGE1_TOKENS, substituting ONLY the TASA/CAT value line — every other baseline
    token, including the CAT/TASA LABEL row itself, is untouched)."""
    if variant not in C1_GEOMETRY_VARIANTS:
        raise ValueError(f"Unknown C1 geometry variant {variant!r}; expected one of {C1_GEOMETRY_VARIANTS}")
    text = _C1_TASACAT_VARIANT_TEXT[variant]
    return [
        (x, b, text if txt == _C1_TASACAT_BASELINE_TEXT else txt)
        for (x, b, txt) in PAGE1_TOKENS
    ]


def build_pdf_c1_variant(variant: str) -> "fitz.Document":
    """Build one C1.0a geometry-adversarial PDF off the s6211 Dummie-VEC baseline
    layout (540x780, right-column) — same page-1 geometry as the baseline everywhere
    except the single TASA/CAT value line."""
    doc = fitz.open()
    page1 = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
    for x, bottom, text in _c1_variant_tokens(variant):
        put(page1, x, bottom, text)
    for page_num in range(2, PAGE_COUNT + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT, height=PAGE_HEIGHT_PT)
        put(page, 20.0, PAGE_HEIGHT_PT - 30.0, f"C1.0a synthetic filler — page {page_num}")
    return doc


def build_manifest_c1_variant(variant: str) -> dict[str, Any]:
    """God's-eye manifest for one C1.0a geometry variant — baseline overlaid with the
    geometry defect. See the module-level comment above this section for the
    `fields` vs. `geometryDefect.trueValue` contract (load-bearing for 'swap' — do
    NOT "fix" fields.Cat/Tasa to the true value without re-reading that comment)."""
    if variant not in C1_GEOMETRY_VARIANTS:
        raise ValueError(f"Unknown C1 geometry variant {variant!r}; expected one of {C1_GEOMETRY_VARIANTS}")

    specimen_id = _C1_VARIANT_ID[variant]
    true_cat = 0.2886
    true_tasa = 0.2736

    manifest = build_manifest()
    manifest["pdf"] = dict(manifest["pdf"])
    manifest["pdf"]["fileName"] = f"{specimen_id}.pdf"
    manifest["fields"] = dict(manifest["fields"])

    if variant in _C1_TRANSPOSED_VARIANTS:
        # Extractor's ACTUAL (transposed, WRONG) read — see the module-level contract
        # comment. This is NOT the document's true CAT/TASA; see geometryDefect.trueValue.
        # Same transposition magnitude for 'swap' and 'marker-displaced-swap' — they differ
        # only in WHERE 'sin IVA' sits relative to the mis-picked tokens, not in the wrong
        # value itself.
        manifest["fields"]["Cat"] = {"value": true_tasa, "clrType": "decimal", "expectedStatus": "Extracted"}
        manifest["fields"]["Tasa"] = {"value": true_cat, "clrType": "decimal", "expectedStatus": "Extracted"}
    # else: 'missing-order-marker' / 'decoy-percent' — Cat/Tasa are already the true
    # (== actually-extracted) baseline values inherited from build_manifest() above.

    manifest["geometryDefect"] = {
        "type": _C1_GEOMETRY_DEFECT_TYPE[variant],
        "decoyToken": _C1_DECOY_TOKEN[variant],
        "trueValue": {"Cat": true_cat, "Tasa": true_tasa},
    }
    # Every C1.0a/C1.0b variant is designed to be geometrically ambiguous — the future
    # scorer (C1.2) should score Tasa/Cat low-band on all four, even though only
    # 'swap' and 'marker-displaced-swap' actually flip today's extracted value.
    manifest["confidenceExpectations"] = {
        "Cat": {"band": "low"},
        "Tasa": {"band": "low"},
    }
    # Tasa/Cat play no role in CL-21/CL-22's operands (Pago/Saldo money fields); the
    # geometry defect is confined to the % band, so the baseline's arithmetic outcome
    # is unaffected by every C1 variant, including the transposed ones.
    manifest["arithmeticChecks"] = [
        _arith("CL-21", "Pass", "Tasa/Cat geometry defect does not touch CL-21's money-field operands."),
        _arith("CL-22", "Pass", "Tasa/Cat geometry defect does not touch CL-22's money-field operands."),
    ]
    if variant == "swap":
        manifest["knownFixtureDefects"] = [
            {"field": "Cat", "reason": "ExtractTasaAndCat picks CAT/TASA by pure X-order with no label/"
             "column disambiguation; this specimen transposes the two percent tokens (keeping 'sin IVA' "
             "between them) so the extractor reads Cat=0.2736 (should be 0.2886) at confidence 1.0. See "
             "geometryDefect.trueValue for the correct identity."},
            {"field": "Tasa", "reason": "Same transposition: the extractor reads Tasa=0.2886 (should be "
             "0.2736) at confidence 1.0. See geometryDefect.trueValue for the correct identity, and "
             "SyntheticDefectVerdictE2ETests for the live-pipeline proof that this drives CL-10 from a "
             "legitimate Pass to a confident-WRONG Fail. NOTE: this transposition is GEOMETRICALLY "
             "INVISIBLE — 'sin IVA' sits equidistant between both tokens exactly like the clean "
             "baseline, so no positional signal can separate it from a legitimate read. It is kept as "
             "a negative control documenting that blind spot; see 's-c1-swap-displaced' for the "
             "realistic, separable counterpart the C1.0b spike calibrates against."},
        ]
    elif variant == "marker-displaced-swap":
        manifest["knownFixtureDefects"] = [
            {"field": "Cat", "reason": "Realistic CAT/TASA swap (C1.0b): the statement prints TASA "
             "before CAT on the value line (a template reordering, not a random shuffle) while 'sin "
             "IVA' keeps tracking the true CAT. ExtractTasaAndCat's 'leftmost=CAT' heuristic reads "
             "Cat=0.2736 (should be 0.2886) at confidence 1.0. UNLIKE 's-c1-swap', 'sin IVA' is "
             "displaced off the extractor's CAT pick onto its TASA pick — a geometric tell the C1.0b "
             "sibling-adjacency signal is calibrated to catch. See geometryDefect.trueValue."},
            {"field": "Tasa", "reason": "Same transposition: the extractor reads Tasa=0.2886 (should be "
             "0.2736) at confidence 1.0. See the Cat entry above for the displaced-marker rationale."},
        ]
    else:
        manifest["knownFixtureDefects"] = []

    return manifest


def _write_c1_geometry_variants(output_dir: Path) -> None:
    for variant in C1_GEOMETRY_VARIANTS:
        specimen_id = _C1_VARIANT_ID[variant]

        doc = build_pdf_c1_variant(variant)
        pdf_path = output_dir / f"{specimen_id}.pdf"
        doc.save(str(pdf_path), garbage=4, deflate=True)
        doc.close()
        print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

        manifest = build_manifest_c1_variant(variant)
        manifest_path = output_dir / f"{specimen_id}.manifest.json"
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"Wrote {manifest_path}")


# ═══════════════════════════════════════════════════════════════════════════
# C1.4 — Adversarial GEOMETRY specimen for the RESUMEN money-field slice
# ═══════════════════════════════════════════════════════════════════════════
# Extends the C1.0a/C1.0b TASA/CAT geometry-adversarial pattern above to the 7
# RESUMEN DE CARGOS Y ABONOS DEL PERIODO money fields
# (PdfPigStatementFieldExtractor.ExtractResumenField / ScanResumenColumn). Design
# authority: docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-
# confidence.md ("C1 intended-solution design", story C1.4) — signal #1
# (label-to-pick rank adjacency).
#
# WHY this specimen exists: ScanResumenColumn's `findLeftmost=true` amount pick
# selects the LEFTMOST value-shaped token at or after the matched label, within
# the pass's column bound. If an unrelated word/number (e.g. a footnote or an
# adjacent row's own content) lands within YBandTolerance=5.0pt of the target
# row's Y and its own X sits BEFORE the row's true amount, `findLeftmost` grabs
# the DECOY instead of the true value — a real number, in range, wrong slot, at
# confidence 1.0 today (exactly the CAT/TASA swap's failure shape, one column
# over). Mary's non-negotiable (C1 tracker): build this on BOTH the dummievec
# (right-column) AND realbanamex (left-column) profiles — ScanResumenColumn's
# two passes are genuinely different code paths (`labelMinX`/`labelMaxX`/
# `amtMaxX` bounds differ) and "dummievec does not occur in production".
#
# GOD'S-EYE `fields` CONTRACT (same shape as C1.0a's 'swap' — read before
# touching `fields` below): AdeudoPeriodoAnterior's `fields` entry holds the
# extractor's ACTUAL (decoy, WRONG) output — the value the golden round-trip
# suite (index-driven, SyntheticGoldenRoundTripTests) must observe — while the
# true printed value lives in `geometryDefect.trueValue`, never conflated with
# `fields`.
#
# SEPARABILITY (the C1.0a/C1.0b lesson, re-applied, TWICE): the decoy sits
# BEFORE the true amount in X (so `findLeftmost` picks it) but is separated
# from the label by FOUR intervening junk tokens (single digits — real
# footnote-marker shapes, excluded from being picked as the amount by the same
# `IsSingleDigit` check `FindAmountInBand` already applies, but each still
# occupies its own ordinal rank) — a real, structural label-to-pick RANK gap of
# 5, exceeding `MaxLabelToPickRankGap`=4. That threshold (not 2, as an earlier
# draft of this specimen assumed) is itself measured, not guessed — see
# `FieldCalibrationTable.ResumenDefault`'s remarks: two RESUMEN labelTokens
# arrays (`CargosRegularesNoMeses`, `IvaInteresesYComisiones`) deliberately
# match only a PREFIX of the printed label, leaving 2 trailing label words
# before the sign+amount, a real clean-pick rank gap of 4. A first attempt at
# this specimen used only 2 junk tokens ("Nota", "1") rendered close enough
# that PdfPig's own word-tokenizer MERGED them into one token ("Nota1") — an
# empirical lesson (verified via a throwaway PdfPig word-dump diagnostic, then
# reverted) that separate `put()` calls do NOT guarantee separate PdfPig words;
# each junk token below is spaced >= the observed safe gap (the corpus's own
# "sin"/"IVA" tokens stay separate at a measured 2.22pt gap — Helvetica 8pt's
# space-character advance width) to avoid the same trap.

PDF_FILENAME_C14_DECOY_RESUMEN = "decoy-resumen-amount.pdf"
MANIFEST_FILENAME_C14_DECOY_RESUMEN = "decoy-resumen-amount.manifest.json"

PDF_FILENAME_C14_DECOY_RESUMEN_REALBANAMEX = "decoy-resumen-amount-realbanamex.pdf"
MANIFEST_FILENAME_C14_DECOY_RESUMEN_REALBANAMEX = "decoy-resumen-amount-realbanamex.manifest.json"

# True Adeudo values (unchanged from each profile's own baseline).
_C14_TRUE_ADEUDO_DUMMIEVEC = 67796.35
_C14_TRUE_ADEUDO_REALBANAMEX = 45320.10

# The decoy's own (wrong, but in-range/parseable) amount value — distinct from
# every other field's value in each baseline so a mis-pick is unambiguous.
_C14_DECOY_AMOUNT_DUMMIEVEC = 5.00
_C14_DECOY_AMOUNT_REALBANAMEX = 0.85

# Four single-digit junk tokens (footnote-marker shapes) placed between the
# label and the decoy amount, each its own separate PdfPig word (>=2.3pt gaps
# — safely above the corpus's own measured 2.22pt "sin"/"IVA" safe-gap
# baseline) — see the module comment above for why this replaced an earlier,
# too-close 2-token design that PdfPig's tokenizer silently merged.
_C14_JUNK_DIGITS = ("1", "2", "3", "4")

_C14_RESUMEN_CLEAN_FIELDS = (
    "CargosRegularesNoMeses",
    "CargosComprasAMesesCapital",
    "MontoIntereses",
    "MontoComisiones",
    "IvaInteresesYComisiones",
    "PagosYAbonos",
)


def build_pdf_c14_decoy_resumen() -> "fitz.Document":
    """s6211 baseline + a decoy amount leaking into the AdeudoPeriodoAnterior
    row's Y-band (right-column pass: labelMinX=280, amtMaxX=unconstrained;
    Y=350.9, 3.0pt from the true row's 353.9 — within YBandTolerance=5.0pt).
    Four single-digit junk tokens sit between the label ('anterior' ends at
    X=382.32 in the baseline) and the decoy amount, each individually spaced
    (>=2.3pt gaps, verified via a PdfPig word-dump diagnostic — see the
    module comment above) so PdfPig tokenizes them as four SEPARATE words — a
    label-to-pick RANK gap of 5 (> MaxLabelToPickRankGap=4). The decoy amount
    itself sits at X=412.7 (ends ~432.7), left of the true amount's X=442.6,
    so ScanResumenColumn's findLeftmost pick grabs the decoy."""
    tokens = list(PAGE1_TOKENS)
    junk_x = [385.5, 392.3, 399.1, 405.9]
    for x, digit in zip(junk_x, _C14_JUNK_DIGITS):
        tokens.append((x, 350.9, digit))
    tokens.append((412.7, 350.9, f"${_C14_DECOY_AMOUNT_DUMMIEVEC:,.2f}"))
    return _build_s6211_doc(tokens)


def build_manifest_c14_decoy_resumen() -> dict[str, Any]:
    """God's-eye manifest for the dummievec RESUMEN decoy. Every RESUMEN field
    except AdeudoPeriodoAnterior stays at its true baseline value (clean picks,
    confidenceExpectations band 'high'); AdeudoPeriodoAnterior's `fields` entry
    holds the extractor's ACTUAL (decoy) output — see the module-level
    contract comment above this section."""
    manifest = build_manifest()
    manifest["pdf"] = dict(manifest["pdf"])
    manifest["pdf"]["fileName"] = PDF_FILENAME_C14_DECOY_RESUMEN
    manifest["fields"] = dict(manifest["fields"])
    manifest["fields"]["AdeudoPeriodoAnterior"] = {
        "value": _C14_DECOY_AMOUNT_DUMMIEVEC, "clrType": "decimal", "expectedStatus": "Extracted",
    }
    manifest["geometryDefect"] = {
        "type": "decoy-resumen-amount",
        "decoyToken": f"${_C14_DECOY_AMOUNT_DUMMIEVEC:,.2f}",
        "trueValue": {"AdeudoPeriodoAnterior": _C14_TRUE_ADEUDO_DUMMIEVEC},
    }
    manifest["confidenceExpectations"] = {
        "AdeudoPeriodoAnterior": {"band": "low"},
        **{name: {"band": "high", "min": 0.8} for name in _C14_RESUMEN_CLEAN_FIELDS},
    }
    # Extraction-only slice (same discipline as S6.2.4's variance manifest): the mis-pick's
    # downstream verdict impact (e.g. CL-21) is not asserted here — that belongs to a
    # verdict-level Orchestration test, not this extraction-fidelity fixture.
    manifest["arithmeticChecks"] = []
    manifest["knownFixtureDefects"] = [
        {"field": "AdeudoPeriodoAnterior", "reason": "A decoy amount ('1 2 3 4 $5.00') leaks "
         "into the label's Y-band (within YBandTolerance=5.0pt) at an X left of the true "
         "amount; ScanResumenColumn's findLeftmost pick (right-column pass) grabs the decoy "
         "instead of the true $67,796.35, at confidence 1.0 today. See geometryDefect.trueValue "
         "for the correct identity, and GeometricPlausibilityResumenCalibrationTests for the "
         "C1.4 rank-adjacency signal that catches it once armed."},
    ]
    return manifest


def _build_s622_doc(page1_tokens: list[tuple[float, float, str]]) -> "fitz.Document":
    """9-page real-Banamex-geometry doc from an explicit page-1 token list
    (mirrors _build_s6211_doc for the s622/realbanamex profile)."""
    doc = fitz.open()
    page1 = doc.new_page(width=PAGE_WIDTH_PT_S622, height=PAGE_HEIGHT_PT_S622)
    for x, bottom, text in page1_tokens:
        put(page1, x, bottom, text, page_height=PAGE_HEIGHT_PT_S622)
    for page_num in range(2, PAGE_COUNT_S622 + 1):
        page = doc.new_page(width=PAGE_WIDTH_PT_S622, height=PAGE_HEIGHT_PT_S622)
        put(page, 20.0, PAGE_HEIGHT_PT_S622 - 30.0, f"C1.4 synthetic filler — page {page_num}",
            page_height=PAGE_HEIGHT_PT_S622)
    return doc


def build_pdf_c14_decoy_resumen_realbanamex() -> "fitz.Document":
    """s622 baseline + a decoy amount leaking into the AdeudoPeriodoAnterior
    row's Y-band (LEFT-column pass: labelMinX=0/labelMaxX=280, amtMaxX=280,
    Y=397.0, 3.0pt from the true row's 400.0) — a genuinely different code
    path from the dummievec right-column variant above (Mary's
    non-negotiable). Same four-single-digit-junk-token construction as the
    dummievec variant (rank gap 5 > MaxLabelToPickRankGap=4), offset to this
    profile's label geometry ('anterior' ends at X~124.2 here vs. dummievec's
    382.32 — same word, same font, different label start X=25.5 vs. 283.6);
    the decoy amount sits at X=154.7, left of the true amount's X=207.7."""
    tokens = list(PAGE1_TOKENS_S622)
    junk_x = [127.5, 134.3, 141.1, 147.9]
    for x, digit in zip(junk_x, _C14_JUNK_DIGITS):
        tokens.append((x, 397.0, digit))
    tokens.append((154.7, 397.0, f"${_C14_DECOY_AMOUNT_REALBANAMEX:,.2f}"))
    return _build_s622_doc(tokens)


def build_manifest_c14_decoy_resumen_realbanamex() -> dict[str, Any]:
    """God's-eye manifest for the realbanamex RESUMEN decoy — same contract as
    build_manifest_c14_decoy_resumen(), off the s622 baseline instead."""
    manifest = build_manifest_s622()
    manifest["pdf"] = dict(manifest["pdf"])
    manifest["pdf"]["fileName"] = PDF_FILENAME_C14_DECOY_RESUMEN_REALBANAMEX
    manifest["fields"] = dict(manifest["fields"])
    manifest["fields"]["AdeudoPeriodoAnterior"] = {
        "value": _C14_DECOY_AMOUNT_REALBANAMEX, "clrType": "decimal", "expectedStatus": "Extracted",
    }
    manifest["geometryDefect"] = {
        "type": "decoy-resumen-amount",
        "decoyToken": f"${_C14_DECOY_AMOUNT_REALBANAMEX:,.2f}",
        "trueValue": {"AdeudoPeriodoAnterior": _C14_TRUE_ADEUDO_REALBANAMEX},
    }
    manifest["confidenceExpectations"] = {
        "AdeudoPeriodoAnterior": {"band": "low"},
        **{name: {"band": "high", "min": 0.8} for name in _C14_RESUMEN_CLEAN_FIELDS},
    }
    manifest["movements"] = []
    manifest["knownFixtureDefects"] = [
        {"field": "AdeudoPeriodoAnterior", "reason": "Same decoy-leak defect as "
         "'decoy-resumen-amount' (dummievec), on the LEFT-column pass instead: a decoy amount "
         "leaks into the label's Y-band at an X left of the true amount; ScanResumenColumn's "
         "findLeftmost pick grabs $0.85 instead of the true $45,320.10 at confidence 1.0 today. "
         "See geometryDefect.trueValue for the correct identity."},
    ]
    return manifest


def _write_c14_resumen_decoy(output_dir: Path) -> None:
    doc = build_pdf_c14_decoy_resumen()
    pdf_path = output_dir / PDF_FILENAME_C14_DECOY_RESUMEN
    doc.save(str(pdf_path), garbage=4, deflate=True)
    doc.close()
    print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

    manifest = build_manifest_c14_decoy_resumen()
    manifest_path = output_dir / MANIFEST_FILENAME_C14_DECOY_RESUMEN
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {manifest_path}")

    doc2 = build_pdf_c14_decoy_resumen_realbanamex()
    pdf_path2 = output_dir / PDF_FILENAME_C14_DECOY_RESUMEN_REALBANAMEX
    doc2.save(str(pdf_path2), garbage=4, deflate=True)
    doc2.close()
    print(f"Wrote {pdf_path2} ({pdf_path2.stat().st_size} bytes)")

    manifest2 = build_manifest_c14_decoy_resumen_realbanamex()
    manifest_path2 = output_dir / MANIFEST_FILENAME_C14_DECOY_RESUMEN_REALBANAMEX
    with open(manifest_path2, "w", encoding="utf-8") as f:
        json.dump(manifest2, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {manifest_path2}")


# ═══════════════════════════════════════════════════════════════════════════
# S6.2.6 — Standing corpus index (batch runner + corpus-manifest.json)
# ═══════════════════════════════════════════════════════════════════════════
# A single hardcoded table is the source of truth for what the 13-specimen
# standing corpus SHOULD contain — the index is built FROM this table, never
# by scanning OUTPUT_DIR (a directory scan is OS/filesystem-order-dependent
# and would silently index stale/leftover files instead of asserting the
# intended corpus). Keep this table in sync whenever a specimen is added,
# renamed, or retired by one of the _write_* functions above.
#
# `defect` mirrors each specimen's manifest["defect"] value where the writer
# sets one (math/font/scanned/abstain); the two S6.2.4 edge variants use their
# own short slugs ("edge-band"/"edge-label", vs. the manifest's shared "edge")
# since those are more informative for an at-a-glance index. `slice` records
# which epic slice's writer emits the specimen (see module docstring headers
# above for the full slice-by-slice narrative).

CORPUS_MANIFEST_FILENAME = "corpus-manifest.json"

CORPUS_SPECIMENS: list[dict[str, Any]] = [
    {
        "id": "s6211-baseline",
        "pdf": "s6211-baseline.pdf",
        "manifest": "s6211-baseline.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.1",
        "defect": None,
        "description": "Dummie-VEC 540x780 right-column baseline",
    },
    {
        "id": "s622-realbanamex-baseline",
        "pdf": "s622-realbanamex-baseline.pdf",
        "manifest": "s622-realbanamex-baseline.manifest.json",
        "profile": "realbanamex",
        "slice": "S6.2.2",
        "defect": None,
        "description": "Real-Banamex 612x792 left-column baseline clone",
    },
    {
        "id": "s6211-math",
        "pdf": "s6211-math.pdf",
        "manifest": "s6211-math.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.3",
        "defect": "math",
        "description": "Baseline + $11.00 fat-finger on printed Pago; trips CL-21/CL-22",
    },
    {
        "id": "s6211-font",
        "pdf": "s6211-font.pdf",
        "manifest": "s6211-font.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.3",
        "defect": "font",
        "description": "Baseline + injected Courier token; trips CL-35 font-consistency",
    },
    {
        "id": "s6211-scanned",
        "pdf": "s6211-scanned.pdf",
        "manifest": "s6211-scanned.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.3",
        "defect": "scanned",
        "description": "Baseline rasterized to an image-only PDF; 0 fields extracted, ExtractionGap",
    },
    {
        "id": "s6211-abstain",
        "pdf": "s6211-abstain.pdf",
        "manifest": "s6211-abstain.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.3",
        "defect": "abstain",
        "description": "Baseline with the TASA/CAT block omitted; honest NotExtracted abstention",
    },
    {
        "id": "s6211-var-a",
        "pdf": "s6211-var-a.pdf",
        "manifest": "s6211-var-a.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.4",
        "defect": None,
        "description": "Seeded value persona 'a' + rigid whole-page Y-shift (in-tolerance variance)",
    },
    {
        "id": "s6211-var-b",
        "pdf": "s6211-var-b.pdf",
        "manifest": "s6211-var-b.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.4",
        "defect": None,
        "description": "Seeded value persona 'b' + rigid whole-page Y-shift (in-tolerance variance)",
    },
    {
        "id": "s6211-var-c",
        "pdf": "s6211-var-c.pdf",
        "manifest": "s6211-var-c.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.4",
        "defect": None,
        "description": "Seeded value persona 'c' + rigid whole-page Y-shift (in-tolerance variance)",
    },
    {
        "id": "s6211-edge-band",
        "pdf": "s6211-edge-band.pdf",
        "manifest": "s6211-edge-band.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.4",
        "defect": "edge-band",
        "description": "Adeudo amount displaced +7pt off its label band (> YBandTolerance); "
                        "ExtractedInvalidFormat tolerance edge",
    },
    {
        "id": "s6211-edge-label",
        "pdf": "s6211-edge-label.pdf",
        "manifest": "s6211-edge-label.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.4",
        "defect": "edge-label",
        "description": "Accent dropped from the 'Crédito' label; NotExtracted exact-match edge",
    },
    {
        "id": "s6211-desglose",
        "pdf": "s6211-desglose.pdf",
        "manifest": "s6211-desglose.manifest.json",
        "profile": "dummievec",
        "slice": "S6.2.5",
        "defect": None,
        "description": "Baseline page 1 (unchanged canary) + a populated DESGLOSE movements "
                        "table on page 2",
    },
    {
        "id": "s71-header-ocr-costco",
        "pdf": "s71-header-ocr-costco.pdf",
        "manifest": "s71-header-ocr-costco.manifest.json",
        "profile": "header-ocr",
        "slice": "S7.1",
        "defect": None,
        "description": "S6.2.2 real-Banamex baseline + a rasterized 'Tarjeta de Crédito COSTCO "
                        "BANAMEX' banner in the top-30% header band (resolved-identity manifest "
                        "block headerOcrProduct, consumed by a LiveOcr test, not the fields gate)",
    },
    {
        "id": "s-c1-swap",
        "pdf": "s-c1-swap.pdf",
        "manifest": "s-c1-swap.manifest.json",
        "profile": "dummievec",
        "slice": "C1.0a",
        "defect": "geometry-swap",
        "description": "CAT/TASA percent tokens transposed (sin IVA kept between them); "
                        "ExtractTasaAndCat's pure X-order pick reads them WRONG at confidence 1.0 "
                        "(true values in geometryDefect.trueValue) — the C1 make-or-break specimen, "
                        "see SyntheticDefectVerdictE2ETests for the live confident-wrong CL-10 proof",
    },
    {
        "id": "missing-order-marker",
        "pdf": "missing-order-marker.pdf",
        "manifest": "missing-order-marker.manifest.json",
        "profile": "dummievec",
        "slice": "C1.0a",
        "defect": "geometry-missing-order-marker",
        "description": "'sin IVA' sibling marker removed; CAT/TASA values stay in their true X-order "
                        "(extraction unaffected) — proves the sibling-marker signal (#1) is independent "
                        "of the token-competition-count signal (#2)",
    },
    {
        "id": "decoy-percent",
        "pdf": "decoy-percent.pdf",
        "manifest": "decoy-percent.manifest.json",
        "profile": "dummievec",
        "slice": "C1.0a",
        "defect": "geometry-decoy-percent",
        "description": "Spurious 3rd percent token appended after the true CAT/TASA pair (3-way "
                        "token competition; extraction unaffected, an ambiguity signal only)",
    },
    {
        "id": "s-c1-swap-displaced",
        "pdf": "s-c1-swap-displaced.pdf",
        "manifest": "s-c1-swap-displaced.manifest.json",
        "profile": "dummievec",
        "slice": "C1.0b",
        "defect": "geometry-swap-marker-displaced",
        "description": "REALISTIC CAT/TASA swap: TASA prints before CAT on the value line (template "
                        "reordering) while 'sin IVA' keeps tracking the true CAT — displaces the "
                        "sibling marker off the extractor's (wrong) CAT pick, unlike the geometrically"
                        "-invisible 's-c1-swap' negative control. The C1.0b separation-spike's blind "
                        "holdout ambiguous specimen.",
    },
    {
        "id": "decoy-resumen-amount",
        "pdf": "decoy-resumen-amount.pdf",
        "manifest": "decoy-resumen-amount.manifest.json",
        "profile": "dummievec",
        "slice": "C1.4",
        "defect": "geometry-decoy-resumen-amount",
        "description": "A decoy amount ('1 2 3 4 $5.00') leaks into the AdeudoPeriodoAnterior "
                        "row's Y-band on the RIGHT-column pass; ScanResumenColumn's findLeftmost "
                        "pick grabs the decoy instead of the true $67,796.35, at confidence 1.0 "
                        "today (true value in geometryDefect.trueValue)",
    },
    {
        "id": "decoy-resumen-amount-realbanamex",
        "pdf": "decoy-resumen-amount-realbanamex.pdf",
        "manifest": "decoy-resumen-amount-realbanamex.manifest.json",
        "profile": "realbanamex",
        "slice": "C1.4",
        "defect": "geometry-decoy-resumen-amount",
        "description": "Same decoy-leak defect as 'decoy-resumen-amount', on the real-Banamex "
                        "LEFT-column pass instead — a genuinely different ScanResumenColumn code "
                        "path (labelMinX/labelMaxX/amtMaxX bounds differ); findLeftmost grabs "
                        "$0.85 instead of the true $45,320.10 at confidence 1.0 today",
    },
]


def build_corpus_index() -> dict[str, Any]:
    """The byte-deterministic standing-corpus index (design: E6.S6.2.6 part A).

    Sourced entirely from the hardcoded CORPUS_SPECIMENS table above — no
    filesystem scan — and sorted by `id` so repeated regeneration is
    byte-identical (no wall-clock, no dict/OS ordering dependency)."""
    specimens = sorted((dict(s) for s in CORPUS_SPECIMENS), key=lambda s: s["id"])
    return {
        "$comment": (
            "E6.S6.2.6 standing synthetic corpus index. Regenerate: python "
            "scripts/veriqan-corpus/synth_gen.py --write-index (index only, no "
            "PDF churn) or --profile all (full regeneration, index rewritten at "
            "the end). Word-geometry is the contract, NOT PDF bytes (PyMuPDF "
            "churns container bytes per regen); manifests are byte-deterministic."
        ),
        "generator": "scripts/veriqan-corpus/synth_gen.py",
        "specimens": specimens,
    }


def _write_corpus_index(output_dir: Path) -> None:
    index = build_corpus_index()
    index_path = output_dir / CORPUS_MANIFEST_FILENAME
    with open(index_path, "w", encoding="utf-8") as f:
        json.dump(index, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {index_path} ({len(index['specimens'])} specimens)")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--profile",
        choices=["dummievec", "realbanamex", "all"],
        default="all",
        help="Which layout profile to generate (default: all).",
    )
    parser.add_argument(
        "--write-index",
        action="store_true",
        help=(
            "Index-only mode: write only "
            "Prisma/Fixtures/PRP2/synthetic/corpus-manifest.json (the standing "
            "corpus index, sourced from the hardcoded CORPUS_SPECIMENS table) and "
            "exit — no PDF/manifest is generated or byte-churned. Ignores "
            "--profile. A '--profile all' run (the default with no flags) always "
            "(re)writes the index too, once generation completes, so the index "
            "stays in sync without a separate step in the common case."
        ),
    )
    args = parser.parse_args()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    if args.write_index:
        # Index-only mode: refresh the index cheaply without regenerating (and
        # thereby byte-churning) any of the 12 committed PDFs.
        _write_corpus_index(OUTPUT_DIR)
        return 0

    if args.profile in ("dummievec", "all"):
        _write_dummievec(OUTPUT_DIR)
        _write_s6211_variants(OUTPUT_DIR)  # S6.2.3 defect variants (math/font/scanned/abstain)
        _write_s6211_variance(OUTPUT_DIR)  # S6.2.4 variance variants (var-a/b/c + edge-band/label)
        _write_s6211_desglose(OUTPUT_DIR)  # S6.2.5 populated DESGLOSE movements table + totals
        _write_c1_geometry_variants(OUTPUT_DIR)  # C1.0a adversarial TASA/CAT geometry specimens
    if args.profile in ("realbanamex", "all"):
        _write_realbanamex(OUTPUT_DIR)
        _write_s71_header_ocr(OUTPUT_DIR)  # S7.1: header-image OCR product fixture
    if args.profile == "all":
        # C1.4: writes BOTH the dummievec and realbanamex decoy-resumen-amount specimens in one
        # call (Mary's non-negotiable — both profiles, one code path each) so a single-profile
        # `--profile dummievec` or `--profile realbanamex` run doesn't silently omit half the pair.
        _write_c14_resumen_decoy(OUTPUT_DIR)
        _write_corpus_index(OUTPUT_DIR)  # S6.2.6: (re)write the standing-corpus index

    return 0


if __name__ == "__main__":
    sys.exit(main())
