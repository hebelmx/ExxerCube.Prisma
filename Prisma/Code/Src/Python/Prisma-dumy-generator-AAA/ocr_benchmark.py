#!/usr/bin/env python3
"""
OCR Models Benchmarking Framework
Comprehensive testing suite for multiple OCR engines on Spanish legal documents

Mission 3: Performance Analysis & Optimization Framework
Benchmarks: SmolVLM2, DocTR, PaddleOCR, GOT-OCR2
"""

import json
import os
import time
import argparse
import traceback
from typing import Dict, List, Any, Optional, Tuple
from pathlib import Path
import importlib.util

# Core libraries
import pandas as pd
from datetime import datetime

# -------------------------------
# Benchmarking Framework
# -------------------------------
class OCRBenchmark:
    """
    Multi-model OCR benchmarking suite
    """
    
    def __init__(self, results_dir: str = "benchmark_results"):
        self.results_dir = Path(results_dir)
        self.results_dir.mkdir(exist_ok=True)
        
        # Available OCR models
        self.available_models = {
            'SmolVLM': 'smolvlm_extractor.py',
            'DocTR': 'doctr_extractor.py', 
            'PaddleOCR': 'paddleocr_extractor.py',
            'GOT-OCR2': 'got_ocr2_extractor.py'
        }
        
        # Loaded models cache
        self.loaded_models = {}
        
        print(f"[INFO] OCR Benchmark initialized")
        print(f"[INFO] Results directory: {self.results_dir}")
    
    def load_model(self, model_name: str) -> Optional[Any]:
        """
        Dynamically load an OCR model
        """
        if model_name in self.loaded_models:
            return self.loaded_models[model_name]
            
        if model_name not in self.available_models:
            print(f"[ERROR] Unknown model: {model_name}")
            return None
            
        script_file = self.available_models[model_name]
        
        try:
            # Import the extractor module
            spec = importlib.util.spec_from_file_location(
                f"{model_name.lower()}_extractor", 
                script_file
            )
            if spec is None:
                print(f"[ERROR] Cannot load {script_file}")
                return None
                
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            
            self.loaded_models[model_name] = module
            print(f"[SUCCESS] Loaded {model_name} model")
            return module
            
        except Exception as e:
            print(f"[ERROR] Failed to load {model_name}: {e}")
            return None
    
    def extract_with_model(self, model_name: str, image_path: str) -> Dict[str, Any]:
        """
        Extract information using a specific model
        """
        model_module = self.load_model(model_name)
        if not model_module:
            return {
                "error": f"Model {model_name} not available",
                "model": model_name,
                "processing_time": 0.0
            }
        
        try:
            # Call the extraction function
            result = model_module.extract_requerimiento_from_image(image_path)
            
            # Ensure metadata includes model name
            if isinstance(result, dict) and '_metadata' not in result:
                result['_metadata'] = {'model': model_name}
            elif isinstance(result, dict) and '_metadata' in result:
                result['_metadata']['model'] = model_name
                
            return result
            
        except Exception as e:
            return {
                "error": str(e),
                "model": model_name,
                "processing_time": 0.0,
                "traceback": traceback.format_exc()
            }
    
    def benchmark_single_image(self, image_path: str, models: List[str] = None) -> Dict[str, Any]:
        """
        Benchmark multiple models on a single image
        """
        if models is None:
            models = list(self.available_models.keys())
            
        print(f"\n[INFO] Benchmarking image: {os.path.basename(image_path)}")
        
        results = {
            "image_path": image_path,
            "timestamp": datetime.now().isoformat(),
            "models": {}
        }
        
        for model_name in models:
            print(f"  Testing {model_name}...")
            start_time = time.time()
            
            model_result = self.extract_with_model(model_name, image_path)
            
            # Ensure model_result is a dictionary
            if not isinstance(model_result, dict):
                model_result = {
                    "error": f"Model returned non-dict result: {type(model_result)}",
                    "model": model_name,
                    "processing_time": time.time() - start_time
                }
            
            # Add timing if not present
            if 'processing_time' not in model_result:
                model_result['processing_time'] = time.time() - start_time
            
            results["models"][model_name] = model_result
            
            # Print quick summary
            if "error" in model_result:
                print(f"    ❌ {model_name}: ERROR - {str(model_result['error'])[:50]}...")
            else:
                processing_time = model_result.get('processing_time', 0)
                confidence = "N/A"
                if '_metadata' in model_result:
                    confidence = model_result['_metadata'].get('ocr_confidence', 
                                model_result['_metadata'].get('average_confidence', 'N/A'))
                    
                print(f"    ✅ {model_name}: {processing_time:.2f}s, confidence: {confidence}")
        
        return results
    
    def benchmark_dataset(self, image_dir: str, models: List[str] = None, max_images: int = None) -> List[Dict]:
        """
        Benchmark multiple models on a dataset of images
        """
        image_dir = Path(image_dir)
        
        # Find all image files
        image_files = []
        for ext in ['*.png', '*.jpg', '*.jpeg']:
            image_files.extend(image_dir.glob(ext))
            
        if max_images:
            image_files = image_files[:max_images]
            
        print(f"[INFO] Found {len(image_files)} images to benchmark")
        
        all_results = []
        
        for i, image_path in enumerate(image_files, 1):
            print(f"\n--- Image {i}/{len(image_files)} ---")
            
            result = self.benchmark_single_image(str(image_path), models)
            all_results.append(result)
            
            # Save intermediate results
            if i % 5 == 0:
                self.save_results(all_results, f"benchmark_interim_{i}.json")
        
        return all_results
    
    def save_results(self, results: List[Dict], filename: str = None):
        """
        Save benchmark results to JSON file
        """
        if filename is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"ocr_benchmark_{timestamp}.json"
            
        filepath = self.results_dir / filename
        
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(results, f, ensure_ascii=False, indent=2)
            
        print(f"[INFO] Results saved to: {filepath}")
        return filepath
    
    def generate_summary_report(self, results: List[Dict]) -> Dict[str, Any]:
        """
        Generate comprehensive summary report from benchmark results
        """
        if not results:
            return {"error": "No results to analyze"}
            
        # Initialize summary structure
        summary = {
            "benchmark_info": {
                "total_images": len(results),
                "timestamp": datetime.now().isoformat(),
                "models_tested": []
            },
            "model_performance": {},
            "extraction_success": {},
            "average_processing_time": {},
            "confidence_stats": {},
            "field_extraction_success": {}
        }
        
        # Get all model names
        all_models = set()
        for result in results:
            all_models.update(result["models"].keys())
        
        summary["benchmark_info"]["models_tested"] = list(all_models)
        
        # Analyze each model
        for model_name in all_models:
            successful_extractions = 0
            processing_times = []
            confidences = []
            field_extractions = {
                'fecha': 0, 'autoridadEmisora': 0, 'expediente': 0, 
                'tipoRequerimiento': 0, 'total_fields': 0
            }
            
            for result in results:
                if model_name not in result["models"]:
                    continue
                    
                model_result = result["models"][model_name]
                
                # Success rate
                if "error" not in model_result:
                    successful_extractions += 1
                    
                    # Field extraction analysis
                    for field in ['fecha', 'autoridadEmisora', 'expediente', 'tipoRequerimiento']:
                        if model_result.get(field, "unknown") != "unknown":
                            field_extractions[field] += 1
                            field_extractions['total_fields'] += 1
                
                # Processing time
                if 'processing_time' in model_result:
                    processing_times.append(model_result['processing_time'])
                
                # Confidence scores
                if '_metadata' in model_result:
                    metadata = model_result['_metadata']
                    conf = metadata.get('ocr_confidence', metadata.get('average_confidence'))
                    if conf and isinstance(conf, (int, float)):
                        confidences.append(float(conf))
            
            # Calculate statistics
            total_tests = len([r for r in results if model_name in r["models"]])
            
            summary["model_performance"][model_name] = {
                "total_tests": total_tests,
                "successful_extractions": successful_extractions,
                "success_rate": round(successful_extractions / total_tests * 100, 2) if total_tests > 0 else 0
            }
            
            summary["average_processing_time"][model_name] = {
                "avg_time": round(sum(processing_times) / len(processing_times), 2) if processing_times else 0,
                "min_time": round(min(processing_times), 2) if processing_times else 0,
                "max_time": round(max(processing_times), 2) if processing_times else 0
            }
            
            summary["confidence_stats"][model_name] = {
                "avg_confidence": round(sum(confidences) / len(confidences), 3) if confidences else 0,
                "min_confidence": round(min(confidences), 3) if confidences else 0,
                "max_confidence": round(max(confidences), 3) if confidences else 0,
                "samples_with_confidence": len(confidences)
            }
            
            # Field extraction rates
            summary["field_extraction_success"][model_name] = {}
            for field, count in field_extractions.items():
                if field != 'total_fields':
                    rate = round(count / successful_extractions * 100, 2) if successful_extractions > 0 else 0
                    summary["field_extraction_success"][model_name][field] = {
                        "extracted": count,
                        "success_rate": rate
                    }
        
        return summary
    
    def print_summary_report(self, summary: Dict[str, Any]):
        """
        Print formatted summary report to console
        """
        print("\n" + "="*80)
        print("OCR MODELS BENCHMARK SUMMARY REPORT")
        print("="*80)
        
        info = summary["benchmark_info"]
        print(f"Images tested: {info['total_images']}")
        print(f"Models tested: {', '.join(info['models_tested'])}")
        print(f"Generated: {info['timestamp']}")
        
        print("\n" + "-"*50)
        print("OVERALL PERFORMANCE")
        print("-"*50)
        
        # Create performance table
        performance_data = []
        for model in info['models_tested']:
            if model in summary["model_performance"]:
                perf = summary["model_performance"][model]
                time_stats = summary["average_processing_time"][model]
                conf_stats = summary["confidence_stats"][model]
                
                performance_data.append({
                    "Model": model,
                    "Success Rate": f"{perf['success_rate']}%",
                    "Avg Time (s)": f"{time_stats['avg_time']}",
                    "Avg Confidence": f"{conf_stats['avg_confidence']}"
                })
        
        if performance_data:
            df = pd.DataFrame(performance_data)
            print(df.to_string(index=False))
        
        print("\n" + "-"*50)
        print("FIELD EXTRACTION RATES")
        print("-"*50)
        
        for model in info['models_tested']:
            if model in summary["field_extraction_success"]:
                print(f"\n{model}:")
                field_data = summary["field_extraction_success"][model]
                for field, stats in field_data.items():
                    print(f"  {field}: {stats['success_rate']}% ({stats['extracted']} extracted)")
        
        print("\n" + "="*80)

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    parser = argparse.ArgumentParser(description="OCR Models Benchmarking Suite")
    parser.add_argument("--image", help="Single image to benchmark")
    parser.add_argument("--dataset", help="Directory containing images to benchmark")
    parser.add_argument("--models", nargs="+", help="Models to test", 
                       choices=['SmolVLM', 'DocTR', 'PaddleOCR', 'GOT-OCR2'],
                       default=['SmolVLM', 'DocTR', 'PaddleOCR'])
    parser.add_argument("--max-images", type=int, help="Maximum number of images to test")
    parser.add_argument("--output", help="Output file for results")
    parser.add_argument("--report", action="store_true", help="Generate summary report")
    
    args = parser.parse_args()
    
    # Initialize benchmark
    benchmark = OCRBenchmark()
    
    results = []
    
    if args.image:
        # Single image benchmark
        if not os.path.exists(args.image):
            print(f"Error: Image file not found: {args.image}")
            exit(1)
        
        result = benchmark.benchmark_single_image(args.image, args.models)
        results.append(result)
        
    elif args.dataset:
        # Dataset benchmark
        if not os.path.exists(args.dataset):
            print(f"Error: Dataset directory not found: {args.dataset}")
            exit(1)
        
        results = benchmark.benchmark_dataset(args.dataset, args.models, args.max_images)
        
    else:
        print("Error: Please specify either --image or --dataset")
        exit(1)
    
    # Save results
    output_file = benchmark.save_results(results, args.output)
    
    # Generate and print summary report
    if args.report:
        summary = benchmark.generate_summary_report(results)
        
        # Save summary
        summary_file = str(output_file).replace('.json', '_summary.json')
        with open(summary_file, 'w', encoding='utf-8') as f:
            json.dump(summary, f, ensure_ascii=False, indent=2)
        
        benchmark.print_summary_report(summary)
        print(f"\n[INFO] Summary saved to: {summary_file}")

if __name__ == "__main__":
    main()