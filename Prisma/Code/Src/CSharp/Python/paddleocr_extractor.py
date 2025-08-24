#!/usr/bin/env python3
"""
PaddleOCR Extractor for Spanish Legal Documents
Efficient OCR using PaddleOCR - Production-ready OCR toolkit

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

# PaddleOCR imports
try:
    from paddleocr import PaddleOCR
    import cv2
except ImportError as e:
    print(f"Error: PaddleOCR not installed. Run: uv add paddlepaddle paddleocr")
    exit(1)

# -------------------------------
# Configuration
# -------------------------------
print(f"[INFO] PaddleOCR device=CPU")

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
# PaddleOCR Model Setup
# -------------------------------
try:
    # Initialize PaddleOCR with Spanish language support
    print("[INFO] Loading PaddleOCR model...")
    # Use multilingual model that supports Spanish well
    ocr_model = PaddleOCR(lang='en')  # Simple initialization
    print("[SUCCESS] PaddleOCR model loaded")
except Exception as e:
    print(f"[ERROR] Failed to load PaddleOCR: {e}")
    exit(1)

# -------------------------------
# OCR Extraction Functions
# -------------------------------
def extract_text_with_paddleocr(image_path: str) -> tuple[str, Dict]:
    """
    Extract text using PaddleOCR
    Returns (text, metadata) tuple
    """
    try:
        # Run OCR
        result = ocr_model.ocr(image_path)
        
        if not result or not result[0]:
            return "", {"error": "No text detected"}
        
        # Extract text and metadata
        text_blocks = []
        confidence_scores = []
        bounding_boxes = []
        
        for line in result[0]:
            if line and len(line) == 2:
                bbox, (text, confidence) = line
                if text and text.strip():
                    text_blocks.append(text.strip())
                    confidence_scores.append(confidence)
                    bounding_boxes.append(bbox)
        
        # Sort by vertical position (y-coordinate) to maintain reading order
        if bounding_boxes and text_blocks:
            # Calculate y-position for each text block (average of top coordinates)
            y_positions = []
            for bbox in bounding_boxes:
                y_avg = (bbox[0][1] + bbox[1][1]) / 2  # Average y of top-left and top-right
                y_positions.append(y_avg)
            
            # Sort by y-position
            sorted_indices = sorted(range(len(y_positions)), key=lambda i: y_positions[i])
            text_blocks = [text_blocks[i] for i in sorted_indices]
            confidence_scores = [confidence_scores[i] for i in sorted_indices]
        
        full_text = "\n".join(text_blocks)
        
        # Calculate metadata
        avg_confidence = np.mean(confidence_scores) if confidence_scores else 0.0
        
        metadata = {
            "total_lines": len(text_blocks),
            "average_confidence": round(float(avg_confidence), 3),
            "min_confidence": round(float(min(confidence_scores)), 3) if confidence_scores else 0.0,
            "max_confidence": round(float(max(confidence_scores)), 3) if confidence_scores else 0.0,
            "bounding_boxes_count": len(bounding_boxes)
        }
        
        return full_text, metadata
        
    except Exception as e:
        print(f"[ERROR] PaddleOCR text extraction failed: {e}")
        return "", {"error": str(e)}

def extract_structured_data(raw_text: str) -> Dict[str, Any]:
    """
    Parse structured data from raw OCR text
    Enhanced rule-based extraction optimized for PaddleOCR output
    """
    if not raw_text:
        return {}
    
    extracted = {}
    lines = [line.strip() for line in raw_text.split('\n') if line.strip()]
    full_text = " ".join(lines).upper()  # For broader pattern matching
    
    import re
    
    # Enhanced date extraction patterns
    date_patterns = [
        r'FECHA[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})',
        r'FECHA[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{4})',
        r'(\d{1,2}\s+DE\s+\w+\s+DE\s+\d{4})',  # Spanish date format
        r'(\d{4}[-/]\d{1,2}[-/]\d{1,2})',       # ISO format anywhere
        r'(\d{1,2}[-/]\d{1,2}[-/]\d{4})',       # US format anywhere
    ]
    
    # Authority patterns (more comprehensive)
    authority_patterns = [
        r'(CONDUSEF)',
        r'(CNBV)',
        r'(CNSF)', 
        r'(BANXICO)',
        r'COMISION\s+NACIONAL\s+BANCARIA',
        r'COMISION\s+NACIONAL.*SEGUROS',
        r'SECRETARIA.*HACIENDA',
        r'PODER\s+JUDICIAL'
    ]
    
    # Expediente patterns (enhanced)
    expediente_patterns = [
        r'EXPEDIENTE[:\s]*([A-Z0-9\-/]+)',
        r'EXP(?:EDIENTE)?[:\.\s]*([A-Z0-9\-/]+)',
        r'OFICIO[:\s]*([A-Z0-9\-/]+)',
        r'No\.?\s*([A-Z0-9\-/]{3,})',  # Generic number pattern
    ]
    
    # Document type patterns (comprehensive)
    doc_type_patterns = [
        r'(REQUERIMIENTO\s+DE\s+INFORMACION)',
        r'(REQUERIMIENTO)',
        r'(SOLICITUD\s+DE\s+INFORMACION)',
        r'(SOLICITUD)',
        r'(OFICIO)',
        r'(SANCION)',
        r'(MULTA)',
        r'(CITATORIO)',
        r'(NOTIFICACION)',
        r'(RESOLUCION)'
    ]
    
    # Entity patterns (banks, institutions)
    entity_patterns = [
        r'(BBVA\s+MEXICO)',
        r'(BANAMEX)',
        r'(SANTANDER)',
        r'(HSBC)',
        r'(SCOTIABANK)',
        r'(BANORTE)',
        r'(CITIBANK)',
        r'(BANCO\s+\w+)'
    ]
    
    # Process patterns
    for line in lines:
        line_upper = line.upper()
        
        # Extract date
        if 'fecha' not in extracted:
            for pattern in date_patterns:
                match = re.search(pattern, line_upper)
                if match:
                    extracted['fecha'] = match.group(1)
                    break
    
    # Extract from full text for broader matching
    # Authority extraction
    if 'autoridadEmisora' not in extracted:
        for pattern in authority_patterns:
            match = re.search(pattern, full_text)
            if match:
                extracted['autoridadEmisora'] = match.group(1)
                break
    
    # Expediente extraction
    if 'expediente' not in extracted:
        for pattern in expediente_patterns:
            match = re.search(pattern, full_text)
            if match:
                extracted['expediente'] = match.group(1).strip()
                break
    
    # Document type extraction
    if 'tipoRequerimiento' not in extracted:
        for pattern in doc_type_patterns:
            match = re.search(pattern, full_text)
            if match:
                extracted['tipoRequerimiento'] = match.group(1)
                break
    
    # Entity extraction (for partes field)
    entities_found = []
    for pattern in entity_patterns:
        matches = re.findall(pattern, full_text)
        entities_found.extend(matches)
    
    if entities_found:
        extracted['partes'] = list(set(entities_found))  # Remove duplicates
    
    # Monetary amount extraction
    money_patterns = [
        r'\$\s*([0-9,]+\.?\d*)',
        r'PESOS\s+([0-9,]+\.?\d*)',
        r'MXN\s*([0-9,]+\.?\d*)'
    ]
    
    for pattern in money_patterns:
        match = re.search(pattern, full_text)
        if match and 'monto' not in extracted:
            try:
                amount_str = match.group(1).replace(',', '')
                extracted['monto'] = float(amount_str)
                extracted['moneda'] = 'MXN'
                break
            except ValueError:
                pass
    
    return extracted

def extract_requerimiento_from_image(image_path: str) -> Union[Requerimiento, Dict]:
    """
    Complete extraction pipeline using PaddleOCR
    """
    start_time = time.time()
    
    try:
        # Step 1: Extract text with PaddleOCR
        print(f"[INFO] Extracting text with PaddleOCR...")
        raw_text, ocr_metadata = extract_text_with_paddleocr(image_path)
        
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
        
        # Populate main fields
        for field in ['fecha', 'autoridadEmisora', 'expediente', 'tipoRequerimiento']:
            if field in structured_data:
                setattr(requerimiento, field, structured_data[field])
        
        # Handle partes (entities)
        if 'partes' in structured_data:
            requerimiento.partes = structured_data['partes']
        
        # Handle monetary details
        if 'monto' in structured_data or 'moneda' in structured_data:
            requerimiento.detalle.monto = structured_data.get('monto')
            requerimiento.detalle.moneda = structured_data.get('moneda', 'unknown')
        
        # Add comprehensive metadata
        processing_time = time.time() - start_time
        result = requerimiento.model_dump()  # Use model_dump instead of dict()
        result['_metadata'] = {
            'model': 'PaddleOCR',
            'processing_time': round(processing_time, 2),
            'raw_text_length': len(raw_text),
            'ocr_confidence': ocr_metadata.get('average_confidence', 0.0),
            'lines_extracted': ocr_metadata.get('total_lines', 0),
            'bounding_boxes': ocr_metadata.get('bounding_boxes_count', 0),
            'structured_fields_found': len(structured_data),
            'raw_text_preview': raw_text[:300] + "..." if len(raw_text) > 300 else raw_text
        }
        
        return result
        
    except Exception as e:
        processing_time = time.time() - start_time
        return {
            "error": str(e),
            "model": "PaddleOCR", 
            "processing_time": round(processing_time, 2)
        }

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    parser = argparse.ArgumentParser(description="PaddleOCR Spanish Legal Document Extractor")
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
        print(f"[METADATA] Bounding boxes: {metadata.get('bounding_boxes', 'N/A')}")
        print(f"[METADATA] Structured fields: {metadata.get('structured_fields_found', 'N/A')}")
        print(f"[METADATA] Text length: {metadata['raw_text_length']} chars")

if __name__ == "__main__":
    main()