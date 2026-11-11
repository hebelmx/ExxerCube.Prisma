# OCR Enhancement Filter Optimization Plan v2

## Overview

Optimize image enhancement filters for OCR using:
1. **Degradation spectrum** - synthetic artifacts targeting 50-70% confidence bands
2. **Document clustering** - group by image properties (not OCR output)
3. **Per-cluster GA optimization** - specialized filters per cluster
4. **Polynomial fitting** - map image properties to optimal filter parameters

---

## Phase 0: Baseline Data (COMPLETED)

### Pristine Documents
```
/Fixtures/PRP1/
├── 222AAA-44444444442025_page-0001.jpg  (731KB)
├── 333BBB-44444444442025_page1.png      (692KB)
├── 333ccc-6666666662025_page1.png       (540KB)
└── 555CCC-66666662025_page-0001.png     (220KB)
```

### Baseline OCR Results (Tesseract PSM 6, Spanish)

| Document | Confidence | Blur Score | Contrast | Edge% | Words | Chars |
|----------|------------|------------|----------|-------|-------|-------|
| 222AAA   | 88.4%      | 1814       | 34.9     | 6.85  | 277   | 1838  |
| 333BBB   | 89.8%      | 2171       | 34.9     | 8.41  | 248   | 1783  |
| 333ccc   | 84.0%      | 1474       | 30.7     | 6.45  | 156   | 1158  |
| 555CCC   | 95.0%      | 8589       | 53.8     | 7.31  | 279   | 1812  |

**Key Observation:** 555CCC is significantly different:
- Blur score 4x higher (8589 vs ~2000)
- Contrast 1.5x higher (53.8 vs ~33)
- This suggests natural clustering potential

---

## Phase 1: Degradation Spectrum Generation

### Target Confidence Bands
From ~85-95% pristine down to rescuable limit (~50%):

| Band | Target Confidence | Description |
|------|-------------------|-------------|
| D80  | 75-84%           | Light degradation |
| D70  | 65-74%           | Moderate degradation |
| D60  | 55-64%           | Heavy degradation |
| D50  | 50-54%           | Rescuable limit |

**Note:** Q3/Q4 (<50%) are NOT salvageable - excluded from optimization.

### Degradation Parameters (from `degrade_images_for_ocr_testing.py`)

```python
DegradationProfile:
- blur_radius: float        # 0.5 - 3.0 (Gaussian blur)
- noise_intensity: float    # 0.02 - 0.15 (Gaussian noise)
- rotation_angle: float     # 0.5 - 3.5 degrees
- contrast_factor: float    # 0.65 - 0.95 (reduction)
- brightness_factor: float  # 0.80 - 0.98 (reduction)
- jpeg_quality: int         # 35 - 90
- salt_pepper_amount: float # 0 - 0.006
- add_scan_lines: bool      # Scanner artifacts
```

### Random Artifact Variations (2 per document for clustering)

For each of the 4 documents, generate 2 distinct degradation profiles:
- **Variant A:** Scanner-like (blur + scan lines + JPEG compression)
- **Variant B:** Handling-like (noise + rotation + contrast loss)

**Output:** 4 docs × 4 bands × 2 variants = **32 degraded images**

---

## Phase 2: Document Clustering

### Why Cluster by Image Properties (NOT OCR Confidence)?

- OCR confidence is the **output** we're optimizing
- Two documents with same confidence may need different filters
- Image properties are the **input** to filter selection
- Clustering on inputs allows predictive filter selection

### Clustering Features (7 dimensions)

```python
features = {
    'blur_score':     # Laplacian variance (higher = sharper)
    'noise_estimate': # High-frequency energy
    'contrast':       # Standard deviation of intensity
    'brightness':     # Mean intensity
    'edge_density':   # Canny edge pixel ratio
    'high_freq_pct':  # FFT high-frequency percentage
    'text_density':   # Estimated text coverage (optional)
}
```

### Clustering Method

