#!/usr/bin/env python3
"""
Aggressive enhancement pipeline for Q1 and Q2 degraded images.

This pipeline uses more aggressive filters than the standard enhancement:
1. Grayscale conversion
2. Fast Non-local Means Denoising (h=30) - aggressive
3. CLAHE (Contrast Limited Adaptive Histogram Equalization)
4. Adaptive Thresholding (binarization)
5. Deskewing (contour-based angle detection)

WARNING: This includes binarization and deskewing which previously caused issues.
This is an EXPERIMENTAL pipeline to test if more aggressive filtering can rescue Q2 images.

Output: PRP1_Enhanced_Aggressive/{Q1_Poor,Q2_MediumPoor}/
"""

import cv2
import numpy as np
from pathlib import Path
from PIL import Image

def aggressive_enhance_image(input_path: Path, output_path: Path) -> dict:
    """
    Apply aggressive enhancement pipeline to an image.

    Returns metrics dict with processing info.
    """
    print(f"\n{'='*80}")
    print(f"Processing: {input_path.name}")
    print(f"{'='*80}")

    # Load image
    image = cv2.imread(str(input_path))
    if image is None:
        raise ValueError(f"Failed to load image: {input_path}")

    print(f"Original size: {image.shape}")

    # Step 1: Convert to Grayscale
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    print("✓ Grayscale conversion")

    # Step 2: Aggressive Denoising (h=30 is strong)
    denoised = cv2.fastNlMeansDenoising(gray, h=30)
    print("✓ Fast Non-local Means Denoising (h=30)")

    # Step 3: CLAHE - Contrast enhancement
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)
    print("✓ CLAHE contrast enhancement")

    # Step 4: Adaptive Thresholding (Binarization)
    # WARNING: This may harm ML-based OCR (Tesseract LSTM, GOT-OCR2)
    binarized = cv2.adaptiveThreshold(
        enhanced, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        cv2.THRESH_BINARY, 15, 11
    )
    print("✓ Adaptive thresholding (binarization)")

    # Step 5: Deskewing (contour-based angle detection)
    # WARNING: This previously caused catastrophic failures on perspective-distorted images
    coords = np.column_stack(np.where(binarized > 0))
    if coords.size > 0:
        angle = cv2.minAreaRect(coords)[-1]
        if angle < -45:
            angle = -(90 + angle)
        else:
            angle = -angle

        print(f"  Detected angle: {angle:.3f}°")

        (h, w) = binarized.shape[:2]
        M = cv2.getRotationMatrix2D((w // 2, h // 2), angle, 1.0)
        deskewed = cv2.warpAffine(
            binarized, M, (w, h),
            flags=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REPLICATE
        )
        print(f"✓ Deskewing applied ({angle:.3f}°)")
    else:
        deskewed = binarized
        angle = 0.0
        print("⚠ No coordinates found for deskewing, skipping")

    # Save output
    output_path.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(output_path), deskewed)
    print(f"✓ Saved to: {output_path}")

    return {
        "input_path": str(input_path),
        "output_path": str(output_path),
        "original_shape": image.shape,
        "final_shape": deskewed.shape,
        "detected_angle": angle,
    }


def main():
    """Process all Q1 and Q2 degraded images with aggressive enhancement."""

    # Define paths
    base_path = Path(__file__).parent.parent
    degraded_base = base_path / "Fixtures" / "PRP1_Degraded"
    output_base = base_path / "Fixtures" / "PRP1_Enhanced_Aggressive"

    # Test images
    test_images = [
        "222AAA-44444444442025_page-0001.jpg",
        "333BBB-44444444442025_page1.png",
        "333ccc-6666666662025_page1.png",
        "555CCC-66666662025_page1.png"
    ]

    # Quality levels to process
    quality_levels = ["Q1_Poor", "Q2_MediumPoor"]

    print("="*80)
    print("AGGRESSIVE ENHANCEMENT PIPELINE FOR PRP1 DEGRADED FIXTURES")
    print("="*80)
    print()
    print("Pipeline steps:")
    print("  1. Grayscale conversion")
    print("  2. Fast Non-local Means Denoising (h=30)")
    print("  3. CLAHE contrast enhancement")
    print("  4. Adaptive thresholding (binarization)")
    print("  5. Contour-based deskewing")
    print()
    print("WARNING: This pipeline includes binarization and deskewing")
    print("         which previously caused issues. This is EXPERIMENTAL.")
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
                result = aggressive_enhance_image(input_path, output_path)
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
    print("Angle corrections applied:")
    for result in all_results:
        img_name = Path(result['input_path']).name
        angle = result['detected_angle']
        print(f"  {img_name:45s} → {angle:+7.3f}°")
    print("="*80)
    print()
    print("Next steps:")
    print("  1. Run Tesseract tests on PRP1_Enhanced_Aggressive fixtures")
    print("  2. Run GOT-OCR2 tests on PRP1_Enhanced_Aggressive fixtures")
    print("  3. Compare results against baseline and standard enhancement")
    print()


if __name__ == "__main__":
    main()
