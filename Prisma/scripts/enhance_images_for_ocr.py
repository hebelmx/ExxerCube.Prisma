#!/usr/bin/env python3
"""
Image Enhancement for OCR Testing
==================================

Apply digital enhancement filters to degraded images to test OCR improvement.

PHASE 2: Enhancement Filters Testing
- Baseline: Q1_Poor (~78-92% confidence, "very good text")
- Target: Q2_MediumPoor (~42-53% confidence, "highly corrupted text")
- Goal: Lift Q2 from ~42-53% → ~70%+ (production threshold)

Enhancement Pipeline:
1. Contrast enhancement (PIL ImageEnhance)
2. Denoising (PIL MedianFilter)
3. Adaptive thresholding (OpenCV Gaussian)
4. Deskewing (OpenCV rotation correction)

Usage:
    python enhance_images_for_ocr.py --quality Q1_Poor Q2_MediumPoor
    python enhance_images_for_ocr.py --all
    python enhance_images_for_ocr.py --quality Q2_MediumPoor --aggressive
"""

import sys
import argparse
from pathlib import Path
from typing import List, Tuple
import numpy as np
from PIL import Image, ImageEnhance, ImageFilter
import cv2

# Add Python modules to path
SCRIPT_DIR = Path(__file__).parent.parent
PRISMA_AI_EXTRACTORS = SCRIPT_DIR / "Code" / "Src" / "Python" / "prisma-ai-extractors" / "src"
PRISMA_OCR_PIPELINE = SCRIPT_DIR / "Code" / "Src" / "Python" / "prisma-ocr-pipeline" / "src"

sys.path.insert(0, str(PRISMA_AI_EXTRACTORS))
sys.path.insert(0, str(PRISMA_OCR_PIPELINE))


def pil_to_cv2(pil_image: Image.Image) -> np.ndarray:
    """Convert PIL Image to OpenCV format."""
    return cv2.cvtColor(np.array(pil_image), cv2.COLOR_RGB2BGR)


def cv2_to_pil(cv2_image: np.ndarray) -> Image.Image:
    """Convert OpenCV image to PIL format."""
    return Image.fromarray(cv2.cvtColor(cv2_image, cv2.COLOR_BGR2RGB))


