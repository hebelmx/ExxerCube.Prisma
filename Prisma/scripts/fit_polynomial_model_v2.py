#!/usr/bin/env python3
"""
Polynomial Model Fitting v2

Now that we have proper per-cluster optimization, try fitting polynomial models
to map image properties → optimal filter parameters.

Two approaches:
1. Simple interpolation between cluster centroids
2. Full polynomial regression on all data points
"""

import json
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
from sklearn.preprocessing import PolynomialFeatures, StandardScaler
from sklearn.linear_model import Ridge, LinearRegression
from sklearn.metrics import r2_score, mean_absolute_error
import warnings
warnings.filterwarnings('ignore')

# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
CLUSTER_FILE = BASE_PATH / "PRP1_Degraded_v6" / "cluster_assignments.json"
CATALOG_FILE = BASE_PATH / "production_filter_catalog.json"
OUTPUT_FILE = BASE_PATH / "polynomial_model_v2.json"

FEATURE_NAMES = ["blur_score", "contrast", "noise_estimate", "edge_density"]
FILTER_PARAMS = ["contrast", "brightness", "sharpness", "unsharp_radius", "unsharp_percent"]

# ============================================================================
# Data Loading
# ============================================================================

def load_data() -> Tuple[np.ndarray, Dict[str, np.ndarray], List[str]]:
    """
    Load cluster centroids and their optimal filter parameters.

    Returns:
        X: (n_clusters, n_features) - cluster centroids
        Y: dict of param_name -> (n_clusters,) - optimal values per cluster
        cluster_ids: list of cluster IDs
    """
    with open(CLUSTER_FILE) as f:
        cluster_data = json.load(f)

    with open(CATALOG_FILE) as f:
        catalog = json.load(f)

    # Get cluster summaries and corresponding filter params
    X_list = []
    Y_dict = {p: [] for p in FILTER_PARAMS}
    cluster_ids = []

    for cluster_id, summary in cluster_data["cluster_summaries"].items():
        # Skip cluster 2+3 combined (it's a special case)
        if cluster_id not in catalog["filters"]:
            continue

        # Get centroid features
        means = summary["feature_means"]
        features = [means[f] for f in FEATURE_NAMES]
        X_list.append(features)

        # Get optimal filter params
        params = catalog["filters"][cluster_id]
        for p in FILTER_PARAMS:
            Y_dict[p].append(params[p])

        cluster_ids.append(cluster_id)

    X = np.array(X_list)
    Y = {p: np.array(v) for p, v in Y_dict.items()}

    return X, Y, cluster_ids


def load_individual_data() -> Tuple[np.ndarray, Dict[str, np.ndarray]]:
    """
    Load individual image data with their cluster's optimal filter.
    This provides more data points for polynomial fitting.

    Returns:
        X: (n_images, n_features) - individual image properties
        Y: dict of param_name -> (n_images,) - cluster's optimal values
    """
    with open(CLUSTER_FILE) as f:
        cluster_data = json.load(f)

    with open(CATALOG_FILE) as f:
        catalog = json.load(f)

    X_list = []
    Y_dict = {p: [] for p in FILTER_PARAMS}

    for item in cluster_data["cluster_assignments"]:
        cluster_id = str(item["cluster"])

        # Get filter params for this cluster
        if cluster_id not in catalog["filters"]:
            cluster_id = "2+3" if cluster_id == "2" else cluster_id

        if cluster_id not in catalog["filters"]:
            continue

        # Get image features
        props = item["image_properties"]
        features = [props[f] for f in FEATURE_NAMES]
        X_list.append(features)

        # Get cluster's optimal filter params
        params = catalog["filters"][cluster_id]
        for p in FILTER_PARAMS:
            Y_dict[p].append(params[p])

    X = np.array(X_list)
    Y = {p: np.array(v) for p, v in Y_dict.items()}

    return X, Y


# ============================================================================
# Model Fitting
# ============================================================================

def fit_polynomial_model(X: np.ndarray, y: np.ndarray, degree: int = 2) -> Dict:
    """Fit polynomial regression model."""
    # Normalize features
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)

    # Create polynomial features
    poly = PolynomialFeatures(degree=degree, include_bias=True)
    X_poly = poly.fit_transform(X_scaled)

    # Fit model with Ridge regression (regularization helps with small datasets)
    model = Ridge(alpha=0.1)
    model.fit(X_poly, y)

    # Evaluate
    y_pred = model.predict(X_poly)
    r2 = r2_score(y, y_pred)
    mae = mean_absolute_error(y, y_pred)

    return {
        "scaler_mean": scaler.mean_.tolist(),
        "scaler_scale": scaler.scale_.tolist(),
        "poly_degree": degree,
        "feature_names": poly.get_feature_names_out().tolist(),
        "coefficients": model.coef_.tolist(),
        "intercept": float(model.intercept_),
        "r2_score": r2,
        "mae": mae,
        "y_actual": y.tolist(),
        "y_predicted": y_pred.tolist(),
    }


