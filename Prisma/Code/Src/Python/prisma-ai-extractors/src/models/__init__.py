"""
Data models for AI document extraction.
"""

from .document_models import (
    RequerimientoDetalle,
    Requerimiento,
    ExtractedDocument,
    ExtractionResult
)

__all__ = [
    'RequerimientoDetalle',
    'Requerimiento', 
    'ExtractedDocument',
    'ExtractionResult'
]