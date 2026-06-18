"""Unit tests for LogoDetector."""

import pytest
from PIL import Image

from vec_visual_font_identification.models.logo_detection import LogoDetectionResult
from vec_visual_font_identification.visual import LogoDetector


class TestLogoDetector:
    """Test suite for LogoDetector."""

    def test_logo_detector_initialization(self):
        """Test LogoDetector initialization."""
        detector = LogoDetector()
        assert detector is not None
        assert detector.device in ["cuda", "cpu"]

    def test_detect_logos_empty_pdf(self):
        """Test logo detection with empty/invalid PDF."""
        detector = LogoDetector()

        # Create minimal PDF bytes (would need actual PDF structure)
        # For now, test error handling
        with pytest.raises(Exception):  # Would be ValueError or similar
            detector.detect_logos(b"invalid pdf bytes")

    @pytest.mark.skip(reason="Requires actual PDF fixture")
    def test_detect_logos_with_pdf(self):
        """Test logo detection with actual PDF."""
        detector = LogoDetector()

        # Would load actual PDF from fixtures
        # with open("tests/fixtures/sample_pdfs/vec_statement.pdf", "rb") as f:
        #     pdf_bytes = f.read()
        #
        # result = detector.detect_logos(pdf_bytes)
        # assert isinstance(result, LogoDetectionResult)
        # assert result.total_pages > 0

    def test_calculate_overall_quality_score(self):
        """Test overall quality score calculation."""
        detector = LogoDetector()

        # Test with empty logos
        score = detector._calculate_overall_quality_score([])
        assert score == 0.0

        # Would test with actual LogoInfo objects
        # from vec_visual_font_identification.models.logo_detection import LogoInfo, Position, ImageQualityMetrics
        # logos = [LogoInfo(...)]
        # score = detector._calculate_overall_quality_score(logos)
        # assert 0.0 <= score <= 1.0
