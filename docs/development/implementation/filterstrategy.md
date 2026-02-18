# Adaptive Filter Strategy for OCR Enhancement

## Problem Statement

Different document sources require different enhancement parameters:
- SAT printed forms (high quality) need minimal enhancement
- Poor scans need aggressive denoising
- Phone photos need contrast enhancement
- Multi-source pipeline needs to handle all cases automatically

**Current Challenge:** Fixed filter parameters (h=10, CLAHE clipLimit=2.0) work well on average but are suboptimal for edge cases.

**Goal:** Adaptive filter selection based on image quality analysis - classical computer vision, not neural networks.

---

## Experimental Results Summary

### Phase 1: Baseline Testing (Degraded Images, No Enhancement)
- Q1_Poor: 78-92% confidence (production quality)
- Q2_MediumPoor: 42-53% confidence (below 70% threshold)
- Q3_Low: 25-27% confidence
- Q4_VeryLow: 0-15% confidence

### Phase 2: Standard Enhancement (Light Filters)
**Pipeline:** CLAHE (clipLimit=2.0) + NLM Denoising (h=10) + Bilateral Filter + Unsharp Mask

**Q2 Results (Target: 70%):**
- 222AAA: 61.25% → **74.13%** (+12.88%) ✅ PASS
- 555CCC: 37.25% → **75.57%** (+38.32%) ✅ PASS
- 333BBB: 47.50% → **69.62%** (+22.12%) ❌ FAIL (0.38% short!)
- 333ccc: 52.58% → **60.14%** (+7.56%) ❌ FAIL

**Success Rate:** 50% (2/4 documents rescued to 70%+)

### Phase 3: Aggressive Enhancement (FAILED)
**Pipeline:** FastNLM (h=30) + CLAHE + Adaptive Threshold + Deskewing

**Q1 Results:**
- 555CCC: 85% → **46.69%** (-39.81% CATASTROPHIC DEGRADATION)

**Root Cause:**
1. Binarization destroys gradient information needed by ML-based OCR
2. Deskewing detected -90° rotation on ALL images (systematically wrong)
3. Contour-based angle detection confused by binarized artifacts

**Conclusion:** Aggressive enhancement is counterproductive. Standard enhancement is already near-optimal.

### Tesseract PSM Mode Testing
Tested PSM modes 1, 3, 4, 6 on degraded 333BBB document:

| PSM Mode | File Size | Quality | Recommendation |
|----------|-----------|---------|----------------|
| PSM 1 (Auto + OSD) | 344 bytes | Poor, fragmented | ❌ Skip |
| PSM 3 (Fully auto) | 344 bytes | Poor, fragmented | ❌ Skip |
| PSM 4 (Single column) | 454 bytes | Poor | ❌ Skip |
| **PSM 6 (Uniform block)** | **2.2KB** | **Best** | ✅ **USE THIS** |

**Winner:** PSM 6 produces 6x more complete text than other modes.

---

## Production Pipeline Recommendation

### Current Optimal Configuration
```
Enhancement:  Light filters (CLAHE + NLM h=10, NO binarization, NO deskewing)
OCR Engine:   Tesseract only with PSM 6
Fallback:     Skip GOT-OCR2 (complexity not justified)
Threshold:    70% confidence
Rejection:    Below 70% → manual review queue
```

### Pipeline Philosophy: Early Rejection Strategy

```
┌─────────────────────────────────────────────────────────┐
│ Stage 1: OCR Quality Gate (Enhancement + OCR)          │
│ Goal: Early rejection of obvious garbage               │
│ • True Rejects: Caught here (low confidence < 70%)     │
│ • False Accepts: Pass through → caught in Stage 2+     │
└─────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────┐
│ Stage 2+: Defensive Programming                        │
│ • XML extraction validation (XPath success/failure)    │
│ • Business rule validation (RFC format, date ranges)   │
│ • Required field presence checks                       │
│ • Cross-field consistency validation                   │
│ • Catch false accepts from Stage 1 here                │
└─────────────────────────────────────────────────────────┘
```

**Key Insight:** 70% threshold is a **quality gate**, not perfection. Documents with good OCR but bad/corrupted data will be caught by downstream validation logic.

---

## NEW STRATEGY: Cluster-Based Specialized Filter Optimization (BREAKTHROUGH!)

**Date:** 2025-11-27
**Status:** Active Development

### Critical Discovery from NSGA-II Run 1

