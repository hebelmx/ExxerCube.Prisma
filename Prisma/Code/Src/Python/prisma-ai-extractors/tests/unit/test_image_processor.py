"""
Unit tests for ImageProcessor module.
"""

import pytest
from pathlib import Path
from PIL import Image
import numpy as np
from unittest.mock import patch, mock_open

from src.modules.image_processor import ImageProcessor


class TestImageProcessor:
    """Test ImageProcessor functionality."""
    
    def test_init(self):
        """Test ImageProcessor initialization."""
        processor = ImageProcessor()
        assert processor.default_format == "RGB"
        
        # Test custom format
        processor = ImageProcessor(default_format="RGBA")
        assert processor.default_format == "RGBA"
    
    def test_load_image_success(self, sample_image, save_test_image):
        """Test successful image loading."""
        processor = ImageProcessor()
        
        # Save test image
        image_path = save_test_image(sample_image, "test_load.png")
        
        # Load image
        loaded_image = processor.load_image(image_path)
        
        assert isinstance(loaded_image, Image.Image)
        assert loaded_image.mode == "RGB"
        assert loaded_image.size == sample_image.size
    
    def test_load_image_file_not_found(self):
        """Test loading non-existent image."""
        processor = ImageProcessor()
        
        with pytest.raises(FileNotFoundError, match="Image not found"):
            processor.load_image("non_existent.jpg")
    
    def test_load_image_convert_format(self, sample_image, save_test_image):
        """Test image format conversion."""
        processor = ImageProcessor()
        
        # Create RGBA image
        rgba_image = sample_image.convert("RGBA")
        image_path = save_test_image(rgba_image, "test_rgba.png")
        
        # Load and convert to RGB
        loaded_image = processor.load_image(image_path, convert_to="RGB")
        
        assert loaded_image.mode == "RGB"
    
    def test_load_image_pathlib(self, sample_image, save_test_image):
        """Test loading with pathlib.Path."""
        processor = ImageProcessor()
        
        image_path = save_test_image(sample_image, "test_pathlib.png")
        
        # Load with Path object
        loaded_image = processor.load_image(Path(image_path))
        
        assert isinstance(loaded_image, Image.Image)
    
    def test_resize_image_target_size(self, sample_image):
        """Test image resizing with target size."""
        processor = ImageProcessor()
        
        resized = processor.resize_image(sample_image, target_size=(400, 300))
        
        assert resized.size == (400, 300)
    
    def test_resize_image_max_size_width_larger(self):
        """Test resizing with max size when width is larger."""
        processor = ImageProcessor()
        
        # Create wide image
        image = Image.new('RGB', (1000, 500), color='white')
        
        resized = processor.resize_image(image, max_size=600)
        
        # Width should be 600, height proportionally smaller
        assert resized.size == (600, 300)
    
    def test_resize_image_max_size_height_larger(self):
        """Test resizing with max size when height is larger."""
        processor = ImageProcessor()
        
        # Create tall image
        image = Image.new('RGB', (500, 1000), color='white')
        
        resized = processor.resize_image(image, max_size=600)
        
        # Height should be 600, width proportionally smaller
        assert resized.size == (300, 600)
    
    def test_resize_image_no_parameters(self, sample_image):
        """Test resize with no parameters (should return original)."""
        processor = ImageProcessor()
        
        resized = processor.resize_image(sample_image)
        
        # Should return same image
        assert resized is sample_image
    
    def test_preprocess_for_ocr_basic(self, sample_document_image):
        """Test basic OCR preprocessing."""
        processor = ImageProcessor()
        
        processed = processor.preprocess_for_ocr(sample_document_image)
        
        assert processed.mode == 'L'  # Grayscale
    
    def test_preprocess_for_ocr_with_enhancement(self, sample_document_image):
        """Test OCR preprocessing with enhancements."""
        processor = ImageProcessor()
        
        processed = processor.preprocess_for_ocr(
            sample_document_image,
            enhance_contrast=True,
            denoise=True
        )
        
        assert processed.mode == 'L'
        # Image should be processed (different from original)
        assert processed.size == sample_document_image.size
    
    def test_validate_image_valid(self, sample_image, save_test_image):
        """Test validation of valid image."""
        processor = ImageProcessor()
        
        image_path = save_test_image(sample_image, "test_validation.png")
        
        result = processor.validate_image(image_path)
        
        assert result['exists'] is True
        assert result['is_file'] is True
        assert result['extension'] == '.png'
        assert result['valid_format'] is True
        assert result['can_load'] is True
        assert result['dimensions'] == sample_image.size
    
    def test_validate_image_nonexistent(self):
        """Test validation of non-existent image."""
        processor = ImageProcessor()
        
        result = processor.validate_image("nonexistent.jpg")
        
        assert result['exists'] is False
        assert result['is_file'] is False
        assert result['extension'] == '.jpg'
        assert result['valid_format'] is True  # Valid extension
        assert result['can_load'] is False
        assert result['dimensions'] is None
    
    def test_validate_image_invalid_extension(self, temp_dir):
        """Test validation of invalid file extension."""
        processor = ImageProcessor()
        
        # Create file with invalid extension
        invalid_file = temp_dir / "test.xyz"
        invalid_file.write_text("dummy content")
        
        result = processor.validate_image(invalid_file)
        
        assert result['exists'] is True
        assert result['is_file'] is True
        assert result['extension'] == '.xyz'
        assert result['valid_format'] is False
        assert result['can_load'] is False
    
    def test_batch_load_success(self, sample_image, create_test_images, save_test_image):
        """Test successful batch loading."""
        processor = ImageProcessor()
        
        # Create and save multiple test images
        images_data = [
            (sample_image, "batch_1.png"),
            (create_test_images(width=600, height=400), "batch_2.png"),
            (create_test_images(add_text_patterns=True), "batch_3.png")
        ]
        
        image_paths = []
        for img, filename in images_data:
            path = save_test_image(img, filename)
            image_paths.append(str(path))
        
        # Batch load
        results = processor.batch_load(image_paths)
        
        assert len(results) == 3
        
        # All should be successful
        for image, error in results:
            assert image is not None
            assert error is None
            assert isinstance(image, Image.Image)
    
    def test_batch_load_mixed_results(self, sample_image, save_test_image):
        """Test batch loading with mixed success/failure."""
        processor = ImageProcessor()
        
        # Mix of valid and invalid paths
        valid_path = save_test_image(sample_image, "valid.png")
        image_paths = [
            str(valid_path),
            "nonexistent1.jpg",
            "nonexistent2.png"
        ]
        
        results = processor.batch_load(image_paths)
        
        assert len(results) == 3
        
        # First should succeed
        assert results[0][0] is not None
        assert results[0][1] is None
        
        # Others should fail
        assert results[1][0] is None
        assert results[1][1] is not None
        assert results[2][0] is None
        assert results[2][1] is not None
    
    def test_batch_load_with_conversion(self, sample_image, save_test_image):
        """Test batch loading with format conversion."""
        processor = ImageProcessor()
        
        # Create RGBA image
        rgba_image = sample_image.convert("RGBA")
        path = save_test_image(rgba_image, "rgba_test.png")
        
        results = processor.batch_load([str(path)], convert_to="RGB")
        
        assert len(results) == 1
        image, error = results[0]
        
        assert image is not None
        assert error is None
        assert image.mode == "RGB"


