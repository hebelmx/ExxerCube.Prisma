#!/usr/bin/env python3
"""
DocTR (Document Text Recognition) Extractor for Spanish Legal Documents
Efficient OCR using DocTR - Deep Learning for Document Analysis

Mission 3: Performance Analysis & Optimization Framework
Part of OCR model benchmarking suite
"""

import json
import os
import argparse
import time
from typing import Optional, Union, List, Dict, Any
from pathlib import Path

# Core libraries
import numpy as np
from PIL import Image
from pydantic import BaseModel, Field

# DocTR imports
try:
    from doctr.io import DocumentFile
    from doctr.models import ocr_predictor
    import cv2
except ImportError as e:
    print(f"Error: DocTR not installed. Run: uv add python-doctr opencv-python")
    exit(1)

# -------------------------------
# Configuration
# -------------------------------
print(f"[INFO] DocTR device=CPU (lightweight)")

# -------------------------------
# Pydantic Models (Same as others)
# -------------------------------
class RequerimientoDetalle(BaseModel):
    """Detailed information extracted from legal document"""
    descripcion: Optional[str] = Field(default="unknown")
    monto: Optional[float] = Field(default=None)
    moneda: Optional[str] = Field(default="unknown")
    activoVirtual: Optional[str] = Field(default="unknown")

class Requerimiento(BaseModel):
    """Spanish legal document extraction schema"""
    fecha: Optional[str] = Field(default="unknown")
    autoridadEmisora: Optional[str] = Field(default="unknown")
    expediente: Optional[str] = Field(default="unknown")
    tipoRequerimiento: Optional[str] = Field(default="unknown")
    subtipoRequerimiento: Optional[str] = Field(default="unknown")
    fundamentoLegal: Optional[str] = Field(default="unknown")
    motivacion: Optional[str] = Field(default="unknown")
    partes: Optional[List[str]] = Field(default_factory=list)
    detalle: Optional[RequerimientoDetalle] = Field(default_factory=RequerimientoDetalle)

# -------------------------------
# DocTR Model Setup
# -------------------------------
try:
    # Load DocTR OCR model (detection + recognition)
    print("[INFO] Loading DocTR OCR model...")
    # Use English model as base (works well for Spanish)
    model = ocr_predictor(pretrained=True)
    print("[SUCCESS] DocTR model loaded")
except Exception as e:
    print(f"[ERROR] Failed to load DocTR: {e}")
    exit(1)

# -------------------------------
# OCR Extraction Functions
# -------------------------------
def extract_text_with_doctr(image_path: str) -> tuple[str, Dict]:
    """
    Extract text using DocTR OCR
    Returns (text, metadata) tuple
    """
    try:
        # Load document
        doc = DocumentFile.from_images(image_path)
        
        # Run OCR
        result = model(doc)
        
        # Extract text and metadata
        text_blocks = []
        confidence_scores = []
        
        for page in result.pages:
            for block in page.blocks:
                for line in block.lines:
                    line_text = ""
                    line_confidences = []
                    for word in line.words:
                        line_text += word.value + " "
                        line_confidences.append(word.confidence)
                    
                    if line_text.strip():
                        text_blocks.append(line_text.strip())
                        confidence_scores.append(np.mean(line_confidences) if line_confidences else 0.0)
        
        full_text = "\n".join(text_blocks)
        avg_confidence = np.mean(confidence_scores) if confidence_scores else 0.0
        
        metadata = {
            "total_lines": len(text_blocks),
            "average_confidence": round(float(avg_confidence), 3),
            "min_confidence": round(float(min(confidence_scores)), 3) if confidence_scores else 0.0,
            "max_confidence": round(float(max(confidence_scores)), 3) if confidence_scores else 0.0
        }
        
        return full_text, metadata
        
    except Exception as e:
        print(f"[ERROR] DocTR text extraction failed: {e}")
        return "", {"error": str(e)}