**Problem Identified:**
After 38 generations (75% complete), the 8-objective NSGA-II optimization is stuck:
- Best Q2_333BBB: **576 edits** (vs baseline 431 edits = 34% WORSE)
- Best Q1 total: **334 edits** (44% BETTER than baseline!)

**Root Cause:** **COMPETING OBJECTIVES**
```
Q1 documents (78-92% quality) ←→ Want LIGHT filters
Q2 documents (42-53% quality) ←→ Want HEAVY filters

These are TOO DIFFERENT!
Algorithm can't optimize both simultaneously → compromised mediocre solution
```

**Analogy:** Asking one pair of glasses to work for both near-sighted AND far-sighted people.

---

## Multi-Stage Cluster-Based Optimization Strategy

### Stage 1: Generate Degradation Spectrum

**Current Limited Spectrum:**
```
Pristine (100%) → Q1 (78-92%) → Q2 (42-53%) → Q3 (25-27%) → Q4 (0-15%)
     ↑              ↑              ↑              ↑              ↑
  Perfect        Light         Medium       HOPELESS       HOPELESS
                                            LIMIT (D0)
```

**New Fine-Grained Spectrum (Pristine → Q3 ONLY):**
```
Pristine → D90 → D80 → D70 → D60 → D50 → D40 → D30 → Q3(D0)
(100%)    (90%)  (80%)  (70%)  (60%)  (50%)  (40%)  (30%)  (25%)
   ↑                                                           ↑
Perfect                                            RESCUABLE LIMIT
                                                   (anything beyond is hopeless)
```

**Key Insight:** Q2 is **D0** - the worst degradation level that's still rescuable. Q4 and beyond cannot be rescued by any filter - don't waste optimization time on hopeless cases.

**Implementation:**
```python
def generate_degradation_spectrum(pristine_image, num_levels=10):
    """
    Generate degradation spectrum from Pristine to Q3 (D0).

    Q3 is the rescuable limit - beyond this, no filter can help.
    """
    degraded_images = []

    for level in range(num_levels):
        # Intensity: 0.0 (pristine) to 1.0 (Q3 level)
        intensity = level / (num_levels - 1)

        # Apply degradation filters with increasing intensity
        degraded = apply_degradation(
            pristine_image,
            blur_sigma=0.5 + intensity * 2.5,      # 0.5 → 3.0
            noise_level=5 + intensity * 20,        # 5 → 25
            contrast_factor=1.0 - intensity * 0.4, # 1.0 → 0.6
            jpeg_quality=95 - intensity * 45       # 95 → 50
        )

        degraded_images.append({
            'level': f'D{int((1-intensity)*100)}',  # D100, D90, ..., D25
            'intensity': intensity,
            'image': degraded,
            'rescuable': True  # All levels up to Q3 are rescuable
        })

    return degraded_images
```

**Output:** 10 degradation levels for each of 4 documents = **40 test images** total

---

### Stage 2: Build Comprehensive Performance Matrix

**Test ALL Pareto filters against degradation spectrum:**

```python
def build_performance_matrix(pareto_filters, degradation_spectrum):
    """
    Test every filter on every degradation level of every document.

    Result: HUGE performance matrix showing which filters work best
            for which degradation levels.
    """
    matrix = {}

    # From Server Run 1: 30-50 Pareto solutions
    for filter_id, filter_params in enumerate(pareto_filters):
        matrix[filter_id] = {}

        # Test on all degradation levels (D100 → D25/Q3)
        for degradation_level in degradation_spectrum:
            matrix[filter_id][degradation_level['level']] = {}

            # Test on all 4 documents
            for doc_name in ['222AAA', '333BBB', '333ccc', '555CCC']:
                degraded_img = degradation_level['images'][doc_name]

                # Apply filter
                enhanced_img = apply_filter(degraded_img, filter_params)

                # Run OCR
                ocr_text = run_tesseract(enhanced_img)

                # Measure quality (Levenshtein distance to ground truth)
                distance = levenshtein_distance(ground_truth[doc_name], ocr_text)

                matrix[filter_id][degradation_level['level']][doc_name] = distance

    return matrix  # [filter_id][degradation_level][document] = edit_distance
```

**Result:** Comprehensive map of filter performance across entire degradation spectrum

---

### Stage 3: Cluster Documents by Similarity

**Goal:** Group documents that respond similarly to degradation/enhancement

