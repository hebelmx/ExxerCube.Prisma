#!/usr/bin/env python3
"""
GOT-OCR2 Extractor for Spanish Legal Documents
Multi-modal OCR using GOT-OCR2 (General OCR Theory 2.0)

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
import torch
from PIL import Image
from pydantic import BaseModel, Field

# GOT-OCR2 imports
try:
    from transformers import AutoProcessor, AutoModelForImageTextToText
except ImportError as e:
    print(f"Error: transformers not installed. Run: uv add transformers")
    exit(1)

# -------------------------------
# Configuration
# -------------------------------
def is_cuda_supported():
    """Check if CUDA is available and working"""
    if not torch.cuda.is_available():
        return False
    try:
        # Test CUDA functionality
        torch.cuda.current_device()
        torch.cuda.get_device_name(0)
        return True
    except Exception:
        return False

# Device and model configuration
HAS_CUDA = is_cuda_supported()
DEVICE = "cuda" if HAS_CUDA else "cpu"
DTYPE = torch.bfloat16 if HAS_CUDA else torch.float32

print(f"[INFO] GOT-OCR2 device={DEVICE}, dtype={DTYPE}")

# -------------------------------
# Pydantic Models (Same as SmolVLM)
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
# GOT-OCR2 Model Setup
# -------------------------------
MODEL_ID = os.getenv("GOT_OCR2_ID", "stepfun-ai/GOT-OCR-2.0-hf")

try:
    # Load GOT-OCR2 model and processor using the correct implementation
    print(f"Loading GOT-OCR2 model: {MODEL_ID}")
    model = AutoModelForImageTextToText.from_pretrained(
        MODEL_ID, 
        device_map=DEVICE,
        torch_dtype=DTYPE
    )
    processor = AutoProcessor.from_pretrained(MODEL_ID, use_fast=True)
    print(f"[SUCCESS] GOT-OCR2 loaded on {DEVICE}")
except Exception as e:
    print(f"[ERROR] Failed to load GOT-OCR2: {e}")
    exit(1)

# -------------------------------
# Prompt Templates
# -------------------------------
SYSTEM_INSTRUCTIONS = (
    "Extract structured information from this Spanish legal document. "
    "Focus on: fecha (date), autoridad emisora (issuing authority), expediente (case number), "
    "tipo de requerimiento (requirement type), and other key legal fields."
)

EXTRACTION_PROMPT = (
    "Read this Spanish legal document and extract key information. "
    "Output the text content first, then provide structured JSON with extracted fields."
)

# -------------------------------
# OCR Extraction Functions
# -------------------------------
def extract_text_with_got_ocr2(image_path: str) -> str:
    """
    Extract raw text using GOT-OCR2
    Returns plain text content from the document
    """
    try:
        # Load and process image
        image = Image.open(image_path).convert("RGB")
        
        # Process image with GOT-OCR2 processor
        inputs = processor(image, return_tensors="pt", device=DEVICE).to(DEVICE)
        
        # Generate OCR output
        generate_ids = model.generate(
            **inputs,
            do_sample=False,
            tokenizer=processor.tokenizer,
            stop_strings="<|im_end|>",
            max_new_tokens=4096,
        )
        
        # Decode the generated text
        result = processor.decode(
            generate_ids[0, inputs["input_ids"].shape[1]:], 
            skip_special_tokens=True
        )
        
        return result if result else ""
        
    except Exception as e:
        print(f"[ERROR] GOT-OCR2 text extraction failed: {e}")
        return ""

def extract_structured_data(raw_text: str) -> Dict[str, Any]:
    """
    Parse structured data from raw OCR text
    Uses rule-based extraction for Spanish legal documents
    """
    if not raw_text:
        return {}
    
    extracted = {}
    lines = raw_text.split('\n')
    
    # Extract date patterns
    import re
    date_patterns = [
        r'FECHA[:\s]+(\d{4}-\d{2}-\d{2})',
        r'(\d{1,2}[\-/]\d{1,2}[\-/]\d{4})',
        r'(\d{4}[\-/]\d{1,2}[\-/]\d{1,2})'
    ]
    
    for line in lines:
        line = line.strip()
        
        # Date extraction
        for pattern in date_patterns:
            match = re.search(pattern, line, re.IGNORECASE)
            if match and 'fecha' not in extracted:
                extracted['fecha'] = match.group(1)
                break
        
        # Authority extraction
        if any(word in line.upper() for word in ['CONDUSEF', 'CNBV', 'CNSF', 'BANXICO']):
            if 'autoridadEmisora' not in extracted:
                # Extract the authority name
                for auth in ['CONDUSEF', 'CNBV', 'CNSF', 'BANXICO']:
                    if auth in line.upper():
                        extracted['autoridadEmisora'] = auth
                        break
        
        # Expediente (case number) extraction
        if 'EXPEDIENTE' in line.upper():
            match = re.search(r'EXPEDIENTE[:\s]+([A-Z0-9\-/]+)', line, re.IGNORECASE)
            if match:
                extracted['expediente'] = match.group(1)
        
        # Document type extraction
        if any(word in line.upper() for word in ['REQUERIMIENTO', 'SOLICITUD', 'OFICIO']):
            if 'tipoRequerimiento' not in extracted:
                if 'REQUERIMIENTO' in line.upper():
                    extracted['tipoRequerimiento'] = 'REQUERIMIENTO'
                elif 'SOLICITUD' in line.upper():
                    extracted['tipoRequerimiento'] = 'SOLICITUD'
                elif 'OFICIO' in line.upper():
                    extracted['tipoRequerimiento'] = 'OFICIO'
    
    return extracted

def extract_requerimiento_from_image(image_path: str) -> Union[Requerimiento, Dict]:
    """
    Complete extraction pipeline using GOT-OCR2
    """
    start_time = time.time()
    
    try:
        # Step 1: Extract raw text
        print(f"[INFO] Extracting text with GOT-OCR2...")
        raw_text = extract_text_with_got_ocr2(image_path)
        
        if not raw_text:
            print("[WARNING] No text extracted")
            return {"error": "No text extracted", "raw_text": ""}
        
        # Step 2: Structure extraction
        print(f"[INFO] Parsing structured data...")
        structured_data = extract_structured_data(raw_text)
        
        # Step 3: Create Pydantic model
        requerimiento = Requerimiento()
        
        # Populate fields from extracted data
        if 'fecha' in structured_data:
            requerimiento.fecha = structured_data['fecha']
        if 'autoridadEmisora' in structured_data:
            requerimiento.autoridadEmisora = structured_data['autoridadEmisora']
        if 'expediente' in structured_data:
            requerimiento.expediente = structured_data['expediente']
        if 'tipoRequerimiento' in structured_data:
            requerimiento.tipoRequerimiento = structured_data['tipoRequerimiento']
        
        # Add metadata
        processing_time = time.time() - start_time
        result = requerimiento.dict()
        result['_metadata'] = {
            'model': 'GOT-OCR2',
            'processing_time': round(processing_time, 2),
            'raw_text_length': len(raw_text),
            'raw_text_preview': raw_text[:200] + "..." if len(raw_text) > 200 else raw_text
        }
        
        return result
        
    except Exception as e:
        processing_time = time.time() - start_time
        return {
            "error": str(e),
            "model": "GOT-OCR2",
            "processing_time": round(processing_time, 2)
        }

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    parser = argparse.ArgumentParser(description="GOT-OCR2 Spanish Legal Document Extractor")
    parser.add_argument("--image", required=True, help="Path to document image")
    parser.add_argument("--output", help="Output JSON file (optional)")
    parser.add_argument("--verbose", action="store_true", help="Verbose output")
    
    args = parser.parse_args()
    
    if not os.path.exists(args.image):
        print(f"Error: Image file not found: {args.image}")
        exit(1)
    
    print(f"Processing: {args.image}")
    
    # Extract information
    result = extract_requerimiento_from_image(args.image)
    
    # Output results
    if args.output:
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        print(f"Results saved to: {args.output}")
    else:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    
    if args.verbose and '_metadata' in result:
        print(f"\n[METADATA] Processing time: {result['_metadata']['processing_time']}s")
        print(f"[METADATA] Raw text length: {result['_metadata']['raw_text_length']} chars")

if __name__ == "__main__":
    main()