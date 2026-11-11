"""
Build OCR Performance Matrix Across Degradation Spectrum

Runs Tesseract OCR on all spectrum images and builds a performance matrix
showing how each document degrades across quality levels.

This enables:
1. Degradation curve analysis: visualize OCR quality degradation
2. Sensitivity clustering: group documents by degradation patterns
3. Filter strategy selection: target optimization to document clusters

Usage:
    python build_spectrum_performance_matrix.py

Outputs:
    - spectrum_performance_matrix.json: Complete performance data
    - spectrum_performance_matrix.csv: Human-readable table
    - degradation_curves.txt: Summary analysis
"""

import subprocess
import json
import csv
from pathlib import Path
from typing import Dict, List
import re


def levenshtein_distance(s1: str, s2: str) -> int:
    """Compute Levenshtein distance (minimum edit operations)."""
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)
    if len(s2) == 0:
        return len(s1)

    previous_row = range(len(s2) + 1)
    for i, c1 in enumerate(s1):
        current_row = [i + 1]
        for j, c2 in enumerate(s2):
            insertions = previous_row[j + 1] + 1
            deletions = current_row[j] + 1
            substitutions = previous_row[j] + (c1 != c2)
            current_row.append(min(insertions, deletions, substitutions))
        previous_row = current_row

    return previous_row[-1]


def normalize_text(text: str) -> str:
    """Normalize OCR text for comparison."""
    text = text.lower()
    text = re.sub(r'\s+', ' ', text)
    return text.strip()


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    """Run Tesseract OCR on an image."""
    cmd = [
        "C:/Program Files/Tesseract-OCR/tesseract.exe",
        "--tessdata-dir", "C:/Program Files/Tesseract-OCR/tessdata",
        str(image_path),
        "stdout",
        "-l", lang,
        "--psm", str(psm)
    ]

    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        return result.stdout
    except Exception as e:
        return ""


def build_performance_matrix(
    spectrum_base: Path,
    quality_levels: List[str],
    documents: List[str]
) -> Dict:
    """
    Build complete performance matrix across spectrum.

    Args:
        spectrum_base: Base directory containing spectrum subdirectories
        quality_levels: List of quality level names (ordered worst to best)
        documents: List of document filenames

    Returns:
        Performance matrix dictionary
    """
    print("=" * 80)
    print("BUILDING OCR PERFORMANCE MATRIX")
    print("=" * 80)
    print()
    print(f"Spectrum directory: {spectrum_base}")
    print(f"Quality levels: {len(quality_levels)}")
    print(f"Documents: {len(documents)}")
    print(f"Total OCR runs: {len(quality_levels) * len(documents)}")
    print("=" * 80)
    print()

    matrix = {}

    # Process each document
    for doc_idx, doc_name in enumerate(documents):
        print(f"[{doc_idx + 1}/{len(documents)}] Processing: {doc_name}")
        print()

        doc_key = doc_name.split('-')[0]  # Extract doc ID (e.g., "222AAA")
        matrix[doc_key] = {
            'filename': doc_name,
            'levels': {}
        }

        baseline_text = None

        # Process each quality level
        for level_idx, level in enumerate(quality_levels):
            image_path = spectrum_base / level / doc_name

            if not image_path.exists():
                print(f"  WARNING: Image not found: {level}")
                continue

            # Run OCR
            ocr_text = run_tesseract_ocr(image_path)
            normalized_text = normalize_text(ocr_text)

            # Use Q0 (pristine) as baseline
            if level == 'Q0_Pristine':
                baseline_text = normalized_text
                edit_distance = 0
            else:
                if baseline_text is None:
                    print(f"  ERROR: No baseline for {doc_key}")
                    edit_distance = 9999
                else:
                    edit_distance = levenshtein_distance(baseline_text, normalized_text)

            # Store results
            matrix[doc_key]['levels'][level] = {
                'edit_distance': edit_distance,
                'text_length': len(normalized_text),
                'baseline_length': len(baseline_text) if baseline_text else 0
            }

            # Progress indicator
            status = "✓" if edit_distance < 100 else "⚠" if edit_distance < 300 else "✗"
            print(f"  {status} {level:20} Edit distance: {edit_distance:4d}")

        print()

    print("=" * 80)
    print("✓ Performance matrix complete!")
    print("=" * 80)

    return matrix


