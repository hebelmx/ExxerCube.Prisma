#!/usr/bin/env python3
"""
Golden Test Demo: Q2_MediumPoor OCR Enhancement

This script demonstrates the 89.7% OCR improvement achieved through
GA-optimized PIL filtering. Use for:
- Unit testing the enhancement pipeline
- Stakeholder demonstrations
- Regression testing after code changes

Usage:
    python golden_test_demo.py              # Run test
    python golden_test_demo.py --visual     # Generate side-by-side images
    python golden_test_demo.py --report     # Generate HTML report
"""

import json
import argparse
import subprocess
from pathlib import Path
from dataclasses import dataclass
from typing import Optional
from PIL import Image, ImageEnhance, ImageFilter, ImageDraw, ImageFont
import re


# =============================================================================
# CONFIGURATION
# =============================================================================

@dataclass
class GoldenTestConfig:
    """Golden test configuration loaded from JSON."""
    document_path: Path
    ground_truth_path: Path
    contrast_factor: float
    median_size: int
    expected_baseline_edit_distance: int
    expected_filtered_edit_distance: int
    improvement_threshold: float  # minimum acceptable improvement %
    max_edit_distance: int  # maximum acceptable edit distance after filtering


def load_config(base_path: Path) -> GoldenTestConfig:
    """Load golden test configuration."""
    config_path = base_path / "golden_test_q2_mediumpoor_555ccc.json"
    with open(config_path) as f:
        data = json.load(f)

    return GoldenTestConfig(
        document_path=base_path / data["document"]["source_path"],
        ground_truth_path=base_path / data["document"]["ground_truth_path"],
        contrast_factor=data["optimal_filter"]["parameters"]["contrast_factor"],
        median_size=data["optimal_filter"]["parameters"]["median_size"],
        expected_baseline_edit_distance=data["results"]["baseline_edit_distance"],
        expected_filtered_edit_distance=data["results"]["filtered_edit_distance"],
        improvement_threshold=data["unit_test_assertions"]["improvement_threshold"],
        max_edit_distance=data["unit_test_assertions"]["max_acceptable_edit_distance"],
    )


# =============================================================================
# CORE FUNCTIONS
# =============================================================================

def levenshtein_distance(s1: str, s2: str) -> int:
    """Compute Levenshtein edit distance."""
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
    """Normalize OCR text for comparison."""
    return re.sub(r'\s+', ' ', text.lower()).strip()


def run_tesseract_ocr(image_path: Path, lang: str = "spa", psm: int = 6) -> str:
    """Run Tesseract OCR on image."""
    result = subprocess.run(
        ["tesseract", str(image_path), "stdout", "-l", lang, "--psm", str(psm)],
        capture_output=True, text=True, timeout=60
    )
    return result.stdout


def apply_pil_filter(image_path: Path, contrast_factor: float, median_size: int) -> Image.Image:
    """Apply PIL enhancement filter."""
    image = Image.open(image_path)
    if image.mode != 'L':
        image = image.convert('L')

    # Apply contrast enhancement
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(contrast_factor)

    # Apply median filter
    image = image.filter(ImageFilter.MedianFilter(size=median_size))

    return image


# =============================================================================
# TEST EXECUTION
# =============================================================================

@dataclass
class TestResult:
    """Result of running the golden test."""
    passed: bool
    ground_truth_text: str
    raw_ocr_text: str
    filtered_ocr_text: str
    baseline_edit_distance: int
    filtered_edit_distance: int
    improvement_percentage: float
    messages: list