def enhance_image_moderate(image_path: Path) -> Image.Image:
    """
    Apply moderate enhancement filters (Q1_Poor, Q2_MediumPoor).

    Enhancement pipeline:
    1. Convert to grayscale
    2. Moderate contrast enhancement (1.3x)
    3. Light denoising (median filter size=3)
    4. Adaptive Gaussian thresholding
    5. Deskewing (if needed)

    Args:
        image_path: Path to degraded image

    Returns:
        Enhanced PIL Image
    """
    print(f"  [MODERATE] Enhancing: {image_path.name}")

    # Load image
    image = Image.open(image_path)
    original_mode = image.mode

    # Step 1: Convert to grayscale
    if image.mode != 'L':
        image = image.convert('L')
    print(f"    ✓ Converted to grayscale")

    # Step 2: Moderate contrast enhancement
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(1.3)  # Moderate boost
    print(f"    ✓ Contrast enhanced (1.3x)")

    # Step 3: Light denoising
    image = image.filter(ImageFilter.MedianFilter(size=3))
    print(f"    ✓ Denoising applied (median filter size=3)")

    # Step 4: Adaptive Gaussian thresholding (OpenCV)
    cv2_image = np.array(image)
    binary = cv2.adaptiveThreshold(
        src=cv2_image,
        maxValue=255,
        adaptiveMethod=cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        thresholdType=cv2.THRESH_BINARY,
        blockSize=41,
        C=11
    )
    print(f"    ✓ Adaptive Gaussian threshold applied (block_size=41)")

    # Step 5: Deskewing
    _, binary_inv = cv2.threshold(cv2_image, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    coords = np.column_stack(np.where(binary_inv > 0))

    if coords.size > 0:
        rect = cv2.minAreaRect(coords)
        angle = rect[-1]
        if angle < -45:
            angle = 90 + angle

        if abs(angle) > 0.5:  # Only deskew if angle is significant
            height, width = binary.shape[:2]
            center = (width // 2, height // 2)
            rotation_matrix = cv2.getRotationMatrix2D(center, angle, 1.0)
            binary = cv2.warpAffine(
                binary,
                rotation_matrix,
                (width, height),
                flags=cv2.INTER_CUBIC,
                borderMode=cv2.BORDER_REPLICATE
            )
            print(f"    ✓ Deskewed by {angle:.2f}°")
        else:
            print(f"    ✓ No deskewing needed (angle={angle:.2f}°)")
    else:
        print(f"    ⚠ Cannot detect skew (no text detected)")

    # Convert back to PIL
    enhanced = Image.fromarray(binary)

    # Convert back to original mode if needed
    if original_mode == 'RGB':
        enhanced = enhanced.convert('RGB')

    return enhanced


def enhance_image_aggressive(image_path: Path) -> Image.Image:
    """
    Apply aggressive enhancement filters (Q2_MediumPoor only).

    Enhanced pipeline for severely degraded images:
    1. Convert to grayscale
    2. Strong contrast enhancement (1.7x)
    3. Bilateral denoising (OpenCV - preserves edges)
    4. Adaptive Gaussian thresholding (smaller block size)
    5. Morphological operations (closing to connect text)
    6. Deskewing

    Args:
        image_path: Path to degraded image

    Returns:
        Enhanced PIL Image
    """
    print(f"  [AGGRESSIVE] Enhancing: {image_path.name}")

    # Load image
    image = Image.open(image_path)
    original_mode = image.mode

    # Step 1: Convert to grayscale
    if image.mode != 'L':
        image = image.convert('L')
    print(f"    ✓ Converted to grayscale")

    # Step 2: Strong contrast enhancement
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(1.7)  # Aggressive boost
    print(f"    ✓ Contrast enhanced (1.7x)")

    # Step 3: Bilateral denoising (OpenCV - preserves edges better)
    cv2_image = np.array(image)
    denoised = cv2.bilateralFilter(cv2_image, d=9, sigmaColor=75, sigmaSpace=75)
    print(f"    ✓ Bilateral denoising applied (d=9, sigmaColor=75)")

    # Step 4: Adaptive Gaussian thresholding (smaller block size for degraded text)
    binary = cv2.adaptiveThreshold(
        src=denoised,
        maxValue=255,
        adaptiveMethod=cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        thresholdType=cv2.THRESH_BINARY,
        blockSize=31,  # Smaller block size for degraded text
        C=15           # Higher C value to reduce noise
    )
    print(f"    ✓ Adaptive Gaussian threshold applied (block_size=31, C=15)")

    # Step 5: Morphological closing (connect broken text)
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (2, 2))
    binary = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, kernel)
    print(f"    ✓ Morphological closing applied (kernel=2x2)")

    # Step 6: Deskewing
    _, binary_inv = cv2.threshold(denoised, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    coords = np.column_stack(np.where(binary_inv > 0))

    if coords.size > 0:
        rect = cv2.minAreaRect(coords)
        angle = rect[-1]
        if angle < -45:
            angle = 90 + angle

        if abs(angle) > 0.3:  # More sensitive deskewing
            height, width = binary.shape[:2]
            center = (width // 2, height // 2)
            rotation_matrix = cv2.getRotationMatrix2D(center, angle, 1.0)
            binary = cv2.warpAffine(
                binary,
                rotation_matrix,
                (width, height),
                flags=cv2.INTER_CUBIC,
                borderMode=cv2.BORDER_REPLICATE
            )
            print(f"    ✓ Deskewed by {angle:.2f}°")
        else:
            print(f"    ✓ No deskewing needed (angle={angle:.2f}°)")
    else:
        print(f"    ⚠ Cannot detect skew (no text detected)")

    # Convert back to PIL
    enhanced = Image.fromarray(binary)

    # Convert back to original mode if needed
    if original_mode == 'RGB':
        enhanced = enhanced.convert('RGB')

    return enhanced


def process_degraded_images(
    quality_levels: List[str],
    aggressive: bool = False,
    input_dir: Path = None,
    output_dir: Path = None
) -> Tuple[int, int]:
    """
    Process degraded images and apply enhancement filters.

    Args:
        quality_levels: List of quality levels to process (e.g., ["Q1_Poor", "Q2_MediumPoor"])
        aggressive: Use aggressive enhancement (recommended for Q2_MediumPoor)
        input_dir: Input directory (default: Fixtures/PRP1_Degraded)
        output_dir: Output directory (default: Fixtures/PRP1_Enhanced)

    Returns:
        Tuple of (processed_count, error_count)
    """
    # Default directories
    if input_dir is None:
        input_dir = SCRIPT_DIR / "Fixtures" / "PRP1_Degraded"
    if output_dir is None:
        output_dir = SCRIPT_DIR / "Fixtures" / "PRP1_Enhanced"

    print(f"\n{'='*80}")
    print(f"IMAGE ENHANCEMENT FOR OCR TESTING")
    print(f"{'='*80}")
    print(f"Input:  {input_dir}")
    print(f"Output: {output_dir}")
    print(f"Quality levels: {', '.join(quality_levels)}")
    print(f"Enhancement mode: {'AGGRESSIVE' if aggressive else 'MODERATE'}")
    print(f"{'='*80}\n")

    processed = 0
    errors = 0

    for quality_level in quality_levels:
        quality_input_dir = input_dir / quality_level
        quality_output_dir = output_dir / quality_level

        if not quality_input_dir.exists():
            print(f"⚠ WARNING: Quality level directory not found: {quality_input_dir}")
            continue

        # Create output directory
        quality_output_dir.mkdir(parents=True, exist_ok=True)

        print(f"\n[{quality_level}] Processing images...")
        print(f"  Input:  {quality_input_dir}")
        print(f"  Output: {quality_output_dir}")

        # Find all image files
        image_files = list(quality_input_dir.glob("*.jpg")) + list(quality_input_dir.glob("*.png"))

        if not image_files:
            print(f"  ⚠ No images found in {quality_input_dir}")
            continue

        print(f"  Found {len(image_files)} images to process\n")

        for image_file in sorted(image_files):
            try:
                # Choose enhancement mode
                if aggressive:
                    enhanced = enhance_image_aggressive(image_file)
                else:
                    enhanced = enhance_image_moderate(image_file)

                # Save enhanced image
                output_path = quality_output_dir / image_file.name
                enhanced.save(output_path, quality=95)

                # Verify file was created
                if output_path.exists():
                    file_size = output_path.stat().st_size
                    print(f"    ✓ Saved: {output_path.name} ({file_size:,} bytes)\n")
                    processed += 1
                else:
                    print(f"    ✗ FAILED to save: {output_path.name}\n")
                    errors += 1

            except Exception as e:
                print(f"    ✗ ERROR processing {image_file.name}: {e}\n")
                errors += 1

        print(f"[{quality_level}] Completed: {processed} processed, {errors} errors\n")

    return processed, errors


def main():
    parser = argparse.ArgumentParser(
        description="Apply enhancement filters to degraded images for OCR testing",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Process Q1 and Q2 with moderate enhancement
  python enhance_images_for_ocr.py --quality Q1_Poor Q2_MediumPoor

  # Process Q2 only with aggressive enhancement
  python enhance_images_for_ocr.py --quality Q2_MediumPoor --aggressive

  # Process all quality levels (Q1-Q4) with moderate enhancement
  python enhance_images_for_ocr.py --all

  # Process Q1 and Q2 with custom directories
  python enhance_images_for_ocr.py --quality Q1_Poor Q2_MediumPoor --input ./custom_degraded --output ./custom_enhanced
        """
    )

    parser.add_argument(
        "--quality",
        nargs="+",
        choices=["Q1_Poor", "Q2_MediumPoor", "Q3_Low", "Q4_VeryLow"],
        help="Quality levels to process"
    )

    parser.add_argument(
        "--all",
        action="store_true",
        help="Process all quality levels (Q1-Q4)"
    )

    parser.add_argument(
        "--aggressive",
        action="store_true",
        help="Use aggressive enhancement (stronger filters, recommended for Q2+)"
    )

    parser.add_argument(
        "--input",
        type=Path,
        help="Input directory (default: Fixtures/PRP1_Degraded)"
    )

    parser.add_argument(
        "--output",
        type=Path,
        help="Output directory (default: Fixtures/PRP1_Enhanced)"
    )

    args = parser.parse_args()

    # Determine quality levels
    if args.all:
        quality_levels = ["Q1_Poor", "Q2_MediumPoor", "Q3_Low", "Q4_VeryLow"]
    elif args.quality:
        quality_levels = args.quality
    else:
        # Default: Focus on Q1 and Q2 (most relevant for testing)
        quality_levels = ["Q1_Poor", "Q2_MediumPoor"]
        print("No quality levels specified. Using default: Q1_Poor, Q2_MediumPoor")

    # Process images
    processed, errors = process_degraded_images(
        quality_levels=quality_levels,
        aggressive=args.aggressive,
        input_dir=args.input,
        output_dir=args.output
    )

    # Summary
    print(f"\n{'='*80}")
    print(f"ENHANCEMENT COMPLETE")
    print(f"{'='*80}")
    print(f"✓ Successfully processed: {processed}")
    print(f"✗ Errors:                 {errors}")
    print(f"{'='*80}\n")

    if errors > 0:
        print("⚠ WARNING: Some images failed to process. Check logs above.")
        return 1

    print("✓ All images enhanced successfully!")
    print("\nNext steps:")
    print("  1. Run enhanced tests: TesseractOcrExecutorEnhancedTests.cs")
    print("  2. Compare baseline vs enhanced performance")
    print("  3. Verify Q2 confidence lifts from ~42-53% → ~70%+")

    return 0


if __name__ == "__main__":
    sys.exit(main())
