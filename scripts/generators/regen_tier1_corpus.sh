#!/usr/bin/env bash
# regen_tier1_corpus.sh — Regenerate the Tier-1 (green-gate) synthetic SIARA corpus.
#
# WHAT IT PRODUCES
#   8 internally-consistent 3-companion cases (PDF + DOCX + XML) under:
#     Deployments/Siara.Simulator/bulk_generated_documents_all_formats/<caseId>/
#
# WHY THESE ARE "GREEN"
#   chaos=none → no field mutations.  Cnbv_NumeroOficio = the SIARA folio (e.g.
#   AGAFF/2023/023947) so the XML, DOCX, and PDF OCR sources all extract the
#   SAME NumeroOficio value → fusion AllAgree, 0 conflicts, NextAction !=
#   ManualReviewRequired → ExportGatePolicy allows Stage-5 export.
#
# SEEDS
#   seed=100 → 3 cases (AGAFF×2, SEIDO×1)
#   seed=200 → 5 cases (INFONAVIT×2, IMSS×1, CNBV×1, AGAFF×1)
#   The folio values are deterministic per seed.  Dir names include a timestamp
#   (non-deterministic) but the case content is always the same.
#
# DEPENDENCIES
#   Python 3.8+, venv at .venv-corpus-gen (created by this script if absent)
#   google-chrome or chromium-browser (for PDF headless render)
#
# USAGE
#   cd /home/abel/ExxerProjects/IndFusion/ExxerCube.Prisma
#   bash scripts/generators/regen_tier1_corpus.sh
#
# CORPUS STORAGE
#   Deployments/ is gitignored in .gitignore (line 671).  Do NOT commit the
#   corpus.  Re-run this script on any box to regenerate it.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GENERATOR_DIR="$REPO_ROOT/Prisma/PRP/PRP1/research/generators/AAAV2_refactored"
VENV="$REPO_ROOT/.venv-corpus-gen"
CORPUS_DIR="$REPO_ROOT/Deployments/Siara.Simulator/bulk_generated_documents_all_formats"

echo "=== Tier-1 corpus regeneration ==="
echo "Repository root : $REPO_ROOT"
echo "Generator       : $GENERATOR_DIR/main_generator.py"
echo "Output corpus   : $CORPUS_DIR"
echo ""

# ── Python venv setup ────────────────────────────────────────────────────────
if [ ! -f "$VENV/bin/python3" ]; then
  echo "Creating Python venv at $VENV ..."
  python3 -m venv "$VENV"
fi

PYTHON="$VENV/bin/python3"
PIP="$VENV/bin/pip"

echo "Installing generator dependencies ..."
"$PIP" install --quiet faker python-docx lxml tqdm python-dateutil jinja2 requests

# ── Output directory ─────────────────────────────────────────────────────────
mkdir -p "$CORPUS_DIR"

# ── Wipe existing tier-1 cases (optional - comment out to append) ─────────────
echo "Clearing existing corpus ..."
find "$CORPUS_DIR" -mindepth 1 -maxdepth 1 -type d -exec rm -rf {} + 2>/dev/null || true

# ── Generate: batch 1 (seed=100, 3 cases) ────────────────────────────────────
echo ""
echo "Generating batch 1 (seed=100, 3 cases, chaos=none) ..."
(cd "$GENERATOR_DIR" && "$PYTHON" main_generator.py \
  --count 3 \
  --output "$CORPUS_DIR" \
  --chaos none \
  --formats pdf docx xml \
  --seed 100)

# ── Generate: batch 2 (seed=200, 5 cases) ────────────────────────────────────
echo ""
echo "Generating batch 2 (seed=200, 5 cases, chaos=none) ..."
(cd "$GENERATOR_DIR" && "$PYTHON" main_generator.py \
  --count 5 \
  --output "$CORPUS_DIR" \
  --chaos none \
  --formats pdf docx xml \
  --seed 200)

# ── Quick consistency check ───────────────────────────────────────────────────
echo ""
echo "Consistency check ..."
"$PYTHON" - << 'PYEOF'
import os, re, sys, xml.etree.ElementTree as ET
from docx import Document

corpus_dir = os.environ.get("CORPUS_DIR") or "/home/abel/ExxerProjects/IndFusion/ExxerCube.Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats"
ns = '{http://www.cnbv.gob.mx}'
siara_pat = re.compile(r'[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}')

failures = []
for case_id in sorted(os.listdir(corpus_dir)):
    d = os.path.join(corpus_dir, case_id)
    if not os.path.isdir(d): continue
    files = os.listdir(d)
    xml_f  = next((f for f in files if f.endswith('.xml')),  None)
    docx_f = next((f for f in files if f.endswith('.docx')), None)
    pdf_f  = next((f for f in files if f.endswith('.pdf')),  None)
    if not (xml_f and docx_f and pdf_f):
        failures.append(f"{case_id}: missing file (xml={bool(xml_f)}, docx={bool(docx_f)}, pdf={bool(pdf_f)})")
        continue
    root = ET.parse(os.path.join(d, xml_f)).getroot()
    el = root.find(ns + 'Cnbv_NumeroOficio')
    xml_oficio = el.text if el is not None else ''
    doc_text = ' '.join(p.text for p in Document(os.path.join(d, docx_f)).paragraphs if p.text.strip())
    m = siara_pat.search(doc_text)
    docx_match = m.group(0) if m else ''
    pdf_ok = os.path.getsize(os.path.join(d, pdf_f)) > 50_000
    if xml_oficio != docx_match or not pdf_ok:
        failures.append(f"{case_id}: xml={xml_oficio!r} docx={docx_match!r} pdf_ok={pdf_ok}")
    else:
        print(f"  OK  {case_id}  NumeroOficio={xml_oficio}")

if failures:
    print("\nFAILURES:")
    for f in failures: print(f"  FAIL  {f}")
    sys.exit(1)
else:
    print(f"\nAll {len(os.listdir(corpus_dir))} cases consistent — tier-1 corpus ready.")
PYEOF
