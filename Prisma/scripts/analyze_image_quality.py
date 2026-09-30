#!/usr/bin/env python3
"""
Adaptive Image Quality Analyzer for OCR Enhancement.

Analyzes image characteristics to determine optimal filter parameters:
- Blur detection (Laplacian variance)
- Noise estimation (high-frequency analysis)
- Contrast measurement (standard deviation)
- Brightness measurement (mean intensity)

Usage:
    python analyze_image_quality.py

Outputs:
    - Console report with quality metrics for all images
    - JSON file with detailed metrics for further analysis
"""

import cv2
import numpy as np
from pathlib import Path
import json
from typing import Dict, Tuple


def estimate_noise_level(image: np.ndarray) -> float:
    """
    Estimate noise level using high-frequency analysis.

    Uses Median Absolute Deviation (MAD) on high-pass filtered image.
    Returns noise sigma estimate.
    """
    # Convert to float for precision
    img_float = image.astype(np.float64)

    # Compute median filter (low-pass)
    median = cv2.medianBlur(image, 5)

    # High-pass = original - low-pass
    high_freq = cv2.absdiff(image, median)

    # Median Absolute Deviation
    sigma = np.median(high_freq) / 0.6745

    return float(sigma)


def analyze_fft_spectrum(image: np.ndarray) -> Dict[str, float]:
    """
    Analyze image frequency spectrum using FFT.

    Returns frequency domain characteristics:
    - high_freq_energy: Energy in high frequencies (edges, text, noise)
    - low_freq_energy: Energy in low frequencies (smooth areas, backgrounds)
    - freq_ratio: High/Low ratio (texture vs smooth)
    - peak_frequency: Dominant periodic frequency (scan artifacts)
    - spectral_entropy: Frequency distribution entropy (complexity measure)
    """
    # Compute 2D FFT
    f_transform = np.fft.fft2(image)
    f_shift = np.fft.fftshift(f_transform)

    # Magnitude spectrum (power)
    magnitude_spectrum = np.abs(f_shift)
    power_spectrum = magnitude_spectrum ** 2

    # Get image dimensions
    rows, cols = image.shape
    crow, ccol = rows // 2, cols // 2

    # Create frequency masks
    # High-pass mask (edges, text, noise)
    radius_low = min(rows, cols) // 10
    high_pass_mask = np.ones((rows, cols), dtype=np.uint8)
    cv2.circle(high_pass_mask, (ccol, crow), radius_low, 0, -1)

    # Low-pass mask (smooth areas, backgrounds)
    low_pass_mask = 1 - high_pass_mask

    # Compute energy in different frequency bands
    high_freq_energy = float(np.sum(power_spectrum * high_pass_mask))
    low_freq_energy = float(np.sum(power_spectrum * low_pass_mask))
    total_energy = float(np.sum(power_spectrum))

    # Frequency ratio (texture indicator)
    freq_ratio = high_freq_energy / low_freq_energy if low_freq_energy > 0 else 0.0

    # Find peak frequency (periodic patterns)
    # Exclude DC component (center)
    center_mask = np.ones((rows, cols), dtype=np.uint8)
    cv2.circle(center_mask, (ccol, crow), 5, 0, -1)
    masked_spectrum = magnitude_spectrum * center_mask

    # Find peak location
    peak_y, peak_x = np.unravel_index(np.argmax(masked_spectrum), masked_spectrum.shape)
    peak_dist = np.sqrt((peak_y - crow)**2 + (peak_x - ccol)**2)
    peak_frequency = float(peak_dist / max(rows, cols))  # Normalized [0, 1]

    # Spectral entropy (frequency distribution complexity)
    # Normalize spectrum to probability distribution
    spectrum_normalized = power_spectrum / total_energy if total_energy > 0 else power_spectrum
    spectrum_normalized = spectrum_normalized[spectrum_normalized > 0]  # Remove zeros
    spectral_entropy = float(-np.sum(spectrum_normalized * np.log2(spectrum_normalized + 1e-10)))

    return {
        'high_freq_energy': high_freq_energy,
        'low_freq_energy': low_freq_energy,
        'freq_ratio': freq_ratio,
        'peak_frequency': peak_frequency,
        'spectral_entropy': spectral_entropy,
        'high_freq_pct': 100.0 * high_freq_energy / total_energy if total_energy > 0 else 0.0,
        'low_freq_pct': 100.0 * low_freq_energy / total_energy if total_energy > 0 else 0.0
    }


