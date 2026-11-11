#!/usr/bin/env python3
"""
CLI entry point for the modular OCR pipeline.
Replaces the monolithic ocr_pipeline.py with composable modules.
"""
import argparse
import sys
from pathlib import Path

# Add the ocr_modules to Python path
sys.path.insert(0, str(Path(__file__).parent))

from ocr_modules import (
    process_path, create_default_config, ProcessingConfig, OCRConfig
)


def create_cli_config(args) -> ProcessingConfig:
    """
    Create processing configuration from CLI arguments.
    
    Args:
        args: Parsed command line arguments
        
    Returns:
        Processing configuration
    """
    ocr_config = OCRConfig(
        language=args.language,
        fallback_language=args.fallback_language,
        oem=args.oem,
        psm=args.psm
    )
    
    return ProcessingConfig(
        remove_watermark=not args.no_watermark_removal,
        deskew=not args.no_deskew,
        binarize=not args.no_binarize,
        ocr_config=ocr_config,
        extract_sections=not args.no_extract_sections,
        normalize_text=not args.no_normalize_text
    )


def print_results_summary(results):
    """Print a summary of processing results."""
    total_files = len(set(result.source_path for result in results))
    total_pages = len(results)
    successful = sum(1 for r in results if not r.processing_errors)
    failed = total_pages - successful
    
    print(f"\nProcessing Summary:")
    print(f"  Files processed: {total_files}")
    print(f"  Pages processed: {total_pages}")
    print(f"  Successful: {successful}")
    print(f"  Failed: {failed}")
    
    if failed > 0:
        print(f"\nErrors encountered:")
        for result in results:
            if result.processing_errors:
                print(f"  {result.source_path} (page {result.page_number}):")
                for error in result.processing_errors:
                    print(f"    - {error}")
    
    # Show average confidence for successful results
    successful_results = [r for r in results if r.ocr_result and not r.processing_errors]
    if successful_results:
        avg_confidence = sum(r.ocr_result.confidence_avg for r in successful_results) / len(successful_results)
        print(f"\nAverage OCR confidence: {avg_confidence:.1f}%")


def main():
    """Main CLI entry point."""
    parser = argparse.ArgumentParser(
        description="Modular OCR pipeline for Spanish documents with red watermark removal.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    
    # Required arguments
    parser.add_argument(
        "--input", 
        required=True,
        help="Path to image/PDF file or directory"
    )
    parser.add_argument(
        "--outdir", 
        required=True,
        help="Output directory for TXT/JSON files"
    )
    
    # OCR configuration
    parser.add_argument(
        "--language", 
        default="spa",
        help="Primary OCR language (3-letter code)"
    )
    parser.add_argument(
        "--fallback-language",
        default="eng", 
        help="Fallback OCR language"
    )
    parser.add_argument(
        "--oem",
        type=int,
        default=1,
        choices=[0, 1, 2, 3],
        help="Tesseract OCR Engine Mode"
    )
    parser.add_argument(
        "--psm",
        type=int, 
        default=6,
        choices=list(range(14)),
        help="Tesseract Page Segmentation Mode"
    )
    
    # Processing options (disable flags)
    parser.add_argument(
        "--no-watermark-removal",
        action="store_true",
        help="Skip red watermark removal"
    )
    parser.add_argument(
        "--no-deskew",
        action="store_true", 
        help="Skip image deskewing"
    )
    parser.add_argument(
        "--no-binarize",
        action="store_true",
        help="Skip image binarization"
    )
    parser.add_argument(
        "--no-extract-sections",
        action="store_true",
        help="Skip section extraction"
    )
    parser.add_argument(
        "--no-normalize-text",
        action="store_true",
        help="Skip text normalization"
    )
    
    # Output options
    parser.add_argument(
        "--verbose", 
        action="store_true",
        help="Verbose output"
    )
    
    args = parser.parse_args()
    
    # Validate input path
    input_path = Path(args.input)
    if not input_path.exists():
        print(f"Error: Input path does not exist: {args.input}", file=sys.stderr)
        sys.exit(1)
    
    # Create output directory
    output_path = Path(args.outdir)
    try:
        output_path.mkdir(parents=True, exist_ok=True)
    except Exception as e:
        print(f"Error: Could not create output directory: {e}", file=sys.stderr)
        sys.exit(1)
    
    # Create processing configuration
    config = create_cli_config(args)
    
    if args.verbose:
        print(f"Input: {args.input}")
        print(f"Output: {args.outdir}")
        print(f"Language: {config.ocr_config.language} (fallback: {config.ocr_config.fallback_language})")
        print(f"Processing options:")
        print(f"  Watermark removal: {config.remove_watermark}")
        print(f"  Deskewing: {config.deskew}")
        print(f"  Binarization: {config.binarize}")
        print(f"  Section extraction: {config.extract_sections}")
        print(f"  Text normalization: {config.normalize_text}")
        print()
    
    try:
        # Process the input path
        results = process_path(
            str(input_path),
            config,
            str(output_path)
        )
        
        # Print individual results if verbose
        if args.verbose:
            for result in results:
                confidence = result.ocr_result.confidence_avg if result.ocr_result else 0
                print(f"[OK] {result.source_path} page {result.page_number}: confidence={confidence:.1f}%")
                
                if result.processing_errors:
                    for error in result.processing_errors:
                        print(f"  [WARN] {error}")
                
                # Check for incomplete extraction
                if result.extracted_fields:
                    if not result.extracted_fields.causa or not result.extracted_fields.accion_solicitada:
                        print(f"  [WARN] Some sections not detected; consider adjusting header aliases.")
        
        # Print summary
        print_results_summary(results)
        
        # Exit with error code if any processing failed
        failed_count = sum(1 for r in results if r.processing_errors)
        if failed_count > 0:
            sys.exit(1)
    
    except Exception as e:
        print(f"Error: Processing failed: {e}", file=sys.stderr)
        if args.verbose:
            import traceback
            traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()