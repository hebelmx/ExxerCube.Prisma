#!/usr/bin/env python3
"""
Adaptive Image Enhancement Pipeline for OCR.

Uses image quality analysis to select optimal filter parameters dynamically:
- Analyzes blur, noise, contrast, brightness
- Selects denoising strength (h parameter)
- Selects CLAHE strength (clipLimit parameter)
- Applies adaptive enhancement

Output: PRP1_Enhanced_Adaptive/{Q1_Poor,Q2_MediumPoor,Q3_Low,Q4_VeryLow}/
"""

import cv2
import numpy as np
from pathlib import Path
from typing import Dict, Tuple


def estimate_noise_level(image: np.ndarray) -> float:
    """Estimate noise level using MAD on high-pass filtered image."""
    median = cv2.medianBlur(image, 5)
    high_freq = cv2.absdiff(image, median)
    sigma = np.median(high_freq) / 0.6745
    return float(sigma)


def analyze_image_quality(image: np.ndarray) -> Dict[str, float]:
    """Analyze image quality metrics."""
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image

    # Blur detection
    laplacian = cv2.Laplacian(gray, cv2.CV_64F)
    blur_score = laplacian.var()

    # Noise estimation
    noise_score = estimate_noise_level(gray)

    # Contrast measurement
    contrast = gray.std()

    # Brightness measurement
    brightness = gray.mean()

    return {
        'blur_score': float(blur_score),
        'noise_score': float(noise_score),
        'contrast': float(contrast),
        'brightness': float(brightness)
    }


def select_denoising_strength(noise_score: float) -> int:
    """Select optimal denoising h parameter based on noise."""
    if noise_score > 15:      # Very noisy
        return 30
    elif noise_score > 10:    # Heavy noise
        return 20
    elif noise_score > 5:     # Moderate noise
        return 10
    else:                     # Clean/minimal noise
        return 5


def select_clahe_strength(contrast: float) -> float:
    """Select optimal CLAHE clipLimit based on contrast."""
    if contrast < 30:         # Very low contrast
        return 3.0
    elif contrast < 40:       # Low contrast
        return 2.5
    elif contrast < 50:       # Medium contrast
        return 2.0
    else:                     # Good contrast
        return 1.5


def adaptive_enhance_image(input_path: Path, output_path: Path) -> dict:
    """
    Apply adaptive enhancement pipeline to an image.

    Returns metrics dict with processing info and recommendations.
    """
    print(f"\n{'='*80}")
    print(f"Processing: {input_path.name}")
    print(f"{'='*80}")

    # Load image
    image = cv2.imread(str(input_path))
    if image is None:
        raise ValueError(f"Failed to load image: {input_path}")

    print(f"Original size: {image.shape}")

    # Step 1: Analyze image quality
    metrics = analyze_image_quality(image)
    denoise_h = select_denoising_strength(metrics['noise_score'])
    clahe_clip = select_clahe_strength(metrics['contrast'])

    print(f"\n  Image Quality Metrics:")
    print(f"    Blur score:    {metrics['blur_score']:8.2f}  "
          f"{'(SHARP)' if metrics['blur_score'] > 200 else '(BLURRY)'}")
    print(f"    Noise score:   {metrics['noise_score']:8.2f}  "
          f"{'(CLEAN)' if metrics['noise_score'] < 5 else '(NOISY)' if metrics['noise_score'] > 10 else '(MODERATE)'}")
    print(f"    Contrast:      {metrics['contrast']:8.2f}  "
          f"{'(GOOD)' if metrics['contrast'] > 50 else '(POOR)' if metrics['contrast'] < 30 else '(MODERATE)'}")
    print(f"    Brightness:    {metrics['brightness']:8.2f}")

    print(f"\n  Adaptive Filter Parameters:")
    print(f"    Denoising (h):     {denoise_h}")
    print(f"    CLAHE clipLimit:   {clahe_clip}")
    print(f"\n  Fixed Parameters (for comparison):")
    print(f"    Denoising (h):     10")
    print(f"    CLAHE clipLimit:   2.0")

    # Step 2: Convert to Grayscale
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    print("  ✓ Grayscale conversion")

    # Step 3: Adaptive Denoising
    denoised = cv2.fastNlMeansDenoising(gray, h=denoise_h)
    print(f"  ✓ Fast Non-local Means Denoising (h={denoise_h})")

    # Step 4: Adaptive CLAHE
    clahe = cv2.createCLAHE(clipLimit=clahe_clip, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)
    print(f"  ✓ CLAHE contrast enhancement (clipLimit={clahe_clip})")

    # Step 5: Bilateral Filter (edge-preserving smoothing)
    bilateral = cv2.bilateralFilter(enhanced, d=9, sigmaColor=75, sigmaSpace=75)
    print("  ✓ Bilateral filter (edge-preserving)")

    # Step 6: Unsharp Mask (text sharpening)
    gaussian = cv2.GaussianBlur(bilateral, (9, 9), 10.0)
    unsharp = cv2.addWeighted(bilateral, 1.5, gaussian, -0.5, 0)
    print("  ✓ Unsharp mask (text sharpening)")

    # Save output
    output_path.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(output_path), unsharp)
    print(f"  ✓ Saved to: {output_path}")

    return {
        "input_path": str(input_path),
        "output_path": str(output_path),
        "original_shape": image.shape,
        "final_shape": unsharp.shape,
        "metrics": metrics,
        "adaptive_params": {
            "denoise_h": denoise_h,
            "clahe_clip": clahe_clip
        },
        "fixed_params": {
            "denoise_h": 10,
            "clahe_clip": 2.0
        }
    }


