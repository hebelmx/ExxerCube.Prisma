#!/usr/bin/env python3
"""
Degradation Spectrum Generator v3

IMPROVEMENTS:
- Per-document blur calibration (documents respond differently)
- Localized scan lines (10-30% coverage, not full)
- Random scan line application (50% of images get them)
- No full-coverage scan lines (they were destroying OCR)

Target confidence bands:
- D80: 75-84% (light degradation)
- D70: 65-74% (moderate degradation)
- D60: 55-64% (heavy degradation)
"""

import subprocess
import json
import io
import random
from pathlib import Path
from dataclasses import dataclass, asdict
from typing import Optional

import numpy as np
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw
import cv2


# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
INPUT_DIR = BASE_PATH / "PRP1"
OUTPUT_DIR = BASE_PATH / "PRP1_Degraded_v3"

# Documents with their blur sensitivity calibration
# blur_for_70 = blur level that drops them to ~70% confidence
DOCUMENTS = {
    "222AAA": {
        "filename": "222AAA-44444444442025_page-1.png",
        "blur_for_70": 4.0,    # Very resistant to blur
        "baseline_conf": 85.0,
    },
    "333BBB": {
        "filename": "333BBB-44444444442025_page1.png",
        "blur_for_70": 1.8,    # Sensitive to blur
        "baseline_conf": 89.8,
    },
    "333ccc": {
        "filename": "333ccc-6666666662025_page1.png",
        "blur_for_70": 1.5,    # Very sensitive
        "baseline_conf": 84.0,
    },
    "555CCC": {
        "filename": "555CCC-66666662025_page-0001.png",
        "blur_for_70": 2.5,    # Adjusted - very resistant to blur
        "baseline_conf": 95.0,
    },
}

CONFIDENCE_BANDS = ["D80", "D70", "D60"]
RANDOM_SEED = 42


# ============================================================================
# Degradation Functions
# ============================================================================

