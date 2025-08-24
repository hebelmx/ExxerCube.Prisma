#!/usr/bin/env python3
"""
Comprehensive Document Quality Validator
DocTR OCR + Vision Models for complete bank statement validation

Handles: Logo detection, font analysis, layout quality, content validation, print quality
Replaces: CLIP-based approaches with specialized document analysis
"""

import json
import os
import time
import cv2
import numpy as np
from typing import Dict, List, Any, Optional, Tuple
from pathlib import Path
from datetime import datetime

# Core libraries
from PIL import Image, ImageFont, ImageDraw, ImageFilter, ImageStat
import matplotlib.pyplot as plt
from sklearn.cluster import KMeans

# DocTR for text extraction
from doctr.io import DocumentFile
from doctr.models import ocr_predictor

# Pydantic models
from pydantic import BaseModel, Field

# -------------------------------
# Comprehensive Validation Models
# -------------------------------
class LogoAnalysis(BaseModel):
    """Logo detection and quality analysis"""
    logo_detected: bool = Field(default=False)
    logo_position: Optional[Tuple[float, float]] = Field(default=None)  # (x, y)
    logo_size: Optional[Tuple[float, float]] = Field(default=None)      # (width, height)
    logo_quality_score: float = Field(default=0.0)
    logo_alignment: str = Field(default="unknown")  # left, center, right
    logo_consistency: bool = Field(default=True)

class FontAnalysis(BaseModel):
    """Detailed font analysis"""
    detected_font_sizes: List[float] = Field(default_factory=list)
    primary_font_size: float = Field(default=0.0)
    font_size_variance: float = Field(default=0.0)
    font_consistency_score: float = Field(default=0.0)
    header_font_size: float = Field(default=0.0)
    body_font_size: float = Field(default=0.0)
    font_spacing_analysis: Dict[str, float] = Field(default_factory=dict)

class LayoutAnalysis(BaseModel):
    """Layout and positioning analysis"""
    margin_consistency: Dict[str, float] = Field(default_factory=dict)  # top, bottom, left, right
    column_alignment: List[float] = Field(default_factory=list)
    line_spacing_consistency: float = Field(default=0.0)
    table_alignment_score: float = Field(default=0.0)
    overall_layout_score: float = Field(default=0.0)

class PrintQuality(BaseModel):
    """Print and image quality metrics"""
    resolution_dpi: float = Field(default=0.0)
    sharpness_score: float = Field(default=0.0)
    contrast_score: float = Field(default=0.0)
    brightness_consistency: float = Field(default=0.0)
    noise_level: float = Field(default=0.0)
    blur_detection: float = Field(default=0.0)
    overall_print_quality: float = Field(default=0.0)

class ContentValidation(BaseModel):
    """Content accuracy and completeness"""
    required_sections_present: Dict[str, bool] = Field(default_factory=dict)
    data_completeness_score: float = Field(default=0.0)
    content_accuracy_flags: List[str] = Field(default_factory=list)
    missing_elements: List[str] = Field(default_factory=list)
    extra_elements: List[str] = Field(default_factory=list)

class ComprehensiveValidation(BaseModel):
    """Complete validation results"""
    logo_analysis: LogoAnalysis
    font_analysis: FontAnalysis
    layout_analysis: LayoutAnalysis
    print_quality: PrintQuality
    content_validation: ContentValidation
    overall_quality_score: float = Field(default=0.0)
    validation_summary: Dict[str, Any] = Field(default_factory=dict)
    processing_metadata: Dict[str, Any] = Field(default_factory=dict)

