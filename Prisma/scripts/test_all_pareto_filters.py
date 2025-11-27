"""
Comprehensive Filter Testing: PIL vs OpenCV Pareto Solutions

Tests ALL Pareto-optimal filters from both optimizations on the degradation spectrum.

Test Matrix:
- 4 documents × 5 degradation levels = 20 degraded images
- 20 PIL Pareto solutions
- 40 OpenCV Pareto solutions
- Total: 20 images × 60 filters = 1,200 OCR runs

Output:
- Comprehensive performance matrix comparing PIL vs OpenCV across all degradation levels
- Winner analysis: which pipeline works best for which degradation levels
- Cluster validation: does single-cluster finding hold with all filters?

Usage:
    python test_all_pareto_filters.py
"""

import json
import cv2
import numpy as np
import os
import pytesseract
from pathlib import Path
from PIL import Image, ImageEnhance, ImageFilter
import Levenshtein
from typing import Dict, List, Tuple
import pandas as pd
from datetime import datetime

# Configure Tesseract path and tessdata
pytesseract.pytesseract.tesseract_cmd = r'C:\Program Files\Tesseract-OCR\tesseract.exe'
os.environ['TESSDATA_PREFIX'] = r'C:\Program Files\Tesseract-OCR\tessdata'

# ============================================================================
# Configuration
# ============================================================================

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
FIXTURES_DIR = PROJECT_ROOT / "Fixtures"

# Input directories
SPECTRUM_DIR = FIXTURES_DIR / "PRP1_Spectrum"
PRISTINE_DIR = FIXTURES_DIR / "PRP1"

# Pareto front files
PIL_PARETO = FIXTURES_DIR / "nsga2_q2_pil_pareto_front.json"
OPENCV_PARETO = FIXTURES_DIR / "nsga2_opencv_top20.json"  # Top 20 from 100 server solutions

# Output files
OUTPUT_MATRIX = FIXTURES_DIR / "comprehensive_filter_performance_matrix.json"
OUTPUT_CSV = FIXTURES_DIR / "comprehensive_filter_performance_matrix.csv"
OUTPUT_SUMMARY = FIXTURES_DIR / "pil_vs_opencv_comparison_summary.txt"

# Ground truth documents (pristine OCR)
GROUND_TRUTH_DOCS = [
    "222AAA-44444444442025_page-0001.jpg",
    "333BBB-44444444442025_page1.png",
    "333ccc-6666666662025_page1.png",
    "555CCC-66666662025_page1.png"
]

# Degradation spectrum levels
SPECTRUM_LEVELS = [
    "Q0_Pristine",
    "Q05_VeryGood",
    "Q1_Poor",
    "Q15_Medium",
    "Q2_MediumPoor"
]

# Tesseract configuration (PSM 6 = uniform text block)
TESSERACT_CONFIG = '--psm 6'


# ============================================================================
# Ground Truth Loading
# ============================================================================

def load_ground_truth() -> Dict[str, str]:
    """Load ground truth OCR text from pristine documents."""
    print("Loading ground truth from pristine documents...")
    ground_truth = {}

    for doc_file in GROUND_TRUTH_DOCS:
        pristine_path = PRISTINE_DIR / doc_file

        if not pristine_path.exists():
            print(f"  WARNING: Missing pristine document: {doc_file}")
            continue

        # Load image
        img = cv2.imread(str(pristine_path))
        if img is None:
            print(f"  ERROR: Failed to load {doc_file}")
            continue

        # Convert to grayscale
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

        # Run OCR (no enhancement on pristine)
        text = pytesseract.image_to_string(gray, config=TESSERACT_CONFIG)

        # Extract document ID from filename
        doc_id = doc_file.split('-')[0]  # "222AAA", "333BBB", etc.

        ground_truth[doc_id] = text
        print(f"  ✓ {doc_id}: {len(text)} characters")

    return ground_truth


# ============================================================================
# Filter Application Functions
# ============================================================================

