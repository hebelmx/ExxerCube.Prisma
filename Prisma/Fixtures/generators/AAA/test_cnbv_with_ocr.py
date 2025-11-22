#!/usr/bin/env python3
"""
CNBV Document Validation with Existing OCR Infrastructure

Integrates with existing prisma-ai-extractors:
- GOT-OCR2 (multimodal, state-of-the-art)
- ComprehensiveDocumentValidator (layout/quality)
- GroundTruthValidator (field extraction validation)

Tests generated CNBV documents against technical specification.
"""

import sys
import json
from pathlib import Path
from typing import Dict, List, Optional

# Add paths
sys.path.insert(0, str(Path(__file__).parent))
sys.path.insert(0, str(Path(__file__).parent / "../../Prisma/Code/Src/Python/prisma-ai-extractors/src"))

from prp1_generator import parse_cnbv_xml, xml_to_pdf

# Import existing OCR infrastructure
try:
    from got_ocr2_extractor import GOTOCR2Extractor
    from comprehensive_document_validator import ComprehensiveDocumentValidator
    from ground_truth_validator import GroundTruthValidator
    OCR_AVAILABLE = True
except ImportError as e:
    print(f"⚠ OCR infrastructure not available: {e}")
    print("  Make sure prisma-ai-extractors dependencies are installed")
    OCR_AVAILABLE = False