def analyze_degradation_curves(matrix: Dict, quality_levels: List[str]) -> Dict:
    """
    Analyze degradation curves to identify document sensitivity patterns.

    Args:
        matrix: Performance matrix dictionary
        quality_levels: List of quality level names

    Returns:
        Analysis results dictionary
    """
    print()
    print("=" * 80)
    print("DEGRADATION CURVE ANALYSIS")
    print("=" * 80)
    print()

    analysis = {}

    for doc_key, doc_data in matrix.items():
        levels = doc_data['levels']

        # Extract degradation curve (edit distances across levels)
        curve = [levels.get(level, {}).get('edit_distance', 9999) for level in quality_levels]

        # Calculate degradation metrics
        q0_to_q1 = curve[2] - curve[0] if len(curve) > 2 else 0  # Q0_Pristine to Q1_Poor
        q1_to_q2 = curve[4] - curve[2] if len(curve) > 4 else 0  # Q1_Poor to Q2_MediumPoor
        total_degradation = curve[4] - curve[0] if len(curve) > 4 else 0  # Q0 to Q2

        # Degradation rate (edits per quality step)
        avg_degradation_rate = total_degradation / 4 if len(curve) > 4 else 0

        # Sensitivity classification
        if avg_degradation_rate < 50:
            sensitivity = "LOW"  # Robust to degradation
        elif avg_degradation_rate < 150:
            sensitivity = "MEDIUM"  # Moderate sensitivity
        else:
            sensitivity = "HIGH"  # Very sensitive to degradation

        analysis[doc_key] = {
            'curve': curve,
            'q0_to_q1_degradation': q0_to_q1,
            'q1_to_q2_degradation': q1_to_q2,
            'total_degradation': total_degradation,
            'avg_degradation_rate': avg_degradation_rate,
            'sensitivity': sensitivity,
            'q2_edit_distance': curve[4] if len(curve) > 4 else 9999
        }

        # Print summary
        print(f"{doc_key}:")
        print(f"  Sensitivity: {sensitivity}")
        print(f"  Degradation curve: {curve}")
        print(f"  Q0→Q1: +{q0_to_q1:3d} edits")
        print(f"  Q1→Q2: +{q1_to_q2:3d} edits")
        print(f"  Total: +{total_degradation:3d} edits")
        print(f"  Avg rate: {avg_degradation_rate:.1f} edits/step")
        print()

    print("=" * 80)
    print("✓ Degradation analysis complete!")
    print("=" * 80)

    return analysis


