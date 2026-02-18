# Adaptive OCR Enhancement through Polynomial Regression on Image Properties: A Per-Cluster Genetic Algorithm Approach

**Author:**
Abel Briones Ramirez
Exxerpro Solutions SA de CV

**Correspondence:** abel.briones@exxerpro.com

**Date:** November 2024

**Keywords:** OCR, Image Enhancement, Genetic Algorithms, Polynomial Regression, Document Processing, Machine Learning

---

## Abstract

We present a novel methodology for optimizing OCR (Optical Character Recognition) preprocessing filters using a combination of genetic algorithms and polynomial regression. Unlike traditional approaches that seek a universal filter configuration, our method clusters documents by measurable image properties and optimizes filter parameters independently for each cluster. A polynomial regression model then enables continuous interpolation for unseen images. Our approach achieves an **18.4% reduction in OCR edit distance** compared to unprocessed images, outperforming discrete lookup tables by 49%. The resulting model predicts 5 filter parameters from 4 image features with R² > 0.89 for all parameters.

---

## 1. Introduction

### 1.1 Problem Statement

Document digitization pipelines frequently encounter degraded images—scans with blur, noise, low contrast, or scanning artifacts. OCR engines perform suboptimally on such images, leading to transcription errors that propagate through downstream processing. While image enhancement filters can improve OCR accuracy, selecting optimal filter parameters remains challenging because:

1. Different degradation types require different corrections
2. The relationship between image properties and optimal filters is non-linear
3. Manual tuning is time-consuming and does not generalize

### 1.2 Contributions

This work makes the following contributions:

1. **Per-cluster optimization**: We demonstrate that clustering images by measurable properties (not OCR output) and optimizing filters per cluster significantly outperforms universal filter approaches.

2. **Polynomial interpolation model**: We show that polynomial regression on cluster centroids enables continuous filter prediction for unseen images, achieving better generalization than discrete lookup tables.

3. **Reproducible methodology**: We provide a complete pipeline from dataset generation through validation, including all scripts and trained model coefficients.

4. **Production-ready implementation**: We deliver both Python inference code and C# implementation guidelines for integration into enterprise document processing systems.

---

## 2. Related Work

### 2.1 Traditional Image Enhancement for OCR

Classical approaches apply fixed filter chains: binarization, noise reduction, and contrast enhancement. While effective for uniform document types, these methods fail when document quality varies significantly.

### 2.2 Adaptive Enhancement

Recent work explores adaptive methods that analyze image properties before selecting filters. However, most approaches use rule-based thresholds rather than learned mappings, limiting their adaptability.

### 2.3 Evolutionary Optimization

Genetic algorithms have been applied to image processing parameter optimization, but typically seek single optimal solutions rather than models that predict parameters for new inputs.

---

## 3. Methodology

### 3.1 Overview

Our pipeline consists of five stages:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  1. Dataset     │───▶│  2. Feature     │───▶│  3. Clustering  │
│  Generation     │    │  Extraction     │    │  (K-Means)      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                                      │
                                                      ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  5. Polynomial  │◀───│  4. Per-Cluster │◀───│  Cluster        │
│  Model Fitting  │    │  GA Optimization│    │  Assignments    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │
         ▼