**Clustering Algorithm:**
```python
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler

def cluster_documents(performance_matrix, n_clusters=4):
    """
    Cluster documents based on how they respond to filters.

    Documents in same cluster have similar:
    - Degradation patterns
    - Response to enhancement
    - Optimal filter characteristics
    """
    # Extract features: How does each document perform with each filter?
    # Shape: [n_documents × n_filters × n_degradation_levels]

    features = []
    doc_names = ['222AAA', '333BBB', '333ccc', '555CCC']

    for doc in doc_names:
        doc_features = []
        for filter_id in performance_matrix:
            for degradation_level in performance_matrix[filter_id]:
                edit_distance = performance_matrix[filter_id][degradation_level][doc]
                doc_features.append(edit_distance)
        features.append(doc_features)

    # Normalize and cluster
    scaler = StandardScaler()
    features_scaled = scaler.fit_transform(features)

    clusters = KMeans(n_clusters=n_clusters, random_state=42).fit_predict(features_scaled)

    # Map documents to clusters
    cluster_map = {
        doc_names[i]: {
            'cluster_id': clusters[i],
            'cluster_name': get_cluster_name(clusters[i], doc_names[i])
        }
        for i in range(len(doc_names))
    }

    return cluster_map

def get_cluster_name(cluster_id, representative_doc):
    """Assign interpretable names to clusters based on characteristics."""
    cluster_names = {
        0: f"high_contrast_{representative_doc}_like",
        1: f"low_contrast_333BBB_like",      # THE HEARTBREAKER cluster
        2: f"noisy_background",
        3: f"complex_layout"
    }
    return cluster_names.get(cluster_id, f"cluster_{cluster_id}")
```

**Expected Clusters (Hypothetical):**
```
Cluster 0: High Contrast Documents (222AAA-like)
  - Good contrast even when degraded
  - Respond well to light enhancement
  - Documents: 222AAA, 555CCC

Cluster 1: Low Contrast Documents (333BBB "HEARTBREAKER" cluster)
  - Poor contrast gets worse when degraded
  - Need aggressive CLAHE/contrast boost
  - Documents: 333BBB, 333ccc

Cluster 2: Noisy Documents
  - Clean text but noisy background
  - Respond well to heavy denoising

Cluster 3: Complex Layout
  - Multiple columns, tables
  - Need specific PSM modes + careful filtering
```

---

### Stage 4: Specialized Parallel GA per Cluster

**Instead of ONE slow compromised GA:**
```
NSGA-II (8 objectives: all documents)
→ Runtime: 14 hours
→ Result: Compromised solution (can't optimize both Q1 and Q2)
```

**Run MULTIPLE fast specialized GAs in PARALLEL:**
```
Server Job 1: Cluster 0 GA (High Contrast Docs)
  - Objectives: 10-15 (same cluster docs at different degradation levels)
  - Objectives SIMILAR → faster convergence!
  - Test BOTH PIL and OpenCV pipelines
  - Runtime: ~3-4 hours

Server Job 2: Cluster 1 GA (333BBB HEARTBREAKER cluster)
  - Objectives: 10-15 (low-contrast docs at degradation levels)
  - Specialized for poor contrast rescue
  - Test BOTH PIL and OpenCV pipelines
  - Runtime: ~3-4 hours

Server Job 3: Cluster 2 GA (Noisy Docs)
  - Objectives: 10-15
  - Specialized for noise reduction
  - Runtime: ~3-4 hours

Server Job 4: Cluster 3 GA (Complex Layout)
  - Objectives: 10-15
  - Specialized for layout preservation
  - Runtime: ~3-4 hours

TOTAL: ~4 hours (ALL RUNNING IN PARALLEL!)
75% time savings vs sequential!
```

**Why This Works:**
1. **Similar objectives** → algorithm converges faster (less compromise needed)
2. **Specialized filters** → each cluster gets optimal solution (not mediocre average)
3. **Parallel execution** → 4 jobs × 4 hours = 4 hours total (not 16!)
4. **Empirical pipeline comparison** → each cluster tests BOTH PIL and OpenCV, picks winner

---

### Stage 5: Hybrid Pipeline Comparison per Cluster

**For each cluster, test BOTH pipelines:**

**Pipeline A: PIL (Simple, Proven)**
```python
# 2 parameters only
contrast_factor: 1.0-2.5
median_size: 3, 5, 7

# Matches baseline that achieved 1,219 edits
```

