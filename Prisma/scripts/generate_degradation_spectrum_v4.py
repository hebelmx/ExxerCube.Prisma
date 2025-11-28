#!/usr/bin/env python3
"""
Degradation Spectrum Generator v4

GOAL: Create ~40-50 degraded documents spanning 50% to ~90% Tesseract confidence

APPROACH:
- 4 source documents × multiple degradation levels
- More granular blur steps (not just 3 bands)
- Some with scan lines, some without
- Target: uniform distribution across 50-90% confidence range

After generation:
1. Cluster by IMAGE PROPERTIES (blur_score, contrast, noise)
2. Optimize filters per cluster using edit distance
"""

import subprocess
import json
import io
import random
from pathlib import Path
from dataclasses import dataclass, asdict
from typing import Optional, List, Dict

import numpy as np
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw
import cv2


# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
INPUT_DIR = BASE_PATH / "PRP1"
OUTPUT_DIR = BASE_PATH / "PRP1_Degraded_v4"

# Documents with their calibration data
DOCUMENTS = {
    "222AAA": {
        "filename": "222AAA-44444444442025_page-1.png",
        "blur_sensitivity": 0.25,  # % confidence drop per unit blur
        "baseline_conf": 85.0,
    },
    "333BBB": {
        "filename": "333BBB-44444444442025_page1.png",
        "blur_sensitivity": 0.55,  # More sensitive
        "baseline_conf": 89.8,
    },
    "333ccc": {
        "filename": "333ccc-6666666662025_page1.png",
        "blur_sensitivity": 0.60,  # Very sensitive
        "baseline_conf": 84.0,
    },
    "555CCC": {
        "filename": "555CCC-66666662025_page-0001.png",
        "blur_sensitivity": 0.50,  # Medium
        "baseline_conf": 95.0,
    },
}

# Generate multiple degradation levels per document
# Each level is a combination of blur + optional scan lines
DEGRADATION_VARIANTS = [
    # (blur_multiplier, has_scan_lines, scan_coverage)
    # Light degradation (targeting 80-90%)
    (0.5, False, 0),
    (0.7, False, 0),
    (0.9, False, 0),
    (0.5, True, 0.10),

    # Medium degradation (targeting 70-80%)
    (1.0, False, 0),
    (1.2, False, 0),
    (1.4, False, 0),
    (1.0, True, 0.15),
    (1.2, True, 0.12),

    # Heavy degradation (targeting 55-70%)
    (1.6, False, 0),
    (1.8, False, 0),
    (2.0, False, 0),
    (1.6, True, 0.18),
    (1.8, True, 0.15),
]

RANDOM_SEED = 42


# ============================================================================
# Degradation Functions
# ============================================================================

def add_localized_scan_lines(img, coverage=0.2, band_height=50, intensity=0.15):
    """Add scan lines to random bands of the image."""
    img = img.copy()
    draw = ImageDraw.Draw(img)
    w, h = img.size

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
    """Apply JPEG compression artifacts."""
    if quality >= 95:
        return image

    buffer = io.BytesIO()
    image.save(buffer, format='JPEG', quality=quality)
    buffer.seek(0)
    return Image.open(buffer).convert('RGB')


def calculate_blur_for_target(doc_info: dict, target_conf: float) -> float:
    """Calculate blur needed to reach target confidence."""
    baseline = doc_info["baseline_conf"]
    sensitivity = doc_info["blur_sensitivity"]

    conf_drop = baseline - target_conf
    blur = conf_drop / (sensitivity * 10)  # Scale factor
    return max(0, blur)


def apply_degradation(image: Image.Image, blur: float, has_scan_lines: bool,
                      scan_coverage: float, seed: int) -> Image.Image:
    """Apply degradation with given parameters."""
    random.seed(seed)
    np.random.seed(seed)

    degraded = image.copy()
    if degraded.mode != 'RGB':
        degraded = degraded.convert('RGB')

    # 1. Gaussian Blur (primary degradation)
    if blur > 0:
        degraded = degraded.filter(ImageFilter.GaussianBlur(radius=blur))

    # 2. Light noise (scales with blur)
    noise_intensity = min(0.02, blur * 0.005)
    if noise_intensity > 0:
        degraded = add_gaussian_noise(degraded, noise_intensity)

    # 3. Slight contrast reduction (scales with blur)
    contrast_factor = max(0.85, 1.0 - blur * 0.03)
    if contrast_factor < 1.0:
        degraded = ImageEnhance.Contrast(degraded).enhance(contrast_factor)

    # 4. Localized scan lines
    if has_scan_lines and scan_coverage > 0:
        degraded = add_localized_scan_lines(
            degraded,
            coverage=scan_coverage,
            intensity=random.uniform(0.12, 0.18)
        )

    # 5. JPEG compression (scales with blur)
    jpeg_quality = max(55, 85 - int(blur * 8))
    if jpeg_quality < 95:
        degraded = apply_jpeg_compression(degraded, jpeg_quality)

    return degraded