def apply_pil_filter(image: np.ndarray, params: dict) -> np.ndarray:
    """
    Apply PIL enhancement filter.

    Args:
        image: OpenCV image (BGR)
        params: {"contrast_factor": float, "median_size": int}

    Returns:
        Enhanced grayscale image (numpy array)
    """
    # Convert BGR to RGB for PIL
    rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    pil_img = Image.fromarray(rgb)

    # Convert to grayscale
    gray_pil = pil_img.convert('L')

    # Apply contrast enhancement
    contrast_factor = params.get('contrast_factor', 1.0)
    if contrast_factor != 1.0:
        enhancer = ImageEnhance.Contrast(gray_pil)
        gray_pil = enhancer.enhance(contrast_factor)

    # Apply median filter
    median_size = params.get('median_size', 3)
    if median_size > 1:
        gray_pil = gray_pil.filter(ImageFilter.MedianFilter(size=median_size))

    # Convert back to numpy array
    return np.array(gray_pil)


def apply_opencv_filter(image: np.ndarray, params: dict) -> np.ndarray:
    """
    Apply OpenCV enhancement pipeline.

    Args:
        image: OpenCV image (BGR)
        params: {
            "denoise_h": float,
            "clahe_clip": float,
            "bilateral_d": int,
            "bilateral_sigma_color": float,
            "bilateral_sigma_space": float,
            "unsharp_amount": float,
            "unsharp_radius": float
        }

    Returns:
        Enhanced grayscale image (numpy array)
    """
    # Convert to grayscale
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # 1. Non-Local Means Denoising
    h = int(params.get('denoise_h', 10))
    denoised = cv2.fastNlMeansDenoising(gray, h=h)

    # 2. CLAHE (Contrast Limited Adaptive Histogram Equalization)
    clip_limit = params.get('clahe_clip', 2.0)
    clahe = cv2.createCLAHE(clipLimit=clip_limit, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)

    # 3. Bilateral Filter (edge-preserving smoothing)
    d = int(params.get('bilateral_d', 5))
    sigma_color = params.get('bilateral_sigma_color', 75)
    sigma_space = params.get('bilateral_sigma_space', 75)
    bilateral = cv2.bilateralFilter(enhanced, d, sigma_color, sigma_space)

    # 4. Unsharp Mask (sharpening)
    amount = params.get('unsharp_amount', 1.0)
    radius = params.get('unsharp_radius', 1.0)

    # Gaussian blur for unsharp mask
    blurred = cv2.GaussianBlur(bilateral, (0, 0), radius)

    # Unsharp mask formula: original + amount * (original - blurred)
    sharpened = cv2.addWeighted(bilateral, 1.0 + amount, blurred, -amount, 0)

    return sharpened


# ============================================================================
# OCR and Performance Testing
# ============================================================================

def run_ocr_and_measure(image: np.ndarray, ground_truth: str) -> Tuple[str, int]:
    """
    Run OCR on image and measure Levenshtein distance to ground truth.

    Returns:
        (ocr_text, edit_distance)
    """
    ocr_text = pytesseract.image_to_string(image, config=TESSERACT_CONFIG)
    edit_distance = Levenshtein.distance(ground_truth, ocr_text)
    return ocr_text, edit_distance


def test_filter_on_spectrum(
    filter_id: str,
    filter_type: str,
    filter_params: dict,
    ground_truth: Dict[str, str]
) -> Dict:
    """
    Test a single filter on all degraded spectrum images.

    Args:
        filter_id: Unique filter identifier (e.g., "PIL_0", "OpenCV_5")
        filter_type: "PIL" or "OpenCV"
        filter_params: Filter parameters
        ground_truth: Ground truth OCR text per document

    Returns:
        Performance results dictionary
    """
    results = {
        'filter_id': filter_id,
        'filter_type': filter_type,
        'params': filter_params,
        'performance': {}
    }

    # Test on each degradation level
    for level in SPECTRUM_LEVELS:
        level_dir = SPECTRUM_DIR / level

        if not level_dir.exists():
            continue

        results['performance'][level] = {}

        # Test on each document
        for doc_file in GROUND_TRUTH_DOCS:
            doc_id = doc_file.split('-')[0]
            degraded_path = level_dir / doc_file

            if not degraded_path.exists():
                continue

            # Load degraded image
            img = cv2.imread(str(degraded_path))
            if img is None:
                continue

            # Apply filter
            if filter_type == "PIL":
                enhanced = apply_pil_filter(img, filter_params)
            else:  # OpenCV
                enhanced = apply_opencv_filter(img, filter_params)

            # Run OCR and measure performance
            _, edit_distance = run_ocr_and_measure(enhanced, ground_truth[doc_id])

            results['performance'][level][doc_id] = edit_distance

    return results