**Pipeline B: OpenCV (Sophisticated)**
```python
# 7 parameters
denoise_h: 5-30
clahe_clip: 1.0-4.0
bilateral_d: 5-15
bilateral_sigma_color: 50-100
bilateral_sigma_space: 50-100
unsharp_amount: 0.3-2.0
unsharp_radius: 0.5-5.0
```

**Selection Logic:**
```python
for cluster in clusters:
    # Run both optimizers
    pil_best = optimize_pil(cluster_documents)
    opencv_best = optimize_opencv(cluster_documents)

    # Compare objectively
    if pil_best.total_edits < opencv_best.total_edits:
        cluster.winner = "PIL"
        cluster.best_filter = pil_best
    else:
        cluster.winner = "OpenCV"
        cluster.best_filter = opencv_best

    print(f"Cluster {cluster.name}: {cluster.winner} wins!")
```

**Possible Outcomes:**
- Cluster 0 (high contrast): PIL wins (simple is better)
- Cluster 1 (333BBB low contrast): OpenCV wins (needs sophisticated CLAHE)
- Cluster 2 (noisy): OpenCV wins (bilateral filter helps)
- Cluster 3 (complex): PIL wins (less aggressive = preserves layout)

**Let the algorithm decide empirically!**

---

### Stage 6: Build Production Filter Catalog

**Final Output:**
```json
{
  "cluster_0_high_contrast_222AAA_like": {
    "documents": ["222AAA", "555CCC"],
    "characteristics": {
      "contrast": "> 50",
      "noise": "< 10",
      "blur_score": "> 200"
    },
    "winner": "PIL",
    "best_filter": {
      "pipeline": "PIL",
      "contrast_factor": 1.3,
      "median_size": 3
    },
    "performance": {
      "avg_edits": 245,
      "improvement_vs_baseline": "15% better"
    }
  },

  "cluster_1_low_contrast_333BBB_heartbreaker": {
    "documents": ["333BBB", "333ccc"],
    "characteristics": {
      "contrast": "< 30",
      "noise": "moderate",
      "fft_high_freq_pct": "< 20%"
    },
    "winner": "OpenCV",
    "best_filter": {
      "pipeline": "OpenCV",
      "denoise_h": 18,
      "clahe_clip": 3.2,
      "bilateral_d": 11,
      "bilateral_sigma_color": 85,
      "bilateral_sigma_space": 90,
      "unsharp_amount": 1.5,
      "unsharp_radius": 2.0
    },
    "performance": {
      "Q2_333BBB_edits": 385,  # RESCUED from 576 → 385! Beat 431 baseline!
      "improvement_vs_baseline": "11% better"
    }
  },

  "cluster_2_noisy": { ... },
  "cluster_3_complex": { ... }
}
```

---

### Stage 7: Production Intelligent Selector

```python
def select_optimal_filter(document_image):
    """
    Production filter selector - uses catalog from cluster optimization.
    """
    # Step 1: Analyze document characteristics
    metrics = analyze_image_quality(document_image)  # Blur, noise, contrast, FFT

    # Step 2: Classify cluster
    features = [
        metrics['blur_score'],
        metrics['noise_score'],
        metrics['contrast'],
        metrics['brightness'],
        metrics['fft_high_freq_pct'],
        metrics['fft_freq_ratio'],
        metrics['fft_spectral_entropy']
    ]

    cluster_id = classifier_model.predict([features])[0]  # Trained on cluster results
    cluster_name = cluster_names[cluster_id]

    # Step 3: Load optimal filter for this cluster
    filter_config = catalog[cluster_name]['best_filter']

    # Step 4: Apply filter
    if filter_config['pipeline'] == 'PIL':
        enhanced = apply_pil_filter(document_image, filter_config)
    else:
        enhanced = apply_opencv_filter(document_image, filter_config)

    return enhanced, {
        'cluster': cluster_name,
        'pipeline': filter_config['pipeline'],
        'expected_performance': catalog[cluster_name]['performance']
    }
```

---

## Why This Approach is Revolutionary

### 1. Faster Convergence
```
Before: 8 VERY different objectives competing
After: 10-15 SIMILAR objectives per cluster

Similar objectives = algorithm finds optimum 3-4x faster!
```

### 2. Better Solutions
```
Before: One compromised filter (mediocre for all)
After: Specialized filters (excellent for specific cases)

333BBB gets its own cluster instead of competing with Q1!
```

### 3. Parallel Execution
```
Before: 1 job × 14 hours = 14 hours
After: 4 jobs × 4 hours (parallel) = 4 hours

75% time savings!
```

