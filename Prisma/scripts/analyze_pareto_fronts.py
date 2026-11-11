#!/usr/bin/env python3
"""
Pareto Front Analysis & Polynomial Model Fitting

This script:
1. Loads all Pareto front results from GA optimization
2. Extracts anchor points (filter params + fitness)
3. Fits polynomial models: image_properties → optimal_filter_params
4. Validates with R², cross-validation, prediction intervals
5. Outputs fitted coefficients for production use

Usage:
    python analyze_pareto_fronts.py
"""

import json
import numpy as np
import pandas as pd
from pathlib import Path
from typing import Dict, List, Tuple
from dataclasses import dataclass

# scipy for curve fitting
from scipy.optimize import curve_fit
from scipy import stats

# sklearn for validation
from sklearn.model_selection import cross_val_score, LeaveOneOut
from sklearn.linear_model import LinearRegression
from sklearn.preprocessing import PolynomialFeatures
from sklearn.pipeline import make_pipeline
from sklearn.metrics import r2_score, mean_squared_error


# =============================================================================
# DATA STRUCTURES
# =============================================================================

@dataclass
class ClusterInfo:
    """Image cluster characteristics."""
    cluster_id: int
    description: str
    levels: List[str]
    blur: float
    noise: float
    contrast: float


CLUSTERS = {
    0: ClusterInfo(0, "Ultra-sharp", ["PRP1_Pristine", "Q1_Poor"], 6905.9, 0.47, 51.9),
    1: ClusterInfo(1, "Normal quality", ["Q1_Poor", "Q2_MediumPoor"], 1436.7, 1.01, 32.8),
    2: ClusterInfo(2, "Degraded", ["Q2_MediumPoor", "Q3_Low", "Q4_VeryLow"], 1238.2, 6.83, 25.1),
}

# Quality level characteristics (approximate - can be refined with actual measurements)
QUALITY_LEVELS = {
    "PRP1_Pristine": {"blur": 7000, "noise": 0.3, "contrast": 55},
    "Q1_Poor": {"blur": 5000, "noise": 1.0, "contrast": 45},
    "Q2_MediumPoor": {"blur": 2000, "noise": 3.0, "contrast": 35},
    "Q3_Low": {"blur": 1200, "noise": 7.0, "contrast": 25},
    "Q4_VeryLow": {"blur": 800, "noise": 12.0, "contrast": 18},
}


# =============================================================================
# DATA LOADING
# =============================================================================

def load_pareto_fronts(base_path: Path) -> Dict[str, List[dict]]:
    """Load all Pareto front JSON files."""
    fronts = {}

    for pipeline in ["pil", "opencv"]:
        for cluster in [0, 1, 2]:
            filename = f"cluster{cluster}_{pipeline}_pareto_front.json"
            filepath = base_path / filename

            if filepath.exists():
                with open(filepath) as f:
                    data = json.load(f)
                    fronts[f"cluster{cluster}_{pipeline}"] = data
                    print(f"Loaded {filename}: {len(data)} solutions")
            else:
                print(f"WARNING: {filename} not found")

    return fronts


def load_baseline(base_path: Path) -> dict:
    """Load baseline (no enhancement) results."""
    filepath = base_path / "ocr_baseline_no_enhancement.json"
    if filepath.exists():
        with open(filepath) as f:
            return json.load(f)
    return {}


# =============================================================================
# DATA PREPARATION FOR FITTING
# =============================================================================

def prepare_pil_data(fronts: Dict, baseline: dict) -> pd.DataFrame:
    """
    Prepare PIL data for polynomial fitting.

    Output columns:
    - blur, noise, contrast (image properties)
    - contrast_factor, median_size (filter params)
    - total_edits, improvement_pct (fitness)
    """
    rows = []

    for cluster_id in [0, 1, 2]:
        key = f"cluster{cluster_id}_pil"
        if key not in fronts:
            continue

        cluster = CLUSTERS[cluster_id]

        for solution in fronts[key]:
            genome = solution["genome"]
            objectives = solution["objectives"]
            total_edits = solution["total_edits"]

            rows.append({
                "cluster": cluster_id,
                "blur": cluster.blur,
                "noise": cluster.noise,
                "contrast": cluster.contrast,
                "contrast_factor": genome["contrast_factor"],
                "median_size": genome["median_size"],
                "total_edits": total_edits,
                "n_objectives": len(objectives),
            })

    df = pd.DataFrame(rows)
    return df


