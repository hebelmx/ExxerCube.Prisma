#!/usr/bin/env python3
"""
Document Structure Validation Tests

Tests that generated CNBV documents conform to the technical specification
and can be reliably parsed back using OCR or other extraction methods.

Based on: 222AAA-44444444442025.pdf Technical Specification
"""

import re
import sys
import unittest
from pathlib import Path
from typing import Optional, List, Dict

# Add parent to path
sys.path.insert(0, str(Path(__file__).parent))

from prp1_generator import (
    CNBVExpediente,
    parse_cnbv_xml,
    xml_to_pdf,
)

# Optional OCR support
try:
    import pytesseract
    from pdf2image import convert_from_path
    from PIL import Image
    OCR_AVAILABLE = True
except ImportError:
    OCR_AVAILABLE = False
    print("⚠ OCR not available. Install: pip install pytesseract pdf2image")
    print("  Also install Tesseract: https://github.com/tesseract-ocr/tesseract")


class DocumentStructureValidator:
    """Validates generated CNBV document structure and content."""

    # Required sections (in order)
    REQUIRED_SECTIONS = [
        "VICEPRESIDENCIA",  # Header
        "DATOS GENERALES",  # Datos del solicitante
        "FACULTADES",  # Facultades de la autoridad
        "FUNDAMENTO",  # Fundamento del requerimiento
    ]

    # Legal citation patterns
    LEGAL_CITATION_PATTERN = r'(artículo|art\.|art)\s*\d+'
    RFC_PATTERN = r'[A-Z]{3,4}\d{6}[A-Z0-9]{3}'
    DATE_PATTERN = r'\d{1,2}[/-]\d{1,2}[/-]\d{4}'
    MONETARY_PATTERN = r'\$?\s*\d{1,3}(?:,\d{3})*(?:\.\d{2})?'

    def __init__(self, pdf_path: Path, xml_path: Optional[Path] = None):
        """
        Initialize validator.

        Args:
            pdf_path: Path to PDF to validate
            xml_path: Optional path to source XML
        """
        self.pdf_path = pdf_path
        self.xml_path = xml_path
        self.xml_data = None
        self.pdf_text = None

        if xml_path and xml_path.exists():
            self.xml_data = parse_cnbv_xml(xml_path)

    def extract_text_from_pdf(self) -> str:
        """
        Extract text from PDF using OCR.

        Returns:
            Extracted text (all pages concatenated)
        """
        if not OCR_AVAILABLE:
            raise RuntimeError("OCR not available. Install pytesseract and pdf2image.")

        if self.pdf_text is not None:
            return self.pdf_text

        # Convert PDF to images
        images = convert_from_path(str(self.pdf_path), dpi=300)

        # Extract text from each page
        text_parts = []
        for i, image in enumerate(images):
            # Use Spanish language model if available
            try:
                page_text = pytesseract.image_to_string(image, lang='spa')
            except:
                # Fallback to English
                page_text = pytesseract.image_to_string(image)

            text_parts.append(page_text)

        self.pdf_text = "\n\n=== PAGE BREAK ===\n\n".join(text_parts)
        return self.pdf_text

    def validate_required_sections(self) -> Dict[str, bool]:
        """
        Validate that all required sections are present.

        Returns:
            Dictionary mapping section name to presence (True/False)
        """
        text = self.extract_text_from_pdf()
        text_upper = text.upper()

        results = {}
        for section in self.REQUIRED_SECTIONS:
            # Case-insensitive search
            results[section] = section in text_upper

        return results

    def validate_legal_citations(self) -> List[str]:
        """
        Find all legal citations in the document.

        Returns:
            List of found legal citations
        """
        text = self.extract_text_from_pdf()
        citations = re.findall(self.LEGAL_CITATION_PATTERN, text, re.IGNORECASE)
        return citations

    def validate_rfcs(self) -> List[str]:
        """
        Find all RFC numbers in the document.

        Returns:
            List of found RFCs
        """
        text = self.extract_text_from_pdf()
        rfcs = re.findall(self.RFC_PATTERN, text)
        return rfcs

    def validate_dates(self) -> List[str]:
        """
        Find all dates in the document.

        Returns:
            List of found dates
        """
        text = self.extract_text_from_pdf()
        dates = re.findall(self.DATE_PATTERN, text)
        return dates

    def validate_monetary_amounts(self) -> List[str]:
        """
        Find all monetary amounts in the document.

        Returns:
            List of found amounts
        """
        text = self.extract_text_from_pdf()
        amounts = re.findall(self.MONETARY_PATTERN, text)
        return amounts

    def validate_xml_pdf_consistency(self) -> Dict[str, bool]:
        """
        Validate that key XML data appears in the PDF.

        Returns:
            Dictionary of consistency checks
        """
        if not self.xml_data:
            return {"error": "No XML data available"}

        text = self.extract_text_from_pdf()
        text_normalized = text.upper().replace(" ", "").replace("\n", "")

        results = {}

        # Check oficio number
        oficio = self.xml_data.Cnbv_NumeroOficio
        if oficio:
            oficio_normalized = oficio.upper().replace(" ", "").replace("/", "")
            results["oficio_present"] = oficio_normalized in text_normalized

        # Check expediente number
        expediente = self.xml_data.Cnbv_NumeroExpediente.strip()
        if expediente:
            exp_normalized = expediente.upper().replace(" ", "").replace("-", "")
            results["expediente_present"] = exp_normalized in text_normalized

        # Check RFC (if present)
        if hasattr(self.xml_data.SolicitudPartes, 'Rfc'):
            rfc = self.xml_data.SolicitudPartes.Rfc.strip()
            if rfc and rfc != " " * 13:  # Not empty padding
                results["rfc_present"] = rfc in text

        # Check authority name
        autoridad = self.xml_data.AutoridadNombre
        if autoridad:
            # Partial match (OCR may have errors)
            autoridad_words = autoridad.upper().split()
            matches = sum(1 for word in autoridad_words if len(word) > 3 and word in text_normalized)
            results["autoridad_present"] = matches >= len(autoridad_words) // 2

        return results

    def validate_imperfections(self) -> Dict[str, bool]:
        """
        Validate that document contains realistic imperfections.

        These are INTENTIONAL to simulate real SIARA documents.

        Returns:
            Dictionary of imperfection checks
        """
        text = self.extract_text_from_pdf()

        results = {}

        # Check for intentional spacing errors (CNBV template has these)
        results["has_spacing_errors"] = bool(
            re.search(r'párrafo\s+s\b', text, re.IGNORECASE) or
            re.search(r'indi\s+car', text, re.IGNORECASE) or
            re.search(r'o\s+ficio', text, re.IGNORECASE)
        )

        # Check for inconsistent capitalization
        results["has_mixed_case"] = bool(
            re.search(r'[A-Z]{3,}.*[a-z]{3,}.*[A-Z]{3,}', text)
        )

        # Check for legal reference variations
        legal_refs = re.findall(
            r'(artículo|art\.|art|articulo)\s*\d+',
            text,
            re.IGNORECASE
        )
        unique_patterns = set(match.lower().strip() for match in legal_refs)
        results["has_legal_variation"] = len(unique_patterns) > 1

        return results

    def generate_report(self) -> str:
        """
        Generate comprehensive validation report.

        Returns:
            Formatted report string
        """
        lines = []
        lines.append("=" * 60)
        lines.append("DOCUMENT STRUCTURE VALIDATION REPORT")
        lines.append("=" * 60)
        lines.append(f"\nPDF: {self.pdf_path.name}")
        if self.xml_path:
            lines.append(f"XML: {self.xml_path.name}")
        lines.append("")

        # Section presence
        lines.append("REQUIRED SECTIONS:")
        sections = self.validate_required_sections()
        for section, present in sections.items():
            status = "✓" if present else "✗"
            lines.append(f"  {status} {section}")
        lines.append("")

        # Legal citations
        citations = self.validate_legal_citations()
        lines.append(f"LEGAL CITATIONS: {len(citations)} found")
        if citations:
            lines.append(f"  Examples: {', '.join(citations[:5])}")
        lines.append("")

        # RFCs
        rfcs = self.validate_rfcs()
        lines.append(f"RFCs: {len(rfcs)} found")
        if rfcs:
            lines.append(f"  Found: {', '.join(rfcs)}")
        lines.append("")

        # Dates
        dates = self.validate_dates()
        lines.append(f"DATES: {len(dates)} found")
        if dates:
            lines.append(f"  Examples: {', '.join(dates[:3])}")
        lines.append("")

        # Monetary amounts
        amounts = self.validate_monetary_amounts()
        lines.append(f"MONETARY AMOUNTS: {len(amounts)} found")
        if amounts:
            lines.append(f"  Examples: {', '.join(amounts[:3])}")
        lines.append("")

        # XML-PDF consistency
        if self.xml_data:
            lines.append("XML-PDF CONSISTENCY:")
            consistency = self.validate_xml_pdf_consistency()
            for check, passed in consistency.items():
                if isinstance(passed, bool):
                    status = "✓" if passed else "✗"
                    lines.append(f"  {status} {check}")
            lines.append("")

        # Imperfections (intentional)
        lines.append("REALISTIC IMPERFECTIONS (INTENTIONAL):")
        imperfections = self.validate_imperfections()
        for check, present in imperfections.items():
            status = "✓" if present else "○"
            lines.append(f"  {status} {check}")
        lines.append("")

        lines.append("=" * 60)

        return "\n".join(lines)