```python
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler

# Normalize features
scaler = StandardScaler()
features_scaled = scaler.fit_transform(features)

# K-means with k=2 (hypothesis: high-contrast vs normal)
# Can increase k if more clusters emerge
kmeans = KMeans(n_clusters=2, random_state=42)
clusters = kmeans.fit_predict(features_scaled)
```

### Expected Clusters (Hypothesis)

Based on baseline data:
- **Cluster A:** 555CCC-like (high blur score, high contrast)
- **Cluster B:** 222AAA/333BBB/333ccc-like (normal properties)

---

## Phase 3: PIL Filter Parameters (Expanded)

### Current Parameters (2)
```python
contrast_factor: Real [1.0, 2.5]  # ImageEnhance.Contrast
median_size: Choice [3, 5, 7]     # ImageFilter.MedianFilter (VALIDATED: 3 is optimal)
```

### Additional Parameters to Explore (4 new)

```python
# Sharpening
sharpness_factor: Real [0.5, 2.0]  # ImageEnhance.Sharpness (1.0 = no change)

# Unsharp Mask (better than simple sharpen)
unsharp_radius: Real [0.5, 3.0]    # ImageFilter.UnsharpMask radius
unsharp_percent: Real [50, 200]    # Percentage of sharpening
unsharp_threshold: Real [1, 5]     # Threshold for edge detection

# Brightness (if degradation reduced it)
brightness_factor: Real [0.9, 1.3] # ImageEnhance.Brightness
```

### Why These Parameters?

| Parameter | Counteracts Degradation |
|-----------|------------------------|
| contrast_factor | Contrast loss from scanning |
| median_size | Salt-pepper noise, speckles |
| sharpness_factor | General blur |
| unsharp_mask | Focus blur (scanner) |
| brightness_factor | Dark scans |

### Median Size Investigation

The median_size=3 was always optimal in previous runs. To investigate:
1. **Test with heavy salt-pepper noise** - may require larger median
2. **Test with minimal noise** - may benefit from no median (size=1)
3. If still always 3, **fix it** and search other parameters

---

## Phase 4: Per-Cluster GA Optimization

### GA Configuration per Cluster

```python
# For each cluster, run NSGA-II
problem = ClusterFilterOptimization(
    cluster_documents=cluster_docs,  # Documents in this cluster
    degradation_levels=['D80', 'D70', 'D60', 'D50'],
)

# Variables (6 parameters)
variables = {
    'contrast_factor': Real(bounds=(1.0, 2.5)),
    'median_size': Choice(options=[1, 3, 5, 7]),  # Include 1 (no median)
    'sharpness_factor': Real(bounds=(0.5, 2.0)),
    'unsharp_radius': Real(bounds=(0.5, 3.0)),
    'unsharp_percent': Real(bounds=(50, 200)),
    'brightness_factor': Real(bounds=(0.9, 1.3)),
}

# Objectives: Edit distance for each doc × degradation level
# e.g., 2 docs × 4 levels = 8 objectives per cluster
```

### Parallel Execution

```
Cluster A GA (high-contrast docs): ~3-4 hours
Cluster B GA (normal docs): ~3-4 hours
───────────────────────────────────────────
Total (parallel): ~4 hours
```

---

## Phase 5: Model Fitting

### Option A: Lookup Table (Simple, Data-Driven)

```json
{
  "cluster_A_high_contrast": {
    "filter_params": {
      "contrast_factor": 1.15,
      "median_size": 3,
      "sharpness_factor": 1.2,
      ...
    },
    "performance": {
      "D80_avg_edits": 150,
      "D70_avg_edits": 280,
      ...
    }
  },
  "cluster_B_normal": { ... }
}
```

**Pros:** Simple, exact, no approximation error
**Cons:** Only works for known clusters

### Option B: Polynomial Fitting (Generalization)

```python
# Map image properties → filter parameters
# Degree 2 polynomial for each filter parameter

contrast_factor = f(blur, noise, contrast, brightness)
               = a₀ + a₁*blur + a₂*noise + a₃*contrast + a₄*brightness
                   + a₅*blur² + a₆*noise² + ...
                   + a₇*blur*noise + ...
```