# ============================================================================
# OCR Functions
# ============================================================================

def run_tesseract_with_confidence(image_path: Path) -> dict:
    """Run Tesseract and get text + confidence."""
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
    print("DEGRADATION SPECTRUM GENERATOR v4")
    print("=" * 70)
    print()
    print("Goal: Create 40-50 documents spanning 50%-90% confidence")
    print("Method: Multiple blur levels × scan line variants")
    print()
    print(f"Input: {INPUT_DIR}")
    print(f"Output: {OUTPUT_DIR}")
    print(f"Documents: {len(DOCUMENTS)}")
    print(f"Variants per doc: {len(DEGRADATION_VARIANTS)}")
    print(f"Total images: {len(DOCUMENTS) * len(DEGRADATION_VARIANTS)}")
    print()

    # Create output directory
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    results = []
    confidence_distribution = {"50s": 0, "60s": 0, "70s": 0, "80s": 0, "90s": 0}

    # Process each document
    for doc_id, doc_info in DOCUMENTS.items():
        filename = doc_info["filename"]
        input_path = INPUT_DIR / filename

        if not input_path.exists():
            print(f"WARNING: {filename} not found!")
            continue

        print(f"\n{'='*60}")
        print(f"Processing: {doc_id}")
        print(f"  Baseline: {doc_info['baseline_conf']}%")
        print(f"  Sensitivity: {doc_info['blur_sensitivity']}")
        print(f"{'='*60}")

        original = Image.open(input_path)
        if original.mode != 'RGB':
            original = original.convert('RGB')

        # Base blur to reach ~70% (calibration reference)
        base_blur = calculate_blur_for_target(doc_info, 70.0)
        print(f"  Base blur for 70%: {base_blur:.2f}")

        for var_idx, (blur_mult, has_scan, scan_cov) in enumerate(DEGRADATION_VARIANTS):
            # Calculate actual blur
            blur = base_blur * blur_mult

            # Generate unique filename
            scan_suffix = f"_scan{int(scan_cov*100)}" if has_scan else ""
            output_filename = f"{doc_id}_v{var_idx:02d}_blur{blur:.1f}{scan_suffix}.png"
            output_path = OUTPUT_DIR / output_filename

            # Apply degradation
            seed = hash(f"{doc_id}_{var_idx}") % (2**32)
            degraded = apply_degradation(original, blur, has_scan, scan_cov, seed)

            # Save
            degraded.save(output_path, 'PNG')

            # OCR
            ocr_result = run_tesseract_with_confidence(output_path)
            img_props = analyze_image_properties(output_path)

            conf = ocr_result['mean_confidence']

            # Track distribution
            if conf >= 90:
                confidence_distribution["90s"] += 1
            elif conf >= 80:
                confidence_distribution["80s"] += 1
            elif conf >= 70:
                confidence_distribution["70s"] += 1
            elif conf >= 60:
                confidence_distribution["60s"] += 1
            else:
                confidence_distribution["50s"] += 1

            results.append({
                "doc_id": doc_id,
                "variant_idx": var_idx,
                "filename": output_filename,
                "blur": round(blur, 2),
                "has_scan_lines": has_scan,
                "scan_coverage": scan_cov,
                "ocr": ocr_result,
                "image_properties": img_props,
            })

            scan_str = "📊" if has_scan else "  "
            print(f"  v{var_idx:02d} {scan_str}: {conf:5.1f}% (blur={blur:.2f})")

    # Save results
    results_path = OUTPUT_DIR / "degradation_results_v4.json"
    with open(results_path, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    # Summary
    print("\n" + "=" * 70)
    print("CONFIDENCE DISTRIBUTION")
    print("=" * 70)
    print()

    total = sum(confidence_distribution.values())
    for band, count in sorted(confidence_distribution.items()):
        pct = (count / total * 100) if total > 0 else 0
        bar = "█" * int(pct / 2)
        print(f"  {band}: {count:3d} ({pct:5.1f}%) {bar}")

    print()
    print(f"Total images: {total}")
    print(f"Results saved to: {results_path}")
    print()

    # Show target ranges
    in_range = confidence_distribution["50s"] + confidence_distribution["60s"] + \
               confidence_distribution["70s"] + confidence_distribution["80s"]
    print(f"In target range (50-89%): {in_range}/{total} ({in_range/total*100:.1f}%)")
    print("=" * 70)

    return results


if __name__ == "__main__":
    main()
