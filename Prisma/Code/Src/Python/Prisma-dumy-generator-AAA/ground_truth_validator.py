#!/usr/bin/env python3
"""
Ground Truth Validation for OCR Results
Compare OCR extraction against original source data (test_corpus.json)

Mission 3: Performance Analysis & Optimization Framework
Validates OCR accuracy using known ground truth from document generation
"""

import json
import os
import argparse
import re
from typing import Dict, List, Any, Optional, Tuple
from pathlib import Path
from datetime import datetime
from difflib import SequenceMatcher

# -------------------------------
# Ground Truth Validator
# -------------------------------
class GroundTruthValidator:
    """
    Validates OCR results against original source data
    """
    
    def __init__(self, corpus_file: str = "test_corpus.json"):
        self.corpus_file = corpus_file
        self.ground_truth = self._load_ground_truth()
        
        print(f"[INFO] Ground Truth Validator initialized")
        print(f"[INFO] Loaded {len(self.ground_truth)} ground truth documents")
    
    def _load_ground_truth(self) -> List[Dict]:
        """Load the original test corpus"""
        try:
            with open(self.corpus_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
                
            # Handle both direct list and wrapped format
            if isinstance(data, list):
                return data
            elif isinstance(data, dict) and 'documents' in data:
                return data['documents']
            else:
                print(f"[ERROR] Unexpected corpus format")
                return []
                
        except Exception as e:
            print(f"[ERROR] Failed to load ground truth: {e}")
            return []
    
    def get_ground_truth_for_fixture(self, fixture_number: int) -> Optional[Dict]:
        """
        Get ground truth data for a specific fixture number
        Fixture001.png corresponds to index 0, etc.
        """
        index = fixture_number - 1  # Convert to 0-based index
        
        if 0 <= index < len(self.ground_truth):
            return self.ground_truth[index]
        else:
            return None
    
    def extract_fixture_number(self, filename: str) -> Optional[int]:
        """Extract fixture number from filename (e.g., Fixture123.png -> 123)"""
        match = re.search(r'Fixture(\d+)', filename)
        if match:
            return int(match.group(1))
        return None
    
    def normalize_text(self, text: str) -> str:
        """Normalize text for comparison"""
        if not text:
            return ""
            
        # Convert to uppercase, remove extra whitespace
        normalized = re.sub(r'\s+', ' ', text.upper().strip())
        
        # Remove common OCR artifacts
        normalized = normalized.replace('—', '-')  # Em dash to hyphen
        normalized = normalized.replace('–', '-')  # En dash to hyphen
        
        return normalized
    
    def calculate_text_similarity(self, ocr_text: str, ground_truth_text: str) -> float:
        """Calculate text similarity using SequenceMatcher"""
        norm_ocr = self.normalize_text(ocr_text)
        norm_truth = self.normalize_text(ground_truth_text)
        
        if not norm_ocr and not norm_truth:
            return 1.0  # Both empty
        if not norm_ocr or not norm_truth:
            return 0.0  # One empty
        
        matcher = SequenceMatcher(None, norm_ocr, norm_truth)
        return matcher.ratio()
    
    def validate_field_extraction(self, ocr_result: Dict, ground_truth: Dict) -> Dict[str, Any]:
        """
        Validate field extraction accuracy
        """
        validation = {
            'field_accuracy': {},
            'exact_matches': 0,
            'partial_matches': 0,
            'total_fields': 0
        }
        
        # Fields to validate
        field_mappings = {
            'fecha': ['fecha'],
            'autoridadEmisora': ['autoridad_emisora', 'autoridadEmisora'], 
            'expediente': ['expediente', 'numero_expediente'],
            'tipoRequerimiento': ['tipo_requerimiento', 'tipoRequerimiento'],
            'motivacion': ['motivacion', 'causa'],
        }
        
        for ocr_field, possible_gt_fields in field_mappings.items():
            validation['total_fields'] += 1
            
            ocr_value = ocr_result.get(ocr_field, "unknown")
            if ocr_value == "unknown":
                ocr_value = ""
            
            # Find corresponding ground truth field
            gt_value = ""
            for gt_field in possible_gt_fields:
                if gt_field in ground_truth:
                    gt_value = str(ground_truth[gt_field])
                    break
            
            # Calculate accuracy
            if not ocr_value and not gt_value:
                accuracy = 1.0  # Both empty/unknown
                match_type = "exact"
            elif not ocr_value or not gt_value:
                accuracy = 0.0  # One empty
                match_type = "none"
            else:
                accuracy = self.calculate_text_similarity(ocr_value, gt_value)
                if accuracy >= 0.9:
                    match_type = "exact"
                    validation['exact_matches'] += 1
                elif accuracy >= 0.5:
                    match_type = "partial"
                    validation['partial_matches'] += 1
                else:
                    match_type = "none"
            
            validation['field_accuracy'][ocr_field] = {
                'accuracy': round(accuracy, 3),
                'match_type': match_type,
                'ocr_value': ocr_value,
                'ground_truth': gt_value
            }
        
        return validation
    
    def validate_single_result(self, ocr_result: Dict, image_path: str) -> Dict[str, Any]:
        """
        Validate a single OCR result against ground truth
        """
        # Extract fixture number from filename
        filename = os.path.basename(image_path)
        fixture_num = self.extract_fixture_number(filename)
        
        if fixture_num is None:
            return {
                "error": f"Could not extract fixture number from {filename}",
                "validation_status": "failed"
            }
        
        # Get ground truth
        ground_truth = self.get_ground_truth_for_fixture(fixture_num)
        if ground_truth is None:
            return {
                "error": f"No ground truth found for fixture {fixture_num}",
                "validation_status": "failed"
            }
        
        # Validate
        validation = {
            "fixture_number": fixture_num,
            "image_path": image_path,
            "validation_status": "success",
            "timestamp": datetime.now().isoformat()
        }
        
        # Text similarity validation
        ocr_text = ""
        if '_metadata' in ocr_result and 'raw_text_preview' in ocr_result['_metadata']:
            ocr_text = ocr_result['_metadata']['raw_text_preview']
        
        # Create expected text from ground truth (approximate)
        expected_text_parts = []
        for field in ['autoridad_emisora', 'fecha', 'expediente', 'tipo_requerimiento', 'motivacion']:
            if field in ground_truth and ground_truth[field]:
                expected_text_parts.append(str(ground_truth[field]))
        
        expected_text = " ".join(expected_text_parts)
        
        text_similarity = self.calculate_text_similarity(ocr_text, expected_text)
        
        validation['text_validation'] = {
            'similarity_score': round(text_similarity, 3),
            'ocr_text_length': len(ocr_text),
            'expected_elements': len(expected_text_parts)
        }
        
        # Field extraction validation
        validation['field_validation'] = self.validate_field_extraction(ocr_result, ground_truth)
        
        return validation
    
    def validate_batch_results(self, batch_results: List[Dict]) -> Dict[str, Any]:
        """
        Validate a batch of OCR results
        """
        print(f"[INFO] Validating batch of {len(batch_results)} results against ground truth")
        
        batch_validation = {
            "batch_info": {
                "total_documents": len(batch_results),
                "timestamp": datetime.now().isoformat(),
                "validation_type": "ground_truth_comparison"
            },
            "individual_results": [],
            "aggregate_metrics": {}
        }
        
        # Validate each result
        successful_validations = 0
        text_similarities = []
        field_accuracies = {
            'fecha': [], 'autoridadEmisora': [], 'expediente': [], 
            'tipoRequerimiento': [], 'motivacion': []
        }
        
        for result in batch_results:
            image_path = result.get('image_path', '')
            
            # Find the extractor result (assuming DocTR)
            ocr_result = None
            if 'extractors' in result and 'DocTR' in result['extractors']:
                ocr_result = result['extractors']['DocTR']
            elif 'models' in result and 'DocTR' in result['models']:
                ocr_result = result['models']['DocTR']
            
            if not ocr_result or 'error' in ocr_result:
                validation = {
                    "error": "OCR result not found or contains errors",
                    "validation_status": "failed",
                    "image_path": image_path
                }
            else:
                validation = self.validate_single_result(ocr_result, image_path)
            
            batch_validation['individual_results'].append(validation)
            
            # Aggregate metrics
            if validation.get('validation_status') == 'success':
                successful_validations += 1
                
                # Text similarity
                if 'text_validation' in validation:
                    text_similarities.append(validation['text_validation']['similarity_score'])
                
                # Field accuracies
                if 'field_validation' in validation:
                    for field, metrics in validation['field_validation']['field_accuracy'].items():
                        if field in field_accuracies:
                            field_accuracies[field].append(metrics['accuracy'])
        
        # Calculate aggregate metrics
        batch_validation['aggregate_metrics'] = {
            'validation_success_rate': round(successful_validations / len(batch_results) * 100, 2),
            'avg_text_similarity': round(sum(text_similarities) / len(text_similarities), 3) if text_similarities else 0,
            'field_accuracy_averages': {}
        }
        
        for field, accuracies in field_accuracies.items():
            if accuracies:
                batch_validation['aggregate_metrics']['field_accuracy_averages'][field] = {
                    'avg_accuracy': round(sum(accuracies) / len(accuracies), 3),
                    'min_accuracy': round(min(accuracies), 3),
                    'max_accuracy': round(max(accuracies), 3),
                    'samples': len(accuracies)
                }
        
        return batch_validation
    
    def save_validation_results(self, validation_results: Dict, filename: str = None):
        """Save validation results to JSON file"""
        if filename is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"ground_truth_validation_{timestamp}.json"
        
        results_dir = Path("validation_results")
        results_dir.mkdir(exist_ok=True)
        filepath = results_dir / filename
        
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(validation_results, f, ensure_ascii=False, indent=2)
        
        print(f"[INFO] Validation results saved to: {filepath}")
        return filepath
    
    def print_validation_summary(self, validation_results: Dict):
        """Print formatted validation summary"""
        print("\n" + "="*80)
        print("GROUND TRUTH VALIDATION REPORT")
        print("="*80)
        
        batch_info = validation_results['batch_info']
        metrics = validation_results['aggregate_metrics']
        
        print(f"Documents validated: {batch_info['total_documents']}")
        print(f"Validation success rate: {metrics['validation_success_rate']}%")
        print(f"Average text similarity: {metrics['avg_text_similarity']}")
        
        print("\n" + "-"*60)
        print("FIELD ACCURACY ANALYSIS")
        print("-"*60)
        
        for field, stats in metrics['field_accuracy_averages'].items():
            print(f"\n{field}:")
            print(f"  Average Accuracy: {stats['avg_accuracy']}")
            print(f"  Range: {stats['min_accuracy']} - {stats['max_accuracy']}")
            print(f"  Samples: {stats['samples']}")
        
        print("\n" + "="*80)

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    parser = argparse.ArgumentParser(description="Ground Truth OCR Validation")
    parser.add_argument("--batch-results", required=True, help="JSON file with OCR batch results")
    parser.add_argument("--corpus", default="test_corpus.json", help="Ground truth corpus file")
    parser.add_argument("--output", help="Output validation file")
    parser.add_argument("--summary", action="store_true", help="Print validation summary")
    
    args = parser.parse_args()
    
    # Load batch results
    if not os.path.exists(args.batch_results):
        print(f"Error: Batch results file not found: {args.batch_results}")
        exit(1)
    
    with open(args.batch_results, 'r', encoding='utf-8') as f:
        batch_results = json.load(f)
    
    # Initialize validator
    validator = GroundTruthValidator(args.corpus)
    
    # Validate results
    validation_results = validator.validate_batch_results(batch_results)
    
    # Save results
    output_file = validator.save_validation_results(validation_results, args.output)
    
    # Print summary
    if args.summary:
        validator.print_validation_summary(validation_results)

if __name__ == "__main__":
    main()