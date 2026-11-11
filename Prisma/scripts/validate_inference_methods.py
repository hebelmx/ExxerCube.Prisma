#!/usr/bin/env python3
"""
Validate Polynomial vs Lookup Table Inference

Test hypothesis: Polynomial interpolation should work for unseen images
that fall "in between" the training clusters.

Steps:
1. Generate new degraded images with intermediate blur values (not in v6 training set)
2. Apply both lookup and polynomial methods
3. Run Tesseract OCR on enhanced images
4. Compare edit distances to ground truth
"""

import subprocess
import json
import tempfile
from pathlib import Path
from typing import Dict, Tuple, List

import numpy as np
from PIL import Image, ImageFilter
import cv2
import Levenshtein

# Import our inference pipeline
from production_filter_inference import enhance_for_ocr, extract_image_properties

# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
PRISTINE_DIR = BASE_PATH / "PRP1"
OUTPUT_DIR = BASE_PATH / "validation_test"

PRISTINE_DOCS = {
    "222AAA": "222AAA-44444444442025_page-1.png",
    "333BBB": "333BBB-44444444442025_page1.png",
    "333ccc": "333ccc-6666666662025_page1.png",
    "555CCC": "555CCC-66666662025_page-0001.png",
}

# Intermediate blur values NOT used in v6 training
# v6 used specific step values per document, these are in between
VALIDATION_BLURS = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0]

# ============================================================================
# Degradation
# ============================================================================

def apply_gaussian_blur(image: Image.Image, sigma: float) -> Image.Image:
    """Apply Gaussian blur to image."""
    if sigma <= 0:
        return image
    return image.filter(ImageFilter.GaussianBlur(radius=sigma))


def apply_light_noise(image: Image.Image, intensity: float = 0.01) -> Image.Image:
    """Add light Gaussian noise."""
    img_array = np.array(image, dtype=np.float32)
    noise = np.random.normal(0, intensity * 255, img_array.shape)
    noisy = np.clip(img_array + noise, 0, 255).astype(np.uint8)
    return Image.fromarray(noisy)


def degrade_image(image: Image.Image, blur_sigma: float) -> Image.Image:
    """Apply degradation with given blur sigma."""
    img = image.copy()
    if img.mode != 'RGB':
        img = img.convert('RGB')

    # Apply blur
    img = apply_gaussian_blur(img, blur_sigma)

    # Apply light noise for realism
    img = apply_light_noise(img, 0.01)

    return img


# ============================================================================
# OCR
# ============================================================================

def run_tesseract(image: Image.Image) -> str:
    """Run Tesseract OCR on image."""
    with tempfile.NamedTemporaryFile(suffix='.png', delete=True) as tmp:
        image.save(tmp.name, 'PNG')
        result = subprocess.run(
            ["tesseract", tmp.name, "stdout", "-l", "spa", "--psm", "6"],
            capture_output=True, text=True, timeout=60
        )
        return result.stdout


# Ground truth cache
_ground_truth_cache = {}

def get_ground_truth(doc_id: str) -> str:
    """Get ground truth OCR from pristine image."""
    if doc_id not in _ground_truth_cache:
        path = PRISTINE_DIR / PRISTINE_DOCS[doc_id]
        if path.exists():
            _ground_truth_cache[doc_id] = run_tesseract(Image.open(path))
        else:
            _ground_truth_cache[doc_id] = ""
    return _ground_truth_cache[doc_id]


# ============================================================================
# Validation
# ============================================================================

def validate_image(image: Image.Image, doc_id: str) -> Dict:
    """
    Test image with both methods and compare results.

    Returns dict with:
    - properties: image properties
    - no_filter: edit distance without any filter
    - lookup: edit distance with lookup table method
    - polynomial: edit distance with polynomial method
    """
    truth = get_ground_truth(doc_id)

    # Properties
    props = extract_image_properties(image)

    # No filter baseline
    ocr_no_filter = run_tesseract(image)
    edit_no_filter = Levenshtein.distance(ocr_no_filter, truth)

    # Lookup table method
    enhanced_lookup, meta_lookup = enhance_for_ocr(image, method="lookup", verbose=False)
    ocr_lookup = run_tesseract(enhanced_lookup)
    edit_lookup = Levenshtein.distance(ocr_lookup, truth)

    # Polynomial method
    enhanced_poly, meta_poly = enhance_for_ocr(image, method="polynomial", verbose=False)
    ocr_poly = run_tesseract(enhanced_poly)
    edit_poly = Levenshtein.distance(ocr_poly, truth)

    return {
        "properties": props,
        "no_filter": edit_no_filter,
        "lookup": edit_lookup,
        "lookup_cluster": meta_lookup["cluster_id"],
        "polynomial": edit_poly,
        "poly_params": meta_poly["filter_params"],
        "improvement_lookup": edit_no_filter - edit_lookup,
        "improvement_poly": edit_no_filter - edit_poly,
    }


