#!/usr/bin/env python3
"""
Degradation Spectrum Generator v5

APPROACH: Step-by-step scan with measurements
- Scan blur from 0.5 to 5.0 in small steps
- Measure OCR confidence at each step
- Keep only variants in target ranges (55%, 65%, 75%, 85%)
- Also try scan line variants for each blur level

Goal: ~40-50 documents covering 55%-85% range uniformly
"""

import subprocess
import json
import io
import random
from pathlib import Path
from dataclasses import dataclass
from typing import Optional, List, Dict, Tuple

import numpy as np
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw
import cv2


# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
INPUT_DIR = BASE_PATH / "PRP1"
OUTPUT_DIR = BASE_PATH / "PRP1_Degraded_v5"

DOCUMENTS = {
    "222AAA": "222AAA-44444444442025_page-1.png",
    "333BBB": "333BBB-44444444442025_page1.png",
    "333ccc": "333ccc-6666666662025_page1.png",
    "555CCC": "555CCC-66666662025_page-0001.png",
}

# Target confidence levels (we want ~10-12 images per target)
TARGET_CONFIDENCES = [55, 65, 75, 85]
TOLERANCE = 5  # Accept confidence within ±5%

# Blur scan range
BLUR_MIN = 0.3
BLUR_MAX = 5.0
BLUR_STEP = 0.3

RANDOM_SEED = 42


# ============================================================================
# Degradation Functions
# ============================================================================

