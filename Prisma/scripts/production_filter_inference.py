#!/usr/bin/env python3
"""
Production Filter Inference Pipeline

Given a new document image:
1. Extract image properties
2. Either:
   a) Assign to nearest cluster → lookup table (discrete)
   b) Use polynomial model → continuous interpolation
3. Apply optimal filter parameters
4. Return enhanced image ready for OCR

Usage:
    python production_filter_inference.py <image_path> [--method lookup|polynomial]

Or as a module:
    from production_filter_inference import enhance_for_ocr
    enhanced_img = enhance_for_ocr(image, method="polynomial")
"""

import json
import sys
from pathlib import Path
from typing import Dict, Tuple, Optional

import numpy as np
from PIL import Image, ImageFilter, ImageEnhance
import cv2

# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
CATALOG_FILE = BASE_PATH / "PRP1_GA_Results_v6" / "all_clusters_combined.json"
CLUSTER_FILE = BASE_PATH / "PRP1_Degraded_v6" / "cluster_assignments.json"
POLYNOMIAL_FILE = BASE_PATH / "polynomial_model_v2.json"

FEATURE_NAMES = ["blur_score", "contrast", "noise_estimate", "edge_density"]
FILTER_PARAMS = ["contrast", "brightness", "sharpness", "unsharp_radius", "unsharp_percent"]

# ============================================================================
# Image Property Extraction
# ============================================================================

def extract_image_properties(image: Image.Image) -> Dict[str, float]:
    """Extract properties used for cluster assignment."""
    img_array = np.array(image)

    # Convert to grayscale if needed
    if len(img_array.shape) == 3:
        gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
    else:
        gray = img_array

    # Blur score (Laplacian variance - higher = sharper)
    laplacian = cv2.Laplacian(gray, cv2.CV_64F)
    blur_score = laplacian.var()

    # Contrast (std dev of intensity)
    contrast = float(gray.std())

    # Noise estimate (high-frequency energy via Laplacian)
    noise_estimate = float(np.abs(laplacian).mean())

    # Edge density (Canny edge pixel ratio)
    edges = cv2.Canny(gray, 100, 200)
    edge_density = float(np.sum(edges > 0) / edges.size)

    return {
        "blur_score": blur_score,
        "contrast": contrast,
        "noise_estimate": noise_estimate,
        "edge_density": edge_density,
    }


# ============================================================================
# Cluster Assignment
# ============================================================================

def load_cluster_centroids() -> Dict:
    """Load cluster centroids from clustering results (using feature_means as centroids)."""
    with open(CLUSTER_FILE) as f:
        data = json.load(f)

    # Build centroids from cluster_summaries feature_means
    centroids = {}
    for cluster_id, summary in data["cluster_summaries"].items():
        means = summary["feature_means"]
        centroids[cluster_id] = [
            means["blur_score"],
            means["contrast"],
            means["noise_estimate"],
            means["edge_density"],
        ]
    return centroids


def assign_cluster(properties: Dict[str, float], centroids: Dict) -> Tuple[int, float]:
    """Assign image to nearest cluster based on properties."""
    # Feature order must match clustering
    feature_order = ["blur_score", "contrast", "noise_estimate", "edge_density"]
    props_vec = np.array([properties[f] for f in feature_order])

    min_dist = float('inf')
    best_cluster = 0

    for cluster_id, centroid in centroids.items():
        centroid_vec = np.array(centroid)
        dist = np.linalg.norm(props_vec - centroid_vec)
        if dist < min_dist:
            min_dist = dist
            best_cluster = int(cluster_id)

    return best_cluster, min_dist


# ============================================================================
# Filter Application
# ============================================================================

def load_filter_catalog() -> Dict:
    """Load optimal filters for each cluster."""
    with open(CATALOG_FILE) as f:
        data = json.load(f)
    return data["cluster_filters"]


def apply_bilateral_filter(img: Image.Image) -> Image.Image:
    """Apply bilateral filter for edge-preserving smoothing."""
    img_array = np.array(img)
    filtered = cv2.bilateralFilter(img_array, 9, 75, 75)
    return Image.fromarray(filtered)


def apply_filters(image: Image.Image, params: Dict) -> Image.Image:
    """Apply the filter chain based on parameters."""
    img = image.copy()
    if img.mode != 'RGB':
        img = img.convert('RGB')

    # Extract parameters
    contrast = params.get("contrast", 1.0)
    brightness = params.get("brightness", 1.0)
    sharpness = params.get("sharpness", 1.0)
    median_size = params.get("median_size", 0)
    unsharp_radius = params.get("unsharp_radius", 0)
    unsharp_percent = params.get("unsharp_percent", 0)
    unsharp_threshold = params.get("unsharp_threshold", 1)
    bilateral = params.get("bilateral", 0)

    # Apply bilateral filter (edge-preserving smoothing)
    if bilateral == 1:
        img = apply_bilateral_filter(img)

    # Apply brightness adjustment
    if brightness != 1.0:
        img = ImageEnhance.Brightness(img).enhance(brightness)

    # Apply contrast adjustment
    if contrast != 1.0:
        img = ImageEnhance.Contrast(img).enhance(contrast)

    # Apply median filter (noise reduction)
    if median_size > 1:
        img = img.filter(ImageFilter.MedianFilter(size=2*median_size+1))

    # Apply sharpness adjustment
    if sharpness != 1.0:
        img = ImageEnhance.Sharpness(img).enhance(sharpness)

    # Apply unsharp mask
    if unsharp_radius > 0.5 and unsharp_percent > 50:
        img = img.filter(ImageFilter.UnsharpMask(
            radius=unsharp_radius,
            percent=unsharp_percent,
            threshold=unsharp_threshold
        ))

    return img


