"""
Main pipeline orchestrator that composes all modules.
Single Responsibility: Coordinate the complete OCR processing workflow.
"""
import cv2
from typing import List, Optional, Dict, Any
from pathlib import Path

from .models import (
    ImageData, ProcessingConfig, ProcessingResult, 
    ExtractedFields, OCRConfig, AmountData
)
from .file_loader import load_images_from_path, list_supported_files
from .watermark_remover import remove_red_watermark
from .image_deskewer import deskew_image
from .image_binarizer import binarize_image
from .ocr_executor import execute_ocr
from .text_normalizer import normalize_text
from .section_extractor import extract_section
from .expediente_extractor import extract_expediente
from .date_extractor import extract_dates
from .amount_extractor import extract_amounts
from .output_writer import write_processing_result


# Section header aliases for Spanish legal documents
CAUSA_HEADERS = [
    "CAUSA QUE MOTIVA EL REQUERIMIENTO",
    "CAUSA QUE MOTIVA EL REQUERIMIENTO.",
    "CAUSA DEL REQUERIMIENTO",
    "CAUSA QUE MOTIVA",
]

ACCION_HEADERS = [
    "ACCIÓN SOLICITADA",
    "ACCION SOLICITADA", 
    "ACCIÓN REQUERIDA",
    "ACCION REQUERIDA",
    "PETICIÓN",
    "PETICION",
    "REQUERIMIENTO",
]


def preprocess_image(image: ImageData, config: ProcessingConfig) -> ImageData:
    """
    Apply image preprocessing steps.
    Pure function for image transformation.
    
    Args:
        image: Input image data
        config: Processing configuration
        
    Returns:
        Preprocessed image data
    """
    processed_data = image.data.copy()
    
    # Remove red watermark if enabled
    if config.remove_watermark:
        processed_data = remove_red_watermark(processed_data)
    
    # Convert to grayscale
    if len(processed_data.shape) == 3:
        processed_data = cv2.cvtColor(processed_data, cv2.COLOR_BGR2GRAY)
    
    # Deskew if enabled
    if config.deskew:
        processed_data = deskew_image(processed_data)
    
    # Binarize if enabled
    if config.binarize:
        processed_data = binarize_image(processed_data, method="adaptive_gaussian")
        # Invert for OCR (text should be white on black)
        processed_data = cv2.bitwise_not(processed_data)
    
    return ImageData(
        data=processed_data,
        source_path=image.source_path,
        page_number=image.page_number,
        total_pages=image.total_pages
    )


def extract_structured_fields(text: str, ocr_confidence: float) -> ExtractedFields:
    """
    Extract structured fields from OCR text.
    Pure function for field extraction.
    
    Args:
        text: OCR text
        ocr_confidence: OCR confidence score
        
    Returns:
        Extracted structured fields
    """
    # Extract expediente
    expediente = extract_expediente(text)
    
    # Extract sections
    causa = extract_section(text, CAUSA_HEADERS, ACCION_HEADERS, include_header=False)
    accion = extract_section(text, ACCION_HEADERS, [], include_header=False)
    
    # Normalize sections if found
    if causa:
        causa = normalize_text(causa)
    if accion:
        accion = normalize_text(accion)
    
    # Extract dates
    fechas = extract_dates(text, extract_spanish=True, extract_iso=True)
    
    # Extract amounts
    montos = extract_amounts(
        text,
        currencies=[("$", "MXN"), ("USD", "USD"), ("€", "EUR")],
        amount_keywords=["monto", "importe", "cantidad", "total"]
    )
    
    return ExtractedFields(
        expediente=expediente,
        causa=causa,
        accion_solicitada=accion,
        fechas=fechas,
        montos=montos
    )