# ============================================================================
# Main Testing Pipeline
# ============================================================================

def load_pareto_fronts() -> Tuple[List[dict], List[dict]]:
    """Load PIL and OpenCV Pareto front solutions."""
    print("\nLoading Pareto front solutions...")

    # Load PIL solutions
    with open(PIL_PARETO, 'r') as f:
        pil_solutions = json.load(f)
    print(f"  ✓ PIL: {len(pil_solutions)} Pareto solutions")

    # Load OpenCV solutions
    with open(OPENCV_PARETO, 'r') as f:
        opencv_solutions = json.load(f)
    print(f"  ✓ OpenCV: {len(opencv_solutions)} Pareto solutions")

    return pil_solutions, opencv_solutions


def run_comprehensive_test():
    """Run comprehensive filter testing on degradation spectrum."""
    print("=" * 80)
    print("COMPREHENSIVE FILTER TESTING: PIL vs OpenCV")
    print("=" * 80)
    print()

    start_time = datetime.now()

    # Load ground truth
    ground_truth = load_ground_truth()
    print(f"  Ground truth loaded: {len(ground_truth)} documents")
    print()

    # Load Pareto solutions
    pil_solutions, opencv_solutions = load_pareto_fronts()
    total_filters = len(pil_solutions) + len(opencv_solutions)
    print(f"  Total filters to test: {total_filters}")
    print()

    # Calculate total OCR runs
    total_ocr_runs = len(GROUND_TRUTH_DOCS) * len(SPECTRUM_LEVELS) * total_filters
    print(f"  Total OCR runs: {total_ocr_runs:,}")
    print(f"  Estimated time: ~{total_ocr_runs * 2 / 60:.0f} minutes (2 sec/OCR)")
    print()
    print("=" * 80)
    print()

    all_results = []

    # Test PIL filters
    print("Testing PIL Pareto solutions...")
    for idx, solution in enumerate(pil_solutions):
        filter_id = f"PIL_{solution['id']}"
        params = solution['genome']

        print(f"  [{idx+1}/{len(pil_solutions)}] {filter_id}: contrast={params['contrast_factor']:.3f}, median={params['median_size']}")

        results = test_filter_on_spectrum(filter_id, "PIL", params, ground_truth)
        all_results.append(results)

    print()

    # Test OpenCV filters
    print("Testing OpenCV Pareto solutions...")
    for idx, solution in enumerate(opencv_solutions):
        # OpenCV solutions have different structure - extract params from genome
        filter_id = f"OpenCV_{idx}"

        # Parse OpenCV solution structure
        if 'genome' in solution:
            params = solution['genome']
        elif 'parameters' in solution:
            params = solution['parameters']
        else:
            # Skip if structure is unexpected
            print(f"  WARNING: Unexpected structure for solution {idx}")
            continue

        print(f"  [{idx+1}/{len(opencv_solutions)}] {filter_id}: h={params.get('denoise_h', 0):.0f}, CLAHE={params.get('clahe_clip', 0):.2f}")

        results = test_filter_on_spectrum(filter_id, "OpenCV", params, ground_truth)
        all_results.append(results)

    print()
    print("=" * 80)
    print("✓ All filters tested!")
    print()

    # Save comprehensive results
    print("Saving results...")
    with open(OUTPUT_MATRIX, 'w') as f:
        json.dump(all_results, f, indent=2)
    print(f"  ✓ Saved: {OUTPUT_MATRIX}")

    # Build comparison analysis
    analyze_results(all_results, ground_truth)

    elapsed = datetime.now() - start_time
    print()
    print("=" * 80)
    print(f"COMPLETE! Total time: {elapsed}")
    print("=" * 80)


