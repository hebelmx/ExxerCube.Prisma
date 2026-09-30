#!/usr/bin/env python3
"""
Model Selection Validation & Random Sample Testing

This script:
1. Loads the fitted polynomial model
2. Implements the decision logic (threshold + model selection)
3. Validates on random holdout samples
4. Compares R², cross-validation, and prediction intervals
5. Reports total error for each approach

Usage:
    python validate_model_selection.py
"""

import json
import numpy as np
import pandas as pd
from pathlib import Path
from typing import Tuple, Dict
from dataclasses import dataclass

from sklearn.model_selection import train_test_split, KFold
from sklearn.preprocessing import PolynomialFeatures
from sklearn.linear_model import LinearRegression
from sklearn.pipeline import make_pipeline
from sklearn.metrics import r2_score, mean_squared_error, mean_absolute_error


# =============================================================================
# DECISION LOGIC IMPLEMENTATION
# =============================================================================

@dataclass
class EnhancementDecision:
    """Result of the enhancement decision logic."""
    apply_filter: bool
    model: str  # "PIL", "OpenCV", or "None"
    predicted_improvement_pil: float
    predicted_improvement_opencv: float
    confidence: float


class EnhancementModelSelector:
    """
    Implements the decision logic:
    1. Threshold → filter or no filter
    2. Predicted improvement → which model
    3. Tie-breaker → simplicity (PIL wins ties)
    """

    def __init__(self, fitted_model_path: Path,
                 skip_threshold: float = 0.05,  # 5% minimum improvement to apply filter
                 tie_tolerance: float = 0.10):  # 10% tolerance for PIL vs OpenCV tie
        self.skip_threshold = skip_threshold
        self.tie_tolerance = tie_tolerance

        # Load fitted model
        with open(fitted_model_path) as f:
            self.model_config = json.load(f)

        self.pil_model = None
        self.opencv_model = None
        self._build_models()

    def _build_models(self):
        """Rebuild sklearn models from coefficients (for prediction)."""
        # For now, we'll use the comparison data directly
        # In production, you'd reconstruct the polynomial from coefficients
        pass

    def predict_improvement(self, blur: float, noise: float, contrast: float,
                            baseline_edits: float) -> Tuple[float, float]:
        """
        Predict improvement percentage for PIL and OpenCV.

        Returns: (pil_improvement_pct, opencv_improvement_pct)
        """
        # Simplified: use cluster-based lookup from comparison data
        # In full implementation, use polynomial prediction

        comparison = self.model_config.get("model_comparison", {})

        # Determine cluster based on image properties
        if noise < 0.7:
            cluster = "cluster_0"
        elif noise < 2.0:
            cluster = "cluster_1"
        else:
            cluster = "cluster_2"

        if cluster in comparison:
            stats = comparison[cluster]
            # Estimate improvement as reduction from baseline
            pil_improvement = (baseline_edits - stats["pil_best"]) / baseline_edits
            opencv_improvement = (baseline_edits - stats["opencv_best"]) / baseline_edits
            return max(0, pil_improvement), max(0, opencv_improvement)

        return 0.0, 0.0

    def decide(self, blur: float, noise: float, contrast: float,
               baseline_edits: float) -> EnhancementDecision:
        """
        Make enhancement decision for an image.
        """
        pil_imp, opencv_imp = self.predict_improvement(blur, noise, contrast, baseline_edits)
        max_improvement = max(pil_imp, opencv_imp)

        # Decision 1: Apply filter or not
        if max_improvement < self.skip_threshold:
            return EnhancementDecision(
                apply_filter=False,
                model="None",
                predicted_improvement_pil=pil_imp,
                predicted_improvement_opencv=opencv_imp,
                confidence=1.0 - max_improvement  # High confidence in skipping
            )

        # Decision 2: Which model
        if abs(pil_imp - opencv_imp) / max(pil_imp, opencv_imp, 0.001) < self.tie_tolerance:
            # Tie - use PIL (simpler)
            model = "PIL"
            confidence = 0.8  # Moderate confidence in tie-breaker
        elif pil_imp > opencv_imp:
            model = "PIL"
            confidence = min(1.0, pil_imp / max(opencv_imp, 0.001) - 1)
        else:
            model = "OpenCV"
            confidence = min(1.0, opencv_imp / max(pil_imp, 0.001) - 1)

        return EnhancementDecision(
            apply_filter=True,
            model=model,
            predicted_improvement_pil=pil_imp,
            predicted_improvement_opencv=opencv_imp,
            confidence=confidence
        )


