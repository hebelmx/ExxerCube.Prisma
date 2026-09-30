#!/usr/bin/env python3
"""
CNBV Document Basic Validation for Fixtures

Simple validation without heavy dependencies.
Validates structure and data presence for fixture generation.

Uses only lightweight tools:
- PyPDF2 for basic PDF text extraction
- Regex for pattern matching
- PIL for image checks
"""

import re
import sys
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# Add parent to path
sys.path.insert(0, str(Path(__file__).parent))

from prp1_generator import parse_cnbv_xml

# Optional PDF text extraction
try:
    import PyPDF2
    PYPDF2_AVAILABLE = True
except ImportError:
    PYPDF2_AVAILABLE = False
    print("⚠ PyPDF2 not available. Install: pip install PyPDF2")

# Optional OCR (fallback)
try:
    import pytesseract
    from pdf2image import convert_from_path
    OCR_AVAILABLE = True
except ImportError:
    OCR_AVAILABLE = False


class CNBVFixtureValidator:
    """
    Lightweight validator for CNBV fixtures.

    Validates against technical specification without heavy ML dependencies.
    """

    # Required sections (case-insensitive search)
    REQUIRED_SECTIONS = [
        "VICEPRESIDENCIA",
        "DIRECCIÓN GENERAL",
        "COORDINACIÓN",
    ]

    # Expected patterns
    RFC_PATTERN = r'[A-Z]{3,4}\d{6}[A-Z0-9]{3}'
    DATE_PATTERN = r'\d{1,2}[/-]\d{1,2}[/-]\d{4}'
    LEGAL_CITATION_PATTERN = r'(artículo|art\.|art)\s*\d+'
    OFICIO_PATTERN = r'\d{3}/[A-Z]{3}/-?\d+/\d{4}'
    MONETARY_PATTERN = r'\$?\s*\d{1,3}(?:,\d{3})*(?:\.\d{2})?'

    def __init__(self):
        """Initialize validator."""
        self.extraction_method = None

        if PYPDF2_AVAILABLE:
            self.extraction_method = "pypdf2"
            print("[INFO] Using PyPDF2 for text extraction")
        elif OCR_AVAILABLE:
            self.extraction_method = "ocr"
            print("[INFO] Using OCR for text extraction (slower)")
        else:
            raise RuntimeError(
                "No text extraction method available. Install either:\n"
                "  pip install PyPDF2  (recommended, faster)\n"
                "  pip install pytesseract pdf2image  (slower, requires Tesseract)"
            )

    def extract_text_from_pdf(self, pdf_path: Path) -> str:
        """
        Extract text from PDF using available method.

        Returns:
            Extracted text
        """
        if self.extraction_method == "pypdf2":
            return self._extract_with_pypdf2(pdf_path)
        elif self.extraction_method == "ocr":
            return self._extract_with_ocr(pdf_path)
        else:
            return ""

    def _extract_with_pypdf2(self, pdf_path: Path) -> str:
        """Extract text using PyPDF2 (fast, no OCR)."""
        try:
            with open(pdf_path, 'rb') as f:
                reader = PyPDF2.PdfReader(f)
                text_parts = []

                for page in reader.pages:
                    text_parts.append(page.extract_text())

                return "\n\n".join(text_parts)
        except Exception as e:
            print(f"  ⚠ PyPDF2 extraction error: {e}")
            return ""

    def _extract_with_ocr(self, pdf_path: Path) -> str:
        """Extract text using OCR (slow, for scanned docs)."""
        try:
            images = convert_from_path(str(pdf_path), dpi=300)
            text_parts = []

            for image in images:
                try:
                    page_text = pytesseract.image_to_string(image, lang='spa')
                except:
                    page_text = pytesseract.image_to_string(image)
                text_parts.append(page_text)

            return "\n\n".join(text_parts)
        except Exception as e:
            print(f"  ⚠ OCR extraction error: {e}")
            return ""

    def validate_fixture(
        self,
        pdf_path: Path,
        xml_path: Optional[Path] = None,
    ) -> Dict:
        """
        Validate CNBV fixture against basic requirements.

        Args:
            pdf_path: Path to generated PDF
            xml_path: Optional path to source XML

        Returns:
            Validation report dictionary
        """
        print(f"\nValidating: {pdf_path.name}")
        print("-" * 60)

        # Load ground truth from XML if available
        ground_truth = None
        if xml_path and xml_path.exists():
            ground_truth = parse_cnbv_xml(xml_path)

        # Extract text
        print("1. Extracting text...")
        text = self.extract_text_from_pdf(pdf_path)

        if not text:
            return {
                "pdf": str(pdf_path),
                "error": "Failed to extract text",
                "passed": False,
            }

        # Validate sections
        print("2. Checking required sections...")
        sections_result = self._validate_sections(text)

        # Validate data presence
        print("3. Checking data patterns...")
        data_result = self._validate_data_patterns(text)

        # Validate XML-PDF consistency
        consistency_result = None
        if ground_truth:
            print("4. Checking XML-PDF consistency...")
            consistency_result = self._validate_consistency(text, ground_truth)

        # Check intentional imperfections
        print("5. Checking realistic imperfections...")
        imperfections_result = self._validate_imperfections(text)

        # Calculate overall pass/fail
        passed = (
            sections_result["passed"] and
            data_result["passed"] and
            (consistency_result is None or consistency_result["passed"]) and
            imperfections_result["passed"]
        )

        report = {
            "pdf": str(pdf_path),
            "xml": str(xml_path) if xml_path else None,
            "sections": sections_result,
            "data_patterns": data_result,
            "consistency": consistency_result,
            "imperfections": imperfections_result,
            "passed": passed,
            "extraction_method": self.extraction_method,
        }

        return report

    def _validate_sections(self, text: str) -> Dict:
        """Check that required sections are present."""
        text_upper = text.upper()

        found_sections = {}
        for section in self.REQUIRED_SECTIONS:
            found_sections[section] = section in text_upper

        found_count = sum(found_sections.values())
        required_count = len(self.REQUIRED_SECTIONS)

        return {
            "required": required_count,
            "found": found_count,
            "sections": found_sections,
            "passed": found_count >= required_count,  # All required
        }

    def _validate_data_patterns(self, text: str) -> Dict:
        """Check that required data patterns are present."""
        # Find all matches
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
            "rfcs": rfcs[:3],  # First 3
            "dates": dates[:3],
            "legal_citations": legal_citations[:3],
            "oficios": oficios[:3],
            "passed": sum(checks.values()) >= 3,  # At least 3 of 4
        }

    def _validate_consistency(self, text: str, ground_truth) -> Dict:
        """Check XML-PDF consistency."""
        text_normalized = text.upper().replace(" ", "").replace("\n", "")

        checks = {}

        # Check oficio number
        if ground_truth.Cnbv_NumeroOficio:
            oficio = ground_truth.Cnbv_NumeroOficio.upper().replace(" ", "").replace("/", "")
            # Partial match (OCR may have issues)
            checks["oficio_present"] = oficio[:10] in text_normalized

        # Check expediente number
        if ground_truth.Cnbv_NumeroExpediente:
            exp = ground_truth.Cnbv_NumeroExpediente.strip().upper().replace(" ", "").replace("-", "")
            # Partial match
            checks["expediente_present"] = exp[:10] in text_normalized

        # Check folio
        if ground_truth.Cnbv_Folio:
            folio = ground_truth.Cnbv_Folio
            checks["folio_present"] = folio in text

        # Check authority name (partial)
        if ground_truth.AutoridadNombre:
            autoridad_words = [w for w in ground_truth.AutoridadNombre.upper().split() if len(w) > 3]
            if autoridad_words:
                matches = sum(1 for word in autoridad_words if word in text_normalized)
                checks["autoridad_present"] = matches >= min(2, len(autoridad_words) // 2)

        passed_count = sum(checks.values())
        total_count = len(checks)

        return {
            "checks": checks,
            "passed": passed_count >= max(1, total_count // 2),  # At least half
        }

    def _validate_imperfections(self, text: str) -> Dict:
        """Check for realistic imperfections."""
        checks = {
            "has_spacing_errors": bool(
                re.search(r'párrafo\s+s\b', text, re.IGNORECASE) or
                re.search(r'indi\s+car', text, re.IGNORECASE) or
                re.search(r'o\s+ficio', text, re.IGNORECASE)
            ),
            "has_mixed_case": bool(
                re.search(r'[A-Z]{5,}', text) and re.search(r'[a-z]{5,}', text)
            ),
            "has_legal_variations": len(set(
                m.lower().strip() for m in re.findall(
                    r'(artículo|art\.|art|articulo)',
                    text,
                    re.IGNORECASE
                )
            )) > 1,
        }

        return {
            "checks": checks,
            "passed": sum(checks.values()) >= 1,  # At least 1 imperfection
        }

    def print_report(self, report: Dict):
        """Print formatted validation report."""
        print()
        print("=" * 60)
        print("FIXTURE VALIDATION REPORT")
        print("=" * 60)
        print()

        # Sections
        sections = report["sections"]
        status = "✅" if sections["passed"] else "❌"
        print(f"{status} SECTIONS: {sections['found']}/{sections['required']}")
        for section, found in sections["sections"].items():
            s = "  ✓" if found else "  ✗"
            print(f"{s} {section}")
        print()

        # Data patterns
        data = report["data_patterns"]
        status = "✅" if data["passed"] else "❌"
        checks_passed = sum(data["checks"].values())
        print(f"{status} DATA PATTERNS: {checks_passed}/4")
        for check, passed in data["checks"].items():
            s = "  ✓" if passed else "  ✗"
            print(f"{s} {check}")

        # Show examples
        if data.get("rfcs"):
            print(f"      RFCs: {', '.join(data['rfcs'])}")
        if data.get("dates"):
            print(f"      Dates: {', '.join(data['dates'])}")
        if data.get("oficios"):
            print(f"      Oficios: {', '.join(data['oficios'])}")
        print()

        # Consistency
        if report["consistency"]:
            consistency = report["consistency"]
            status = "✅" if consistency["passed"] else "❌"
            checks_passed = sum(consistency["checks"].values())
            total = len(consistency["checks"])
            print(f"{status} XML-PDF CONSISTENCY: {checks_passed}/{total}")
            for check, passed in consistency["checks"].items():
                s = "  ✓" if passed else "  ✗"
                print(f"{s} {check}")
            print()

        # Imperfections
        imperfections = report["imperfections"]
        status = "✅" if imperfections["passed"] else "⚠"
        checks_passed = sum(imperfections["checks"].values())
        print(f"{status} REALISTIC IMPERFECTIONS: {checks_passed}/3")
        for check, present in imperfections["checks"].items():
            s = "  ✓" if present else "  ○"
            print(f"{s} {check}")
        print()

        # Overall
        if report["passed"]:
            print("✅ FIXTURE VALIDATION PASSED")
        else:
            print("❌ FIXTURE VALIDATION FAILED")

        print(f"\nExtraction method: {report.get('extraction_method', 'unknown')}")
        print("=" * 60)
        print()


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Validate CNBV fixture documents"
    )
    parser.add_argument("pdf", type=Path, help="PDF to validate")
    parser.add_argument("--xml", type=Path, help="Source XML for consistency check")
    parser.add_argument("--batch", action="store_true", help="Batch mode (less verbose)")

    args = parser.parse_args()

    # Validate document
    validator = CNBVFixtureValidator()
    report = validator.validate_fixture(args.pdf, args.xml)

    # Print report
    if not args.batch:
        validator.print_report(report)

    # Return exit code
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