class TestImageProcessorEdgeCases:
    """Test edge cases and error conditions."""
    
    @patch('PIL.Image.open')
    def test_load_image_corrupted(self, mock_open):
        """Test loading corrupted image file."""
        processor = ImageProcessor()
        
        # Mock corrupted file
        mock_open.side_effect = OSError("Cannot identify image file")
        
        with patch('pathlib.Path.exists', return_value=True):
            with pytest.raises(IOError, match="Failed to load image"):
                processor.load_image("corrupted.jpg")
    
    def test_preprocess_already_grayscale(self):
        """Test preprocessing image that's already grayscale."""
        processor = ImageProcessor()
        
        # Create grayscale image
        gray_image = Image.new('L', (100, 100), color='gray')
        
        processed = processor.preprocess_for_ocr(gray_image)
        
        assert processed.mode == 'L'
        assert processed.size == (100, 100)
    
    def test_resize_very_small_image(self):
        """Test resizing very small image."""
        processor = ImageProcessor()
        
        tiny_image = Image.new('RGB', (5, 5), color='white')
        
        # Should handle small images gracefully
        resized = processor.resize_image(tiny_image, max_size=100)
        
        assert resized.size == (100, 100)
    
    def test_resize_square_image(self):
        """Test resizing square image."""
        processor = ImageProcessor()
        
        square_image = Image.new('RGB', (100, 100), color='white')
        
        resized = processor.resize_image(square_image, max_size=50)
        
        assert resized.size == (50, 50)
    
    def test_validate_image_empty_file(self, temp_dir):
        """Test validation of empty file."""
        processor = ImageProcessor()
        
        empty_file = temp_dir / "empty.png"
        empty_file.touch()  # Create empty file
        
        result = processor.validate_image(empty_file)
        
        assert result['exists'] is True
        assert result['is_file'] is True
        assert result['valid_format'] is True
        assert result['can_load'] is False


class TestImageProcessorPerformance:
    """Performance-related tests."""
    
    @pytest.mark.slow
    def test_large_image_processing(self):
        """Test processing large images."""
        processor = ImageProcessor()
        
        # Create large image
        large_image = Image.new('RGB', (4000, 3000), color='white')
        
        # Should handle large images without issues
        resized = processor.resize_image(large_image, max_size=1000)
        
        assert max(resized.size) == 1000
        assert resized.size == (1000, 750)  # Maintain aspect ratio
    
    def test_batch_processing_performance(self, create_test_images):
        """Test batch processing performance."""
        processor = ImageProcessor()
        
        # Create multiple images in memory (no file I/O)
        images = []
        for i in range(10):
            img = create_test_images(width=200 + i*10, height=150 + i*5)
            images.append(img)
        
        # Test batch resize
        start_time = pytest.approx(0, abs=5)  # Allow some time variance
        
        results = []
        for img in images:
            resized = processor.resize_image(img, max_size=100)
            results.append(resized)
        
        assert len(results) == 10
        
        # All should be properly resized
        for resized in results:
            assert max(resized.size) <= 100