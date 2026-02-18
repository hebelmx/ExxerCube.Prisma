# Phase 2D - Parallel Development Progress

**Date:** 2025-11-27
**Status:** Built while Server Run 1 executes (Gen 26/50)

---

## Parallel Workstreams Strategy

**Server (Ubuntu):** NSGA-II Full Run (8 objectives, Pop: 100, Gen: 50)
- Status: Gen 26/50 (~52% complete, ETA: 6-7 hours)
- Best Q1 so far: 334 edits (44% better than baseline!)
- Best Q2_333BBB so far: 576 edits (vs 431 baseline)
- Total runtime: ~14 hours from start

**Workstation (Windows):** Phase 2D Tools Development
- **FFT Spectrum Analyzer** ✅ COMPLETE
- **Q2-Only Optimizer** ✅ COMPLETE
- Top-4 Filter Selection Strategy ⏳ PENDING (awaits server results)
- Performance Matrix Builder ⏳ PENDING (awaits server results)

---

## Tools Built (While Server Runs)

### 1. FFT Spectrum Analysis

**File:** `Prisma/scripts/analyze_image_quality.py`

**New Metrics Added:**
```python
fft_high_freq_energy    # Edges, text, noise
fft_low_freq_energy     # Smooth areas, backgrounds
fft_freq_ratio          # High/Low ratio (texture indicator)
fft_peak_frequency      # Periodic patterns (scan artifacts)
fft_spectral_entropy    # Frequency distribution complexity
fft_high_freq_pct       # % energy in high frequencies
fft_low_freq_pct        # % energy in low frequencies
```

**Complete Metric Suite:**
- **Spatial Domain:** Blur, Noise, Contrast, Brightness
- **Frequency Domain:** FFT spectrum analysis (NEW!)

**Purpose:**
- Characterize images in frequency space
- Detect periodic noise/artifacts
- Measure texture complexity
- Input features for intelligent filter mapping

---

### 2. Q2-Only NSGA-II Optimizer

**File:** `Prisma/scripts/optimize_filters_nsga2_q2_only.py`

**Configuration:**
- **4 Objectives:** Q2 documents ONLY (222AAA, 333BBB, 333ccc, 555CCC)
- **NO Q1 Compromise:** Pure Q2 rescue optimization
- **Population:** 50
- **Generations:** 50
- **Total Evaluations:** 2,500
- **Estimated Runtime:** ~7 hours

**vs Full Run:**
| Metric | Full Run | Q2-Only |
|--------|----------|---------|
| Objectives | 8 (Q1+Q2) | 4 (Q2 only) |
| Population | 100 | 50 |
| Evaluations | 5,000 | 2,500 |
| Runtime | ~14 hours | ~7 hours |
| Q1 Optimization | Yes | **NO** |
| Q2 Optimization | Compromised | **Maximum** |

**Special Features:**
- 333BBB "heartbreaker" analysis (baseline: 431 edits)
- Automatic comparison to baseline
- Outputs: JSON Pareto front, progress log, checkpoint

**Usage:**
```bash
cd Prisma/scripts
python optimize_filters_nsga2_q2_only.py
```

---

## Multi-Run Evolutionary Strategy

### Run 1 (Server - Current): Balanced Exploration
- **8 Objectives:** Q1 + Q2 (balance performance)
- **Status:** Gen 26/50, ETA: 6-7 hours
- **Discovery:** Q1 specialists already found (334 edits - 44% better!)
- **Q2 Status:** Still converging (best: 576 edits vs 431 baseline)

### Run 2 (After Run 1): Q2 Specialists
- **4 Objectives:** Q2 ONLY (no Q1 compromise)
- **Duration:** ~7 hours
- **Warm Start:** Seed with Run 1's best Q2 solutions
- **Goal:** Beat 431 edits on 333BBB

### Run 3+ (If Needed): Refinement
- Constrained optimization: "Q1 < 400 edits" (force Q2 focus)
- Expanded search bounds around promising regions
- Different crossover/mutation operators

---

## Top-4 Filter Selection Strategy

**Why Top-4 (Not All Solutions)?**

1. **Efficiency:** 4 filters vs 30-50 Pareto solutions
2. **Interpretability:** Small catalog easy to understand/debug
3. **Coverage:** Sufficient for diverse document types
4. **Performance:** Fast lookup in production

**Selection Criteria:**

**Filter Type A - Q1 Specialist:**
- Minimize: Total Q1 edits
- Use when: High blur_score (>200), good contrast (>50), clean FFT
- Best candidate: Eval 20480 (Q1: 334 edits)

**Filter Type B - Q2 Specialist:**
- Minimize: Total Q2 edits
- Use when: Poor contrast (<30), high noise, noisy FFT
- Best candidate: TBD from Run 1 final results

**Filter Type C - 333BBB Heartbreaker Specialist:**
- Minimize: Q2_333BBB edits specifically
- Use when: Document matches 333BBB characteristics
- Goal: Beat 431 edits baseline

