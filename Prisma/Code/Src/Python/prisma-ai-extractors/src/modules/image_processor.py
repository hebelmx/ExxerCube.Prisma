"""
Image Processor Module - Single Responsibility: Image loading and preprocessing.
"""

from pathlib import Path
from typing import Union, Optional, Tuple
from PIL import Image
import numpy as np


class ImageProcessor:
    """
    Responsible ONLY for image loading and preprocessing.
    Single Responsibility: Image data preparation.
    """
    
    def __init__(self, default_format: str = "RGB"):
        self.default_format = default_format
    
    def load_image(
        self, 
        image_path: Union[str, Path],
        convert_to: Optional[str] = None
    ) -> Image.Image:
        """
        Load an image from file path.
        
        Args:
            image_path: Path to image file
            convert_to: Target format (RGB, RGBA, L, etc.)
            
        Returns:
            PIL Image object
            
        Raises:
            FileNotFoundError: If image doesn't exist
            IOError: If image can't be loaded
        """
        image_path = Path(image_path)
        
        if not image_path.exists():
            raise FileNotFoundError(f"Image not found: {image_path}")
        
        try:
            image = Image.open(image_path)
            convert_format = convert_to or self.default_format
            
            if image.mode != convert_format:
                image = image.convert(convert_format)
                
            return image
            
        except Exception as e:
            raise IOError(f"Failed to load image {image_path}: {e}")
    
    def resize_image(
        self,
        image: Image.Image,
        target_size: Optional[Tuple[int, int]] = None,
        max_size: Optional[int] = None
    ) -> Image.Image:
        """
        Resize image while maintaining aspect ratio.
        
        Args:
            image: PIL Image
            target_size: Target (width, height)
            max_size: Maximum dimension size
            
        Returns:
            Resized image
        """
        if target_size:
            return image.resize(target_size, Image.Resampling.LANCZOS)
        
        if max_size:
            # Maintain aspect ratio
            width, height = image.size
            if width > height:
                new_width = max_size
                new_height = int(height * (max_size / width))
            else:
                new_height = max_size
                new_width = int(width * (max_size / height))
            
            return image.resize((new_width, new_height), Image.Resampling.LANCZOS)
        
        return image
    
    def preprocess_for_ocr(
        self,
        image: Image.Image,
        enhance_contrast: bool = False,
        denoise: bool = False
    ) -> Image.Image:
        """
        Preprocess image for OCR.
        
        Args:
            image: Input image
            enhance_contrast: Apply contrast enhancement
            denoise: Apply denoising
            
        Returns:
            Preprocessed image
        """
        # Convert to grayscale for better OCR
        if image.mode != 'L':
            image = image.convert('L')
        
        if enhance_contrast:
            from PIL import ImageEnhance
            enhancer = ImageEnhance.Contrast(image)
            image = enhancer.enhance(1.5)
        
        if denoise:
            # Simple denoising using PIL filters
            from PIL import ImageFilter
            image = image.filter(ImageFilter.MedianFilter(size=3))
        
        return image
    
    def validate_image(self, image_path: Union[str, Path]) -> dict:
        """
        Validate image file.
        
        Args:
            image_path: Path to image
            
        Returns:
            Validation results dictionary
        """
        image_path = Path(image_path)
        
        validation = {
            'exists': image_path.exists(),
            'is_file': image_path.is_file() if image_path.exists() else False,
            'extension': image_path.suffix.lower(),
            'valid_format': False,
            'can_load': False,
            'dimensions': None
        }
        
        valid_extensions = {'.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.webp'}
        validation['valid_format'] = validation['extension'] in valid_extensions
        
        if validation['exists'] and validation['is_file']:
            try:
                img = Image.open(image_path)
                validation['can_load'] = True
                validation['dimensions'] = img.size
                img.close()
            except:
                validation['can_load'] = False
        
        return validation
    
    def batch_load(
        self,
        image_paths: list,
        convert_to: Optional[str] = None
    ) -> list:
        """
        Load multiple images.
        
        Args:
            image_paths: List of image paths
            convert_to: Target format
            
        Returns:
            List of (image, error) tuples
        """
        results = []
        for path in image_paths:
            try:
                image = self.load_image(path, convert_to)
                results.append((image, None))
            except Exception as e:
                results.append((None, str(e)))
        
        return results