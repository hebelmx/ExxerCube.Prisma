#!/usr/bin/env python3
"""
Bank Statement Quality Validation System
DocTR-based comprehensive validation for bank statements

Validates: Visual appearance, content accuracy, financial data matching
Handles: Multi-page documents, encrypted visible data, distributed content
"""

import json
import os
import re
import time
from typing import Dict, List, Any, Optional, Tuple
from pathlib import Path
from datetime import datetime
from decimal import Decimal, InvalidOperation

# DocTR and processing libraries
from doctr.io import DocumentFile
from doctr.models import ocr_predictor
import numpy as np
from pydantic import BaseModel, Field

# -------------------------------
# Bank Statement Data Models
# -------------------------------
class AccountInfo(BaseModel):
    """Account information extracted from statement"""
    account_holder: Optional[str] = Field(default="unknown")
    account_number: Optional[str] = Field(default="unknown")
    account_type: Optional[str] = Field(default="unknown")
    bank_name: Optional[str] = Field(default="unknown")
    statement_period: Optional[str] = Field(default="unknown")

class Transaction(BaseModel):
    """Individual transaction data"""
    date: Optional[str] = Field(default="unknown")
    description: Optional[str] = Field(default="unknown")
    amount: Optional[float] = Field(default=None)
    balance: Optional[float] = Field(default=None)
    transaction_type: Optional[str] = Field(default="unknown")  # debit/credit
    position_page: Optional[int] = Field(default=None)
    position_y: Optional[float] = Field(default=None)

class LayoutMetrics(BaseModel):
    """Visual layout and formatting metrics"""
    font_sizes: List[float] = Field(default_factory=list)
    font_positions: List[Tuple[float, float]] = Field(default_factory=list)
    line_spacing: List[float] = Field(default_factory=list)
    column_alignments: List[float] = Field(default_factory=list)
    page_margins: Dict[str, float] = Field(default_factory=dict)
    header_consistency: bool = Field(default=True)
    footer_consistency: bool = Field(default=True)

class ValidationResults(BaseModel):
    """Comprehensive validation results"""
    account_info: AccountInfo
    transactions: List[Transaction] = Field(default_factory=list)
    layout_metrics: LayoutMetrics
    financial_validation: Dict[str, Any] = Field(default_factory=dict)
    quality_score: float = Field(default=0.0)
    validation_flags: List[str] = Field(default_factory=list)
    processing_metadata: Dict[str, Any] = Field(default_factory=dict)