### 4. Empirical Pipeline Selection
```
No theoretical guessing!
Each cluster tests PIL vs OpenCV empirically
Winner determined by objective Levenshtein distance
```

### 5. Production-Ready Mapping
```python
# Simple, fast, accurate
metrics = analyze(document)
cluster = classify(metrics)
filter = catalog[cluster]
enhanced = apply(document, filter)
```

---

## Implementation Timeline

**Today (Server Run 1 completes):**
- ✅ Extract best filters from Run 1 Pareto front
- ✅ Launch PIL Q2-only quick test (~2 hours)

**Tomorrow:**
1. Generate degradation spectrum (Pristine → Q3)
2. Build performance matrix (test all filters on spectrum)
3. Cluster documents by similarity
4. Identify cluster characteristics

**Day 3:**
1. Launch 4 parallel cluster GAs (one per cluster)
2. Each tests PIL + OpenCV
3. Wait ~4 hours for all to complete
4. Extract winners per cluster

**Day 4:**
1. Build filter catalog JSON
2. Train cluster classifier
3. Implement production selector
4. Deploy and A/B test

---

## Adaptive Filter Strategy (Future Enhancement)

### Approach #1: Image Quality Metrics → Dynamic Parameters (SUPERSEDED BY CLUSTER APPROACH)

**Concept:** Analyze image characteristics, then select filter parameters dynamically.

**Metrics to Measure:**
1. **Blur Detection** - Laplacian variance (low = blurry → more denoising)
2. **Noise Level** - High-frequency analysis (high = noisy → aggressive denoising)
3. **Contrast** - Standard deviation (low = needs CLAHE boost)
4. **Brightness** - Mean intensity (too dark/bright → adjust)

**Implementation:**
```python
def analyze_image_quality(image):
    """Analyze image to determine optimal filter parameters."""
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # Blur detection (Laplacian variance)
    blur_score = cv2.Laplacian(gray, cv2.CV_64F).var()

    # Noise estimation (high-frequency analysis)
    noise_score = estimate_noise_level(gray)

    # Contrast measurement
    contrast = gray.std()

    # Brightness
    brightness = gray.mean()

    return {
        'blur': blur_score,      # Low = blurry
        'noise': noise_score,    # High = noisy
        'contrast': contrast,    # Low = needs CLAHE
        'brightness': brightness # Too dark/bright
    }

def select_denoising_strength(noise_score):
    """Decide denoising parameter based on noise level."""
    if noise_score > 50:      # Very noisy
        return 30
    elif noise_score > 20:    # Medium noise
        return 10
    else:                     # Clean
        return 5

def select_clahe_strength(contrast):
    """Decide CLAHE parameter based on contrast."""
    if contrast < 30:         # Very low contrast
        return 3.0
    elif contrast < 50:       # Medium contrast
        return 2.0
    else:                     # Good contrast
        return 1.5

def adaptive_enhance(image):
    """Smart enhancement that analyzes image first."""
    metrics = analyze_image_quality(image)

    denoise_h = select_denoising_strength(metrics['noise'])
    clahe_clip = select_clahe_strength(metrics['contrast'])

    # Apply adaptive filters
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    denoised = cv2.fastNlMeansDenoising(gray, h=denoise_h)
    clahe = cv2.createCLAHE(clipLimit=clahe_clip, tileGridSize=(8,8))
    enhanced = clahe.apply(denoised)

    return enhanced, metrics  # Return metrics for logging/analysis
```

**Advantages:**
- No training data needed
- Fast (real-time analysis)
- Interpretable (you know WHY it chose parameters)
- Works across different document sources
- Easy to tune thresholds based on empirical results

**Testing Strategy:**
1. Run on pristine PRP1 originals → should select minimal enhancement
2. Run on Q1-Q4 degraded images → should adapt parameters
3. Compare results against fixed-parameter baseline

---

### Approach #2: Document Type Classification → Filter Recipe Catalog

**Concept:** Build a catalog of pre-tuned filter presets for different document types.

