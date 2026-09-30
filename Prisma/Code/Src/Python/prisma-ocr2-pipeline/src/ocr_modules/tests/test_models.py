"""
Tests for Pydantic models.
"""
import pytest
import numpy as np
from pydantic import ValidationError

from ..models import (
    ImageData, OCRConfig, OCRResult, AmountData, ExtractedFields,
    ProcessingConfig, OutputData, ProcessingResult
)


class TestImageData:
    def test_valid_image_data(self):
        """Test creating valid ImageData."""
        img_data = np.zeros((100, 100, 3), dtype=np.uint8)
        image = ImageData(
            data=img_data,
            source_path="/path/to/image.jpg",
            page_number=1,
            total_pages=1
        )
        assert image.data.shape == (100, 100, 3)
        assert image.source_path == "/path/to/image.jpg"
        assert image.page_number == 1
        assert image.total_pages == 1

    def test_invalid_image_data(self):
        """Test validation with invalid image data."""
        with pytest.raises(ValidationError):
            ImageData(
                data="not an array",
                source_path="/path/to/image.jpg"
            )

    def test_grayscale_image(self):
        """Test with grayscale image."""
        img_data = np.zeros((100, 100), dtype=np.uint8)
        image = ImageData(
            data=img_data,
            source_path="/path/to/image.jpg"
        )
        assert image.data.shape == (100, 100)


class TestOCRConfig:
    def test_default_config(self):
        """Test default OCR configuration."""
        config = OCRConfig()
        assert config.language == "spa"
        assert config.oem == 1
        assert config.psm == 6
        assert config.fallback_language == "eng"

    def test_custom_config(self):
        """Test custom OCR configuration."""
        config = OCRConfig(
            language="eng",
            oem=3,
            psm=8,
            fallback_language="fra"
        )
        assert config.language == "eng"
        assert config.oem == 3
        assert config.psm == 8
        assert config.fallback_language == "fra"

    def test_invalid_language(self):
        """Test invalid language format."""
        with pytest.raises(ValidationError):
            OCRConfig(language="spanish")  # Should be 3-letter code


class TestOCRResult:
    def test_valid_ocr_result(self):
        """Test creating valid OCR result."""
        result = OCRResult(
            text="Sample text",
            confidence_avg=85.5,
            confidence_median=87.0,
            confidences=[80, 85, 90, 85],
            language_used="spa"
        )
        assert result.text == "Sample text"
        assert result.confidence_avg == 85.5
        assert result.confidence_median == 87.0
        assert len(result.confidences) == 4
        assert result.language_used == "spa"

    def test_confidence_validation(self):
        """Test confidence values are clamped to 0-100."""
        result = OCRResult(
            text="Test",
            confidence_avg=85.0,
            confidence_median=85.0,
            confidences=[-10, 50, 150, 80],
            language_used="eng"
        )
        # Confidences should be clamped
        assert all(0 <= conf <= 100 for conf in result.confidences)


class TestAmountData:
    def test_valid_amount(self):
        """Test creating valid amount data."""
        amount = AmountData(
            currency="USD",
            value=1500.75,
            original_text="$1,500.75"
        )
        assert amount.currency == "USD"
        assert amount.value == 1500.75
        assert amount.original_text == "$1,500.75"

    def test_default_currency(self):
        """Test default currency."""
        amount = AmountData(value=100.0, original_text="$100")
        assert amount.currency == "MXN"

    def test_negative_value(self):
        """Test negative value is rejected."""
        with pytest.raises(ValidationError):
            AmountData(value=-100.0, original_text="-$100")


class TestExtractedFields:
    def test_empty_fields(self):
        """Test creating empty extracted fields."""
        fields = ExtractedFields()
        assert fields.expediente is None
        assert fields.causa is None
        assert fields.accion_solicitada is None
        assert fields.fechas == []
        assert fields.montos == []

    def test_fields_with_data(self):
        """Test fields with data."""
        amount = AmountData(value=500.0, original_text="$500")
        fields = ExtractedFields(
            expediente="EXP-123/2023",
            causa="Test cause",
            accion_solicitada="Test action",
            fechas=["2023-10-15"],
            montos=[amount]
        )
        assert fields.expediente == "EXP-123/2023"
        assert fields.causa == "Test cause"
        assert fields.accion_solicitada == "Test action"
        assert len(fields.fechas) == 1
        assert len(fields.montos) == 1

    def test_date_validation(self):
        """Test date format validation."""
        fields = ExtractedFields(
            fechas=["2023-10-15", "invalid-date", "2023-12-01"]
        )
        # Should only keep valid dates
        assert len(fields.fechas) == 2
        assert "2023-10-15" in fields.fechas
        assert "2023-12-01" in fields.fechas
        assert "invalid-date" not in fields.fechas


class TestProcessingConfig:
    def test_default_processing_config(self):
        """Test default processing configuration."""
        config = ProcessingConfig()
        assert config.remove_watermark is True
        assert config.deskew is True
        assert config.binarize is True
        assert config.extract_sections is True
        assert config.normalize_text is True
        assert isinstance(config.ocr_config, OCRConfig)

    def test_custom_processing_config(self):
        """Test custom processing configuration."""
        ocr_config = OCRConfig(language="eng")
        config = ProcessingConfig(
            remove_watermark=False,
            deskew=False,
            ocr_config=ocr_config
        )
        assert config.remove_watermark is False
        assert config.deskew is False
        assert config.ocr_config.language == "eng"


class TestProcessingResult:
    def test_valid_processing_result(self):
        """Test creating valid processing result."""
        ocr_result = OCRResult(
            text="Test text",
            confidence_avg=85.0,
            confidence_median=85.0,
            confidences=[85],
            language_used="spa"
        )
        fields = ExtractedFields()
        
        result = ProcessingResult(
            source_path="/path/to/file.jpg",
            page_number=1,
            ocr_result=ocr_result,
            extracted_fields=fields
        )
        
        assert result.source_path == "/path/to/file.jpg"
        assert result.page_number == 1
        assert result.ocr_result == ocr_result
        assert result.extracted_fields == fields
        assert result.output_path is None
        assert result.processing_errors == []