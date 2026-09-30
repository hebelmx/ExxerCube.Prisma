#!/usr/bin/env python3
"""
Reference-Based OCR Quality Analyzer using Levenshtein Distance.

Objective measurement of OCR quality by comparing against ground truth:
1. OCR pristine documents → ground truth reference text
2. OCR degraded/enhanced documents → test text
3. Compute Levenshtein distance (character-level edit distance)
4. Generate objective quality report

Metrics:
- Character Error Rate (CER) = edits / total_chars
- Word Error Rate (WER) = word_edits / total_words
- Total Distance = sum of all character edits

This provides the FITNESS FUNCTION for genetic algorithm optimization.

Usage:
    python measure_ocr_quality.py
"""

import cv2
import subprocess
from pathlib import Path
from typing import Dict, List, Tuple
import json
import re


def levenshtein_distance(s1: str, s2: str) -> int:
    """
    Compute Levenshtein distance (minimum edit operations).

    Returns: Number of insertions, deletions, substitutions needed
             to transform s1 into s2.
    """
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)

    if len(s2) == 0:
        return len(s1)

    previous_row = range(len(s2) + 1)
    for i, c1 in enumerate(s1):
        current_row = [i + 1]
        for j, c2 in enumerate(s2):
            # Cost of insertions, deletions, substitutions
            insertions = previous_row[j + 1] + 1
            deletions = current_row[j] + 1
            substitutions = previous_row[j] + (c1 != c2)
            current_row.append(min(insertions, deletions, substitutions))
        previous_row = current_row

    return previous_row[-1]


def normalize_text(text: str) -> str:
    """
    Normalize OCR text for fair comparison.

    - Convert to lowercase
    - Remove extra whitespace
    - Normalize line breaks
    """
    # Convert to lowercase
    text = text.lower()

    # Normalize whitespace
    text = re.sub(r'\s+', ' ', text)

    # Remove leading/trailing whitespace
    text = text.strip()

    return text


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    """
    Run Tesseract OCR on an image and return text.

    Args:
        image_path: Path to image file
        lang: Tesseract language (default: spa)
        psm: Page segmentation mode (default: 6)

    Returns:
        OCR text output
    """
    cmd = [
        "C:/Program Files/Tesseract-OCR/tesseract.exe",
        "--tessdata-dir", "C:/Program Files/Tesseract-OCR/tessdata",
        str(image_path),
        "stdout",
        "-l", lang,
        "--psm", str(psm)
    ]

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=60
        )
        return result.stdout
    except Exception as e:
        print(f"OCR failed for {image_path}: {e}")
        return ""


def compute_quality_metrics(ground_truth: str, test_text: str) -> Dict[str, float]:
    """
    Compute OCR quality metrics by comparing test text to ground truth.

    Returns:
        Dictionary with:
        - char_distance: Total character edit distance (Levenshtein)
        - char_error_rate: CER = edits / total_chars
        - word_distance: Word-level edit distance
        - word_error_rate: WER = word_edits / total_words
    """
    # Normalize both texts
    gt_norm = normalize_text(ground_truth)
    test_norm = normalize_text(test_text)

    # Character-level metrics
    char_distance = levenshtein_distance(gt_norm, test_norm)
    char_total = len(gt_norm)
    char_error_rate = char_distance / char_total if char_total > 0 else 0.0

    # Word-level metrics
    gt_words = gt_norm.split()
    test_words = test_norm.split()
    word_distance = levenshtein_distance(' '.join(gt_words), ' '.join(test_words))
    word_total = len(gt_words)
    word_error_rate = word_distance / word_total if word_total > 0 else 0.0

    return {
        'char_distance': char_distance,
        'char_total': char_total,
        'char_error_rate': char_error_rate,
        'word_distance': word_distance,
        'word_total': word_total,
        'word_error_rate': word_error_rate,
        'accuracy': 1.0 - char_error_rate  # For GA fitness (maximize accuracy)
    }


def extract_ground_truth(pristine_base: Path) -> Dict[str, str]:
    """
    Extract ground truth text from pristine documents.

    Returns:
        Dictionary mapping document name → OCR text
    """
    print("="*80)
    print("EXTRACTING GROUND TRUTH FROM PRISTINE DOCUMENTS")
    print("="*80)

    ground_truth = {}

    # Document names
    docs = [
        "222AAA-44444444442025_page-0001.jpg",
        "333BBB-44444444442025_page1.png",
        "333ccc-6666666662025_page1.png",
        "555CCC-66666662025_page1.png"
    ]

    for doc in docs:
        doc_path = pristine_base / doc

        if not doc_path.exists():
            print(f"  ⚠ SKIP: {doc} not found")
            continue

        print(f"\n  Processing: {doc}")
        text = run_tesseract_ocr(doc_path)

        # Store ground truth
        ground_truth[doc] = text

        print(f"    ✓ Extracted {len(text)} characters")

    print(f"\n  Ground truth extracted for {len(ground_truth)} documents")
    print("="*80)

    return ground_truth


def measure_enhancement_quality(
    ground_truth: Dict[str, str],
    test_base: Path,
    test_name: str
) -> Dict[str, Dict]:
    """
    Measure OCR quality for a test set by comparing to ground truth.

    Args:
        ground_truth: Reference text from pristine documents
        test_base: Base path for test images
        test_name: Name of test set (e.g., "Q2_MediumPoor")

    Returns:
        Dictionary mapping document → quality metrics
    """
    print(f"\n{'#'*80}")
    print(f"# MEASURING: {test_name}")
    print(f"# Path: {test_base}")
    print(f"{'#'*80}\n")

    results = {}

    for doc_name, gt_text in ground_truth.items():
        test_path = test_base / doc_name

        if not test_path.exists():
            print(f"  ⚠ SKIP: {doc_name} not found")
            continue

        print(f"  Processing: {doc_name}")

        # OCR test image
        test_text = run_tesseract_ocr(test_path)

        # Compute quality metrics
        metrics = compute_quality_metrics(gt_text, test_text)

        print(f"    Character Error Rate: {metrics['char_error_rate']*100:.2f}%")
        print(f"    Edit Distance: {metrics['char_distance']} chars")
        print(f"    Accuracy: {metrics['accuracy']*100:.2f}%")

        results[doc_name] = metrics

    return results