def add_localized_scan_lines(img, coverage=0.2, band_height=50, intensity=0.15):
    """
    Add scan lines to random bands of the image (not full coverage).

    coverage: fraction of image height affected (0.2 = 20%)
    band_height: height of each affected band
    intensity: darkness of scan lines (0.1-0.2 realistic)
    """
    img = img.copy()
    draw = ImageDraw.Draw(img)
    w, h = img.size

    # Calculate how many bands
    num_bands = max(1, int((h * coverage) / band_height))

    # Pick random y positions for bands
    possible_positions = list(range(0, h - band_height, band_height // 2))
    if len(possible_positions) > num_bands:
        band_starts = random.sample(possible_positions, num_bands)
    else:
        band_starts = possible_positions[:num_bands]

    # Draw scan lines only in the bands
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


def get_degradation_params(doc_info: dict, band: str, has_scan_lines: bool) -> dict:
    """
    Get degradation parameters for a document and band.

    Calibrated based on document's blur sensitivity.
    """
    blur_70 = doc_info["blur_for_70"]

    # Scale blur based on target band
    # D80 = 70% of blur_70, D70 = 100% of blur_70, D60 = 115% of blur_70
    # (D60 was too aggressive at 1.4, reduced to 1.15)
    blur_scale = {
        "D80": 0.7,
        "D70": 1.0,
        "D60": 1.15,
    }

    blur = blur_70 * blur_scale[band]

    # Other params scale similarly
    params = {
        "D80": {"contrast": 0.90, "jpeg": 75, "noise": 0.01},
        "D70": {"contrast": 0.85, "jpeg": 65, "noise": 0.015},
        "D60": {"contrast": 0.80, "jpeg": 55, "noise": 0.02},
    }

    result = {
        "blur": blur,
        "contrast": params[band]["contrast"],
        "jpeg_quality": params[band]["jpeg"],
        "noise": params[band]["noise"],
        "scan_lines": has_scan_lines,
        "scan_coverage": random.uniform(0.15, 0.25) if has_scan_lines else 0,
        "scan_intensity": random.uniform(0.12, 0.18) if has_scan_lines else 0,
    }

    return result


def apply_degradation(image: Image.Image, params: dict, seed: int) -> Image.Image:
    """Apply degradation with given parameters."""
    random.seed(seed)
    np.random.seed(seed)

    degraded = image.copy()
    if degraded.mode != 'RGB':
        degraded = degraded.convert('RGB')

    # 1. Gaussian Blur
    if params["blur"] > 0:
        degraded = degraded.filter(ImageFilter.GaussianBlur(radius=params["blur"]))

    # 2. Light Gaussian Noise
    if params["noise"] > 0:
        degraded = add_gaussian_noise(degraded, params["noise"])

    # 3. Contrast reduction
    if params["contrast"] != 1.0:
        degraded = ImageEnhance.Contrast(degraded).enhance(params["contrast"])

    # 4. Localized scan lines (randomly applied)
    if params["scan_lines"] and params["scan_coverage"] > 0:
        degraded = add_localized_scan_lines(
            degraded,
            coverage=params["scan_coverage"],
            intensity=params["scan_intensity"]
        )

    # 5. JPEG compression (last)
    if params["jpeg_quality"] < 95:
        degraded = apply_jpeg_compression(degraded, params["jpeg_quality"])

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
    print("DEGRADATION SPECTRUM GENERATOR v3")
    print("=" * 70)
    print()
    print("Features:")
    print("  - Per-document blur calibration")
    print("  - Localized scan lines (10-25% coverage)")
    print("  - Random scan line application (~50% of images)")
    print()
    print(f"Input: {INPUT_DIR}")
    print(f"Output: {OUTPUT_DIR}")
    print(f"Documents: {len(DOCUMENTS)}")
    print(f"Bands: {CONFIDENCE_BANDS}")
    print(f"Total images: {len(DOCUMENTS) * len(CONFIDENCE_BANDS)}")
    print()

    # Create output directories
    for band in CONFIDENCE_BANDS:
        (OUTPUT_DIR / band).mkdir(parents=True, exist_ok=True)

    results = {}

    # Process each document
    for doc_id, doc_info in DOCUMENTS.items():
        filename = doc_info["filename"]
        input_path = INPUT_DIR / filename

        if not input_path.exists():
            print(f"WARNING: {filename} not found!")
            continue

        print(f"\n{'='*60}")
        print(f"Processing: {doc_id}")
        print(f"  Blur calibration: blur_for_70 = {doc_info['blur_for_70']}")
        print(f"{'='*60}")

        original = Image.open(input_path)
        if original.mode != 'RGB':
            original = original.convert('RGB')

        results[doc_id] = {
            "filename": filename,
            "baseline_conf": doc_info["baseline_conf"],
            "blur_for_70": doc_info["blur_for_70"],
            "degradations": {}
        }

        for band in CONFIDENCE_BANDS:
            # Randomly decide if this image gets scan lines (~50%)
            has_scan_lines = random.random() < 0.5

            # Get calibrated params
            params = get_degradation_params(doc_info, band, has_scan_lines)

            # Apply degradation
            seed = hash(f"{doc_id}_{band}") % (2**32)
            degraded = apply_degradation(original, params, seed)

            # Save
            output_path = OUTPUT_DIR / band / filename
            if output_path.suffix.lower() in ['.jpg', '.jpeg']:
                output_path = output_path.with_suffix('.png')
            degraded.save(output_path, 'PNG')

            # OCR
            ocr_result = run_tesseract_with_confidence(output_path)
            img_props = analyze_image_properties(output_path)

            results[doc_id]["degradations"][band] = {
                "params": params,
                "output_path": str(output_path),
                "ocr": ocr_result,
                "image_properties": img_props,
            }

            scan_str = "📊" if has_scan_lines else "  "
            conf = ocr_result['mean_confidence']
            print(f"  {band} {scan_str}: {conf:.1f}% (blur={params['blur']:.2f})")

    # Save results
    results_path = OUTPUT_DIR / "degradation_results_v3.json"
    with open(results_path, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    # Summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    print()
    print(f"{'Document':<10} {'Baseline':<10} ", end="")
    for band in CONFIDENCE_BANDS:
        print(f"{band:<10} ", end="")
    print()
    print("-" * 50)

    for doc_id, data in results.items():
        baseline = data['baseline_conf']
        print(f"{doc_id:<10} {baseline:<10.1f} ", end="")
        for band in CONFIDENCE_BANDS:
            if band in data['degradations']:
                conf = data['degradations'][band]['ocr']['mean_confidence']
                print(f"{conf:<10.1f} ", end="")
        print()

    print()
    print(f"Results saved to: {results_path}")
    print("=" * 70)

    return results


if __name__ == "__main__":
    main()
