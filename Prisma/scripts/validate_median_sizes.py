#!/usr/bin/env python3
"""
Validation Test: Compare median_size = 3, 5, 7

This script tests whether the GA is correctly exploring the median_size
search space, or if median=3 is indeed optimal.

Tests on the golden test document (555CCC Q2_MediumPoor) with:
- contrast_factor = 1.05 (known optimal)
- median_size = 3, 5, 7

Expected: median=3 might be optimal, but 5 and 7 should give different
(possibly worse) results, proving the search space is being explored.
"""

import subprocess
from pathlib import Path
from PIL import Image, ImageEnhance, ImageFilter
import re


def levenshtein_distance(s1: str, s2: str) -> int:
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)
    if len(s2) == 0:
        return len(s1)
    prev = range(len(s2) + 1)
    for i, c1 in enumerate(s1):
        curr = [i + 1]
        for j, c2 in enumerate(s2):
            curr.append(min(prev[j + 1] + 1, curr[j] + 1, prev[j] + (c1 != c2)))
        prev = curr
    return prev[-1]


def normalize_text(text: str) -> str:
    return re.sub(r'\s+', ' ', text.lower()).strip()


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    result = subprocess.run(
        ["tesseract", str(image_path), "stdout", "-l", lang, "--psm", str(psm)],
        capture_output=True, text=True, timeout=60
    )
    return result.stdout


def apply_pil_filter(image_path: Path, contrast_factor: float, median_size: int) -> Image.Image:
    image = Image.open(image_path)
    if image.mode != 'L':
        image = image.convert('L')
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(contrast_factor)
    image = image.filter(ImageFilter.MedianFilter(size=median_size))
    return image


def main():
    base_path = Path(__file__).parent.parent / "Fixtures"

    # Golden test document
    degraded_path = base_path / "PRP1_Degraded" / "Q2_MediumPoor" / "555CCC-66666662025_page1.png"
    pristine_path = base_path / "PRP1" / "555CCC-66666662025_page1.png"

    if not degraded_path.exists():
        print(f"ERROR: Degraded image not found: {degraded_path}")
        return

    # Get ground truth
    print("Loading ground truth from pristine document...")
    gt_text = run_tesseract_ocr(pristine_path)
    gt_normalized = normalize_text(gt_text)

    # Baseline (no filter)
    print("\nBaseline (no filter):")
    baseline_text = run_tesseract_ocr(degraded_path)
    baseline_dist = levenshtein_distance(gt_normalized, normalize_text(baseline_text))
    print(f"  Edit distance: {baseline_dist}")

    # Test different median sizes
    contrast = 1.05  # Known optimal

    print("\n" + "="*60)
    print("MEDIAN SIZE COMPARISON (contrast=1.05)")
    print("="*60)

    results = []
    for median_size in [3, 5, 7]:
        print(f"\nTesting median_size = {median_size}...")
        enhanced = apply_pil_filter(degraded_path, contrast, median_size)

        temp_path = base_path / f"temp_median_test_{median_size}.png"
        enhanced.save(temp_path)

        ocr_text = run_tesseract_ocr(temp_path)
        edit_dist = levenshtein_distance(gt_normalized, normalize_text(ocr_text))

        temp_path.unlink()

        improvement = (baseline_dist - edit_dist) / baseline_dist * 100
        results.append((median_size, edit_dist, improvement))

        print(f"  Edit distance: {edit_dist}")
        print(f"  Improvement: {improvement:.1f}%")

    # Summary
    print("\n" + "="*60)
    print("SUMMARY")
    print("="*60)
    print(f"\nBaseline: {baseline_dist} edits")
    print("\n| Median Size | Edit Distance | Improvement |")
    print("|-------------|---------------|-------------|")
    for median, dist, imp in results:
        marker = " <-- BEST" if dist == min(r[1] for r in results) else ""
        print(f"| {median:^11} | {dist:^13} | {imp:>10.1f}% |{marker}")

    # Check if median varies
    unique_results = len(set(r[1] for r in results))
    if unique_results == 1:
        print("\n>>> WARNING: All median sizes give SAME result!")
        print("    This might indicate a bug in the filter application.")
    else:
        print(f"\n>>> OK: Different median sizes give {unique_results} different results.")
        print("    The search space is valid.")

        best_median = min(results, key=lambda x: x[1])[0]
        if best_median == 3:
            print(f"\n    Conclusion: median=3 IS the optimal value for this document.")
            print("    GA convergence to median=3 is LEGITIMATE.")
        else:
            print(f"\n    Conclusion: median={best_median} is better than median=3.")
            print("    GA should have found this - potential issue!")


if __name__ == "__main__":
    main()
