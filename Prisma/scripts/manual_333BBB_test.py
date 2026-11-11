"""
Manual Testing: 333BBB Document with PIL vs OpenCV Filters

Test both filter types on 333BBB at different quality levels to see practical differences.
"""

import json
import cv2
import numpy as np
import os
import pytesseract
from pathlib import Path
from PIL import Image, ImageEnhance, ImageFilter
import Levenshtein

# Configure Tesseract
pytesseract.pytesseract.tesseract_cmd = r'C:\Program Files\Tesseract-OCR\tesseract.exe'
os.environ['TESSDATA_PREFIX'] = r'C:\Program Files\Tesseract-OCR\tessdata'

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
FIXTURES_DIR = PROJECT_ROOT / "Fixtures"

# Document to test
DOC_FILE = "333BBB-44444444442025_page1.png"
DOC_ID = "333BBB"

# Quality levels to test
TEST_LEVELS = ["Q0_Pristine", "Q1_Poor", "Q2_MediumPoor"]

# Best filters from comprehensive test
BEST_PIL_FILTER = {
    "contrast_factor": 1.514,
    "median_size": 3
}

BEST_OPENCV_FILTER = {
    "denoise_h": 13,
    "clahe_clip": 1.16,
    "bilateral_d": 5,
    "bilateral_sigma_color": 75,
    "bilateral_sigma_space": 75,
    "unsharp_amount": 1.0,
    "unsharp_radius": 1.0
}

TESSERACT_CONFIG = '--psm 6'


def apply_pil_filter(image: np.ndarray, params: dict) -> np.ndarray:
    """Apply PIL enhancement filter."""
    rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    pil_img = Image.fromarray(rgb)
    gray_pil = pil_img.convert('L')

    # Contrast enhancement
    contrast_factor = params.get('contrast_factor', 1.0)
    if contrast_factor != 1.0:
        enhancer = ImageEnhance.Contrast(gray_pil)
        gray_pil = enhancer.enhance(contrast_factor)

    # Median filter
    median_size = params.get('median_size', 3)
    if median_size > 1:
        gray_pil = gray_pil.filter(ImageFilter.MedianFilter(size=median_size))

    return np.array(gray_pil)


def apply_opencv_filter(image: np.ndarray, params: dict) -> np.ndarray:
    """Apply OpenCV enhancement pipeline."""
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # 1. Non-Local Means Denoising
    h = int(params.get('denoise_h', 10))
    denoised = cv2.fastNlMeansDenoising(gray, h=h)

    # 2. CLAHE
    clip_limit = params.get('clahe_clip', 2.0)
    clahe = cv2.createCLAHE(clipLimit=clip_limit, tileGridSize=(8, 8))
    enhanced = clahe.apply(denoised)

    # 3. Bilateral Filter
    d = int(params.get('bilateral_d', 5))
    sigma_color = params.get('bilateral_sigma_color', 75)
    sigma_space = params.get('bilateral_sigma_space', 75)
    bilateral = cv2.bilateralFilter(enhanced, d, sigma_color, sigma_space)

    # 4. Unsharp Mask
    amount = params.get('unsharp_amount', 1.0)
    radius = params.get('unsharp_radius', 1.0)
    blurred = cv2.GaussianBlur(bilateral, (0, 0), radius)
    sharpened = cv2.addWeighted(bilateral, 1.0 + amount, blurred, -amount, 0)

    return sharpened


def load_ground_truth():
    """Load ground truth from pristine document."""
    pristine_path = FIXTURES_DIR / "PRP1" / DOC_FILE
    img = cv2.imread(str(pristine_path))
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    text = pytesseract.image_to_string(gray, config=TESSERACT_CONFIG)
    return text


print("=" * 80)
print(f"MANUAL TEST: {DOC_ID} - PIL vs OpenCV")
print("=" * 80)
print()

# Load ground truth
print("Loading ground truth from pristine document...")
ground_truth = load_ground_truth()
print(f"  ✓ Ground truth: {len(ground_truth)} characters")
print()

# Test on each quality level
for level in TEST_LEVELS:
    print("-" * 80)
    print(f"TESTING: {level}")
    print("-" * 80)

    # Load degraded image
    degraded_path = FIXTURES_DIR / "PRP1_Spectrum" / level / DOC_FILE
    img = cv2.imread(str(degraded_path))

    if img is None:
        print(f"  ERROR: Could not load {degraded_path}")
        continue

    print(f"Image loaded: {img.shape}")
    print()

    # Test NO FILTER (baseline)
    print("1. NO FILTER (baseline):")
    gray_baseline = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    ocr_baseline = pytesseract.image_to_string(gray_baseline, config=TESSERACT_CONFIG)
    edits_baseline = Levenshtein.distance(ground_truth, ocr_baseline)
    print(f"   Levenshtein distance: {edits_baseline} edits")
    print()

    # Test PIL filter
    print(f"2. PIL FILTER (contrast={BEST_PIL_FILTER['contrast_factor']:.3f}, median={BEST_PIL_FILTER['median_size']}):")
    enhanced_pil = apply_pil_filter(img, BEST_PIL_FILTER)
    ocr_pil = pytesseract.image_to_string(enhanced_pil, config=TESSERACT_CONFIG)
    edits_pil = Levenshtein.distance(ground_truth, ocr_pil)
    improvement_pil = edits_baseline - edits_pil
    pct_pil = (improvement_pil / edits_baseline * 100) if edits_baseline > 0 else 0
    print(f"   Levenshtein distance: {edits_pil} edits")
    print(f"   vs Baseline: {improvement_pil:+d} edits ({pct_pil:+.1f}%)")
    print()

    # Test OpenCV filter
    print(f"3. OPENCV FILTER (h={BEST_OPENCV_FILTER['denoise_h']}, CLAHE={BEST_OPENCV_FILTER['clahe_clip']:.2f}):")
    enhanced_opencv = apply_opencv_filter(img, BEST_OPENCV_FILTER)
    ocr_opencv = pytesseract.image_to_string(enhanced_opencv, config=TESSERACT_CONFIG)
    edits_opencv = Levenshtein.distance(ground_truth, ocr_opencv)
    improvement_opencv = edits_baseline - edits_opencv
    pct_opencv = (improvement_opencv / edits_baseline * 100) if edits_baseline > 0 else 0
    print(f"   Levenshtein distance: {edits_opencv} edits")
    print(f"   vs Baseline: {improvement_opencv:+d} edits ({pct_opencv:+.1f}%)")
    print()

    # Compare PIL vs OpenCV
    print("COMPARISON:")
    if edits_pil < edits_opencv:
        diff = edits_opencv - edits_pil
        pct = (diff / edits_opencv * 100)
        print(f"  ✓ PIL WINS by {diff} edits ({pct:.1f}% better)")
    elif edits_opencv < edits_pil:
        diff = edits_pil - edits_opencv
        pct = (diff / edits_pil * 100)
        print(f"  ✓ OPENCV WINS by {diff} edits ({pct:.1f}% better)")
    else:
        print(f"  = TIE ({edits_pil} edits)")
    print()

print("=" * 80)
print("TEST COMPLETE")
print("=" * 80)