def process_single_image(image: ImageData, config: ProcessingConfig) -> ProcessingResult:
    """
    Process a single image through the complete pipeline.
    Main processing function that orchestrates all steps.
    
    Args:
        image: Image to process
        config: Processing configuration
        
    Returns:
        Processing result with extracted data
    """
    errors = []
    
    try:
        # Preprocess image
        preprocessed = preprocess_image(image, config)
        
        # Execute OCR
        ocr_result = execute_ocr(preprocessed.data, config.ocr_config)
        
        # Normalize text if enabled
        text = ocr_result.text
        if config.normalize_text:
            text = normalize_text(text)
        
        # Extract structured fields if enabled
        if config.extract_sections:
            extracted_fields = extract_structured_fields(text, ocr_result.confidence_avg)
        else:
            extracted_fields = ExtractedFields()
        
        return ProcessingResult(
            source_path=image.source_path,
            page_number=image.page_number,
            ocr_result=ocr_result,
            extracted_fields=extracted_fields,
            processing_errors=errors
        )
    
    except Exception as e:
        errors.append(f"Processing failed: {str(e)}")
        # Return minimal result with error
        return ProcessingResult(
            source_path=image.source_path,
            page_number=image.page_number,
            ocr_result=ocr_result if 'ocr_result' in locals() else None,
            extracted_fields=ExtractedFields(),
            processing_errors=errors
        )


def process_file(file_path: str, 
                config: ProcessingConfig,
                output_directory: Optional[str] = None) -> List[ProcessingResult]:
    """
    Process a complete file (image or PDF) through the pipeline.
    
    Args:
        file_path: Path to file to process
        config: Processing configuration
        output_directory: Optional output directory for results
        
    Returns:
        List of processing results (one per page)
    """
    results = []
    
    try:
        # Load images from file
        images = load_images_from_path(file_path)
        
        # Process each image/page
        for image in images:
            result = process_single_image(image, config)
            results.append(result)
            
            # Write output if directory specified
            if output_directory:
                try:
                    output_path = write_processing_result(
                        result, 
                        output_directory,
                        include_summary=True
                    )
                    result.output_path = output_path
                except Exception as e:
                    result.processing_errors.append(f"Failed to write output: {str(e)}")
    
    except Exception as e:
        # Create error result for the file
        error_result = ProcessingResult(
            source_path=file_path,
            page_number=1,
            ocr_result=None,
            extracted_fields=ExtractedFields(),
            processing_errors=[f"Failed to load file: {str(e)}"]
        )
        results.append(error_result)
    
    return results


def process_directory(directory_path: str,
                     config: ProcessingConfig,
                     output_directory: Optional[str] = None) -> List[ProcessingResult]:
    """
    Process all supported files in a directory.
    
    Args:
        directory_path: Directory containing files to process
        config: Processing configuration  
        output_directory: Optional output directory for results
        
    Returns:
        List of all processing results
    """
    all_results = []
    
    # Get list of supported files
    file_paths = list_supported_files(directory_path)
    
    # Process each file
    for file_path in file_paths:
        file_results = process_file(file_path, config, output_directory)
        all_results.extend(file_results)
    
    return all_results


def create_default_config() -> ProcessingConfig:
    """
    Create default processing configuration.
    Pure function for config creation.
    
    Returns:
        Default processing configuration
    """
    return ProcessingConfig(
        remove_watermark=True,
        deskew=True,
        binarize=True,
        ocr_config=OCRConfig(language="spa", fallback_language="eng"),
        extract_sections=True,
        normalize_text=True
    )


def process_path(input_path: str,
                config: Optional[ProcessingConfig] = None,
                output_directory: Optional[str] = None) -> List[ProcessingResult]:
    """
    Process a file or directory path through the complete pipeline.
    Main entry point for the modular OCR pipeline.
    
    Args:
        input_path: Path to file or directory to process
        config: Processing configuration (uses default if None)
        output_directory: Optional output directory for results
        
    Returns:
        List of processing results
    """
    if config is None:
        config = create_default_config()
    
    path_obj = Path(input_path)
    
    if path_obj.is_file():
        return process_file(str(path_obj), config, output_directory)
    elif path_obj.is_dir():
        return process_directory(str(path_obj), config, output_directory)
    else:
        raise ValueError(f"Path does not exist: {input_path}")