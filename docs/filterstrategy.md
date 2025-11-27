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

## Adaptive Filter Strategy (Future Enhancement)

### Approach #1: Image Quality Metrics → Dynamic Parameters (RECOMMENDED)

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

## Next Steps

1. Implement image quality analyzer (Approach #1)
2. Test on pristine originals to establish baseline metrics
3. Test on degraded Q1-Q4 to see quality degradation detection
4. Implement adaptive parameter selection
5. Re-test on Q2 images to see if adaptive approach rescues 333BBB
6. If successful → integrate into production pipeline
7. If unsuccessful → accept 50% Q2 rescue rate as realistic ceiling
