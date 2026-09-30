#!/usr/bin/env python3
"""
Unit tests for got_ocr2_wrapper.py

Tests the Python OCR wrapper functionality before C# integration.
"""

import io
import os
import sys
from pathlib import Path

import pytest
from PIL import Image

# Add parent directory to path to import got_ocr2_wrapper
sys.path.insert(0, str(Path(__file__).parent.parent))

from got_ocr2_wrapper import (
    execute_ocr,
    execute_ocr_from_file,
    calculate_confidence_heuristic,
    get_version,
    get_model_info,
    health_check,
    is_cuda_supported,
    DEVICE,
    HAS_CUDA
)

# -------------------------------
# Test Fixtures
# -------------------------------
@pytest.fixture
def sample_image_bytes():
    """Create a simple test image as bytes"""
    img = Image.new('RGB', (100, 50), color='white')
    img_bytes = io.BytesIO()
    img.save(img_bytes, format='PNG')
    return img_bytes.getvalue()

@pytest.fixture
def sample_text_image_bytes():
    """Create an image with text for OCR testing"""
    # Note: This is a white image without text
    # In real tests, you'd use an image with actual text
    img = Image.new('RGB', (400, 200), color='white')
    img_bytes = io.BytesIO()
    img.save(img_bytes, format='PNG')
    return img_bytes.getvalue()

# -------------------------------
# Device and Configuration Tests
# -------------------------------
def test_device_configuration():
    """Test device configuration is set correctly"""
    assert DEVICE in ["cuda", "cpu"]
    assert isinstance(HAS_CUDA, bool)
    print(f"Device: {DEVICE}, CUDA available: {HAS_CUDA}")

def test_is_cuda_supported():
    """Test CUDA support detection"""
    result = is_cuda_supported()
    assert isinstance(result, bool)

# -------------------------------
# Module Info Tests
# -------------------------------
def test_get_version():
    """Test version string is returned"""
    version = get_version()
    assert isinstance(version, str)
    assert len(version) > 0
    assert version == "1.0.0"

def test_get_model_info():
    """Test model info is returned"""
    info = get_model_info()
    assert isinstance(info, str)
    assert "GOT-OCR2" in info
    assert DEVICE in info

# -------------------------------
# Health Check Tests
# -------------------------------
def test_health_check():
    """Test health check can run"""
    # Note: This will download the model on first run (~5GB)
    # Skip in CI environments without model access
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    result = health_check()
    assert isinstance(result, bool)
    # Health check should pass if model loads
    assert result is True

# -------------------------------
# Confidence Heuristic Tests
# -------------------------------
def test_calculate_confidence_heuristic_empty():
    """Test confidence calculation with empty text"""
    confidence = calculate_confidence_heuristic("", 0.7)
    assert confidence == 0.0

def test_calculate_confidence_heuristic_short_text():
    """Test confidence calculation with short text"""
    confidence = calculate_confidence_heuristic("Hello", 0.7)
    assert 0.0 <= confidence <= 100.0

def test_calculate_confidence_heuristic_spanish_text():
    """Test confidence calculation with Spanish text"""
    text = "Este es un documento de prueba en español con palabras comunes"
    confidence = calculate_confidence_heuristic(text, 0.7)
    assert 0.0 <= confidence <= 100.0
    assert confidence > 0.0  # Should have non-zero confidence

def test_calculate_confidence_heuristic_english_text():
    """Test confidence calculation with English text"""
    text = "This is a test document in English with common words"
    confidence = calculate_confidence_heuristic(text, 0.7)
    assert 0.0 <= confidence <= 100.0
    assert confidence > 0.0

def test_calculate_confidence_heuristic_garbage():
    """Test confidence calculation with garbage text"""
    text = "xyzabc123!@#$%^&*()"
    confidence = calculate_confidence_heuristic(text, 0.7)
    assert 0.0 <= confidence <= 100.0

# -------------------------------
# OCR Execution Tests
# -------------------------------
def test_execute_ocr_basic(sample_image_bytes):
    """Test basic OCR execution with image bytes"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    text, avg, median, confidences, lang = execute_ocr(
        sample_image_bytes,
        language="spa",
        confidence_threshold=0.7
    )

    # Validate return types
    assert isinstance(text, str)
    assert isinstance(avg, float)
    assert isinstance(median, float)
    assert isinstance(confidences, list)
    assert isinstance(lang, str)

    # Validate ranges
    assert 0.0 <= avg <= 100.0
    assert 0.0 <= median <= 100.0
    assert len(confidences) > 0
    assert all(0.0 <= c <= 100.0 for c in confidences)
    assert lang == "spa"

def test_execute_ocr_english(sample_image_bytes):
    """Test OCR with English language setting"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    text, avg, median, confidences, lang = execute_ocr(
        sample_image_bytes,
        language="eng",
        confidence_threshold=0.5
    )

    assert isinstance(text, str)
    assert lang == "eng"