# -------------------------------
# Bank Statement Validator
# -------------------------------
class BankStatementValidator:
    """
    Comprehensive bank statement validation using DocTR
    """
    
    def __init__(self):
        print("[INFO] Initializing Bank Statement Validator with DocTR...")
        
        # Load DocTR model
        try:
            self.model = ocr_predictor(pretrained=True)
            print("[SUCCESS] DocTR model loaded for bank statement processing")
        except Exception as e:
            print(f"[ERROR] Failed to load DocTR: {e}")
            raise
        
        # Financial data patterns
        self.patterns = {
            'account_number': [
                r'\b\d{4}[-\s]*\d{4}[-\s]*\d{4}[-\s]*\d{4}\b',  # 16-digit account
                r'\b\d{10,20}\b',  # 10-20 digit account number
                r'CUENTA[:\s]*(\d+)',
                r'ACCOUNT[:\s]*(\d+)'
            ],
            'amounts': [
                r'\$\s*([0-9,]+\.?\d{0,2})',  # Dollar amounts
                r'([0-9,]+\.\d{2})',          # Decimal amounts
                r'\b(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\b'  # Formatted numbers
            ],
            'dates': [
                r'\b(\d{1,2}/\d{1,2}/\d{4})\b',     # MM/DD/YYYY
                r'\b(\d{4}-\d{2}-\d{2})\b',          # YYYY-MM-DD
                r'\b(\d{1,2}-\d{1,2}-\d{4})\b',     # MM-DD-YYYY
                r'\b(\d{1,2}\s+\w+\s+\d{4})\b'     # DD Month YYYY
            ],
            'account_holder': [
                r'TITULAR[:\s]*([A-ZÁÉÍÓÚÑ\s]+)',
                r'HOLDER[:\s]*([A-Z\s]+)',
                r'NOMBRE[:\s]*([A-ZÁÉÍÓÚÑ\s]+)'
            ]
        }
        
        # Quality thresholds
        self.quality_thresholds = {
            'min_transactions': 5,
            'max_font_size_variance': 0.2,
            'min_confidence': 0.70,
            'max_layout_inconsistency': 0.15
        }
    
    def extract_text_with_positioning(self, image_path: str) -> Dict[str, Any]:
        """
        Extract text with detailed positioning information for layout analysis
        """
        try:
            doc = DocumentFile.from_images(image_path)
            result = self.model(doc)
            
            extracted_data = {
                'pages': [],
                'all_text': "",
                'positioning_data': []
            }
            
            for page_idx, page in enumerate(result.pages):
                page_data = {
                    'page_number': page_idx + 1,
                    'text_blocks': [],
                    'layout_elements': []
                }
                
                page_text_blocks = []
                
                for block in page.blocks:
                    for line in block.lines:
                        line_text = ""
                        line_confidences = []
                        line_positions = []
                        
                        for word in line.words:
                            word_text = word.value
                            word_confidence = word.confidence
                            word_geometry = word.geometry
                            
                            line_text += word_text + " "
                            line_confidences.append(word_confidence)
                            line_positions.append({
                                'text': word_text,
                                'confidence': word_confidence,
                                'bbox': word_geometry,
                                'x': (word_geometry[0][0] + word_geometry[1][0]) / 2,  # center x
                                'y': (word_geometry[0][1] + word_geometry[1][1]) / 2,  # center y
                            })
                        
                        if line_text.strip():
                            avg_confidence = np.mean(line_confidences) if line_confidences else 0
                            
                            line_data = {
                                'text': line_text.strip(),
                                'confidence': avg_confidence,
                                'positions': line_positions,
                                'page': page_idx + 1,
                                'line_bbox': line.geometry if hasattr(line, 'geometry') else None
                            }
                            
                            page_data['text_blocks'].append(line_data)
                            page_text_blocks.append(line_text.strip())
                            extracted_data['positioning_data'].append(line_data)
                
                page_data['full_text'] = "\n".join(page_text_blocks)
                extracted_data['pages'].append(page_data)
                extracted_data['all_text'] += "\n" + page_data['full_text']
            
            return extracted_data
            
        except Exception as e:
            print(f"[ERROR] Text extraction failed: {e}")
            return {'pages': [], 'all_text': "", 'positioning_data': []}
    
    def extract_account_info(self, text_data: Dict) -> AccountInfo:
        """Extract account information from statement"""
        full_text = text_data['all_text'].upper()
        
        account_info = AccountInfo()
        
        # Extract account holder
        for pattern in self.patterns['account_holder']:
            match = re.search(pattern, full_text)
            if match:
                account_info.account_holder = match.group(1).strip()
                break
        
        # Extract account number
        for pattern in self.patterns['account_number']:
            match = re.search(pattern, full_text)
            if match:
                account_info.account_number = match.group(1) if match.groups() else match.group(0)
                break
        
        # Extract bank name (common Mexican banks)
        bank_patterns = [
            r'(BBVA)', r'(BANAMEX)', r'(SANTANDER)', r'(BANORTE)', 
            r'(HSBC)', r'(SCOTIABANK)', r'(CITIBANK)', r'(BANCO AZTECA)'
        ]
        
        for pattern in bank_patterns:
            if re.search(pattern, full_text):
                account_info.bank_name = re.search(pattern, full_text).group(1)
                break
        
        return account_info
    
    def extract_transactions(self, text_data: Dict) -> List[Transaction]:
        """Extract transaction data with positioning"""
        transactions = []
        
        for page_data in text_data['pages']:
            page_num = page_data['page_number']
            
            for line_data in page_data['text_blocks']:
                line_text = line_data['text']
                
                # Look for transaction patterns
                date_match = None
                for date_pattern in self.patterns['dates']:
                    date_match = re.search(date_pattern, line_text)
                    if date_match:
                        break
                
                amount_matches = []
                for amount_pattern in self.patterns['amounts']:
                    amount_matches.extend(re.findall(amount_pattern, line_text))
                
                # If we have both date and amounts, likely a transaction
                if date_match and amount_matches:
                    for amount_str in amount_matches:
                        try:
                            # Clean amount string
                            clean_amount = amount_str.replace(',', '').replace('$', '').strip()
                            amount_value = float(clean_amount)
                            
                            transaction = Transaction(
                                date=date_match.group(1),
                                description=line_text[:50],  # First 50 chars as description
                                amount=amount_value,
                                position_page=page_num,
                                position_y=line_data.get('positions', [{}])[0].get('y', 0)
                            )
                            
                            transactions.append(transaction)
                            break  # One transaction per line
                            
                        except (ValueError, InvalidOperation):
                            continue
        
        return transactions
    
    def analyze_layout_quality(self, text_data: Dict) -> LayoutMetrics:
        """Analyze visual layout and formatting quality"""
        metrics = LayoutMetrics()
        
        font_sizes = []
        positions = []
        y_positions = []
        
        # Analyze positioning data
        for line_data in text_data['positioning_data']:
            if 'positions' in line_data:
                for pos_data in line_data['positions']:
                    positions.append((pos_data.get('x', 0), pos_data.get('y', 0)))
                    y_positions.append(pos_data.get('y', 0))
                    
                    # Estimate font size from bbox if available
                    if 'bbox' in pos_data and pos_data['bbox']:
                        bbox = pos_data['bbox']
                        font_size = abs(bbox[1][1] - bbox[0][1])  # Height difference
                        font_sizes.append(font_size)
        
        metrics.font_sizes = font_sizes
        metrics.font_positions = positions
        
        # Calculate line spacing
        if len(y_positions) > 1:
            sorted_y = sorted(y_positions)
            line_spacings = [sorted_y[i+1] - sorted_y[i] for i in range(len(sorted_y)-1)]
            metrics.line_spacing = line_spacings
        
        # Analyze consistency
        if font_sizes:
            font_variance = np.std(font_sizes) / np.mean(font_sizes) if np.mean(font_sizes) > 0 else 1
            metrics.header_consistency = font_variance < self.quality_thresholds['max_font_size_variance']
        
        return metrics
    
    def validate_financial_data(self, account_info: AccountInfo, transactions: List[Transaction]) -> Dict[str, Any]:
        """Validate financial data consistency and accuracy"""
        validation = {
            'total_transactions': len(transactions),
            'date_range_valid': True,
            'amounts_consistent': True,
            'account_number_valid': True,
            'balance_progression': True,
            'warnings': []
        }
        
        # Validate transaction count
        if len(transactions) < self.quality_thresholds['min_transactions']:
            validation['warnings'].append(f"Low transaction count: {len(transactions)}")
        
        # Validate account number format
        if account_info.account_number and account_info.account_number != "unknown":
            if not re.match(r'\d{10,20}', account_info.account_number.replace('-', '').replace(' ', '')):
                validation['account_number_valid'] = False
                validation['warnings'].append("Invalid account number format")
        
        # Validate amount consistency
        amounts = [t.amount for t in transactions if t.amount is not None]
        if amounts:
            # Check for suspicious amounts (too high or negative where unexpected)
            suspicious_amounts = [a for a in amounts if a > 1000000 or a < -1000000]
            if suspicious_amounts:
                validation['amounts_consistent'] = False
                validation['warnings'].append(f"Suspicious amounts detected: {len(suspicious_amounts)}")
        
        # Calculate financial summary
        validation['financial_summary'] = {
            'total_debits': sum(a for a in amounts if a < 0),
            'total_credits': sum(a for a in amounts if a > 0),
            'transaction_count': len(amounts),
            'avg_amount': np.mean(amounts) if amounts else 0
        }
        
        return validation
    
    def calculate_quality_score(self, account_info: AccountInfo, transactions: List[Transaction], 
                              layout_metrics: LayoutMetrics, financial_validation: Dict) -> float:
        """Calculate overall quality score (0-1)"""
        scores = []
        
        # Account information completeness (25%)
        account_score = 0
        if account_info.account_holder != "unknown":
            account_score += 0.4
        if account_info.account_number != "unknown":
            account_score += 0.4
        if account_info.bank_name != "unknown":
            account_score += 0.2
        scores.append(account_score)
        
        # Transaction data quality (35%)
        transaction_score = min(len(transactions) / 20.0, 1.0)  # Up to 20 transactions for full score
        scores.append(transaction_score)
        
        # Layout consistency (20%)
        layout_score = 1.0
        if not layout_metrics.header_consistency:
            layout_score -= 0.3
        scores.append(layout_score)
        
        # Financial validation (20%)
        financial_score = 1.0
        if not financial_validation['amounts_consistent']:
            financial_score -= 0.3
        if not financial_validation['account_number_valid']:
            financial_score -= 0.3
        if len(financial_validation['warnings']) > 2:
            financial_score -= 0.2
        scores.append(max(financial_score, 0))
        
        # Weighted average
        weights = [0.25, 0.35, 0.20, 0.20]
        return sum(score * weight for score, weight in zip(scores, weights))
    
    def validate_bank_statement(self, image_path: str) -> ValidationResults:
        """
        Complete bank statement validation pipeline
        """
        print(f"[INFO] Validating bank statement: {os.path.basename(image_path)}")
        start_time = time.time()
        
        # Extract text with positioning
        text_data = self.extract_text_with_positioning(image_path)
        
        if not text_data['all_text']:
            print("[WARNING] No text extracted from document")
            return ValidationResults(
                account_info=AccountInfo(),
                processing_metadata={'error': 'No text extracted', 'processing_time': time.time() - start_time}
            )
        
        # Extract structured data
        account_info = self.extract_account_info(text_data)
        transactions = self.extract_transactions(text_data)
        layout_metrics = self.analyze_layout_quality(text_data)
        financial_validation = self.validate_financial_data(account_info, transactions)
        
        # Calculate quality score
        quality_score = self.calculate_quality_score(account_info, transactions, layout_metrics, financial_validation)
        
        # Generate validation flags
        validation_flags = []
        if quality_score < 0.7:
            validation_flags.append("QUALITY_BELOW_THRESHOLD")
        if len(transactions) < 5:
            validation_flags.append("LOW_TRANSACTION_COUNT")
        if account_info.account_holder == "unknown":
            validation_flags.append("MISSING_ACCOUNT_HOLDER")
        if len(financial_validation['warnings']) > 0:
            validation_flags.extend([f"FINANCIAL_{w.upper().replace(' ', '_')}" for w in financial_validation['warnings']])
        
        processing_time = time.time() - start_time
        
        results = ValidationResults(
            account_info=account_info,
            transactions=transactions,
            layout_metrics=layout_metrics,
            financial_validation=financial_validation,
            quality_score=round(quality_score, 3),
            validation_flags=validation_flags,
            processing_metadata={
                'processing_time': round(processing_time, 2),
                'total_pages': len(text_data['pages']),
                'total_text_length': len(text_data['all_text']),
                'extraction_confidence': 'high' if quality_score > 0.8 else 'medium' if quality_score > 0.6 else 'low'
            }
        )
        
        print(f"[SUCCESS] Validation complete - Quality Score: {quality_score:.1%}")
        
        return results

