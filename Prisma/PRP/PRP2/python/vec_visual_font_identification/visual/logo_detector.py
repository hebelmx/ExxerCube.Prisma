"""CLIP-based logo detection for VEC statements."""

import time
from typing import List, Optional

import torch
from PIL import Image
from transformers import CLIPModel, CLIPProcessor

from vec_visual_font_identification.models.logo_detection import (
    ImageQualityMetrics,
    LogoDetectionResult,
    LogoInfo,
    Position,
)
from vec_visual_font_identification.utils.image_utils import pdf_to_images
from vec_visual_font_identification.visual.image_quality import ImageQualityAnalyzer


class LogoDetector:
    """CLIP-based logo detector for VEC statements."""

    def __init__(
        self,
        model_name: str = "openai/clip-vit-large-patch14",
        device: Optional[str] = None,
    ):
        """
        Initialize logo detector.

        Args:
            model_name: HuggingFace model name for CLIP
            device: Device to use ('cuda', 'cpu', or None for auto-detect)
        """
        self.device = device or ("cuda" if torch.cuda.is_available() else "cpu")
        self.processor = CLIPProcessor.from_pretrained(model_name)
        self.model = CLIPModel.from_pretrained(model_name).to(self.device)
        self.model.eval()  # Set to evaluation mode

        self.image_quality_analyzer = ImageQualityAnalyzer()

        # Logo reference texts (can be extended with fine-tuned model)
        self.logo_texts = [
            "a bank logo",
            "a financial institution logo",
            "a company logo",
            "a watermark",
            "Vector Casa de Bolsa logo",
            "VEC logo",
        ]

    def detect_logos(
        self,
        pdf_bytes: bytes,
        page_number: Optional[int] = None,
        logo_specifications: Optional[dict] = None,
    ) -> LogoDetectionResult:
        """
        Detect logos in PDF document.

        Args:
            pdf_bytes: PDF file as bytes
            page_number: Specific page to process (None for all pages)
            logo_specifications: Logo position and size specifications

        Returns:
            LogoDetectionResult with detected logos and quality metrics
        """
        start_time = time.time()

        # Convert PDF to images
        images = pdf_to_images(pdf_bytes)
        if page_number is not None:
            images = [images[page_number]]

        detected_logos = []

        for page_idx, image in enumerate(images):
            page_logos = self._detect_logos_on_page(
                image, page_idx, logo_specifications or {}
            )
            detected_logos.extend(page_logos)

        processing_time = time.time() - start_time

        # Calculate overall quality score
        overall_score = self._calculate_overall_quality_score(detected_logos)

        return LogoDetectionResult(
            logos=detected_logos,
            total_pages=len(images),
            processing_time_seconds=processing_time,
            overall_quality_score=overall_score,
        )

    def _detect_logos_on_page(
        self, image: Image.Image, page_number: int, logo_specs: dict
    ) -> List[LogoInfo]:
        """Detect logos on a single page."""
        logos = []

        # Prepare inputs for CLIP
        inputs = self.processor(
            text=self.logo_texts, images=image, return_tensors="pt", padding=True
        ).to(self.device)

        # Run CLIP inference
        with torch.no_grad():
            outputs = self.model(**inputs)
            logits_per_image = outputs.logits_per_image
            probs = logits_per_image.softmax(dim=1)

        # Find logo detections above threshold
        confidence_threshold = 0.5
        max_prob, max_idx = probs.max(dim=1)

        if max_prob.item() > confidence_threshold:
            # For now, detect logo at center (can be enhanced with object detection)
            # In production, use fine-tuned CLIP with bounding box regression
            image_width, image_height = image.size
            logo_position = Position(
                x=image_width * 0.1,  # Approximate position (top-left)
                y=image_height * 0.1,
                width=image_width * 0.2,
                height=image_height * 0.1,
                page_number=page_number,
            )

            # Analyze image quality
            quality_metrics = self.image_quality_analyzer.analyze(image)

            # Validate position and size
            position_valid = self._validate_position(logo_position, logo_specs)
            size_valid = self._validate_size(logo_position, logo_specs)

            # Generate issue codes
            issue_codes = self._generate_issue_codes(
                quality_metrics, position_valid, size_valid
            )

            logo_info = LogoInfo(
                family=self.logo_texts[max_idx.item()],
                confidence=max_prob.item(),
                position=logo_position,
                quality=quality_metrics,
                size_valid=size_valid,
                position_valid=position_valid,
                issue_codes=issue_codes,
            )

            logos.append(logo_info)

        return logos

    def _validate_position(self, position: Position, logo_specs: dict) -> bool:
        """Validate logo position against specifications."""
        if not logo_specs.get("allowed_positions"):
            return True  # No specifications, assume valid

        # Check if position is within allowed regions
        # Simplified validation (can be enhanced)
        return True

    def _validate_size(self, position: Position, logo_specs: dict) -> bool:
        """Validate logo size against specifications."""
        if not logo_specs.get("size_range"):
            return True  # No specifications, assume valid

        min_width, max_width = logo_specs["size_range"]["width"]
        min_height, max_height = logo_specs["size_range"]["height"]

        return (
            min_width <= position.width <= max_width
            and min_height <= position.height <= max_height
        )

    def _generate_issue_codes(
        self,
        quality: ImageQualityMetrics,
        position_valid: bool,
        size_valid: bool,
    ) -> List[str]:
        """Generate issue codes based on quality metrics and validation."""
        issues = []

        if not position_valid:
            issues.append("IMG-002")  # Logo position invalid
        if not size_valid:
            issues.append("IMG-003")  # Logo size invalid
        if quality.resolution_dpi < 300:
            issues.append("IMG-004")  # Resolution too low
        if quality.blur_detected:
            issues.append("IMG-005")  # Blur detected
        if quality.contrast_ratio < 4.5:
            issues.append("IMG-006")  # Contrast insufficient
        if quality.color_accuracy_score < 0.8:
            issues.append("IMG-007")  # Color accuracy low
        if quality.compression_artifacts_detected:
            issues.append("IMG-008")  # Compression artifacts

        return issues

    def _calculate_overall_quality_score(self, logos: List[LogoInfo]) -> float:
        """Calculate overall logo quality score."""
        if not logos:
            return 0.0

        # Average quality scores across all logos
        scores = []
        for logo in logos:
            score = (
                logo.quality.clarity_score * 0.3
                + logo.quality.color_accuracy_score * 0.3
                + (1.0 if logo.size_valid else 0.0) * 0.2
                + (1.0 if logo.position_valid else 0.0) * 0.2
            )
            scores.append(score)

        return sum(scores) / len(scores) if scores else 0.0
