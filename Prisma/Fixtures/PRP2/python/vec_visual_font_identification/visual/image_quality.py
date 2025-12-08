"""Image quality analysis for logo and document images."""

import cv2
import numpy as np
from PIL import Image

from vec_visual_font_identification.models.logo_detection import ImageQualityMetrics


class ImageQualityAnalyzer:
    """Analyze image quality metrics for logos and document images."""

    def analyze(self, image: Image.Image) -> ImageQualityMetrics:
        """
        Analyze image quality metrics.

        Args:
            image: PIL Image to analyze

        Returns:
            ImageQualityMetrics with quality scores
        """
        # Convert PIL to OpenCV format
        img_array = np.array(image.convert("RGB"))
        img_cv = cv2.cvtColor(img_array, cv2.COLOR_RGB2BGR)

        # Calculate resolution (DPI estimation)
        dpi = self._estimate_dpi(image)

        # Calculate clarity score (blur detection)
        clarity_score, blur_detected = self._calculate_clarity(img_cv)

        # Calculate contrast ratio
        contrast_ratio = self._calculate_contrast(img_cv)

        # Color accuracy (placeholder - would compare to brand guidelines)
        color_accuracy = 1.0  # Default, would compare to brand color palette

        # Compression artifacts detection
        artifacts_detected = self._detect_compression_artifacts(img_cv)

        return ImageQualityMetrics(
            resolution_dpi=dpi,
            clarity_score=clarity_score,
            contrast_ratio=contrast_ratio,
            color_accuracy_score=color_accuracy,
            compression_artifacts_detected=artifacts_detected,
            blur_detected=blur_detected,
        )

    def _estimate_dpi(self, image: Image.Image) -> float:
        """Estimate DPI from image dimensions."""
        # Default DPI if not available
        dpi = image.info.get("dpi", (72, 72))
        return float(dpi[0])  # Use horizontal DPI

    def _calculate_clarity(self, img: np.ndarray) -> tuple[float, bool]:
        """
        Calculate image clarity using Laplacian variance.

        Returns:
            (clarity_score, blur_detected) tuple
        """
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        laplacian_var = cv2.Laplacian(gray, cv2.CV_64F).var()

        # Normalize to 0-1 range (threshold: 100 for blur detection)
        clarity_score = min(laplacian_var / 500.0, 1.0)
        blur_detected = laplacian_var < 100

        return clarity_score, blur_detected

    def _calculate_contrast(self, img: np.ndarray) -> float:
        """Calculate contrast ratio (WCAG)."""
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

        # Calculate luminance
        mean_luminance = np.mean(gray) / 255.0

        # Calculate standard deviation as contrast measure
        std_dev = np.std(gray) / 255.0

        # Convert to contrast ratio (simplified)
        contrast_ratio = 1.0 + (std_dev * 20.0)  # Scale to reasonable range

        return max(contrast_ratio, 1.0)  # Minimum 1:1

    def _detect_compression_artifacts(self, img: np.ndarray) -> bool:
        """Detect JPEG compression artifacts."""
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

        # Apply DCT to detect block artifacts (simplified)
        # In production, use more sophisticated artifact detection
        edges = cv2.Canny(gray, 50, 150)
        edge_density = np.sum(edges > 0) / (edges.shape[0] * edges.shape[1])

        # High edge density with low clarity suggests compression artifacts
        return edge_density > 0.1 and self._calculate_clarity(img)[0] < 0.5