**Filter Recipes:**
```python
FILTER_RECIPES = {
    'sat_form_printed': {      # SAT printed forms (high quality)
        'denoising_h': 5,
        'clahe_clip': 1.5,
        'bilateral': True
    },
    'sat_form_scanned_poor': { # Poor quality scans
        'denoising_h': 20,
        'clahe_clip': 3.0,
        'bilateral': True
    },
    'photo_of_document': {     # Phone photos (perspective issues)
        'denoising_h': 15,
        'clahe_clip': 2.5,
        'bilateral': True,
        'perspective_correction': False  # Dangerous - see Phase 3 results
    },
    'fax_transmission': {      # Fax documents (heavy noise)
        'denoising_h': 25,
        'clahe_clip': 2.0,
        'bilateral': True
    }
}

def classify_document_type(image):
    """Use simple rules to classify document type."""
    # Check resolution (fax = 200dpi, scan = 300dpi, photo = varies)
    # Check if borders detected (scanned vs photo)
    # Check text density (form vs letter)
    # Check for form lines/structure
    return document_type

def apply_recipe(image, recipe):
    """Apply pre-tuned filter recipe."""
    # Apply filters with recipe parameters
```

**Advantages:**
- Expert knowledge encoded in recipes
- Predictable behavior per document type
- Easy to A/B test different recipes

**Disadvantages:**
- Requires accurate document classification
- Limited to predefined types
- More brittle than metrics-based approach

---

### Approach #3: FFT-Based Noise Analysis

**Concept:** Analyze noise patterns in frequency domain to detect specific artifacts (scanner lines, compression, etc.)

**Implementation:**
```python
def analyze_noise_spectrum(image):
    """FFT analysis to detect noise patterns."""
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # Compute FFT
    f = np.fft.fft2(gray)
    fshift = np.fft.fftshift(f)
    magnitude = np.abs(fshift)

    # Analyze high-frequency components (noise indicator)
    h, w = magnitude.shape
    center_mask = np.zeros((h, w))
    # Exclude low frequencies (document structure)
    cv2.circle(center_mask, (w//2, h//2), 30, 1, -1)
    high_freq_energy = np.sum(magnitude * (1 - center_mask))

    # Detect periodic noise (scanner artifacts, JPEG compression)
    peaks = detect_frequency_peaks(magnitude)

    return {
        'high_freq_energy': high_freq_energy,
        'periodic_noise': peaks,
        'compression_artifacts': detect_compression_blocks(magnitude)
    }
```

**Use Cases:**
- Detect JPEG compression artifacts → apply deblocking filter
- Detect scanner line noise → apply frequency-domain filter
- Detect periodic patterns → targeted notch filtering

**Advantages:**
- Detects specific noise types accurately
- Can apply targeted fixes (notch filters for periodic noise)

**Disadvantages:**
- More complex to implement
- Slower than spatial domain analysis
- May be overkill for this use case

---

### Approach #4: Reference-Based Optimization (Most Sophisticated)

**Concept:** Compare enhanced image against a "perfect" reference document, iteratively tune parameters to maximize similarity.

**Implementation:**
```python
def optimize_filters_against_reference(degraded_img, reference_img):
    """Find best filter params by comparing to reference."""
    from skimage.metrics import structural_similarity as ssim

    best_ssim = 0
    best_params = None

    # Grid search over parameter space
    for denoise_h in [5, 10, 15, 20, 30]:
        for clahe_clip in [1.5, 2.0, 2.5, 3.0]:
            # Apply filters
            enhanced = apply_filters(degraded_img, denoise_h, clahe_clip)

            # Compare to reference (SSIM = structural similarity)
            similarity = ssim(enhanced, reference_img, data_range=255)

            if similarity > best_ssim:
                best_ssim = similarity
                best_params = (denoise_h, clahe_clip)

    return best_params, best_ssim
```

**Advantages:**
- Objectively optimal parameters for that specific document
- Can validate enhancement effectiveness
- Useful for testing/tuning

**Disadvantages:**
- Requires reference "perfect" document (not available in production)
- Computationally expensive (grid search)
- Only useful for testing/development

**Use in Testing:**
- Use pristine PRP1 originals as references
- Optimize against degraded versions
- Validate that adaptive approach selects similar parameters

---

## Implementation Roadmap

### Phase 1: Prototype Image Quality Analyzer (CURRENT)
- ✅ Implement `analyze_image_quality()` function
- ✅ Test on pristine PRP1 originals (expect high quality scores)
- ✅ Test on Q1-Q4 degraded images (expect quality degradation detection)
- ✅ Log metrics to understand threshold tuning

### Phase 2: Adaptive Parameter Selection
- Implement `select_denoising_strength()` and `select_clahe_strength()`
- Run on Q2 images to see if adaptive params rescue 333BBB (69.62% → 70%+?)
- Compare adaptive vs fixed parameters on all quality levels

### Phase 3: Reference-Based Validation
- Use pristine PRP1 as references
- Optimize parameters against degraded versions
- Validate that adaptive approach converges to similar values

