# OCR Filter Optimization - Retrospective & Lessons Learned

**Project Duration:** Multi-session iterative development
**Final Result:** Polynomial interpolation model with R² > 0.89 for all parameters
**Improvement:** 18.4% reduction in OCR edit distance (vs baseline)

---

## 1. Project Evolution

### Phase 1: Initial NSGA-II Attempt (FAILED)

**Approach:**
- Single NSGA-II multi-objective optimization
- Tried to find "universal" filter for all images
- Used all images together in fitness evaluation

**Result:** R² = 0.037 for polynomial fitting (essentially random)

**Why it failed:**
- Different document types need different filters
- Pareto front contained trade-offs, not single optima
- Averaging across diverse images masks important patterns

---

### Phase 2: Clustering by OCR Confidence (WRONG APPROACH)

**Approach:**
- Cluster documents by their OCR confidence scores
- Optimize filters per confidence band

**Result:** Still poor generalization

**Why it failed:**
- OCR confidence is the OUTPUT we're trying to improve
- Two images with same confidence may need completely different filters
- Clustering on outputs doesn't help predict filters for new images

---

### Phase 3: Proper Methodology (SUCCESS)

**Approach:**
1. **Cluster by INPUT properties** (blur, contrast, noise, edge density)
2. **Parallel GAs** - one independent GA per cluster
3. **Polynomial interpolation** for continuous filter prediction

**Result:** R² > 0.89 for all parameters, 18.4% improvement

---

## 2. What Went Well

### 2.1 Balanced Dataset Generation (v6)

Created 64 degraded images with:
- 4 pristine source documents (diversity)
- 4 confidence bands (50s%, 60s%, 70s%, 80s%)
- 4 variants per combination (artifacts: blur, texture, scan lines, vignette, skew)
- Per-document blur calibration (some docs more blur-sensitive)

**Key insight:** Balance matters. v5 had 26/39 images from one document - unusable.

### 2.2 Clustering by Image Properties

Features used:
- `blur_score`: Laplacian variance (measures sharpness)
- `contrast`: Standard deviation of grayscale intensity
- `noise_estimate`: Mean absolute Laplacian
- `edge_density`: Canny edge pixel ratio

**Why these work:** They measure WHAT'S WRONG with the image, which predicts WHAT FILTER IS NEEDED.

### 2.3 Parallel GA Architecture

```
Cluster 0 (18 imgs) ─────> GA 0 ─────> optimal_filter_0
Cluster 1 (17 imgs) ─────> GA 1 ─────> optimal_filter_1
Cluster 2 (2 imgs)  ─────> [skipped, merged with 3]
Cluster 3 (4 imgs)  ─────> GA 3 ─────> optimal_filter_3
Cluster 4 (14 imgs) ─────> GA 4 ─────> optimal_filter_4
Cluster 5 (9 imgs)  ─────> GA 5 ─────> optimal_filter_5
```

**Benefits:**
- Each GA focuses on similar images
- No conflicting objectives
- Parallelizable (ran 6 GAs simultaneously)
- Each cluster gets specialized filter

### 2.4 Polynomial Model Fitting

Degree-2 polynomial with Ridge regression:
```
filter_param = f(blur, contrast, noise, edge_density)
             = intercept + Σ(coef_i × feature_i) + Σ(coef_ij × feature_i × feature_j)
```

**Why degree 2:** Higher degrees overfit with only 64 training points.

---

## 3. What Went Wrong (and Fixes)

### 3.1 Universal Filter Fallacy

**Problem:** Initial approach tried to find one filter for all images.

**Fix:** Recognize that different degradation types need different corrections.

### 3.2 Clustering on Outputs

**Problem:** Clustering by OCR confidence doesn't help prediction.

**Fix:** Cluster by measurable INPUT properties that correlate with needed correction.

### 3.3 Unbalanced Dataset

**Problem:** v5 dataset had 26 images from 222AAA, only 4-5 from others.

**Fix:** v6 enforced balanced sampling: 4 images per doc per confidence band.

### 3.4 Python Stdout Buffering

**Problem:** Log files appeared empty because Python buffers stdout.

**Fix:** Add `flush=True` to print statements, or run with `python -u`.

### 3.5 OpenCV Kernel Size Error

**Problem:** `cv2.error: (-215:Assertion failed) anchor.inside(Rect...)`

**Fix:** Ensure morphological kernel dimensions are >= 1:
```python
kernel_width = max(1, int(kernel_width))
```

### 3.6 JSON Serialization of Numpy Types

**Problem:** `TypeError: Object of type int64 is not JSON serializable`

**Fix:** Convert numpy types before JSON serialization:
```python
def convert_numpy(obj):
    if isinstance(obj, np.floating):
        return float(obj)
    elif isinstance(obj, np.integer):
        return int(obj)
    # ...recursive for dicts/lists
```

---

## 4. Methodology for Reproducibility

### 4.1 Dataset Generation

1. Start with pristine documents (high-quality scans)
2. Apply controlled degradation with known parameters
3. Ensure balanced distribution across:
   - Document sources
   - Degradation levels
   - Artifact types
