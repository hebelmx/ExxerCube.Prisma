# Server Execution Instructions for Cluster-Specific GA Optimization

## Mission: Run 6 NSGA-II Optimizations in Parallel + Commit Results

This document contains step-by-step instructions for running all 6 cluster-specific genetic algorithm optimizations concurrently on the server, then committing and pushing the results to the repository.

---

## PART 1: Pre-Execution Checklist

### 1.1 Verify Repository is Up to Date

```bash
cd /path/to/ExxerCube.Prisma
git status
git pull origin Kt2
```

**Expected Result**: Repository should be on branch `Kt2` and up to date with origin.

### 1.2 Verify Python Environment

```bash
# Check Python version (should be 3.8+)
python --version

# Verify required packages are installed
python -c "import pymoo; import cv2; import PIL; import numpy; print('✓ All packages available')"
```

**If packages are missing**:
```bash
pip install pymoo opencv-python pillow numpy
```

### 1.3 Verify Tesseract-OCR Installation

```bash
# Linux/Ubuntu
tesseract --version
tesseract --list-langs | grep spa

# If Tesseract is not installed:
# sudo apt-get update
# sudo apt-get install tesseract-ocr tesseract-ocr-spa
```

### 1.4 Verify Directory Structure

```bash
cd /path/to/ExxerCube.Prisma/Prisma/scripts
ls -lh optimize_cluster*.py

cd ../Fixtures
ls -lh PRP1_Degraded/
```

**Expected**: You should see:
- 6 `optimize_cluster*.py` scripts
- Degraded image directories: Q0_Pristine, Q05_VeryGood, Q1_Poor, Q15_Medium, Q2_MediumPoor

---

## PART 2: Launch All 6 GAs Concurrently Using tmux

### 2.1 Navigate to Scripts Directory

```bash
cd /path/to/ExxerCube.Prisma/Prisma/scripts
```

### 2.2 Launch All 6 GAs in tmux Sessions

**Option A: Use the launcher script (Recommended)**
```bash
bash launch_cluster_gas.sh
```

**Option B: Manual tmux launch**
```bash
# Cluster 0 - Ultra-Sharp Images
tmux new -s cluster0_pil -d "cd $(pwd) && python optimize_cluster0_pil.py 2>&1 | tee cluster0_pil_run.log"
tmux new -s cluster0_opencv -d "cd $(pwd) && python optimize_cluster0_opencv.py 2>&1 | tee cluster0_opencv_run.log"

# Cluster 1 - Normal Quality Images
tmux new -s cluster1_pil -d "cd $(pwd) && python optimize_cluster1_pil.py 2>&1 | tee cluster1_pil_run.log"
tmux new -s cluster1_opencv -d "cd $(pwd) && python optimize_cluster1_opencv.py 2>&1 | tee cluster1_opencv_run.log"

# Cluster 2 - Degraded Images
tmux new -s cluster2_pil -d "cd $(pwd) && python optimize_cluster2_pil.py 2>&1 | tee cluster2_pil_run.log"
tmux new -s cluster2_opencv -d "cd $(pwd) && python optimize_cluster2_opencv.py 2>&1 | tee cluster2_opencv_run.log"
```

### 2.3 Verify All Sessions Are Running

```bash
# List all tmux sessions
tmux ls
```

**Expected Output**: Should show 6 sessions:
```
cluster0_opencv: 1 windows (created ...)
cluster0_pil: 1 windows (created ...)
cluster1_opencv: 1 windows (created ...)
cluster1_pil: 1 windows (created ...)
cluster2_opencv: 1 windows (created ...)
cluster2_pil: 1 windows (created ...)
```

```bash
# Verify Python processes are running
ps aux | grep optimize_cluster | grep -v grep
```

**Expected**: Should show 6 Python processes.

---

## PART 3: Monitor Progress (Optional but Recommended)

### 3.1 Monitor All Progress Logs

```bash
# Watch all progress logs in real-time (opens in less)
tail -f cluster*_progress.log

# Or monitor specific cluster
tail -f cluster0_pil_progress.log
```

### 3.2 Attach to a tmux Session

```bash
# Attach to a specific session to see live output
tmux attach -t cluster0_pil

# Detach from tmux session (without killing it)
# Press: Ctrl+B, then D
```

