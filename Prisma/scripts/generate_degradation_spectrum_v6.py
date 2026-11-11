#!/usr/bin/env python3
"""
Degradation Spectrum Generator v6

IMPROVEMENTS:
- Finer blur steps for sensitive documents
- More artifact types: texture, stains, folds, uneven lighting, skew
- Balanced dataset across all 4 source documents
- Target: ~10-12 images per document in 55-85% confidence range
"""

import subprocess
import json
import io
import random
import math
from pathlib import Path
from typing import List, Dict, Tuple

import numpy as np
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw
import cv2


# ============================================================================
# Configuration
# ============================================================================

BASE_PATH = Path(__file__).parent.parent / "Fixtures"
INPUT_DIR = BASE_PATH / "PRP1"
OUTPUT_DIR = BASE_PATH / "PRP1_Degraded_v6"

DOCUMENTS = {
    "222AAA": {
        "filename": "222AAA-44444444442025_page-1.png",
        "blur_step": 0.4,      # Can use larger steps (resistant)
        "blur_max": 5.0,
    },
    "333BBB": {
        "filename": "333BBB-44444444442025_page1.png",
        "blur_step": 0.15,     # Fine steps (sensitive)
        "blur_max": 2.5,
    },
    "333ccc": {
        "filename": "333ccc-6666666662025_page1.png",
        "blur_step": 0.12,     # Very fine steps (very sensitive)
        "blur_max": 2.0,
    },
    "555CCC": {
        "filename": "555CCC-66666662025_page-0001.png",
        "blur_step": 0.15,     # Fine steps
        "blur_max": 2.5,
    },
}

# Target confidence levels
TARGET_CONFIDENCES = [55, 65, 75, 85]
TOLERANCE = 5
MAX_PER_TARGET_PER_DOC = 4  # Ensure balance

RANDOM_SEED = 42


# ============================================================================
# Artifact Functions
# ============================================================================

def add_paper_texture(img: Image.Image, intensity: float = 0.1) -> Image.Image:
    """Add subtle paper texture noise."""
    img = img.copy()
    w, h = img.size

    # Create texture pattern
    texture = np.random.normal(128, 15 * intensity, (h, w)).astype(np.uint8)
    texture_img = Image.fromarray(texture).convert('L')

    # Blend with original
    img_array = np.array(img, dtype=np.float32)
    texture_array = np.array(texture_img.convert('RGB'), dtype=np.float32)

    # Soft light blend
    blended = img_array + (texture_array - 128) * intensity * 0.5
    blended = np.clip(blended, 0, 255).astype(np.uint8)

    return Image.fromarray(blended)


def add_coffee_stain(img: Image.Image, x: int, y: int, radius: int, intensity: float = 0.3) -> Image.Image:
    """Add a coffee-like stain at position."""
    img = img.copy()
    img_array = np.array(img, dtype=np.float32)
    h, w = img_array.shape[:2]

    # Create stain mask with gradient
    Y, X = np.ogrid[:h, :w]
    dist = np.sqrt((X - x)**2 + (Y - y)**2)

    # Ring-like stain (darker at edges)
    inner_radius = radius * 0.7
    mask = np.zeros((h, w))
    ring_area = (dist >= inner_radius) & (dist <= radius)
    mask[ring_area] = 1 - (dist[ring_area] - inner_radius) / (radius - inner_radius) * 0.5

    # Slight fill inside
    inner_area = dist < inner_radius
    mask[inner_area] = 0.3

    # Add some noise to the stain
    noise = np.random.normal(1, 0.1, mask.shape)
    mask = mask * noise
    mask = np.clip(mask, 0, 1)

    # Apply brownish tint
    stain_color = np.array([200, 180, 150])  # Light brown
    for c in range(3):
        img_array[:, :, c] = img_array[:, :, c] * (1 - mask * intensity) + stain_color[c] * mask * intensity

    return Image.fromarray(np.clip(img_array, 0, 255).astype(np.uint8))


def add_fold_line(img: Image.Image, orientation: str = 'horizontal', position: float = 0.5, intensity: float = 0.2) -> Image.Image:
    """Add a fold/crease line."""
    img = img.copy()
    draw = ImageDraw.Draw(img)
    w, h = img.size

    if orientation == 'horizontal':
        y = int(h * position)
        # Draw shadow line
        for offset in range(-2, 3):
            alpha = int(255 * (1 - abs(offset) / 3) * intensity)
            color = (255 - alpha, 255 - alpha, 255 - alpha)
            draw.line([(0, y + offset), (w, y + offset)], fill=color, width=1)
    else:  # vertical
        x = int(w * position)
        for offset in range(-2, 3):
            alpha = int(255 * (1 - abs(offset) / 3) * intensity)
            color = (255 - alpha, 255 - alpha, 255 - alpha)
            draw.line([(x + offset, 0), (x + offset, h)], fill=color, width=1)

    return img


