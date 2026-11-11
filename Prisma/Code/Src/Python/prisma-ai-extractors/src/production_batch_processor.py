#!/usr/bin/env python3
"""
Production Batch Processor for Client Documents
Enterprise-ready OCR processing with DocTR for Spanish legal documents

Ready for client deployment - Optimized for speed and reliability
"""

import json
import os
import time
import argparse
from typing import Dict, List, Any, Optional
from pathlib import Path
from datetime import datetime
import logging

# Core libraries
from doctr.io import DocumentFile
from doctr.models import ocr_predictor
import numpy as np
from PIL import Image

# Pydantic for validation
from pydantic import BaseModel, Field

# -------------------------------
# Production Logging Setup
# -------------------------------
def setup_production_logging(log_file: str = None):
    """Set up production-grade logging"""
    if log_file is None:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        log_file = f"production_ocr_{timestamp}.log"
    
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(levelname)s - %(message)s',
        handlers=[
            logging.FileHandler(log_file),
            logging.StreamHandler()
        ]
    )
    return logging.getLogger(__name__)

# -------------------------------
# Production Models
# -------------------------------
class ProcessingMetadata(BaseModel):
    """Processing metadata for client reporting"""
    model: str = "DocTR"
    processing_time: float
    ocr_confidence: float
    characters_extracted: int
    lines_extracted: int
    quality_score: str  # HIGH, MEDIUM, LOW
    review_required: bool

class RequerimientoDetalle(BaseModel):
    """Document detail information"""
    descripcion: Optional[str] = Field(default="unknown")
    monto: Optional[float] = Field(default=None)
    moneda: Optional[str] = Field(default="unknown")
    activoVirtual: Optional[str] = Field(default="unknown")

class ClientRequerimiento(BaseModel):
    """Client-ready extraction schema"""
    # Core fields
    fecha: Optional[str] = Field(default="unknown")
    autoridadEmisora: Optional[str] = Field(default="unknown")
    expediente: Optional[str] = Field(default="unknown")
    tipoRequerimiento: Optional[str] = Field(default="unknown")
    subtipoRequerimiento: Optional[str] = Field(default="unknown")
    fundamentoLegal: Optional[str] = Field(default="unknown")
    motivacion: Optional[str] = Field(default="unknown")
    partes: Optional[List[str]] = Field(default_factory=list)
    detalle: Optional[RequerimientoDetalle] = Field(default_factory=RequerimientoDetalle)
    
    # Processing metadata
    processing_metadata: Optional[ProcessingMetadata] = None