┌─────────────────┐
│  6. Production  │
│  Inference      │
└─────────────────┘
```

### 3.2 Dataset Generation

We created a balanced dataset of 64 degraded images from 4 pristine source documents (Spanish legal/financial documents). Each document was degraded to 4 OCR confidence bands (50s%, 60s%, 70s%, 80s%) with 4 artifact variants per band:

- **Blur**: Gaussian blur (σ calibrated per document)
- **Texture**: Paper texture overlay
- **Scan lines**: Simulated scanner artifacts
- **Vignette**: Edge darkening
- **Skew**: Slight rotation (±2°)

**Critical insight**: Balance matters. Our v5 dataset had 26/39 images from one document, causing the model to overfit to that document's characteristics. Version 6 enforced strict balance: exactly 4 images per document per confidence band.

### 3.3 Feature Extraction

We extract 4 properties from each image that characterize its degradation:

| Feature | Computation | Interpretation |
|---------|-------------|----------------|
| `blur_score` | Laplacian variance | Higher = sharper |
| `contrast` | Grayscale std dev | Higher = more contrast |
| `noise_estimate` | Mean absolute Laplacian | Higher = more noise |
| `edge_density` | Canny edge pixel ratio | Higher = more edges |

These features were chosen because they:
1. Are computable without OCR (input properties, not outputs)
2. Correlate with degradation type/severity
3. Are discriminative between different corrections needed

### 3.4 Clustering

We applied K-Means clustering with k=6 (determined by silhouette score analysis). The clusters naturally separated images by degradation characteristics:

| Cluster | Size | Characteristics |
|---------|------|-----------------|
| 0 | 18 | High blur, low contrast |
| 1 | 17 | Medium blur, high noise |
| 2 | 2 | (Merged with 3) |
| 3 | 4 | Low blur, high contrast |
| 4 | 14 | Medium blur, texture artifacts |
| 5 | 9 | High blur, scan lines |

### 3.5 Per-Cluster Genetic Algorithm Optimization

For each cluster, we ran an independent genetic algorithm to find optimal filter parameters:

**GA Configuration:**
```
Population size: 30
Generations: 25
Crossover probability: 0.7
Mutation probability: 0.3
Selection: Tournament (k=3)
```

**Filter Parameter Search Space:**
| Parameter | Range | Description |
|-----------|-------|-------------|
| contrast | [0.8, 1.5] | PIL ImageEnhance.Contrast |
| brightness | [0.9, 1.2] | PIL ImageEnhance.Brightness |
| sharpness | [0.8, 2.5] | PIL ImageEnhance.Sharpness |
| unsharp_radius | [0.5, 3.0] | UnsharpMask radius |
| unsharp_percent | [50, 200] | UnsharpMask strength |

**Fitness Function:**
Levenshtein edit distance between Tesseract OCR output and ground truth (pristine document OCR), summed across all images in the cluster.

### 3.6 Polynomial Model Fitting

With optimal filters for each cluster centroid, we fit a degree-2 polynomial regression model:

```
filter_param = f(blur, contrast, noise, edge_density)
             = intercept + Σ(βᵢ × xᵢ) + Σ(βᵢⱼ × xᵢ × xⱼ)
```

We used:
- StandardScaler normalization for input features
- Ridge regression (α=1.0) to prevent overfitting
- Degree 2 polynomials (15 features including interactions)

---

## 4. Results

### 4.1 Model Performance

| Parameter | R² Score | MAE |
|-----------|----------|-----|
| contrast | 0.949 | 0.052 |
| brightness | 0.987 | 0.004 |
| sharpness | 0.947 | 0.089 |
| unsharp_radius | 0.938 | 0.197 |
| unsharp_percent | 0.897 | 16.4 |

All R² scores exceed 0.89, indicating excellent predictive capability.

### 4.2 Validation on Unseen Images

We generated 32 new test images with intermediate blur values not present in the training set (to test interpolation capability).

| Method | Avg Edit Distance | Improvement | Wins |
|--------|-------------------|-------------|------|
| No filter | 755.0 | baseline | - |
| Lookup table | 661.9 | 93.1 (12.3%) | 11 |
| **Polynomial** | **616.4** | **138.6 (18.4%)** | **21** |

**Key finding**: Polynomial interpolation wins 21 vs 11 against lookup tables, achieving 49% more improvement. This validates our hypothesis that continuous interpolation generalizes better than discrete cluster assignment.

### 4.3 Comparison with Previous Approaches

| Approach | R² | Edit Distance Improvement |
|----------|-----|---------------------------|
| Universal NSGA-II | 0.037 | ~5% |
| Cluster by OCR confidence | 0.15 | ~8% |
| **Per-cluster GA + Polynomial** | **0.89+** | **18.4%** |

---

## 5. Discussion

### 5.1 Why Universal Filters Fail

Our initial NSGA-II approach sought a single optimal filter for all images. This failed (R²=0.037) because:

1. Different documents need different corrections
2. The Pareto front contained trade-offs, not global optima
3. Averaging across diverse images masks important patterns

### 5.2 Why Clustering by OCR Confidence Fails

Clustering by OCR confidence (the output we're trying to improve) is circular:
- Two images with identical confidence may need completely different filters
- Confidence doesn't predict what correction is needed, only that correction is needed

### 5.3 Why Image Property Clustering Succeeds

Clustering by input properties (blur, contrast, noise, edges) works because:
- These features measure *what's wrong* with the image
- *What's wrong* predicts *what filter is needed*
- Similar degradations require similar corrections

### 5.4 Limitations

1. **Domain specificity**: Model trained on Spanish legal/financial documents; may need retraining for other document types
2. **Feature selection**: Current 4 features may not capture all degradation types (e.g., JPEG artifacts, color shifts)
3. **OCR engine dependency**: Optimized for Tesseract; other engines may have different optimal parameters

---

## 6. Implementation

### 6.1 Production Inference Pipeline

```python
from production_filter_inference import enhance_for_ocr
from PIL import Image

