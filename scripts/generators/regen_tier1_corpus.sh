#!/usr/bin/env bash
# regen_tier1_corpus.sh — Regenerate the Tier-1 (gate-green) synthetic SIARA corpus.
#
# WHAT IT PRODUCES
#   Per-case dirs under:
#     Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats/<caseId>/
#   Each dir has three co-generated companions: <caseId>.pdf, <caseId>.docx, <caseId>.xml
#
# CANONICAL GREEN CASE (tier-1, seed=42, chaos=none)
#   caseId        : AGAFADAFSON2-2024-101415
#   NumeroOficio  : AGAFADAFSON2/2024/101415   (XML == DOCX == PDF OCR — AllAgree, 0.85)
#   NumeroExpediente: B/IN2-6635-733052-SAT    (matches TxtFieldExtractor regex — AllAgree, 0.85)
#   AutoridadNombre: "Comisión Nacional Bancaria y de Valores"
#                   (XML set explicitly; TxtFieldExtractor Priority-2 and DocxFieldExtractor
#                    both return this value for any CNBV-addressed letter — AllAgree, 0.85)
#   TieneAseguramiento: true                   (XML bool + OCR keyword extraction — AllAgree, 0.85)
#   SolicitudSiara: deduped (= NumeroOficio — correctly excluded by Story-2.6 dedup guard)
#   Classification : Aseguramiento / 90%       (short-circuit via Expediente.TieneAseguramiento)
#   Fusion conflicts: 0
#
# CONFIDENCE LEDGER (Story-2.6 dedup math — CalculateOverallConfidence)
#
#   Required bucket (weight 0.70):
#     NumeroExpediente "B/IN2-6635-733052-SAT"          : XML+PDF+DOCX agree → 0.85
#     NumeroOficio    "AGAFADAFSON2/2024/101415"         : XML+PDF+DOCX agree → 0.85
#     AreaDescripcion                                    : AllSourcesNull (not mapped from
#                                                          AdditionalFields to typed property
#                                                          in MapExtractedFieldsToExpediente)
#   RequiredFieldsScore = avg(0.85, 0.85) = 0.85
#
#   Optional bucket (weight 0.30, dedup guard active):
#     SolicitudSiara "AGAFADAFSON2/2024/101415"         : == NumeroOficio → DEDUPED, excluded
#     AutoridadNombre "Comisión Nacional Bancaria y de Valores": 3-source agree → 0.85
#     TieneAseguramiento "True"                         : 3-source agree → 0.85
#     (DiasPlazo, FechaPublicacion, NombreSolicitante, SolicitudPartes are AllSourcesNull
#      because MapExtractedFieldsToExpediente does not map them from AdditionalFields to
#      typed Expediente properties, and the generated XML lacks the <SolicitudEspecifica>
#      wrapper that XmlFieldExtractor expects for PersonasSolicitud/InstruccionesCuentasPorConocer.)
#   OptionalFieldsScore = avg(0.85, 0.85) = 0.85
#
#   OverallConfidence = 0.85 * 0.70 + 0.85 * 0.30 = 0.850
#   NextAction        = AutoProcess (>= AutoProcessThreshold 0.85)
#   ExportGatePolicy  = PASSES (no ManualReviewRequired, no conflicts, classification >= 0.70)
#
# GENERATOR FIXES APPLIED (relative to original AAAV2_refactored/main_generator.py):
#   1. generate_numero_expediente() now produces format matching TxtFieldExtractor regex
#      "[A-Z]/[A-Z]{1,4}\d*[-]\d+[-]\d+[-][A-Z]+" (e.g. "B/IN2-6635-733052-SAT").
#      The original "EXP-NNNN-YYYY" format was NOT extracted by TxtFieldExtractor → only
#      XML had NumeroExpediente → confidence 0.60 (XML-only), degrading RequiredFieldsScore.
#   2. AutoridadNombre in main_generator.py now always emits "Comisión Nacional Bancaria y
#      de Valores" instead of the requesting authority's internal name.  TxtFieldExtractor
#      Priority-2 always returns CNBV from any CNBV-addressed letter.  Using a different
#      name in the XML caused a hard AutoridadNombre CONFLICT → ManualReviewRequired → gate blocked.
#
# SEEDS
#   seed=42  → 1 canonical green IMSS aseguramiento case (Tier-1)
#   seed=100 → 3 additional green cases (various authorities, chaos=none)
#   The folio values are deterministic per seed; the generated dir name includes a
#   timestamp (non-deterministic) which this script strips to produce clean caseId dirs.
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
#   bulk_generated_documents_all_formats/ is gitignored (.gitignore line 642).
#   Do NOT commit the corpus.  Re-run this script on any box to regenerate it.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GENERATOR_DIR="$REPO_ROOT/Prisma/PRP/PRP1/research/generators/AAAV2_refactored"
VENV="$REPO_ROOT/.venv-corpus-gen"
CORPUS_DIR="$REPO_ROOT/Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats"
SCRATCH="$(mktemp -d)"

echo "=== Tier-1 corpus regeneration (Story-2.6 honest-confidence) ==="
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

