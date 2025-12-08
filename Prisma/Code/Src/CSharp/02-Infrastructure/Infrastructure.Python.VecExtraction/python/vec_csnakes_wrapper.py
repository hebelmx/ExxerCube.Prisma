#!/usr/bin/env python3
"""
VEC Statement Extraction Wrapper for CSnakes Integration

This module provides type-safe Python functions callable from C# via CSnakes.
CSnakes will generate strongly-typed C# wrapper classes from this module.

DO NOT import the full vec_visual_font_identification package here.
This is a thin wrapper that delegates to the actual implementation.
"""

import sys
import os
import logging
from typing import Dict, Any

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='[%(asctime)s] [%(levelname)s] %(message)s'
)
logger = logging.getLogger(__name__)

# Module version
__version__ = "1.0.0"

# Global flag to track if main module is available
_main_module_available = False
_extract_func = None


def _ensure_main_module():
    """
    Ensure the main VEC extraction module is available.

    This lazy-loads the actual implementation module to avoid
    import issues during CSnakes code generation.
    """
    global _main_module_available, _extract_func

    if _main_module_available and _extract_func is not None:
        return True

    try:
        # Add parent directory to path to find vec_visual_font_identification
        current_dir = os.path.dirname(os.path.abspath(__file__))
        parent_dir = os.path.dirname(current_dir)
        if parent_dir not in sys.path:
            sys.path.insert(0, parent_dir)

        # Import the actual implementation
        logger.info("Importing vec_visual_font_identification.csnakes_integration")
        from vec_visual_font_identification.csnakes_integration import extract_vec_statement_csnakes

        _extract_func = extract_vec_statement_csnakes
        _main_module_available = True
        logger.info("✓ Main VEC extraction module loaded successfully")
        return True

    except ImportError as e:
        logger.error(f"Failed to import main VEC extraction module: {e}")
        _main_module_available = False
        return False


def get_version() -> str:
    """
    Get wrapper module version.

    Returns:
        Version string
    """
    return __version__


def get_module_info() -> str:
    """
    Get information about the VEC extraction module.

    Returns:
        Module information string
    """
    if _ensure_main_module():
        return f"VEC Extraction Wrapper v{__version__} | Main module: LOADED"
    else:
        return f"VEC Extraction Wrapper v{__version__} | Main module: NOT LOADED"


def health_check() -> bool:
    """
    Perform health check to verify VEC extraction module can be loaded.

    Returns:
        True if module loads successfully, False otherwise
    """
    logger.info("Running health check...")

    try:
        success = _ensure_main_module()

        if success:
            logger.info("✓ Health check PASSED")
            return True
        else:
            logger.error("✗ Health check FAILED - Could not load main module")
            return False

    except Exception as e:
        logger.error(f"✗ Health check FAILED - Exception: {e}")
        return False


def extract_vec_statement(pdf_bytes: bytes, document_id: str = None) -> Dict[str, Any]:
    """
    Extract VEC statement data from PDF (CSnakes entry point).

    This is the main function that CSnakes will generate C# bindings for.

    Args:
        pdf_bytes: PDF file as bytes
        document_id: Optional document identifier (auto-generated if not provided)

    Returns:
        Dictionary with extraction result:
        {
            "success": bool,
            "data": {
                "document_id": str,
                "header": {...},
                "transactions": [...],
                "total_pages": int,
                "extraction_confidence": float,
                ...
            } | None,
            "error_code": str | None,
            "error_message": str | None
        }
    """
    logger.info(f"extract_vec_statement called with document_id={document_id}")

    try:
        # Ensure main module is loaded
        if not _ensure_main_module():
            return {
                "success": False,
                "data": None,
                "error_code": "MODULE-001",
                "error_message": "VEC extraction module not available"
            }

        # Delegate to actual implementation
        logger.info("Delegating to extract_vec_statement_csnakes...")
        result = _extract_func(pdf_bytes, document_id)

        logger.info(f"Extraction completed: success={result.get('success', False)}")
        return result

    except Exception as e:
        import traceback
        error_msg = f"VEC extraction failed: {str(e)}"
        logger.error(error_msg)
        logger.error(traceback.format_exc())

        return {
            "success": False,
            "data": None,
            "error_code": "WRAPPER-001",
            "error_message": error_msg
        }


def extract_vec_statement_from_file(file_path: str, document_id: str = None) -> Dict[str, Any]:
    """
    Extract VEC statement data from PDF file (convenience function for testing).

    Args:
        file_path: Path to PDF file
        document_id: Optional document identifier

    Returns:
        Same dictionary as extract_vec_statement()
    """
    try:
        logger.info(f"Reading PDF file: {file_path}")

        with open(file_path, "rb") as f:
            pdf_bytes = f.read()

        logger.info(f"PDF size: {len(pdf_bytes)} bytes")

        return extract_vec_statement(pdf_bytes, document_id)

    except FileNotFoundError:
        error_msg = f"File not found: {file_path}"
        logger.error(error_msg)
        return {
            "success": False,
            "data": None,
            "error_code": "FILE-001",
            "error_message": error_msg
        }
    except Exception as e:
        error_msg = f"Failed to read file: {str(e)}"
        logger.error(error_msg)
        return {
            "success": False,
            "data": None,
            "error_code": "FILE-002",
            "error_message": error_msg
        }


# Module-level test/CLI
if __name__ == "__main__":
    import argparse
    import json

    parser = argparse.ArgumentParser(description="VEC Extraction CSnakes Wrapper Test")
    parser.add_argument("--file", help="Path to PDF file")
    parser.add_argument("--health", action="store_true", help="Run health check")
    parser.add_argument("--info", action="store_true", help="Show module info")

    args = parser.parse_args()

    if args.health:
        is_healthy = health_check()
        print(f"Health check: {'PASS' if is_healthy else 'FAIL'}")
        sys.exit(0 if is_healthy else 1)

    elif args.info:
        print(f"Version: {get_version()}")
        print(f"Module: {get_module_info()}")

    elif args.file:
        print(f"Processing: {args.file}")
        result = extract_vec_statement_from_file(args.file)

        # Pretty print result (truncate data if large)
        if result.get("success") and result.get("data"):
            data = result["data"].copy()
            if "transactions" in data:
                txn_count = len(data["transactions"])
                data["transactions"] = f"[{txn_count} transactions]"

        print(json.dumps(result, indent=2, ensure_ascii=False))

    else:
        parser.print_help()