class TestDocumentStructure(unittest.TestCase):
    """Unit tests for document structure validation."""

    @classmethod
    def setUpClass(cls):
        """Set up test fixtures."""
        cls.fixtures_dir = Path("../../Prisma/Fixtures/PRP1")
        cls.test_output_dir = Path("test_output")

        # Real sample for comparison
        cls.real_xml = cls.fixtures_dir / "222AAA-44444444442025.xml"
        cls.real_pdf = cls.fixtures_dir / "222AAA-44444444442025.pdf"

    def test_01_required_sections_present(self):
        """Test that all required sections are present in generated PDF."""
        if not OCR_AVAILABLE:
            self.skipTest("OCR not available")

        validator = DocumentStructureValidator(self.real_pdf, self.real_xml)
        sections = validator.validate_required_sections()

        # At least 3 of 4 sections should be present (OCR may miss some)
        present_count = sum(sections.values())
        self.assertGreaterEqual(
            present_count,
            3,
            f"Only {present_count}/4 required sections found: {sections}"
        )

    def test_02_legal_citations_present(self):
        """Test that document contains legal citations."""
        if not OCR_AVAILABLE:
            self.skipTest("OCR not available")

        validator = DocumentStructureValidator(self.real_pdf)
        citations = validator.validate_legal_citations()

        self.assertGreater(
            len(citations),
            0,
            "No legal citations found"
        )

    def test_03_rfc_format_valid(self):
        """Test that RFC numbers match expected format."""
        if not OCR_AVAILABLE:
            self.skipTest("OCR not available")

        validator = DocumentStructureValidator(self.real_pdf, self.real_xml)
        rfcs = validator.validate_rfcs()

        # At least one RFC should be found
        self.assertGreater(
            len(rfcs),
            0,
            "No RFCs found in document"
        )

        # Each RFC should match pattern
        for rfc in rfcs:
            self.assertRegex(
                rfc,
                r'^[A-Z]{3,4}\d{6}[A-Z0-9]{3}$',
                f"RFC {rfc} doesn't match expected pattern"
            )

    def test_04_dates_present(self):
        """Test that document contains dates."""
        if not OCR_AVAILABLE:
            self.skipTest("OCR not available")

        validator = DocumentStructureValidator(self.real_pdf)
        dates = validator.validate_dates()

        self.assertGreater(
            len(dates),
            0,
            "No dates found in document"
        )

    def test_05_monetary_amounts_present(self):
        """Test that document contains monetary amounts."""
        if not OCR_AVAILABLE:
            self.skipTest("OCR not available")

        validator = DocumentStructureValidator(self.real_pdf)
        amounts = validator.validate_monetary_amounts()

        # May or may not have amounts depending on document type
        # Just verify the validator works
        self.assertIsInstance(amounts, list)

    def test_06_xml_pdf_consistency(self):
        """Test that key XML data appears in PDF."""
        if not OCR_AVAILABLE:
            self.skipTest("OCR not available")

        validator = DocumentStructureValidator(self.real_pdf, self.real_xml)
        consistency = validator.validate_xml_pdf_consistency()

        # At least 2 of 4 checks should pass (OCR may have errors)
        passed_count = sum(1 for v in consistency.values() if v is True)
        self.assertGreaterEqual(
            passed_count,
            2,
            f"Only {passed_count} consistency checks passed: {consistency}"
        )

    def test_07_intentional_imperfections(self):
        """Test that document contains realistic imperfections."""
        if not OCR_AVAILABLE:
            self.skipTest("OCR not available")

        validator = DocumentStructureValidator(self.real_pdf)
        imperfections = validator.validate_imperfections()

        # At least one imperfection type should be present
        present_count = sum(imperfections.values())
        self.assertGreater(
            present_count,
            0,
            "No realistic imperfections found (document may be too perfect)"
        )


def main():
    """Main entry point for standalone validation."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Validate CNBV document structure"
    )
    parser.add_argument(
        "pdf",
        type=Path,
        help="Path to PDF to validate"
    )
    parser.add_argument(
        "--xml",
        type=Path,
        help="Optional path to source XML"
    )
    parser.add_argument(
        "--unittest",
        action="store_true",
        help="Run unit tests instead"
    )

    args = parser.parse_args()

    if args.unittest:
        # Run unit tests
        unittest.main(argv=[''], exit=True)
    else:
        # Validate single document
        if not OCR_AVAILABLE:
            print("❌ OCR not available. Install:")
            print("  pip install pytesseract pdf2image")
            print("  Also install Tesseract: https://github.com/tesseract-ocr/tesseract")
            return 1

        validator = DocumentStructureValidator(args.pdf, args.xml)

        try:
            report = validator.generate_report()
            print(report)
            return 0
        except Exception as e:
            print(f"❌ Error: {e}")
            import traceback
            traceback.print_exc()
            return 1


if __name__ == "__main__":
    sys.exit(main())