def analyze_image_quality(image_path: Path) -> Dict[str, float]:
    """
    Analyze image to determine optimal filter parameters.

    Returns:
        Dictionary with quality metrics:
        - blur_score: Laplacian variance (low = blurry)
        - noise_score: Estimated noise sigma (high = noisy)
        - contrast: Standard deviation (low = poor contrast)
        - brightness: Mean intensity (0-255)
        - fft_*: Frequency domain characteristics (FFT spectrum analysis)
    """
    # Load image
    image = cv2.imread(str(image_path))
    if image is None:
        raise ValueError(f"Failed to load image: {image_path}")

    # Convert to grayscale
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    # 1. Blur detection - Laplacian variance
    # Low variance = blurry, High variance = sharp
    laplacian = cv2.Laplacian(gray, cv2.CV_64F)
    blur_score = laplacian.var()

    # 2. Noise estimation
    noise_score = estimate_noise_level(gray)

    # 3. Contrast measurement
    contrast = gray.std()

    # 4. Brightness measurement
    brightness = gray.mean()

    # 5. FFT spectrum analysis
    fft_metrics = analyze_fft_spectrum(gray)

    # Combine all metrics
    metrics = {
        'blur_score': float(blur_score),
        'noise_score': float(noise_score),
        'contrast': float(contrast),
        'brightness': float(brightness)
    }

    # Add FFT metrics with prefix
    for key, value in fft_metrics.items():
        metrics[f'fft_{key}'] = value

    return metrics


def select_denoising_strength(noise_score: float) -> int:
    """
    Decide denoising parameter (h) based on noise level.

    h parameter for cv2.fastNlMeansDenoising():
    - h=5: Minimal denoising (clean images)
    - h=10: Standard denoising (moderate noise)
    - h=20: Aggressive denoising (heavy noise)
    - h=30: Very aggressive (extreme noise)
    """
    if noise_score > 15:      # Very noisy
        return 30
    elif noise_score > 10:    # Heavy noise
        return 20
    elif noise_score > 5:     # Moderate noise
        return 10
    else:                     # Clean/minimal noise
        return 5


def select_clahe_strength(contrast: float) -> float:
    """
    Decide CLAHE clipLimit parameter based on contrast.

    clipLimit parameter for cv2.createCLAHE():
    - 1.5: Minimal enhancement (good contrast already)
    - 2.0: Standard enhancement (moderate contrast)
    - 2.5: Aggressive enhancement (poor contrast)
    - 3.0: Very aggressive (very poor contrast)
    """
    if contrast < 30:         # Very low contrast
        return 3.0
    elif contrast < 40:       # Low contrast
        return 2.5
    elif contrast < 50:       # Medium contrast
        return 2.0
    else:                     # Good contrast
        return 1.5


def classify_image_quality(metrics: Dict[str, float]) -> str:
    """
    Classify overall image quality based on metrics.

    Returns: "pristine", "high", "medium", "low", "very_low"
    """
    blur = metrics['blur_score']
    noise = metrics['noise_score']
    contrast = metrics['contrast']

    # Pristine: high sharpness, low noise, good contrast
    if blur > 500 and noise < 5 and contrast > 50:
        return "pristine"
    # High quality: decent on all metrics
    elif blur > 200 and noise < 10 and contrast > 40:
        return "high"
    # Medium: moderate degradation
    elif blur > 100 and noise < 15 and contrast > 30:
        return "medium"
    # Low: significant degradation
    elif blur > 50 or noise < 20:
        return "low"
    # Very low: severe degradation
    else:
        return "very_low"


