# Polynomial Filter Selection Strategy

## Overview

The `PolynomialFilterSelectionStrategy` is an advanced filter parameter prediction system that uses polynomial regression models trained on NSGA-II optimization results to continuously predict optimal filter parameters from image quality metrics.

## Architecture

```
Image Quality Metrics → Feature Normalization → Polynomial Models → Filter Parameters
     ↓                         ↓                       ↓                    ↓
[blur, noise,          [0,1]^4 normalized      9 polynomial          [config with
 contrast,              feature vector          models predict        optimized
 sharpness]                                     parameters]           parameters]
```

### Components

1. **FeatureNormalizer** - Normalizes raw metrics to [0, 1] range
2. **PolynomialModel** - Polynomial regression for parameter prediction
3. **PolynomialFilterSelectionStrategy** - Main strategy implementing IFilterSelectionStrategy

## Current Status: STUB IMPLEMENTATION

⚠️ **This is currently a stub implementation!**

The strategy is implemented with:
- ✅ Complete polynomial model infrastructure
- ✅ Feature normalization pipeline
- ✅ Filter type selection logic
- ✅ Parameter prediction framework
- ❌ **NOT YET TRAINED** - Uses default parameter values

**When filtering study results arrive, you will need to:**
1. Train polynomial regression models from your data
2. Export coefficients to code or JSON
3. Load coefficients into the polynomial models
4. Validate against test set

## How to Use (After Training)

### 1. Enable Polynomial Strategy

```csharp
// In your DI configuration
services.AddImagingInfrastructure(FilterSelectionStrategyType.Polynomial);
```

### 2. Current Behavior (Stub Mode)

Without trained models, the strategy:
- Uses simple heuristics for filter type selection
- Returns midpoint values for parameter ranges
- Falls back to Q2 optimized config on errors

**Stub Output Example:**
```
- PIL ContrastFactor: 1.75 (midpoint of 0.5-3.0 range)
- PIL MedianSize: 4 (midpoint of 1-7 range)
- OpenCV parameters: midpoint of valid ranges
```

## Training Workflow (When Results Arrive)

### Step 1: Prepare Training Data

Extract from your filtering study results:

```python
import pandas as pd
import json

# Load your filtering study results
results = pd.read_json('Fixtures/filtering_study_results.json')

# Extract features and targets
features = results[['blur_score', 'noise_level', 'contrast_level', 'sharpness_level']]
targets = {
    'pil_contrast': results['optimal_pil_contrast'],
    'pil_median': results['optimal_pil_median'],
    'opencv_denoise': results['optimal_opencv_denoise_h'],
    # ... etc for all 9 parameters
}
```

### Step 2: Train Polynomial Models

```python
from sklearn.preprocessing import PolynomialFeatures
from sklearn.linear_model import Ridge
from sklearn.model_selection import cross_val_score
import numpy as np

def train_polynomial_model(X, y, degree=2, alpha=1.0):
    """
    Train polynomial regression model.

    Args:
        X: Feature matrix (N x 4) - [blur, noise, contrast, sharpness]
        y: Target values (N,)
        degree: Polynomial degree (1=linear, 2=quadratic)
        alpha: Ridge regularization parameter

    Returns:
        coefficients, cv_score
    """
    # Generate polynomial features
    poly = PolynomialFeatures(degree=degree, include_bias=True)
    X_poly = poly.fit_transform(X)

    # Fit Ridge regression (helps prevent overfitting)
    model = Ridge(alpha=alpha)
    model.fit(X_poly, y)

    # Cross-validate
    cv_scores = cross_val_score(model, X_poly, y, cv=5,
                                 scoring='neg_mean_squared_error')
    cv_rmse = np.sqrt(-cv_scores.mean())

    return model.coef_, cv_rmse, poly.get_feature_names_out()

# Train each parameter model
models = {}
for param_name, target in targets.items():
    coefs, rmse, feature_names = train_polynomial_model(features, target)
    models[param_name] = {
        'coefficients': coefs.tolist(),
        'cv_rmse': rmse,
        'features': feature_names.tolist()
    }
    print(f"{param_name}: RMSE = {rmse:.4f}")

# Export to JSON
with open('polynomial_coefficients.json', 'w') as f:
    json.dump(models, f, indent=2)
```

### Step 3: Update C# Code with Trained Coefficients

#### Option A: Hardcode Coefficients (Simple)

