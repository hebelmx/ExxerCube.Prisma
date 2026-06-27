#!/usr/bin/env bash
# regen_soak_corpus_e2s7.sh — Regenerate the PRISMA-E2-S7 soak corpus (50 distinct,
# pipeline-compatible SIARA cases) using the SAME proven recipe as
# regen_tier1_corpus.sh (which produces the gate-green AGAFADAFSON2 case).
#
# WHY: the earlier soak corpus (generate_soak_corpus_e2s7.py / fpdf2 stubs, and a
# python wrapper that called main_generator.py WITHOUT --types aseguramiento) did
# not flow through the pipeline. This script uses the EXACT tier-1 recipe — the
# fixed AAAV2_refactored/main_generator.py with --chaos none --types aseguramiento
# — just scaled to --count 50, so every case is an aseguramiento case with a
# distinct folio that auto-exports like AGAFADAFSON2.
#
# OUTPUT: Prisma/Deployments/Siara.Simulator/soak_corpus_50/<caseId>/{.pdf,.docx,.xml}
# CORPUS is gitignored; re-run on any box to regenerate.
#
# USAGE: bash scripts/generators/regen_soak_corpus_e2s7.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GENERATOR_DIR="$REPO_ROOT/Prisma/PRP/PRP1/research/generators/AAAV2_refactored"
VENV="$REPO_ROOT/.venv-corpus-gen"
CORPUS_DIR="$REPO_ROOT/Prisma/Deployments/Siara.Simulator/soak_corpus_50"
SCRATCH="$(mktemp -d)"

echo "=== E2-S7 soak corpus regeneration (proven tier-1 recipe, 50 cases) ==="
echo "Generator     : $GENERATOR_DIR/main_generator.py"
echo "Output corpus : $CORPUS_DIR"

if [ ! -f "$VENV/bin/python3" ]; then
  echo "Creating Python venv at $VENV ..."
  python3 -m venv "$VENV"
fi
PYTHON="$VENV/bin/python3"
PIP="$VENV/bin/pip"
echo "Installing generator dependencies ..."
"$PIP" install --quiet faker python-docx lxml tqdm python-dateutil jinja2 requests

# Fresh corpus dir (replace any prior soak corpus).
rm -rf "$CORPUS_DIR"
mkdir -p "$CORPUS_DIR"

# Generate 50 aseguramiento cases (chaos=none → all 3 companions agree → auto-export),
# then strip the generator's "<folio>_<timestamp>" suffix to clean caseId dirs.
gen_scratch="$SCRATCH/seed42"
mkdir -p "$gen_scratch"
(cd "$GENERATOR_DIR" && "$PYTHON" main_generator.py \
  --count 50 --chaos none --types aseguramiento \
  --output "$gen_scratch" --seed 42 --formats pdf docx xml)

for src_dir in "$gen_scratch"/*/; do
  base=$(basename "$src_dir")
  case_id="${base%_*}"        # strip _HHMMSS
  case_id="${case_id%_*}"     # strip _YYYYMMDD
  dest="$CORPUS_DIR/$case_id"
  rm -rf "$dest"; mkdir -p "$dest"
  for f in "$src_dir"*.xml "$src_dir"*.pdf "$src_dir"*.docx; do
    [ -f "$f" ] || continue
    ext="${f##*.}"
    cp "$f" "$dest/${case_id}.${ext}"
  done
done

rm -rf "$SCRATCH"

n=$(find "$CORPUS_DIR" -mindepth 1 -maxdepth 1 -type d | wc -l)
echo "Generated $n soak cases into $CORPUS_DIR"
echo "Sample sizes:"
sample=$(find "$CORPUS_DIR" -mindepth 1 -maxdepth 1 -type d | head -1)
du -b "$sample"/* 2>/dev/null || true