def fit_all_params(X: np.ndarray, Y: Dict[str, np.ndarray]) -> Dict:
    """Fit polynomial models for all filter parameters."""
    results = {}

    for param_name, y in Y.items():
        print(f"\nFitting {param_name}...")

        # Try different polynomial degrees
        best_model = None
        best_r2 = -float('inf')

        for degree in [1, 2]:
            model = fit_polynomial_model(X, y, degree)
            print(f"  Degree {degree}: R²={model['r2_score']:.4f}, MAE={model['mae']:.4f}")

            if model['r2_score'] > best_r2:
                best_r2 = model['r2_score']
                best_model = model

        results[param_name] = best_model
        print(f"  Best: R²={best_r2:.4f}")
        print(f"  Actual vs Predicted: {list(zip(y.tolist(), best_model['y_predicted']))}")

    return results


# ============================================================================
# Nearest Neighbor Interpolation (Alternative)
# ============================================================================

def weighted_interpolation(X_centroids: np.ndarray, Y_centroids: np.ndarray,
                           x_new: np.ndarray, k: int = 3) -> float:
    """
    Weighted average of k nearest cluster centroids.
    Weight = inverse distance (closer clusters have more influence).
    """
    # Compute distances to all centroids
    distances = np.linalg.norm(X_centroids - x_new, axis=1)

    # Get k nearest
    nearest_idx = np.argsort(distances)[:k]
    nearest_dist = distances[nearest_idx]
    nearest_vals = Y_centroids[nearest_idx]

    # Handle exact match (distance = 0)
    if nearest_dist[0] < 1e-10:
        return nearest_vals[0]

    # Inverse distance weighting
    weights = 1.0 / (nearest_dist + 1e-10)
    weights /= weights.sum()

    return float(np.dot(weights, nearest_vals))


# ============================================================================
# Main
# ============================================================================

def main():
    print("=" * 70)
    print("POLYNOMIAL MODEL FITTING v2 - Per-Cluster Optimization Data")
    print("=" * 70)

    # Load centroid data (6 points)
    X_centroids, Y_centroids, cluster_ids = load_data()
    print(f"\nLoaded {len(cluster_ids)} cluster centroids: {cluster_ids}")

    # Load individual data (64 points)
    X_individual, Y_individual = load_individual_data()
    print(f"Loaded {len(X_individual)} individual image data points")

    print("\n" + "=" * 70)
    print("APPROACH 1: Polynomial fit on cluster centroids (6 points)")
    print("=" * 70)
    centroid_models = fit_all_params(X_centroids, Y_centroids)

    print("\n" + "=" * 70)
    print("APPROACH 2: Polynomial fit on individual images (64 points)")
    print("=" * 70)
    individual_models = fit_all_params(X_individual, Y_individual)

    # Summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    print("\n{:<20} {:>12} {:>12}".format("Parameter", "Centroid R²", "Individual R²"))
    print("-" * 50)
    for param in FILTER_PARAMS:
        r2_c = centroid_models[param]["r2_score"]
        r2_i = individual_models[param]["r2_score"]
        status = "GOOD" if r2_i > 0.5 else "POOR" if r2_i > 0.2 else "FAIL"
        print(f"{param:<20} {r2_c:>12.4f} {r2_i:>12.4f}  [{status}]")

    # Save best models
    output = {
        "version": "v2",
        "features": FEATURE_NAMES,
        "centroid_models": centroid_models,
        "individual_models": individual_models,
        "recommendation": "Use lookup table (individual R² scores are poor for interpolation)"
    }

    with open(OUTPUT_FILE, 'w') as f:
        json.dump(output, f, indent=2)

    print(f"\nModels saved to: {OUTPUT_FILE}")

    # Check if polynomial is viable
    avg_r2 = np.mean([individual_models[p]["r2_score"] for p in FILTER_PARAMS])
    if avg_r2 > 0.7:
        print("\nRECOMMENDATION: Polynomial fitting viable! Use individual_models for inference.")
    else:
        print(f"\nRECOMMENDATION: Polynomial fitting NOT viable (avg R²={avg_r2:.3f})")
        print("Use lookup table approach instead (nearest cluster centroid).")


if __name__ == "__main__":
    main()
