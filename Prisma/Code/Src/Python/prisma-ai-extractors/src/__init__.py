"""
Prisma AI Extractors - Modular document extraction library.

This library provides AI-powered document extraction with clean, modular architecture
following Single Responsibility Principle (SRP).

Example usage:

    # Simple extraction
    from prisma_extractors import quick_extract
    
    result = quick_extract('document.jpg', 'smolvlm')
    print(result['data'])
    
    # Advanced usage with API
    from prisma_extractors import PrismaExtractorAPI
    
    with PrismaExtractorAPI() as api:
        result = api.extract('document.jpg', 'smolvlm')
        performance = api.get_performance_report()
    
    # Factory pattern
    from prisma_extractors import ExtractorFactory
    
    factory = ExtractorFactory()
    extractor = factory.create_extractor('paddle')
    result = extractor.extract('document.jpg')
"""

__version__ = '2.0.0-modular'
__author__ = 'Prisma Team'

# High-level API exports
from .api import (
    PrismaExtractorAPI,
    quick_extract,
    quick_extract_batch,
    compare_all_extractors
)

# Factory exports
from .factory import (
    ExtractorFactory,
    create_extractor,
    extract_from_image,
    extract_from_images
)

# Modular extractors
from .extractors_v2 import (
    ModularExtractor,
    SmolVLMModularExtractor,
    PaddleModularExtractor,
    DocTRModularExtractor
)

# Individual modules (for advanced usage)
from .modules import (
    ModelLoader,
    ImageProcessor,
    TextExtractor,
    JsonParser,
    DocumentValidator,
    PerformanceMonitor,
    ConfigManager,
    ErrorHandler
)

# Data models
from .models import (
    ExtractionResult,
    ExtractedDocument,
    Requerimiento
)

# Utilities
from .utils import (
    device_utils,
    image_utils,
    json_utils
)

__all__ = [
    # Version info
    '__version__',
    '__author__',
    
    # High-level API
    'PrismaExtractorAPI',
    'quick_extract',
    'quick_extract_batch', 
    'compare_all_extractors',
    
    # Factory
    'ExtractorFactory',
    'create_extractor',
    'extract_from_image',
    'extract_from_images',
    
    # Modular extractors
    'ModularExtractor',
    'SmolVLMModularExtractor',
    'PaddleModularExtractor', 
    'DocTRModularExtractor',
    
    # Modules
    'ModelLoader',
    'ImageProcessor',
    'TextExtractor', 
    'JsonParser',
    'DocumentValidator',
    'PerformanceMonitor',
    'ConfigManager',
    'ErrorHandler',
    
    # Models
    'ExtractionResult',
    'ExtractedDocument',
    'Requerimiento',
    
    # Utils
    'device_utils',
    'image_utils', 
    'json_utils'
]

# Module metadata
MODULES = {
    'ModelLoader': 'Model loading and caching',
    'ImageProcessor': 'Image loading and preprocessing',
    'TextExtractor': 'Text extraction from ML models',
    'JsonParser': 'JSON parsing and cleaning',
    'DocumentValidator': 'Document data validation',
    'PerformanceMonitor': 'Performance tracking',
    'ConfigManager': 'Configuration management',
    'ErrorHandler': 'Error handling and recovery'
}

EXTRACTORS = {
    'smolvlm': 'Vision-language model for document understanding',
    'paddle': 'PaddleOCR for text extraction',
    'doctr': 'DocTR for document text recognition'
}