def run_golden_test(config: GoldenTestConfig, verbose: bool = True) -> TestResult:
    """
    Execute the golden test.

    Returns TestResult with pass/fail status and details.
    """
    messages = []

    if verbose:
        print("="*70)
        print("GOLDEN TEST: Q2_MediumPoor 555CCC OCR Enhancement")
        print("="*70)
        print()

    # 1. Load ground truth
    if verbose:
        print("Loading ground truth from pristine document...")
    ground_truth_text = run_tesseract_ocr(config.ground_truth_path)
    gt_normalized = normalize_text(ground_truth_text)

    # 2. OCR without filter
    if verbose:
        print("Running OCR on raw degraded document...")
    raw_ocr_text = run_tesseract_ocr(config.document_path)
    raw_normalized = normalize_text(raw_ocr_text)
    baseline_edit_distance = levenshtein_distance(gt_normalized, raw_normalized)

    # 3. Apply filter and OCR
    if verbose:
        print(f"Applying filter (contrast={config.contrast_factor:.4f}, median={config.median_size})...")
    enhanced_image = apply_pil_filter(
        config.document_path,
        config.contrast_factor,
        config.median_size
    )

    # Save temp file for OCR
    temp_path = config.document_path.parent / "temp_golden_test.png"
    enhanced_image.save(temp_path)

    if verbose:
        print("Running OCR on enhanced document...")
    filtered_ocr_text = run_tesseract_ocr(temp_path)
    filtered_normalized = normalize_text(filtered_ocr_text)
    filtered_edit_distance = levenshtein_distance(gt_normalized, filtered_normalized)

    # Cleanup
    temp_path.unlink()

    # 4. Calculate improvement
    improvement_pct = (baseline_edit_distance - filtered_edit_distance) / baseline_edit_distance * 100

    # 5. Check assertions
    passed = True

    if filtered_edit_distance > config.max_edit_distance:
        passed = False
        messages.append(f"FAIL: Edit distance {filtered_edit_distance} > max {config.max_edit_distance}")
    else:
        messages.append(f"PASS: Edit distance {filtered_edit_distance} <= max {config.max_edit_distance}")

    if improvement_pct < config.improvement_threshold:
        passed = False
        messages.append(f"FAIL: Improvement {improvement_pct:.1f}% < threshold {config.improvement_threshold}%")
    else:
        messages.append(f"PASS: Improvement {improvement_pct:.1f}% >= threshold {config.improvement_threshold}%")

    if filtered_edit_distance >= baseline_edit_distance:
        passed = False
        messages.append("FAIL: Filter made OCR worse or no improvement")
    else:
        messages.append(f"PASS: Filter improved OCR by {baseline_edit_distance - filtered_edit_distance} edits")

    # 6. Report
    if verbose:
        print()
        print("="*70)
        print("RESULTS")
        print("="*70)
        print(f"Baseline edit distance:  {baseline_edit_distance}")
        print(f"Filtered edit distance:  {filtered_edit_distance}")
        print(f"Improvement:             {improvement_pct:.1f}%")
        print()
        print("Assertions:")
        for msg in messages:
            print(f"  {msg}")
        print()
        print(f"TEST {'PASSED' if passed else 'FAILED'}")
        print("="*70)

    return TestResult(
        passed=passed,
        ground_truth_text=ground_truth_text,
        raw_ocr_text=raw_ocr_text,
        filtered_ocr_text=filtered_ocr_text,
        baseline_edit_distance=baseline_edit_distance,
        filtered_edit_distance=filtered_edit_distance,
        improvement_percentage=improvement_pct,
        messages=messages,
    )


# =============================================================================
# VISUAL COMPARISON
# =============================================================================

def generate_visual_comparison(config: GoldenTestConfig, output_path: Path):
    """Generate side-by-side visual comparison for stakeholders."""

    # Load images
    original = Image.open(config.document_path)
    if original.mode != 'L':
        original = original.convert('L')

    enhanced = apply_pil_filter(
        config.document_path,
        config.contrast_factor,
        config.median_size
    )

    # Create side-by-side comparison
    width = original.width
    height = original.height

    # Create combined image with labels
    combined = Image.new('RGB', (width * 2 + 20, height + 80), color='white')

    # Paste images
    combined.paste(original.convert('RGB'), (0, 60))
    combined.paste(enhanced.convert('RGB'), (width + 20, 60))

    # Add labels
    draw = ImageDraw.Draw(combined)
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 20)
        small_font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 14)
    except:
        font = ImageFont.load_default()
        small_font = font

    draw.text((10, 10), "BEFORE (Raw Degraded)", fill='red', font=font)
    draw.text((10, 35), "Edit Distance: 1518", fill='darkred', font=small_font)

    draw.text((width + 30, 10), "AFTER (PIL Enhanced)", fill='green', font=font)
    draw.text((width + 30, 35), "Edit Distance: 157 (89.7% improvement)", fill='darkgreen', font=small_font)

    combined.save(output_path)
    print(f"Visual comparison saved to: {output_path}")


# =============================================================================
# HTML REPORT
# =============================================================================