image = Image.open("degraded_document.png")
enhanced, metadata = enhance_for_ocr(image, method="polynomial")
# enhanced image ready for OCR
```

### 6.2 C# Integration

Complete implementation guide provided in `POLYNOMIAL_FILTER_IMPLEMENTATION_GUIDE.md`, including:
- EmguCV-based feature extraction
- Polynomial coefficient embedding
- Filter application chain
- Dependency injection registration

---

## 7. Conclusion

We presented a methodology for adaptive OCR enhancement that:

1. **Clusters images by measurable properties** rather than OCR outputs
2. **Optimizes filters per cluster** using genetic algorithms
3. **Interpolates continuously** using polynomial regression

This approach achieves 18.4% OCR improvement on degraded documents, outperforming both universal filters and discrete lookup tables. The methodology is reproducible, and we provide production-ready implementations in both Python and C#.

---

## 8. Future Work

1. **Expanded feature set**: FFT-based frequency analysis, local contrast measures
2. **Additional document types**: Handwritten documents, receipts, forms
3. **Online learning**: Update model as new documents are processed
4. **Confidence estimation**: Predict expected improvement before applying filter
5. **Multi-engine optimization**: Optimize for multiple OCR engines simultaneously

---

## 9. Reproducibility

All code and data are available in the project repository:

### Scripts
| File | Purpose |
|------|---------|
| `generate_degradation_spectrum_v6.py` | Dataset generation |
| `cluster_by_image_properties.py` | K-Means clustering |
| `ga_single_cluster.py` | Per-cluster GA optimization |
| `fit_polynomial_model_v2.py` | Polynomial model training |
| `validate_inference_methods.py` | Validation |
| `production_filter_inference.py` | Production inference |

### Data
| File | Purpose |
|------|---------|
| `Fixtures/PRP1_Degraded_v6/` | Training images (64) |
| `Fixtures/polynomial_model_v2.json` | Trained model |
| `Fixtures/validation_test/` | Validation results |

### Runtime
- Training: ~8 hours on 6-core CPU (GA optimization is bottleneck)
- Inference: <100ms per image

---

## Acknowledgments

This work was conducted as part of the Prisma document processing project at ExxerCube.

**AI Assistance Disclosure:** This research was developed with assistance from Claude (Anthropic), a large language model. Claude contributed to algorithm implementation, experimental design iteration, data analysis, code development, and technical documentation. The author directed the research, defined the problem, validated results, and takes full responsibility for the scientific content and conclusions presented in this work. All experimental results were independently verified by the author.

---

## References

1. Smith, R. (2007). An Overview of the Tesseract OCR Engine. ICDAR.
2. Deb, K., et al. (2002). A Fast and Elitist Multiobjective Genetic Algorithm: NSGA-II. IEEE TEC.
3. Fortin, F.-A., et al. (2012). DEAP: Evolutionary Algorithms Made Easy. JMLR.
4. Pedregosa, F., et al. (2011). Scikit-learn: Machine Learning in Python. JMLR.

---

## Appendix A: Model Coefficients

The complete polynomial model coefficients are stored in `Fixtures/polynomial_model_v2.json`.

**Scaler Parameters (StandardScaler):**
```
mean  = [565.76, 29.11, 15.22, 4.40]
scale = [1225.02, 5.88, 18.28, 2.53]
```

**Polynomial Features (degree 2):**
```
[1, x₀, x₁, x₂, x₃, x₀², x₀x₁, x₀x₂, x₀x₃, x₁², x₁x₂, x₁x₃, x₂², x₂x₃, x₃²]
```

---

## Appendix B: Cluster Characteristics

| Cluster | blur_score | contrast | noise_estimate | edge_density | Optimal Contrast | Optimal Sharpness |
|---------|------------|----------|----------------|--------------|------------------|-------------------|
| 0 | 127.3 | 22.4 | 8.2 | 2.1 | 1.379 | 1.378 |
| 1 | 4413.7 | 45.5 | 67.7 | 9.5 | 0.707 | 2.569 |
| 3 | 369.9 | 28.7 | 13.0 | 3.9 | 1.486 | 1.141 |
| 4 | 1031.1 | 30.7 | 22.9 | 4.3 | 1.398 | 2.006 |
| 5 | 2055.9 | 35.7 | 36.9 | 6.2 | 1.502 | 2.020 |

---

*Document Version: 1.0*
*Last Updated: November 28, 2024*