def add_uneven_lighting(img: Image.Image, gradient_type: str = 'corner', intensity: float = 0.15) -> Image.Image:
    """Add uneven lighting (vignette or gradient)."""
    img = img.copy()
    img_array = np.array(img, dtype=np.float32)
    h, w = img_array.shape[:2]

    if gradient_type == 'corner':
        # Darker in one corner
        Y, X = np.ogrid[:h, :w]
        # Distance from top-left corner
        dist = np.sqrt(X**2 + Y**2)
        max_dist = np.sqrt(w**2 + h**2)
        gradient = 1 - (dist / max_dist) * intensity
    elif gradient_type == 'vignette':
        # Classic vignette
        Y, X = np.ogrid[:h, :w]
        center_x, center_y = w // 2, h // 2
        dist = np.sqrt((X - center_x)**2 + (Y - center_y)**2)
        max_dist = np.sqrt(center_x**2 + center_y**2)
        gradient = 1 - (dist / max_dist)**2 * intensity
    else:  # side
        # Gradient from left to right
        gradient = np.linspace(1 - intensity, 1, w)
        gradient = np.tile(gradient, (h, 1))

    gradient = gradient[:, :, np.newaxis]
    img_array = img_array * gradient

    return Image.fromarray(np.clip(img_array, 0, 255).astype(np.uint8))


def add_slight_skew(img: Image.Image, angle: float = 0.5) -> Image.Image:
    """Add slight rotation/skew."""
    return img.rotate(angle, resample=Image.BICUBIC, expand=False, fillcolor=(255, 255, 255))


def add_localized_scan_lines(img: Image.Image, coverage: float = 0.15, intensity: float = 0.15) -> Image.Image:
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


# ============================================================================
# Degradation Combinations
# ============================================================================

ARTIFACT_COMBOS = [
    # (name, artifact_func, probability)
    ("clean", None, 1.0),
    ("texture", lambda img: add_paper_texture(img, 0.08), 0.4),
    ("scan", lambda img: add_localized_scan_lines(img, 0.12, 0.12), 0.3),
    ("fold_h", lambda img: add_fold_line(img, 'horizontal', random.uniform(0.3, 0.7), 0.15), 0.2),
    ("fold_v", lambda img: add_fold_line(img, 'vertical', random.uniform(0.3, 0.7), 0.15), 0.2),
    ("vignette", lambda img: add_uneven_lighting(img, 'vignette', 0.12), 0.3),
    ("corner_light", lambda img: add_uneven_lighting(img, 'corner', 0.1), 0.2),
    ("skew", lambda img: add_slight_skew(img, random.uniform(-0.8, 0.8)), 0.25),
    ("stain", lambda img: add_coffee_stain(img,
                                           random.randint(50, img.size[0]-50),
                                           random.randint(50, img.size[1]-50),
                                           random.randint(30, 80), 0.2), 0.15),
]


def apply_degradation(image: Image.Image, blur: float, artifacts: List[str], seed: int) -> Image.Image:
    """Apply blur + selected artifacts."""
    random.seed(seed)
    np.random.seed(seed)

    degraded = image.copy()
    if degraded.mode != 'RGB':
        degraded = degraded.convert('RGB')

    # 1. Gaussian blur (primary)
    if blur > 0:
        degraded = degraded.filter(ImageFilter.GaussianBlur(radius=blur))

    # 2. Light noise
    noise = min(0.012, blur * 0.003)
    if noise > 0:
        degraded = add_gaussian_noise(degraded, noise)

    # 3. Slight contrast reduction
    contrast = max(0.90, 1.0 - blur * 0.015)
    if contrast < 1.0:
        degraded = ImageEnhance.Contrast(degraded).enhance(contrast)

    # 4. Apply selected artifacts
    for artifact_name in artifacts:
        for name, func, _ in ARTIFACT_COMBOS:
            if name == artifact_name and func is not None:
                degraded = func(degraded)
                break

    # 5. JPEG compression
    jpeg_q = max(65, 85 - int(blur * 4))
    degraded = apply_jpeg_compression(degraded, jpeg_q)

    return degraded


# ============================================================================
# OCR Functions
# ============================================================================

