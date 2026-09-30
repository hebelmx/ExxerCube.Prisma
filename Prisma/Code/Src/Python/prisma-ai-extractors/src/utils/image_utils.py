"""
Image loading and preprocessing utilities.
"""

from pathlib import Path
from typing import Union, Tuple, Optional
from PIL import Image
import numpy as np


def load_image(image_path: Union[str, Path]) -> Image.Image:
    """
    Load an image from file path.
    
    Args:
        image_path: Path to image file
        
    Returns:
        PIL Image in RGB format
        
    Raises:
        FileNotFoundError: If image file doesn't exist
        IOError: If image cannot be loaded
    """
    image_path = Path(image_path)
    
    if not image_path.exists():
        raise FileNotFoundError(f"Image file not found: {image_path}")
    
    try:
        image = Image.open(image_path)
        # Convert to RGB if necessary
        if image.mode != 'RGB':
            image = image.convert('RGB')
        return image
    except Exception as e:
        raise IOError(f"Failed to load image {image_path}: {e}")


def preprocess_image(
    image: Image.Image,
    target_size: Optional[Tuple[int, int]] = None,
    normalize: bool = False
) -> Union[Image.Image, np.ndarray]:
    """
    Preprocess an image for model input.
    
    Args:
        image: PIL Image
        target_size: Optional target size (width, height)
        normalize: Whether to normalize pixel values
        
    Returns:
        Preprocessed image (PIL Image or numpy array if normalized)
    """
    # Resize if target size specified
    if target_size:
        image = resize_image(image, target_size)
    
    # Normalize if requested
    if normalize:
        # Convert to numpy array
        img_array = np.array(image).astype(np.float32)
        # Normalize to [0, 1]
        img_array = img_array / 255.0
        return img_array
    
    return image


def resize_image(
    image: Image.Image,
    target_size: Tuple[int, int],
    maintain_aspect: bool = True
) -> Image.Image:
    """
    Resize image to target size.
    
    Args:
        image: PIL Image
        target_size: Target size (width, height)
        maintain_aspect: Whether to maintain aspect ratio
        
    Returns:
        Resized image
    """
    if maintain_aspect:
        # Calculate aspect-preserving size
        image.thumbnail(target_size, Image.Resampling.LANCZOS)
        
        # Create new image with target size and paste
        new_image = Image.new('RGB', target_size, (255, 255, 255))
        # Center the image
        x = (target_size[0] - image.width) // 2
        y = (target_size[1] - image.height) // 2
        new_image.paste(image, (x, y))
        return new_image
    else:
        return image.resize(target_size, Image.Resampling.LANCZOS)


def get_image_info(image_path: Union[str, Path]) -> dict:
    """
    Get information about an image file.
    
    Args:
        image_path: Path to image file
        
    Returns:
        Dictionary with image information
    """
    image_path = Path(image_path)
    
    if not image_path.exists():
        return {"error": "File not found"}
    
    try:
        with Image.open(image_path) as img:
            return {
                "filename": image_path.name,
                "format": img.format,
                "mode": img.mode,
                "size": img.size,
                "width": img.width,
                "height": img.height,
                "file_size_bytes": image_path.stat().st_size
            }
    except Exception as e:
        return {"error": str(e)}


def is_valid_image(image_path: Union[str, Path]) -> bool:
    """
    Check if a file is a valid image.
    
    Args:
        image_path: Path to file
        
    Returns:
        True if valid image, False otherwise
    """
    try:
        with Image.open(image_path) as img:
            img.verify()
        return True
    except:
        return False