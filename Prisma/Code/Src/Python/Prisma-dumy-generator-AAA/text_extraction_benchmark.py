#!/usr/bin/env python3
"""
Text Extraction Quality Benchmark
Phase 1: Pure OCR text extraction capabilities (no classification)

Mission 3: Performance Analysis & Optimization Framework
Focus: Raw text extraction quality, speed, confidence
"""

import json
import os
import time
import argparse
from typing import Dict, List, Any, Optional, Tuple
from pathlib import Path

# Core libraries  
from datetime import datetime

# -------------------------------
# Text Extraction Benchmarker
# -------------------------------
class TextExtractionBenchmark:
    """
    Pure text extraction benchmarking (Phase 1)
    Evaluates OCR models on raw text extraction quality only
    """
    
    def __init__(self, results_dir: str = "text_extraction_results"):
        self.results_dir = Path(results_dir)
        self.results_dir.mkdir(exist_ok=True)
        
        # Available text extractors
        self.available_extractors = {
            'DocTR': self._extract_with_doctr,
            'PaddleOCR': self._extract_with_paddleocr,
            'SmolVLM': self._extract_with_smolvlm,
            'GOT-OCR2': self._extract_with_got_ocr2,
        }
        
        # Models cache
        self._models_cache = {}
        
        print(f"[INFO] Text Extraction Benchmark initialized")
        print(f"[INFO] Focus: Raw OCR text extraction quality")
    
    def _load_doctr(self):
        """Load DocTR model"""
        if 'DocTR' not in self._models_cache:
            try:
                from doctr.io import DocumentFile
                from doctr.models import ocr_predictor
                model = ocr_predictor(pretrained=True)
                self._models_cache['DocTR'] = {'model': model, 'DocumentFile': DocumentFile}
                print("[SUCCESS] DocTR model loaded")
            except Exception as e:
                print(f"[ERROR] DocTR load failed: {e}")
                return None
        return self._models_cache['DocTR']
    
    def _extract_with_doctr(self, image_path: str) -> Dict[str, Any]:
        """Extract text using DocTR"""
        doctr_data = self._load_doctr()
        if not doctr_data:
            return {"error": "DocTR not available", "raw_text": ""}
            
        start_time = time.time()
        
        try:
            doc = doctr_data['DocumentFile'].from_images(image_path)
            result = doctr_data['model'](doc)
            
            # Extract text and confidence
            text_blocks = []
            confidences = []
            
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
                                confidences.append(sum(line_confidences) / len(line_confidences))
            
            raw_text = "\n".join(text_blocks)
            avg_confidence = sum(confidences) / len(confidences) if confidences else 0.0
            
            return {
                "raw_text": raw_text,
                "processing_time": time.time() - start_time,
                "confidence": round(avg_confidence, 3),
                "lines_count": len(text_blocks),
                "characters_count": len(raw_text),
                "words_count": len(raw_text.split()) if raw_text else 0
            }
            
        except Exception as e:
            return {
                "error": str(e),
                "raw_text": "",
                "processing_time": time.time() - start_time
            }
    
    def _load_paddleocr(self):
        """Load PaddleOCR model"""
        if 'PaddleOCR' not in self._models_cache:
            try:
                from paddleocr import PaddleOCR
                model = PaddleOCR(lang='en')
                self._models_cache['PaddleOCR'] = model
                print("[SUCCESS] PaddleOCR model loaded")
            except Exception as e:
                print(f"[ERROR] PaddleOCR load failed: {e}")
                return None
        return self._models_cache['PaddleOCR']
    
    def _extract_with_paddleocr(self, image_path: str) -> Dict[str, Any]:
        """Extract text using PaddleOCR"""
        model = self._load_paddleocr()
        if not model:
            return {"error": "PaddleOCR not available", "raw_text": ""}
            
        start_time = time.time()
        
        try:
            result = model.ocr(image_path)
            
            if not result or not result[0]:
                return {
                    "raw_text": "",
                    "processing_time": time.time() - start_time,
                    "confidence": 0.0,
                    "lines_count": 0,
                    "characters_count": 0,
                    "words_count": 0
                }
            
            # Extract and sort text by position
            text_data = []
            for line in result[0]:
                if line and len(line) == 2:
                    bbox, (text, confidence) = line
                    if text and text.strip():
                        y_pos = (bbox[0][1] + bbox[1][1]) / 2  # Average y position
                        text_data.append((y_pos, text.strip(), confidence))
            
            # Sort by y position for reading order
            text_data.sort(key=lambda x: x[0])
            
            text_blocks = [item[1] for item in text_data]
            confidences = [item[2] for item in text_data]
            
            raw_text = "\n".join(text_blocks)
            avg_confidence = sum(confidences) / len(confidences) if confidences else 0.0
            
            return {
                "raw_text": raw_text,
                "processing_time": time.time() - start_time,
                "confidence": round(avg_confidence, 3),
                "lines_count": len(text_blocks),
                "characters_count": len(raw_text),
                "words_count": len(raw_text.split()) if raw_text else 0
            }
            
        except Exception as e:
            return {
                "error": str(e),
                "raw_text": "",
                "processing_time": time.time() - start_time
            }
    
    def _extract_with_smolvlm(self, image_path: str) -> Dict[str, Any]:
        """Extract text using SmolVLM (text only, no structured extraction)"""
        # For now, return placeholder - SmolVLM needs debugging
        return {
            "error": "SmolVLM implementation needs debugging for raw text extraction",
            "raw_text": "",
            "processing_time": 0.0
        }
    
    def _extract_with_got_ocr2(self, image_path: str) -> Dict[str, Any]:
        """Extract text using GOT-OCR2"""
        # For now, return placeholder - GOT-OCR2 needs testing
        return {
            "error": "GOT-OCR2 implementation needs testing",
            "raw_text": "",
            "processing_time": 0.0
        }
    
    def extract_text_single_image(self, image_path: str, extractors: List[str] = None) -> Dict[str, Any]:
        """
        Extract text from single image using multiple extractors
        """
        if extractors is None:
            extractors = ['DocTR', 'PaddleOCR']  # Working extractors only
            
        print(f"\n[INFO] Text extraction: {os.path.basename(image_path)}")
        
        results = {
            "image_path": image_path,
            "timestamp": datetime.now().isoformat(),
            "extractors": {}
        }
        
        for extractor_name in extractors:
            if extractor_name not in self.available_extractors:
                continue
                
            print(f"  Extracting with {extractor_name}...")
            
            extractor_func = self.available_extractors[extractor_name]
            result = extractor_func(image_path)
            
            results["extractors"][extractor_name] = result
            
            # Print summary
            if "error" in result:
                print(f"    ❌ {extractor_name}: ERROR - {result['error'][:50]}...")
            else:
                print(f"    ✅ {extractor_name}: {result['processing_time']:.2f}s, " +
                     f"confidence: {result.get('confidence', 'N/A')}, " +
                     f"chars: {result.get('characters_count', 0)}")
        
        return results
    
    def benchmark_text_extraction(self, image_dir: str, extractors: List[str] = None, max_images: int = 5) -> List[Dict]:
        """
        Benchmark text extraction on multiple images
        """
        image_dir = Path(image_dir)
        
        # Find images
        image_files = []
        for ext in ['*.png', '*.jpg', '*.jpeg']:
            image_files.extend(image_dir.glob(ext))
        
        if max_images:
            image_files = image_files[:max_images]
            
        print(f"[INFO] Benchmarking text extraction on {len(image_files)} images")
        
        all_results = []
        
        for i, image_path in enumerate(image_files, 1):
            print(f"\n--- Image {i}/{len(image_files)} ---")
            
            result = self.extract_text_single_image(str(image_path), extractors)
            all_results.append(result)
        
        return all_results
    
    def save_results(self, results: List[Dict], filename: str = None):
        """Save benchmark results"""
        if filename is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"text_extraction_{timestamp}.json"
            
        filepath = self.results_dir / filename
        
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(results, f, ensure_ascii=False, indent=2)
            
        print(f"[INFO] Results saved to: {filepath}")
        return filepath
    
    def generate_text_quality_report(self, results: List[Dict]) -> Dict[str, Any]:
        """
        Generate text extraction quality report
        """
        if not results:
            return {"error": "No results to analyze"}
            
        # Initialize report
        report = {
            "benchmark_info": {
                "total_images": len(results),
                "timestamp": datetime.now().isoformat(),
                "phase": "Text Extraction Quality (Phase 1)"
            },
            "extractor_performance": {},
            "text_quality_metrics": {}
        }
        
        # Get all extractor names
        all_extractors = set()
        for result in results:
            all_extractors.update(result["extractors"].keys())
        
        # Analyze each extractor
        for extractor_name in all_extractors:
            successful_extractions = 0
            total_chars = 0
            total_words = 0
            total_lines = 0
            processing_times = []
            confidences = []
            
            total_tests = 0
            
            for result in results:
                if extractor_name not in result["extractors"]:
                    continue
                    
                total_tests += 1
                extractor_result = result["extractors"][extractor_name]
                
                if "error" not in extractor_result:
                    successful_extractions += 1
                    total_chars += extractor_result.get('characters_count', 0)
                    total_words += extractor_result.get('words_count', 0)
                    total_lines += extractor_result.get('lines_count', 0)
                    
                    if 'confidence' in extractor_result and extractor_result['confidence']:
                        confidences.append(extractor_result['confidence'])
                
                if 'processing_time' in extractor_result:
                    processing_times.append(extractor_result['processing_time'])
            
            # Calculate metrics
            report["extractor_performance"][extractor_name] = {
                "success_rate": round(successful_extractions / total_tests * 100, 2) if total_tests > 0 else 0,
                "avg_processing_time": round(sum(processing_times) / len(processing_times), 2) if processing_times else 0,
                "avg_confidence": round(sum(confidences) / len(confidences), 3) if confidences else 0,
                "successful_extractions": successful_extractions,
                "total_tests": total_tests
            }
            
            if successful_extractions > 0:
                report["text_quality_metrics"][extractor_name] = {
                    "avg_characters_per_doc": round(total_chars / successful_extractions),
                    "avg_words_per_doc": round(total_words / successful_extractions),
                    "avg_lines_per_doc": round(total_lines / successful_extractions),
                    "total_characters_extracted": total_chars,
                    "total_words_extracted": total_words
                }
        
        return report
    
    def print_text_quality_report(self, report: Dict[str, Any]):
        """Print formatted text quality report"""
        print("\n" + "="*80)
        print("TEXT EXTRACTION QUALITY REPORT (Phase 1)")
        print("="*80)
        
        info = report["benchmark_info"]
        print(f"Images tested: {info['total_images']}")
        print(f"Focus: Pure OCR text extraction capabilities")
        print(f"Generated: {info['timestamp']}")
        
        print("\n" + "-"*60)
        print("EXTRACTOR PERFORMANCE")
        print("-"*60)
        
        for extractor, perf in report["extractor_performance"].items():
            print(f"\n{extractor}:")
            print(f"  Success Rate: {perf['success_rate']}%")
            print(f"  Avg Processing Time: {perf['avg_processing_time']}s")
            print(f"  Avg Confidence: {perf['avg_confidence']}")
            print(f"  Successful Extractions: {perf['successful_extractions']}/{perf['total_tests']}")
        
        print("\n" + "-"*60)
        print("TEXT QUALITY METRICS")
        print("-"*60)
        
        for extractor, metrics in report["text_quality_metrics"].items():
            print(f"\n{extractor}:")
            print(f"  Avg Characters/Doc: {metrics['avg_characters_per_doc']}")
            print(f"  Avg Words/Doc: {metrics['avg_words_per_doc']}")
            print(f"  Avg Lines/Doc: {metrics['avg_lines_per_doc']}")
            print(f"  Total Characters: {metrics['total_characters_extracted']}")
        
        print("\n" + "="*80)

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    parser = argparse.ArgumentParser(description="Text Extraction Quality Benchmark (Phase 1)")
    parser.add_argument("--image", help="Single image to benchmark")
    parser.add_argument("--dataset", help="Directory containing images")
    parser.add_argument("--extractors", nargs="+", help="Extractors to test", 
                       choices=['DocTR', 'PaddleOCR', 'SmolVLM', 'GOT-OCR2'],
                       default=['DocTR', 'PaddleOCR'])
    parser.add_argument("--max-images", type=int, default=5, help="Max images to test")
    parser.add_argument("--output", help="Output file for results")
    parser.add_argument("--report", action="store_true", help="Generate quality report")
    
    args = parser.parse_args()
    
    # Initialize benchmark
    benchmark = TextExtractionBenchmark()
    
    results = []
    
    if args.image:
        if not os.path.exists(args.image):
            print(f"Error: Image not found: {args.image}")
            exit(1)
        result = benchmark.extract_text_single_image(args.image, args.extractors)
        results.append(result)
        
    elif args.dataset:
        if not os.path.exists(args.dataset):
            print(f"Error: Dataset directory not found: {args.dataset}")
            exit(1)
        results = benchmark.benchmark_text_extraction(args.dataset, args.extractors, args.max_images)
        
    else:
        print("Error: Specify --image or --dataset")
        exit(1)
    
    # Save results
    output_file = benchmark.save_results(results, args.output)
    
    # Generate report
    if args.report:
        report = benchmark.generate_text_quality_report(results)
        
        # Save report
        report_file = str(output_file).replace('.json', '_report.json')
        with open(report_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
        
        benchmark.print_text_quality_report(report)
        print(f"\n[INFO] Report saved to: {report_file}")

if __name__ == "__main__":
    main()