def analyze_results(all_results: List[dict], ground_truth: Dict[str, str]):
    """Analyze and compare PIL vs OpenCV performance."""
    print("\nAnalyzing PIL vs OpenCV performance...")

    # Build comparison dataframe
    rows = []

    for result in all_results:
        filter_id = result['filter_id']
        filter_type = result['filter_type']

        # Calculate total edits per degradation level
        for level in SPECTRUM_LEVELS:
            if level not in result['performance']:
                continue

            total_edits = sum(result['performance'][level].values())

            rows.append({
                'filter_id': filter_id,
                'filter_type': filter_type,
                'degradation_level': level,
                'total_edits': total_edits,
                **result['performance'][level]  # Add per-document edits
            })

    df = pd.DataFrame(rows)

    # Save CSV
    df.to_csv(OUTPUT_CSV, index=False)
    print(f"  ✓ Saved CSV: {OUTPUT_CSV}")

    # Generate summary report
    with open(OUTPUT_SUMMARY, 'w') as f:
        f.write("=" * 80 + "\n")
        f.write("PIL vs OpenCV Pareto Solutions Comparison\n")
        f.write("=" * 80 + "\n\n")

        # Best performance per degradation level
        f.write("BEST PERFORMANCE PER DEGRADATION LEVEL:\n")
        f.write("-" * 80 + "\n\n")

        for level in SPECTRUM_LEVELS:
            level_df = df[df['degradation_level'] == level]

            if len(level_df) == 0:
                continue

            # Find best PIL and best OpenCV
            pil_df = level_df[level_df['filter_type'] == 'PIL']
            opencv_df = level_df[level_df['filter_type'] == 'OpenCV']

            best_pil = pil_df.nsmallest(1, 'total_edits')
            best_opencv = opencv_df.nsmallest(1, 'total_edits')

            f.write(f"{level}:\n")

            if not best_pil.empty:
                pil_row = best_pil.iloc[0]
                f.write(f"  Best PIL: {pil_row['filter_id']} - {pil_row['total_edits']:.0f} edits\n")

            if not best_opencv.empty:
                opencv_row = best_opencv.iloc[0]
                f.write(f"  Best OpenCV: {opencv_row['filter_id']} - {opencv_row['total_edits']:.0f} edits\n")

            # Declare winner
            if not best_pil.empty and not best_opencv.empty:
                if pil_row['total_edits'] < opencv_row['total_edits']:
                    improvement = (opencv_row['total_edits'] - pil_row['total_edits']) / opencv_row['total_edits'] * 100
                    f.write(f"  ✓ WINNER: PIL ({improvement:.1f}% better)\n")
                else:
                    improvement = (pil_row['total_edits'] - opencv_row['total_edits']) / pil_row['total_edits'] * 100
                    f.write(f"  ✓ WINNER: OpenCV ({improvement:.1f}% better)\n")

            f.write("\n")

        # Overall winner
        f.write("=" * 80 + "\n")
        f.write("OVERALL WINNER ACROSS ALL DEGRADATION LEVELS:\n")
        f.write("=" * 80 + "\n\n")

        pil_avg = df[df['filter_type'] == 'PIL']['total_edits'].mean()
        opencv_avg = df[df['filter_type'] == 'OpenCV']['total_edits'].mean()

        f.write(f"PIL Average: {pil_avg:.1f} edits\n")
        f.write(f"OpenCV Average: {opencv_avg:.1f} edits\n\n")

        if pil_avg < opencv_avg:
            improvement = (opencv_avg - pil_avg) / opencv_avg * 100
            f.write(f"✓ OVERALL WINNER: PIL ({improvement:.1f}% better on average)\n")
        else:
            improvement = (pil_avg - opencv_avg) / pil_avg * 100
            f.write(f"✓ OVERALL WINNER: OpenCV ({improvement:.1f}% better on average)\n")

    print(f"  ✓ Saved summary: {OUTPUT_SUMMARY}")
    print()
    print("Summary preview:")
    with open(OUTPUT_SUMMARY, 'r') as f:
        print(f.read())


if __name__ == "__main__":
    run_comprehensive_test()
