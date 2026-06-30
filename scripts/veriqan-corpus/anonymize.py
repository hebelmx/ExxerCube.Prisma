#!/usr/bin/env python3
"""
anonymize.py — Banamex bank-statement PII anonymizer → Veriqan VEC demo corpus
================================================================================
Replaces all PII in Banamex/Citibanamex PDF statements with deterministic
fictitious values.  The output PDFs are safe for demo/testing use.

HARD PII RULES
--------------
- This SCRIPT (code only) lives in the repo.
- Real statements, the mapping file, and all output PDFs stay under ~/Downloads.
- No real PII is hardcoded here; values are detected from the source PDFs and
  the mapping is persisted to MAPPING_PATH (outside the repo).

Usage:
    # Single file
    python anonymize.py --in /path/to/real.pdf --out /path/to/anon.pdf

    # With defect injection
    python anonymize.py --in anon.pdf --out bad.pdf --inject math|font|scanned

    # Batch: all 3 products × all months + 4 SUT defect variants
    python anonymize.py --batch

    # Re-read an existing mapping instead of auto-detecting
    python anonymize.py --batch --mapping /home/abel/Downloads/vec-anonymization-map.json

Environment overrides (or use CLI flags):
    STATEMENTS_DIR  default: ~/Downloads/bank-statements-sorted
    OUTPUT_DIR      default: ~/Downloads/vec-corpus-staging
    MAPPING_PATH    default: ~/Downloads/vec-anonymization-map.json
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import logging
import os
import re
import sys
from pathlib import Path
from typing import Any, Optional

try:
    import fitz  # PyMuPDF
except ImportError:
    sys.exit("PyMuPDF not found.  Run: pip install pymupdf")

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow not found.  Run: pip install pillow")

# ─── Validators & generators for synthetic PII ───────────────────────────────
# Defined BEFORE the FAKE dict so FAKE can call generators at module-load time
# and the assertion block can verify every value immediately after.


def luhn_ok(num: str) -> bool:
    """True iff `num` (digit string) passes the Luhn algorithm."""
    digits = [int(c) for c in num]
    s = 0
    for i, d in enumerate(reversed(digits)):
        if i % 2 == 1:
            d *= 2
            if d > 9:
                d -= 9
        s += d
    return s % 10 == 0


_CLABE_W = [3, 7, 1] * 6  # 18-element weight vector (Banxico/SAT 3-7-1)


def clabe_ok(num: str) -> bool:
    """True iff `num` is an 18-digit CLABE passing the Banxico mod-10 check."""
    if len(num) != 18 or not num.isdigit():
        return False
    s = sum((int(num[i]) * _CLABE_W[i]) % 10 for i in range(17))
    cd = (10 - (s % 10)) % 10
    return cd == int(num[17])


_RFC_TBL: dict[str, int] = {
    **{str(i): i for i in range(10)},
    'A': 10, 'B': 11, 'C': 12, 'D': 13, 'E': 14, 'F': 15, 'G': 16,
    'H': 17, 'I': 18, 'J': 19, 'K': 20, 'L': 21, 'M': 22, 'N': 23,
    '&': 24, 'O': 25, 'P': 26, 'Q': 27, 'R': 28, 'S': 29, 'T': 30,
    'U': 31, 'V': 32, 'W': 33, 'X': 34, 'Y': 35, 'Z': 36, ' ': 37, 'Ñ': 38,
}


def rfc_checkdigit_ok(rfc: str) -> bool:
    """
    True iff the last character of `rfc` is the correct SAT mod-11 check digit.
    Supports personal RFC (13 chars) and moral RFC (12 chars); body is
    right-justified in a 12-char field (moral RFCs get a leading space).
    """
    if len(rfc) < 2:
        return False
    body, expected = rfc[:-1], rfc[-1]
    padded = body.rjust(12)
    factors = list(range(13, 1, -1))  # [13, 12, 11, ..., 2]
    try:
        s = sum(_RFC_TBL[c] * f for c, f in zip(padded, factors))
    except KeyError:
        return False
    r = s % 11
    d = 11 - r
    computed = '0' if d == 11 else 'A' if d == 10 else str(d)
    return computed == expected


def _make_pan(prefix: str, last4: str, length: int = 16) -> str:
    """
    Return a Luhn-valid PAN: prefix + zero-padding + one adj digit + last4.
    Deterministic (first adj in 0-9 that satisfies Luhn).
    """
    mid_len = length - len(prefix) - len(last4) - 1
    if mid_len < 0:
        raise ValueError(f"prefix+last4 too long for length={length}")
    middle = "0" * mid_len
    for adj in range(10):
        candidate = prefix + middle + str(adj) + last4
        if luhn_ok(candidate):
            return candidate
    raise ValueError(f"No Luhn-valid PAN: prefix={prefix!r} last4={last4!r}")


def _make_clabe(bank: str, plaza: str, account11: str) -> str:
    """
    Return a valid 18-digit CLABE.
    bank: 3-digit bank code  (e.g. '002' = Citibanamex)
    plaza: 3-digit plaza code (e.g. '180' = CDMX centro)
    account11: 11-digit account number suffix
    """
    if not (len(bank) == 3 and len(plaza) == 3 and len(account11) == 11):
        raise ValueError("bank=3, plaza=3, account11=11 digits required")
    body = bank + plaza + account11
    s = sum((int(body[i]) * _CLABE_W[i]) % 10 for i in range(17))
    cd = (10 - (s % 10)) % 10
    return body + str(cd)


def _make_rfc_personal(name4: str, dob_yymmdd: str, homoclave: str) -> str:
    """
    Build a 13-char personal RFC with the correct SAT mod-11 check digit.
    name4: 4 capital letters from the holder name per SAT extraction rule
    dob_yymmdd: 6-digit birth date (YYMMDD)
    homoclave: 2 alphanumeric characters
    """
    body = name4 + dob_yymmdd + homoclave
    if len(body) != 12:
        raise ValueError(f"body must be 12 chars; got {body!r}")
    factors = list(range(13, 1, -1))
    s = sum(_RFC_TBL[c] * f for c, f in zip(body, factors))
    r = s % 11
    d = 11 - r
    check = '0' if d == 11 else 'A' if d == 10 else str(d)
    return body + check


# ─── Paths ───────────────────────────────────────────────────────────────────

_DOWNLOADS = Path.home() / "Downloads"

DEFAULT_STATEMENTS_DIR = Path(os.environ.get("STATEMENTS_DIR", _DOWNLOADS / "bank-statements-sorted"))
DEFAULT_OUTPUT_DIR     = Path(os.environ.get("OUTPUT_DIR",     _DOWNLOADS / "vec-corpus-staging"))
DEFAULT_MAPPING_PATH   = Path(os.environ.get("MAPPING_PATH",   _DOWNLOADS / "vec-anonymization-map.json"))

# Defect-injection SUT: account-B Visa, intermediate month (2nd of 4).
# Matched by PREFIX so the local folder's last-4 suffix is never hardcoded here
# (keeps real account digits out of the committed repo).
SUT_PRODUCT_PREFIX = "account-B-visa"
SUT_MONTH          = "2026-04"

# ─── Fake / demo identity (generated + validated — safe to commit) ───────────
#
# All numeric PII values are produced by the generators above and asserted
# valid by _assert_fake_values_valid() at the bottom of this section.
#
# RFC name-extraction rule for "CARLOS MENDOZA VARGAS":
#   Apellido paterno (MENDOZA) → 1st letter M + 1st internal vowel E
#   Apellido materno (VARGAS)  → 1st letter V
#   Nombre           (CARLOS)  → 1st letter C
#   → initials = MEVC

_VISA_PFX   = "4111"           # standard Visa BIN
_MC_PFX     = "5100"           # Mastercard 51xx BIN
_DEBIT_PFX  = "4100"           # Visa debit BIN
_CLABE_BANK = "002"            # Citibanamex bank code (Banxico registry)
_CLABE_PLZA = "180"            # CDMX main plaza

FAKE: dict[str, str] = {
    # Holder
    "holder_name":     "CARLOS MENDOZA VARGAS",
    "rfc_personal":    _make_rfc_personal("MEVC", "000101", "XX"),  # → MEVC000101XX5
    # Address (checking account header)
    "address_line1":   "AV REFORMA 1234 DESP 8",
    "address_line2":   "COL JUAREZ",
    "address_line3":   "06600 CIUDAD DE MEXICO, CDMX",
    # Account-A (checking): all values synthetic
    "contract_A":      "7700000000",                               # 10-digit contract
    "branch_A":        "0001",                                     # 4-digit branch
    "debit_card_A":    _make_pan(_DEBIT_PFX, "0003"),              # Luhn-valid; last-4=0003
    "checking_acct_A": "000000042",                                # 9-digit account
    "clabe_A":         _make_clabe(_CLABE_BANK, _CLABE_PLZA, "00000000001"),  # cd=2
    "client_A":        "00000001",                                 # 8-digit client code
    # Account-B (Visa credit — folder prefix: account-B-visa)
    # Fake last-4=0001 avoids the real-last-4 PII gate.
    "card_B":          _make_pan(_VISA_PFX, "0001"),               # Luhn-valid
    "branch_B":        "0002",
    "client_B":        "00000002",
    "clabe_B":         _make_clabe(_CLABE_BANK, _CLABE_PLZA, "00000000002"),  # cd=5
    "rfc_b_field":     _make_rfc_personal("MEVC", "000101", "XX"),
    # Account-C (MC credit — folder prefix: account-C-mc)
    # Fake last-4=0002 avoids the PII gate.
    "card_C":          _make_pan(_MC_PFX, "0002"),                 # Luhn-valid
    "branch_C":        "0003",
    "client_C":        "00000003",
    "clabe_C":         _make_clabe(_CLABE_BANK, _CLABE_PLZA, "00000000003"),  # cd=8
    "rfc_c_field":     _make_rfc_personal("MEVC", "000101", "XX"),
    # Bank identity (synthetic; no public checksum asserted for bank RFC)
    "bank_name_full":  "Banco Demo IndFusion, S.A.",
    "bank_name_sa":    "Banco Demo IndFusion, S.A., Integrante del Grupo Financiero IndFusion",
    "bank_group":      "Grupo Financiero IndFusion",
    "bank_short":      "IndFusion",
    "bank_rfc":        "BDI000101IDF",
    "bank_net":        "DemoNet",
    "bank_app":        "App IndFusion",
    "bank_phone":      "55 0000 0000",
    "bank_address":    "Av. Demo 999, Col. Centro, 01000, Ciudad de Mexico",
    "bank_une_email":  "une@bancoindifusion.demo.mx",
}


def _assert_fake_values_valid() -> None:
    """
    Assert every generated synthetic value passes its Mexican-rule validator.
    Raises AssertionError at import time if any value is invalid.
    """
    for key in ("rfc_personal", "rfc_b_field", "rfc_c_field"):
        v = FAKE[key]
        assert rfc_checkdigit_ok(v), f"FAKE[{key!r}]={v!r} fails rfc_checkdigit_ok"
    for key in ("debit_card_A", "card_B", "card_C"):
        v = FAKE[key]
        assert luhn_ok(v), f"FAKE[{key!r}]={v!r} fails luhn_ok"
    for key in ("clabe_A", "clabe_B", "clabe_C"):
        v = FAKE[key]
        assert clabe_ok(v), f"FAKE[{key!r}]={v!r} fails clabe_ok"


_assert_fake_values_valid()  # ← fails fast at import if any value is wrong

# ─── Real-PII patterns to auto-detect from source PDFs ───────────────────────

# CLABE: 18-digit string starting with known bank prefixes
_RE_CLABE   = re.compile(r"\b(\d{18})\b")
# RFC personal: 4 letters + 6 digits + 2-3 alphanums (13 chars)
_RE_RFC_PER = re.compile(r"\b([A-Z]{4}\d{6}[A-Z0-9]{2,3})\b")
# 16-digit card / account number
_RE_16DIGIT = re.compile(r"\b(\d{16})\b")
# 10-digit contract (checking)
_RE_10DIGIT = re.compile(r"\b(\d{10})\b")

# RFC prefixes that belong to the bank or tax authority (not personal)
_INSTITUTIONAL_RFC_PREFIXES = ("BNM", "CEC", "SAT", "SHC", "BDI")

# Patterns for bank brand replacement (longest first to avoid partial matches)
_BANK_REPLACEMENTS: list[tuple[str, str]] = [
    # Full legal name variants
    (
        "Banco Nacional de México, S.A., Integrante del Grupo Financiero Banamex",
        FAKE["bank_name_sa"],
    ),
    (
        "Banco Nacional de México S.A. Integrante del Grupo Financiero Banamex",
        FAKE["bank_name_sa"],
    ),
    ("Banco Nacional de México, S.A.",    FAKE["bank_name_full"]),
    ("Banco Nacional de México",          FAKE["bank_name_full"]),
    ("Grupo Financiero Banamex",          FAKE["bank_group"]),
    ("App Banamex",                       FAKE["bank_app"]),
    ("BancaNet",                          FAKE["bank_net"]),
    ("une@banamex.com",                   FAKE["bank_une_email"]),
    # Bank RFCs (appear in CFDI invoice pages)
    ("BNM840515VB1",                      FAKE["bank_rfc"]),
    # After full-name replacements, short token
    ("Banamex",                           FAKE["bank_short"]),
]

# ─── Logging ─────────────────────────────────────────────────────────────────

logging.basicConfig(
    level=logging.INFO,
    format="%(levelname)s  %(message)s",
)
log = logging.getLogger(__name__)

# ─── Product-type detection ───────────────────────────────────────────────────

def detect_product_type(doc: fitz.Document) -> str:
    """Return 'checking' or 'credit_card' by scanning page 1 text."""
    p0_text = doc[0].get_text()
    if "Número de contrato" in p0_text or "Tarjeta de Débito" in p0_text:
        return "checking"
    return "credit_card"


def detect_account_label(statements_dir: Path, pdf_path: Path) -> str:
    """Derive account label (e.g. 'B') from parent folder name."""
    parent = pdf_path.parent.name  # e.g. 'account-B-visa'
    if "account-A" in parent:
        return "A"
    if "account-B" in parent:
        return "B"
    if "account-C" in parent:
        return "C"
    return "?"


# ─── Auto-detection of real PII from a PDF ───────────────────────────────────

def extract_real_pii(doc: fitz.Document, product_type: str) -> dict[str, str]:
    """
    Read real PII values from the PDF text layer.
    Returns a dict suitable for inclusion in the mapping file.
    Note: for credit-card page-1 image-layer fields (card#, RFC, CLABE on p.1)
    we rely on the same values appearing in text on other pages.
    """
    pii: dict[str, str] = {}

    # Collect all text across all pages
    full_text = "\n".join(doc[i].get_text() for i in range(len(doc)))

    # Holder name: appears right after a statement-number-like prefix on p1
    p0_text = doc[0].get_text()
    for line in p0_text.splitlines():
        stripped = line.strip()
        # Name line: all caps, 2-4 words, letters only (no digits)
        if re.match(r"^[A-ZÁÉÍÓÚÑÜ]{2,}(?:\s+[A-ZÁÉÍÓÚÑÜ]{2,}){1,3}$", stripped):
            if len(stripped) > 8:
                pii.setdefault("holder_name", stripped)
                break

    # RFC personal: only search in checking account text.
    # In credit-card statements the personal RFC appears ONLY in image-layer on
    # page 1 (AFP character images), so it is NOT in the text layer and must not
    # be guessed from vendor RFCs in transaction descriptions.
    if product_type == "checking":
        # Scan first page text only to avoid picking up vendor RFCs from transactions
        p0_lines = p0_text.splitlines()
        for line in p0_lines:
            for m in _RE_RFC_PER.finditer(line):
                rfc = m.group(1)
                if not any(rfc.startswith(pfx) for pfx in _INSTITUTIONAL_RFC_PREFIXES):
                    pii.setdefault("rfc_personal", rfc)
                    break
            if "rfc_personal" in pii:
                break

    # Bank RFC
    for m in _RE_RFC_PER.finditer(full_text):
        rfc = m.group(1)
        if rfc.startswith("BNM"):
            pii.setdefault("bank_rfc", rfc)
            break

    if product_type == "checking":
        # All numeric IDs appear directly in text for checking
        for line in p0_text.splitlines():
            stripped = line.strip()
            if _RE_CLABE.match(stripped):
                pii.setdefault("clabe", stripped)
            if _RE_16DIGIT.match(stripped):
                pii.setdefault("debit_card", stripped)
            if _RE_10DIGIT.match(stripped) and len(stripped) == 10:
                pii.setdefault("contract", stripped)
            if re.match(r"^\d{9}$", stripped):
                pii.setdefault("checking_acct", stripped)
            if re.match(r"^\d{4}$", stripped):
                pii.setdefault("branch", stripped)
            if re.match(r"^\d{8}$", stripped):
                pii.setdefault("client", stripped)

        # Address: 3 lines after holder name
        lines = [l.strip() for l in p0_text.splitlines() if l.strip()]
        try:
            idx = next(i for i, l in enumerate(lines) if l == pii.get("holder_name", ""))
            if idx + 3 < len(lines):
                pii["address_line1"] = lines[idx + 1]
                pii["address_line2"] = lines[idx + 2]
                pii["address_line3"] = lines[idx + 3]
        except StopIteration:
            pass

    else:
        # Credit card: 16-digit card number found on pages 2+
        for pno in range(1, len(doc)):
            page_txt = doc[pno].get_text()
            for m in _RE_16DIGIT.finditer(page_txt):
                num = m.group(1)
                if num.startswith("4") or num.startswith("5"):
                    pii.setdefault("card_number", num)
                    break
            if "card_number" in pii:
                break

    return pii


# ─── Mapping builder ──────────────────────────────────────────────────────────

def _fake_for_label(label: str, field: str) -> str:
    """Return the correct fake value for the given account label + field."""
    return FAKE.get(f"{field}_{label}", FAKE.get(field, "DEMO"))


def build_mapping(
    statements_dir: Path,
    existing_mapping: Optional[dict] = None,
) -> dict[str, Any]:
    """
    Walk all product folders, detect real PII from first month's PDF,
    and build a deterministic real→fake mapping.
    Merges with any existing mapping so repeated runs are idempotent.
    """
    mapping: dict[str, Any] = existing_mapping or {"_note": "KEEP OUT OF GIT", "products": {}}

    product_dirs = sorted(statements_dir.iterdir())
    for pdir in product_dirs:
        if not pdir.is_dir() or pdir.name.startswith("_"):
            continue
        label = detect_account_label(statements_dir, pdir / "dummy.pdf")
        if label == "?":
            continue

        # Use the earliest month to detect PII
        pdfs = sorted(pdir.glob("*.pdf"))
        if not pdfs:
            continue
        first_pdf = pdfs[0]
        try:
            doc = fitz.open(str(first_pdf))
        except Exception as exc:
            log.warning("Cannot open %s: %s", first_pdf, exc)
            continue

        ptype = detect_product_type(doc)
        real = extract_real_pii(doc, ptype)
        doc.close()

        prod_entry: dict[str, Any] = {
            "folder": pdir.name,
            "product_type": ptype,
            "real_to_fake": {},
        }

        # Holder name — full form and common partial truncations seen in
        # transaction description lines (e.g. "TRANSFERENCIA A ABEL BRIONES")
        if "holder_name" in real:
            full_name = real["holder_name"]
            fake_full = FAKE["holder_name"]
            prod_entry["real_to_fake"][full_name] = fake_full
            # Build partial variants from real name parts
            parts = full_name.split()          # e.g. ["ABEL", "BRIONES", "RAMIREZ"]
            fake_parts = fake_full.split()     # e.g. ["CARLOS", "MENDOZA", "VARGAS"]
            if len(parts) >= 2 and len(fake_parts) >= 2:
                # first + second name (no apellido paterno)
                p12 = " ".join(parts[:2])
                f12 = " ".join(fake_parts[:2])
                prod_entry["real_to_fake"][p12] = f12
            if len(parts) >= 3 and len(fake_parts) >= 3:
                # second + third name
                p23 = " ".join(parts[1:3])
                f23 = " ".join(fake_parts[1:3])
                prod_entry["real_to_fake"][p23] = f23
                # Third name component alone (appears in line-wrapped transfer
                # descriptions, e.g. "RAMIREZ AL BENEF" on continuation line)
                prod_entry["real_to_fake"][parts[2]] = fake_parts[2]

        # RFC personal
        if "rfc_personal" in real:
            prod_entry["real_to_fake"][real["rfc_personal"]] = FAKE["rfc_personal"]

        # Bank RFC
        if "bank_rfc" in real:
            prod_entry["real_to_fake"][real["bank_rfc"]] = FAKE["bank_rfc"]

        if ptype == "checking":
            for field, fake_key in [
                ("contract",      f"contract_{label}"),
                ("branch",        f"branch_{label}"),
                ("debit_card",    f"debit_card_{label}"),
                ("checking_acct", f"checking_acct_{label}"),
                ("clabe",         f"clabe_{label}"),
                ("client",        f"client_{label}"),
            ]:
                if field in real:
                    prod_entry["real_to_fake"][real[field]] = FAKE.get(fake_key, "0000")
            # Address lines replaced individually
            for line_key in ("address_line1", "address_line2", "address_line3"):
                if line_key in real:
                    prod_entry["real_to_fake"][real[line_key]] = FAKE[line_key]
        else:
            if "card_number" in real:
                prod_entry["real_to_fake"][real["card_number"]] = FAKE.get(f"card_{label}", "4000000000000000")

        mapping["products"][label] = prod_entry
        log.info("Built mapping for account-%s (%s, %d real values)", label, ptype, len(prod_entry["real_to_fake"]))

    return mapping


def save_mapping(mapping: dict, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(mapping, f, ensure_ascii=False, indent=2)
    log.info("Mapping saved → %s", path)


def load_mapping(path: Path) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


# ─── Core anonymization ───────────────────────────────────────────────────────

# Default font metrics for replacement text (Helvetica 8pt)
_FONT_NAME = "Helv"
_FONT_SIZE  = 8.0
_COLOR_BLACK = (0, 0, 0)
_COLOR_WHITE = (1, 1, 1)

# Background colour used on "Número de …" row fills (light blue, approx)
# We use white fill under overlay text for safe redaction.
_FILL_WHITE = (1.0, 1.0, 1.0)


def _add_redact_for_value(page: fitz.Page, real: str, fake: str, *, color=_COLOR_BLACK) -> int:
    """
    Find all occurrences of `real` in the page text layer and ADD redaction
    annotations (without applying them yet).  Returns hit count.
    Caller must call page.apply_redactions() once after all values are queued.
    """
    hits = page.search_for(real)
    if not hits:
        return 0
    for rect in hits:
        page.add_redact_annot(
            rect,
            text=fake,
            fontname=_FONT_NAME,
            fontsize=_FONT_SIZE,
            fill=_FILL_WHITE,
            text_color=color,
            align=fitz.TEXT_ALIGN_LEFT,
        )
    return len(hits)


def _redact_and_replace(page: fitz.Page, real: str, fake: str, *, color=_COLOR_BLACK) -> int:
    """
    Find all occurrences of `real` in the text layer of `page`,
    apply a white-fill redaction, and insert `fake` at the same position.

    IMPORTANT: Call this function ONLY after all longer values have already been
    processed (or use anonymize_page which handles the sort order).

    Returns the number of replacements applied.
    """
    n = _add_redact_for_value(page, real, fake, color=color)
    if n:
        page.apply_redactions(images=fitz.PDF_REDACT_IMAGE_NONE)
    return n


def _cover_region(page: fitz.Page, rect: fitz.Rect, fake: str, *, color=_COLOR_BLACK) -> None:
    """Draw a white-filled rect over `rect` and insert `fake` text."""
    page.draw_rect(rect, color=None, fill=_FILL_WHITE)
    # Insert text at baseline (y ≈ rect.y1 - 2)
    page.insert_text(
        fitz.Point(rect.x0 + 1, rect.y1 - 2),
        fake,
        fontname=_FONT_NAME,
        fontsize=_FONT_SIZE,
        color=color,
    )


# Coordinate regions for image-layer field values on credit-card page 1.
# The labels ("Número de tarjeta", "RFC", …) ARE in the text layer at the Y
# ranges below (confirmed by diagnostic: label y=174-185, 186-197, 198-208,
# 209-220, 220-231).  The AFP character-image values sit in the SAME Y rows,
# to the right of the labels (label x≈26-101, value x starts at ≈100).
# Boxes are NON-OVERLAPPING so that step 3's two-pass drawing (all rects
# first, then all text) does not let a later white rect erase earlier text.
_CC_P1_FIELD_BOXES: list[tuple[str, fitz.Rect, str]] = [
    # (fake_key, rect, label_for_logging)
    ("card_number_fake", fitz.Rect(100, 173, 305, 186), "Numero de tarjeta"),
    ("rfc_field_fake",   fitz.Rect(100, 185, 305, 198), "RFC"),
    ("branch_fake",      fitz.Rect(100, 197, 305, 210), "Numero de sucursal"),
    ("client_fake",      fitz.Rect(100, 209, 305, 222), "Numero de cliente"),
    ("clabe_fake",       fitz.Rect(100, 219, 305, 232), "CLABE Interbancaria"),
]


def anonymize_page(
    page: fitz.Page,
    page_num: int,           # 0-based
    product_type: str,
    label: str,              # A / B / C
    rtof: dict[str, str],    # real→fake for this product
) -> None:
    """
    Anonymize a single page in-place.

    Text-layer strategy: process each PII value with its OWN apply_redactions()
    call, sorted LONGEST-FIRST.  This is the key invariant:

        After apply_redactions() for the 18-char CLABE, the CLABE glyph stream
        is gone.  The subsequent search_for("4264") (4 chars) can no longer
        find "4264" as a substring of the now-removed CLABE span.

    Processing longest-first guarantees no shorter substring corrupts a longer
    value that hasn't been removed yet.
    """

    # ── 1. Text-layer PII — per-value redact+apply, LONGEST FIRST ──────────
    sorted_pairs = sorted(rtof.items(), key=lambda kv: len(kv[0]), reverse=True)
    for real_val, fake_val in sorted_pairs:
        if not real_val:
            continue
        n = _redact_and_replace(page, real_val, fake_val)
        if n:
            log.debug("  p%d redacted %r → %r (%d×)", page_num + 1, real_val[:25], fake_val[:25], n)

    # ── 2. Bank brand replacements — also per-value, already longest-first ──
    for real_brand, fake_brand in _BANK_REPLACEMENTS:
        n = _redact_and_replace(page, real_brand, fake_brand)
        if n:
            log.debug("  p%d brand %r → %r (%d×)", page_num + 1, real_brand[:30], fake_brand[:25], n)

    # ── 3. Credit-card page 1: cover AFP image-layer field values ───────────
    #
    # Two-pass approach (critical):
    #   Pass 1 — draw ALL white rects first.
    #   Pass 2 — insert ALL fake text on top.
    # This prevents a later white rect from visually erasing text that was
    # drawn by an earlier insert_text call (painter's algorithm: last wins).
    if product_type == "credit_card" and page_num == 0:
        fake_values = {
            "card_number_fake": FAKE.get(f"card_{label}", "4000000000000000"),
            "rfc_field_fake":   FAKE.get(f"rfc_{label.lower()}_field", FAKE["rfc_personal"]),
            "branch_fake":      FAKE.get(f"branch_{label}", "0001"),
            "client_fake":      FAKE.get(f"client_{label}", "00000001"),
            "clabe_fake":       FAKE.get(f"clabe_{label}", FAKE["clabe_A"]),
        }
        # Pass 1: white covers
        for _fk, rect, _fl in _CC_P1_FIELD_BOXES:
            page.draw_rect(rect, color=None, fill=_FILL_WHITE)
        # Pass 2: fake text on top of ALL covers
        for fkey, rect, field_label in _CC_P1_FIELD_BOXES:
            fake_val = fake_values.get(fkey, "DEMO")
            # Baseline = 3 pt from rect bottom — safely within 11-13 pt row for 8 pt font.
            page.insert_text(
                fitz.Point(rect.x0 + 2, rect.y1 - 3),
                fake_val,
                fontname=_FONT_NAME,
                fontsize=_FONT_SIZE,
                color=_COLOR_BLACK,
            )
            log.debug("  p1 img-cover: %s → %r", field_label, fake_val)

    # ── 4. Bank logo: cover image blocks at top-left of page 1 ─────────────
    if page_num == 0:
        _replace_logo(page)


def _replace_logo(page: fitz.Page) -> None:
    """
    Cover the bank logo (top-left image, ~bbox 22-186 × 5-62) with a white
    rectangle and draw a placeholder bank name.
    Also covers the secondary header image (top-right, ~303-388 × 59-113).
    """
    logo_rect    = fitz.Rect(22, 5, 190, 65)
    logo2_rect   = fitz.Rect(303, 57, 395, 116)

    page.draw_rect(logo_rect,  color=None, fill=_FILL_WHITE)
    page.draw_rect(logo2_rect, color=None, fill=_FILL_WHITE)

    # Primary placeholder
    page.insert_text(
        fitz.Point(25, 30),
        "BANCO DEMO INDIFUSION",
        fontname="Helv",
        fontsize=10,
        color=(0, 0, 0.6),
    )
    page.insert_text(
        fitz.Point(25, 44),
        "Banco Demo IndFusion, S.A.",
        fontname="Helv",
        fontsize=7,
        color=(0.4, 0.4, 0.4),
    )


def anonymize_doc(
    input_path: Path,
    output_path: Path,
    label: str,
    rtof: dict[str, str],
    inject: Optional[str] = None,
) -> None:
    """
    Anonymize a single PDF statement and write to output_path.
    Optionally inject a defect variant.
    'scanned' injection is handled specially: a new image-only document is built
    from the anonymized rendering and the original document is discarded.
    """
    log.info("Anonymizing %s → %s", input_path.name, output_path)
    doc = fitz.open(str(input_path))
    ptype = detect_product_type(doc)

    for pno, page in enumerate(doc):
        anonymize_page(page, pno, ptype, label, rtof)

    output_path.parent.mkdir(parents=True, exist_ok=True)

    if inject == "scanned":
        # Build a brand-new image-only document from the anonymized rendering
        scanned = build_scanned_doc(doc)
        scanned.save(str(output_path), garbage=4, deflate=True)
        scanned.close()
    else:
        if inject:
            _inject_defect(doc, inject, ptype, label)
        doc.save(str(output_path), garbage=4, deflate=True)

    doc.close()
    log.info("  Written: %s (%.0f KB)", output_path.name, output_path.stat().st_size / 1024)


# ─── Defect injection ─────────────────────────────────────────────────────────

def _inject_defect(doc: fitz.Document, defect: str, ptype: str, label: str) -> None:
    # Note: 'scanned' is handled in anonymize_doc before calling here.
    if defect == "math":
        _inject_math(doc)
    elif defect == "font":
        _inject_font(doc)
    elif defect == "scanned":
        raise RuntimeError("'scanned' must be dispatched by anonymize_doc, not _inject_defect")
    else:
        raise ValueError(f"Unknown defect type: {defect!r}.  Use math|font|scanned")


def _inject_math(doc: fitz.Document) -> None:
    """
    Break a CL-21 arithmetic identity by altering the
    'El pago para no generar intereses' figure on page 1.
    A fixed +$11.00 offset is added so the delta (Δ = $11.00) is well above the
    $0.50 legal tolerance — CL-21 must fire RED, not be within-tolerance PASS.
    e.g. $12,604.55 → $12,615.55
    """
    page = doc[0]
    # Find the line containing the payment-to-avoid-interest figure
    blocks = page.get_text("dict")["blocks"]
    target_text = None
    for blk in blocks:
        if blk["type"] != 0:
            continue
        for line in blk["lines"]:
            for span in line["spans"]:
                txt = span["text"]
                # Look for a dollar amount in the right-side zone (x > 200)
                # that is close to "El pago para no generar intereses" row (y ≈ 320-340)
                if (
                    re.match(r"^\$[\d,]+\.\d{2}$", txt.strip())
                    and 190 < span["bbox"][0] < 280
                    and 315 < span["bbox"][1] < 350
                ):
                    target_text = txt.strip()
                    break
            if target_text:
                break
        if target_text:
            break

    if not target_text:
        # Fallback: find any dollar amount on page 1 in the payment-table area
        page_txt = page.get_text()
        for line in page_txt.splitlines():
            m = re.search(r"\$[\d,]+\.\d{2}", line.strip())
            if m and "pago para no generar" not in line.lower():
                target_text = m.group(0)
                break

    if not target_text:
        log.warning("math-inject: could not locate target amount on page 1; skipping")
        return

    # Add a fixed +$11.00 offset — deterministic "fat-finger" entry that is well
    # above the $0.50 legal tolerance so CL-21 fires RED without ambiguity.
    # e.g. $12,604.55 → $12,615.55
    raw = target_text.replace("$", "").replace(",", "")
    try:
        numeric = float(raw)
    except ValueError:
        log.warning("math-inject: could not parse amount %r; skipping", target_text)
        return
    altered = "$" + f"{numeric + 11.00:,.2f}"

    count = _redact_and_replace(page, target_text, altered)
    log.info("math-inject: %s → %s (+$11.00, %d hit(s))", target_text, altered, count)


def _inject_font(doc: fitz.Document) -> None:
    """
    Substitute Courier-Bold for Helvetica on the 'Estado de Cuenta' title
    so that Veriqan's CL-35 font-consistency check fails.
    """
    page = doc[0]
    target = "Estado de Cuenta Mensual"
    hits = page.search_for(target)
    for rect in hits:
        page.add_redact_annot(rect, fill=_FILL_WHITE)
    if hits:
        page.apply_redactions(images=fitz.PDF_REDACT_IMAGE_NONE)
        # Re-insert in wrong font
        page.insert_text(
            fitz.Point(hits[0].x0, hits[0].y1 - 1),
            target,
            fontname="Cour",   # Courier — wrong font for Banamex layout
            fontsize=11,
            color=_COLOR_BLACK,
        )
        log.info("font-inject: replaced %r in Courier-Bold", target)
    else:
        log.warning("font-inject: %r not found in text layer", target)


def build_scanned_doc(doc: fitz.Document, dpi: int = 150) -> fitz.Document:
    """
    Render every page of `doc` to a raster image and return a NEW fitz.Document
    that contains ONLY those images — no text layer, no fonts, no annotations.
    Veriqan's text-density guard returns BLOCKED on the output.

    Returns a new document; caller is responsible for saving and closing it.
    """
    mat = fitz.Matrix(dpi / 72, dpi / 72)
    new_doc = fitz.open()  # blank, empty PDF

    for pno in range(len(doc)):
        src_page = doc[pno]
        # Render the (already-anonymized) page to a raster pixmap
        pm = src_page.get_pixmap(matrix=mat, alpha=False)
        img_bytes = pm.tobytes("png")

        # New blank page at original page dimensions (not scaled)
        new_page = new_doc.new_page(
            width=src_page.rect.width,
            height=src_page.rect.height,
        )
        # Insert the raster image filling the full page
        new_page.insert_image(new_page.rect, stream=img_bytes)

    log.info(
        "scanned-inject: rasterized %d page(s) at %d dpi → image-only PDF",
        len(doc),
        dpi,
    )
    return new_doc


def _inject_scanned(doc: fitz.Document) -> None:
    """
    Stub kept for interface compatibility.
    The real work happens in anonymize_doc which calls build_scanned_doc directly.
    """
    raise RuntimeError("Call build_scanned_doc instead of _inject_scanned.")


# ─── Batch processing ─────────────────────────────────────────────────────────

def build_global_rtof(mapping: dict) -> dict[str, str]:
    """
    Merge real→fake mappings from ALL products into one global dict.
    This handles cross-product references (e.g. Visa card number appearing in
    the checking account's payment transaction descriptions) without needing
    to know which product each source PDF belongs to.
    Longer strings win on collision (process order in anonymize_page handles
    that; here we just deduplicate safely).
    """
    merged: dict[str, str] = {}
    for prod in mapping.get("products", {}).values():
        merged.update(prod.get("real_to_fake", {}))
    return merged


def run_batch(
    statements_dir: Path,
    output_dir: Path,
    mapping: dict,
) -> None:
    """
    Process all products × months → anonymized masters.
    Then produce the 4 SUT defect variants from the SUT intermediate-month master.

    A GLOBAL merged rtof is used for every file so cross-product references
    (e.g. Visa card number appearing in the checking account payment lines) are
    caught and replaced regardless of which product's folder is being processed.
    """
    sut_master_path: Optional[Path] = None
    sut_source_pdf: Optional[Path] = None

    # One unified mapping so cross-product card references in any statement
    # are also replaced (e.g. checking account transactions crediting Visa/MC).
    global_rtof = build_global_rtof(mapping)
    log.info("Global rtof: %d real→fake pairs across all products", len(global_rtof))

    for pno, (label, prod) in enumerate(mapping["products"].items()):
        folder_name = prod["folder"]
        src_dir     = statements_dir / folder_name
        out_dir     = output_dir / folder_name

        pdfs = sorted(src_dir.glob("*.pdf"))
        if not pdfs:
            log.warning("No PDFs in %s", src_dir)
            continue

        for pdf in pdfs:
            month = pdf.stem   # e.g. "2026-04"
            out_pdf = out_dir / f"{month}.pdf"
            anonymize_doc(pdf, out_pdf, label, global_rtof)

            if folder_name.startswith(SUT_PRODUCT_PREFIX) and month == SUT_MONTH:
                sut_master_path = out_pdf
                sut_source_pdf = pdf

    # Defect variants from the SUT master
    if sut_master_path is None or sut_source_pdf is None:
        log.warning("SUT master not found (%s*/%s); skipping defect variants", SUT_PRODUCT_PREFIX, SUT_MONTH)
        return

    sut_label = "B"
    defects_dir = output_dir / "defects"
    defects_dir.mkdir(parents=True, exist_ok=True)

    # good.pdf — copy of the SUT master (baseline for comparison)
    import shutil
    good_path = defects_dir / "good.pdf"
    shutil.copy2(sut_master_path, good_path)
    log.info("SUT good master → %s", good_path)

    # Filenames match the VEC spec: bad-math-cl21, bad-font-cl35, scanned (no "bad-" prefix)
    defect_names = {"math": "bad-math-cl21", "font": "bad-font-cl35", "scanned": "scanned"}
    for defect, stem in defect_names.items():
        out_path = defects_dir / f"{stem}.pdf"
        anonymize_doc(
            sut_source_pdf,
            out_path,
            sut_label,
            global_rtof,
            inject=defect,
        )

    log.info("Defect variants written to %s", defects_dir)


# ─── CLI ──────────────────────────────────────────────────────────────────────

def parse_args(argv: list[str]) -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description="Banamex statement anonymizer → Veriqan VEC demo corpus",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    mode = p.add_mutually_exclusive_group(required=True)
    mode.add_argument("--in", dest="input",   metavar="PDF", help="Single input PDF")
    mode.add_argument("--batch",              action="store_true", help="Process all products")

    p.add_argument("--out",           metavar="PDF",  help="Output PDF (single-file mode)")
    p.add_argument("--inject",        metavar="TYPE", help="Defect type: math|font|scanned")
    p.add_argument("--statements-dir", default=str(DEFAULT_STATEMENTS_DIR), metavar="DIR")
    p.add_argument("--output-dir",     default=str(DEFAULT_OUTPUT_DIR),     metavar="DIR")
    p.add_argument("--mapping",        default=str(DEFAULT_MAPPING_PATH),   metavar="JSON",
                   help="Path to real→fake mapping JSON (created if absent)")
    p.add_argument("--rebuild-mapping", action="store_true",
                   help="Force rebuild of the mapping from source PDFs")
    p.add_argument("--verbose", "-v", action="store_true")
    return p.parse_args(argv)


def main(argv: Optional[list[str]] = None) -> int:
    args = parse_args(argv if argv is not None else sys.argv[1:])

    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)

    statements_dir = Path(args.statements_dir)
    output_dir     = Path(args.output_dir)
    mapping_path   = Path(args.mapping)

    # ── Load or build mapping ──────────────────────────────────────────────
    if mapping_path.exists() and not args.rebuild_mapping:
        mapping = load_mapping(mapping_path)
        log.info("Loaded mapping from %s (%d products)", mapping_path, len(mapping.get("products", {})))
    else:
        log.info("Building mapping from source PDFs …")
        mapping = build_mapping(statements_dir)
        save_mapping(mapping, mapping_path)

    # ── Single-file mode ───────────────────────────────────────────────────
    if args.input:
        if not args.out:
            print("ERROR: --out is required in single-file mode", file=sys.stderr)
            return 1
        input_path  = Path(args.input)
        output_path = Path(args.out)

        # Auto-detect which account label this PDF belongs to
        label = detect_account_label(statements_dir, input_path)
        rtof  = mapping.get("products", {}).get(label, {}).get("real_to_fake", {})

        anonymize_doc(input_path, output_path, label, rtof, inject=args.inject)
        log.info("Done: %s", output_path)
        return 0

    # ── Batch mode ─────────────────────────────────────────────────────────
    run_batch(statements_dir, output_dir, mapping)
    log.info("Batch complete.  Corpus → %s", output_dir)
    return 0


if __name__ == "__main__":
    sys.exit(main())
