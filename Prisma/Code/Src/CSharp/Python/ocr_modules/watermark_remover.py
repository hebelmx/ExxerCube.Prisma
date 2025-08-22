"""
Watermark removal module for removing red diagonal watermarks.
Single Responsibility: Remove specific color-based watermarks from images.
"""
import cv2
import numpy as np
from typing import Tuple


def create_red_mask(hsv_image: np.ndarray, 
                   hue_lower1: int = 0, 
                   hue_upper1: int = 10,
                   hue_lower2: int = 170, 
                   hue_upper2: int = 180,
                   saturation_threshold: int = 80,
                   value_threshold: int = 80) -> np.ndarray:
    """
    Create a binary mask for red regions in HSV color space.
    Pure function: deterministic output for given input.
    
    Args:
        hsv_image: Image in HSV color space
        hue_lower1: Lower bound for red hue (around 0)
        hue_upper1: Upper bound for red hue (around 0)
        hue_lower2: Lower bound for red hue (around 180)
        hue_upper2: Upper bound for red hue (around 180)
        saturation_threshold: Minimum saturation for red detection
        value_threshold: Minimum value (brightness) for red detection
        
    Returns:
        Binary mask where red regions are white (255)
    """
    h, s, v = cv2.split(hsv_image)
    
    # Red wraps around in HSV, so we need two ranges
    mask1 = (h < hue_upper1) & (s > saturation_threshold) & (v > value_threshold)
    mask2 = (h > hue_lower2) & (s > saturation_threshold) & (v > value_threshold)
    
    return ((mask1 | mask2).astype(np.uint8) * 255)


def dilate_mask(mask: np.ndarray, kernel_size: int = 3, iterations: int = 1) -> np.ndarray:
    """
    Dilate a binary mask to cover thin strokes.
    Pure function for morphological dilation.
    
    Args:
        mask: Binary mask
        kernel_size: Size of the dilation kernel
        iterations: Number of dilation iterations
        
    Returns:
        Dilated binary mask
    """
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (kernel_size, kernel_size))
    return cv2.dilate(mask, kernel, iterations=iterations)


def inpaint_masked_regions(image: np.ndarray, mask: np.ndarray, radius: int = 3) -> np.ndarray:
    """
    Inpaint masked regions using nearby pixels.
    Pure function that fills masked areas.
    
    Args:
        image: Original image (BGR)
        mask: Binary mask of regions to inpaint
        radius: Inpainting radius
        
    Returns:
        Inpainted image
    """
    return cv2.inpaint(image, mask, radius, cv2.INPAINT_TELEA)


def remove_red_watermark(image: np.ndarray,
                        hue_thresholds: Tuple[Tuple[int, int], Tuple[int, int]] = ((0, 10), (170, 180)),
                        saturation_threshold: int = 80,
                        value_threshold: int = 80,
                        dilate_kernel_size: int = 3,
                        dilate_iterations: int = 1,
                        inpaint_radius: int = 3) -> np.ndarray:
    """
    Remove red watermark from an image.
    Pure function: same input always produces same output.
    
    Args:
        image: Input image in BGR format
        hue_thresholds: Two tuples for red hue ranges
        saturation_threshold: Minimum saturation for red detection
        value_threshold: Minimum value for red detection
        dilate_kernel_size: Kernel size for mask dilation
        dilate_iterations: Number of dilation iterations
        inpaint_radius: Radius for inpainting
        
    Returns:
        Image with red watermark removed
    """
    # Convert to HSV for better color detection
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    
    # Create mask for red regions
    mask = create_red_mask(
        hsv,
        hue_lower1=hue_thresholds[0][0],
        hue_upper1=hue_thresholds[0][1],
        hue_lower2=hue_thresholds[1][0],
        hue_upper2=hue_thresholds[1][1],
        saturation_threshold=saturation_threshold,
        value_threshold=value_threshold
    )
    
    # Dilate mask to cover thin strokes
    mask = dilate_mask(mask, dilate_kernel_size, dilate_iterations)
    
    # Inpaint red regions
    cleaned = inpaint_masked_regions(image, mask, inpaint_radius)
    
    return cleaned