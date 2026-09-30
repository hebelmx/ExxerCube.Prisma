"""
Image Quality Analyzer

Measures objective quality metrics to determine optimal filter strategy:
- Blur detection (variance of Laplacian)
- Noise level (standard deviation in uniform regions)
- Contrast (histogram spread, dynamic range)
- Brightness (mean intensity)
- Quality score (composite metric)

Usage:
    python image_quality_analyzer.py <image_path>
    python image_quality_analyzer.py --batch <directory>
"""

import cv2
import numpy as np
from pathlib import Path
import argparse
import json
from typing import Dict, Tuple


class ImageQualityAnalyzer:
    """Analyzes image quality metrics for OCR optimization."""

    def __init__(self):
        """Initialize quality analyzer."""
        pass

    def measure_blur(self, image: np.ndarray) -> float:
        """
        Measure blur using variance of Laplacian method.

        Higher values = sharper image
        Lower values = more blurred

        Typical ranges:
        - > 500: Very sharp (pristine)
        - 100-500: Sharp (good quality)
        - 50-100: Slightly blurred (acceptable)
        - < 50: Significantly blurred (poor quality)

        Args:
            image: Grayscale image

        Returns:
            Blur score (higher = sharper)
        """
        laplacian = cv2.Laplacian(image, cv2.CV_64F)
        variance = laplacian.var()
        return float(variance)

    def measure_noise(self, image: np.ndarray) -> float:
        """
        Estimate noise level using standard deviation in uniform regions.

        Lower values = less noise
        Higher values = more noise

        Args:
            image: Grayscale image

        Returns:
            Noise estimate (lower = cleaner)
        """
        # Divide image into blocks
        h, w = image.shape
        block_size = 32

        noise_estimates = []

        for y in range(0, h - block_size, block_size):
            for x in range(0, w - block_size, block_size):
                block = image[y:y+block_size, x:x+block_size]

                # Use blocks with low gradient (uniform regions)
                gradient = cv2.Sobel(block, cv2.CV_64F, 1, 1, ksize=3)
                if gradient.std() < 10:  # Uniform region
                    noise_estimates.append(block.std())

        if len(noise_estimates) > 0:
            # Median of noise estimates from uniform regions
            return float(np.median(noise_estimates))
        else:
            # Fallback: overall image std (less accurate)
            return float(image.std())

    def measure_contrast(self, image: np.ndarray) -> Tuple[float, float]:
        """
        Measure contrast using multiple methods.

        Args:
            image: Grayscale image

        Returns:
            (rms_contrast, michelson_contrast)
        """
        # RMS (Root Mean Square) contrast
        mean_intensity = image.mean()
        rms_contrast = float(np.sqrt(((image - mean_intensity) ** 2).mean()))

        # Michelson contrast
        i_max = float(image.max())
        i_min = float(image.min())
        if (i_max + i_min) > 0:
            michelson_contrast = (i_max - i_min) / (i_max + i_min)
        else:
            michelson_contrast = 0.0

        return rms_contrast, michelson_contrast

    def measure_brightness(self, image: np.ndarray) -> Tuple[float, float, float]:
        """
        Measure brightness statistics.

        Args:
            image: Grayscale image

        Returns:
            (mean, median, std)
        """
        return (
            float(image.mean()),
            float(np.median(image)),
            float(image.std())
        )

    def analyze_histogram(self, image: np.ndarray) -> Dict:
        """
        Analyze histogram properties.

        Args:
            image: Grayscale image

        Returns:
            Histogram statistics
        """
        hist = cv2.calcHist([image], [0], None, [256], [0, 256])
        hist = hist.flatten() / hist.sum()  # Normalize

        # Calculate entropy (information content)
        hist_nonzero = hist[hist > 0]
        entropy = -np.sum(hist_nonzero * np.log2(hist_nonzero))

        # Calculate histogram spread (used pixels range)
        used_bins = np.where(hist > 0.001)[0]  # Bins with >0.1% of pixels
        if len(used_bins) > 0:
            dynamic_range = float(used_bins.max() - used_bins.min())
        else:
            dynamic_range = 0.0

        return {
            'entropy': float(entropy),
            'dynamic_range': dynamic_range,
            'bins_used': int(len(used_bins))
        }

    def calculate_quality_score(self, metrics: Dict) -> float:
        """
        Calculate composite quality score (0-100).

        Higher score = better quality (likely pristine, use NO FILTER)
        Lower score = worse quality (needs filtering)

        Scoring:
        - 80-100: Pristine (NO FILTER recommended)
        - 60-80: Good (NO FILTER or light enhancement)
        - 40-60: Fair (moderate enhancement recommended)
        - 20-40: Poor (heavy enhancement recommended - PIL)
        - 0-20: Very poor (aggressive enhancement - PIL)

        Args:
            metrics: Quality metrics dictionary

        Returns:
            Quality score (0-100)
        """
        score = 0.0

        # Blur contribution (0-30 points)
        # Sharp images (>500) get full points
        blur = metrics['blur']
        if blur >= 500:
            score += 30
        elif blur >= 100:
            score += 15 + (blur - 100) / 400 * 15
        elif blur >= 50:
            score += (blur - 50) / 50 * 15

        # Noise contribution (0-25 points)
        # Clean images (<5 noise) get full points
        noise = metrics['noise']
        if noise <= 5:
            score += 25
        elif noise <= 15:
            score += 15 + (15 - noise) / 10 * 10
        elif noise <= 30:
            score += (30 - noise) / 15 * 15

        # Contrast contribution (0-25 points)
        contrast_rms = metrics['contrast_rms']
        if contrast_rms >= 50:
            score += 25
        elif contrast_rms >= 30:
            score += 15 + (contrast_rms - 30) / 20 * 10
        elif contrast_rms >= 15:
            score += (contrast_rms - 15) / 15 * 15

        # Histogram quality contribution (0-20 points)
        entropy = metrics['histogram']['entropy']
        dynamic_range = metrics['histogram']['dynamic_range']

        # Entropy: higher is better (more information)
        if entropy >= 6:
            score += 10
        elif entropy >= 4:
            score += 5 + (entropy - 4) / 2 * 5

        # Dynamic range: wider is better
        if dynamic_range >= 200:
            score += 10
        elif dynamic_range >= 100:
            score += 5 + (dynamic_range - 100) / 100 * 5

        return min(100.0, max(0.0, score))

    def get_quality_category(self, quality_score: float) -> str:
        """
        Categorize quality score.

        Args:
            quality_score: Quality score (0-100)

        Returns:
            Quality category name
        """
        if quality_score >= 80:
            return "PRISTINE"
        elif quality_score >= 60:
            return "GOOD"
        elif quality_score >= 40:
            return "FAIR"
        elif quality_score >= 20:
            return "POOR"
        else:
            return "VERY_POOR"

    def recommend_filter(self, quality_score: float) -> str:
        """
        Recommend filter strategy based on quality score.

        Args:
            quality_score: Quality score (0-100)

        Returns:
            Filter recommendation
        """
        if quality_score >= 80:
            return "NO_FILTER"
        elif quality_score >= 60:
            return "NO_FILTER_OR_LIGHT"
        elif quality_score >= 40:
            return "OPENCV_MODERATE"
        elif quality_score >= 20:
            return "PIL_HEAVY"
        else:
            return "PIL_AGGRESSIVE"

    def analyze_image(self, image_path: Path) -> Dict:
        """
        Perform complete quality analysis on image.

        Args:
            image_path: Path to image file

        Returns:
            Complete analysis results
        """
        # Load image
        img = cv2.imread(str(image_path))
        if img is None:
            raise ValueError(f"Could not load image: {image_path}")

        # Convert to grayscale
        if len(img.shape) == 3:
            gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        else:
            gray = img

        # Measure all metrics
        blur = self.measure_blur(gray)
        noise = self.measure_noise(gray)
        contrast_rms, contrast_michelson = self.measure_contrast(gray)
        brightness_mean, brightness_median, brightness_std = self.measure_brightness(gray)
        histogram_stats = self.analyze_histogram(gray)

        # Build metrics dictionary
        metrics = {
            'blur': blur,
            'noise': noise,
            'contrast_rms': contrast_rms,
            'contrast_michelson': contrast_michelson,
            'brightness_mean': brightness_mean,
            'brightness_median': brightness_median,
            'brightness_std': brightness_std,
            'histogram': histogram_stats,
            'image_size': gray.shape
        }

        # Calculate quality score
        quality_score = self.calculate_quality_score(metrics)
        quality_category = self.get_quality_category(quality_score)
        filter_recommendation = self.recommend_filter(quality_score)

        return {
            'image_path': str(image_path),
            'metrics': metrics,
            'quality_score': quality_score,
            'quality_category': quality_category,
            'filter_recommendation': filter_recommendation
        }


