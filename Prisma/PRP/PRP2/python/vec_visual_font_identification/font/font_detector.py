"""Font detection for VEC statements."""

import io
import time
from typing import List, Optional, Set

import pdfplumber

from vec_visual_font_identification.models.font_detection import (
    FontDetectionResult,
    FontInfo,
    FontWeight,
    OverlapDetection,
    TypographyMetrics,
)
from vec_visual_font_identification.font.overlap_detector import OverlapDetector
from vec_visual_font_identification.font.typography_analyzer import TypographyAnalyzer


class FontDetector:
    """Font detector for PDF documents."""

    def __init__(
        self,
        approved_fonts: Optional[Set[str]] = None,
        font_size_ranges: Optional[dict] = None,
    ):
        """
        Initialize font detector.

        Args:
            approved_fonts: Set of approved font family names
            font_size_ranges: Font size ranges by text type
                {
                    "header": (12, 16),
                    "body": (9, 11),
                    "footnote": (7, 8),
                }
        """
        self.approved_fonts = approved_fonts or {
            "Arial",
            "Times New Roman",
            "Calibri",
            "Aptos",  # VEC approved font
        }

        self.font_size_ranges = font_size_ranges or {
            "header": (12, 16),
            "body": (9, 11),
            "footnote": (7, 8),
        }

        self.overlap_detector = OverlapDetector()
        self.typography_analyzer = TypographyAnalyzer()

    def detect_fonts(
        self,
        pdf_bytes: bytes,
        page_number: Optional[int] = None,
    ) -> FontDetectionResult:
        """
        Detect fonts in PDF document.

        Args:
            pdf_bytes: PDF file as bytes
            page_number: Specific page to process (None for all pages)

        Returns:
            FontDetectionResult with detected fonts and typography metrics
        """
        start_time = time.time()

        # Open PDF with pdfplumber
        pdf = pdfplumber.open(io.BytesIO(pdf_bytes))
        pages = [pdf.pages[page_number]] if page_number is not None else pdf.pages

        detected_fonts = []
        all_text_objects = []

        for page_idx, page in enumerate(pages):
            # Extract text with font information
            chars = page.chars
            text_objects = self._extract_font_info(chars, page_idx)
            detected_fonts.extend(text_objects)
            all_text_objects.extend(text_objects)

        pdf.close()

        # Analyze typography
        typography_metrics = self.typography_analyzer.analyze(all_text_objects)

        # Detect overlaps
        overlap_detection = self.overlap_detector.detect(all_text_objects)

        # Calculate overall compliance score
        compliance_score = self._calculate_compliance_score(
            detected_fonts, typography_metrics, overlap_detection
        )

        processing_time = time.time() - start_time

        return FontDetectionResult(
            fonts=detected_fonts,
            typography_metrics=typography_metrics,
            overlap_detection=overlap_detection,
            total_pages=len(pages),
            processing_time_seconds=processing_time,
            overall_compliance_score=compliance_score,
        )

    def _extract_font_info(self, chars: List, page_number: int) -> List[FontInfo]:
        """Extract font information from PDF characters."""
        fonts = []
        font_groups = {}

        # Group characters by font
        for char in chars:
            font_key = (
                char.get("fontname", "Unknown"),
                char.get("size", 10),
                char.get("fonttype", "Unknown"),
            )

            if font_key not in font_groups:
                font_groups[font_key] = {
                    "chars": [],
                    "positions": [],
                }

            font_groups[font_key]["chars"].append(char.get("text", ""))
            font_groups[font_key]["positions"].append(
                {
                    "x": char.get("x0", 0),
                    "y": char.get("y0", 0),
                    "width": char.get("width", 0),
                    "height": char.get("height", 0),
                }
            )

        # Create FontInfo for each font group
        for (fontname, size, fonttype), group in font_groups.items():
            text_sample = "".join(group["chars"][:50])  # First 50 chars as sample

            # Determine font weight
            weight = self._determine_font_weight(fontname)

            # Check if font is approved
            is_approved = fontname in self.approved_fonts

            # Generate issue codes
            issue_codes = self._generate_issue_codes(fontname, size, fonttype, is_approved)

            # Calculate average position
            positions = group["positions"]
            avg_position = {
                "x": sum(p["x"] for p in positions) / len(positions),
                "y": sum(p["y"] for p in positions) / len(positions),
                "width": max(p["width"] for p in positions),
                "height": max(p["height"] for p in positions),
            }

            font_info = FontInfo(
                family=fontname,
                size=size,
                weight=weight,
                is_embedded=fonttype != "Unknown",
                is_approved=is_approved,
                text_sample=text_sample,
                position=avg_position,
                page_number=page_number,
                issue_codes=issue_codes,
            )

            fonts.append(font_info)

        return fonts

    def _determine_font_weight(self, fontname: str) -> FontWeight:
        """Determine font weight from font name."""
        fontname_lower = fontname.lower()
        if "bold" in fontname_lower and "italic" in fontname_lower:
            return FontWeight.BOLD_ITALIC
        elif "bold" in fontname_lower:
            return FontWeight.BOLD
        elif "italic" in fontname_lower or "oblique" in fontname_lower:
            return FontWeight.ITALIC
        else:
            return FontWeight.REGULAR

    def _generate_issue_codes(
        self, fontname: str, size: float, fonttype: str, is_approved: bool
    ) -> List[str]:
        """Generate issue codes for font violations."""
        issues = []

        if not is_approved:
            issues.append("FONT-001")  # Unapproved font

        # Check font size ranges
        if not any(
            min_size <= size <= max_size
            for min_size, max_size in self.font_size_ranges.values()
        ):
            issues.append("FONT-002")  # Font size out of range

        if fonttype == "Unknown":
            issues.append("FONT-003")  # Font not embedded

        return issues

    def _calculate_compliance_score(
        self,
        fonts: List[FontInfo],
        typography: TypographyMetrics,
        overlap: OverlapDetection,
    ) -> float:
        """Calculate overall font compliance score."""
        if not fonts:
            return 0.0

        # Score components
        approved_font_score = sum(1.0 if f.is_approved else 0.0 for f in fonts) / len(fonts)
        embedded_font_score = sum(1.0 if f.is_embedded else 0.0 for f in fonts) / len(fonts)
        no_overlap_score = 1.0 if not overlap.has_character_overlap else 0.5
        contrast_score = 1.0 if typography.meets_wcag_aa else 0.5

        # Weighted average
        overall_score = (
            approved_font_score * 0.3
            + embedded_font_score * 0.2
            + no_overlap_score * 0.3
            + contrast_score * 0.2
        )

        return overall_score