def prepare_opencv_data(fronts: Dict, baseline: dict) -> pd.DataFrame:
    """
    Prepare OpenCV data for polynomial fitting.

    Output columns:
    - blur, noise, contrast (image properties)
    - denoise_h, clahe_clip, bilateral_d, etc. (filter params)
    - total_edits, improvement_pct (fitness)
    """
    rows = []

    for cluster_id in [0, 1, 2]:
        key = f"cluster{cluster_id}_opencv"
        if key not in fronts:
            continue

        cluster = CLUSTERS[cluster_id]

        for solution in fronts[key]:
            genome = solution["genome"]
            objectives = solution["objectives"]
            total_edits = solution["total_edits"]

            rows.append({
                "cluster": cluster_id,
                "blur": cluster.blur,
                "noise": cluster.noise,
                "contrast": cluster.contrast,
                "denoise_h": genome["denoise_h"],
                "clahe_clip": genome["clahe_clip"],
                "bilateral_d": genome["bilateral_d"],
                "bilateral_sigma_color": genome["bilateral_sigma_color"],
                "bilateral_sigma_space": genome["bilateral_sigma_space"],
                "unsharp_amount": genome["unsharp_amount"],
                "unsharp_radius": genome["unsharp_radius"],
                "total_edits": total_edits,
                "n_objectives": len(objectives),
            })

    df = pd.DataFrame(rows)
    return df


# =============================================================================
# POLYNOMIAL FITTING
# =============================================================================

def fit_polynomial_model(X: np.ndarray, y: np.ndarray, degree: int = 2) -> Tuple:
    """
    Fit polynomial regression model.

    Returns: (model, r2, rmse, coefficients)
    """
    model = make_pipeline(
        PolynomialFeatures(degree=degree, include_bias=True),
        LinearRegression()
    )

    model.fit(X, y)
    y_pred = model.predict(X)

    r2 = r2_score(y, y_pred)
    rmse = np.sqrt(mean_squared_error(y, y_pred))

    # Get coefficients
    poly_features = model.named_steps['polynomialfeatures']
    lin_reg = model.named_steps['linearregression']
    feature_names = poly_features.get_feature_names_out()
    coefficients = dict(zip(feature_names, lin_reg.coef_))
    coefficients['intercept'] = lin_reg.intercept_

    return model, r2, rmse, coefficients


def cross_validate_model(X: np.ndarray, y: np.ndarray, degree: int = 2, cv: int = 5) -> dict:
    """
    Cross-validate polynomial model.

    Returns: dict with cv scores
    """
    model = make_pipeline(
        PolynomialFeatures(degree=degree, include_bias=True),
        LinearRegression()
    )

    # R² scores
    r2_scores = cross_val_score(model, X, y, cv=cv, scoring='r2')

    # RMSE scores (negative MSE, so negate and sqrt)
    mse_scores = cross_val_score(model, X, y, cv=cv, scoring='neg_mean_squared_error')
    rmse_scores = np.sqrt(-mse_scores)

    return {
        "r2_mean": r2_scores.mean(),
        "r2_std": r2_scores.std(),
        "rmse_mean": rmse_scores.mean(),
        "rmse_std": rmse_scores.std(),
        "r2_scores": r2_scores.tolist(),
        "rmse_scores": rmse_scores.tolist(),
    }


def prediction_intervals(model, X: np.ndarray, y: np.ndarray, X_new: np.ndarray,
                         confidence: float = 0.95) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    """
    Calculate prediction intervals for new data.

    Returns: (y_pred, lower_bound, upper_bound)
    """
    y_pred = model.predict(X)
    residuals = y - y_pred

    n = len(y)
    p = X.shape[1] if len(X.shape) > 1 else 1

    # Standard error of residuals
    se = np.sqrt(np.sum(residuals**2) / (n - p - 1))

    # t-value for confidence interval
    t_val = stats.t.ppf((1 + confidence) / 2, n - p - 1)

    # Predictions for new data
    y_new = model.predict(X_new)

    # Prediction interval (simplified - assumes similar leverage)
    margin = t_val * se * np.sqrt(1 + 1/n)

    return y_new, y_new - margin, y_new + margin