def analyze_all_images():
    """Analyze all PRP1 images and generate report."""

    base_path = Path(__file__).parent.parent

    # Test sets to analyze
    test_sets = [
        ("Pristine Originals", base_path / "Fixtures" / "PRP1"),
        ("Q1_Poor (Degraded)", base_path / "Fixtures" / "PRP1_Degraded" / "Q1_Poor"),
        ("Q2_MediumPoor (Degraded)", base_path / "Fixtures" / "PRP1_Degraded" / "Q2_MediumPoor"),
        ("Q3_Low (Degraded)", base_path / "Fixtures" / "PRP1_Degraded" / "Q3_Low"),
        ("Q4_VeryLow (Degraded)", base_path / "Fixtures" / "PRP1_Degraded" / "Q4_VeryLow"),
    ]

    print("=" * 100)
    print("ADAPTIVE IMAGE QUALITY ANALYZER")
    print("=" * 100)
    print()

    all_results = {}

    for set_name, set_path in test_sets:
        if not set_path.exists():
            print(f"\n⚠ SKIP: {set_name} - Directory not found: {set_path}")
            continue

        print(f"\n{'#' * 100}")
        print(f"# {set_name}")
        print(f"# Path: {set_path}")
        print(f"{'#' * 100}\n")

        set_results = {}

        # Find all image files
        image_files = sorted(list(set_path.glob("*.jpg")) + list(set_path.glob("*.png")))

        if not image_files:
            print(f"⚠ No images found in {set_path}")
            continue

        for img_path in image_files:
            print(f"\n{'─' * 100}")
            print(f"Image: {img_path.name}")
            print(f"{'─' * 100}")

            try:
                # Analyze image
                metrics = analyze_image_quality(img_path)

                # Select recommended parameters
                denoise_h = select_denoising_strength(metrics['noise_score'])
                clahe_clip = select_clahe_strength(metrics['contrast'])
                quality_class = classify_image_quality(metrics)

                # Print metrics
                print(f"  SPATIAL DOMAIN METRICS:")
                print(f"  Blur Score (Laplacian variance):  {metrics['blur_score']:8.2f}  "
                      f"{'(SHARP)' if metrics['blur_score'] > 200 else '(BLURRY)' if metrics['blur_score'] < 100 else '(MODERATE)'}")
                print(f"  Noise Score (sigma estimate):     {metrics['noise_score']:8.2f}  "
                      f"{'(CLEAN)' if metrics['noise_score'] < 5 else '(NOISY)' if metrics['noise_score'] > 10 else '(MODERATE)'}")
                print(f"  Contrast (std deviation):         {metrics['contrast']:8.2f}  "
                      f"{'(GOOD)' if metrics['contrast'] > 50 else '(POOR)' if metrics['contrast'] < 30 else '(MODERATE)'}")
                print(f"  Brightness (mean intensity):      {metrics['brightness']:8.2f}  "
                      f"{'(GOOD)' if 50 < metrics['brightness'] < 200 else '(TOO DARK/BRIGHT)'}")

                print(f"\n  FREQUENCY DOMAIN METRICS (FFT):")
                print(f"  High Freq Energy %:               {metrics['fft_high_freq_pct']:8.2f}%  "
                      f"{'(HIGH DETAIL/TEXT)' if metrics['fft_high_freq_pct'] > 30 else '(LOW DETAIL/SMOOTH)'}")
                print(f"  Low Freq Energy %:                {metrics['fft_low_freq_pct']:8.2f}%  "
                      f"{'(SMOOTH AREAS)' if metrics['fft_low_freq_pct'] > 70 else '(TEXTURED)'}")
                print(f"  Freq Ratio (High/Low):            {metrics['fft_freq_ratio']:8.4f}  "
                      f"{'(TEXTURE-RICH)' if metrics['fft_freq_ratio'] > 0.5 else '(SMOOTH-DOMINANT)'}")
                print(f"  Peak Frequency (normalized):      {metrics['fft_peak_frequency']:8.4f}  "
                      f"{'(PERIODIC ARTIFACTS)' if metrics['fft_peak_frequency'] > 0.1 else '(CLEAN)'}")
                print(f"  Spectral Entropy:                 {metrics['fft_spectral_entropy']:8.2f}  "
                      f"{'(COMPLEX)' if metrics['fft_spectral_entropy'] > 15 else '(SIMPLE)'}")

                print(f"\n  Quality Classification: {quality_class.upper()}")
                print(f"\n  RECOMMENDED FILTER PARAMETERS:")
                print(f"    Denoising strength (h):     {denoise_h}")
                print(f"    CLAHE clip limit:           {clahe_clip}")

                # Store results
                set_results[img_path.name] = {
                    'metrics': metrics,
                    'recommendations': {
                        'denoise_h': denoise_h,
                        'clahe_clip': clahe_clip,
                        'quality_class': quality_class
                    }
                }

            except Exception as e:
                print(f"  ❌ FAILED: {e}")
                set_results[img_path.name] = {'error': str(e)}

        all_results[set_name] = set_results

    # Save detailed results to JSON
    output_file = base_path / "Fixtures" / "image_quality_analysis.json"
    with open(output_file, 'w') as f:
        json.dump(all_results, f, indent=2)

    print(f"\n\n{'=' * 100}")
    print(f"ANALYSIS COMPLETE")
    print(f"{'=' * 100}")
    print(f"Detailed results saved to: {output_file}")
    print()

    # Summary statistics
    print("\nSUMMARY BY QUALITY LEVEL:\n")
    print(f"{'Set Name':<30} {'Avg Blur':<12} {'Avg Noise':<12} {'Avg Contrast':<12} {'Recommendations'}")
    print(f"{'-' * 100}")

    for set_name, results in all_results.items():
        if not results:
            continue

        valid_results = [r for r in results.values() if 'metrics' in r]
        if not valid_results:
            continue

        avg_blur = np.mean([r['metrics']['blur_score'] for r in valid_results])
        avg_noise = np.mean([r['metrics']['noise_score'] for r in valid_results])
        avg_contrast = np.mean([r['metrics']['contrast'] for r in valid_results])

        # Get most common recommendation
        denoise_recs = [r['recommendations']['denoise_h'] for r in valid_results]
        clahe_recs = [r['recommendations']['clahe_clip'] for r in valid_results]
        most_common_denoise = max(set(denoise_recs), key=denoise_recs.count)
        most_common_clahe = max(set(clahe_recs), key=clahe_recs.count)

        print(f"{set_name:<30} {avg_blur:>10.1f}  {avg_noise:>10.2f}  {avg_contrast:>10.2f}  "
              f"h={most_common_denoise}, CLAHE={most_common_clahe}")

    print(f"{'-' * 100}\n")


if __name__ == "__main__":
    analyze_all_images()