**Filter Type D - Balanced Safe:**
- Minimize: Total edits across all documents
- Use when: Quality uncertain or mixed batch
- Safest choice for unknown documents

---

## Filter Performance Matrix Design

**Structure:**
```python
performance_matrix = {
    "filter_id_A": {
        "pristine": {
            "222AAA": {"distance": 50, "cer": 0.05},
            "333BBB": {"distance": 45, "cer": 0.04},
            # ...
        },
        "Q1_Poor": { ... },
        "Q2_MediumPoor": { ... },
        "Q3_Low": { ... },
        "Q4_VeryLow": { ... }
    },
    "filter_id_B": { ... },
    # ... top 4 filters
}
```

**Generation Process:**
1. Load top-4 Pareto solutions from Run 1
2. For each filter:
   - Apply to ALL documents (pristine, Q1, Q2, Q3, Q4)
   - Run Tesseract OCR
   - Compute Levenshtein distance
   - Store in matrix
3. Save to JSON for analysis

**Analysis Queries:**
```python
# Best filter for Q2 documents
best_q2 = min(filters, key=lambda f: sum(matrix[f]["Q2_MediumPoor"].values()))

# Safest filter (lowest worst-case degradation)
safest = min(filters, key=lambda f: max(matrix[f]["Q1_Poor"].values()))

# 333BBB specialist
best_333bbb = min(filters, key=lambda f: matrix[f]["Q2_MediumPoor"]["333BBB"])
```

---

## Intelligent Filter Mapping

**Input Features (10 metrics):**
```python
X = [
    blur_score,           # Laplacian variance
    noise_score,          # MAD estimate
    contrast,             # Std deviation
    brightness,           # Mean intensity
    fft_high_freq_pct,    # High frequency energy %
    fft_low_freq_pct,     # Low frequency energy %
    fft_freq_ratio,       # Texture indicator
    fft_peak_frequency,   # Periodic artifacts
    fft_spectral_entropy, # Complexity
    edge_density          # TBD: Canny edge count
]
```

**Output:** `best_filter_id` (A, B, C, or D)

**Approach Options:**

### Option 1: Rule-Based (Interpretable)
```python
if contrast < 25 and fft_high_freq_pct < 20:
    return "filter_B"  # Q2 specialist (poor contrast, smooth)
elif blur_score > 200 and contrast > 50:
    return "filter_A"  # Q1 specialist (sharp, good contrast)
elif matches_333bbb_profile(metrics):
    return "filter_C"  # 333BBB specialist
else:
    return "filter_D"  # Balanced safe default
```

### Option 2: ML Classifier (Data-Driven)
```python
from sklearn.tree import DecisionTreeClassifier
from sklearn.ensemble import RandomForestClassifier

# Training data: (metrics, best_filter_id) from performance matrix
model = DecisionTreeClassifier(max_depth=5)  # Interpretable
# OR
model = RandomForestClassifier(n_estimators=100)  # Accurate

model.fit(X_train, y_train)

# Production inference
metrics = analyze_image_quality(document)
best_filter = model.predict([metrics])[0]
```

### Option 3: Hybrid (Best of Both)
```python
# ML suggests filter
suggested = ml_model.predict(metrics)

# Safety check: Q1 documents shouldn't use aggressive filters
if baseline_quality > 75% and filter_risk[suggested] > 0.3:
    return "filter_D"  # Safe fallback
else:
    return suggested
```

---

## Next Steps (When You Return)

### Immediate (Server Results Available):
1. Pull final `nsga2_pareto_front.json` from server
2. Extract top-4 filters using selection criteria
3. Generate performance matrix for top-4
4. Analyze 333BBB rescue success

### Analysis:
1. Run FFT analyzer on all fixture sets
2. Correlate image metrics with filter performance
3. Identify patterns: "Low contrast → Filter B works best"
4. Build filter selection rules

### Run 2 Launch:
1. Warm start Q2-only optimizer with Run 1 best solutions
2. Start 7-hour Run 2 on server
3. While running: Build ML classifier for filter mapping

### Phase 3 (Production Deployment):
1. Implement intelligent filter selector
2. A/B testing in staging
3. Monitor rescue rates and quality
4. Continuous optimization (retrain with production data)

---

## Current Git Status

**Commits:**
- `5299ff8` - FFT analysis + Q2-only optimizer ✅
- `8b2777f` - Phase 2 experimental testing ✅
- `0efc1aa` - NSGA-II breakthrough ✅
- `3eae67d` - Server deployment guide ✅

**Branch:** Kt2 (synced with origin)

**Ready for Run 2:** Yes! ✅

---

## Estimated Timeline

**Server Run 1 Complete:** ~6-7 hours from now
**Your Return:** ~7 hours from now (meeting)
**Run 2 Launch:** Immediately upon your return
**Run 2 Complete:** +7 hours (overnight)
**Analysis & Deployment:** Next day

**By tomorrow:** Full Pareto catalog (Run 1 + Run 2), intelligent mapping ready for implementation.

---

**This is parallel optimization at its finest - both computational (server) and developmental (workstation) running concurrently!** 🚀
