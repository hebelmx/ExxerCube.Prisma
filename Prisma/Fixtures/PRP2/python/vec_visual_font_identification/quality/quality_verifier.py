"""Complete quality verification orchestration."""

import time
from typing import Optional

from vec_visual_font_identification.font import FontDetector
from vec_visual_font_identification.models.quality_report import (
    Issue,
    IssueCode,
    IssueSeverity,
    QualityReport,
    QualityScore,
)
from vec_visual_font_identification.visual import LogoDetector


class QualityVerifier:
    """Orchestrates complete document quality verification."""

    def __init__(
        self,
        logo_detector: Optional[LogoDetector] = None,
        font_detector: Optional[FontDetector] = None,
    ):
        """
        Initialize quality verifier.

        Args:
            logo_detector: LogoDetector instance (creates new if None)
            font_detector: FontDetector instance (creates new if None)
        """
        self.logo_detector = logo_detector or LogoDetector()
        self.font_detector = font_detector or FontDetector()

    def verify_document(
        self,
        pdf_bytes: bytes,
        document_id: Optional[str] = None,
    ) -> QualityReport:
        """
        Verify complete document quality.

        Args:
            pdf_bytes: PDF file as bytes
            document_id: Optional document identifier

        Returns:
            QualityReport with complete quality verification results
        """
        start_time = time.time()

        # Run logo detection
        logo_result = self.logo_detector.detect_logos(pdf_bytes)

        # Run font detection
        font_result = self.font_detector.detect_fonts(pdf_bytes)

        # Aggregate issues
        issues = self._aggregate_issues(logo_result, font_result)

        # Calculate category scores
        category_scores = [
            QualityScore(
                category="logo",
                score=logo_result.overall_quality_score,
                weight=0.3,
            ),
            QualityScore(
                category="font",
                score=font_result.overall_compliance_score,
                weight=0.4,
            ),
            QualityScore(
                category="layout",
                score=0.9,  # Placeholder - would implement layout verification
                weight=0.2,
            ),
            QualityScore(
                category="marketing",
                score=0.9,  # Placeholder - would implement marketing verification
                weight=0.1,
            ),
        ]

        # Calculate overall score (weighted average)
        overall_score = sum(
            score.score * score.weight for score in category_scores
        ) / sum(score.weight for score in category_scores)

        # Count issues by severity
        issue_count_by_severity = self._count_issues_by_severity(issues)

        processing_time = time.time() - start_time

        return QualityReport(
            document_id=document_id,
            total_pages=logo_result.total_pages,
            processing_time_seconds=processing_time,
            overall_score=overall_score,
            category_scores=category_scores,
            issues=issues,
            issue_count_by_severity=issue_count_by_severity,
            logo_detection_result=logo_result.model_dump(),
            font_detection_result=font_result.model_dump(),
        )

    def _aggregate_issues(self, logo_result, font_result) -> list[Issue]:
        """Aggregate issues from logo and font detection."""
        issues = []

        # Logo issues
        for logo in logo_result.logos:
            for issue_code_str in logo.issue_codes:
                try:
                    issue_code = IssueCode(issue_code_str)
                    issues.append(
                        Issue(
                            code=issue_code,
                            severity=self._determine_severity(issue_code),
                            message=f"Logo issue: {issue_code_str}",
                            page_number=logo.position.page_number,
                            position={
                                "x": logo.position.x,
                                "y": logo.position.y,
                                "width": logo.position.width,
                                "height": logo.position.height,
                            },
                        )
                    )
                except ValueError:
                    # Unknown issue code, skip
                    pass

        # Font issues
        for font in font_result.fonts:
            for issue_code_str in font.issue_codes:
                try:
                    issue_code = IssueCode(issue_code_str)
                    issues.append(
                        Issue(
                            code=issue_code,
                            severity=self._determine_severity(issue_code),
                            message=f"Font issue: {issue_code_str}",
                            page_number=font.page_number,
                            position=font.position,
                        )
                    )
                except ValueError:
                    # Unknown issue code, skip
                    pass

        # Overlap issues
        if font_result.overlap_detection.issue_codes:
            for issue_code_str in font_result.overlap_detection.issue_codes:
                try:
                    issue_code = IssueCode(issue_code_str)
                    issues.append(
                        Issue(
                            code=issue_code,
                            severity=self._determine_severity(issue_code),
                            message=f"Overlap issue: {issue_code_str}",
                        )
                    )
                except ValueError:
                    pass

        return issues

    def _determine_severity(self, issue_code: IssueCode) -> IssueSeverity:
        """Determine issue severity from issue code."""
        # Critical issues
        critical_codes = {
            IssueCode.IMG_001,  # Logo not found
            IssueCode.FONT_001,  # Unapproved font
            IssueCode.FONT_003,  # Font not embedded
        }

        # High severity issues
        high_codes = {
            IssueCode.IMG_004,  # Resolution too low
            IssueCode.IMG_005,  # Blur detected
            IssueCode.FONT_005,  # Character overlap
            IssueCode.FONT_011,  # Contrast insufficient
        }

        if issue_code in critical_codes:
            return IssueSeverity.CRITICAL
        elif issue_code in high_codes:
            return IssueSeverity.HIGH
        else:
            return IssueSeverity.MEDIUM

    def _count_issues_by_severity(self, issues: list[Issue]) -> dict:
        """Count issues grouped by severity."""
        counts = {
            IssueSeverity.CRITICAL: 0,
            IssueSeverity.HIGH: 0,
            IssueSeverity.MEDIUM: 0,
            IssueSeverity.LOW: 0,
        }

        for issue in issues:
            counts[issue.severity] = counts.get(issue.severity, 0) + 1

        return {severity.value: count for severity, count in counts.items()}