### Phase 4: Production Integration (If Successful)
- Replace fixed enhancement with adaptive enhancement
- Add metrics logging to database (track image quality over time)
- Monitor production results vs baseline

---

## Key Learnings

1. **Binarization is harmful for ML-based OCR** - Destroys gradient information needed by Tesseract LSTM and GOT-OCR2
2. **Deskewing is dangerous** - Contour-based angle detection systematically wrong on these documents (detected -90° on ALL images)
3. **PSM 6 is optimal** - Produces 6x more text than other PSM modes
4. **GOT-OCR2 adds minimal value** - Complexity not justified, Tesseract alone is sufficient
5. **70% threshold is realistic** - Q2 images at 50% rescue rate (2/4 passing) appears to be the ceiling for filter-based enhancement
6. **Early rejection strategy is correct** - Let downstream validation catch false accepts, focus OCR stage on rejecting garbage

---

## Testing Files Generated

### Degraded Fixtures
```
Fixtures/PRP1_Degraded/
├── Q1_Poor/          # 4 images (78-92% baseline)
├── Q2_MediumPoor/    # 4 images (42-53% baseline) ← Target for rescue
├── Q3_Low/           # 4 images (25-27% baseline)
└── Q4_VeryLow/       # 4 images (0-15% baseline)
```

### Enhanced Fixtures
```
Fixtures/PRP1_Enhanced/       # Standard enhancement (h=10, CLAHE 2.0)
Fixtures/PRP1_Enhanced_Aggressive/  # FAILED - binarization + deskewing
```

### PSM Test Results
```
Fixtures/PRP1_Degraded/Q2_MediumPoor/
├── test1.psm1.txt    # 344 bytes (PSM 1 - Auto + OSD)
├── test1.psm3.txt    # 344 bytes (PSM 3 - Fully auto)
├── test1.psm4.txt    # 454 bytes (PSM 4 - Single column)
└── test1.psm6.txt    # 2.2KB (PSM 6 - Uniform block) ⭐ BEST
```

---

## References

- Phase 1 Results: `docs/Phase1_Degradation_Baseline_Results.md`
- Phase 2 Results: `docs/Phase2_Enhancement_ROI_Final_Results.md`
- Enhancement Scripts:
  - `scripts/enhance_images.py` (standard, RECOMMENDED)
  - `scripts/enhance_images_aggressive.py` (FAILED experiment)
- Test Files:
  - `Tests.Infrastructure.Extraction.GotOcr2/TesseractOcrExecutorDegradedTests.cs`
  - `Tests.Infrastructure.Extraction.GotOcr2/TesseractOcrExecutorEnhancedTests.cs`
  - `Tests.Infrastructure.Extraction.GotOcr2/TesseractOcrExecutorEnhancedAggressiveTests.cs`

---

## ACTUAL CLUSTERING RESULTS (2025-11-27)

**Status:** COMPLETED - Spectrum analysis validates single-cluster Q2-focused strategy

### Degradation Spectrum Generated

Created 5 quality levels with smooth parameter interpolation:
- Q0_Pristine: 0.00 blur, 0.000 noise, 1.00 contrast, 100 JPEG
- Q0.5_VeryGood: 0.25 blur, 0.010 noise, 0.97 contrast, 95 JPEG
- Q1_Poor: 0.50 blur, 0.020 noise, 0.95 contrast, 90 JPEG
- Q1.5_Medium: 0.85 blur, 0.035 noise, 0.90 contrast, 80 JPEG
- Q2_MediumPoor: 1.20 blur, 0.050 noise, 0.85 contrast, 70 JPEG

**Total generated:** 20 images (4 documents × 5 quality levels)

### Performance Matrix: ALL Documents are HIGH Sensitivity

| Document | Q0→Q1 | Q1→Q2 | Total (Q0→Q2) | Avg Rate (edits/step) | Sensitivity |
|----------|-------|-------|---------------|----------------------|-------------|
| 222AAA   | +175  | +635  | +810          | 202.5                | **HIGH** |
| 333BBB   | +109  | +629  | +738          | 184.5                | **HIGH** |
| 333ccc   | +148  | +681  | +829          | 207.2                | **HIGH** |
| 555CCC   | +34   | +1628 | +1662         | 415.5                | **HIGH** |