# ============================================================================
# Polynomial Model Inference
# ============================================================================

def load_polynomial_models() -> Dict:
    """Load polynomial models for filter parameter prediction."""
    with open(POLYNOMIAL_FILE) as f:
        return json.load(f)


def predict_with_polynomial(properties: Dict[str, float], models: Dict) -> Dict:
    """Predict filter parameters using polynomial model."""
    from sklearn.preprocessing import PolynomialFeatures

    # Get features in correct order
    features = np.array([[properties[f] for f in FEATURE_NAMES]])

    params = {}
    for param_name in FILTER_PARAMS:
        model_data = models["individual_models"][param_name]

        # Normalize features
        mean = np.array(model_data["scaler_mean"])
        scale = np.array(model_data["scaler_scale"])
        features_scaled = (features - mean) / scale

        # Create polynomial features
        poly = PolynomialFeatures(degree=model_data["poly_degree"], include_bias=True)
        features_poly = poly.fit_transform(features_scaled)

        # Predict
        coef = np.array(model_data["coefficients"])
        intercept = model_data["intercept"]
        pred = float((np.dot(features_poly, coef) + intercept)[0])

        # Clamp to reasonable bounds
        if param_name == "contrast":
            pred = max(0.5, min(2.0, pred))
        elif param_name == "brightness":
            pred = max(0.8, min(1.3, pred))
        elif param_name == "sharpness":
            pred = max(0.5, min(3.0, pred))
        elif param_name == "unsharp_radius":
            pred = max(0.0, min(5.0, pred))
        elif param_name == "unsharp_percent":
            pred = max(0, min(250, int(pred)))

        params[param_name] = pred

    # Add defaults for non-predicted params
    params["median_size"] = 0
    params["unsharp_threshold"] = 2
    params["bilateral"] = False

    return params


# ============================================================================
# Main Pipeline
# ============================================================================

def enhance_for_ocr(image: Image.Image, method: str = "lookup", verbose: bool = False) -> Tuple[Image.Image, Dict]:
    """
    Main enhancement pipeline.

    Args:
        image: Input PIL Image
        method: "lookup" (cluster-based) or "polynomial" (continuous interpolation)
        verbose: Print debug info

    Returns:
        (enhanced_image, metadata_dict)
    """
    # Step 1: Extract image properties
    properties = extract_image_properties(image)

    if method == "polynomial":
        # Step 2a: Use polynomial model for continuous prediction
        try:
            models = load_polynomial_models()
            filter_params = predict_with_polynomial(properties, models)
            cluster_id = "polynomial"
            distance = 0.0

            if verbose:
                print(f"Image properties: {properties}")
                print(f"Method: polynomial interpolation")
                print(f"Predicted params: {filter_params}")

        except Exception as e:
            print(f"Polynomial model failed ({e}), falling back to lookup")
            method = "lookup"

    if method == "lookup":
        # Step 2b: Lookup table (cluster-based)
        centroids = load_cluster_centroids()
        cluster_id, distance = assign_cluster(properties, centroids)

        catalog = load_filter_catalog()

        # Handle cluster 2 (skipped) - use combined 2+3 filter
        cluster_key = str(cluster_id)
        if cluster_key not in catalog:
            cluster_key = "2+3"

        filter_entry = catalog.get(cluster_key, catalog.get("3"))
        filter_params = filter_entry["params"]

        if verbose:
            print(f"Image properties: {properties}")
            print(f"Assigned cluster: {cluster_id} (distance: {distance:.2f})")
            print(f"Filter params: {filter_params}")

    # Step 3: Apply filters
    enhanced = apply_filters(image, filter_params)

    metadata = {
        "method": method,
        "properties": properties,
        "cluster_id": cluster_id,
        "cluster_distance": distance,
        "filter_params": filter_params,
    }

    return enhanced, metadata


def enhance_file(image_path: str, output_path: Optional[str] = None, verbose: bool = True) -> Dict:
    """
    Enhance an image file.

    Args:
        image_path: Path to input image
        output_path: Path to save enhanced image (optional)
        verbose: Print debug info

    Returns:
        Metadata dict
    """
    image = Image.open(image_path)
    enhanced, metadata = enhance_for_ocr(image, verbose=verbose)

    if output_path:
        enhanced.save(output_path)
        print(f"Enhanced image saved to: {output_path}")

    return metadata


# ============================================================================
# CLI
# ============================================================================

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="OCR Enhancement Filter Inference")
    parser.add_argument("image_path", help="Input image path")
    parser.add_argument("output_path", nargs="?", help="Output image path (optional)")
    parser.add_argument("--method", choices=["lookup", "polynomial"], default="polynomial",
                        help="Inference method: lookup (cluster-based) or polynomial (continuous)")

    args = parser.parse_args()

    if not Path(args.image_path).exists():
        print(f"Error: Image not found: {args.image_path}")
        sys.exit(1)

    # Load and enhance
    image = Image.open(args.image_path)
    enhanced, metadata = enhance_for_ocr(image, method=args.method, verbose=True)

    if args.output_path:
        enhanced.save(args.output_path)
        print(f"\nEnhanced image saved to: {args.output_path}")

    print("\n=== Enhancement Summary ===")
    print(f"Method: {metadata['method']}")
    print(f"Cluster/Model: {metadata['cluster_id']}")
    print(f"Filter params: {metadata['filter_params']}")
