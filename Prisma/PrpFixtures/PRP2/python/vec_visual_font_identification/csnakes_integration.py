"""
CSnakes integration wrapper for C# interop.

This module provides CSnakes-compatible functions that can be called from C# code
via CSnakes.Runtime. All functions accept bytes and return JSON-serializable dictionaries.
"""

from typing import Dict, Optional

from vec_visual_font_identification.extraction import VecExtractionOrchestrator
from vec_visual_font_identification.font import FontDetector
from vec_visual_font_identification.quality import QualityVerifier
from vec_visual_font_identification.visual import LogoDetector


# Singleton instances (loaded once, reused)
_logo_detector: Optional[LogoDetector] = None
_font_detector: Optional[FontDetector] = None
_quality_verifier: Optional[QualityVerifier] = None
_vec_orchestrator: Optional[VecExtractionOrchestrator] = None


def _get_logo_detector() -> LogoDetector:
    """Get or create LogoDetector singleton."""
    global _logo_detector
    if _logo_detector is None:
        _logo_detector = LogoDetector()
    return _logo_detector


def _get_font_detector() -> FontDetector:
    """Get or create FontDetector singleton."""
    global _font_detector
    if _font_detector is None:
        _font_detector = FontDetector()
    return _font_detector


def _get_quality_verifier() -> QualityVerifier:
    """Get or create QualityVerifier singleton."""
    global _quality_verifier
    if _quality_verifier is None:
        _quality_verifier = QualityVerifier()
    return _quality_verifier


def _get_vec_orchestrator() -> VecExtractionOrchestrator:
    """Get or create VecExtractionOrchestrator singleton."""
    global _vec_orchestrator
    if _vec_orchestrator is None:
        _vec_orchestrator = VecExtractionOrchestrator()
    return _vec_orchestrator


def detect_logos_csnakes(pdf_bytes: bytes, page_number: Optional[int] = None) -> Dict:
    """
    CSnakes-compatible function for logo detection.

    Args:
        pdf_bytes: PDF file as bytes
        page_number: Optional page number (0-indexed) to process specific page

    Returns:
        Dictionary with logo detection results (JSON-serializable)
    """
    try:
        detector = _get_logo_detector()
        result = detector.detect_logos(pdf_bytes, page_number=page_number)
        return result.model_dump()  # Pydantic to dict
    except Exception as e:
        return {
            "error": True,
            "message": str(e),
            "logos": [],
            "total_pages": 0,
            "processing_time_seconds": 0.0,
            "overall_quality_score": 0.0,
        }


def detect_fonts_csnakes(pdf_bytes: bytes, page_number: Optional[int] = None) -> Dict:
    """
    CSnakes-compatible function for font detection.

    Args:
        pdf_bytes: PDF file as bytes
        page_number: Optional page number (0-indexed) to process specific page

    Returns:
        Dictionary with font detection results (JSON-serializable)
    """
    try:
        detector = _get_font_detector()
        result = detector.detect_fonts(pdf_bytes, page_number=page_number)
        return result.model_dump()  # Pydantic to dict
    except Exception as e:
        return {
            "error": True,
            "message": str(e),
            "fonts": [],
            "typography_metrics": {},
            "overlap_detection": {},
            "total_pages": 0,
            "processing_time_seconds": 0.0,
            "overall_compliance_score": 0.0,
        }


def verify_quality_csnakes(pdf_bytes: bytes, document_id: Optional[str] = None) -> Dict:
    """
    CSnakes-compatible function for complete quality verification.

    Args:
        pdf_bytes: PDF file as bytes
        document_id: Optional document identifier

    Returns:
        Dictionary with complete quality report (JSON-serializable)
    """
    try:
        verifier = _get_quality_verifier()
        report = verifier.verify_document(pdf_bytes, document_id=document_id)
        return report.model_dump()  # Pydantic to dict
    except Exception as e:
        return {
            "error": True,
            "message": str(e),
            "document_id": document_id,
            "total_pages": 0,
            "processing_time_seconds": 0.0,
            "overall_score": 0.0,
            "category_scores": [],
            "issues": [],
            "issue_count_by_severity": {},
        }


def extract_vec_statement_csnakes(pdf_bytes: bytes, document_id: Optional[str] = None) -> Dict:
    """
    CSnakes-compatible function for VEC statement extraction.

    This is the main entry point for complete VEC statement processing from C#.

    Args:
        pdf_bytes: PDF file as bytes
        document_id: Optional document identifier

    Returns:
        Dictionary with VEC extraction result (JSON-serializable)
        Format: {"success": bool, "data": VecStatementData | None, "error_code": str | None, "error_message": str | None}
    """
    from datetime import datetime

    try:
        if document_id is None:
            document_id = f"VEC-{datetime.utcnow().strftime('%Y%m%d%H%M%S')}"

        orchestrator = _get_vec_orchestrator()
        result = orchestrator.extract_vec_statement(pdf_bytes, document_id)
        return result.model_dump()  # Pydantic to dict
    except Exception as e:
        return {
            "success": False,
            "data": None,
            "error_code": "EXT-999",
            "error_message": f"Unexpected error in CSnakes integration: {str(e)}",
        }
