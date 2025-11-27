"""
Comprehensive Filter Testing: PIL vs OpenCV vs NO FILTER (Baseline)

CRITICAL TEST: Includes NO FILTER baseline to determine when filtering degrades quality.

Test Matrix:
- 4 documents × 5 degradation levels = 20 degraded images
- 1 baseline (NO FILTER)
- 20 PIL Pareto solutions
- 20 OpenCV Pareto solutions
- Total: 20 images × 41 filters = 820 OCR runs

Output:
- Comprehensive matrix including baseline performance
- Analysis: when does NO FILTER beat all optimized filters?
- Quality threshold determination: what defines "pristine"?
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

# Configure Tesseract
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
OPENCV_PARETO = FIXTURES_DIR / "nsga2_opencv_top20.json"

# Output files
OUTPUT_MATRIX = FIXTURES_DIR / "comprehensive_with_baseline_matrix.json"
OUTPUT_CSV = FIXTURES_DIR / "comprehensive_with_baseline_matrix.csv"
OUTPUT_SUMMARY = FIXTURES_DIR / "baseline_vs_filters_analysis.txt"

# Ground truth documents
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
        doc_id = doc_file.split('-')[0]

        ground_truth[doc_id] = text
        print(f"  ✓ {doc_id}: {len(text)} characters")

    return ground_truth


# ============================================================================
# Filter Application Functions
# ============================================================================

def apply_pil_filter(image: np.ndarray, params: dict) -> np.ndarray:
    """Apply PIL enhancement filter."""
    rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    pil_img = Image.fromarray(rgb)
    gray_pil = pil_img.convert('L')

    # Contrast enhancement
    contrast_factor = params.get('contrast_factor', 1.0)
    if contrast_factor != 1.0:
        enhancer = ImageEnhance.Contrast(gray_pil)
        gray_pil = enhancer.enhance(contrast_factor)

    # Median filter
    median_size = params.get('median_size', 3)
    if median_size > 1:
        gray_pil = gray_pil.filter(ImageFilter.MedianFilter(size=median_size))

    return np.array(gray_pil)


def apply_opencv_filter(image: np.ndarray, params: dict) -> np.ndarray:
    """Apply OpenCV enhancement pipeline."""
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # 1. Non-Local Means Denoising
    h = int(params.get('denoise_h', 10))
    denoised = cv2.fastNlMeansDenoising(gray, h=h)

    # 2. CLAHE
    clip_limit = params.get('clahe_clip', 2.0)
    clahe = cv2.createCLAHE(clipLimit=clip_limit, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)

    # 3. Bilateral Filter
    d = int(params.get('bilateral_d', 5))
    sigma_color = params.get('bilateral_sigma_color', 75)
    sigma_space = params.get('bilateral_sigma_space', 75)
    bilateral = cv2.bilateralFilter(enhanced, d, sigma_color, sigma_space)

    # 4. Unsharp Mask
    amount = params.get('unsharp_amount', 1.0)
    radius = params.get('unsharp_radius', 1.0)
    blurred = cv2.GaussianBlur(bilateral, (0, 0), radius)
    sharpened = cv2.addWeighted(bilateral, 1.0 + amount, blurred, -amount, 0)

    return sharpened


def apply_baseline(image: np.ndarray) -> np.ndarray:
    """NO FILTER - just convert to grayscale."""
    return cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)


# ============================================================================
# OCR and Performance Testing
# ============================================================================

def run_ocr_and_measure(image: np.ndarray, ground_truth: str) -> Tuple[str, int]:
    """Run OCR and measure Levenshtein distance."""
    ocr_text = pytesseract.image_to_string(image, config=TESSERACT_CONFIG)
    edit_distance = Levenshtein.distance(ground_truth, ocr_text)
    return ocr_text, edit_distance


def test_filter_on_spectrum(
    filter_id: str,
    filter_type: str,
    filter_params: dict,
    ground_truth: Dict[str, str]
) -> Dict:
    """Test a single filter on all degraded spectrum images."""
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
            if filter_type == "BASELINE":
                enhanced = apply_baseline(img)
            elif filter_type == "PIL":
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
    """Run comprehensive filter testing INCLUDING baseline."""
    print("=" * 80)
    print("COMPREHENSIVE FILTER TESTING: BASELINE vs PIL vs OpenCV")
    print("=" * 80)
    print()

    start_time = datetime.now()

    # Load ground truth
    ground_truth = load_ground_truth()
    print(f"  Ground truth loaded: {len(ground_truth)} documents")
    print()

    # Load Pareto solutions
    pil_solutions, opencv_solutions = load_pareto_fronts()
    total_filters = 1 + len(pil_solutions) + len(opencv_solutions)  # +1 for baseline
    print(f"  Total filters to test: {total_filters} (1 baseline + {len(pil_solutions)} PIL + {len(opencv_solutions)} OpenCV)")
    print()

    # Calculate total OCR runs
    total_ocr_runs = len(GROUND_TRUTH_DOCS) * len(SPECTRUM_LEVELS) * total_filters
    print(f"  Total OCR runs: {total_ocr_runs:,}")
    print(f"  Estimated time: ~{total_ocr_runs * 2 / 60:.0f} minutes (2 sec/OCR)")
    print()
    print("=" * 80)
    print()

    all_results = []

    # TEST BASELINE FIRST
    print("Testing BASELINE (NO FILTER)...")
    baseline_results = test_filter_on_spectrum("BASELINE", "BASELINE", {}, ground_truth)
    all_results.append(baseline_results)
    print("  ✓ Baseline tested")
    print()

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
        filter_id = f"OpenCV_{idx}"

        # Parse OpenCV solution structure
        if 'genome' in solution:
            params = solution['genome']
        elif 'parameters' in solution:
            params = solution['parameters']
        else:
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
    """Analyze and compare BASELINE vs PIL vs OpenCV performance."""
    print("\nAnalyzing BASELINE vs PIL vs OpenCV performance...")

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
        f.write("BASELINE vs PIL vs OpenCV Comparison\n")
        f.write("=" * 80 + "\n\n")

        # Best performance per degradation level (INCLUDING BASELINE)
        f.write("BEST PERFORMANCE PER DEGRADATION LEVEL:\n")
        f.write("-" * 80 + "\n\n")

        for level in SPECTRUM_LEVELS:
            level_df = df[df['degradation_level'] == level]

            if len(level_df) == 0:
                continue

            # Find best baseline, PIL, and OpenCV
            baseline_df = level_df[level_df['filter_type'] == 'BASELINE']
            pil_df = level_df[level_df['filter_type'] == 'PIL']
            opencv_df = level_df[level_df['filter_type'] == 'OpenCV']

            best_baseline = baseline_df.nsmallest(1, 'total_edits')
            best_pil = pil_df.nsmallest(1, 'total_edits')
            best_opencv = opencv_df.nsmallest(1, 'total_edits')

            f.write(f"{level}:\n")

            if not best_baseline.empty:
                baseline_row = best_baseline.iloc[0]
                f.write(f"  Baseline (NO FILTER): {baseline_row['total_edits']:.0f} edits\n")

            if not best_pil.empty:
                pil_row = best_pil.iloc[0]
                f.write(f"  Best PIL: {pil_row['filter_id']} - {pil_row['total_edits']:.0f} edits\n")

            if not best_opencv.empty:
                opencv_row = best_opencv.iloc[0]
                f.write(f"  Best OpenCV: {opencv_row['filter_id']} - {opencv_row['total_edits']:.0f} edits\n")

            # Declare overall winner (including baseline)
            if not best_baseline.empty:
                all_scores = [
                    (baseline_row['total_edits'], 'BASELINE', baseline_row),
                ]
                if not best_pil.empty:
                    all_scores.append((pil_row['total_edits'], 'PIL', pil_row))
                if not best_opencv.empty:
                    all_scores.append((opencv_row['total_edits'], 'OpenCV', opencv_row))

                all_scores.sort(key=lambda x: x[0])
                winner_score, winner_type, winner_row = all_scores[0]

                if winner_type == 'BASELINE':
                    f.write(f"  ✓ WINNER: BASELINE (NO FILTER)\n")
                    f.write(f"    → Filtering DEGRADES quality on this level!\n")
                else:
                    improvement_vs_baseline = (baseline_row['total_edits'] - winner_score) / baseline_row['total_edits'] * 100
                    f.write(f"  ✓ WINNER: {winner_type} ({winner_row['filter_id']}) - {improvement_vs_baseline:.1f}% better than baseline\n")

            f.write("\n")

        # Overall statistics
        f.write("=" * 80 + "\n")
        f.write("OVERALL STATISTICS:\n")
        f.write("=" * 80 + "\n\n")

        baseline_avg = df[df['filter_type'] == 'BASELINE']['total_edits'].mean()
        pil_avg = df[df['filter_type'] == 'PIL']['total_edits'].mean()
        opencv_avg = df[df['filter_type'] == 'OpenCV']['total_edits'].mean()

        f.write(f"Baseline Average: {baseline_avg:.1f} edits\n")
        f.write(f"PIL Average: {pil_avg:.1f} edits\n")
        f.write(f"OpenCV Average: {opencv_avg:.1f} edits\n\n")

        # Quality threshold analysis
        f.write("=" * 80 + "\n")
        f.write("QUALITY THRESHOLD ANALYSIS:\n")
        f.write("=" * 80 + "\n\n")

        f.write("When does NO FILTER beat all optimized filters?\n\n")

        for level in SPECTRUM_LEVELS:
            level_df = df[df['degradation_level'] == level]

            baseline_score = level_df[level_df['filter_type'] == 'BASELINE']['total_edits'].min()
            best_filter_score = level_df[level_df['filter_type'] != 'BASELINE']['total_edits'].min()

            if baseline_score <= best_filter_score:
                f.write(f"  {level}: BASELINE WINS ({baseline_score:.0f} vs {best_filter_score:.0f})\n")
            else:
                improvement = (baseline_score - best_filter_score) / baseline_score * 100
                f.write(f"  {level}: Filters help ({improvement:.1f}% improvement)\n")

    print(f"  ✓ Saved summary: {OUTPUT_SUMMARY}")
    print()
    print("Summary preview:")
    with open(OUTPUT_SUMMARY, 'r') as f:
        print(f.read())


if __name__ == "__main__":
    run_comprehensive_test()
