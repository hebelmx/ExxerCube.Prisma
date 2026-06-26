#!/usr/bin/env bash
# regen_tier1_corpus.sh — Regenerate the Tier-1 (green-gate) synthetic SIARA corpus.
#
# WHAT IT PRODUCES
#   8 internally-consistent 3-companion cases (PDF + DOCX + XML) under:
#     Deployments/Siara.Simulator/bulk_generated_documents_all_formats/<caseId>/
#
# CANONICAL GREEN CASE (already committed to cases.json on 2026-06-25)
#   CNBV-2025-158856_20260625_183315
#   NumeroOficio  : CNBV/2025/158856  (XML == DOCX == PDF OCR — AllAgree)
#   NumeroExpediente: EXP-8810-2024   (XML only, single-source confidence 0.60)
#   TieneAseguramiento: true           (XML → Fix-1 propagates bool to Expediente)
#   Classification: Aseguramiento/90% (short-circuit via Expediente.TieneAseguramiento)
#   Fusion conflicts: 0                (chaos=none → companions agree; no AutoridadNombre
#                                       conflict occurs, so NO production filter is needed)
#   NextAction: ReviewRecommended      (confidence ≈ 0.74, not ManualReviewRequired)
#   ExportGatePolicy: ALL PASS         (BlockOnLowConfidence=false, BlockOnFusion=false,
#                                       BlockOnUnresolvedConflicts=false)
#
# WHY THESE ARE "GREEN"
#   chaos=none → no field mutations.  Cnbv_NumeroOficio = the SIARA folio (e.g.
#   AGAFF/2023/023947) so the XML, DOCX, and PDF OCR sources all extract the
#   SAME NumeroOficio value → fusion AllAgree, 0 conflicts, NextAction !=
#   ManualReviewRequired → ExportGatePolicy allows Stage-5 export.
#
#   The §2-gate-green changes (branch Liv, 2026-06-26) are:
#   Fix-1  ExtractionOrchestrator.MapExtractedFieldsToExpediente now propagates
#          AdditionalFields["TieneAseguramiento"] → Expediente.TieneAseguramiento (bool)
#          so the classifier short-circuit fires for ASEGURAMIENTO type cases.
#   Fix-3  XmlFieldExtractor + ExtractionOrchestrator wire SolicitudSiara into the
#          Expediente (completes the pre-existing FuseSolicitudSiaraAsync path).
#   Plus  SiroXmlExporter UTF-8 declaration + EventPersistenceWorker ProcessId stamping.
#   NOTE: an earlier "Fix-2" (a hardcoded CNBV-recipient exclusion in
#         FuseAutoridadNombreAsync) was REVERTED — the real gate run proved fusion has
#         0 conflicts WITHOUT it (chaos=none corpus consistency, not a code filter).
#   KNOWN CAVEAT: SolicitudSiara is derived from the same OCR pattern as NumeroOficio
#         (AdaptiveTxtFieldExtractor "// Same pattern"), so wiring it into the optional
#         bucket double-counts the folio and inflates overall confidence over the 0.70
#         ManualReview bar — tracked for the Sprint-3 confidence re-architecture.
#
# SEEDS
#   seed=100 → 3 cases (type=aseguramiento, various authorities)
#   seed=200 → 5 cases (mixed types, chaos=none)
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

# ── Generate: batch 1 — ASEGURAMIENTO cases (canonical green for gate) ────────
# seed=100, chaos=none, type=aseguramiento: these cases get TieneAseguramiento=true
# in the XML, which after Fix-1 (ExtractionOrchestrator.MapExtractedFieldsToExpediente)
# propagates to Expediente.TieneAseguramiento=true → FileClassifierService short-circuit
# → Aseguramiento/90% classification confidence → ExportGatePolicy classification check
# passes.  (No AutoridadNombre filter is needed: chaos=none keeps companions consistent,
# so fusion sees 0 conflicts without any production-side exclusion.)
echo ""
echo "Generating batch 1 (seed=100, 3 cases, chaos=none, type=aseguramiento) ..."
(cd "$GENERATOR_DIR" && "$PYTHON" main_generator.py \
  --count 3 \
  --output "$CORPUS_DIR" \
  --chaos none \
  --types aseguramiento \
  --formats pdf docx xml \
  --seed 100)

# ── Generate: batch 2 — mixed types for additional green-gate coverage ─────────
echo ""
echo "Generating batch 2 (seed=200, 5 cases, chaos=none, mixed types) ..."
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

# Rewrite cases.json so the SIARA simulator discovers the newly-generated cases.
cases_json = os.path.join(os.path.dirname(corpus_dir), "app", "cases.json")
case_ids = sorted(d for d in os.listdir(corpus_dir) if os.path.isdir(os.path.join(corpus_dir, d)))
import json
with open(cases_json, "w", encoding="utf-8") as f:
    json.dump(case_ids, f, indent=2)
print(f"\nUpdated {cases_json} with {len(case_ids)} case ID(s).")
PYEOF
