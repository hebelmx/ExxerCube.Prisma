# NSGA-II Full Optimization - Server Deployment Instructions

**Target:** Ubuntu server with high CPU/RAM
**Estimated Runtime:** 33 hours (Pop: 100, Gen: 50, 5,000 evaluations)
**Goal:** Generate comprehensive Pareto front for OCR filter optimization

---

## Prerequisites Verification

### 1. Verify Tesseract OCR Installation

```bash
# Check if Tesseract is installed
tesseract --version

# Expected output: tesseract 5.x.x

# If not installed, install:
sudo apt-get update
sudo apt-get install -y tesseract-ocr tesseract-ocr-spa

# Verify Spanish language pack
ls /usr/share/tesseract-ocr/*/tessdata/spa.traineddata

# If missing Spanish:
sudo apt-get install -y tesseract-ocr-spa
```

### 2. Verify Repository Clone

```bash
# Clone repository (if not already present)
git clone https://github.com/hebelmx/ExxerCube.Prisma.git
cd ExxerCube.Prisma

# Switch to Kt2 branch (contains NSGA-II implementation)
git checkout Kt2
git pull origin Kt2

# Verify optimization scripts exist
ls -lh Prisma/scripts/optimize_filters_nsga2.py
ls -lh Prisma/scripts/optimize_filters_nsga2_medium.py
ls -lh Prisma/scripts/optimize_filters_nsga2_quick.py
```

---

## Environment Setup

### 3. Create Python Virtual Environment

```bash
# Navigate to scripts directory
cd Prisma/scripts

# Create virtual environment
python3 -m venv .venv_nsga2

# Activate environment
source .venv_nsga2/bin/activate

# Upgrade pip
pip install --upgrade pip
```

### 4. Install Python Dependencies

```bash
# Install required packages
pip install pymoo opencv-python numpy

# Verify installations
python3 -c "import pymoo; print(f'pymoo: {pymoo.__version__}')"
python3 -c "import cv2; print(f'opencv: {cv2.__version__}')"
python3 -c "import numpy; print(f'numpy: {numpy.__version__}')"
```

### 5. Verify Tesseract Python Access

```bash
# Test Tesseract from Python
python3 << 'EOF'
import subprocess
from pathlib import Path

# Test Tesseract command
cmd = ["tesseract", "--version"]
result = subprocess.run(cmd, capture_output=True, text=True)
print(result.stdout)

# Check Spanish data
spa_data = Path("/usr/share/tesseract-ocr/5/tessdata/spa.traineddata")
if not spa_data.exists():
    spa_data = Path("/usr/share/tesseract-ocr/4.00/tessdata/spa.traineddata")

print(f"\nSpanish traineddata: {spa_data}")
print(f"Exists: {spa_data.exists()}")
EOF
```

---

## Pre-Flight Validation

### 6. Run Quick Test (5 minutes)

```bash
# Ensure you're in Prisma/scripts with activated venv
cd ~/ExxerCube.Prisma/Prisma/scripts
source .venv_nsga2/bin/activate

# Run quick test to validate environment
python3 optimize_filters_nsga2_quick.py

# Expected output:
# - Environment validation success
# - 5 Pareto solutions generated
# - Results saved to ../Fixtures/nsga2_quick_test_results.json
# - Runtime: ~2-5 minutes

# Verify output exists
ls -lh ../Fixtures/nsga2_quick_test_results.json
cat ../Fixtures/nsga2_quick_test_results.json | jq '.[] | .total_edits_all' | head -5
```

---

## Full Optimization Launch

### 7. Start NSGA-II Full Optimization (33 hours)

```bash
# Navigate to scripts directory
cd ~/ExxerCube.Prisma/Prisma/scripts
source .venv_nsga2/bin/activate

# Launch full optimization in background with nohup
nohup python3 optimize_filters_nsga2.py > nsga2_full_run.log 2>&1 &

# Capture process ID
echo $! > nsga2_full_run.pid

# Verify process is running
ps -p $(cat nsga2_full_run.pid)

# Alternative: Use screen for detachable session
screen -S nsga2_full
source .venv_nsga2/bin/activate
python3 optimize_filters_nsga2.py
# Press Ctrl+A then D to detach
# Reattach with: screen -r nsga2_full
```

### 8. Monitor Progress

```bash
# Check log file tail
tail -f nsga2_full_run.log

# Check detailed progress log (updates every 10 evaluations)
tail -f ../Fixtures/nsga2_progress.log

# Count evaluations completed
wc -l ../Fixtures/nsga2_progress.log

# Estimate completion time
# Total evaluations: 5,000 (100 population × 50 generations)
# Current evaluations: (line count in progress.log × 10)
# Time per evaluation: ~24 seconds
# Remaining time: (5,000 - current) × 24s ÷ 3600 = hours remaining
```

### 9. Optimization Completion Check

```bash
# Check if optimization completed successfully
grep -A 5 "OPTIMIZATION COMPLETE" nsga2_full_run.log

# Verify output files exist
ls -lh ../Fixtures/nsga2_pareto_front.json
ls -lh ../Fixtures/nsga2_checkpoint.pkl

# Count Pareto solutions
jq '. | length' ../Fixtures/nsga2_pareto_front.json
```

---

## Results Analysis

### 10. Examine Pareto Front

