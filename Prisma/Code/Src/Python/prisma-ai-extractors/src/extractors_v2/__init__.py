"""
Modular Extractors - Refactored with Single Responsibility Principle.
"""

from .base_extractor_v2 import ModularExtractor
from .smolvlm_extractor_v2 import SmolVLMModularExtractor
from .paddle_extractor_v2 import PaddleModularExtractor
from .doctr_extractor_v2 import DocTRModularExtractor

__all__ = [
    'ModularExtractor',
    'SmolVLMModularExtractor', 
    'PaddleModularExtractor',
    'DocTRModularExtractor'
]