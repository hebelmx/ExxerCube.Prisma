"""Image utility functions for PDF processing."""

import io
from typing import List

import pdfplumber
from PIL import Image
from pdf2image import convert_from_bytes


def pdf_to_images(pdf_bytes: bytes, dpi: int = 300) -> List[Image.Image]:
    """
    Convert PDF bytes to PIL Images.

    Args:
        pdf_bytes: PDF file as bytes
        dpi: Resolution for image conversion

    Returns:
        List of PIL Images (one per page)
    """
    try:
        images = convert_from_bytes(pdf_bytes, dpi=dpi)
        return images
    except Exception as e:
        raise ValueError(f"Failed to convert PDF to images: {e}") from e