def run_tesseract_quick(image: Image.Image) -> float:
    """Run Tesseract and return confidence."""
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
    """Run Tesseract with full results."""
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
    print("DEGRADATION SPECTRUM GENERATOR v6")
    print("=" * 70)
    print()
    print("Features:")
    print("  - Per-document blur steps (finer for sensitive docs)")
    print("  - Multiple artifact types: texture, stains, folds, lighting, skew")
    print("  - Balanced sampling across all documents")
    print()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    kept_images = []

    # Track per-document, per-target counts
    found_counts = {doc_id: {t: 0 for t in TARGET_CONFIDENCES} for doc_id in DOCUMENTS}

    for doc_id, doc_info in DOCUMENTS.items():
        filename = doc_info["filename"]
        input_path = INPUT_DIR / filename

        if not input_path.exists():
            print(f"WARNING: {filename} not found!")
            continue

        print(f"\n{'='*60}")
        print(f"SCANNING: {doc_id}")
        print(f"  Blur step: {doc_info['blur_step']}, max: {doc_info['blur_max']}")
        print(f"{'='*60}")

        original = Image.open(input_path)
        if original.mode != 'RGB':
            original = original.convert('RGB')

        baseline_conf = run_tesseract_quick(original)
        print(f"Baseline confidence: {baseline_conf:.1f}%")
        print()

        # Scan blur levels with document-specific steps
        blur_levels = np.arange(0.1, doc_info['blur_max'] + 0.01, doc_info['blur_step'])

        print(f"{'Blur':>5} | {'Conf':>5} | {'Artifacts':>15} | Target | Status")
        print("-" * 60)

        for blur in blur_levels:
            # Try different artifact combinations
            artifact_options = [
                [],  # clean
                ["texture"],
                ["scan"],
                ["vignette"],
                ["skew"],
                ["texture", "vignette"],
                ["fold_h"],
                ["stain"],
            ]

            for artifacts in artifact_options:
                # Check if we still need images for any target
                all_full = all(found_counts[doc_id][t] >= MAX_PER_TARGET_PER_DOC for t in TARGET_CONFIDENCES)
                if all_full:
                    break

                seed = hash(f"{doc_id}_{blur}_{','.join(artifacts)}") % (2**32)
                degraded = apply_degradation(original, blur, artifacts, seed)
                conf = run_tesseract_quick(degraded)

                # Check if matches any target
                matched_target = None
                for target in TARGET_CONFIDENCES:
                    if target - TOLERANCE <= conf <= target + TOLERANCE:
                        if found_counts[doc_id][target] < MAX_PER_TARGET_PER_DOC:
                            matched_target = target
                            break

                artifact_str = "+".join(artifacts) if artifacts else "clean"

                if matched_target:
                    found_counts[doc_id][matched_target] += 1

                    # Save
                    output_name = f"{doc_id}_b{blur:.2f}_{artifact_str}_t{matched_target}.png"
                    output_path = OUTPUT_DIR / output_name
                    degraded.save(output_path, 'PNG')

                    kept_images.append({
                        "doc_id": doc_id,
                        "filename": output_name,
                        "blur": round(blur, 2),
                        "artifacts": artifacts,
                        "target_conf": matched_target,
                        "actual_conf": round(conf, 1),
                    })

                    print(f"{blur:5.2f} | {conf:5.1f} | {artifact_str:>15} | {matched_target:>6} | ✓ KEEP")

            if all_full:
                print(f"  Document {doc_id} complete!")
                break

        print()
        print(f"Counts for {doc_id}: {found_counts[doc_id]}")

    # Get full OCR results
    print("\n" + "=" * 70)
    print("FINAL PROCESSING")
    print("=" * 70)

    final_results = []
    for img_info in kept_images:
        output_path = OUTPUT_DIR / img_info["filename"]
        ocr_result = run_tesseract_full(output_path)
        img_props = analyze_image_properties(output_path)

        final_results.append({
            **img_info,
            "ocr": ocr_result,
            "image_properties": img_props,
        })

    # Save results
    results_path = OUTPUT_DIR / "degradation_results_v6.json"
    with open(results_path, 'w', encoding='utf-8') as f:
        json.dump(final_results, f, indent=2, ensure_ascii=False)

    # Summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)

    print(f"\nTotal images: {len(kept_images)}")

    print("\nDistribution by document and target:")
    print(f"{'Doc':<10}", end="")
    for t in TARGET_CONFIDENCES:
        print(f"{t}%{'':<5}", end="")
    print("Total")
    print("-" * 50)

    for doc_id in DOCUMENTS:
        print(f"{doc_id:<10}", end="")
        doc_total = 0
        for t in TARGET_CONFIDENCES:
            count = found_counts[doc_id][t]
            doc_total += count
            print(f"{count:<8}", end="")
        print(f"{doc_total}")

    print()
    print("Artifact distribution:")
    artifact_counts = {}
    for img in kept_images:
        key = "+".join(img["artifacts"]) if img["artifacts"] else "clean"
        artifact_counts[key] = artifact_counts.get(key, 0) + 1
    for artifact, count in sorted(artifact_counts.items(), key=lambda x: -x[1]):
        print(f"  {artifact}: {count}")

    print(f"\nResults saved to: {results_path}")
    print("=" * 70)

    return final_results


if __name__ == "__main__":
    main()