**Critical Finding: SINGLE CLUSTER**
- All 4 documents exhibit HIGH sensitivity (>150 edits/step)
- 555CCC is ULTRA-fragile: gentle Q0→Q1 (+34) but catastrophic Q1→Q2 (+1628)
- Hypothetical multi-cluster strategy was wrong
- **Result:** Single Q2-focused optimizer is optimal

### PIL Q2-Only Optimizer Results (100% COMPLETE!)

**Configuration:**
- Population: 20, Generations: 30, Runtime: ~2 hours
- Pipeline: PIL (contrast + median filter only)
- Parameters: 2 (vs OpenCV's 7)
- Objectives: 4 (Q2 documents only)

**Final Performance:**
- **Q2_333BBB: 371 edits** (baseline: 431 = **13.9% improvement!**)
- **Total Q2: 1,126 edits** [274, 371, 320, 161]
- **Pareto front: 20 optimal solutions**
- OpenCV Server failed: 576 edits (34% worse)
- OpenCV Windows failed: 595 edits (38% worse)

**Optimal Parameters (Best Overall Solution):**
```python
contrast_factor = 1.157  # Very conservative enhancement
median_size = 3          # Small 3×3 kernel
```

**Why PIL Wins:**
1. Simpler pipeline (2 params vs 7) = faster convergence
2. Single cluster = no compromised objectives
3. Conservative enhancement (1.157 contrast) beats aggressive
4. Small median kernel (3×3) preserves text detail

**Files Generated:**
- `Prisma/Fixtures/PRP1_Spectrum/` - 20 degraded spectrum images
- `Prisma/Fixtures/spectrum_performance_matrix.json` - Complete OCR data
- `Prisma/Fixtures/spectrum_performance_matrix.csv` - Human-readable table
- `Prisma/Fixtures/degradation_curves_summary.txt` - Cluster analysis
- `Prisma/Fixtures/nsga2_q2_pil_pareto_front.json` - 20 Pareto optimal solutions
- `Prisma/scripts/generate_degradation_spectrum.py` - Spectrum generator
- `Prisma/scripts/build_spectrum_performance_matrix.py` - Matrix builder

---

## SIMPLIFIED PRODUCTION STRATEGY (Single Cluster Reality)

**Finding:** All 4 documents belong to ONE HIGH-sensitivity cluster
**Impact:** No complex multi-cluster classification needed!

### Production Filter Catalog

**Top 5 Filters from 20 Pareto Solutions:**

| Filter | Contrast | Median | 333BBB | Total | Use When |
|--------|----------|--------|--------|-------|----------|
| **optimal_overall** | 1.157 | 3 | 371 | 1126 | **Default** - best balance |
| conservative | 1.040 | 3 | 403 | 1151 | High quality input |
| aggressive | 1.878 | 3 | 389 | 1158 | Heavy degradation |
| balanced | 1.525 | 3 | 387 | 1175 | Medium degradation |
| alt_balanced | 1.818 | 3 | 398 | 1210 | Alternative balance |

**Key Pattern:** ALL use `median_size = 3` (small kernel preserves text detail)

### Production Implementation

**Simple 3-step process:**

```python
# Step 1: Analyze image quality
metrics = analyze_image_quality(image)

# Step 2: Select filter from catalog
if metrics['contrast'] > 50 and metrics['blur_score'] > 200:
    filter_params = CATALOG['conservative']  # High quality
elif metrics['contrast'] < 30 or metrics['blur_score'] < 100:
    filter_params = CATALOG['aggressive']    # Heavy degradation
else:
    filter_params = CATALOG['optimal_overall']  # Default

# Step 3: Apply PIL enhancement
enhanced = apply_pil_filter(image, filter_params)
```

**No clustering ML needed!** Just quality metrics → lookup table → filter

---

## Next Steps

**Immediate (This Week):**
1. ✅ PIL Q2 optimizer complete (371 edits, 13.9% improvement!)
2. 📋 Create production filter catalog JSON from Pareto front
3. 📋 Implement `analyze_image_quality()` function (blur, noise, contrast)
4. 📋 Implement `select_optimal_filter()` decision tree
5. 📋 C# integration with Python enhancement service

**Short-term (Next 2 Weeks):**
1. Deploy to production with A/B testing (baseline vs optimal)
2. Monitor real-world performance metrics
3. Collect image quality distributions
4. Fine-tune selection thresholds based on production data

**Future (If Needed):**
1. Add more filters if new document patterns emerge
2. Paper submission to Computación y Sistemas
3. Explore multi-stage pipelines if single-stage plateaus