# =============================================================================
# VALIDATION METHODS
# =============================================================================

def validate_with_holdout(df: pd.DataFrame, target_col: str,
                          feature_cols: list, degree: int = 2,
                          test_size: float = 0.2, n_iterations: int = 10) -> Dict:
    """
    Validate using random holdout samples.
    """
    X = df[feature_cols].values
    y = df[target_col].values

    r2_scores = []
    rmse_scores = []
    mae_scores = []

    for i in range(n_iterations):
        X_train, X_test, y_train, y_test = train_test_split(
            X, y, test_size=test_size, random_state=i
        )

        model = make_pipeline(
            PolynomialFeatures(degree=degree),
            LinearRegression()
        )
        model.fit(X_train, y_train)
        y_pred = model.predict(X_test)

        r2_scores.append(r2_score(y_test, y_pred))
        rmse_scores.append(np.sqrt(mean_squared_error(y_test, y_pred)))
        mae_scores.append(mean_absolute_error(y_test, y_pred))

    return {
        "method": "random_holdout",
        "test_size": test_size,
        "n_iterations": n_iterations,
        "r2_mean": np.mean(r2_scores),
        "r2_std": np.std(r2_scores),
        "rmse_mean": np.mean(rmse_scores),
        "rmse_std": np.std(rmse_scores),
        "mae_mean": np.mean(mae_scores),
        "mae_std": np.std(mae_scores),
    }


def validate_with_kfold(df: pd.DataFrame, target_col: str,
                        feature_cols: list, degree: int = 2, k: int = 5) -> Dict:
    """
    Validate using K-fold cross-validation.
    """
    X = df[feature_cols].values
    y = df[target_col].values

    kf = KFold(n_splits=k, shuffle=True, random_state=42)

    r2_scores = []
    rmse_scores = []
    mae_scores = []

    for train_idx, test_idx in kf.split(X):
        X_train, X_test = X[train_idx], X[test_idx]
        y_train, y_test = y[train_idx], y[test_idx]

        model = make_pipeline(
            PolynomialFeatures(degree=degree),
            LinearRegression()
        )
        model.fit(X_train, y_train)
        y_pred = model.predict(X_test)

        r2_scores.append(r2_score(y_test, y_pred))
        rmse_scores.append(np.sqrt(mean_squared_error(y_test, y_pred)))
        mae_scores.append(mean_absolute_error(y_test, y_pred))

    return {
        "method": "k_fold",
        "k": k,
        "r2_mean": np.mean(r2_scores),
        "r2_std": np.std(r2_scores),
        "rmse_mean": np.mean(rmse_scores),
        "rmse_std": np.std(rmse_scores),
        "mae_mean": np.mean(mae_scores),
        "mae_std": np.std(mae_scores),
    }


def compare_validation_methods(df: pd.DataFrame, target_col: str,
                               feature_cols: list, degrees: list = [2, 3, 4]) -> pd.DataFrame:
    """
    Compare different validation methods and polynomial degrees.
    """
    results = []

    for degree in degrees:
        # R² on full data (baseline)
        X = df[feature_cols].values
        y = df[target_col].values
        model = make_pipeline(PolynomialFeatures(degree=degree), LinearRegression())
        model.fit(X, y)
        r2_full = r2_score(y, model.predict(X))

        # Random holdout
        holdout = validate_with_holdout(df, target_col, feature_cols, degree)

        # K-fold
        kfold = validate_with_kfold(df, target_col, feature_cols, degree)

        results.append({
            "degree": degree,
            "r2_full_data": r2_full,
            "r2_holdout": holdout["r2_mean"],
            "r2_holdout_std": holdout["r2_std"],
            "r2_kfold": kfold["r2_mean"],
            "r2_kfold_std": kfold["r2_std"],
            "rmse_holdout": holdout["rmse_mean"],
            "rmse_kfold": kfold["rmse_mean"],
            "mae_holdout": holdout["mae_mean"],
            "mae_kfold": kfold["mae_mean"],
        })

    return pd.DataFrame(results)


