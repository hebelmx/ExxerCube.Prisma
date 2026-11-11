#!/bin/bash
#
# Launch all 6 cluster-specific NSGA-II optimizations in parallel using tmux
#
# Usage:
#   bash launch_cluster_gas.sh
#
# Monitor:
#   tmux ls                     # List all sessions
#   tmux attach -t cluster0_pil # Attach to a session
#   tail -f cluster*_progress.log # Monitor progress
#

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "================================================================================"
echo "CLUSTER-SPECIFIC NSGA-II OPTIMIZATION LAUNCHER"
echo "================================================================================"
echo ""
echo "Launching 6 parallel NSGA-II optimizations in tmux sessions:"
echo ""

# Cluster 0 - Ultra-Sharp Images (555CCC Q0/Q05)
echo "  [1/6] cluster0_pil     - Cluster 0 PIL (Pop=20, Gen=20, ~40 min)"
tmux new -s cluster0_pil -d "cd '$SCRIPT_DIR' && python optimize_cluster0_pil.py 2>&1 | tee cluster0_pil_run.log"

echo "  [2/6] cluster0_opencv  - Cluster 0 OpenCV (Pop=50, Gen=30, ~3 hours)"
tmux new -s cluster0_opencv -d "cd '$SCRIPT_DIR' && python optimize_cluster0_opencv.py 2>&1 | tee cluster0_opencv_run.log"

# Cluster 1 - Normal Quality Images (All docs Q0/Q05/Q1)
echo "  [3/6] cluster1_pil     - Cluster 1 PIL (Pop=30, Gen=40, ~3 hours)"
tmux new -s cluster1_pil -d "cd '$SCRIPT_DIR' && python optimize_cluster1_pil.py 2>&1 | tee cluster1_pil_run.log"

echo "  [4/6] cluster1_opencv  - Cluster 1 OpenCV (Pop=50, Gen=50, ~10 hours)"
tmux new -s cluster1_opencv -d "cd '$SCRIPT_DIR' && python optimize_cluster1_opencv.py 2>&1 | tee cluster1_opencv_run.log"

# Cluster 2 - Degraded Images (All docs Q15/Q2)
echo "  [5/6] cluster2_pil     - Cluster 2 PIL (Pop=30, Gen=40, ~3 hours)"
tmux new -s cluster2_pil -d "cd '$SCRIPT_DIR' && python optimize_cluster2_pil.py 2>&1 | tee cluster2_pil_run.log"

echo "  [6/6] cluster2_opencv  - Cluster 2 OpenCV (Pop=50, Gen=50, ~10 hours)"
tmux new -s cluster2_opencv -d "cd '$SCRIPT_DIR' && python optimize_cluster2_opencv.py 2>&1 | tee cluster2_opencv_run.log"

echo ""
echo "================================================================================"
echo "✓ All 6 GA optimizations launched in tmux sessions"
echo "================================================================================"
echo ""
echo "Active tmux sessions:"
tmux ls
echo ""
echo "Commands:"
echo "  tmux attach -t cluster0_pil     # Attach to a session to monitor"
echo "  tmux kill-session -t cluster0_pil # Kill a session"
echo "  tail -f cluster*_progress.log   # Monitor all progress logs"
echo "  ps aux | grep optimize_cluster  # Check running processes"
echo ""
echo "Expected completion: ~10 hours (limited by longest GA)"
echo ""
echo "Output files (generated as GAs complete):"
echo "  - cluster0_pil_pareto_front.json"
echo "  - cluster0_opencv_pareto_front.json"
echo "  - cluster1_pil_pareto_front.json"
echo "  - cluster1_opencv_pareto_front.json"
echo "  - cluster2_pil_pareto_front.json"
echo "  - cluster2_opencv_pareto_front.json"
echo ""