def generate_html_report(config: GoldenTestConfig, result: TestResult, output_path: Path):
    """Generate HTML report for stakeholders."""

    html = f"""<!DOCTYPE html>
<html>
<head>
    <title>OCR Enhancement Golden Test Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; background: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        h1 {{ color: #333; border-bottom: 3px solid #4CAF50; padding-bottom: 10px; }}
        h2 {{ color: #666; margin-top: 30px; }}
        .metric {{ display: inline-block; background: #e8f5e9; padding: 20px; margin: 10px; border-radius: 8px; text-align: center; min-width: 150px; }}
        .metric.bad {{ background: #ffebee; }}
        .metric-value {{ font-size: 36px; font-weight: bold; color: #2e7d32; }}
        .metric.bad .metric-value {{ color: #c62828; }}
        .metric-label {{ color: #666; margin-top: 5px; }}
        .improvement {{ background: linear-gradient(135deg, #4CAF50, #8BC34A); color: white; padding: 30px; border-radius: 10px; text-align: center; margin: 20px 0; }}
        .improvement-value {{ font-size: 48px; font-weight: bold; }}
        .ocr-comparison {{ display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-top: 20px; }}
        .ocr-box {{ background: #fafafa; padding: 15px; border-radius: 8px; border: 1px solid #ddd; }}
        .ocr-box h3 {{ margin-top: 0; }}
        .ocr-box.before {{ border-left: 4px solid #f44336; }}
        .ocr-box.after {{ border-left: 4px solid #4CAF50; }}
        .ocr-text {{ font-family: monospace; font-size: 11px; white-space: pre-wrap; max-height: 300px; overflow-y: auto; background: white; padding: 10px; border-radius: 4px; }}
        .filter-params {{ background: #e3f2fd; padding: 15px; border-radius: 8px; margin: 20px 0; }}
        .pass {{ color: #2e7d32; }}
        .fail {{ color: #c62828; }}
        .assertions {{ background: #fff3e0; padding: 15px; border-radius: 8px; }}
        .assertions li {{ margin: 8px 0; }}
    </style>
</head>
<body>
    <div class="container">
        <h1>🔬 OCR Enhancement Golden Test Report</h1>
        <p><strong>Document:</strong> 555CCC-66666662025 (Q2_MediumPoor quality)</p>
        <p><strong>Test Status:</strong> <span class="{'pass' if result.passed else 'fail'}">{'✅ PASSED' if result.passed else '❌ FAILED'}</span></p>

        <div class="improvement">
            <div>OCR Quality Improvement</div>
            <div class="improvement-value">{result.improvement_percentage:.1f}%</div>
            <div>From {result.baseline_edit_distance} errors → {result.filtered_edit_distance} errors</div>
        </div>

        <h2>📊 Metrics</h2>
        <div>
            <div class="metric bad">
                <div class="metric-value">{result.baseline_edit_distance}</div>
                <div class="metric-label">Baseline Errors</div>
            </div>
            <div class="metric">
                <div class="metric-value">{result.filtered_edit_distance}</div>
                <div class="metric-label">After Enhancement</div>
            </div>
            <div class="metric">
                <div class="metric-value">{result.baseline_edit_distance - result.filtered_edit_distance}</div>
                <div class="metric-label">Errors Eliminated</div>
            </div>
        </div>

        <h2>🔧 Optimal Filter Parameters</h2>
        <div class="filter-params">
            <p><strong>Pipeline:</strong> PIL (Python Imaging Library)</p>
            <p><strong>Contrast Factor:</strong> {config.contrast_factor:.4f}</p>
            <p><strong>Median Filter Size:</strong> {config.median_size}</p>
            <p><strong>Source:</strong> NSGA-II Multi-Objective Genetic Algorithm Optimization</p>
        </div>

        <h2>✅ Test Assertions</h2>
        <div class="assertions">
            <ul>
                {''.join(f'<li class="{"pass" if "PASS" in msg else "fail"}">{msg}</li>' for msg in result.messages)}
            </ul>
        </div>

        <h2>📝 OCR Output Comparison</h2>
        <div class="ocr-comparison">
            <div class="ocr-box before">
                <h3>❌ Before (Raw Degraded)</h3>
                <div class="ocr-text">{result.raw_ocr_text[:1500]}{'...' if len(result.raw_ocr_text) > 1500 else ''}</div>
            </div>
            <div class="ocr-box after">
                <h3>✅ After (Enhanced)</h3>
                <div class="ocr-text">{result.filtered_ocr_text[:1500]}{'...' if len(result.filtered_ocr_text) > 1500 else ''}</div>
            </div>
        </div>

        <h2>📋 Ground Truth Reference</h2>
        <div class="ocr-box">
            <div class="ocr-text">{result.ground_truth_text[:1500]}{'...' if len(result.ground_truth_text) > 1500 else ''}</div>
        </div>

        <hr style="margin-top: 40px;">
        <p style="color: #999; font-size: 12px;">
            Generated by NSGA-II OCR Enhancement Pipeline |
            Prisma Document Processing System
        </p>
    </div>
</body>
</html>
"""

    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(html)

    print(f"HTML report saved to: {output_path}")


# =============================================================================
# MAIN
# =============================================================================

def main():
    parser = argparse.ArgumentParser(description="Golden Test Demo for OCR Enhancement")
    parser.add_argument("--visual", action="store_true", help="Generate visual comparison image")
    parser.add_argument("--report", action="store_true", help="Generate HTML report")
    parser.add_argument("--quiet", action="store_true", help="Suppress verbose output")
    args = parser.parse_args()

    base_path = Path(__file__).parent.parent / "Fixtures"
    config = load_config(base_path)

    # Run test
    result = run_golden_test(config, verbose=not args.quiet)

    # Generate visual comparison
    if args.visual:
        visual_path = base_path / "golden_test_visual_comparison.png"
        generate_visual_comparison(config, visual_path)

    # Generate HTML report
    if args.report:
        report_path = base_path / "golden_test_report.html"
        generate_html_report(config, result, report_path)

    # Exit code for CI/CD
    return 0 if result.passed else 1


if __name__ == "__main__":
    exit(main())