def add_localized_scan_lines(img, coverage=0.15, intensity=0.15):
    """Add scan lines to random bands."""
    img = img.copy()
    draw = ImageDraw.Draw(img)
    w, h = img.size
    band_height = 50

    num_bands = max(1, int((h * coverage) / band_height))
    possible_positions = list(range(0, h - band_height, band_height // 2))

    if len(possible_positions) > num_bands:
        band_starts = random.sample(possible_positions, num_bands)
    else:
        band_starts = possible_positions[:num_bands]

    for band_y in band_starts:
        spacing = random.randint(8, 15)
        for y in range(band_y, min(band_y + band_height, h), spacing):
            opacity = int(255 - intensity * 80)
            opacity = max(180, min(250, opacity + random.randint(-10, 10)))
            draw.line([(0, y), (w, y)], fill=(opacity, opacity, opacity), width=1)

    return img


def add_gaussian_noise(image: Image.Image, intensity: float) -> Image.Image:
    """Add Gaussian noise."""
    if intensity <= 0:
        return image
    img_array = np.array(image, dtype=np.float32)
    noise = np.random.normal(0, intensity * 255, img_array.shape)
    noisy_img = np.clip(img_array + noise, 0, 255).astype(np.uint8)
    return Image.fromarray(noisy_img)


def apply_jpeg_compression(image: Image.Image, quality: int) -> Image.Image:
    """Apply JPEG compression."""
    if quality >= 95:
        return image
    buffer = io.BytesIO()
    image.save(buffer, format='JPEG', quality=quality)
    buffer.seek(0)
    return Image.open(buffer).convert('RGB')


def apply_blur_degradation(image: Image.Image, blur: float, seed: int) -> Image.Image:
    """Apply blur-based degradation (no scan lines)."""
    random.seed(seed)
    np.random.seed(seed)

    degraded = image.copy()
    if degraded.mode != 'RGB':
        degraded = degraded.convert('RGB')

    # Gaussian blur
    if blur > 0:
        degraded = degraded.filter(ImageFilter.GaussianBlur(radius=blur))

    # Light noise (scales with blur)
    noise = min(0.015, blur * 0.003)
    if noise > 0:
        degraded = add_gaussian_noise(degraded, noise)

    # Slight contrast reduction
    contrast = max(0.88, 1.0 - blur * 0.02)
    if contrast < 1.0:
        degraded = ImageEnhance.Contrast(degraded).enhance(contrast)

    # JPEG compression
    jpeg_q = max(60, 80 - int(blur * 5))
    degraded = apply_jpeg_compression(degraded, jpeg_q)

    return degraded


def apply_scan_degradation(image: Image.Image, blur: float, scan_coverage: float, seed: int) -> Image.Image:
    """Apply blur + scan line degradation."""
    random.seed(seed)
    np.random.seed(seed)

    degraded = apply_blur_degradation(image, blur, seed)

    if scan_coverage > 0:
        degraded = add_localized_scan_lines(degraded, coverage=scan_coverage, intensity=0.15)

    return degraded


# ============================================================================
# OCR Functions
# ============================================================================

def run_tesseract_quick(image: Image.Image) -> float:
    """Run Tesseract on PIL image and return confidence (for scanning)."""
    import tempfile
    with tempfile.NamedTemporaryFile(suffix='.png', delete=True) as tmp:
        image.save(tmp.name, 'PNG')

        result_tsv = subprocess.run(
            ["tesseract", tmp.name, "stdout", "-l", "spa", "--psm", "6", "tsv"],
            capture_output=True, text=True, timeout=60
        )

        confidences = []
        for line in result_tsv.stdout.strip().split('\n')[1:]:
            parts = line.split('\t')
            if len(parts) >= 12:
                try:
                    conf = float(parts[10])
                    if conf > 0:
                        confidences.append(conf)
                except ValueError:
                    pass

        return sum(confidences) / len(confidences) if confidences else 0


def run_tesseract_full(image_path: Path) -> dict:
    """Run Tesseract and get full results."""
    result_text = subprocess.run(
        ["tesseract", str(image_path), "stdout", "-l", "spa", "--psm", "6"],
        capture_output=True, text=True, timeout=60
    )
    ocr_text = result_text.stdout

    result_tsv = subprocess.run(
        ["tesseract", str(image_path), "stdout", "-l", "spa", "--psm", "6", "tsv"],
        capture_output=True, text=True, timeout=60
    )

    confidences = []
    for line in result_tsv.stdout.strip().split('\n')[1:]:
        parts = line.split('\t')
        if len(parts) >= 12:
            try:
                conf = float(parts[10])
                if conf > 0:
                    confidences.append(conf)
            except ValueError:
                pass

    mean_conf = sum(confidences) / len(confidences) if confidences else 0

    return {
        "text": ocr_text,
        "mean_confidence": round(mean_conf, 2),
        "word_count": len(confidences),
        "char_count": len(ocr_text),
    }


def analyze_image_properties(image_path: Path) -> dict:
    """Analyze image properties for clustering."""
    img = cv2.imread(str(image_path), cv2.IMREAD_GRAYSCALE)
    if img is None:
        return {}

    blur_score = cv2.Laplacian(img, cv2.CV_64F).var()
    noise_estimate = np.std(cv2.Laplacian(img, cv2.CV_64F))
    contrast = np.std(img)
    brightness = np.mean(img)

    edges = cv2.Canny(img, 50, 150)
    edge_density = np.sum(edges > 0) / edges.size

    return {
        "blur_score": round(blur_score, 2),
        "noise_estimate": round(noise_estimate, 2),
        "contrast": round(contrast, 2),
        "brightness": round(brightness, 2),
        "edge_density": round(edge_density * 100, 4),
    }


# ============================================================================
# Main
# ============================================================================

def main():
    random.seed(RANDOM_SEED)
    np.random.seed(RANDOM_SEED)

    print("=" * 70)
    print("DEGRADATION SPECTRUM GENERATOR v5")
    print("=" * 70)
    print()
    print("Method: Step-by-step blur scan with measurements")
    print(f"Blur range: {BLUR_MIN} to {BLUR_MAX}, step {BLUR_STEP}")
    print(f"Target confidences: {TARGET_CONFIDENCES} (±{TOLERANCE}%)")
    print()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    all_results = []
    kept_images = []

    # For each target confidence, track how many we've found
    found_per_target = {t: 0 for t in TARGET_CONFIDENCES}
    MAX_PER_TARGET = 12

    # Process each document
    for doc_id, filename in DOCUMENTS.items():
        input_path = INPUT_DIR / filename

        if not input_path.exists():
            print(f"WARNING: {filename} not found!")
            continue

        print(f"\n{'='*60}")
        print(f"SCANNING: {doc_id}")
        print(f"{'='*60}")

        original = Image.open(input_path)
        if original.mode != 'RGB':
            original = original.convert('RGB')

        # Get baseline confidence
        baseline_conf = run_tesseract_quick(original)
        print(f"Baseline confidence: {baseline_conf:.1f}%")
        print()

        # Scan blur levels
        blur_levels = np.arange(BLUR_MIN, BLUR_MAX + 0.01, BLUR_STEP)
        print(f"{'Blur':>6} | {'Conf':>6} | {'Target':>8} | Status")
        print("-" * 45)

        for blur in blur_levels:
            seed = hash(f"{doc_id}_{blur}") % (2**32)

            # Test without scan lines
            degraded = apply_blur_degradation(original, blur, seed)
            conf = run_tesseract_quick(degraded)

            # Check if matches any target
            matched_target = None
            for target in TARGET_CONFIDENCES:
                if target - TOLERANCE <= conf <= target + TOLERANCE:
                    if found_per_target[target] < MAX_PER_TARGET:
                        matched_target = target
                        break

            if matched_target:
                status = f"✓ KEEP (target {matched_target}%)"
                found_per_target[matched_target] += 1

                # Save this image
                output_name = f"{doc_id}_blur{blur:.1f}_t{matched_target}.png"
                output_path = OUTPUT_DIR / output_name
                degraded.save(output_path, 'PNG')

                kept_images.append({
                    "doc_id": doc_id,
                    "filename": output_name,
                    "blur": round(blur, 2),
                    "has_scan_lines": False,
                    "target_conf": matched_target,
                    "actual_conf": round(conf, 1),
                })
            else:
                status = ""

            print(f"{blur:6.2f} | {conf:5.1f}% | {matched_target or '-':>8} | {status}")

            # Also test with scan lines (10% coverage)
            seed_scan = hash(f"{doc_id}_{blur}_scan") % (2**32)
            degraded_scan = apply_scan_degradation(original, blur, 0.12, seed_scan)
            conf_scan = run_tesseract_quick(degraded_scan)

            matched_target_scan = None
            for target in TARGET_CONFIDENCES:
                if target - TOLERANCE <= conf_scan <= target + TOLERANCE:
                    if found_per_target[target] < MAX_PER_TARGET:
                        matched_target_scan = target
                        break

            if matched_target_scan:
                found_per_target[matched_target_scan] += 1

                output_name = f"{doc_id}_blur{blur:.1f}_scan_t{matched_target_scan}.png"
                output_path = OUTPUT_DIR / output_name
                degraded_scan.save(output_path, 'PNG')

                kept_images.append({
                    "doc_id": doc_id,
                    "filename": output_name,
                    "blur": round(blur, 2),
                    "has_scan_lines": True,
                    "target_conf": matched_target_scan,
                    "actual_conf": round(conf_scan, 1),
                })

                print(f"{'':>6} | {conf_scan:5.1f}% | {matched_target_scan:>8} | ✓ KEEP (scan)")

        print()
        print(f"Progress: {dict(found_per_target)}")

    # Get full OCR results for kept images
    print("\n" + "=" * 70)
    print("FINAL PROCESSING: Getting full OCR results")
    print("=" * 70)

    final_results = []
    for img_info in kept_images:
        output_path = OUTPUT_DIR / img_info["filename"]
        print(f"  Processing {img_info['filename']}...")

        ocr_result = run_tesseract_full(output_path)
        img_props = analyze_image_properties(output_path)

        final_results.append({
            **img_info,
            "ocr": ocr_result,
            "image_properties": img_props,
        })

    # Save results
    results_path = OUTPUT_DIR / "degradation_results_v5.json"
    with open(results_path, 'w', encoding='utf-8') as f:
        json.dump(final_results, f, indent=2, ensure_ascii=False)

    # Summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    print()

    print(f"Total kept images: {len(kept_images)}")
    print()
    print("Distribution by target:")
    for target in TARGET_CONFIDENCES:
        count = sum(1 for img in kept_images if img["target_conf"] == target)
        bar = "█" * count
        print(f"  {target}%: {count:2d} {bar}")

    print()
    print("Distribution by document:")
    for doc_id in DOCUMENTS:
        count = sum(1 for img in kept_images if img["doc_id"] == doc_id)
        print(f"  {doc_id}: {count}")

    print()
    print(f"Results saved to: {results_path}")
    print("=" * 70)

    return final_results


if __name__ == "__main__":
    main()
