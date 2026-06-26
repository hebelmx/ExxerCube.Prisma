#!/usr/bin/env bash
# regen_tier2_corpus.sh — Generate a Tier-2 edge-case / soak workload.
#
# WHAT IT PRODUCES
#   A large diverse corpus of cases with chaos low/medium/high, varying
#   authorities and requirement types.  These cases are NOT expected to pass
#   the §2 export gate — they exercise conflict/manual-review/abstain paths,
#   validate robustness, and feed the PRISMA-E2-S7 soak test and the SLA
#   dashboard throughput tests.
#
# SIZING (see siara-simulator-load-roadmap.md)
#   Default TOTAL=100 for a quick soak run.  For the full load target (~10k
#   cases, ~11.5 GB) set TOTAL=10000 — expect 5-7 hours single-threaded.
#   Disk: ~1.15 MB/case (pdf ~379 KB + docx ~130 KB + html ~637 KB + xml ~2 KB).
#
# USAGE
#   cd /home/abel/ExxerProjects/IndFusion/ExxerCube.Prisma
#   TOTAL=200 BATCH=20 bash scripts/generators/regen_tier2_corpus.sh
#
# CHAOS DISTRIBUTION
#   33% low, 33% medium, 34% high  (all 4 CHAOS_LEVELS minus "none").
#   Authorities: all 9 SIARA authorities, cycled.
#   Requirement types: all 5 types, cycled.
#
# OUTPUT DIRECTORY
#   Same corpus dir as tier-1 by default so the simulator serves both:
#     Deployments/Siara.Simulator/bulk_generated_documents_all_formats/
#   Set OUTPUT_DIR env var to override (e.g. a separate soak corpus dir).
#
# CORPUS STORAGE
#   Deployments/ is gitignored (.gitignore line 671).  Never commit the corpus.

set -euo pipefail

TOTAL="${TOTAL:-100}"
BATCH="${BATCH:-10}"
SLEEP_BETWEEN="${SLEEP_BETWEEN:-2}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GENERATOR_DIR="$REPO_ROOT/Prisma/PRP/PRP1/research/generators/AAAV2_refactored"
VENV="$REPO_ROOT/.venv-corpus-gen"
OUTPUT_DIR="${OUTPUT_DIR:-$REPO_ROOT/Deployments/Siara.Simulator/bulk_generated_documents_all_formats}"

echo "=== Tier-2 edge/soak corpus generation ==="
echo "Repository root : $REPO_ROOT"
echo "Output          : $OUTPUT_DIR"
echo "Total cases     : $TOTAL  (batch=$BATCH, delay=${SLEEP_BETWEEN}s)"
echo ""

# ── Python venv ───────────────────────────────────────────────────────────────
if [ ! -f "$VENV/bin/python3" ]; then
  python3 -m venv "$VENV"
fi
PYTHON="$VENV/bin/python3"
"$VENV/bin/pip" install --quiet faker python-docx lxml tqdm python-dateutil jinja2 requests

mkdir -p "$OUTPUT_DIR"

# ── Generation loop ────────────────────────────────────────────────────────────
CHAOS_LEVELS=("low" "medium" "high")
AUTHORITIES=("IMSS" "SAT" "UIF" "FGR" "SEIDO" "PJF" "INFONAVIT" "SHCP" "CONDUSEF")
TYPES=("fiscal" "judicial" "pld" "aseguramiento" "informacion")

BATCHES=$(( (TOTAL + BATCH - 1) / BATCH ))
GENERATED=0
SEED=1000

for ((i=1; i<=BATCHES; i++)); do
  THIS_BATCH=$BATCH
  if (( GENERATED + BATCH > TOTAL )); then
    THIS_BATCH=$(( TOTAL - GENERATED ))
  fi

  CHAOS=${CHAOS_LEVELS[$(( (i - 1) % 3 ))]}
  AUTH=${AUTHORITIES[$(( (i - 1) % 9 ))]}
  TYPE=${TYPES[$(( (i - 1) % 5 ))]}

  echo "Batch $i/$BATCHES: count=$THIS_BATCH chaos=$CHAOS auth=$AUTH type=$TYPE seed=$SEED"

  (cd "$GENERATOR_DIR" && "$PYTHON" main_generator.py \
    --count "$THIS_BATCH" \
    --output "$OUTPUT_DIR" \
    --chaos "$CHAOS" \
    --formats pdf docx xml \
    --authority "$AUTH" \
    --types "$TYPE" \
    --seed "$SEED") 2>&1 | grep -E "Generated|Error|Completed"

  GENERATED=$(( GENERATED + THIS_BATCH ))
  SEED=$(( SEED + 1 ))

  if (( i < BATCHES )); then
    sleep "$SLEEP_BETWEEN"
  fi
done

echo ""
echo "Tier-2 generation complete."
echo "Cases in $OUTPUT_DIR: $(ls "$OUTPUT_DIR" | wc -l)"
echo ""
echo "NOTE: These cases exercise conflict/manual-review/abstain paths."
echo "They are NOT expected to pass the §2 export gate."
echo "Use for soak (PRISMA-E2-S7) and robustness/dashboard throughput testing."