### 3.3 Check Output Files

```bash
# Check which Pareto front files have been generated
ls -lh cluster*_pareto_front.json

# Check file sizes (should grow over time)
watch -n 60 'ls -lh cluster*_pareto_front.json'
```

### 3.4 Estimated Completion Times

| Script | Expected Duration |
|--------|-------------------|
| cluster0_pil | ~40 minutes |
| cluster0_opencv | ~3 hours |
| cluster1_pil | ~3 hours |
| cluster1_opencv | ~10 hours |
| cluster2_pil | ~3 hours |
| cluster2_opencv | ~10 hours |

**Total parallel runtime**: ~10 hours (limited by longest GA: cluster1_opencv and cluster2_opencv)

---

## PART 4: Wait for Completion

### 4.1 Create Completion Checker Script

```bash
cat > check_ga_completion.sh << 'EOF'
#!/bin/bash
# Check if all 6 GAs have completed

EXPECTED_FILES=(
  "cluster0_pil_pareto_front.json"
  "cluster0_opencv_pareto_front.json"
  "cluster1_pil_pareto_front.json"
  "cluster1_opencv_pareto_front.json"
  "cluster2_pil_pareto_front.json"
  "cluster2_opencv_pareto_front.json"
)

echo "Checking GA completion..."
echo ""

COMPLETED=0
for file in "${EXPECTED_FILES[@]}"; do
  if [ -f "$file" ]; then
    SIZE=$(stat -f%z "$file" 2>/dev/null || stat -c%s "$file" 2>/dev/null)
    if [ "$SIZE" -gt 1000 ]; then
      echo "✓ $file (${SIZE} bytes)"
      ((COMPLETED++))
    else
      echo "⚠ $file exists but is too small (${SIZE} bytes)"
    fi
  else
    echo "✗ $file - NOT FOUND"
  fi
done

echo ""
echo "Completed: $COMPLETED / 6"

if [ $COMPLETED -eq 6 ]; then
  echo ""
  echo "✓✓✓ ALL 6 GAs COMPLETED! ✓✓✓"
  echo ""
  echo "Ready to commit results."
  exit 0
else
  echo ""
  echo "⏳ Still waiting for GAs to complete..."
  exit 1
fi
EOF

chmod +x check_ga_completion.sh
```

### 4.2 Monitor Completion Status

```bash
# Check status every 30 minutes
watch -n 1800 './check_ga_completion.sh'

# Or check manually
./check_ga_completion.sh
```

### 4.3 Wait for All GAs to Complete

**DO NOT PROCEED TO PART 5 UNTIL ALL 6 GAs HAVE COMPLETED.**

You can verify completion by:
1. All 6 tmux sessions have exited: `tmux ls` shows no sessions
2. All 6 Pareto front JSON files exist and are > 1KB
3. All 6 run logs show "COMPLETE" messages

---

## PART 5: Commit and Push Results to Git

### 5.1 Navigate to Repository Root

```bash
cd /path/to/ExxerCube.Prisma
```

### 5.2 Verify All Output Files Exist

```bash
ls -lh Prisma/scripts/cluster*_pareto_front.json
ls -lh Prisma/scripts/cluster*_run.log
ls -lh Prisma/scripts/cluster*_progress.log
```

**Expected**: 6 Pareto front files, 6 run logs, 6 progress logs.

### 5.3 Check Git Status

```bash
git status
```

**Expected**: Should show new files:
```
Untracked files:
  Prisma/scripts/cluster0_pil_pareto_front.json
  Prisma/scripts/cluster0_opencv_pareto_front.json
  Prisma/scripts/cluster1_pil_pareto_front.json
  Prisma/scripts/cluster1_opencv_pareto_front.json
  Prisma/scripts/cluster2_pil_pareto_front.json
  Prisma/scripts/cluster2_opencv_pareto_front.json
  Prisma/scripts/cluster*_run.log
  Prisma/scripts/cluster*_progress.log
  Prisma/scripts/optimize_cluster*.py
  Prisma/scripts/CLUSTER_GA_OPTIMIZATION_README.md
  Prisma/scripts/launch_cluster_gas.sh
  Prisma/scripts/determine_image_clusters.py
  Prisma/Fixtures/cluster_image_assignments.json
```