def run_validation():
    """Run full validation."""
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print("=" * 70)
    print("VALIDATION: Polynomial vs Lookup Table on Unseen Images")
    print("=" * 70)

    # Load ground truths
    print("\nLoading ground truths...")
    for doc_id in PRISTINE_DOCS:
        get_ground_truth(doc_id)

    results = []

    for doc_id, filename in PRISTINE_DOCS.items():
        pristine_path = PRISTINE_DIR / filename
        if not pristine_path.exists():
            print(f"  Skipping {doc_id}: pristine not found")
            continue

        pristine_img = Image.open(pristine_path)
        print(f"\n=== {doc_id} ===")

        for blur in VALIDATION_BLURS:
            print(f"  Blur σ={blur:.1f}...", end=" ", flush=True)

            # Create degraded image
            degraded = degrade_image(pristine_img, blur)

            # Validate
            result = validate_image(degraded, doc_id)
            result["doc_id"] = doc_id
            result["blur_sigma"] = blur
            results.append(result)

            # Print summary
            winner = "POLY" if result["polynomial"] < result["lookup"] else "LOOKUP" if result["lookup"] < result["polynomial"] else "TIE"
            print(f"no_filter={result['no_filter']}, lookup={result['lookup']} (cluster {result['lookup_cluster']}), "
                  f"poly={result['polynomial']} → {winner}")

    # Summary statistics
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)

    lookup_wins = sum(1 for r in results if r["lookup"] < r["polynomial"])
    poly_wins = sum(1 for r in results if r["polynomial"] < r["lookup"])
    ties = sum(1 for r in results if r["polynomial"] == r["lookup"])

    avg_lookup = np.mean([r["lookup"] for r in results])
    avg_poly = np.mean([r["polynomial"] for r in results])
    avg_no_filter = np.mean([r["no_filter"] for r in results])

    avg_improvement_lookup = np.mean([r["improvement_lookup"] for r in results])
    avg_improvement_poly = np.mean([r["improvement_poly"] for r in results])

    print(f"\nTotal tests: {len(results)}")
    print(f"Lookup wins: {lookup_wins}")
    print(f"Polynomial wins: {poly_wins}")
    print(f"Ties: {ties}")
    print(f"\nAverage edit distance:")
    print(f"  No filter:   {avg_no_filter:.1f}")
    print(f"  Lookup:      {avg_lookup:.1f} (improvement: {avg_improvement_lookup:.1f})")
    print(f"  Polynomial:  {avg_poly:.1f} (improvement: {avg_improvement_poly:.1f})")

    winner = "POLYNOMIAL" if avg_poly < avg_lookup else "LOOKUP"
    print(f"\n>>> WINNER: {winner}")

    # Save results
    output = {
        "summary": {
            "total_tests": len(results),
            "lookup_wins": lookup_wins,
            "polynomial_wins": poly_wins,
            "ties": ties,
            "avg_no_filter": avg_no_filter,
            "avg_lookup": avg_lookup,
            "avg_polynomial": avg_poly,
            "avg_improvement_lookup": avg_improvement_lookup,
            "avg_improvement_polynomial": avg_improvement_poly,
            "winner": winner,
        },
        "results": results,
    }

    # Convert numpy types
    def convert(obj):
        if isinstance(obj, np.floating):
            return float(obj)
        elif isinstance(obj, np.integer):
            return int(obj)
        elif isinstance(obj, dict):
            return {k: convert(v) for k, v in obj.items()}
        elif isinstance(obj, list):
            return [convert(i) for i in obj]
        return obj

    output = convert(output)

    output_path = OUTPUT_DIR / "validation_results.json"
    with open(output_path, 'w') as f:
        json.dump(output, f, indent=2)

    print(f"\nResults saved to: {output_path}")


if __name__ == "__main__":
    run_validation()