def main():
    """Run comprehensive OCR quality measurement."""

    base_path = Path(__file__).parent.parent / "Fixtures"

    # Test sets to measure
    test_sets = [
        ("Pristine Baseline", base_path / "PRP1"),
        ("Q1_Poor Degraded", base_path / "PRP1_Degraded" / "Q1_Poor"),
        ("Q2_MediumPoor Degraded", base_path / "PRP1_Degraded" / "Q2_MediumPoor"),
        ("Q2 Fixed Enhancement", base_path / "PRP1_Enhanced" / "Q2_MediumPoor"),
        ("Q2 Adaptive Enhancement", base_path / "PRP1_Enhanced_Adaptive" / "Q2_MediumPoor"),
    ]

    print("="*80)
    print("REFERENCE-BASED OCR QUALITY ANALYZER")
    print("Objective measurement using Levenshtein Distance")
    print("="*80)
    print()

    # Step 1: Extract ground truth from pristine documents
    pristine_base = base_path / "PRP1"
    ground_truth = extract_ground_truth(pristine_base)

    # Step 2: Measure quality for all test sets
    all_results = {}

    for test_name, test_base in test_sets:
        if not test_base.exists():
            print(f"\n⚠ SKIP: {test_name} - directory not found")
            continue

        results = measure_enhancement_quality(ground_truth, test_base, test_name)
        all_results[test_name] = results

    # Step 3: Generate comparative summary
    print("\n\n" + "="*80)
    print("COMPARATIVE SUMMARY - CHARACTER ERROR RATE (CER)")
    print("="*80)
    print()
    print(f"{'Document':<30} {'Test Set':<30} {'CER':<10} {'Accuracy':<10} {'Edits':<10}")
    print("-"*90)

    for test_name, results in all_results.items():
        for doc_name, metrics in results.items():
            doc_short = doc_name.split('-')[0]  # 222AAA, 333BBB, etc.
            print(f"{doc_short:<30} {test_name:<30} "
                  f"{metrics['char_error_rate']*100:>8.2f}% "
                  f"{metrics['accuracy']*100:>8.2f}% "
                  f"{metrics['char_distance']:>8d}")

    print("-"*90)

    # Step 4: Aggregate statistics by test set
    print("\n\n" + "="*80)
    print("AGGREGATE STATISTICS BY TEST SET")
    print("="*80)
    print()
    print(f"{'Test Set':<40} {'Avg CER':<12} {'Avg Accuracy':<12} {'Total Edits':<12}")
    print("-"*76)

    for test_name, results in all_results.items():
        if not results:
            continue

        avg_cer = sum(m['char_error_rate'] for m in results.values()) / len(results)
        avg_acc = sum(m['accuracy'] for m in results.values()) / len(results)
        total_edits = sum(m['char_distance'] for m in results.values())

        print(f"{test_name:<40} {avg_cer*100:>10.2f}% {avg_acc*100:>10.2f}% {total_edits:>10d}")

    print("-"*76)

    # Step 5: Winner determination
    print("\n\n" + "="*80)
    print("WINNER DETERMINATION (Q2 Documents Only)")
    print("="*80)
    print()

    # Compare only Q2 enhancement strategies
    q2_results = {
        "Degraded Baseline": all_results.get("Q2_MediumPoor Degraded", {}),
        "Fixed Enhancement": all_results.get("Q2 Fixed Enhancement", {}),
        "Adaptive Enhancement": all_results.get("Q2 Adaptive Enhancement", {}),
    }

    for strategy_name, results in q2_results.items():
        if not results:
            continue

        avg_edits = sum(m['char_distance'] for m in results.values()) / len(results)
        avg_cer = sum(m['char_error_rate'] for m in results.values()) / len(results)

        print(f"  {strategy_name:<25} Avg Edits: {avg_edits:>8.1f}  CER: {avg_cer*100:>6.2f}%")

    # Determine winner
    winners = sorted(
        [(name, sum(m['char_distance'] for m in res.values()))
         for name, res in q2_results.items() if res],
        key=lambda x: x[1]
    )

    if winners:
        print(f"\n  🏆 WINNER: {winners[0][0]} (Lowest total edit distance: {winners[0][1]})")

    # Step 6: Save detailed results to JSON
    output_file = base_path / "ocr_quality_analysis.json"
    with open(output_file, 'w') as f:
        json.dump(all_results, f, indent=2)

    print(f"\n\nDetailed results saved to: {output_file}")
    print()
    print("="*80)
    print("NEXT STEPS FOR GENETIC ALGORITHM OPTIMIZATION")
    print("="*80)
    print()
    print("This analysis provides the FITNESS FUNCTION for GA:")
    print("  - Fitness = Minimize total edit distance")
    print("  - Genome = Filter parameters (denoise_h, clahe_clip, etc.)")
    print("  - GA will explore parameter space to find optimal combinations")
    print()
    print("Suggested GA parameters:")
    print("  - Population size: 20")
    print("  - Generations: 50")
    print("  - Mutation rate: 0.1")
    print("  - Crossover rate: 0.7")
    print("="*80)
    print()


if __name__ == "__main__":
    main()
