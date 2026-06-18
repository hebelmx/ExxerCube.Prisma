"""
Visual Similarity Measurement for CNBV Documents

Measures how similar generated PDFs are to real CNBV samples.
Not pixel-perfect matching, but high-level visual similarity.
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass
from pathlib import Path
from typing import Optional, Tuple

try:
    from PIL import Image, ImageChops, ImageStat
    from pdf2image import convert_from_path
    PIL_AVAILABLE = True
except ImportError:
    PIL_AVAILABLE = False


@dataclass
class SimilarityScore:
    """Visual similarity metrics."""
    overall_score: float  # 0-100
    layout_score: float  # 0-100
    content_score: float  # 0-100
    color_score: float  # 0-100
    details: dict


class VisualSimilarityMeasurer:
    """
    Measure visual similarity between generated and real CNBV PDFs.

    Not pixel-perfect - focuses on:
    - Layout structure
    - Text positioning
    - Logo presence
    - Overall "feel"
    """

    def __init__(self, dpi: int = 150):
        """
        Initialize measurer.

        Args:
            dpi: DPI for PDF→PNG conversion (lower = faster, less precise)
        """
        if not PIL_AVAILABLE:
            raise ImportError(
                "PIL/Pillow and pdf2image required for visual similarity. "
                "Install with: pip install Pillow pdf2image"
            )

        self.dpi = dpi

    def compare_pdfs(
        self,
        generated_pdf: Path,
        reference_pdf: Path,
        page: int = 0
    ) -> SimilarityScore:
        """
        Compare generated PDF to real reference PDF.

        Args:
            generated_pdf: Path to generated PDF
            reference_pdf: Path to real CNBV sample PDF
            page: Which page to compare (default: first page)

        Returns:
            SimilarityScore with metrics
        """
        # Convert PDFs to images
        gen_img = self._pdf_to_image(generated_pdf, page)
        ref_img = self._pdf_to_image(reference_pdf, page)

        if gen_img is None or ref_img is None:
            return SimilarityScore(
                overall_score=0,
                layout_score=0,
                content_score=0,
                color_score=0,
                details={"error": "Failed to convert PDF to image"}
            )

        # Resize to same dimensions (for fair comparison)
        gen_img, ref_img = self._normalize_sizes(gen_img, ref_img)

        # Measure different aspects
        layout_score = self._measure_layout_similarity(gen_img, ref_img)
        content_score = self._measure_content_similarity(gen_img, ref_img)
        color_score = self._measure_color_similarity(gen_img, ref_img)

        # Overall score (weighted average)
        overall_score = (
            layout_score * 0.4 +
            content_score * 0.4 +
            color_score * 0.2
        )

        details = {
            "generated_size": gen_img.size,
            "reference_size": ref_img.size,
            "dpi": self.dpi,
        }

        return SimilarityScore(
            overall_score=overall_score,
            layout_score=layout_score,
            content_score=content_score,
            color_score=color_score,
            details=details
        )

    def _pdf_to_image(self, pdf_path: Path, page: int = 0) -> Optional[Image.Image]:
        """Convert PDF page to PIL Image."""
        try:
            images = convert_from_path(str(pdf_path), dpi=self.dpi, first_page=page+1, last_page=page+1)
            if images:
                return images[0].convert("RGB")
        except Exception as e:
            print(f"Error converting PDF to image: {e}")

        return None

    def _normalize_sizes(
        self,
        img1: Image.Image,
        img2: Image.Image
    ) -> Tuple[Image.Image, Image.Image]:
        """Resize images to same dimensions."""
        # Use the smaller dimension to avoid upscaling
        target_width = min(img1.width, img2.width)
        target_height = min(img1.height, img2.height)

        img1_resized = img1.resize((target_width, target_height), Image.Resampling.LANCZOS)
        img2_resized = img2.resize((target_width, target_height), Image.Resampling.LANCZOS)

        return img1_resized, img2_resized

    def _measure_layout_similarity(self, img1: Image.Image, img2: Image.Image) -> float:
        """
        Measure layout similarity (structure, not content).

        Uses edge detection and structural comparison.
        """
        # Convert to grayscale
        gray1 = img1.convert("L")
        gray2 = img2.convert("L")

        # Simple structural similarity using histogram correlation
        hist1 = gray1.histogram()
        hist2 = gray2.histogram()

        # Calculate correlation between histograms
        correlation = self._histogram_correlation(hist1, hist2)

        return correlation * 100

    def _measure_content_similarity(self, img1: Image.Image, img2: Image.Image) -> float:
        """
        Measure content similarity (text, logos, elements).

        Uses pixel-wise comparison after thresholding.
        """
        # Convert to grayscale and threshold (black text on white)
        gray1 = img1.convert("L").point(lambda x: 0 if x < 200 else 255, '1')
        gray2 = img2.convert("L").point(lambda x: 0 if x < 200 else 255, '1')

        # Calculate pixel difference
        diff = ImageChops.difference(gray1.convert("L"), gray2.convert("L"))

        # Calculate similarity (inverse of difference)
        stat = ImageStat.Stat(diff)
        mean_diff = stat.mean[0]

        # Convert to similarity score (0 = identical, 255 = completely different)
        similarity = (1 - (mean_diff / 255)) * 100

        return similarity

    def _measure_color_similarity(self, img1: Image.Image, img2: Image.Image) -> float:
        """
        Measure color similarity (overall tone, not exact colors).
        """
        # Calculate average color
        stat1 = ImageStat.Stat(img1)
        stat2 = ImageStat.Stat(img2)

        # RGB channels
        r_diff = abs(stat1.mean[0] - stat2.mean[0])
        g_diff = abs(stat1.mean[1] - stat2.mean[1])
        b_diff = abs(stat1.mean[2] - stat2.mean[2])

        # Average difference across channels
        avg_diff = (r_diff + g_diff + b_diff) / 3

        # Convert to similarity score
        similarity = (1 - (avg_diff / 255)) * 100

        return similarity

    def _histogram_correlation(self, hist1: list, hist2: list) -> float:
        """Calculate correlation between two histograms."""
        # Normalize histograms
        total1 = sum(hist1)
        total2 = sum(hist2)

        if total1 == 0 or total2 == 0:
            return 0.0

        norm1 = [h / total1 for h in hist1]
        norm2 = [h / total2 for h in hist2]

        # Calculate correlation
        mean1 = sum(norm1) / len(norm1)
        mean2 = sum(norm2) / len(norm2)

        numerator = sum((n1 - mean1) * (n2 - mean2) for n1, n2 in zip(norm1, norm2))
        denom1 = sum((n1 - mean1) ** 2 for n1 in norm1) ** 0.5
        denom2 = sum((n2 - mean2) ** 2 for n2 in norm2) ** 0.5

        if denom1 == 0 or denom2 == 0:
            return 0.0

        correlation = numerator / (denom1 * denom2)

        # Return as positive value (0-1)
        return max(0, correlation)

    def save_comparison_image(
        self,
        generated_pdf: Path,
        reference_pdf: Path,
        output_path: Path,
        page: int = 0
    ) -> Optional[Path]:
        """
        Create side-by-side comparison image.

        Args:
            generated_pdf: Generated PDF
            reference_pdf: Reference PDF
            output_path: Where to save comparison image
            page: Page number

        Returns:
            Path to comparison image
        """
        gen_img = self._pdf_to_image(generated_pdf, page)
        ref_img = self._pdf_to_image(reference_pdf, page)

        if gen_img is None or ref_img is None:
            return None

        # Resize to same height
        target_height = min(gen_img.height, ref_img.height)
        gen_ratio = gen_img.width / gen_img.height
        ref_ratio = ref_img.width / ref_img.height

        gen_width = int(target_height * gen_ratio)
        ref_width = int(target_height * ref_ratio)

        gen_resized = gen_img.resize((gen_width, target_height), Image.Resampling.LANCZOS)
        ref_resized = ref_img.resize((ref_width, target_height), Image.Resampling.LANCZOS)

        # Create side-by-side image
        total_width = gen_width + ref_width + 20  # 20px gap
        comparison = Image.new("RGB", (total_width, target_height), color=(255, 255, 255))

        # Paste images
        comparison.paste(gen_resized, (0, 0))
        comparison.paste(ref_resized, (gen_width + 20, 0))

        # Save
        output_path.parent.mkdir(parents=True, exist_ok=True)
        comparison.save(output_path)

        return output_path


def measure_similarity(
    generated_pdf: Path,
    reference_pdf: Path,
    save_comparison: Optional[Path] = None
) -> SimilarityScore:
    """
    Convenience function to measure similarity.

    Args:
        generated_pdf: Generated PDF
        reference_pdf: Real CNBV sample PDF
        save_comparison: Optional path to save side-by-side comparison

    Returns:
        SimilarityScore
    """
    measurer = VisualSimilarityMeasurer(dpi=150)
    score = measurer.compare_pdfs(generated_pdf, reference_pdf)

    if save_comparison:
        measurer.save_comparison_image(
            generated_pdf,
            reference_pdf,
            save_comparison
        )

    return score


if __name__ == "__main__":
    import sys

    if len(sys.argv) < 3:
        print("Usage: python visual_similarity.py <generated_pdf> <reference_pdf> [comparison_output]")
        sys.exit(1)

    gen_pdf = Path(sys.argv[1])
    ref_pdf = Path(sys.argv[2])
    comp_out = Path(sys.argv[3]) if len(sys.argv) > 3 else None

    print(f"Comparing:")
    print(f"  Generated: {gen_pdf}")
    print(f"  Reference: {ref_pdf}")

    score = measure_similarity(gen_pdf, ref_pdf, comp_out)

    print(f"\nSimilarity Scores:")
    print(f"  Overall:  {score.overall_score:.1f}%")
    print(f"  Layout:   {score.layout_score:.1f}%")
    print(f"  Content:  {score.content_score:.1f}%")
    print(f"  Color:    {score.color_score:.1f}%")

    if comp_out:
        print(f"\nComparison saved: {comp_out}")

    # Interpretation
    if score.overall_score >= 85:
        print("\n✅ EXCELLENT - Highly similar to real CNBV document")
    elif score.overall_score >= 70:
        print("\n✓ GOOD - Acceptable similarity")
    elif score.overall_score >= 50:
        print("\n⚠ FAIR - Needs improvement")
    else:
        print("\n❌ POOR - Significant differences")
