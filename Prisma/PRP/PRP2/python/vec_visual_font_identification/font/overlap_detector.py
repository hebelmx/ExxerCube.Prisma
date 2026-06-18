"""Text overlap detection for font analysis."""

from typing import List

from shapely.geometry import box

from vec_visual_font_identification.models.font_detection import FontInfo, OverlapDetection


class OverlapDetector:
    """Detect text overlaps in PDF documents."""

    def detect(self, fonts: List[FontInfo]) -> OverlapDetection:
        """
        Detect text overlaps.

        Args:
            fonts: List of FontInfo objects with position information

        Returns:
            OverlapDetection with overlap detection results
        """
        if not fonts or len(fonts) < 2:
            return OverlapDetection(
                has_character_overlap=False,
                has_block_overlap=False,
                has_image_overlap=False,
                has_margin_overlap=False,
                overlap_count=0,
                issue_codes=[],
            )

        # Convert font positions to Shapely boxes for spatial analysis
        boxes = []
        for font in fonts:
            if font.position:
                boxes.append(
                    (
                        box(
                            font.position["x"],
                            font.position["y"],
                            font.position["x"] + font.position["width"],
                            font.position["y"] + font.position["height"],
                        ),
                        font,
                    )
                )

        # Detect overlaps
        character_overlaps = self._detect_character_overlaps(boxes)
        block_overlaps = self._detect_block_overlaps(boxes)
        margin_overlaps = self._detect_margin_overlaps(boxes)

        # Generate issue codes
        issue_codes = []
        if character_overlaps:
            issue_codes.append("FONT-005")
        if block_overlaps:
            issue_codes.append("FONT-006")
        if margin_overlaps:
            issue_codes.append("FONT-008")

        return OverlapDetection(
            has_character_overlap=len(character_overlaps) > 0,
            has_block_overlap=len(block_overlaps) > 0,
            has_image_overlap=False,  # Would require image detection
            has_margin_overlap=len(margin_overlaps) > 0,
            overlap_count=len(character_overlaps) + len(block_overlaps) + len(margin_overlaps),
            issue_codes=issue_codes,
        )

    def _detect_character_overlaps(self, boxes: List[tuple]) -> List[tuple]:
        """Detect character-level overlaps."""
        overlaps = []
        for i, (box1, font1) in enumerate(boxes):
            for j, (box2, font2) in enumerate(boxes[i + 1 :], start=i + 1):
                if box1.intersects(box2):
                    # Check if same page
                    if font1.page_number == font2.page_number:
                        overlap_area = box1.intersection(box2).area
                        if overlap_area > 0:
                            overlaps.append((font1, font2))
        return overlaps

    def _detect_block_overlaps(self, boxes: List[tuple]) -> List[tuple]:
        """Detect text block overlaps."""
        # Similar to character overlaps but with larger threshold
        return self._detect_character_overlaps(boxes)  # Simplified

    def _detect_margin_overlaps(self, boxes: List[tuple]) -> List[tuple]:
        """Detect text extending beyond margins."""
        # Would need page dimensions
        # Simplified: check if text extends beyond typical margins
        margin_overlaps = []
        page_width = 612  # Default A4 width in points
        page_height = 792  # Default A4 height in points
        margin = 72  # 1 inch margin

        for box_geom, font in boxes:
            if font.position:
                if (
                    font.position["x"] < margin
                    or font.position["x"] + font.position["width"] > page_width - margin
                    or font.position["y"] < margin
                    or font.position["y"] + font.position["height"] > page_height - margin
                ):
                    margin_overlaps.append((font, None))

        return margin_overlaps