def save_results(matrix: Dict, analysis: Dict, output_dir: Path, quality_levels: List[str]):
    """Save performance matrix and analysis results."""
    print()
    print("=" * 80)
    print("SAVING RESULTS")
    print("=" * 80)
    print()

    # Save complete JSON
    json_path = output_dir / "spectrum_performance_matrix.json"
    with open(json_path, 'w') as f:
        json.dump({
            'matrix': matrix,
            'analysis': analysis,
            'quality_levels': quality_levels
        }, f, indent=2)
    print(f"✓ JSON saved: {json_path}")

    # Save CSV table
    csv_path = output_dir / "spectrum_performance_matrix.csv"
    with open(csv_path, 'w', newline='') as f:
        writer = csv.writer(f)

        # Header
        writer.writerow(['Document'] + quality_levels + ['Sensitivity', 'Avg Degradation Rate'])

        # Data rows
        for doc_key in sorted(matrix.keys()):
            curve = analysis[doc_key]['curve']
            sensitivity = analysis[doc_key]['sensitivity']
            avg_rate = analysis[doc_key]['avg_degradation_rate']

            writer.writerow([doc_key] + curve + [sensitivity, f"{avg_rate:.1f}"])

    print(f"✓ CSV saved: {csv_path}")

    # Save analysis summary
    summary_path = output_dir / "degradation_curves_summary.txt"
    with open(summary_path, 'w') as f:
        f.write("=" * 80 + "\n")
        f.write("DEGRADATION CURVE SUMMARY\n")
        f.write("=" * 80 + "\n\n")

        # Group by sensitivity
        by_sensitivity = {'LOW': [], 'MEDIUM': [], 'HIGH': []}
        for doc_key, data in analysis.items():
            by_sensitivity[data['sensitivity']].append(doc_key)

        for sensitivity in ['LOW', 'MEDIUM', 'HIGH']:
            docs = by_sensitivity[sensitivity]
            f.write(f"{sensitivity} SENSITIVITY ({len(docs)} documents):\n")
            for doc_key in docs:
                data = analysis[doc_key]
                f.write(f"  {doc_key}: {data['avg_degradation_rate']:.1f} edits/step, ")
                f.write(f"Q2 distance: {data['q2_edit_distance']} edits\n")
            f.write("\n")

        f.write("=" * 80 + "\n")
        f.write("CLUSTER RECOMMENDATION\n")
        f.write("=" * 80 + "\n\n")

        if len(by_sensitivity['LOW']) > 0:
            f.write("Cluster 1 (LOW sensitivity - Robust documents):\n")
            f.write(f"  Documents: {', '.join(by_sensitivity['LOW'])}\n")
            f.write(f"  Strategy: Light filtering, focus on Q1 rescue\n\n")

        if len(by_sensitivity['MEDIUM']) > 0:
            f.write("Cluster 2 (MEDIUM sensitivity - Moderate documents):\n")
            f.write(f"  Documents: {', '.join(by_sensitivity['MEDIUM'])}\n")
            f.write(f"  Strategy: Balanced filtering, Q1+Q2 optimization\n\n")

        if len(by_sensitivity['HIGH']) > 0:
            f.write("Cluster 3 (HIGH sensitivity - Fragile documents):\n")
            f.write(f"  Documents: {', '.join(by_sensitivity['HIGH'])}\n")
            f.write(f"  Strategy: Aggressive enhancement, Q2 focus\n\n")

    print(f"✓ Summary saved: {summary_path}")

    print("=" * 80)
    print("✓ All results saved!")
    print("=" * 80)


def main():
    """Main execution function."""

    # Configuration
    script_dir = Path(__file__).parent
    project_root = script_dir.parent

    spectrum_base = project_root / "Fixtures" / "PRP1_Spectrum"
    output_dir = project_root / "Fixtures"

    # Quality levels (ordered from best to worst for analysis)
    quality_levels = [
        'Q0_Pristine',
        'Q05_VeryGood',
        'Q1_Poor',
        'Q15_Medium',
        'Q2_MediumPoor'
    ]

    # Documents to process
    documents = [
        "222AAA-44444444442025_page-0001.jpg",
        "333BBB-44444444442025_page1.png",
        "333ccc-6666666662025_page1.png",
        "555CCC-66666662025_page1.png"
    ]

    # Build performance matrix
    matrix = build_performance_matrix(spectrum_base, quality_levels, documents)

    # Analyze degradation curves
    analysis = analyze_degradation_curves(matrix, quality_levels)

    # Save results
    save_results(matrix, analysis, output_dir, quality_levels)

    print()
    print("NEXT STEPS:")
    print("1. Review degradation_curves_summary.txt for cluster recommendations")
    print("2. Design specialized NSGA-II for each cluster")
    print("3. Launch parallel optimizations")


if __name__ == "__main__":
    main()
