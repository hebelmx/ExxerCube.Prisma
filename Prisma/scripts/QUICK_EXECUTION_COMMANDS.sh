#!/bin/bash
################################################################################
# QUICK EXECUTION COMMANDS - Copy/Paste for Server Agent
#
# Run these commands in sequence to:
# 1. Launch all 6 GAs in parallel
# 2. Wait for completion
# 3. Commit and push results
#
# Total time: ~10-12 hours
################################################################################

# ============================================================================
# STEP 1: SETUP AND VERIFICATION
# ============================================================================

# Navigate to repository
cd /path/to/ExxerCube.Prisma  # UPDATE THIS PATH!

# Pull latest changes
git pull origin Kt2

# Navigate to scripts directory
cd Prisma/scripts

# Verify Python packages
python -c "import pymoo; import cv2; import PIL; import numpy; print('✓ All packages available')"

# Verify Tesseract
tesseract --version


# ============================================================================
# STEP 2: LAUNCH ALL 6 GAs IN PARALLEL (tmux)
# ============================================================================

# Option A: Use launcher script (Recommended)
bash launch_cluster_gas.sh

# Option B: Manual launch (if launcher script fails)
tmux new -s cluster0_pil -d "cd $(pwd) && python optimize_cluster0_pil.py 2>&1 | tee cluster0_pil_run.log"
tmux new -s cluster0_opencv -d "cd $(pwd) && python optimize_cluster0_opencv.py 2>&1 | tee cluster0_opencv_run.log"
tmux new -s cluster1_pil -d "cd $(pwd) && python optimize_cluster1_pil.py 2>&1 | tee cluster1_pil_run.log"
tmux new -s cluster1_opencv -d "cd $(pwd) && python optimize_cluster1_opencv.py 2>&1 | tee cluster1_opencv_run.log"
tmux new -s cluster2_pil -d "cd $(pwd) && python optimize_cluster2_pil.py 2>&1 | tee cluster2_pil_run.log"
tmux new -s cluster2_opencv -d "cd $(pwd) && python optimize_cluster2_opencv.py 2>&1 | tee cluster2_opencv_run.log"

# Verify all sessions launched
tmux ls

# Verify Python processes
ps aux | grep optimize_cluster | grep -v grep


# ============================================================================
# STEP 3: MONITOR PROGRESS (Optional)
# ============================================================================

# Monitor all progress logs
tail -f cluster*_progress.log

# Check completion status (run periodically)
ls -lh cluster*_pareto_front.json

# Attach to specific session
tmux attach -t cluster0_pil
# Detach: Ctrl+B, then D


# ============================================================================
# STEP 4: WAIT FOR COMPLETION (~10 HOURS)
# ============================================================================

# Create completion checker
cat > check_completion.sh << 'EOFCHECK'
#!/bin/bash
EXPECTED=(cluster0_pil_pareto_front.json cluster0_opencv_pareto_front.json cluster1_pil_pareto_front.json cluster1_opencv_pareto_front.json cluster2_pil_pareto_front.json cluster2_opencv_pareto_front.json)
COMPLETED=0
for file in "${EXPECTED[@]}"; do
  if [ -f "$file" ] && [ $(stat -f%z "$file" 2>/dev/null || stat -c%s "$file" 2>/dev/null) -gt 1000 ]; then
    echo "✓ $file"
    ((COMPLETED++))
  else
    echo "✗ $file"
  fi
done
echo "Completed: $COMPLETED / 6"
[ $COMPLETED -eq 6 ] && echo "✓✓✓ ALL COMPLETE ✓✓✓" && exit 0
echo "⏳ Still running..." && exit 1
EOFCHECK

chmod +x check_completion.sh

# Run checker every hour (or manually)
./check_completion.sh


# ============================================================================
# STEP 5: COMMIT AND PUSH RESULTS (AFTER ALL 6 COMPLETE!)
# ============================================================================

# Navigate to repository root
cd /path/to/ExxerCube.Prisma  # UPDATE THIS PATH!

# Verify all output files exist
ls -lh Prisma/scripts/cluster*_pareto_front.json

# Check git status
git status

# Stage all changes
git add .

# Verify staged changes
git status

# Create commit
git commit -m "feat(ocr): Cluster-specific NSGA-II optimization results

- Completed 6 parallel cluster-specific GA optimizations
- Cluster 0 (ultra-sharp): 2 Pareto fronts (PIL + OpenCV)
- Cluster 1 (normal quality): 2 Pareto fronts (PIL + OpenCV)
- Cluster 2 (degraded): 2 Pareto fronts (PIL + OpenCV)

Total runtime: ~10 hours parallel execution

Files added:
- 6 GA optimization scripts (optimize_cluster*.py)
- 6 Pareto front JSON catalogs
- 6 run logs + 6 progress logs
- Launcher script (launch_cluster_gas.sh)
- Documentation (CLUSTER_GA_OPTIMIZATION_README.md)
- Server execution instructions (SERVER_EXECUTION_INSTRUCTIONS.md)
- Cluster assignment mapping (cluster_image_assignments.json)

Next steps:
- Analyze Pareto fronts across clusters
- Update correlation matrix with cluster-specific filters
- Build production model: (image_cluster, quality_level) → filter_parameters

🤖 Generated with Claude Code
Co-Authored-By: Claude <noreply@anthropic.com>"

# Verify commit
git log -1 --stat

# Push to origin
git push origin Kt2

# Verify push
git status


# ============================================================================
# STEP 6: CLEANUP AND SUMMARY
# ============================================================================

# Clean up tmux sessions
tmux kill-session -t cluster0_pil 2>/dev/null
tmux kill-session -t cluster0_opencv 2>/dev/null
tmux kill-session -t cluster1_pil 2>/dev/null
tmux kill-session -t cluster1_opencv 2>/dev/null
tmux kill-session -t cluster2_pil 2>/dev/null
tmux kill-session -t cluster2_opencv 2>/dev/null

# Create completion summary
cat > COMPLETION_SUMMARY.txt << 'EOFSUMMARY'
================================================================================
CLUSTER-SPECIFIC NSGA-II OPTIMIZATION - COMPLETE
================================================================================

Execution completed: $(date)
Branch: Kt2
Commit: $(git log -1 --format='%h - %s')

All 6 cluster-specific GAs completed successfully.
Results committed and pushed to origin/Kt2.

Files generated:
- cluster0_pil_pareto_front.json
- cluster0_opencv_pareto_front.json
- cluster1_pil_pareto_front.json
- cluster1_opencv_pareto_front.json
- cluster2_pil_pareto_front.json
- cluster2_opencv_pareto_front.json

✓ Mission accomplished!
================================================================================
EOFSUMMARY

cat COMPLETION_SUMMARY.txt

echo ""
echo "✓✓✓ EXECUTION COMPLETE ✓✓✓"
echo ""


################################################################################
# EXECUTION SUMMARY
################################################################################
#
# Total steps: 6
# Total time: ~10-12 hours
#
# What was done:
# 1. Launched 6 NSGA-II optimizations in parallel (tmux)
# 2. Generated 6 Pareto front JSON catalogs
# 3. Committed all results to git
# 4. Pushed to origin/Kt2
#
# Next actions (manual):
# - Analyze Pareto fronts
# - Update correlation matrix
# - Build production model
#
################################################################################