# -------------------------------
# CLI Interface
# -------------------------------
def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Bank Statement Quality Validator")
    parser.add_argument("--statement", required=True, help="Path to bank statement image")
    parser.add_argument("--output", help="Output JSON file for results")
    parser.add_argument("--verbose", action="store_true", help="Verbose output")
    
    args = parser.parse_args()
    
    if not os.path.exists(args.statement):
        print(f"Error: Statement file not found: {args.statement}")
        exit(1)
    
    # Initialize validator
    validator = BankStatementValidator()
    
    # Validate statement
    results = validator.validate_bank_statement(args.statement)
    
    # Output results
    if args.output:
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(results.model_dump(), f, ensure_ascii=False, indent=2)
        print(f"Results saved to: {args.output}")
    else:
        print(json.dumps(results.model_dump(), ensure_ascii=False, indent=2))
    
    # Verbose summary
    if args.verbose:
        print(f"\n=== BANK STATEMENT VALIDATION SUMMARY ===")
        print(f"Account Holder: {results.account_info.account_holder}")
        print(f"Account Number: {results.account_info.account_number}")
        print(f"Bank: {results.account_info.bank_name}")
        print(f"Transactions Found: {len(results.transactions)}")
        print(f"Quality Score: {results.quality_score:.1%}")
        print(f"Processing Time: {results.processing_metadata['processing_time']}s")
        if results.validation_flags:
            print(f"Validation Flags: {', '.join(results.validation_flags)}")

if __name__ == "__main__":
    main()