# -------------------------------
# Comprehensive Document Validator
# -------------------------------
class ComprehensiveDocumentValidator:
    """
    Multi-modal document validator combining OCR + Computer Vision
    Specialized for bank statements and financial documents
    """
    
    def __init__(self):
        print("[INFO] Initializing Comprehensive Document Validator...")
        
        # Load DocTR for text analysis
        try:
            self.ocr_model = ocr_predictor(pretrained=True)
            print("[SUCCESS] DocTR OCR model loaded")
        except Exception as e:
            print(f"[ERROR] Failed to load DocTR: {e}")
            raise
        
        # Define bank logo templates (simplified - would use actual logo templates)
        self.logo_templates = {
            'BBVA': {'colors': [(0, 72, 153), (255, 255, 255)], 'aspect_ratio': 2.5},
            'BANAMEX': {'colors': [(200, 16, 46), (255, 255, 255)], 'aspect_ratio': 3.0},
            'SANTANDER': {'colors': [(236, 28, 36), (255, 255, 255)], 'aspect_ratio': 2.8},
            'BANORTE': {'colors': [(230, 126, 34), (255, 255, 255)], 'aspect_ratio': 2.2}
        }
        
        # Quality thresholds
        self.quality_standards = {
            'min_logo_quality': 0.75,
            'font_size_variance_max': 0.15,
            'min_resolution_dpi': 150,
            'min_sharpness': 0.70,
            'min_contrast': 0.60,
            'max_noise_level': 0.20
        }
    
    def analyze_logo_detection(self, image: np.ndarray) -> LogoAnalysis:
        """
        Advanced logo detection and analysis
        Much more precise than CLIP for specific logos
        """
        analysis = LogoAnalysis()
        
        try:
            # Convert to different color spaces for analysis
            hsv = cv2.cvtColor(image, cv2.COLOR_RGB2HSV)
            gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
            
            # Logo detection using template matching and color analysis
            height, width = image.shape[:2]
            
            # Focus on header region (top 20% of document)
            header_region = image[:int(height * 0.2), :]
            header_gray = gray[:int(height * 0.2), :]
            
            # Detect potential logo regions using contour analysis
            edges = cv2.Canny(header_gray, 50, 150)
            contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            
            logo_candidates = []
            for contour in contours:
                x, y, w, h = cv2.boundingRect(contour)
                area = cv2.contourArea(contour)
                aspect_ratio = w / h if h > 0 else 0
                
                # Filter for logo-like regions
                if (area > 1000 and area < width * height * 0.1 and 
                    1.5 < aspect_ratio < 4.0 and y < height * 0.15):
                    
                    logo_candidates.append({
                        'bbox': (x, y, w, h),
                        'area': area,
                        'aspect_ratio': aspect_ratio,
                        'region': header_region[y:y+h, x:x+w]
                    })
            
            # Analyze best candidate
            if logo_candidates:
                # Sort by area (largest likely to be main logo)
                best_logo = max(logo_candidates, key=lambda x: x['area'])
                
                analysis.logo_detected = True
                analysis.logo_position = (
                    best_logo['bbox'][0] / width,
                    best_logo['bbox'][1] / height
                )
                analysis.logo_size = (
                    best_logo['bbox'][2] / width,
                    best_logo['bbox'][3] / height
                )
                
                # Determine alignment
                center_x = analysis.logo_position[0] + analysis.logo_size[0] / 2
                if center_x < 0.2:
                    analysis.logo_alignment = "left"
                elif center_x > 0.8:
                    analysis.logo_alignment = "right"
                else:
                    analysis.logo_alignment = "center"
                
                # Quality analysis of logo region
                logo_region = best_logo['region']
                if logo_region.size > 0:
                    # Sharpness analysis
                    laplacian = cv2.Laplacian(cv2.cvtColor(logo_region, cv2.COLOR_RGB2GRAY), cv2.CV_64F)
                    sharpness = laplacian.var()
                    
                    # Color consistency
                    std_dev = np.std(logo_region)
                    
                    analysis.logo_quality_score = min(1.0, (sharpness / 1000.0 + (255 - std_dev) / 255.0) / 2)
            
        except Exception as e:
            print(f"[WARNING] Logo analysis failed: {e}")
        
        return analysis
    
    def analyze_font_details(self, ocr_data: Dict, image: np.ndarray) -> FontAnalysis:
        """
        Detailed font size and spacing analysis using DocTR positioning data
        """
        analysis = FontAnalysis()
        
        try:
            font_sizes = []
            line_spacings = []
            y_positions = []
            
            # Extract font size information from bounding boxes
            for page_data in ocr_data.get('pages', []):
                for line_data in page_data.get('text_blocks', []):
                    if 'positions' in line_data:
                        for pos_data in line_data['positions']:
                            if 'bbox' in pos_data and pos_data['bbox']:
                                bbox = pos_data['bbox']
                                # Calculate font size from bounding box height
                                font_height = abs(bbox[1][1] - bbox[0][1]) * image.shape[0]  # Convert to pixels
                                font_sizes.append(font_height)
                                
                                # Collect y positions for spacing analysis
                                y_pos = (bbox[0][1] + bbox[1][1]) / 2 * image.shape[0]
                                y_positions.append(y_pos)
            
            # Calculate line spacing
            if len(y_positions) > 1:
                sorted_y = sorted(y_positions)
                line_spacings = [sorted_y[i+1] - sorted_y[i] for i in range(len(sorted_y)-1)]
            
            # Analyze font sizes
            if font_sizes:
                analysis.detected_font_sizes = font_sizes
                analysis.primary_font_size = float(np.median(font_sizes))
                analysis.font_size_variance = float(np.std(font_sizes) / np.mean(font_sizes))
                
                # Determine header vs body font sizes
                font_clusters = KMeans(n_clusters=min(3, len(set(font_sizes))), random_state=42)
                clusters = font_clusters.fit_predict(np.array(font_sizes).reshape(-1, 1))
                
                cluster_sizes = []
                for i in range(font_clusters.n_clusters):
                    cluster_fonts = [font_sizes[j] for j in range(len(font_sizes)) if clusters[j] == i]
                    cluster_sizes.append(np.mean(cluster_fonts))
                
                analysis.header_font_size = float(max(cluster_sizes))
                analysis.body_font_size = float(min(cluster_sizes))
                
                # Font consistency score
                analysis.font_consistency_score = max(0.0, 1.0 - analysis.font_size_variance)
            
            # Line spacing analysis
            if line_spacings:
                analysis.font_spacing_analysis = {
                    'average_spacing': float(np.mean(line_spacings)),
                    'spacing_variance': float(np.std(line_spacings)),
                    'consistent_spacing': float(np.std(line_spacings) / np.mean(line_spacings)) < 0.2
                }
            
        except Exception as e:
            print(f"[WARNING] Font analysis failed: {e}")
        
        return analysis
    
    def analyze_layout_quality(self, ocr_data: Dict, image: np.ndarray) -> LayoutAnalysis:
        """
        Advanced layout and positioning analysis
        """
        analysis = LayoutAnalysis()
        
        try:
            height, width = image.shape[:2]
            
            # Collect all text positions
            text_elements = []
            for page_data in ocr_data.get('pages', []):
                for line_data in page_data.get('text_blocks', []):
                    if 'positions' in line_data:
                        for pos_data in line_data['positions']:
                            if 'bbox' in pos_data and pos_data['bbox']:
                                bbox = pos_data['bbox']
                                text_elements.append({
                                    'x': bbox[0][0] * width,
                                    'y': bbox[0][1] * height,
                                    'width': (bbox[1][0] - bbox[0][0]) * width,
                                    'height': (bbox[1][1] - bbox[0][1]) * height,
                                    'text': pos_data.get('text', '')
                                })
            
            if text_elements:
                # Margin analysis
                left_margins = [elem['x'] for elem in text_elements]
                right_margins = [width - (elem['x'] + elem['width']) for elem in text_elements]
                top_positions = [elem['y'] for elem in text_elements]
                
                analysis.margin_consistency = {
                    'left_margin_std': float(np.std(left_margins)),
                    'right_margin_std': float(np.std(right_margins)),
                    'top_margin_consistency': float(1.0 - (np.std(top_positions) / height))
                }
                
                # Column alignment analysis
                x_positions = [elem['x'] for elem in text_elements]
                if len(x_positions) > 5:
                    # Use clustering to find column positions
                    kmeans = KMeans(n_clusters=min(5, len(set(x_positions))), random_state=42)
                    clusters = kmeans.fit_predict(np.array(x_positions).reshape(-1, 1))
                    analysis.column_alignment = [float(center[0]) for center in kmeans.cluster_centers_]
                
                # Line spacing consistency
                y_positions = sorted([elem['y'] for elem in text_elements])
                if len(y_positions) > 1:
                    spacings = [y_positions[i+1] - y_positions[i] for i in range(len(y_positions)-1)]
                    spacing_variance = np.std(spacings) / np.mean(spacings) if np.mean(spacings) > 0 else 1
                    analysis.line_spacing_consistency = max(0.0, 1.0 - spacing_variance)
                
                # Overall layout score
                margin_score = 1.0 - min(1.0, analysis.margin_consistency['left_margin_std'] / width)
                spacing_score = analysis.line_spacing_consistency
                analysis.overall_layout_score = (margin_score + spacing_score) / 2
            
        except Exception as e:
            print(f"[WARNING] Layout analysis failed: {e}")
        
        return analysis
    
    def analyze_print_quality(self, image: np.ndarray) -> PrintQuality:
        """
        Comprehensive print quality analysis
        Much more detailed than CLIP for quality assessment
        """
        analysis = PrintQuality()
        
        try:
            height, width = image.shape[:2]
            
            # Convert to different formats for analysis
            gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
            
            # Resolution estimation (basic)
            analysis.resolution_dpi = min(width, height) / 8.5 * 72  # Approximate for letter size
            
            # Sharpness analysis using Laplacian
            laplacian = cv2.Laplacian(gray, cv2.CV_64F)
            analysis.sharpness_score = min(1.0, laplacian.var() / 1000.0)
            
            # Contrast analysis
            contrast = gray.std()
            analysis.contrast_score = min(1.0, contrast / 128.0)
            
            # Brightness consistency
            brightness_mean = gray.mean()
            brightness_std = gray.std()
            analysis.brightness_consistency = max(0.0, 1.0 - brightness_std / 128.0)
            
            # Noise level estimation
            noise = cv2.fastNlMeansDenoising(gray) - gray
            analysis.noise_level = min(1.0, np.abs(noise).mean() / 255.0)
            
            # Blur detection using edge analysis
            edges = cv2.Canny(gray, 50, 150)
            edge_density = np.count_nonzero(edges) / (width * height)
            analysis.blur_detection = min(1.0, edge_density * 10)  # More edges = less blur
            
            # Overall print quality score
            quality_components = [
                analysis.sharpness_score * 0.3,
                analysis.contrast_score * 0.25,
                analysis.brightness_consistency * 0.2,
                (1.0 - analysis.noise_level) * 0.15,
                analysis.blur_detection * 0.1
            ]
            analysis.overall_print_quality = sum(quality_components)
            
        except Exception as e:
            print(f"[WARNING] Print quality analysis failed: {e}")
        
        return analysis
    
    def validate_content_completeness(self, ocr_data: Dict) -> ContentValidation:
        """
        Content validation for required sections and elements
        """
        validation = ContentValidation()
        
        try:
            full_text = ocr_data.get('all_text', '').upper()
            
            # Required sections for bank statements
            required_sections = {
                'account_info': ['CUENTA', 'ACCOUNT', 'TITULAR', 'HOLDER'],
                'statement_period': ['PERIODO', 'PERIOD', 'FECHA', 'DATE'],
                'transaction_table': ['FECHA', 'DESCRIPCION', 'CARGO', 'ABONO', 'SALDO'],
                'bank_info': ['BANCO', 'BANK'],
                'balance_info': ['SALDO', 'BALANCE']
            }
            
            # Check for required sections
            sections_found = {}
            for section, keywords in required_sections.items():
                found = any(keyword in full_text for keyword in keywords)
                sections_found[section] = found
                if not found:
                    validation.missing_elements.append(section)
            
            validation.required_sections_present = sections_found
            
            # Calculate completeness score
            found_sections = sum(sections_found.values())
            total_sections = len(required_sections)
            validation.data_completeness_score = found_sections / total_sections
            
            # Additional content validation
            if 'DRAFT' in full_text or 'BORRADOR' in full_text:
                validation.content_accuracy_flags.append('DRAFT_WATERMARK')
            
            if len(full_text) < 500:  # Too little text for a bank statement
                validation.content_accuracy_flags.append('INSUFFICIENT_CONTENT')
            
        except Exception as e:
            print(f"[WARNING] Content validation failed: {e}")
        
        return validation
    
    def validate_document_comprehensive(self, image_path: str) -> ComprehensiveValidation:
        """
        Complete comprehensive validation pipeline
        """
        print(f"[INFO] Starting comprehensive validation: {os.path.basename(image_path)}")
        start_time = time.time()
        
        try:
            # Load image
            image = np.array(Image.open(image_path))
            
            # Extract text with positioning using DocTR
            doc = DocumentFile.from_images(image_path)
            ocr_result = self.ocr_model(doc)
            
            # Convert OCR result to our format
            ocr_data = self._convert_doctr_result(ocr_result)
            
            # Run all analyses
            logo_analysis = self.analyze_logo_detection(image)
            font_analysis = self.analyze_font_details(ocr_data, image)
            layout_analysis = self.analyze_layout_quality(ocr_data, image)
            print_quality = self.analyze_print_quality(image)
            content_validation = self.validate_content_completeness(ocr_data)
            
            # Calculate overall quality score
            quality_components = {
                'logo_quality': logo_analysis.logo_quality_score * 0.15,
                'font_consistency': font_analysis.font_consistency_score * 0.20,
                'layout_quality': layout_analysis.overall_layout_score * 0.25,
                'print_quality': print_quality.overall_print_quality * 0.25,
                'content_completeness': content_validation.data_completeness_score * 0.15
            }
            
            overall_score = sum(quality_components.values())
            
            processing_time = time.time() - start_time
            
            # Create validation summary
            validation_summary = {
                'quality_grade': 'EXCELLENT' if overall_score > 0.9 else 'GOOD' if overall_score > 0.75 else 'ACCEPTABLE' if overall_score > 0.6 else 'POOR',
                'primary_issues': [],
                'recommendations': []
            }
            
            # Add specific issues and recommendations
            if logo_analysis.logo_quality_score < self.quality_standards['min_logo_quality']:
                validation_summary['primary_issues'].append('Logo quality below standards')
                validation_summary['recommendations'].append('Improve logo resolution and placement')
            
            if font_analysis.font_size_variance > self.quality_standards['font_size_variance_max']:
                validation_summary['primary_issues'].append('Inconsistent font sizing')
                validation_summary['recommendations'].append('Standardize font sizes across document')
            
            if print_quality.overall_print_quality < 0.7:
                validation_summary['primary_issues'].append('Print quality issues detected')
                validation_summary['recommendations'].append('Improve image resolution and clarity')
            
            results = ComprehensiveValidation(
                logo_analysis=logo_analysis,
                font_analysis=font_analysis,
                layout_analysis=layout_analysis,
                print_quality=print_quality,
                content_validation=content_validation,
                overall_quality_score=round(overall_score, 3),
                validation_summary=validation_summary,
                processing_metadata={
                    'processing_time': round(processing_time, 2),
                    'image_dimensions': image.shape[:2],
                    'validator_version': '1.0.0',
                    'analysis_components': list(quality_components.keys())
                }
            )
            
            print(f"[SUCCESS] Comprehensive validation complete - Overall Score: {overall_score:.1%}")
            return results
            
        except Exception as e:
            print(f"[ERROR] Comprehensive validation failed: {e}")
            # Return minimal results with error
            return ComprehensiveValidation(
                logo_analysis=LogoAnalysis(),
                font_analysis=FontAnalysis(),
                layout_analysis=LayoutAnalysis(),
                print_quality=PrintQuality(),
                content_validation=ContentValidation(),
                processing_metadata={'error': str(e), 'processing_time': time.time() - start_time}
            )
    
    def _convert_doctr_result(self, doctr_result) -> Dict:
        """Convert DocTR result to our internal format with positioning data"""
        try:
            pages_data = []
            all_text_parts = []
            positioning_data = []
            
            for page_idx, page in enumerate(doctr_result.pages):
                page_height = page.dimensions[1] if hasattr(page, 'dimensions') else 1.0
                page_width = page.dimensions[0] if hasattr(page, 'dimensions') else 1.0
                
                text_blocks = []
                
                for block_idx, block in enumerate(page.blocks):
                    for line_idx, line in enumerate(block.lines):
                        line_text = ""
                        line_confidences = []
                        word_positions = []
                        
                        # Get line geometry (normalized coordinates)
                        line_geometry = line.geometry if hasattr(line, 'geometry') else ((0, 0), (1, 1))
                        
                        for word in line.words:
                            line_text += word.value + " "
                            line_confidences.append(word.confidence)
                            
                            # Word positioning data
                            word_geometry = word.geometry if hasattr(word, 'geometry') else line_geometry
                            word_positions.append({
                                'text': word.value,
                                'confidence': word.confidence,
                                'bbox': {
                                    'normalized': word_geometry,
                                    'absolute': {
                                        'x1': int(word_geometry[0][0] * page_width),
                                        'y1': int(word_geometry[0][1] * page_height),
                                        'x2': int(word_geometry[1][0] * page_width),
                                        'y2': int(word_geometry[1][1] * page_height)
                                    }
                                }
                            })
                        
                        if line_text.strip():
                            line_data = {
                                'text': line_text.strip(),
                                'confidence': float(np.mean(line_confidences)) if line_confidences else 0.0,
                                'positions': [{
                                    'text': line_text.strip(),
                                    'bbox': line_geometry,
                                    'words': word_positions
                                }],
                                'line_geometry': line_geometry,
                                'page_index': page_idx,
                                'block_index': block_idx,
                                'line_index': line_idx
                            }
                            
                            text_blocks.append(line_data)
                            all_text_parts.append(line_text.strip())
                            
                            # Add to positioning data for layout analysis
                            positioning_data.append({
                                'text': line_text.strip(),
                                'bbox': line_geometry,
                                'page': page_idx,
                                'font_size_estimate': int((line_geometry[1][1] - line_geometry[0][1]) * page_height),
                                'x_position': line_geometry[0][0] * page_width,
                                'y_position': line_geometry[0][1] * page_height
                            })
                
                pages_data.append({
                    'page_index': page_idx,
                    'text_blocks': text_blocks,
                    'dimensions': (page_width, page_height)
                })
            
            return {
                'pages': pages_data,
                'all_text': '\n'.join(all_text_parts),
                'positioning_data': positioning_data,
                'total_pages': len(pages_data),
                'total_text_blocks': sum(len(page['text_blocks']) for page in pages_data)
            }
            
        except Exception as e:
            print(f"[WARNING] DocTR result conversion failed: {e}")
            return {
                'pages': [],
                'all_text': "",
                'positioning_data': [],
                'error': str(e)
            }

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Comprehensive Document Quality Validator")
    parser.add_argument("--document", required=True, help="Path to document image")
    parser.add_argument("--output", help="Output JSON file for results")
    parser.add_argument("--verbose", action="store_true", help="Verbose output")
    parser.add_argument("--report", action="store_true", help="Generate detailed report")
    
    args = parser.parse_args()
    
    if not os.path.exists(args.document):
        print(f"Error: Document file not found: {args.document}")
        exit(1)
    
    # Initialize validator
    validator = ComprehensiveDocumentValidator()
    
    # Run comprehensive validation
    results = validator.validate_document_comprehensive(args.document)
    
    # Output results
    if args.output:
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(results.model_dump(), f, ensure_ascii=False, indent=2)
        print(f"Results saved to: {args.output}")
    else:
        print(json.dumps(results.model_dump(), ensure_ascii=False, indent=2))
    
    # Detailed report
    if args.report:
        print(f"\n=== COMPREHENSIVE DOCUMENT VALIDATION REPORT ===")
        print(f"Overall Quality Score: {results.overall_quality_score:.1%}")
        print(f"Quality Grade: {results.validation_summary['quality_grade']}")
        
        print(f"\n--- Logo Analysis ---")
        print(f"Logo Detected: {results.logo_analysis.logo_detected}")
        print(f"Logo Quality: {results.logo_analysis.logo_quality_score:.1%}")
        print(f"Logo Alignment: {results.logo_analysis.logo_alignment}")
        
        print(f"\n--- Font Analysis ---")
        print(f"Font Consistency: {results.font_analysis.font_consistency_score:.1%}")
        print(f"Primary Font Size: {results.font_analysis.primary_font_size:.1f}px")
        print(f"Font Size Variance: {results.font_analysis.font_size_variance:.3f}")
        
        print(f"\n--- Print Quality ---")
        print(f"Overall Print Quality: {results.print_quality.overall_print_quality:.1%}")
        print(f"Sharpness: {results.print_quality.sharpness_score:.1%}")
        print(f"Contrast: {results.print_quality.contrast_score:.1%}")
        
        if results.validation_summary['primary_issues']:
            print(f"\n--- Issues Detected ---")
            for issue in results.validation_summary['primary_issues']:
                print(f"• {issue}")
        
        if results.validation_summary['recommendations']:
            print(f"\n--- Recommendations ---")
            for rec in results.validation_summary['recommendations']:
                print(f"• {rec}")

if __name__ == "__main__":
    main()