# =============================================================================
# MODEL COMPARISON
# =============================================================================

def compare_models(pil_df: pd.DataFrame, opencv_df: pd.DataFrame) -> dict:
    """
    Compare PIL vs OpenCV models for model selection logic.
    """
    results = {}

    # Per-cluster comparison
    for cluster_id in [0, 1, 2]:
        pil_cluster = pil_df[pil_df['cluster'] == cluster_id] if 'cluster' in pil_df.columns else pd.DataFrame()
        opencv_cluster = opencv_df[opencv_df['cluster'] == cluster_id] if len(opencv_df) > 0 and 'cluster' in opencv_df.columns else pd.DataFrame()

        if len(pil_cluster) > 0:
            pil_best = pil_cluster['total_edits'].min()
            pil_mean = pil_cluster['total_edits'].mean()

            if len(opencv_cluster) > 0:
                opencv_best = opencv_cluster['total_edits'].min()
                opencv_mean = opencv_cluster['total_edits'].mean()
                results[f"cluster_{cluster_id}"] = {
                    "pil_best": pil_best,
                    "opencv_best": opencv_best,
                    "pil_mean": pil_mean,
                    "opencv_mean": opencv_mean,
                    "best_model": "PIL" if pil_best <= opencv_best else "OpenCV",
                    "improvement_pct": abs(pil_best - opencv_best) / max(pil_best, opencv_best) * 100
                }
            else:
                # Only PIL data available
                results[f"cluster_{cluster_id}"] = {
                    "pil_best": pil_best,
                    "opencv_best": None,
                    "pil_mean": pil_mean,
                    "opencv_mean": None,
                    "best_model": "PIL (only data available)",
                    "improvement_pct": 0
                }

    return results


# =============================================================================
# GOLDEN TEST VALIDATION
# =============================================================================

def validate_golden_test(pil_df: pd.DataFrame, pil_models: dict, base_path: Path):
    """
    Validate model predictions against the golden test document.

    Golden test: 555CCC Q2_MediumPoor
    - Known optimal: contrast_factor=1.0497, median_size=3
    - Known result: 89.7% improvement (1518 → 157 edit distance)

    IMPORTANT FINDING:
    - Polynomial fitting for contrast_factor has very low R² (~0.04)
    - This is because Pareto fronts contain diverse trade-offs, not single optima
    - Better approach: LOOKUP from best Pareto solution per cluster
    """
    print()
    print("="*70)
    print("GOLDEN TEST VALIDATION: 555CCC Q2_MediumPoor")
    print("="*70)

    # Load golden test config
    golden_config_path = base_path / "golden_test_q2_mediumpoor_555ccc.json"
    if not golden_config_path.exists():
        print("WARNING: Golden test config not found")
        return None

    with open(golden_config_path) as f:
        golden = json.load(f)

    # Known optimal values from GA
    known_contrast = golden["optimal_filter"]["parameters"]["contrast_factor"]
    known_median = golden["optimal_filter"]["parameters"]["median_size"]
    known_improvement = golden["results"]["improvement_percentage"]

    print(f"\nKnown optimal (from GA single-doc optimization):")
    print(f"  contrast_factor: {known_contrast:.4f}")
    print(f"  median_size:     {known_median}")
    print(f"  improvement:     {known_improvement:.1f}%")

    # Q2_MediumPoor belongs to cluster 1 (Q1_Poor + Q2_MediumPoor)
    q2_properties = QUALITY_LEVELS["Q2_MediumPoor"]

    print(f"\nImage properties (Q2_MediumPoor):")
    print(f"  blur:     {q2_properties['blur']}")
    print(f"  noise:    {q2_properties['noise']}")
    print(f"  contrast: {q2_properties['contrast']}")

    # Test polynomial model (expected to fail for contrast)
    if pil_models:
        model_contrast = pil_models.get("contrast_model")
        model_median = pil_models.get("median_model")

        if model_contrast and model_median:
            X_test = np.array([[q2_properties["blur"], q2_properties["noise"], q2_properties["contrast"]]])
            pred_contrast_poly = model_contrast.predict(X_test)[0]
            pred_median_poly = model_median.predict(X_test)[0]

            print(f"\n--- Method 1: Polynomial Model (low R²) ---")
            print(f"  Predicted contrast_factor: {pred_contrast_poly:.4f}")
            print(f"  Predicted median_size:     {pred_median_poly:.1f}")
            poly_error = abs(pred_contrast_poly - known_contrast) / known_contrast * 100
            print(f"  Contrast error: {poly_error:.1f}% (EXPECTED TO BE HIGH)")

    # Better approach: Lookup from best Pareto solution
    print(f"\n--- Method 2: Best Pareto Solution Lookup (RECOMMENDED) ---")
    cluster1_pil = pil_df[pil_df['cluster'] == 1] if 'cluster' in pil_df.columns else pd.DataFrame()

    if len(cluster1_pil) > 0:
        # Find solution with lowest total_edits
        best_idx = cluster1_pil['total_edits'].idxmin()
        best_solution = cluster1_pil.loc[best_idx]

        pred_contrast_lookup = best_solution['contrast_factor']
        pred_median_lookup = int(best_solution['median_size'])

        print(f"  Best cluster 1 PIL solution:")
        print(f"  contrast_factor: {pred_contrast_lookup:.4f}")
        print(f"  median_size:     {pred_median_lookup}")
        print(f"  total_edits:     {best_solution['total_edits']:.0f}")

        lookup_error = abs(pred_contrast_lookup - known_contrast) / known_contrast * 100
        print(f"  Contrast error: {lookup_error:.1f}%")

        # Verify this IS the golden test solution (or very close)
        if abs(pred_contrast_lookup - known_contrast) < 0.01:
            print(f"\n  >>> MATCH! Best Pareto solution matches golden test optimal <<<")
            validation_passed = True
        else:
            print(f"\n  Note: Different optimal - cluster optimization vs single-doc optimization")
            validation_passed = lookup_error < 15

        return {
            "method": "pareto_lookup",
            "known_contrast": known_contrast,
            "known_median": known_median,
            "predicted_contrast": pred_contrast_lookup,
            "predicted_median": pred_median_lookup,
            "contrast_error_pct": lookup_error,
            "passed": validation_passed,
            "recommendation": "Use Pareto lookup instead of polynomial for contrast_factor"
        }

    return None


