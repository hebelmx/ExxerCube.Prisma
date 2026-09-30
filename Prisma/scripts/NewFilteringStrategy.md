# OCR Enhancement Filter Strategy - Polynomial Interpolation

## Executive Summary

**Recommendation: Use polynomial interpolation for OCR filter selection**

Given an input image, extract 4 image properties and use polynomial regression to predict optimal filter parameters. This provides **49% better improvement** over discrete cluster lookup.

---

## Production Implementation

### Step 1: Extract Image Properties

```python
def extract_image_properties(image):
    """
    Extract 4 features from input image.

    Args:
        image: PIL Image or numpy array (RGB)

    Returns:
        dict with: blur_score, contrast, noise_estimate, edge_density
    """
    import cv2
    import numpy as np

    # Convert to grayscale
    if len(img_array.shape) == 3:
        gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
    else:
        gray = img_array

    # 1. Blur score (Laplacian variance - higher = sharper)
    laplacian = cv2.Laplacian(gray, cv2.CV_64F)
    blur_score = laplacian.var()

    # 2. Contrast (standard deviation of intensity)
    contrast = float(gray.std())

    # 3. Noise estimate (mean absolute Laplacian)
    noise_estimate = float(np.abs(laplacian).mean())

    # 4. Edge density (Canny edge pixel ratio)
    edges = cv2.Canny(gray, 100, 200)
    edge_density = float(np.sum(edges > 0) / edges.size)

    return {
        "blur_score": blur_score,
        "contrast": contrast,
        "noise_estimate": noise_estimate,
        "edge_density": edge_density,
    }
```

### Step 2: Predict Filter Parameters

Load polynomial model from `Fixtures/polynomial_model_v2.json` and predict:

```python
def predict_filter_params(properties, model_data):
    """
    Predict optimal filter parameters using polynomial regression.

    Model: degree-2 polynomial with StandardScaler normalization
    """
    from sklearn.preprocessing import PolynomialFeatures
    import numpy as np

    features = np.array([[
        properties["blur_score"],
        properties["contrast"],
        properties["noise_estimate"],
        properties["edge_density"]
    ]])

    params = {}
    for param_name in ["contrast", "brightness", "sharpness",
                       "unsharp_radius", "unsharp_percent"]:
        model = model_data["individual_models"][param_name]

        # Normalize
        mean = np.array(model["scaler_mean"])
        scale = np.array(model["scaler_scale"])
        features_scaled = (features - mean) / scale

        # Polynomial features
        poly = PolynomialFeatures(degree=2, include_bias=True)
        features_poly = poly.fit_transform(features_scaled)

        # Predict
        coef = np.array(model["coefficients"])
        intercept = model["intercept"]
        pred = float((np.dot(features_poly, coef) + intercept)[0])

        params[param_name] = pred

    # Add fixed defaults
    params["median_size"] = 0
    params["unsharp_threshold"] = 2
    params["bilateral"] = False

    return params
```

### Step 3: Apply Filters (PIL)

```python
from PIL import Image, ImageFilter, ImageEnhance

def apply_enhancement(image, params):
    """Apply filter chain to image."""
    img = image.copy()
    if img.mode != 'RGB':
        img = img.convert('RGB')

    # 1. Brightness
    if params["brightness"] != 1.0:
        img = ImageEnhance.Brightness(img).enhance(params["brightness"])

    # 2. Contrast
    if params["contrast"] != 1.0:
        img = ImageEnhance.Contrast(img).enhance(params["contrast"])

    # 3. Sharpness
    if params["sharpness"] != 1.0:
        img = ImageEnhance.Sharpness(img).enhance(params["sharpness"])

    # 4. Unsharp Mask
    if params["unsharp_radius"] > 0.5 and params["unsharp_percent"] > 50:
        img = img.filter(ImageFilter.UnsharpMask(
            radius=params["unsharp_radius"],
            percent=int(params["unsharp_percent"]),
            threshold=params["unsharp_threshold"]
        ))

    return img
```

---

## Parameter Bounds (for clamping)

| Parameter | Min | Max | Description |
|-----------|-----|-----|-------------|
| contrast | 0.5 | 2.0 | ImageEnhance.Contrast factor |
| brightness | 0.8 | 1.3 | ImageEnhance.Brightness factor |
| sharpness | 0.5 | 3.0 | ImageEnhance.Sharpness factor |
| unsharp_radius | 0.0 | 5.0 | UnsharpMask radius in pixels |
| unsharp_percent | 0 | 250 | UnsharpMask strength percentage |

---

## Model Performance

| Parameter | R² Score | MAE |
|-----------|----------|-----|
| contrast | 0.949 | 0.052 |
| brightness | 0.987 | 0.004 |
| sharpness | 0.947 | 0.089 |
| unsharp_radius | 0.938 | 0.197 |
| unsharp_percent | 0.897 | 16.4 |

---

## Validation Results

Tested on 32 unseen images (4 documents × 8 blur levels not in training):

| Method | Wins | Avg Edit Distance | Avg Improvement |
|--------|------|-------------------|-----------------|
| No filter | - | 755.0 | baseline |
| Lookup table | 11 | 661.9 | 93.1 (12.3%) |
| **Polynomial** | **21** | **616.4** | **138.6 (18.4%)** |

**Polynomial provides 49% more improvement than lookup table.**

---

## Files Reference

| File | Description |
|------|-------------|
| `Fixtures/polynomial_model_v2.json` | Trained polynomial coefficients |
| `Fixtures/production_filter_catalog.json` | Cluster lookup table (fallback) |
| `scripts/production_filter_inference.py` | Production inference script |
| `scripts/fit_polynomial_model_v2.py` | Model training script |
| `scripts/validate_inference_methods.py` | Validation script |

---

## Usage Example

```python
from production_filter_inference import enhance_for_ocr
from PIL import Image

# Load image
image = Image.open("scanned_document.png")

# Enhance for OCR (uses polynomial by default)
enhanced, metadata = enhance_for_ocr(image, method="polynomial")

# Run OCR on enhanced image
# ...
```

CLI:
```bash
python production_filter_inference.py input.png output.png --method polynomial
```
