"""
Tests for image processing modules.
"""
import pytest
import numpy as np
import cv2

from ..watermark_remover import (
    create_red_mask, dilate_mask, inpaint_masked_regions, remove_red_watermark
)
from ..image_deskewer import (
    detect_skew_angle, create_rotation_matrix, apply_rotation, deskew_image
)
from ..image_binarizer import (
    apply_otsu_threshold, apply_adaptive_gaussian, binarize_image
)


class TestWatermarkRemover:
    def create_test_image(self):
        """Create a test image with red regions."""
        img = np.zeros((100, 100, 3), dtype=np.uint8)
        # Add some red regions
        img[20:30, 20:30] = [0, 0, 255]  # Red square in BGR
        img[50:60, 50:60] = [100, 100, 100]  # Gray square
        return img

    def test_create_red_mask(self):
        """Test creating red mask."""
        img = self.create_test_image()
        hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
        mask = create_red_mask(hsv)
        
        assert mask.dtype == np.uint8
        assert mask.shape == (100, 100)
        # Should detect some red regions
        assert np.sum(mask > 0) > 0

    def test_dilate_mask(self):
        """Test mask dilation."""
        mask = np.zeros((50, 50), dtype=np.uint8)
        mask[20:25, 20:25] = 255  # Small white square
        
        dilated = dilate_mask(mask, kernel_size=3, iterations=1)
        
        assert dilated.shape == mask.shape
        # Dilated mask should be larger
        assert np.sum(dilated > 0) > np.sum(mask > 0)

    def test_inpaint_masked_regions(self):
        """Test inpainting masked regions."""
        img = self.create_test_image()
        mask = np.zeros((100, 100), dtype=np.uint8)
        mask[20:30, 20:30] = 255  # Mask the red region
        
        inpainted = inpaint_masked_regions(img, mask)
        
        assert inpainted.shape == img.shape
        assert inpainted.dtype == img.dtype
        # Should be different from original
        assert not np.array_equal(img, inpainted)

    def test_remove_red_watermark(self):
        """Test complete red watermark removal."""
        img = self.create_test_image()
        
        cleaned = remove_red_watermark(img)
        
        assert cleaned.shape == img.shape
        assert cleaned.dtype == img.dtype
        # Should be different from original
        assert not np.array_equal(img, cleaned)


class TestImageDeskewer:
    def create_skewed_text_image(self):
        """Create a simple skewed text-like image."""
        img = np.zeros((100, 200), dtype=np.uint8)
        # Add some horizontal lines (simulating text)
        img[20:25, 10:180] = 255
        img[40:45, 15:175] = 255
        img[60:65, 20:170] = 255
        return img

    def test_detect_skew_angle(self):
        """Test skew angle detection."""
        # Create a binary image with text-like content
        binary_img = self.create_skewed_text_image()
        
        angle = detect_skew_angle(binary_img)
        
        assert isinstance(angle, float)
        # For horizontal text, angle should be small
        assert -45 <= angle <= 45

    def test_create_rotation_matrix(self):
        """Test rotation matrix creation."""
        matrix = create_rotation_matrix((100, 200), 5.0)
        
        assert matrix.shape == (2, 3)
        assert matrix.dtype == np.float64

    def test_apply_rotation(self):
        """Test applying rotation to image."""
        img = self.create_skewed_text_image()
        matrix = create_rotation_matrix(img.shape, 5.0)
        
        rotated = apply_rotation(img, matrix)
        
        assert rotated.shape == img.shape
        assert rotated.dtype == img.dtype

    def test_deskew_image(self):
        """Test complete image deskewing."""
        img = self.create_skewed_text_image()
        
        deskewed = deskew_image(img)
        
        assert deskewed.shape == img.shape
        assert deskewed.dtype == img.dtype

    def test_deskew_with_threshold(self):
        """Test deskewing doesn't rotate for small angles."""
        img = self.create_skewed_text_image()
        
        # With high threshold, should not rotate
        result = deskew_image(img, angle_threshold=45.0)
        
        # Should be identical or very similar to input
        assert result.shape == img.shape


class TestImageBinarizer:
    def create_test_grayscale(self):
        """Create a test grayscale image."""
        img = np.zeros((100, 100), dtype=np.uint8)
        # Add some text-like regions
        img[20:40, 20:80] = 200  # Light gray "text"
        img[50:70, 30:70] = 180
        # Add some background noise
        img[10:90, 10:90] += np.random.randint(0, 50, (80, 80))
        return img.astype(np.uint8)

    def test_apply_otsu_threshold(self):
        """Test Otsu thresholding."""
        gray = self.create_test_grayscale()
        
        binary = apply_otsu_threshold(gray, invert=True)
        
        assert binary.shape == gray.shape
        assert binary.dtype == np.uint8
        # Should only contain 0 and 255
        unique_values = np.unique(binary)
        assert len(unique_values) <= 2
        assert 0 in unique_values or 255 in unique_values

    def test_apply_adaptive_gaussian(self):
        """Test adaptive Gaussian thresholding."""
        gray = self.create_test_grayscale()
        
        binary = apply_adaptive_gaussian(gray, block_size=21, c_value=10, invert=True)
        
        assert binary.shape == gray.shape
        assert binary.dtype == np.uint8
        # Should only contain 0 and 255
        unique_values = np.unique(binary)
        assert len(unique_values) <= 2

    def test_binarize_image_otsu(self):
        """Test binarization with Otsu method."""
        gray = self.create_test_grayscale()
        
        binary = binarize_image(gray, method="otsu")
        
        assert binary.shape == gray.shape
        assert binary.dtype == np.uint8

    def test_binarize_image_adaptive(self):
        """Test binarization with adaptive method."""
        gray = self.create_test_grayscale()
        
        binary = binarize_image(gray, method="adaptive_gaussian")
        
        assert binary.shape == gray.shape
        assert binary.dtype == np.uint8

    def test_binarize_image_niblack_fallback(self):
        """Test Niblack with fallback to Gaussian."""
        gray = self.create_test_grayscale()
        
        # Should fallback to Gaussian if Niblack not available
        binary = binarize_image(gray, method="niblack", fallback_to_gaussian=True)
        
        assert binary.shape == gray.shape
        assert binary.dtype == np.uint8

    def test_invalid_method(self):
        """Test error for invalid method."""
        gray = self.create_test_grayscale()
        
        with pytest.raises(ValueError):
            binarize_image(gray, method="invalid_method")