### 5.4 Stage All Changes

```bash
git add .
```

### 5.5 Verify Staged Changes

```bash
git status
```

**Expected**: All cluster optimization files should be staged for commit.

### 5.6 Create Commit with Descriptive Message

```bash
git commit -m "feat(ocr): Cluster-specific NSGA-II optimization results

- Completed 6 parallel cluster-specific GA optimizations (10 hours total)
- Cluster 0 (ultra-sharp): 2 Pareto fronts (PIL + OpenCV)
- Cluster 1 (normal quality): 2 Pareto fronts (PIL + OpenCV)
- Cluster 2 (degraded): 2 Pareto fronts (PIL + OpenCV)

Results:
- cluster0_pil: $(wc -l < Prisma/scripts/cluster0_pil_pareto_front.json) solutions
- cluster0_opencv: $(wc -l < Prisma/scripts/cluster0_opencv_pareto_front.json) solutions
- cluster1_pil: $(wc -l < Prisma/scripts/cluster1_pil_pareto_front.json) solutions
- cluster1_opencv: $(wc -l < Prisma/scripts/cluster1_opencv_pareto_front.json) solutions
- cluster2_pil: $(wc -l < Prisma/scripts/cluster2_pil_pareto_front.json) solutions
- cluster2_opencv: $(wc -l < Prisma/scripts/cluster2_opencv_pareto_front.json) solutions

Files:
- 6 GA optimization scripts (optimize_cluster*.py)
- 6 Pareto front JSON catalogs
- 6 run logs + 6 progress logs
- Launcher script (launch_cluster_gas.sh)
- Documentation (CLUSTER_GA_OPTIMIZATION_README.md)
- Cluster assignment mapping (cluster_image_assignments.json)

Next steps:
- Analyze Pareto fronts across clusters
- Update correlation matrix with cluster-specific filters
- Build production model: (image_cluster, quality_level) → filter_parameters"
```

### 5.7 Verify Commit

```bash
git log -1 --stat
```

**Expected**: Should show your commit with all the staged files.

### 5.8 Push to Origin

```bash
git push origin Kt2
```

**Expected Output**:
```
Enumerating objects: XX, done.
Counting objects: 100% (XX/XX), done.
Delta compression using up to N threads
Compressing objects: 100% (XX/XX), done.
Writing objects: 100% (XX/XX), XXX KiB | XXX MiB/s, done.
Total XX (delta XX), reused XX (delta XX)
To https://github.com/your-repo/ExxerCube.Prisma.git
   xxxxxxx..yyyyyyy  Kt2 -> Kt2
```

---

## PART 6: Post-Execution Verification

### 6.1 Verify Push on GitHub

```bash
git log --oneline -3
```

**Expected**: Your commit should appear at the top.

### 6.2 Verify Remote Tracking

```bash
git status
```

**Expected**:
```
On branch Kt2
Your branch is up to date with 'origin/Kt2'.

nothing to commit, working tree clean
```

### 6.3 Clean Up tmux Sessions (if any remain)

```bash
# List remaining sessions
tmux ls

# Kill all cluster sessions
tmux kill-session -t cluster0_pil
tmux kill-session -t cluster0_opencv
tmux kill-session -t cluster1_pil
tmux kill-session -t cluster1_opencv
tmux kill-session -t cluster2_pil
tmux kill-session -t cluster2_opencv
```

### 6.4 Create Completion Summary