def main():
    """Process all degraded images with adaptive enhancement."""

    # Define paths
    base_path = Path(__file__).parent.parent
    degraded_base = base_path / "Fixtures" / "PRP1_Degraded"
    output_base = base_path / "Fixtures" / "PRP1_Enhanced_Adaptive"

    # Test images
    test_images = [
        "222AAA-44444444442025_page-0001.jpg",
        "333BBB-44444444442025_page1.png",
        "333ccc-6666666662025_page1.png",
        "555CCC-66666662025_page1.png"
    ]

    # Quality levels to process
    quality_levels = ["Q1_Poor", "Q2_MediumPoor", "Q3_Low", "Q4_VeryLow"]

    print("="*80)
    print("ADAPTIVE ENHANCEMENT PIPELINE FOR PRP1 DEGRADED FIXTURES")
    print("="*80)
    print()
    print("Enhancement steps:")
    print("  1. Grayscale conversion")
    print("  2. Adaptive denoising (h selected based on noise analysis)")
    print("  3. Adaptive CLAHE (clipLimit selected based on contrast)")
    print("  4. Bilateral filter (edge-preserving smoothing)")
    print("  5. Unsharp mask (text sharpening)")
    print()
    print("Output: PRP1_Enhanced_Adaptive/{Q1,Q2,Q3,Q4}/")
    print("="*80)

    all_results = []

    for quality_level in quality_levels:
        print(f"\n\n{'#'*80}")
        print(f"# Processing {quality_level}")
        print(f"{'#'*80}")

        for img_name in test_images:
            input_path = degraded_base / quality_level / img_name
            output_path = output_base / quality_level / img_name

            if not input_path.exists():
                print(f"\n❌ SKIP: {img_name} not found in {quality_level}")
                continue

            try:
                result = adaptive_enhance_image(input_path, output_path)
                all_results.append(result)
            except Exception as e:
                print(f"\n❌ FAILED: {img_name}")
                print(f"   Error: {e}")

    # Summary
    print("\n\n" + "="*80)
    print("ENHANCEMENT SUMMARY")
    print("="*80)
    print(f"Total images processed: {len(all_results)}")
    print(f"Output directory: {output_base}")
    print()
    print("Adaptive vs Fixed Parameters:")
    print()
    print(f"{'Image':<50} {'Contrast':<10} {'Fixed':<20} {'Adaptive':<20}")
    print(f"{'-'*100}")
    for result in all_results:
        img_name = Path(result['input_path']).name
        contrast = result['metrics']['contrast']
        fixed_params = f"h={result['fixed_params']['denoise_h']}, C={result['fixed_params']['clahe_clip']}"
        adaptive_params = f"h={result['adaptive_params']['denoise_h']}, C={result['adaptive_params']['clahe_clip']}"
        print(f"{img_name:<50} {contrast:>8.2f}  {fixed_params:<20} {adaptive_params:<20}")
    print("="*80)
    print()
    print("Next steps:")
    print("  1. Copy fixtures to test output directory")
    print("  2. Run Tesseract tests on PRP1_Enhanced_Adaptive fixtures")
    print("  3. Compare results: Adaptive vs Fixed vs Baseline")
    print("  4. Check if adaptive approach rescues Q2 333BBB (69.62% → 70%+)")
    print()


if __name__ == "__main__":
    main()