# =============================================================================
# MAIN VALIDATION
# =============================================================================

def main():
    base_path = Path(__file__).parent.parent / "Fixtures"

    print("="*70)
    print("MODEL SELECTION VALIDATION")
    print("="*70)
    print()

    # Load solution data
    pil_csv = base_path / "pil_solutions_data.csv"
    opencv_csv = base_path / "opencv_solutions_data.csv"

    if not pil_csv.exists() or not opencv_csv.exists():
        print("ERROR: Solution data CSVs not found.")
        print("Run analyze_pareto_fronts.py first.")
        return

    pil_df = pd.read_csv(pil_csv)
    opencv_df = pd.read_csv(opencv_csv)

    print(f"Loaded PIL data: {len(pil_df)} solutions")
    print(f"Loaded OpenCV data: {len(opencv_df)} solutions")
    print()

    # ==========================================================================
    # PIL VALIDATION
    # ==========================================================================
    print("="*70)
    print("PIL MODEL VALIDATION")
    print("="*70)

    if len(pil_df) >= 10:
        feature_cols = ['blur', 'noise', 'contrast']

        print("\n--- contrast_factor ---")
        comparison = compare_validation_methods(pil_df, 'contrast_factor', feature_cols, [1, 2, 3])
        print(comparison.to_string(index=False))

        print("\n--- median_size ---")
        comparison = compare_validation_methods(pil_df, 'median_size', feature_cols, [1, 2, 3])
        print(comparison.to_string(index=False))
    else:
        print("Not enough PIL data for validation")

    # ==========================================================================
    # OPENCV VALIDATION
    # ==========================================================================
    print()
    print("="*70)
    print("OPENCV MODEL VALIDATION")
    print("="*70)

    if len(opencv_df) >= 10:
        feature_cols = ['blur', 'noise', 'contrast']

        for param in ['denoise_h', 'clahe_clip', 'bilateral_d', 'unsharp_amount']:
            print(f"\n--- {param} ---")
            comparison = compare_validation_methods(opencv_df, param, feature_cols, [2, 3, 4])
            print(comparison.to_string(index=False))
    else:
        print("Not enough OpenCV data for validation")

    # ==========================================================================
    # MODEL SELECTION TEST
    # ==========================================================================
    print()
    print("="*70)
    print("MODEL SELECTION DECISION TEST")
    print("="*70)

    fitted_model_path = base_path / "fitted_enhancement_model.json"
    if fitted_model_path.exists():
        selector = EnhancementModelSelector(fitted_model_path)

        # Test cases
        test_cases = [
            {"blur": 7000, "noise": 0.5, "contrast": 50, "baseline": 100, "desc": "Ultra-sharp (cluster 0)"},
            {"blur": 1500, "noise": 1.5, "contrast": 35, "baseline": 500, "desc": "Normal (cluster 1)"},
            {"blur": 1000, "noise": 8.0, "contrast": 20, "baseline": 2000, "desc": "Degraded (cluster 2)"},
            {"blur": 6000, "noise": 0.3, "contrast": 55, "baseline": 20, "desc": "Near-pristine (skip?)"},
        ]

        print()
        for tc in test_cases:
            decision = selector.decide(tc["blur"], tc["noise"], tc["contrast"], tc["baseline"])
            print(f"{tc['desc']}:")
            print(f"  Apply filter: {decision.apply_filter}")
            print(f"  Model: {decision.model}")
            print(f"  PIL improvement: {decision.predicted_improvement_pil:.1%}")
            print(f"  OpenCV improvement: {decision.predicted_improvement_opencv:.1%}")
            print(f"  Confidence: {decision.confidence:.2f}")
            print()
    else:
        print("Fitted model not found. Run analyze_pareto_fronts.py first.")

    print("="*70)
    print("VALIDATION COMPLETE")
    print("="*70)


if __name__ == "__main__":
    main()
