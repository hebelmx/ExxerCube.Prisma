"""
Output writer module for persisting processing results.
Single Responsibility: Handle output formatting and file writing.
"""
import json
import os
from pathlib import Path
from typing import Dict, Any, Optional
from .models import OutputData, ExtractedFields, ProcessingResult


def prepare_json_data(extracted_fields: ExtractedFields, metadata: Dict[str, Any] = None) -> Dict[str, Any]:
    """
    Prepare extracted fields for JSON serialization.
    Pure function for data preparation.
    
    Args:
        extracted_fields: Extracted fields data
        metadata: Optional additional metadata
        
    Returns:
        Dictionary ready for JSON serialization
    """
    # Convert Pydantic model to dict
    json_data = extracted_fields.model_dump()
    
    # Add metadata if provided
    if metadata:
        json_data["metadata"] = metadata
    
    return json_data


def format_text_output(text: str, extracted_fields: ExtractedFields) -> str:
    """
    Format text with extracted fields summary.
    Pure function for text formatting.
    
    Args:
        text: Original OCR text
        extracted_fields: Extracted fields
        
    Returns:
        Formatted text with summary header
    """
    lines = ["=" * 80, "OCR TEXT OUTPUT", "=" * 80, ""]
    
    # Add summary section
    if any([extracted_fields.expediente, extracted_fields.causa, extracted_fields.accion_solicitada]):
        lines.extend(["EXTRACTED FIELDS SUMMARY:", "-" * 30])
        
        if extracted_fields.expediente:
            lines.append(f"Expediente: {extracted_fields.expediente}")
        
        if extracted_fields.fechas:
            lines.append(f"Fechas: {', '.join(extracted_fields.fechas)}")
        
        if extracted_fields.montos:
            amounts = [f"{m.value} {m.currency}" for m in extracted_fields.montos]
            lines.append(f"Montos: {', '.join(amounts)}")
        
        lines.extend(["", "=" * 80, ""])
    
    # Add original text
    lines.append("FULL OCR TEXT:")
    lines.append("-" * 15)
    lines.append(text)
    
    return "\n".join(lines)


def create_output_data(text: str, 
                      extracted_fields: ExtractedFields,
                      metadata: Optional[Dict[str, Any]] = None) -> OutputData:
    """
    Create OutputData object from components.
    Pure function for data creation.
    
    Args:
        text: OCR text
        extracted_fields: Extracted fields
        metadata: Optional metadata
        
    Returns:
        OutputData object ready for persistence
    """
    return OutputData(
        text_content=text,
        extracted_fields=extracted_fields,
        metadata=metadata or {}
    )


def ensure_directory_exists(file_path: str) -> None:
    """
    Ensure the directory for a file path exists.
    Side-effect function for directory creation.
    
    Args:
        file_path: Path to file (directory will be created)
    """
    directory = Path(file_path).parent
    directory.mkdir(parents=True, exist_ok=True)


def write_text_file(file_path: str, content: str, encoding: str = "utf-8") -> None:
    """
    Write text content to file.
    Side-effect function for file writing.
    
    Args:
        file_path: Path to output file
        content: Text content to write
        encoding: File encoding
        
    Raises:
        OSError: If file cannot be written
    """
    ensure_directory_exists(file_path)
    
    with open(file_path, "w", encoding=encoding) as f:
        f.write(content)


def write_json_file(file_path: str, 
                   data: Dict[str, Any], 
                   encoding: str = "utf-8",
                   indent: int = 2) -> None:
    """
    Write JSON data to file.
    Side-effect function for JSON writing.
    
    Args:
        file_path: Path to output file
        data: Data to serialize as JSON
        encoding: File encoding
        indent: JSON indentation
        
    Raises:
        OSError: If file cannot be written
    """
    ensure_directory_exists(file_path)
    
    with open(file_path, "w", encoding=encoding) as f:
        json.dump(data, f, ensure_ascii=False, indent=indent)


def generate_output_paths(base_path: str, 
                         source_path: str,
                         page_number: int,
                         total_pages: int) -> Dict[str, str]:
    """
    Generate output file paths for text and JSON files.
    Pure function for path generation.
    
    Args:
        base_path: Base output directory
        source_path: Original source file path
        page_number: Page number (1-based)
        total_pages: Total number of pages
        
    Returns:
        Dictionary with 'text' and 'json' file paths
    """
    source_stem = Path(source_path).stem
    
    # Add page suffix if multiple pages
    if total_pages > 1:
        file_stem = f"{source_stem}_p{page_number}"
    else:
        file_stem = source_stem
    
    base_output_path = Path(base_path) / file_stem
    
    return {
        "text": str(base_output_path) + ".txt",
        "json": str(base_output_path) + ".json"
    }


def write_output_files(output_data: OutputData, 
                      text_path: str,
                      json_path: str,
                      include_summary: bool = True) -> None:
    """
    Write output data to both text and JSON files.
    Side-effect function for file writing.
    
    Args:
        output_data: Output data to write
        text_path: Path for text file
        json_path: Path for JSON file
        include_summary: Include fields summary in text file
        
    Raises:
        OSError: If files cannot be written
    """
    # Prepare text content
    if include_summary:
        text_content = format_text_output(
            output_data.text_content, 
            output_data.extracted_fields
        )
    else:
        text_content = output_data.text_content
    
    # Write text file
    write_text_file(text_path, text_content)
    
    # Prepare and write JSON file
    json_data = prepare_json_data(output_data.extracted_fields, output_data.metadata)
    write_json_file(json_path, json_data)


def write_processing_result(result: ProcessingResult, 
                           output_directory: str,
                           include_summary: bool = True) -> str:
    """
    Write a complete processing result to output files.
    Side-effect function that coordinates file writing.
    
    Args:
        result: Processing result to write
        output_directory: Base output directory
        include_summary: Include fields summary in text file
        
    Returns:
        Base path of written files (without extension)
        
    Raises:
        OSError: If files cannot be written
    """
    # Generate output paths
    paths = generate_output_paths(
        output_directory,
        result.source_path,
        result.page_number,
        1  # Assuming single page per result
    )
    
    # Create output data
    metadata = {
        "source_path": result.source_path,
        "page_number": result.page_number,
        "ocr_confidence_avg": result.ocr_result.confidence_avg,
        "ocr_confidence_median": result.ocr_result.confidence_median,
        "language_used": result.ocr_result.language_used,
        "processing_errors": result.processing_errors
    }
    
    output_data = create_output_data(
        result.ocr_result.text,
        result.extracted_fields,
        metadata
    )
    
    # Write files
    write_output_files(
        output_data,
        paths["text"],
        paths["json"],
        include_summary
    )
    
    # Return base path
    return str(Path(paths["text"]).with_suffix(""))