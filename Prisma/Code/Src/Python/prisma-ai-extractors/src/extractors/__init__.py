"""
AI-powered document extractors for various models.
"""

from .base_extractor import BaseExtractor
from .smolvlm_extractor import SmolVLMExtractor
from .got_ocr2_extractor import GOTOcr2Extractor
from .paddle_extractor import PaddleOCRExtractor
from .doctr_extractor import DocTRExtractor

__all__ = [
    'BaseExtractor',
    'SmolVLMExtractor',
    'GOTOcr2Extractor',
    'PaddleOCRExtractor',
    'DocTRExtractor'
]