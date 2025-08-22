"""
Image deskewing module for correcting document rotation.
Single Responsibility: Detect and correct skew in document images.
"""
import cv2
import numpy as np
from typing import Tuple, Optional


def detect_skew_angle(binary_image: np.ndarray) -> float:
    """
    Detect the skew angle of a document using minimum area rectangle.
    Pure function for angle detection.
    
    Args:
        binary_image: Binary image (text as white on black background)
        
    Returns:
        Skew angle in degrees
    """
    # Find all white pixels (text)
    coords = np.column_stack(np.where(binary_image > 0))
    
    if coords.size == 0:
        return 0.0
    
    # Find minimum area rectangle
    rect = cv2.minAreaRect(coords)
    angle = rect[-1]
    
    # Correct angle orientation
    if angle < -45:
        angle = 90 + angle
        
    return angle


def create_rotation_matrix(image_shape: Tuple[int, int], angle: float) -> np.ndarray:
    """
    Create a rotation matrix for the given angle.
    Pure function for matrix creation.
    
    Args:
        image_shape: (height, width) of the image
        angle: Rotation angle in degrees
        
    Returns:
        2x3 rotation matrix
    """
    height, width = image_shape[:2]
    center = (width // 2, height // 2)
    return cv2.getRotationMatrix2D(center, angle, 1.0)


def apply_rotation(image: np.ndarray, rotation_matrix: np.ndarray) -> np.ndarray:
    """
    Apply rotation to an image using the rotation matrix.
    Pure function for image transformation.
    
    Args:
        image: Input image
        rotation_matrix: 2x3 rotation matrix
        
    Returns:
        Rotated image
    """
    height, width = image.shape[:2]
    return cv2.warpAffine(
        image, 
        rotation_matrix, 
        (width, height),
        flags=cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_REPLICATE
    )


def deskew_image(image: np.ndarray, 
                angle_threshold: float = 0.5,
                binary_threshold: Optional[int] = None) -> np.ndarray:
    """
    Deskew a document image by detecting and correcting rotation.
    Pure function: deterministic output for given input.
    
    Args:
        image: Input image (grayscale)
        angle_threshold: Minimum angle to correct (degrees)
        binary_threshold: Threshold for binarization (None for OTSU)
        
    Returns:
        Deskewed image
    """
    # Create binary image for angle detection
    if binary_threshold is None:
        _, binary = cv2.threshold(image, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    else:
        _, binary = cv2.threshold(image, binary_threshold, 255, cv2.THRESH_BINARY_INV)
    
    # Detect skew angle
    angle = detect_skew_angle(binary)
    
    # Only correct if angle is significant
    if abs(angle) < angle_threshold:
        return image.copy()
    
    # Create and apply rotation
    rotation_matrix = create_rotation_matrix(image.shape, angle)
    return apply_rotation(image, rotation_matrix)