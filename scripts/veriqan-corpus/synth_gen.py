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
    python synth_gen.py
        Generates s6211-baseline.pdf + s6211-baseline.manifest.json into
        Prisma/Fixtures/PRP2/synthetic/.

Determinism: fixed seed, hardcoded period dates — no wall-clock dependency.
Re-running this script must reproduce byte-identical (or at minimum
word-geometry-identical) output — see design doc §9 acceptance criteria.
"""

from __future__ import annotations

import json
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

def fitz_point(x: float, pdfpig_bottom: float) -> "fitz.Point":
    """Convert a PdfPig-space (Left, Bottom) target into a PyMuPDF insertion Point."""
    return fitz.Point(x, PAGE_HEIGHT_PT - pdfpig_bottom)


def put(page: "fitz.Page", x: float, bottom: float, text: str, *, fontsize: float = FONT_SIZE) -> None:
    """Place `text` as ONE insert_text call at PdfPig-space (x, bottom)."""
    page.insert_text(
        fitz_point(x, bottom),
        text,
        fontname=FONT_NAME,
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
        "movements": [],
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


def main() -> int:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    doc = build_pdf()
    pdf_path = OUTPUT_DIR / PDF_FILENAME
    doc.save(str(pdf_path), garbage=4, deflate=True)
    doc.close()
    print(f"Wrote {pdf_path} ({pdf_path.stat().st_size} bytes)")

    manifest = build_manifest()
    manifest_path = OUTPUT_DIR / MANIFEST_FILENAME
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Wrote {manifest_path}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
