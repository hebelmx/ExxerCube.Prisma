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

    TESSERACT-OPTIMIZED: NO BINARIZATION, NO DESKEWING
    Tesseract is ML-based and works better with grayscale (needs gradient info).
    Binary conversion destroys information and causes catastrophic failures.
    Deskewing causes perspective distortion issues on angled photographs.

    Enhancement pipeline:
    1. Convert to grayscale
    2. Moderate contrast enhancement (1.5x)
    3. Light denoising (median filter size=3)
    4. ❌ NO deskewing (causes perspective distortion damage)
    5. ❌ NO adaptive thresholding (keeps grayscale, not binary)

    Args:
        image_path: Path to degraded image

    Returns:
        Enhanced PIL Image (grayscale, NOT binary)
    """
    print(f"  [MODERATE - Tesseract Optimized] Enhancing: {image_path.name}")

    # Load image
    image = Image.open(image_path)
    original_mode = image.mode

    # Step 1: Convert to grayscale
    if image.mode != 'L':
        image = image.convert('L')
    print(f"    ✓ Converted to grayscale")

    # Step 2: Moderate contrast enhancement
    enhancer = ImageEnhance.Contrast(image)
    image = enhancer.enhance(1.5)  # Slightly higher for degraded images
    print(f"    ✓ Contrast enhanced (1.5x)")

    # Step 3: Light denoising
    image = image.filter(ImageFilter.MedianFilter(size=3))
    print(f"    ✓ Denoising applied (median filter size=3)")

    # Step 4: Deskewing DISABLED (causes perspective distortion issues)
    # Simple rotation cannot fix perspective-distorted images (angled photographs)
    # Deskewing destroyed 222AAA and 333ccc (perspective distortion)
    # For images with perspective issues, this step does more harm than good
    print(f"    ✓ Deskewing SKIPPED (prevents perspective distortion damage)")

    # Convert back to PIL (GRAYSCALE, not binary!)
    enhanced = image

    # Convert back to original mode if needed
    if original_mode == 'RGB':
        enhanced = enhanced.convert('RGB')

    print(f"    ✓ Output: GRAYSCALE (Tesseract-optimized, NOT binary)")
    return enhanced


def enhance_image_aggressive(image_path: Path) -> Image.Image:
    """
    Apply aggressive enhancement filters (Q2_MediumPoor only).

    INDUSTRY BEST PRACTICES + TESSERACT-OPTIMIZED
    Implements professional image preprocessing pipeline while preserving
    grayscale for ML-based OCR (NO binarization, NO deskewing).

    Enhanced pipeline for severely degraded images:
    1. Convert to grayscale
    2. CLAHE (Contrast Limited Adaptive Histogram Equalization) - adaptive, local
    3. Non-local Means Denoising (content-preserving, aggressive)
    4. Bilateral filtering (edge-preserving, secondary smoothing)
    5. Sharpening (unsharp mask to recover detail)
    6. ❌ NO deskewing (causes perspective distortion damage on angled photographs)
    7. ❌ NO binarization (incompatible with Tesseract ML - destroys gradient info)
    8. ❌ NO morphological operations (can erase signatures/delicate strokes)

    Best Practices Reference:
    - CLAHE: Better than simple contrast boost, handles uneven illumination
    - Non-local Means: Superior denoising for severely degraded scans
    - Bilateral Filter: Edge-preserving, signature-safe
    - No Deskewing: Simple rotation can't fix perspective distortion
    - No Erosion/Dilation: Preserves delicate ink strokes

    Args:
        image_path: Path to degraded image

    Returns:
        Enhanced PIL Image (grayscale, NOT binary)
    """
    print(f"  [AGGRESSIVE - Industry Best Practices] Enhancing: {image_path.name}")

    # Load image
    image = Image.open(image_path)
    original_mode = image.mode

    # Step 1: Convert to grayscale
    if image.mode != 'L':
        image = image.convert('L')
    print(f"    ✓ Converted to grayscale")

    cv2_image = np.array(image)

    # Step 2: CLAHE (Contrast Limited Adaptive Histogram Equalization)
    # Industry best practice for uneven illumination and faint text/signatures
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    clahe_image = clahe.apply(cv2_image)
    print(f"    ✓ CLAHE applied (clipLimit=2.0, tileGridSize=8x8) - adaptive local contrast")

    # Step 3: Non-local Means Denoising
    # Content-preserving aggressive denoising for severely degraded scans
    denoised = cv2.fastNlMeansDenoising(clahe_image, h=10, templateWindowSize=7, searchWindowSize=21)
    print(f"    ✓ Non-local Means denoising (h=10, templateSize=7) - content-preserving")

    # Step 4: Bilateral filtering (edge-preserving secondary smoothing)
    # Signature-safe: preserves sharp features like ink strokes
    bilateral = cv2.bilateralFilter(denoised, d=9, sigmaColor=75, sigmaSpace=75)
    print(f"    ✓ Bilateral filter (d=9, sigmaColor=75) - edge-preserving, signature-safe")

    # Step 5: Sharpening (recover detail lost in denoising)
    # Using unsharp mask technique on bilateral filtered image
    blurred = cv2.GaussianBlur(bilateral, (0, 0), 3)
    sharpened = cv2.addWeighted(bilateral, 1.5, blurred, -0.5, 0)
    print(f"    ✓ Sharpening applied (unsharp mask) - detail recovery")

    # Step 6: Deskewing DISABLED (causes perspective distortion issues)
    # Simple rotation cannot fix perspective-distorted images (angled photographs)
    # Deskewing destroyed 222AAA and 333ccc (perspective distortion)
    # For images with perspective issues, this step does more harm than good
    print(f"    ✓ Deskewing SKIPPED (prevents perspective distortion damage)")

    # Convert back to PIL (GRAYSCALE, not binary!)
    enhanced = Image.fromarray(sharpened)

    print(f"    ✓ Industry best practices pipeline complete")
    print(f"    ✓ Output: GRAYSCALE (Tesseract ML-optimized, signature-safe, NO binarization)")

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
