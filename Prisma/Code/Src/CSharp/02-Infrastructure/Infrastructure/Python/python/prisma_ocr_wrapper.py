#!/usr/bin/env python3
"""
CSnakes-compatible Python wrapper for Prisma OCR processing.
Replaces process-based CLI calls with direct function calls.
"""
import sys
import json
import base64
from pathlib import Path
from typing import Dict, List, Optional, Any, Tuple, TYPE_CHECKING
import io
from PIL import Image
import numpy as np

# Import ocr_modules - lazy import to allow CSnakes code generation without ocr_modules
# CSnakes source generator only needs to parse the file, not execute imports
# Runtime will import ocr_modules when functions are called
_ocr_modules_imported = False
_process_path = None
_create_default_config = None
_ProcessingConfig = None
_OCRConfig = None
_extract_expediente = None
_extract_causa = None
_extract_accion_solicitada = None
_extract_dates = None
_extract_amounts = None

def _ensure_ocr_modules():
    """Lazy import ocr_modules only when needed at runtime."""
    global _ocr_modules_imported, _process_path, _create_default_config
    global _ProcessingConfig, _OCRConfig, _extract_expediente, _extract_causa
    global _extract_accion_solicitada, _extract_dates, _extract_amounts
    
    if not _ocr_modules_imported:
        from ocr_modules import (
            process_path, create_default_config, ProcessingConfig, OCRConfig,
            extract_expediente, extract_causa, extract_accion_solicitada,
            extract_dates, extract_amounts
        )
        _process_path = process_path
        _create_default_config = create_default_config
        _ProcessingConfig = ProcessingConfig
        _OCRConfig = OCRConfig
        _extract_expediente = extract_expediente
        _extract_causa = extract_causa
        _extract_accion_solicitada = extract_accion_solicitada
        _extract_dates = extract_dates
        _extract_amounts = extract_amounts
        _ocr_modules_imported = True

def execute_ocr(image_data: bytes, config: Dict[str, Any]) -> Dict[str, Any]:
    """
    Execute OCR on image data using CSnakes-compatible interface.
    
    Args:
        image_data: Raw image bytes
        config: OCR configuration dictionary
        
    Returns:
        Dictionary containing OCR results
    """
    try:
        # Convert bytes to PIL Image
        image = Image.open(io.BytesIO(image_data))
        
        # Ensure ocr_modules is imported
        _ensure_ocr_modules()
        
        # Convert config dict to OCRConfig object
        ocr_config = _OCRConfig(
            language=config.get('language', 'spa'),
            fallback_language=config.get('fallback_language', 'eng'),
            oem=config.get('oem', 3),
            psm=config.get('psm', 6)
        )
        
        # Create processing config
        processing_config = _ProcessingConfig(
            remove_watermark=True,
            deskew=True,
            binarize=True,
            ocr_config=ocr_config,
            extract_sections=True,
            normalize_text=True
        )
        
        # Process the image
        result = _process_path(image, processing_config)
        
        # Convert result to dictionary
        return {
            'text': result.ocr_result.text if result.ocr_result else '',
            'confidence_avg': result.ocr_result.confidence_avg if result.ocr_result else 0.0,
            'confidence_median': result.ocr_result.confidence_median if result.ocr_result else 0.0,
            'confidences': result.ocr_result.confidences if result.ocr_result else [],
            'language_used': result.ocr_result.language_used if result.ocr_result else 'spa',
            'processing_errors': result.processing_errors if result.processing_errors else []
        }
        
    except Exception as e:
        return {
            'error': str(e),
            'text': '',
            'confidence_avg': 0.0,
            'confidence_median': 0.0,
            'confidences': [],
            'language_used': 'spa',
            'processing_errors': [str(e)]
        }

def extract_fields_from_text(text: str, confidence: float) -> Dict[str, Any]:
    """
    Extract structured fields from OCR text.
    
    Args:
        text: OCR text content
        confidence: OCR confidence score
        
    Returns:
        Dictionary containing extracted fields
    """
    try:
        # Ensure ocr_modules is imported
        _ensure_ocr_modules()
        
        # Extract expediente
        expediente = _extract_expediente(text)
        
        # Extract causa
        causa = _extract_causa(text)
        
        # Extract accion solicitada
        accion_solicitada = _extract_accion_solicitada(text)
        
        # Extract dates
        dates = _extract_dates(text)
        
        # Extract amounts
        amounts = _extract_amounts(text)
        
        return {
            'expediente': expediente,
            'causa': causa,
            'accion_solicitada': accion_solicitada,
            'fechas': dates,
            'montos': amounts,
            'confidence': confidence,
            'error': None
        }
        
    except Exception as e:
        return {
            'expediente': None,
            'causa': None,
            'accion_solicitada': None,
            'fechas': [],
            'montos': [],
            'confidence': confidence,
            'error': str(e)
        }

def extract_expediente_from_text(text: str) -> Optional[str]:
    """
    Extract expediente (case file number) from text.
    
    Args:
        text: The text to process
        
    Returns:
        Extracted expediente or None
    """
    try:
        _ensure_ocr_modules()
        return _extract_expediente(text)
    except Exception:
        return None

def extract_causa_from_text(text: str) -> Optional[str]:
    """
    Extract causa (cause) from text.
    
    Args:
        text: The text to process
        
    Returns:
        Extracted causa or None
    """
    try:
        _ensure_ocr_modules()
        return _extract_causa(text)
    except Exception:
        return None

def extract_accion_solicitada_from_text(text: str) -> Optional[str]:
    """
    Extract accion solicitada (requested action) from text.
    
    Args:
        text: The text to process
        
    Returns:
        Extracted accion solicitada or None
    """
    try:
        _ensure_ocr_modules()
        return _extract_accion_solicitada(text)
    except Exception:
        return None

def extract_dates_from_text(text: str) -> List[str]:
    """
    Extract dates from text.
    
    Args:
        text: The text to process
        
    Returns:
        List of extracted dates
    """
    try:
        _ensure_ocr_modules()
        return _extract_dates(text)
    except Exception:
        return []

def extract_amounts_from_text(text: str) -> List[Dict[str, Any]]:
    """
    Extract monetary amounts from text.
    
    Args:
        text: The text to process
        
    Returns:
        List of extracted amounts with currency and value
    """
    try:
        _ensure_ocr_modules()
        amounts = _extract_amounts(text)
        return [
            {
                'currency': amount.currency,
                'value': float(amount.value),
                'original_text': amount.original_text
            }
            for amount in amounts
        ]
    except Exception:
        return []