4. Verify OCR confidence targets are met

### 4.2 Feature Engineering

1. Choose features that measure IMAGE PROPERTIES, not OCR OUTPUTS
2. Features should be:
   - Computable without OCR
   - Indicative of degradation type/severity
   - Discriminative between different corrections needed

### 4.3 Clustering

1. Use K-Means with silhouette score to find optimal k
2. Verify clusters are interpretable (low blur vs high blur, etc.)
3. Handle small clusters (merge or skip)

### 4.4 Per-Cluster Optimization

1. Run independent GA for each cluster
2. Use edit distance (Levenshtein) as fitness metric
3. Evaluate on ALL images in cluster for each individual
4. Save best solution per cluster

### 4.5 Model Fitting

1. Collect cluster centroids and optimal filters
2. Fit polynomial regression (degree 2)
3. Validate R² > 0.7 for usable interpolation
4. Test on UNSEEN images to verify generalization

### 4.6 Validation

1. Generate new test images NOT in training set
2. Use intermediate degradation levels (between clusters)
3. Compare polynomial vs lookup table
4. Winner should have lower average edit distance

---

## 5. Key Parameters

### GA Configuration (per cluster)
```python
POPULATION_SIZE = 30
GENERATIONS = 25
CROSSOVER_PROB = 0.7
MUTATION_PROB = 0.3
TOURNAMENT_SIZE = 3
```

### Filter Parameter Search Space
```python
contrast:        [0.8, 1.5]    # ImageEnhance.Contrast
brightness:      [0.9, 1.2]    # ImageEnhance.Brightness
sharpness:       [0.8, 2.5]    # ImageEnhance.Sharpness
median_size:     [1, 5]        # MedianFilter (2*n+1)
unsharp_radius:  [0.5, 3.0]    # UnsharpMask radius
unsharp_percent: [50, 200]     # UnsharpMask strength
unsharp_threshold: [1, 5]      # UnsharpMask threshold
scan_removal:    [0, 1]        # Boolean
bilateral:       [0, 1]        # Boolean
```

### Clustering
```python
method = "k-means"
k = 6  # Determined by silhouette score
features = ["blur_score", "contrast", "noise_estimate", "edge_density"]
```

---

## 6. Final Results Summary

### Model Performance
| Parameter | R² Score | MAE |
|-----------|----------|-----|
| contrast | 0.949 | 0.052 |
| brightness | 0.987 | 0.004 |
| sharpness | 0.947 | 0.089 |
| unsharp_radius | 0.938 | 0.197 |
| unsharp_percent | 0.897 | 16.4 |

### Validation (32 unseen images)
| Method | Avg Edit Distance | Improvement |
|--------|-------------------|-------------|
| No filter | 755.0 | baseline |
| Lookup table | 661.9 | 93.1 (12.3%) |
| **Polynomial** | **616.4** | **138.6 (18.4%)** |

### Winner: Polynomial Interpolation
- Wins 21 vs 11 (out of 32 tests)
- 49% more improvement than lookup table
- Better generalization for intermediate degradation levels

---

## 7. Files Produced

| File | Purpose |
|------|---------|
| `scripts/NewFilteringStrategy.md` | Implementation guide |
| `scripts/OCR_FILTER_OPTIMIZATION_RETROSPECTIVE.md` | This document |
| `scripts/production_filter_inference.py` | Production inference |
| `scripts/fit_polynomial_model_v2.py` | Model training |
| `scripts/validate_inference_methods.py` | Validation |
| `scripts/ga_single_cluster.py` | Per-cluster GA |
| `scripts/ga_combined_clusters.py` | Combined cluster GA |
| `scripts/generate_degradation_spectrum_v6.py` | Dataset generation |
| `scripts/cluster_by_image_properties.py` | Clustering |
| `Fixtures/polynomial_model_v2.json` | Trained model |
| `Fixtures/production_filter_catalog.json` | Lookup table |
| `Fixtures/PRP1_GA_Results_v6/` | GA results per cluster |
| `Fixtures/PRP1_Degraded_v6/` | Training images |
| `Fixtures/validation_test/` | Validation results |

---

## 8. Future Improvements

1. **More training data:** Add more pristine documents for diversity
2. **Additional features:** Consider FFT-based frequency analysis
3. **Adaptive thresholds:** Different documents may need different parameter bounds
4. **Online learning:** Update model as new documents are processed
5. **Confidence estimation:** Predict expected improvement before applying filter

---

## 9. Citation / Reproducibility

To reproduce these results:

1. Run `generate_degradation_spectrum_v6.py` to create training set
2. Run `cluster_by_image_properties.py` to cluster images
3. Run 6 parallel instances of `ga_single_cluster.py` (0-5)
4. Run `fit_polynomial_model_v2.py` to train polynomial model
5. Run `validate_inference_methods.py` to validate

Total runtime: ~8 hours on 6-core CPU (GA optimization is the bottleneck)

---

*Document created: 2025-11-28*
*Last updated: 2025-11-28*