def main():
    """CLI interface for quality analyzer."""
    parser = argparse.ArgumentParser(description='Analyze image quality metrics')
    parser.add_argument('image_path', type=str, nargs='?', help='Path to image file')
    parser.add_argument('--batch', type=str, help='Analyze all images in directory')
    parser.add_argument('--output', type=str, help='Output JSON file path')

    args = parser.parse_args()

    analyzer = ImageQualityAnalyzer()
    results = []

    if args.batch:
        # Batch mode: analyze directory
        batch_dir = Path(args.batch)
        image_files = list(batch_dir.glob('*.jpg')) + list(batch_dir.glob('*.png'))

        print(f"Analyzing {len(image_files)} images in {batch_dir}...")
        print()

        for img_file in sorted(image_files):
            try:
                result = analyzer.analyze_image(img_file)
                results.append(result)

                print(f"{img_file.name}:")
                print(f"  Quality Score: {result['quality_score']:.1f}/100 ({result['quality_category']})")
                print(f"  Recommendation: {result['filter_recommendation']}")
                print(f"  Blur: {result['metrics']['blur']:.1f}")
                print(f"  Noise: {result['metrics']['noise']:.1f}")
                print(f"  Contrast: {result['metrics']['contrast_rms']:.1f}")
                print()
            except Exception as e:
                print(f"  ERROR: {e}")
                print()

    elif args.image_path:
        # Single image mode
        img_path = Path(args.image_path)
        result = analyzer.analyze_image(img_path)
        results.append(result)

        print("=" * 80)
        print(f"IMAGE QUALITY ANALYSIS: {img_path.name}")
        print("=" * 80)
        print()
        print(f"Quality Score: {result['quality_score']:.1f}/100")
        print(f"Quality Category: {result['quality_category']}")
        print(f"Filter Recommendation: {result['filter_recommendation']}")
        print()
        print("METRICS:")
        print("-" * 80)
        print(f"  Blur (Laplacian variance): {result['metrics']['blur']:.2f}")
        print(f"  Noise (std in uniform): {result['metrics']['noise']:.2f}")
        print(f"  Contrast (RMS): {result['metrics']['contrast_rms']:.2f}")
        print(f"  Contrast (Michelson): {result['metrics']['contrast_michelson']:.3f}")
        print(f"  Brightness (mean): {result['metrics']['brightness_mean']:.2f}")
        print(f"  Brightness (median): {result['metrics']['brightness_median']:.2f}")
        print(f"  Brightness (std): {result['metrics']['brightness_std']:.2f}")
        print()
        print("HISTOGRAM:")
        print("-" * 80)
        print(f"  Entropy: {result['metrics']['histogram']['entropy']:.2f}")
        print(f"  Dynamic Range: {result['metrics']['histogram']['dynamic_range']:.0f}")
        print(f"  Bins Used: {result['metrics']['histogram']['bins_used']}")
        print()

    else:
        parser.print_help()
        return

    # Save results if output specified
    if args.output and len(results) > 0:
        with open(args.output, 'w') as f:
            json.dump(results, f, indent=2)
        print(f"Results saved to: {args.output}")


if __name__ == "__main__":
    main()
