"""VEC Visual & Font Identification Package."""

__version__ = "0.1.0"

from vec_visual_font_identification.visual import LogoDetector
from vec_visual_font_identification.font import FontDetector
from vec_visual_font_identification.quality import QualityVerifier

__all__ = [
    "LogoDetector",
    "FontDetector",
    "QualityVerifier",
]
