"""PDF processing utilities for VEC statement extraction."""

from pathlib import Path
from typing import Union

import pdfplumber
from pdf2image import convert_from_bytes, convert_from_path
from PIL import Image


class PdfProcessor:
    """PDF processing utility for text extraction and image conversion."""

    @staticmethod
    def extract_text(pdf_input: Union[bytes, str, Path]) -> list[str]:
        """
        Extract text from PDF pages.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            List of text strings (one per page)
        """
        if isinstance(pdf_input, bytes):
            with pdfplumber.open(pdf_input) as pdf:
                return [page.extract_text() or "" for page in pdf.pages]
        else:
            with pdfplumber.open(pdf_input) as pdf:
                return [page.extract_text() or "" for page in pdf.pages]

    @staticmethod
    def extract_tables(pdf_input: Union[bytes, str, Path]) -> list[list[list[str]]]:
        """
        Extract tables from PDF pages.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            List of tables per page (each table is a list of rows, each row is a list of cells)
        """
        if isinstance(pdf_input, bytes):
            with pdfplumber.open(pdf_input) as pdf:
                return [page.extract_tables() or [] for page in pdf.pages]
        else:
            with pdfplumber.open(pdf_input) as pdf:
                return [page.extract_tables() or [] for page in pdf.pages]

    @staticmethod
    def convert_to_images(
        pdf_input: Union[bytes, str, Path], dpi: int = 200
    ) -> list[Image.Image]:
        """
        Convert PDF pages to PIL images.

        Args:
            pdf_input: PDF as bytes, file path, or Path object
            dpi: Resolution for image conversion (default: 200)

        Returns:
            List of PIL Image objects (one per page)
        """
        if isinstance(pdf_input, bytes):
            return convert_from_bytes(pdf_input, dpi=dpi)
        else:
            return convert_from_path(pdf_input, dpi=dpi)

    @staticmethod
    def get_page_count(pdf_input: Union[bytes, str, Path]) -> int:
        """
        Get the number of pages in a PDF.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            Number of pages
        """
        if isinstance(pdf_input, bytes):
            with pdfplumber.open(pdf_input) as pdf:
                return len(pdf.pages)
        else:
            with pdfplumber.open(pdf_input) as pdf:
                return len(pdf.pages)

    @staticmethod
    def extract_metadata(pdf_input: Union[bytes, str, Path]) -> dict:
        """
        Extract PDF metadata.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            Dictionary of metadata fields
        """
        if isinstance(pdf_input, bytes):
            with pdfplumber.open(pdf_input) as pdf:
                return pdf.metadata or {}
        else:
            with pdfplumber.open(pdf_input) as pdf:
                return pdf.metadata or {}
