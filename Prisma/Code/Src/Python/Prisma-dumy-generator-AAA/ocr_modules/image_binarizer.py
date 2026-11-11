"""
Image binarization module for converting grayscale to binary images.
Single Responsibility: Apply adaptive thresholding for text extraction.
"""
import cv2
import numpy as np
from typing import Optional, Literal


def apply_otsu_threshold(grayscale: np.ndarray, invert: bool = True) -> np.ndarray:
    """
    Apply Otsu's automatic thresholding.
    Pure function for global thresholding.
    
    Args:
        grayscale: Grayscale image
        invert: If True, text will be white on black background
        
    Returns:
        Binary image
    """
    flag = cv2.THRESH_BINARY_INV if invert else cv2.THRESH_BINARY
    _, binary = cv2.threshold(grayscale, 0, 255, flag + cv2.THRESH_OTSU)
    return binary


def apply_adaptive_gaussian(grayscale: np.ndarray, 
                          block_size: int = 41,
                          c_value: int = 11,
                          invert: bool = True) -> np.ndarray:
    """
    Apply adaptive Gaussian thresholding.
    Pure function for local adaptive thresholding.
    
    Args:
        grayscale: Grayscale image
        block_size: Size of the pixel neighborhood (must be odd)
        c_value: Constant subtracted from weighted mean
        invert: If True, text will be white on black background
        
    Returns:
        Binary image
    """
    method = cv2.ADAPTIVE_THRESH_GAUSSIAN_C
    thresh_type = cv2.THRESH_BINARY_INV if invert else cv2.THRESH_BINARY
    
    binary = cv2.adaptiveThreshold(
        grayscale, 
        255, 
        method,
        thresh_type, 
        block_size, 
        c_value
    )
    return binary


def apply_niblack_threshold(grayscale: np.ndarray,
                          block_size: int = 41,
                          k_value: float = -0.2,
                          invert: bool = True) -> np.ndarray:
    """
    Apply Niblack's adaptive thresholding (requires cv2.ximgproc).
    Pure function for Niblack thresholding.
    
    Args:
        grayscale: Grayscale image
        block_size: Size of the pixel neighborhood
        k_value: Weight factor for standard deviation
        invert: If True, text will be white on black background
        
    Returns:
        Binary image
        
    Raises:
        ImportError: If cv2.ximgproc is not available
    """
    try:
        thresh_type = cv2.THRESH_BINARY_INV if invert else cv2.THRESH_BINARY
        binary = cv2.ximgproc.niBlackThreshold(
            grayscale, 
            255, 
            thresh_type, 
            block_size, 
            k_value
        )
        return binary
    except AttributeError:
        raise ImportError("Niblack thresholding requires opencv-contrib-python")


def binarize_image(grayscale: np.ndarray,
                  method: Literal["otsu", "adaptive_gaussian", "niblack"] = "adaptive_gaussian",
                  block_size: int = 41,
                  invert: bool = True,
                  fallback_to_gaussian: bool = True) -> np.ndarray:
    """
    Binarize a grayscale image using specified method.
    Pure function: same input produces same output.
    
    Args:
        grayscale: Input grayscale image
        method: Thresholding method to use
        block_size: Block size for adaptive methods
        invert: If True, text will be white on black background
        fallback_to_gaussian: Use Gaussian if Niblack fails
        
    Returns:
        Binary image with text regions
    """
    if method == "otsu":
        return apply_otsu_threshold(grayscale, invert)
    
    elif method == "adaptive_gaussian":
        return apply_adaptive_gaussian(grayscale, block_size, c_value=11, invert=invert)
    
    elif method == "niblack":
        try:
            return apply_niblack_threshold(grayscale, block_size, k_value=-0.2, invert=invert)
        except ImportError:
            if fallback_to_gaussian:
                return apply_adaptive_gaussian(grayscale, block_size, c_value=11, invert=invert)
            raise
    
    else:
        raise ValueError(f"Unknown binarization method: {method}")