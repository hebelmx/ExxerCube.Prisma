#!/usr/bin/env python3
"""
Degradation Spectrum Generator v2

Generates degraded versions of pristine documents targeting OCR confidence bands:
- D80: 75-84% confidence (light degradation)
- D70: 65-74% confidence (moderate degradation)
- D60: 55-64% confidence (heavy degradation)
- D50: 50-54% confidence (rescuable limit)

Each band has 2 variants:
- Scanner-like: blur + scan lines + JPEG compression
- Handling-like: noise + rotation + contrast loss

555CCC is treated as an outlier (different baseline properties) and may need
different degradation intensities to reach the same confidence targets.
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
OUTPUT_DIR = BASE_PATH / "PRP1_Degraded_v2"

DOCUMENTS = {
    "222AAA": "222AAA-44444444442025_page-1.png",  # Converted from PDF to PNG
    "333BBB": "333BBB-44444444442025_page1.png",
    "333ccc": "333ccc-6666666662025_page1.png",
    "555CCC": "555CCC-66666662025_page-0001.png",  # OUTLIER - different properties
}

# 555CCC has higher baseline (95% vs 84-90%), needs stronger degradation
# to reach same confidence bands
OUTLIER_DOCS = ["555CCC"]

CONFIDENCE_BANDS = ["D80", "D70", "D60", "D50"]
VARIANTS = ["scanner", "handling"]


# ============================================================================
# Degradation Profiles
# ============================================================================

@dataclass
class DegradationProfile:
    """Configuration for a specific degradation."""
    name: str
    band: str
    variant: str

    # Blur (scanner focus issues)
    blur_radius: float = 0.0

    # Noise
    gaussian_noise: float = 0.0      # 0-1 intensity
    salt_pepper: float = 0.0         # 0-1 amount

    # Geometric
    rotation_angle: float = 0.0      # degrees

    # Tonal
    contrast_factor: float = 1.0     # <1 reduces contrast
    brightness_factor: float = 1.0   # <1 darkens

    # Compression
    jpeg_quality: int = 95           # 0-100

    # Scanner artifacts
    scan_lines: bool = False
    scan_line_intensity: float = 0.0  # 0-1


def get_degradation_profiles() -> dict:
    """
    Create degradation profiles for each band × variant combination.

    Profiles are calibrated to achieve target confidence bands.
    555CCC (outlier) uses stronger degradation due to higher baseline.
    """
    profiles = {}

    # ========== NORMAL DOCUMENTS (222AAA, 333BBB, 333ccc) ==========
    # Baseline: 84-90% confidence

    # D80 (target: 75-84%) - Light degradation
    profiles[("normal", "D80", "scanner")] = DegradationProfile(
        name="D80_scanner_normal",
        band="D80",
        variant="scanner",
        blur_radius=0.8,
        jpeg_quality=75,
        scan_lines=True,
        scan_line_intensity=0.1,
    )
    profiles[("normal", "D80", "handling")] = DegradationProfile(
        name="D80_handling_normal",
        band="D80",
        variant="handling",
        gaussian_noise=0.02,
        rotation_angle=0.8,
        contrast_factor=0.92,
    )

    # D70 (target: 65-74%) - Moderate degradation
    profiles[("normal", "D70", "scanner")] = DegradationProfile(
        name="D70_scanner_normal",
        band="D70",
        variant="scanner",
        blur_radius=1.2,
        jpeg_quality=60,
        scan_lines=True,
        scan_line_intensity=0.15,
    )
    profiles[("normal", "D70", "handling")] = DegradationProfile(
        name="D70_handling_normal",
        band="D70",
        variant="handling",
        gaussian_noise=0.04,
        salt_pepper=0.001,
        rotation_angle=1.5,
        contrast_factor=0.85,
        brightness_factor=0.95,
    )

    # D60 (target: 55-64%) - Heavy degradation
    profiles[("normal", "D60", "scanner")] = DegradationProfile(
        name="D60_scanner_normal",
        band="D60",
        variant="scanner",
        blur_radius=1.8,
        jpeg_quality=45,
        scan_lines=True,
        scan_line_intensity=0.2,
    )
    profiles[("normal", "D60", "handling")] = DegradationProfile(
        name="D60_handling_normal",
        band="D60",
        variant="handling",
        gaussian_noise=0.07,
        salt_pepper=0.002,
        rotation_angle=2.0,
        contrast_factor=0.78,
        brightness_factor=0.90,
    )

    # D50 (target: 50-54%) - Rescuable limit
    profiles[("normal", "D50", "scanner")] = DegradationProfile(
        name="D50_scanner_normal",
        band="D50",
        variant="scanner",
        blur_radius=2.2,
        jpeg_quality=35,
        scan_lines=True,
        scan_line_intensity=0.25,
    )
    profiles[("normal", "D50", "handling")] = DegradationProfile(
        name="D50_handling_normal",
        band="D50",
        variant="handling",
        gaussian_noise=0.10,
        salt_pepper=0.003,
        rotation_angle=2.5,
        contrast_factor=0.70,
        brightness_factor=0.85,
    )

    # ========== OUTLIER DOCUMENT (555CCC) ==========
    # Baseline: 95% confidence - needs STRONGER degradation

    # D80 (target: 75-84%) - Light degradation (but stronger than normal)
    profiles[("outlier", "D80", "scanner")] = DegradationProfile(
        name="D80_scanner_outlier",
        band="D80",
        variant="scanner",
        blur_radius=1.2,  # +50% vs normal
        jpeg_quality=65,  # -10 vs normal
        scan_lines=True,
        scan_line_intensity=0.15,
    )
    profiles[("outlier", "D80", "handling")] = DegradationProfile(
        name="D80_handling_outlier",
        band="D80",
        variant="handling",
        gaussian_noise=0.03,  # +50% vs normal
        rotation_angle=1.2,
        contrast_factor=0.88,  # stronger reduction
    )

    # D70 (target: 65-74%)
    profiles[("outlier", "D70", "scanner")] = DegradationProfile(
        name="D70_scanner_outlier",
        band="D70",
        variant="scanner",
        blur_radius=1.8,
        jpeg_quality=50,
        scan_lines=True,
        scan_line_intensity=0.2,
    )
    profiles[("outlier", "D70", "handling")] = DegradationProfile(
        name="D70_handling_outlier",
        band="D70",
        variant="handling",
        gaussian_noise=0.06,
        salt_pepper=0.0015,
        rotation_angle=2.0,
        contrast_factor=0.80,
        brightness_factor=0.92,
    )

    # D60 (target: 55-64%)
    profiles[("outlier", "D60", "scanner")] = DegradationProfile(
        name="D60_scanner_outlier",
        band="D60",
        variant="scanner",
        blur_radius=2.5,
        jpeg_quality=40,
        scan_lines=True,
        scan_line_intensity=0.25,
    )
    profiles[("outlier", "D60", "handling")] = DegradationProfile(
        name="D60_handling_outlier",
        band="D60",
        variant="handling",
        gaussian_noise=0.10,
        salt_pepper=0.003,
        rotation_angle=2.5,
        contrast_factor=0.72,
        brightness_factor=0.87,
    )

    # D50 (target: 50-54%)
    profiles[("outlier", "D50", "scanner")] = DegradationProfile(
        name="D50_scanner_outlier",
        band="D50",
        variant="scanner",
        blur_radius=3.0,
        jpeg_quality=30,
        scan_lines=True,
        scan_line_intensity=0.3,
    )
    profiles[("outlier", "D50", "handling")] = DegradationProfile(
        name="D50_handling_outlier",
        band="D50",
        variant="handling",
        gaussian_noise=0.13,
        salt_pepper=0.004,
        rotation_angle=3.0,
        contrast_factor=0.65,
        brightness_factor=0.82,
    )

    return profiles


# ============================================================================
# Degradation Functions
# ============================================================================

def add_gaussian_noise(image: Image.Image, intensity: float) -> Image.Image:
    """Add Gaussian noise to simulate sensor noise."""
    if intensity <= 0:
        return image

    img_array = np.array(image, dtype=np.float32)
    noise = np.random.normal(0, intensity * 255, img_array.shape)
    noisy_img = np.clip(img_array + noise, 0, 255).astype(np.uint8)

    return Image.fromarray(noisy_img)


def add_salt_pepper_noise(image: Image.Image, amount: float) -> Image.Image:
    """Add salt-and-pepper noise (random black/white pixels)."""
    if amount <= 0:
        return image

    img_array = np.array(image)

    # Salt (white pixels)
    num_salt = int(amount * img_array.size * 0.5)
    coords = [np.random.randint(0, i - 1, num_salt) for i in img_array.shape]
    img_array[coords[0], coords[1]] = 255

    # Pepper (black pixels)
    num_pepper = int(amount * img_array.size * 0.5)
    coords = [np.random.randint(0, i - 1, num_pepper) for i in img_array.shape]
    img_array[coords[0], coords[1]] = 0

    return Image.fromarray(img_array)


def add_scan_lines(image: Image.Image, intensity: float) -> Image.Image:
    """Add horizontal scan lines to simulate scanner artifacts."""
    if intensity <= 0:
        return image

    img = image.copy()
    draw = ImageDraw.Draw(img)
    width, height = img.size

    # Add subtle horizontal lines every 8-12 pixels
    for y in range(0, height, random.randint(8, 12)):
        # Variable opacity based on intensity
        opacity = int(255 - intensity * 60)  # 255 = invisible, lower = more visible
        opacity = max(180, min(250, opacity + random.randint(-10, 10)))
        draw.line([(0, y), (width, y)], fill=(opacity, opacity, opacity), width=1)

    return img


def apply_jpeg_compression(image: Image.Image, quality: int) -> Image.Image:
    """Apply JPEG compression artifacts."""
    if quality >= 95:
        return image

    buffer = io.BytesIO()
    image.save(buffer, format='JPEG', quality=quality)
    buffer.seek(0)

    return Image.open(buffer).convert('RGB')


def apply_degradation(image: Image.Image, profile: DegradationProfile, seed: int) -> Image.Image:
    """Apply all degradation effects according to profile."""
    random.seed(seed)
    np.random.seed(seed)

    degraded = image.copy()
    if degraded.mode != 'RGB':
        degraded = degraded.convert('RGB')

    # 1. Gaussian Blur
    if profile.blur_radius > 0:
        degraded = degraded.filter(ImageFilter.GaussianBlur(radius=profile.blur_radius))

    # 2. Gaussian Noise
    if profile.gaussian_noise > 0:
        degraded = add_gaussian_noise(degraded, profile.gaussian_noise)

    # 3. Salt-and-Pepper Noise
    if profile.salt_pepper > 0:
        degraded = add_salt_pepper_noise(degraded, profile.salt_pepper)

    # 4. Contrast Adjustment
    if profile.contrast_factor != 1.0:
        enhancer = ImageEnhance.Contrast(degraded)
        degraded = enhancer.enhance(profile.contrast_factor)

    # 5. Brightness Adjustment
    if profile.brightness_factor != 1.0:
        enhancer = ImageEnhance.Brightness(degraded)
        degraded = enhancer.enhance(profile.brightness_factor)

    # 6. Rotation
    if profile.rotation_angle != 0:
        # Random direction
        angle = profile.rotation_angle * random.choice([-1, 1])
        degraded = degraded.rotate(angle, resample=Image.BICUBIC, expand=False, fillcolor='white')

    # 7. Scan Lines
    if profile.scan_lines and profile.scan_line_intensity > 0:
        degraded = add_scan_lines(degraded, profile.scan_line_intensity)

    # 8. JPEG Compression (last step)
    if profile.jpeg_quality < 95:
        degraded = apply_jpeg_compression(degraded, profile.jpeg_quality)

    return degraded


# ============================================================================
# OCR Functions
# ============================================================================

def run_tesseract_with_confidence(image_path: Path) -> dict:
    """Run Tesseract and get text + confidence."""
    # Get OCR text
    result_text = subprocess.run(
        ["tesseract", str(image_path), "stdout", "-l", "spa", "--psm", "6"],
        capture_output=True, text=True, timeout=60
    )
    ocr_text = result_text.stdout

    # Get confidence via TSV output
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

    # Blur score (Laplacian variance)
    blur_score = cv2.Laplacian(img, cv2.CV_64F).var()

    # Noise estimation
    noise_estimate = np.std(cv2.Laplacian(img, cv2.CV_64F))

    # Contrast and brightness
    contrast = np.std(img)
    brightness = np.mean(img)

    # Edge density
    edges = cv2.Canny(img, 50, 150)
    edge_density = np.sum(edges > 0) / edges.size

    # FFT analysis
    f = np.fft.fft2(img)
    fshift = np.fft.fftshift(f)
    magnitude = np.abs(fshift)

    h, w = magnitude.shape
    center_y, center_x = h // 2, w // 2

    low_freq_mask = np.zeros_like(magnitude)
    cv2.circle(low_freq_mask, (center_x, center_y), min(h, w) // 8, 1, -1)

    low_freq_energy = np.sum(magnitude * low_freq_mask)
    high_freq_energy = np.sum(magnitude * (1 - low_freq_mask))
    total_energy = low_freq_energy + high_freq_energy

    high_freq_pct = high_freq_energy / total_energy * 100 if total_energy > 0 else 0

    return {
        "blur_score": round(blur_score, 2),
        "noise_estimate": round(noise_estimate, 2),
        "contrast": round(contrast, 2),
        "brightness": round(brightness, 2),
        "edge_density": round(edge_density * 100, 4),
        "high_freq_pct": round(high_freq_pct, 2),
    }


# ============================================================================
# Main Processing
# ============================================================================

def main():
    print("=" * 70)
    print("DEGRADATION SPECTRUM GENERATOR v2")
    print("=" * 70)
    print()
    print(f"Input: {INPUT_DIR}")
    print(f"Output: {OUTPUT_DIR}")
    print(f"Documents: {len(DOCUMENTS)}")
    print(f"Bands: {CONFIDENCE_BANDS}")
    print(f"Variants: {VARIANTS}")
    print(f"Outlier docs: {OUTLIER_DOCS}")
    print(f"Total images: {len(DOCUMENTS) * len(CONFIDENCE_BANDS) * len(VARIANTS)}")
    print()

    # Create output directories
    for band in CONFIDENCE_BANDS:
        for variant in VARIANTS:
            dir_path = OUTPUT_DIR / f"{band}_{variant}"
            dir_path.mkdir(parents=True, exist_ok=True)

    profiles = get_degradation_profiles()
    results = {}

    # Process each document
    for doc_id, filename in DOCUMENTS.items():
        input_path = INPUT_DIR / filename
        if not input_path.exists():
            print(f"WARNING: {filename} not found!")
            continue

        print(f"\n{'='*60}")
        print(f"Processing: {doc_id} ({filename})")
        print(f"{'='*60}")

        # Load original
        original = Image.open(input_path)
        if original.mode != 'RGB':
            original = original.convert('RGB')

        # Determine if outlier
        doc_type = "outlier" if doc_id in OUTLIER_DOCS else "normal"
        print(f"  Type: {doc_type.upper()}")

        results[doc_id] = {
            "filename": filename,
            "type": doc_type,
            "degradations": {}
        }

        # Process each band × variant
        for band in CONFIDENCE_BANDS:
            for variant in VARIANTS:
                profile_key = (doc_type, band, variant)
                profile = profiles[profile_key]

                # Apply degradation
                seed = hash(f"{doc_id}_{band}_{variant}") % (2**32)
                degraded = apply_degradation(original, profile, seed)

                # Save degraded image
                output_dir = OUTPUT_DIR / f"{band}_{variant}"
                output_path = output_dir / filename

                # Save as PNG to avoid additional compression
                if output_path.suffix.lower() in ['.jpg', '.jpeg']:
                    output_path = output_path.with_suffix('.png')

                degraded.save(output_path, 'PNG')

                # Run OCR
                ocr_result = run_tesseract_with_confidence(output_path)

                # Analyze image properties
                img_props = analyze_image_properties(output_path)

                # Store results
                key = f"{band}_{variant}"
                results[doc_id]["degradations"][key] = {
                    "profile": asdict(profile),
                    "output_path": str(output_path),
                    "ocr": ocr_result,
                    "image_properties": img_props,
                }

                conf = ocr_result['mean_confidence']
                print(f"  {band}_{variant}: {conf:.1f}% confidence")

    # Save results
    results_path = OUTPUT_DIR / "degradation_spectrum_results.json"
    with open(results_path, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    # Print summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    print()
    print(f"{'Document':<10} {'Type':<8} ", end="")
    for band in CONFIDENCE_BANDS:
        print(f"{band+'_s':<8} {band+'_h':<8} ", end="")
    print()
    print("-" * 90)

    for doc_id, data in results.items():
        doc_type = data['type'][:3]
        print(f"{doc_id:<10} {doc_type:<8} ", end="")
        for band in CONFIDENCE_BANDS:
            for variant in VARIANTS:
                key = f"{band}_{variant}"
                if key in data['degradations']:
                    conf = data['degradations'][key]['ocr']['mean_confidence']
                    print(f"{conf:<8.1f} ", end="")
                else:
                    print(f"{'N/A':<8} ", end="")
        print()

    print()
    print(f"Results saved to: {results_path}")
    print("=" * 70)

    return results


if __name__ == "__main__":
    main()