**Pros:** Generalizes to unseen documents, smooth interpolation
**Cons:** May have approximation error, requires good data coverage

### Recommendation: Hybrid Approach

1. **Use lookup table** for production (discrete cluster → filter)
2. **Fit polynomial** for analysis and edge cases
3. **Compare** predictions to validate polynomial accuracy

---

## Phase 6: Production Pipeline

```python
def enhance_document(image):
    # Step 1: Analyze image properties
    props = analyze_image_properties(image)

    # Step 2: Classify cluster (simple rules or trained classifier)
    if props['blur_score'] > 5000 and props['contrast'] > 45:
        cluster = 'high_contrast'
    else:
        cluster = 'normal'

    # Step 3: Get optimal filter from catalog
    filter_params = FILTER_CATALOG[cluster]

    # Step 4: Apply PIL enhancement
    enhanced = apply_pil_filter(image, filter_params)

    return enhanced, {'cluster': cluster, 'params': filter_params}
```

---

## Execution Checklist

### Phase 0: Baseline (DONE)
- [x] Identify 4 pristine documents
- [x] Run Tesseract baseline OCR
- [x] Extract image properties
- [x] Save to `pristine_baseline_ocr.json`

### Phase 1: Degradation
- [ ] Create degradation spectrum script
- [ ] Generate D80, D70, D60, D50 bands
- [ ] Create 2 variants per document (scanner vs handling)
- [ ] Verify OCR confidence targets met
- [ ] Save degraded images to `PRP1_Degraded_v2/`

### Phase 2: Clustering
- [ ] Extract features from all degraded images
- [ ] Run K-means clustering (k=2 initially)
- [ ] Visualize clusters (PCA or t-SNE)
- [ ] Assign documents to clusters
- [ ] Save cluster assignments

### Phase 3: GA Optimization
- [ ] Create expanded PIL filter problem (6 params)
- [ ] Run GA for Cluster A
- [ ] Run GA for Cluster B
- [ ] Extract Pareto fronts
- [ ] Select best solutions per cluster

### Phase 4: Model Fitting
- [ ] Build lookup table from GA results
- [ ] Fit polynomial models (optional)
- [ ] Validate predictions vs actual
- [ ] Export production config

### Phase 5: Production
- [ ] Implement filter selection in C#
- [ ] A/B test vs baseline
- [ ] Monitor production metrics

---

## Files

### Scripts
- `baseline_ocr_pristine.py` - Get baseline OCR (DONE)
- `degrade_images_for_ocr_testing.py` - Degradation (EXISTS)
- `generate_degradation_spectrum_v2.py` - New spectrum (TODO)
- `cluster_documents.py` - Clustering (TODO)
- `optimize_cluster_ga.py` - Per-cluster GA (TODO)
- `fit_polynomial_model.py` - Model fitting (TODO)

### Data
- `pristine_baseline_ocr.json` - Baseline results (DONE)
- `degraded_spectrum_ocr.json` - Degraded OCR (TODO)
- `cluster_assignments.json` - Cluster mapping (TODO)
- `cluster_A_pareto.json` - GA results (TODO)
- `cluster_B_pareto.json` - GA results (TODO)
- `production_filter_catalog.json` - Final config (TODO)

---

## Notes

### On Median Size
Previous validation showed median_size=3 is always optimal for our synthetic degradation.
To investigate further:
1. Test with extreme salt-pepper noise (amount > 0.01)
2. Test with zero noise (should prefer no median)
3. If still constant, fix median_size=3 and search other params

### On Polynomial vs Lookup
- Polynomial fitting had R²=0.037 for contrast_factor (BAD)
- This is because Pareto fronts contain trade-offs, not single optima
- Lookup table from best Pareto solution is more reliable
- Polynomial may work better with expanded parameter space

### On Document Clustering
- Cluster by **input properties** (blur, noise, contrast)
- NOT by **output** (OCR confidence)
- This allows predictive filter selection on new documents
