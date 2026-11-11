"""
Modular OCR Pipeline Package

A decomposed, testable OCR pipeline following SRP and functional programming principles.
"""

from .models import (
    ImageData,
    OCRConfig,
    OCRResult, 
    AmountData,
    ExtractedFields,
    ProcessingConfig,
    OutputData,
    ProcessingResult
)

from .pipeline import (
    process_path,
    process_file,
    process_directory,
    create_default_config
)

__version__ = "1.0.0"
__all__ = [
    # Models
    "ImageData",
    "OCRConfig", 
    "OCRResult",
    "AmountData",
    "ExtractedFields",
    "ProcessingConfig",
    "OutputData", 
    "ProcessingResult",
    # Main functions
    "process_path",
    "process_file",
    "process_directory",
    "create_default_config"
]