# =============================================================================
# EXPORT FITTED MODEL
# =============================================================================

def export_fitted_model(pil_results: dict, opencv_results: dict,
                        comparison: dict, output_path: Path):
    """
    Export fitted model coefficients for production use.
    """
    model_config = {
        "metadata": {
            "description": "Fitted polynomial models for OCR image enhancement",
            "pil_degree": 2,
            "opencv_degree": 3,
        },
        "pil_model": pil_results,
        "opencv_model": opencv_results,
        "model_comparison": comparison,
        "decision_logic": {
            "threshold_skip_filter": "If max predicted improvement < 5%, skip filtering",
            "model_selection": "Use PIL if within 10% of OpenCV improvement, else use best",
        }
    }

    with open(output_path, 'w') as f:
        json.dump(model_config, f, indent=2, default=str)

    print(f"\nExported fitted model to: {output_path}")


# =============================================================================
# MAIN ANALYSIS
# =============================================================================

def main():
    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*70)
    print("PARETO FRONT ANALYSIS & POLYNOMIAL MODEL FITTING")
    print("="*70)
    print()

    # Load data
    print("Loading Pareto fronts...")
    fronts = load_pareto_fronts(base_path)
    baseline = load_baseline(base_path)
    print()

    if not fronts:
        print("ERROR: No Pareto front files found. Wait for GA to complete.")
        return

    # Prepare data
    print("Preparing data for fitting...")
    pil_df = prepare_pil_data(fronts, baseline)
    opencv_df = prepare_opencv_data(fronts, baseline)
    print(f"  PIL data: {len(pil_df)} solutions")
    print(f"  OpenCV data: {len(opencv_df)} solutions")
    print()

    # ==========================================================================
    # PIL MODEL FITTING
    # ==========================================================================
    print("="*70)
    print("PIL MODEL (2nd degree polynomial)")
    print("="*70)

    pil_models = {}  # Store models for golden test validation

    if len(pil_df) > 0:
        # Features: blur, noise, contrast
        X_pil = pil_df[['blur', 'noise', 'contrast']].values

        # Fit contrast_factor
        print("\n--- Fitting: contrast_factor ---")
        y_contrast = pil_df['contrast_factor'].values
        model_contrast, r2_c, rmse_c, coef_c = fit_polynomial_model(X_pil, y_contrast, degree=2)
        cv_contrast = cross_validate_model(X_pil, y_contrast, degree=2, cv=min(5, len(pil_df)))
        print(f"  R² (train): {r2_c:.4f}")
        print(f"  R² (CV):    {cv_contrast['r2_mean']:.4f} ± {cv_contrast['r2_std']:.4f}")
        print(f"  RMSE:       {rmse_c:.4f}")
        pil_models["contrast_model"] = model_contrast

        # Fit median_size
        print("\n--- Fitting: median_size ---")
        y_median = pil_df['median_size'].values
        model_median, r2_m, rmse_m, coef_m = fit_polynomial_model(X_pil, y_median, degree=2)
        cv_median = cross_validate_model(X_pil, y_median, degree=2, cv=min(5, len(pil_df)))
        print(f"  R² (train): {r2_m:.4f}")
        print(f"  R² (CV):    {cv_median['r2_mean']:.4f} ± {cv_median['r2_std']:.4f}")
        print(f"  RMSE:       {rmse_m:.4f}")
        pil_models["median_model"] = model_median

        pil_results = {
            "contrast_factor": {
                "coefficients": coef_c,
                "r2_train": r2_c,
                "r2_cv": cv_contrast['r2_mean'],
                "rmse": rmse_c,
            },
            "median_size": {
                "coefficients": coef_m,
                "r2_train": r2_m,
                "r2_cv": cv_median['r2_mean'],
                "rmse": rmse_m,
            }
        }
    else:
        pil_results = {}
        print("No PIL data available")

    # ==========================================================================
    # OPENCV MODEL FITTING
    # ==========================================================================
    print()
    print("="*70)
    print("OPENCV MODEL (3rd degree polynomial)")
    print("="*70)

    opencv_results = {}
    if len(opencv_df) > 0:
        X_opencv = opencv_df[['blur', 'noise', 'contrast']].values

        opencv_params = ['denoise_h', 'clahe_clip', 'bilateral_d',
                         'bilateral_sigma_color', 'bilateral_sigma_space',
                         'unsharp_amount', 'unsharp_radius']

        for param in opencv_params:
            print(f"\n--- Fitting: {param} ---")
            y = opencv_df[param].values
            model, r2, rmse, coef = fit_polynomial_model(X_opencv, y, degree=3)
            cv = cross_validate_model(X_opencv, y, degree=3, cv=min(5, len(opencv_df)))
            print(f"  R² (train): {r2:.4f}")
            print(f"  R² (CV):    {cv['r2_mean']:.4f} ± {cv['r2_std']:.4f}")
            print(f"  RMSE:       {rmse:.4f}")

            opencv_results[param] = {
                "coefficients": coef,
                "r2_train": r2,
                "r2_cv": cv['r2_mean'],
                "rmse": rmse,
            }
    else:
        print("No OpenCV data available")

    # ==========================================================================
    # MODEL COMPARISON
    # ==========================================================================
    print()
    print("="*70)
    print("MODEL COMPARISON (PIL vs OpenCV)")
    print("="*70)

    comparison = compare_models(pil_df, opencv_df)
    for cluster, stats in comparison.items():
        print(f"\n{cluster}:")
        print(f"  PIL best:    {stats['pil_best']:.0f} edits")
        if stats['opencv_best'] is not None:
            print(f"  OpenCV best: {stats['opencv_best']:.0f} edits")
        else:
            print(f"  OpenCV best: N/A (not yet available)")
        print(f"  Winner:      {stats['best_model']}")

    # ==========================================================================
    # GOLDEN TEST VALIDATION
    # ==========================================================================
    golden_validation = validate_golden_test(pil_df, pil_models, base_path)

    # ==========================================================================
    # EXPORT
    # ==========================================================================
    print()
    output_path = base_path / "fitted_enhancement_model.json"
    export_fitted_model(pil_results, opencv_results, comparison, output_path)

    # Also save raw data for further analysis
    pil_df.to_csv(base_path / "pil_solutions_data.csv", index=False)
    opencv_df.to_csv(base_path / "opencv_solutions_data.csv", index=False)
    print(f"Saved solution data to CSV files")

    print()
    print("="*70)
    print("ANALYSIS COMPLETE")
    print("="*70)


if __name__ == "__main__":
    main()