def extract_structured_data(raw_text: str) -> Dict[str, Any]:
    """
    Parse structured data from raw OCR text
    Enhanced rule-based extraction for Spanish legal documents
    """
    if not raw_text:
        return {}
    
    extracted = {}
    lines = [line.strip() for line in raw_text.split('\n') if line.strip()]
    
    import re
    
    # Date extraction patterns
    date_patterns = [
        r'FECHA[:\s]*(\d{4}-\d{2}-\d{2})',
        r'FECHA[:\s]*(\d{1,2}[\-/]\d{1,2}[\-/]\d{4})',
        r'(\d{1,2}\s+de\s+\w+\s+de\s+\d{4})',  # Spanish format
        r'(\d{4}[\-/]\d{1,2}[\-/]\d{1,2})'
    ]
    
    # Authority patterns
    authority_patterns = [
        r'(CONDUSEF)',
        r'(CNBV)',
        r'(CNSF)',
        r'(BANXICO)',
        r'COMISIÓN\s+NACIONAL\s+BANCARIA',
        r'COMISIÓN\s+NACIONAL\s+DE\s+SEGUROS'
    ]
    
    # Expediente patterns
    expediente_patterns = [
        r'EXPEDIENTE[:\s]*([A-Z0-9\-/]+)',
        r'EXP[:\.\s]*([A-Z0-9\-/]+)',
        r'OFICIO[:\s]*([A-Z0-9\-/]+)'
    ]
    
    # Document type patterns
    doc_type_patterns = [
        r'(REQUERIMIENTO\s+DE\s+INFORMACIÓN)',
        r'(REQUERIMIENTO)',
        r'(SOLICITUD\s+DE\s+INFORMACIÓN)',
        r'(SOLICITUD)',
        r'(OFICIO)',
        r'(SANCIÓN)',
        r'(MULTA)'
    ]
    
    # Process each line
    for line in lines:
        line_upper = line.upper()
        
        # Extract date
        if 'fecha' not in extracted:
            for pattern in date_patterns:
                match = re.search(pattern, line, re.IGNORECASE)
                if match:
                    extracted['fecha'] = match.group(1)
                    break
        
        # Extract authority
        if 'autoridadEmisora' not in extracted:
            for pattern in authority_patterns:
                match = re.search(pattern, line_upper)
                if match:
                    extracted['autoridadEmisora'] = match.group(1)
                    break
        
        # Extract expediente
        if 'expediente' not in extracted:
            for pattern in expediente_patterns:
                match = re.search(pattern, line_upper)
                if match:
                    extracted['expediente'] = match.group(1).strip()
                    break
        
        # Extract document type
        if 'tipoRequerimiento' not in extracted:
            for pattern in doc_type_patterns:
                match = re.search(pattern, line_upper)
                if match:
                    extracted['tipoRequerimiento'] = match.group(1)
                    break
    
    # Additional parsing for monetary amounts
    money_pattern = r'\$\s*([0-9,]+\.?\d*)'
    for line in lines:
        money_match = re.search(money_pattern, line)
        if money_match and 'monto' not in extracted:
            try:
                amount_str = money_match.group(1).replace(',', '')
                extracted['monto'] = float(amount_str)
                extracted['moneda'] = 'MXN'  # Default Mexican peso
            except ValueError:
                pass
    
    return extracted

def extract_requerimiento_from_image(image_path: str) -> Union[Requerimiento, Dict]:
    """
    Complete extraction pipeline using DocTR
    """
    start_time = time.time()
    
    try:
        # Step 1: Extract text with DocTR
        print(f"[INFO] Extracting text with DocTR...")
        raw_text, ocr_metadata = extract_text_with_doctr(image_path)
        
        if not raw_text:
            print("[WARNING] No text extracted")
            return {
                "error": "No text extracted", 
                "ocr_metadata": ocr_metadata,
                "raw_text": ""
            }
        
        # Step 2: Structure extraction
        print(f"[INFO] Parsing structured data...")
        structured_data = extract_structured_data(raw_text)
        
        # Step 3: Create Pydantic model
        requerimiento = Requerimiento()
        
        # Populate fields from extracted data
        for field in ['fecha', 'autoridadEmisora', 'expediente', 'tipoRequerimiento']:
            if field in structured_data:
                setattr(requerimiento, field, structured_data[field])
        
        # Handle monetary details
        if 'monto' in structured_data or 'moneda' in structured_data:
            requerimiento.detalle.monto = structured_data.get('monto')
            requerimiento.detalle.moneda = structured_data.get('moneda', 'unknown')
        
        # Add metadata
        processing_time = time.time() - start_time
        result = requerimiento.dict()
        result['_metadata'] = {
            'model': 'DocTR',
            'processing_time': round(processing_time, 2),
            'raw_text_length': len(raw_text),
            'ocr_confidence': ocr_metadata.get('average_confidence', 0.0),
            'lines_extracted': ocr_metadata.get('total_lines', 0),
            'raw_text_preview': raw_text[:300] + "..." if len(raw_text) > 300 else raw_text
        }
        
        return result
        
    except Exception as e:
        processing_time = time.time() - start_time
        return {
            "error": str(e),
            "model": "DocTR",
            "processing_time": round(processing_time, 2)
        }

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    parser = argparse.ArgumentParser(description="DocTR Spanish Legal Document Extractor")
    parser.add_argument("--image", required=True, help="Path to document image")
    parser.add_argument("--output", help="Output JSON file (optional)")
    parser.add_argument("--verbose", action="store_true", help="Verbose output")
    parser.add_argument("--show-text", action="store_true", help="Show raw extracted text")
    
    args = parser.parse_args()
    
    if not os.path.exists(args.image):
        print(f"Error: Image file not found: {args.image}")
        exit(1)
    
    print(f"Processing: {args.image}")
    
    # Extract information
    result = extract_requerimiento_from_image(args.image)
    
    # Show raw text if requested
    if args.show_text and '_metadata' in result and 'raw_text_preview' in result['_metadata']:
        print(f"\n[RAW TEXT PREVIEW]")
        print(result['_metadata']['raw_text_preview'])
        print(f"[END RAW TEXT]\n")
    
    # Output results
    if args.output:
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        print(f"Results saved to: {args.output}")
    else:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    
    # Verbose metadata
    if args.verbose and '_metadata' in result:
        metadata = result['_metadata']
        print(f"\n[METADATA] Processing time: {metadata['processing_time']}s")
        print(f"[METADATA] OCR confidence: {metadata.get('ocr_confidence', 'N/A')}")
        print(f"[METADATA] Lines extracted: {metadata.get('lines_extracted', 'N/A')}")
        print(f"[METADATA] Text length: {metadata['raw_text_length']} chars")

if __name__ == "__main__":
    main()