```bash
cat > CLUSTER_GA_COMPLETION_SUMMARY.txt << EOF
================================================================================
CLUSTER-SPECIFIC NSGA-II OPTIMIZATION - COMPLETION SUMMARY
================================================================================

Execution Date: $(date)
Server: $(hostname)
Branch: Kt2
Commit: $(git log -1 --format='%H')

================================================================================
RESULTS
================================================================================

Cluster 0 (Ultra-Sharp - 555CCC Q0/Q05):
  - cluster0_pil_pareto_front.json: $(stat -f%z Prisma/scripts/cluster0_pil_pareto_front.json 2>/dev/null || stat -c%s Prisma/scripts/cluster0_pil_pareto_front.json) bytes
  - cluster0_opencv_pareto_front.json: $(stat -f%z Prisma/scripts/cluster0_opencv_pareto_front.json 2>/dev/null || stat -c%s Prisma/scripts/cluster0_opencv_pareto_front.json) bytes

Cluster 1 (Normal Quality - All docs Q0/Q05/Q1):
  - cluster1_pil_pareto_front.json: $(stat -f%z Prisma/scripts/cluster1_pil_pareto_front.json 2>/dev/null || stat -c%s Prisma/scripts/cluster1_pil_pareto_front.json) bytes
  - cluster1_opencv_pareto_front.json: $(stat -f%z Prisma/scripts/cluster1_opencv_pareto_front.json 2>/dev/null || stat -c%s Prisma/scripts/cluster1_opencv_pareto_front.json) bytes

Cluster 2 (Degraded - All docs Q15/Q2):
  - cluster2_pil_pareto_front.json: $(stat -f%z Prisma/scripts/cluster2_pil_pareto_front.json 2>/dev/null || stat -c%s Prisma/scripts/cluster2_pil_pareto_front.json) bytes
  - cluster2_opencv_pareto_front.json: $(stat -f%z Prisma/scripts/cluster2_opencv_pareto_front.json 2>/dev/null || stat -c%s Prisma/scripts/cluster2_opencv_pareto_front.json) bytes

================================================================================
GIT STATUS
================================================================================

$(git log -1 --oneline)

Pushed to: origin/Kt2
Status: $(git status --porcelain | wc -l) uncommitted changes

================================================================================
✓ MISSION COMPLETE
================================================================================

All 6 cluster-specific NSGA-II optimizations completed successfully.
Results committed and pushed to repository.

Ready for next phase:
  1. Analyze Pareto fronts across clusters
  2. Update correlation matrix with cluster-specific filters
  3. Build production model: (image_cluster, quality_level) → filter_parameters
  4. C# implementation in DocumentComparisonService.cs

================================================================================
EOF

cat CLUSTER_GA_COMPLETION_SUMMARY.txt
```

---

## TROUBLESHOOTING

### Problem: tmux session died unexpectedly

**Solution**:
```bash
# Check run log for errors
tail -100 cluster0_pil_run.log

# Restart specific GA manually
cd Prisma/scripts
python optimize_cluster0_pil.py 2>&1 | tee cluster0_pil_run.log
```

### Problem: Out of memory

**Solution**:
```bash
# Check memory usage
free -h

# Kill some GAs temporarily, run in smaller batches
tmux kill-session -t cluster1_opencv
tmux kill-session -t cluster2_opencv
```

### Problem: Tesseract not found (Linux)

**Solution**:
```bash
# Update scripts to use correct Tesseract path
# Edit all optimize_cluster*.py files, change tesseract command from:
# "C:/Program Files/Tesseract-OCR/tesseract.exe"
# to:
# "tesseract"
```

### Problem: Git push fails

**Solution**:
```bash
# Pull latest changes first
git pull origin Kt2

# Resolve any conflicts
git status

# Push again
git push origin Kt2
```

---

## AGENT EXECUTION CHECKLIST

Use this checklist to track progress:

- [ ] 1. Repository pulled and up to date
- [ ] 2. Python environment verified (pymoo, cv2, PIL, numpy)
- [ ] 3. Tesseract-OCR verified
- [ ] 4. Directory structure verified
- [ ] 5. All 6 GAs launched in tmux
- [ ] 6. Verified 6 tmux sessions running
- [ ] 7. Monitored progress (optional)
- [ ] 8. ALL 6 GAs completed (verify with check_ga_completion.sh)
- [ ] 9. All 6 Pareto front JSONs exist and are > 1KB
- [ ] 10. Navigated to repository root
- [ ] 11. Ran `git add .`
- [ ] 12. Created descriptive commit message
- [ ] 13. Ran `git commit`
- [ ] 14. Ran `git push origin Kt2`
- [ ] 15. Verified push on remote (git status clean)
- [ ] 16. Created completion summary
- [ ] 17. Cleaned up tmux sessions

---

**END OF INSTRUCTIONS**

Total estimated time: ~10-12 hours (10 hours for GAs + setup/commit time)