class CNBVDocumentValidator:
    """
    Validates CNBV documents using existing OCR infrastructure.

    Technical Specification Requirements (from 222AAA-44444444442025.pdf):

    1. REQUIRED SECTIONS (in order):
       - Header with authority
       - No. De Identificación del Requerimiento
       - Addressee block (CNBV official)
       - Datos generales del solicitante
       - Facultades de la Autoridad
       - Fundamento del Requerimiento
       - Motivación del Requerimiento
       - Origen del requerimiento
       - Antecedentes / Sujetos de la auditoría
       - Personas de quien se requiere información
       - Cuentas por conocer
       - Instrucciones sobre cuentas por conocer
       - Cierre y firma

    2. DATA PRESENCE:
       - At least one date (DD/MM/YYYY format)
       - At least one monetary field
       - At least one RFC ([A-Z]{3,4}\\d{6}[A-Z0-9]{3})
       - At least one table with subject rows
       - At least one legal citation (artículo \\d+)

    3. LAYOUT INTEGRITY:
       - Headers appear on every page
       - Section titles match expected labels
       - Proper table structure

    4. INTENTIONAL IMPERFECTIONS (must be present for realism):
       - Typographical inconsistencies
       - Legal reference noise
       - Strange whitespace
       - Broken sentence flow
    """

    # Required sections
    REQUIRED_SECTIONS = [
        "VICEPRESIDENCIA",
        "DIRECCIÓN GENERAL",
        "COORDINACIÓN",
        "DATOS GENERALES",
        "FACULTADES",
        "FUNDAMENTO",
    ]

    # Expected patterns
    RFC_PATTERN = r'[A-Z]{3,4}\d{6}[A-Z0-9]{3}'
    DATE_PATTERN = r'\d{1,2}[/-]\d{1,2}[/-]\d{4}'
    LEGAL_CITATION_PATTERN = r'(artículo|art\.|art)\s*\d+'
    OFICIO_PATTERN = r'\d{3}/[A-Z]{3}/-?\d+/\d{4}'

    def __init__(self):
        """Initialize validator with existing OCR infrastructure."""
        if not OCR_AVAILABLE:
            raise RuntimeError("OCR infrastructure not available")

        print("[INFO] Initializing CNBV Document Validator...")
        print("[INFO] Using existing prisma-ai-extractors infrastructure")

        # Initialize GOT-OCR2 (multimodal, best for Spanish legal documents)
        self.ocr_extractor = GOTOCR2Extractor()

        # Initialize comprehensive validator (layout/quality)
        self.quality_validator = ComprehensiveDocumentValidator()

    def validate_document(
        self,
        pdf_path: Path,
        xml_path: Optional[Path] = None,
    ) -> Dict:
        """
        Validate CNBV document against technical specification.

        Args:
            pdf_path: Path to generated PDF
            xml_path: Optional path to source XML

        Returns:
            Validation report dictionary
        """
        print(f"\n{'='*60}")
        print(f"Validating: {pdf_path.name}")
        print(f"{'='*60}\n")

        # Load ground truth from XML if available
        ground_truth = None
        if xml_path and xml_path.exists():
            ground_truth = parse_cnbv_xml(xml_path)

        # Extract text using GOT-OCR2
        print("1. Extracting text with GOT-OCR2...")
        ocr_result = self._extract_with_gotocr2(pdf_path)

        # Validate layout and quality
        print("2. Validating layout and print quality...")
        quality_result = self._validate_quality(pdf_path)

        # Validate required sections
        print("3. Checking required sections...")
        sections_result = self._validate_sections(ocr_result)

        # Validate data presence
        print("4. Checking data presence...")
        data_result = self._validate_data_presence(ocr_result)

        # Validate XML-PDF consistency
        consistency_result = None
        if ground_truth:
            print("5. Validating XML-PDF consistency...")
            consistency_result = self._validate_consistency(ocr_result, ground_truth)

        # Check intentional imperfections
        print("6. Checking realistic imperfections...")
        imperfections_result = self._validate_imperfections(ocr_result)

        # Compile report
        report = {
            "pdf": str(pdf_path),
            "xml": str(xml_path) if xml_path else None,
            "sections": sections_result,
            "data_presence": data_result,
            "quality": quality_result,
            "consistency": consistency_result,
            "imperfections": imperfections_result,
            "overall_score": self._calculate_overall_score(
                sections_result,
                data_result,
                quality_result,
                consistency_result,
                imperfections_result
            ),
        }

        return report

    def _extract_with_gotocr2(self, pdf_path: Path) -> Dict:
        """Extract text and structure using GOT-OCR2."""
        try:
            # GOT-OCR2 extracts both text and structured data
            result = self.ocr_extractor.extract_from_pdf(str(pdf_path))
            return result
        except Exception as e:
            print(f"  ⚠ GOT-OCR2 extraction error: {e}")
            return {"text": "", "error": str(e)}

    def _validate_quality(self, pdf_path: Path) -> Dict:
        """Validate layout and print quality using comprehensive validator."""
        try:
            result = self.quality_validator.validate_document(str(pdf_path))

            return {
                "overall_quality_score": result.overall_quality_score,
                "layout_score": result.layout_analysis.overall_layout_score,
                "print_quality": result.print_quality.overall_print_quality,
                "passed": result.overall_quality_score >= 0.70,
            }
        except Exception as e:
            print(f"  ⚠ Quality validation error: {e}")
            return {"error": str(e), "passed": False}

    def _validate_sections(self, ocr_result: Dict) -> Dict:
        """Validate that required sections are present."""
        text = ocr_result.get("text", "").upper()

        found_sections = {}
        for section in self.REQUIRED_SECTIONS:
            found_sections[section] = section in text

        required_count = len(self.REQUIRED_SECTIONS)
        found_count = sum(found_sections.values())

        return {
            "required": required_count,
            "found": found_count,
            "sections": found_sections,
            "passed": found_count >= (required_count * 0.75),  # 75% threshold
        }

    def _validate_data_presence(self, ocr_result: Dict) -> Dict:
        """Validate that required data elements are present."""
        import re

        text = ocr_result.get("text", "")

        # Check for required patterns
        rfcs = re.findall(self.RFC_PATTERN, text)
        dates = re.findall(self.DATE_PATTERN, text)
        legal_citations = re.findall(self.LEGAL_CITATION_PATTERN, text, re.IGNORECASE)
        oficios = re.findall(self.OFICIO_PATTERN, text)

        checks = {
            "has_rfc": len(rfcs) > 0,
            "has_date": len(dates) > 0,
            "has_legal_citation": len(legal_citations) > 0,
            "has_oficio": len(oficios) > 0,
        }

        return {
            "checks": checks,
            "rfcs_found": rfcs,
            "dates_found": dates,
            "legal_citations_found": legal_citations[:5],  # First 5
            "oficios_found": oficios,
            "passed": sum(checks.values()) >= 3,  # At least 3 of 4
        }

    def _validate_consistency(self, ocr_result: Dict, ground_truth) -> Dict:
        """Validate XML-PDF consistency."""
        text = ocr_result.get("text", "")
        text_normalized = text.upper().replace(" ", "").replace("\n", "")

        checks = {}

        # Check oficio number
        if ground_truth.Cnbv_NumeroOficio:
            oficio_normalized = ground_truth.Cnbv_NumeroOficio.upper().replace(" ", "")
            checks["oficio_present"] = oficio_normalized in text

        # Check expediente number
        if ground_truth.Cnbv_NumeroExpediente:
            exp = ground_truth.Cnbv_NumeroExpediente.strip().upper().replace(" ", "")
            checks["expediente_present"] = exp in text_normalized

        # Check authority name
        if ground_truth.AutoridadNombre:
            autoridad_words = ground_truth.AutoridadNombre.upper().split()
            matches = sum(1 for word in autoridad_words if len(word) > 3 and word in text_normalized)
            checks["autoridad_present"] = matches >= len(autoridad_words) // 2

        return {
            "checks": checks,
            "passed": sum(checks.values()) >= len(checks) * 0.66,  # 66% threshold
        }

    def _validate_imperfections(self, ocr_result: Dict) -> Dict:
        """Validate that document has realistic imperfections."""
        import re

        text = ocr_result.get("text", "")

        checks = {
            "has_spacing_errors": bool(
                re.search(r'párrafo\s+s\b', text, re.IGNORECASE) or
                re.search(r'indi\s+car', text, re.IGNORECASE)
            ),
            "has_mixed_case": bool(re.search(r'[A-Z]{3,}.*[a-z]{3,}.*[A-Z]{3,}', text)),
            "has_legal_variations": len(set(
                re.findall(r'(artículo|art\.|art|articulo)', text, re.IGNORECASE)
            )) > 1,
        }

        return {
            "checks": checks,
            "passed": sum(checks.values()) >= 1,  # At least 1 imperfection
        }

    def _calculate_overall_score(
        self,
        sections: Dict,
        data: Dict,
        quality: Dict,
        consistency: Optional[Dict],
        imperfections: Dict
    ) -> float:
        """Calculate overall validation score (0-100)."""
        scores = []

        # Sections (30%)
        if sections.get("passed"):
            scores.append(sections["found"] / sections["required"] * 30)

        # Data presence (25%)
        if data.get("passed"):
            checks = data["checks"]
            scores.append(sum(checks.values()) / len(checks) * 25)

        # Quality (25%)
        if quality.get("passed"):
            scores.append(quality.get("overall_quality_score", 0) * 25)

        # Consistency (15%, optional)
        if consistency and consistency.get("passed"):
            checks = consistency["checks"]
            if checks:
                scores.append(sum(checks.values()) / len(checks) * 15)

        # Imperfections (5%)
        if imperfections.get("passed"):
            checks = imperfections["checks"]
            scores.append(sum(checks.values()) / len(checks) * 5)

        return sum(scores)

    def print_report(self, report: Dict):
        """Print formatted validation report."""
        print(f"\n{'='*60}")
        print("VALIDATION REPORT")
        print(f"{'='*60}\n")

        # Sections
        sections = report["sections"]
        print(f"REQUIRED SECTIONS: {sections['found']}/{sections['required']}")
        for section, found in sections["sections"].items():
            status = "✓" if found else "✗"
            print(f"  {status} {section}")
        print()

        # Data presence
        data = report["data_presence"]
        print("DATA PRESENCE:")
        for check, passed in data["checks"].items():
            status = "✓" if passed else "✗"
            print(f"  {status} {check}")
        if data.get("rfcs_found"):
            print(f"    RFCs: {', '.join(data['rfcs_found'])}")
        if data.get("dates_found"):
            print(f"    Dates: {', '.join(data['dates_found'][:3])}")
        print()

        # Quality
        quality = report["quality"]
        if "overall_quality_score" in quality:
            print(f"QUALITY SCORE: {quality['overall_quality_score']:.1%}")
            print(f"  Layout: {quality.get('layout_score', 0):.1%}")
            print(f"  Print: {quality.get('print_quality', 0):.1%}")
        print()

        # Consistency
        if report["consistency"]:
            consistency = report["consistency"]
            print("XML-PDF CONSISTENCY:")
            for check, passed in consistency["checks"].items():
                status = "✓" if passed else "✗"
                print(f"  {status} {check}")
            print()

        # Imperfections
        imperfections = report["imperfections"]
        print("REALISTIC IMPERFECTIONS:")
        for check, present in imperfections["checks"].items():
            status = "✓" if present else "○"
            print(f"  {status} {check}")
        print()

        # Overall
        score = report["overall_score"]
        if score >= 75:
            status = "✅ EXCELLENT"
        elif score >= 60:
            status = "✓ GOOD"
        elif score >= 50:
            status = "⚠ FAIR"
        else:
            status = "✗ POOR"

        print(f"OVERALL SCORE: {score:.1f}/100 {status}")
        print(f"{'='*60}\n")


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Validate CNBV document with existing OCR infrastructure"
    )
    parser.add_argument("pdf", type=Path, help="PDF to validate")
    parser.add_argument("--xml", type=Path, help="Source XML for consistency check")
    parser.add_argument("--output", type=Path, help="Save report to JSON file")

    args = parser.parse_args()

    if not OCR_AVAILABLE:
        print("❌ OCR infrastructure not available")
        print("   Make sure prisma-ai-extractors is set up")
        return 1

    # Validate document
    validator = CNBVDocumentValidator()
    report = validator.validate_document(args.pdf, args.xml)

    # Print report
    validator.print_report(report)

    # Save to file if requested
    if args.output:
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        print(f"Report saved to: {args.output}")

    return 0 if report["overall_score"] >= 60 else 1


if __name__ == "__main__":
    sys.exit(main())