# -------------------------------
# Production OCR Processor
# -------------------------------
class ProductionOCRProcessor:
    """
    Production-ready OCR processor for client documents
    Optimized for Spanish legal documents with quality control
    """
    
    def __init__(self, confidence_thresholds: Dict[str, float] = None):
        self.logger = setup_production_logging()
        
        # Quality control thresholds
        self.thresholds = confidence_thresholds or {
            'high': 0.80,    # Auto-approve
            'medium': 0.50,  # Flag for review
            'low': 0.0       # Manual review required
        }
        
        # Load DocTR model
        self.logger.info("Loading DocTR OCR model for production...")
        try:
            self.model = ocr_predictor(pretrained=True)
            self.logger.info("✅ DocTR model loaded successfully")
        except Exception as e:
            self.logger.error(f"❌ Failed to load DocTR model: {e}")
            raise
        
        # Processing statistics
        self.stats = {
            'total_processed': 0,
            'successful': 0,
            'high_quality': 0,
            'medium_quality': 0,
            'low_quality': 0,
            'errors': 0,
            'total_processing_time': 0.0
        }
    
    def extract_text_with_quality_metrics(self, image_path: str) -> Dict[str, Any]:
        """
        Extract text with comprehensive quality metrics
        """
        start_time = time.time()
        
        try:
            # Load document
            doc = DocumentFile.from_images(image_path)
            result = self.model(doc)
            
            # Extract text and confidence scores
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
                            if line_confidences:
                                confidence_scores.append(np.mean(line_confidences))
            
            # Calculate metrics
            full_text = "\n".join(text_blocks)
            avg_confidence = np.mean(confidence_scores) if confidence_scores else 0.0
            processing_time = time.time() - start_time
            
            return {
                "success": True,
                "raw_text": full_text,
                "processing_time": processing_time,
                "confidence": float(avg_confidence),
                "lines_count": len(text_blocks),
                "characters_count": len(full_text),
                "words_count": len(full_text.split()) if full_text else 0
            }
            
        except Exception as e:
            processing_time = time.time() - start_time
            self.logger.error(f"OCR extraction failed for {image_path}: {e}")
            return {
                "success": False,
                "error": str(e),
                "processing_time": processing_time,
                "raw_text": "",
                "confidence": 0.0
            }
    
    def determine_quality_score(self, confidence: float) -> tuple[str, bool]:
        """
        Determine quality score and review requirement
        """
        if confidence >= self.thresholds['high']:
            return "HIGH", False
        elif confidence >= self.thresholds['medium']:
            return "MEDIUM", True
        else:
            return "LOW", True
    
    def extract_structured_fields(self, raw_text: str) -> Dict[str, Any]:
        """
        Enhanced field extraction for production use
        """
        if not raw_text:
            return {}
        
        import re
        extracted = {}
        lines = [line.strip() for line in raw_text.split('\n') if line.strip()]
        full_text = " ".join(lines).upper()
        
        # Enhanced date patterns
        date_patterns = [
            r'FECHA[:\s]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})',
            r'FECHA[:\s]*(\d{1,2}[-/]\d{1,2}[-/]\d{4})',
            r'(\d{1,2}\s+DE\s+\w+\s+DE\s+\d{4})',
            r'(\d{4}[-/]\d{1,2}[-/]\d{1,2})',
        ]
        
        # Authority patterns (comprehensive)
        authority_patterns = [
            r'(CONDUSEF)',
            r'(CNBV)',
            r'(CNSF)',
            r'(BANXICO)',
            r'(COMISION\s+NACIONAL\s+BANCARIA)',
            r'(COMISION\s+NACIONAL.*SEGUROS)',
            r'(PODER\s+JUDICIAL)',
            r'(JUZGADO.*DE.*)'
        ]
        
        # Expediente patterns
        expediente_patterns = [
            r'EXPEDIENTE[:\s]*([A-Z0-9\-/]+)',
            r'EXP(?:EDIENTE)?[:\.\s]*([A-Z0-9\-/]+)',
            r'OFICIO[:\s]*([A-Z0-9\-/]+)',
        ]
        
        # Document type patterns
        doc_type_patterns = [
            r'(REQUERIMIENTO\s+DE\s+INFORMACION)',
            r'(REQUERIMIENTO)',
            r'(SOLICITUD\s+DE\s+INFORMACION)',
            r'(OFICIO)',
            r'(CITATORIO)',
            r'(RESOLUCION)',
        ]
        
        # Extract fields
        for pattern in date_patterns:
            match = re.search(pattern, full_text)
            if match and 'fecha' not in extracted:
                extracted['fecha'] = match.group(1)
                break
        
        for pattern in authority_patterns:
            match = re.search(pattern, full_text)
            if match and 'autoridadEmisora' not in extracted:
                extracted['autoridadEmisora'] = match.group(1)
                break
        
        for pattern in expediente_patterns:
            match = re.search(pattern, full_text)
            if match and 'expediente' not in extracted:
                extracted['expediente'] = match.group(1).strip()
                break
        
        for pattern in doc_type_patterns:
            match = re.search(pattern, full_text)
            if match and 'tipoRequerimiento' not in extracted:
                extracted['tipoRequerimiento'] = match.group(1)
                break
        
        return extracted
    
    def process_single_document(self, image_path: str) -> ClientRequerimiento:
        """
        Process a single document with full production pipeline
        """
        self.logger.info(f"Processing document: {os.path.basename(image_path)}")
        
        # Extract text
        ocr_result = self.extract_text_with_quality_metrics(image_path)
        
        if not ocr_result["success"]:
            self.stats['errors'] += 1
            # Return minimal result with error info
            error_metadata = ProcessingMetadata(
                processing_time=ocr_result.get('processing_time', 0.0),
                ocr_confidence=0.0,
                characters_extracted=0,
                lines_extracted=0,
                quality_score="ERROR",
                review_required=True
            )
            return ClientRequerimiento(processing_metadata=error_metadata)
        
        # Extract structured fields
        structured_data = self.extract_structured_fields(ocr_result["raw_text"])
        
        # Create result object
        requerimiento = ClientRequerimiento()
        
        # Populate fields
        for field in ['fecha', 'autoridadEmisora', 'expediente', 'tipoRequerimiento']:
            if field in structured_data:
                setattr(requerimiento, field, structured_data[field])
        
        # Determine quality and review requirements
        confidence = ocr_result["confidence"]
        quality_score, review_required = self.determine_quality_score(confidence)
        
        # Create metadata
        metadata = ProcessingMetadata(
            processing_time=round(ocr_result["processing_time"], 2),
            ocr_confidence=round(confidence, 3),
            characters_extracted=ocr_result["characters_count"],
            lines_extracted=ocr_result["lines_count"],
            quality_score=quality_score,
            review_required=review_required
        )
        
        requerimiento.processing_metadata = metadata
        
        # Update statistics
        self.stats['total_processed'] += 1
        self.stats['successful'] += 1
        self.stats['total_processing_time'] += ocr_result["processing_time"]
        
        if quality_score == "HIGH":
            self.stats['high_quality'] += 1
        elif quality_score == "MEDIUM":
            self.stats['medium_quality'] += 1
        else:
            self.stats['low_quality'] += 1
        
        self.logger.info(f"✅ Processed successfully - Quality: {quality_score}, Review: {review_required}")
        
        return requerimiento
    
    def process_batch(self, input_dir: str, output_dir: str = "production_results", 
                     max_documents: int = None) -> Dict[str, Any]:
        """
        Process a batch of client documents
        """
        input_path = Path(input_dir)
        output_path = Path(output_dir)
        output_path.mkdir(exist_ok=True)
        
        # Find all image files
        image_extensions = ['*.png', '*.jpg', '*.jpeg', '*.tiff', '*.bmp']
        image_files = []
        for ext in image_extensions:
            image_files.extend(input_path.glob(ext))
        
        if max_documents:
            image_files = image_files[:max_documents]
        
        self.logger.info(f"🚀 Starting batch processing of {len(image_files)} documents")
        
        batch_results = {
            "batch_info": {
                "start_time": datetime.now().isoformat(),
                "input_directory": str(input_path),
                "output_directory": str(output_path),
                "total_documents": len(image_files)
            },
            "results": [],
            "summary": {}
        }
        
        # Process each document
        for i, image_file in enumerate(image_files, 1):
            self.logger.info(f"📄 Processing {i}/{len(image_files)}: {image_file.name}")
            
            try:
                result = self.process_single_document(str(image_file))
                
                # Add to batch results
                document_result = {
                    "filename": image_file.name,
                    "extraction": result.model_dump(),
                    "status": "success"
                }
                
                batch_results["results"].append(document_result)
                
                # Save individual result
                individual_output = output_path / f"{image_file.stem}_result.json"
                with open(individual_output, 'w', encoding='utf-8') as f:
                    json.dump(document_result, f, ensure_ascii=False, indent=2)
                
            except Exception as e:
                self.logger.error(f"❌ Failed to process {image_file.name}: {e}")
                error_result = {
                    "filename": image_file.name,
                    "status": "error",
                    "error": str(e)
                }
                batch_results["results"].append(error_result)
                self.stats['errors'] += 1
        
        # Generate summary
        batch_results["summary"] = self.generate_batch_summary()
        batch_results["batch_info"]["end_time"] = datetime.now().isoformat()
        
        # Save batch results
        batch_output = output_path / "batch_processing_results.json"
        with open(batch_output, 'w', encoding='utf-8') as f:
            json.dump(batch_results, f, ensure_ascii=False, indent=2)
        
        self.logger.info(f"✅ Batch processing complete - Results saved to {batch_output}")
        
        return batch_results
    
    def generate_batch_summary(self) -> Dict[str, Any]:
        """Generate comprehensive batch processing summary"""
        total = self.stats['total_processed'] + self.stats['errors']
        
        if total == 0:
            return {"error": "No documents processed"}
        
        avg_processing_time = (self.stats['total_processing_time'] / 
                              self.stats['successful'] if self.stats['successful'] > 0 else 0)
        
        return {
            "total_documents": total,
            "successful_processing": self.stats['successful'],
            "errors": self.stats['errors'],
            "success_rate": round(self.stats['successful'] / total * 100, 2),
            "quality_distribution": {
                "high_quality": self.stats['high_quality'],
                "medium_quality": self.stats['medium_quality'], 
                "low_quality": self.stats['low_quality']
            },
            "performance_metrics": {
                "avg_processing_time": round(avg_processing_time, 2),
                "total_processing_time": round(self.stats['total_processing_time'], 2),
                "documents_per_minute": round(60 / avg_processing_time if avg_processing_time > 0 else 0, 1)
            },
            "review_recommendations": {
                "auto_approve": self.stats['high_quality'],
                "review_required": self.stats['medium_quality'] + self.stats['low_quality']
            }
        }

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    parser = argparse.ArgumentParser(description="Production OCR Batch Processor for Client Documents")
    parser.add_argument("--input", required=True, help="Input directory with client documents")
    parser.add_argument("--output", default="production_results", help="Output directory")
    parser.add_argument("--max-docs", type=int, help="Maximum documents to process")
    parser.add_argument("--high-threshold", type=float, default=0.80, help="High quality threshold")
    parser.add_argument("--medium-threshold", type=float, default=0.50, help="Medium quality threshold")
    
    args = parser.parse_args()
    
    # Setup thresholds
    thresholds = {
        'high': args.high_threshold,
        'medium': args.medium_threshold,
        'low': 0.0
    }
    
    # Initialize processor
    processor = ProductionOCRProcessor(confidence_thresholds=thresholds)
    
    # Process batch
    results = processor.process_batch(
        input_dir=args.input,
        output_dir=args.output,
        max_documents=args.max_docs
    )
    
    # Print summary
    summary = results["summary"]
    print(f"\n🏆 PRODUCTION BATCH PROCESSING COMPLETE")
    print(f"📊 Success Rate: {summary['success_rate']}%")
    print(f"⏱️  Average Processing: {summary['performance_metrics']['avg_processing_time']}s")
    print(f"🚀 Throughput: {summary['performance_metrics']['documents_per_minute']} docs/min")
    print(f"✅ Auto-approve: {summary['review_recommendations']['auto_approve']} documents")
    print(f"👀 Review required: {summary['review_recommendations']['review_required']} documents")

if __name__ == "__main__":
    main()