```bash
# View top 10 solutions by total edit distance
jq 'sort_by(.total_edits_all) | .[:10] | .[] | {id, total_edits_all, total_edits_Q1, total_edits_Q2}' \
   ../Fixtures/nsga2_pareto_front.json

# Find best solution for 333BBB rescue
jq 'sort_by(.objectives.Q2_333BBB) | .[0] | {
  id,
  Q2_333BBB: .objectives.Q2_333BBB,
  genome: .genome,
  total_edits: .total_edits_all
}' ../Fixtures/nsga2_pareto_front.json

# Generate summary statistics
jq '{
  pareto_size: length,
  min_total_edits: (map(.total_edits_all) | min),
  max_total_edits: (map(.total_edits_all) | max),
  avg_total_edits: (map(.total_edits_all) | add / length),
  best_Q2_333BBB: (map(.objectives.Q2_333BBB) | min)
}' ../Fixtures/nsga2_pareto_front.json
```

### 11. Compare with Baseline

```bash
# Baseline performance (from measure_ocr_quality.py):
# Fixed Enhancement: 1,219 total edits (19.2% CER)
# Q2 333BBB: 431 edits

# Check if any Pareto solution beats baseline
jq '[.[] | select(.total_edits_all < 1219)] | length' \
   ../Fixtures/nsga2_pareto_front.json

# Check if 333BBB was rescued
jq '[.[] | select(.objectives.Q2_333BBB < 431)] | length' \
   ../Fixtures/nsga2_pareto_front.json

# Show best improvement over baseline
jq 'sort_by(.total_edits_all) | .[0] | {
  total_edits: .total_edits_all,
  baseline: 1219,
  improvement: (1219 - .total_edits_all),
  improvement_pct: ((1219 - .total_edits_all) / 1219 * 100)
}' ../Fixtures/nsga2_pareto_front.json
```

---

## Results Transfer

### 12. Package Results for Retrieval

```bash
# Create results package
cd ~/ExxerCube.Prisma/Prisma

# Package all results
tar -czf nsga2_full_results.tar.gz \
  Fixtures/nsga2_pareto_front.json \
  Fixtures/nsga2_checkpoint.pkl \
  Fixtures/nsga2_progress.log \
  scripts/nsga2_full_run.log

# Verify package
ls -lh nsga2_full_results.tar.gz

# Calculate SHA256 checksum
sha256sum nsga2_full_results.tar.gz > nsga2_full_results.tar.gz.sha256
```

### 13. Transfer to Workstation

```bash
# Option A: SCP to workstation (from server)
scp nsga2_full_results.tar.gz* user@workstation:/destination/path/

# Option B: Download via GitHub (commit and push)
git add Fixtures/nsga2_pareto_front.json
git add Fixtures/nsga2_checkpoint.pkl
git commit -m "feat(ocr): NSGA-II full optimization results - 33 hour run complete"
git push origin Kt2

# Option C: Upload to cloud storage
# aws s3 cp nsga2_full_results.tar.gz s3://bucket/path/
# gsutil cp nsga2_full_results.tar.gz gs://bucket/path/
```

---

## Troubleshooting

### Common Issues

**Issue: Tesseract not found**
```bash
# Check PATH
which tesseract

# Find Tesseract installation
find /usr -name "tesseract" 2>/dev/null

# Add to PATH if needed
export PATH="/usr/bin:$PATH"
```

**Issue: Spanish traineddata missing**
```bash
# Locate tessdata directory
find /usr/share -name "tessdata" 2>/dev/null

# Download Spanish manually if needed
sudo wget https://github.com/tesseract-ocr/tessdata/raw/main/spa.traineddata \
  -O /usr/share/tesseract-ocr/5/tessdata/spa.traineddata
```

**Issue: Out of memory**
```bash
# Check available memory
free -h

# Monitor memory usage during run
watch -n 10 'free -h && ps aux | grep optimize_filters | head -5'

# Reduce population size if OOM (edit optimize_filters_nsga2.py)
# Change: pop_size=100 → pop_size=50
# Change: n_gen=50 → n_gen=100 (maintain total evaluations)
```

**Issue: Process killed unexpectedly**
```bash
# Check system logs
sudo dmesg | tail -50
sudo journalctl -xe | tail -50

# Check OOM killer
sudo grep -i oom /var/log/syslog

# Resume from checkpoint if needed
# (Implementation requires modifying optimizer to load from checkpoint.pkl)
```

---

## Expected Timeline

| Event | Time | Cumulative |
|-------|------|------------|
| Environment setup | 10 min | 0h 10m |
| Quick test validation | 5 min | 0h 15m |
| Generation 1 (Pop: 100) | 40 min | 0h 55m |
| Generation 10 | 6.5h | 7h 30m |
| Generation 25 (halfway) | 16.5h | 17h |
| Generation 50 (complete) | 33h | 33h 15m |
| Results packaging | 5 min | 33h 20m |

**Total estimated time:** ~33 hours 20 minutes from environment setup to results package

---

## Success Criteria

✅ Quick test completes in <5 minutes with 5 Pareto solutions
✅ Full optimization runs without crashes for 33 hours
✅ `nsga2_pareto_front.json` contains 20-100 Pareto solutions
✅ Best solution total_edits_all < 1,219 (baseline improvement)
✅ At least one solution with Q2_333BBB < 431 (333BBB rescue)
✅ Pareto front is well-distributed (no single objective dominates)
✅ Results successfully transferred to workstation

---

## Post-Processing (On Workstation)

After retrieving results, continue with Phase 2D analysis:

1. Apply all Pareto filters to all documents
2. Build performance matrix
3. Correlate with image quality metrics
4. Train filter selection model (DecisionTree/RandomForest)
5. Deploy intelligent mapping to production

See `docs/qa/phase2-enhancement-filters-analysis.md` Phase 2D section for details.

---

**Agent Completion Signal:**
When optimization completes, respond with:
- Pareto front size
- Best total_edits_all value
- Best Q2_333BBB value
- Runtime in hours
- Results package SHA256

Good luck with the optimization! This will generate the filter catalog we need for production deployment.
