"""
File loading module with pure functions for listing and loading images.
Single Responsibility: Handle file system operations and image loading.
"""
import os
from typing import List, Set
from pathlib import Path
import cv2
import numpy as np
from .models import ImageData


SUPPORTED_EXTENSIONS: Set[str] = {".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".pdf"}


def is_supported_file(file_path: str) -> bool:
    """
    Check if a file has a supported extension.
    
    Args:
        file_path: Path to the file
        
    Returns:
        True if file extension is supported
    """
    return Path(file_path).suffix.lower() in SUPPORTED_EXTENSIONS


def list_supported_files(path: str) -> List[str]:
    """
    List all supported files in a path (file or directory).
    Pure function: given same input, always returns same output.
    
    Args:
        path: File path or directory path
        
    Returns:
        List of absolute paths to supported files
    """
    path_obj = Path(path).resolve()
    
    if path_obj.is_file():
        return [str(path_obj)] if is_supported_file(str(path_obj)) else []
    
    if path_obj.is_dir():
        files = []
        for file_path in path_obj.rglob("*"):
            if file_path.is_file() and is_supported_file(str(file_path)):
                files.append(str(file_path))
        return sorted(files)
    
    return []


def load_image_from_file(file_path: str) -> np.ndarray:
    """
    Load a single image from file path.
    
    Args:
        file_path: Path to image file
        
    Returns:
        Image as numpy array in BGR format
        
    Raises:
        ValueError: If image cannot be loaded
    """
    img = cv2.imread(file_path, cv2.IMREAD_COLOR)
    if img is None:
        raise ValueError(f"Could not read image: {file_path}")
    return img


def load_pdf_pages(pdf_path: str, dpi: int = 300) -> List[np.ndarray]:
    """
    Load all pages from a PDF as images.
    
    Args:
        pdf_path: Path to PDF file
        dpi: Resolution for PDF rendering
        
    Returns:
        List of images (BGR format)
        
    Raises:
        ImportError: If pdf2image is not available
        ValueError: If PDF cannot be loaded
    """
    try:
        from pdf2image import convert_from_path
    except ImportError:
        raise ImportError("PDF support requires pdf2image package: pip install pdf2image")
    
    try:
        pil_images = convert_from_path(pdf_path, dpi=dpi)
        return [cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR) for img in pil_images]
    except Exception as e:
        raise ValueError(f"Could not load PDF {pdf_path}: {str(e)}")


def load_images_from_path(file_path: str, dpi: int = 300) -> List[ImageData]:
    """
    Load image(s) from a file path, handling both regular images and PDFs.
    Pure function that transforms file path to ImageData objects.
    
    Args:
        file_path: Path to image or PDF file
        dpi: DPI for PDF rendering
        
    Returns:
        List of ImageData objects
        
    Raises:
        ValueError: If file cannot be loaded or has unsupported format
    """
    path_obj = Path(file_path)
    
    if not path_obj.exists():
        raise ValueError(f"File does not exist: {file_path}")
    
    if not is_supported_file(file_path):
        raise ValueError(f"Unsupported file format: {path_obj.suffix}")
    
    if path_obj.suffix.lower() == ".pdf":
        images = load_pdf_pages(file_path, dpi)
        return [
            ImageData(
                data=img,
                source_path=file_path,
                page_number=i + 1,
                total_pages=len(images)
            )
            for i, img in enumerate(images)
        ]
    else:
        img = load_image_from_file(file_path)
        return [ImageData(
            data=img,
            source_path=file_path,
            page_number=1,
            total_pages=1
        )]