def test_execute_ocr_invalid_image_bytes():
    """Test OCR with invalid image bytes"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    invalid_bytes = b"not an image"

    text, avg, median, confidences, lang = execute_ocr(
        invalid_bytes,
        language="spa"
    )

    # Should return empty result on error
    assert text == ""
    assert avg == 0.0
    assert median == 0.0
    assert confidences == [0.0]

def test_execute_ocr_empty_bytes():
    """Test OCR with empty bytes"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    text, avg, median, confidences, lang = execute_ocr(
        b"",
        language="spa"
    )

    # Should return empty result
    assert text == ""
    assert avg == 0.0

# -------------------------------
# File-based OCR Tests
# -------------------------------
def test_execute_ocr_from_file_not_found():
    """Test OCR from non-existent file"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    text, avg, median, confidences, lang = execute_ocr_from_file(
        "/nonexistent/file.png",
        language="spa"
    )

    # Should return empty result
    assert text == ""
    assert avg == 0.0
    assert median == 0.0

def test_execute_ocr_from_real_fixture():
    """Test OCR with real PDF fixture from PRP1"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    # Look for any JPG/PNG/PDF file in the PythonOcrLib directory (parent of tests/)
    fixtures_dir = Path(__file__).parent.parent

    jpg_files = list(fixtures_dir.glob("*.jpg"))
    png_files = list(fixtures_dir.glob("*.png"))
    pdf_files = list(fixtures_dir.glob("*.pdf"))

    if not jpg_files and not png_files and not pdf_files:
        pytest.skip("No image fixtures found in PythonOcrLib")

    # Prefer JPG/PNG over PDF for OCR tests
    if jpg_files:
        test_file = jpg_files[0]
    elif png_files:
        test_file = png_files[0]
    else:
        test_file = pdf_files[0]

    text, avg, median, confidences, lang = execute_ocr_from_file(
        str(test_file),
        language="spa",
        confidence_threshold=0.7
    )

    # Real documents should produce some text
    assert isinstance(text, str)
    print(f"\nExtracted text length: {len(text)}")
    print(f"Confidence avg: {avg:.2f}")
    print(f"Text preview: {text[:100]}...")

# -------------------------------
# Integration Test
# -------------------------------
def test_full_pipeline_integration(sample_text_image_bytes):
    """Test full OCR pipeline from bytes to result"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    # Execute OCR
    text, avg, median, confidences, lang = execute_ocr(
        sample_text_image_bytes,
        language="spa",
        confidence_threshold=0.7
    )

    # Validate complete result structure
    assert isinstance(text, str)
    assert isinstance(avg, float) and 0.0 <= avg <= 100.0
    assert isinstance(median, float) and 0.0 <= median <= 100.0
    assert isinstance(confidences, list)
    assert isinstance(lang, str) and lang == "spa"

    # This should match the IOcrExecutor contract
    print(f"\nOCR Result:")
    print(f"  Text length: {len(text)}")
    print(f"  Confidence avg: {avg:.2f}")
    print(f"  Confidence median: {median:.2f}")
    print(f"  Confidence count: {len(confidences)}")
    print(f"  Language: {lang}")

# -------------------------------
# Performance Marker Tests
# -------------------------------
@pytest.mark.slow
def test_ocr_performance(sample_image_bytes):
    """Test OCR execution performance (marked as slow)"""
    if os.getenv("SKIP_MODEL_TESTS") == "1":
        pytest.skip("Skipping model test (SKIP_MODEL_TESTS=1)")

    import time

    start = time.time()
    execute_ocr(sample_image_bytes, language="spa")
    duration = time.time() - start

    print(f"\nOCR execution time: {duration:.2f}s")
    # First run includes model loading, subsequent runs should be faster
    assert duration < 60.0  # Should complete within 60 seconds

# -------------------------------
# Run tests
# -------------------------------
if __name__ == "__main__":
    pytest.main([__file__, "-v", "--tb=short"])
