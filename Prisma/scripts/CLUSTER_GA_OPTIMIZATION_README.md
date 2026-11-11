# Cluster-Specific NSGA-II Optimization

## Overview

This directory contains 6 cluster-specific NSGA-II genetic algorithm scripts for optimizing OCR preprocessing filters. Images have been clustered into 3 groups based on their quality characteristics, and each cluster requires specialized filter optimization.

## Image Clusters

Based on three-way clustering analysis (see `production_correlation_catalog.json`):

### Cluster 0: Ultra-Sharp Images
- **Characteristics**: blur=6905.9, noise=0.47, contrast=51.9
- **Images**: 555CCC at Q0_Pristine and Q05_VeryGood (2 images)
- **Description**: Pristine, ultra-sharp, high-contrast images
- **Scripts**:
  - `optimize_cluster0_pil.py` - PIL pipeline (Pop=20, Gen=20, ~40 min)
  - `optimize_cluster0_opencv.py` - OpenCV pipeline (Pop=50, Gen=30, ~3 hours)

### Cluster 1: Normal Quality Images
- **Characteristics**: blur=1436.7, noise=1.01, contrast=32.8
- **Images**: All 4 docs (222AAA, 333BBB, 333ccc, 555CCC) at Q0, Q05, Q1 (10 images total, excluding 555CCC Q0/Q05)
- **Description**: Standard quality documents with normal blur and noise levels
- **Scripts**:
  - `optimize_cluster1_pil.py` - PIL pipeline (Pop=30, Gen=40, ~3 hours)
  - `optimize_cluster1_opencv.py` - OpenCV pipeline (Pop=50, Gen=50, ~10 hours)

### Cluster 2: Degraded Images
- **Characteristics**: blur=1238.2, noise=6.83, contrast=25.1
- **Images**: All 4 docs at Q15_Medium and Q2_MediumPoor (8 images)
- **Description**: Degraded documents with high noise, low contrast
- **Scripts**:
  - `optimize_cluster2_pil.py` - PIL pipeline (Pop=30, Gen=40, ~3 hours)
  - `optimize_cluster2_opencv.py` - OpenCV pipeline (Pop=50, Gen=50, ~10 hours)

## Filter Pipelines

### PIL Pipeline (2 parameters)
- Contrast enhancement (1.0-2.5x)
- Median filter (size 3, 5, 7)
- **Faster convergence** due to fewer parameters

### OpenCV Pipeline (7 parameters)
- Denoising (h: 5-30)
- CLAHE (clip: 1.0-4.0)
- Bilateral filter (d: 5-15, sigma_color: 50-100, sigma_space: 50-100)
- Unsharp mask (amount: 0.3-2.0, radius: 0.5-5.0)
- **More powerful** but slower optimization

## Running on Server

### Prerequisites
```bash
pip install pymoo pillow opencv-python pytesseract
```

### Parallel Execution (Recommended)

Run all 6 optimizations in parallel using separate terminal sessions or tmux/screen:

```bash
# Terminal 1 - Cluster 0
python optimize_cluster0_pil.py 2>&1 | tee cluster0_pil_run.log &
python optimize_cluster0_opencv.py 2>&1 | tee cluster0_opencv_run.log &

# Terminal 2 - Cluster 1
python optimize_cluster1_pil.py 2>&1 | tee cluster1_pil_run.log &
python optimize_cluster1_opencv.py 2>&1 | tee cluster1_opencv_run.log &

# Terminal 3 - Cluster 2
python optimize_cluster2_pil.py 2>&1 | tee cluster2_pil_run.log &
python optimize_cluster2_opencv.py 2>&1 | tee cluster2_opencv_run.log &
```

### Using tmux (Recommended for long-running jobs)

```bash
# Create tmux sessions for each GA
tmux new -s cluster0_pil -d "cd /path/to/scripts && python optimize_cluster0_pil.py 2>&1 | tee cluster0_pil_run.log"
tmux new -s cluster0_opencv -d "cd /path/to/scripts && python optimize_cluster0_opencv.py 2>&1 | tee cluster0_opencv_run.log"
tmux new -s cluster1_pil -d "cd /path/to/scripts && python optimize_cluster1_pil.py 2>&1 | tee cluster1_pil_run.log"
tmux new -s cluster1_opencv -d "cd /path/to/scripts && python optimize_cluster1_opencv.py 2>&1 | tee cluster1_opencv_run.log"
tmux new -s cluster2_pil -d "cd /path/to/scripts && python optimize_cluster2_pil.py 2>&1 | tee cluster2_pil_run.log"
tmux new -s cluster2_opencv -d "cd /path/to/scripts && python optimize_cluster2_opencv.py 2>&1 | tee cluster2_opencv_run.log"

# List running sessions
tmux ls

# Attach to a session to monitor progress
tmux attach -t cluster0_pil
```

## Expected Outputs

Each script generates:

1. **Pareto Front JSON**: `clusterX_[pil|opencv]_pareto_front.json`
   - Array of Pareto-optimal solutions
   - Each solution includes genome (filter parameters) and objectives (edit distances)

2. **Progress Log**: `clusterX_[pil|opencv]_progress.log`
   - Real-time evaluation progress
   - Useful for monitoring long-running GAs

## Estimated Runtimes

| Script | Population | Generations | Total Evals | Estimated Time |
|--------|-----------|-------------|-------------|----------------|
| cluster0_pil | 20 | 20 | 400 | ~40 minutes |
| cluster0_opencv | 50 | 30 | 1,500 | ~3 hours |
| cluster1_pil | 30 | 40 | 1,200 | ~3 hours |
| cluster1_opencv | 50 | 50 | 2,500 | ~10 hours |
| cluster2_pil | 30 | 40 | 1,200 | ~3 hours |
| cluster2_opencv | 50 | 50 | 2,500 | ~10 hours |

**Total parallel runtime**: ~10 hours (limited by longest GA)
**Total sequential runtime**: ~30 hours

## Next Steps

After all GAs complete:

1. **Analyze Results**: Compare Pareto fronts across clusters
2. **Update Correlation Matrix**: Rebuild `production_correlation_catalog.json` with cluster-specific filters
3. **Build Production Model**: Create regression model mapping (image_cluster, quality_level) → (filter_type, parameters)
4. **C# Implementation**: Port best filters to DocumentComparisonService.cs using EmguCV

## Monitoring Progress

```bash
# Check which GAs are running
ps aux | grep optimize_cluster

# Monitor progress logs in real-time
tail -f cluster*_progress.log

# Check Pareto front sizes (updated as GAs run)
ls -lh cluster*_pareto_front.json
```

## Troubleshooting

- **Out of memory**: Reduce population size or run fewer GAs in parallel
- **Tesseract errors**: Verify Tesseract-OCR installation and tessdata path
- **Slow performance**: Ensure degraded images are pre-loaded (already implemented in scripts)
- **File not found**: Verify `Fixtures/PRP1_Degraded/` directory structure

## References

- **Clustering Analysis**: `determine_image_clusters.py`
- **Correlation Matrix**: `build_complete_correlation_matrix.py`
- **Quality Metrics**: `image_quality_analyzer.py`
- **NewFilterStrategy**: `docs/NewFilterStrategy.md`