```csharp
// In PolynomialFilterSelectionStrategy constructor
_pilContrastModel = PolynomialModel.CreateQuadratic(
    parameterName: "PIL_ContrastFactor",
    intercept: 1.2,
    linearCoefficients: new[] { -0.5, 0.3, 0.8, 0.1 },
    quadraticCoefficients: new[] { 0.2, -0.1, 0.05, 0.0 },
    interactionCoefficients: new[] { -0.15, 0.1, 0.0, 0.2, 0.0, 0.1 },
    min: 0.5,
    max: 3.0
);
```

#### Option B: Load from JSON File (Flexible)

```csharp
// Add method to PolynomialFilterSelectionStrategy
public void LoadCoefficientsFromJson(string filePath)
{
    var json = File.ReadAllText(filePath);
    var coeffs = JsonSerializer.Deserialize<Dictionary<string, ModelCoefficients>>(json);

    // Update models with loaded coefficients
    _pilContrastModel = CreateModelFromCoefficients(coeffs["pil_contrast"]);
    // ... etc
}
```

### Step 4: Validate Against Test Set

```csharp
// Create validation test
public class PolynomialStrategyValidationTests
{
    [Fact]
    public void Strategy_ShouldPredict_OptimalParameters()
    {
        // Given: Test image with known optimal parameters
        var assessment = new ImageQualityAssessment
        {
            BlurScore = 1500f,
            NoiseLevel = 0.25f,
            ContrastLevel = 35f,
            SharpnessLevel = 0.6f
        };

        // When: Strategy predicts parameters
        var config = _strategy.SelectFilter(assessment);

        // Then: Should be close to optimal
        config.PilParams.ContrastFactor.ShouldBe(1.157f, tolerance: 0.2f);
        config.PilParams.MedianSize.ShouldBe(3);
    }
}
```

## Expected Improvements

Based on NSGA-II results, polynomial strategy should achieve:

- **Q2 Documents**: 78% improvement (6590 → 1444 edits)
- **Q1 Documents**: 25% improvement (538 → 404 edits)
- **Continuous tuning**: Better than threshold-based for edge cases

## Model Coefficients Format

### Degree 2 (Quadratic) Model

For 4 features [blur, noise, contrast, sharpness], degree=2 generates:

```
Total coefficients: 1 + 4 + 4 + 6 = 15

Order:
1. Intercept: β0
2. Linear: β1*blur, β2*noise, β3*contrast, β4*sharpness
3. Quadratic: β5*blur², β6*noise², β7*contrast², β8*sharpness²
4. Interactions: β9*blur*noise, β10*blur*contrast, β11*blur*sharpness,
                 β12*noise*contrast, β13*noise*sharpness, β14*contrast*sharpness
```

**Prediction formula:**
```
parameter = β0 +
            β1*blur + β2*noise + β3*contrast + β4*sharpness +
            β5*blur² + β6*noise² + β7*contrast² + β8*sharpness² +
            β9*blur*noise + β10*blur*contrast + β11*blur*sharpness +
            β12*noise*contrast + β13*noise*sharpness + β14*contrast*sharpness
```

## Performance Metrics

Track these metrics to validate polynomial strategy:

1. **Parameter Prediction Accuracy**
   - Mean Squared Error (MSE) per parameter
   - Cross-validation R² score

2. **OCR Quality Improvement**
   - Levenshtein distance reduction vs baseline
   - Percentage of documents with <5% error

3. **Inference Time**
   - Feature normalization: <1ms
   - Polynomial prediction (9 models): <5ms total
   - Total strategy overhead: <10ms

## TODO Checklist

- [ ] Collect filtering study results
- [ ] Extract (metrics, parameters, performance) tuples
- [ ] Normalize features and compute statistics
- [ ] Train polynomial models in Python
- [ ] Export coefficients to JSON
- [ ] Update PolynomialModel initialization with trained coefficients
- [ ] Run validation tests against test set
- [ ] Measure OCR improvement on validation set
- [ ] Enable polynomial strategy in production if improvement > 5%
- [ ] Monitor and retrain periodically as new data arrives

## References

- Current analytical strategy: `AnalyticalFilterSelectionStrategy.cs`
- NSGA-II results: `Fixtures/comprehensive_with_baseline_matrix.json`
- Pareto front: `Fixtures/nsga2_pareto_front.json`
- Feature ranges: `EmguCvImageQualityAnalyzer.cs` (lines 21-28)

## Questions?

Contact the team when ready to train models. The infrastructure is ready - we just need your filtering study results to generate the polynomial coefficients!