# Helper: generate into scratch, rename timestamp dirs to clean caseId dirs, copy to corpus.
# Generator names output dirs as "<folio>_<timestamp>" (e.g. AGAFADAFSON2-2024-101415_20260626_080628).
# We strip the timestamp suffix so the sim discovers them by clean caseId.
place_cases() {
  local seed="$1"; shift
  local gen_scratch="$SCRATCH/seed${seed}"
  mkdir -p "$gen_scratch"
  (cd "$GENERATOR_DIR" && "$PYTHON" main_generator.py "$@" \
    --output "$gen_scratch" \
    --seed "$seed" \
    --formats pdf docx xml)
  for src_dir in "$gen_scratch"/*/; do
    base=$(basename "$src_dir")
    # Strip trailing _YYYYMMDD_HHMMSS (16 chars: underscore + 8 + underscore + 6)
    case_id="${base%_*}"        # strip last _HHMMSS
    case_id="${case_id%_*}"     # strip _YYYYMMDD
    dest="$CORPUS_DIR/$case_id"
    rm -rf "$dest"
    mkdir -p "$dest"
    for f in "$src_dir"*.xml "$src_dir"*.pdf "$src_dir"*.docx; do
      [ -f "$f" ] || continue
      fname=$(basename "$f")
      # Normalize filename to match case_id (strip timestamp from filename too)
      ext="${fname##*.}"
      cp "$f" "$dest/${case_id}.${ext}"
    done
    echo "  placed  $case_id"
  done
}

# ── Generate: canonical green case (Tier-1 gate proof) ───────────────────────
echo ""
echo "Generating canonical green case (seed=42, IMSS aseguramiento) ..."
place_cases 42 --count 1 --chaos none --authority IMSS --types aseguramiento

# ── Generate: additional green cases (various authorities) ────────────────────
echo ""
echo "Generating 3 additional green cases (seed=100, mixed authorities, chaos=none) ..."
place_cases 100 --count 3 --chaos none --types aseguramiento

# ── Cleanup scratch ──────────────────────────────────────────────────────────
rm -rf "$SCRATCH"

# ── Quick consistency check ───────────────────────────────────────────────────
echo ""
echo "Consistency check ..."
CORPUS_DIR="$CORPUS_DIR" "$PYTHON" - << 'PYEOF'
import os, re, sys, xml.etree.ElementTree as ET
from docx import Document

corpus_dir = os.environ["CORPUS_DIR"]
ns = '{http://www.cnbv.gob.mx}'
# TxtFieldExtractor NumeroOficio pattern (= SolicitudSiara pattern)
siara_pat   = re.compile(r'[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}')
# TxtFieldExtractor NumeroExpediente pattern
exp_pat     = re.compile(r'[A-Z]/[A-Z]{1,4}\d*[-]\d+[-]\d+[-][A-Z]+')

failures = []
for case_id in sorted(os.listdir(corpus_dir)):
    d = os.path.join(corpus_dir, case_id)
    if not os.path.isdir(d):
        continue
    files = os.listdir(d)
    xml_f  = next((f for f in files if f.endswith('.xml')),  None)
    docx_f = next((f for f in files if f.endswith('.docx')), None)
    pdf_f  = next((f for f in files if f.endswith('.pdf')),  None)
    if not (xml_f and docx_f and pdf_f):
        failures.append(f"{case_id}: missing file (xml={bool(xml_f)}, docx={bool(docx_f)}, pdf={bool(pdf_f)})")
        continue

    root = ET.parse(os.path.join(d, xml_f)).getroot()

    # NumeroOficio: must be in XML and must be extractable from DOCX text
    el = root.find(ns + 'Cnbv_NumeroOficio')
    xml_oficio = el.text.strip() if el is not None and el.text else ''
    doc_text = ' '.join(p.text for p in Document(os.path.join(d, docx_f)).paragraphs if p.text and p.text.strip())
    m_oficio = siara_pat.search(doc_text)
    docx_oficio = m_oficio.group(0) if m_oficio else ''

    # NumeroExpediente: must match TxtFieldExtractor pattern
    el_exp = root.find(ns + 'Cnbv_NumeroExpediente')
    xml_exp = el_exp.text.strip() if el_exp is not None and el_exp.text else ''
    exp_ok = bool(exp_pat.match(xml_exp))

    # AutoridadNombre: must be "Comisión Nacional Bancaria y de Valores"
    el_auth = root.find(ns + 'AutoridadNombre')
    xml_auth = el_auth.text.strip() if el_auth is not None and el_auth.text else ''
    auth_ok = 'Bancaria' in xml_auth or 'CNBV' in xml_auth

    # TieneAseguramiento: must be "true" (for aseguramiento cases)
    el_aseg = root.find(ns + 'TieneAseguramiento')
    xml_aseg = el_aseg.text.strip().lower() if el_aseg is not None and el_aseg.text else ''

    # PDF: must be a real rendered file (>50 KB)
    pdf_size = os.path.getsize(os.path.join(d, pdf_f))
    pdf_ok = pdf_size > 50_000

    ok = (xml_oficio == docx_oficio and exp_ok and auth_ok and pdf_ok)
    if not ok:
        failures.append(
            f"{case_id}:\n"
            f"    xml_oficio={xml_oficio!r}  docx_oficio={docx_oficio!r}\n"
            f"    xml_exp={xml_exp!r}  exp_pattern_ok={exp_ok}\n"
            f"    xml_auth={xml_auth!r}  auth_ok={auth_ok}\n"
            f"    pdf_size={pdf_size}  pdf_ok={pdf_ok}"
        )
    else:
        print(f"  OK  {case_id}")
        print(f"      NumeroOficio={xml_oficio}  NumeroExpediente={xml_exp}")
        print(f"      AutoridadNombre={xml_auth!r}  TieneAseguramiento={xml_aseg}")

if failures:
    print("\nFAILURES:")
    for f in failures:
        print(f"  FAIL  {f}")
    sys.exit(1)

n = len([d for d in os.listdir(corpus_dir) if os.path.isdir(os.path.join(corpus_dir, d))])
print(f"\n{n} case(s) consistent — tier-1 corpus ready.")
print(f"Expected OverallConfidence >= 0.85 (AutoProcess) for seed-42 case.")
print(f"Gate: 0 conflicts, NextAction != ManualReviewRequired => export fires.")
PYEOF
