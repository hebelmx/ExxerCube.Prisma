"""
Pydantic models for type-safe data structures in the OCR pipeline.
Each model represents a distinct data concept with validation.
"""
from typing import List, Optional, Dict, Any
from pydantic import BaseModel, Field, field_validator
import numpy as np
from numpy.typing import NDArray


class ImageData(BaseModel):
    """Represents a single image with metadata."""
    
    data: NDArray[np.uint8]
    source_path: str
    page_number: int = 1
    total_pages: int = 1
    
    class Config:
        arbitrary_types_allowed = True
    
    @field_validator('data')
    def validate_image_data(cls, v):
        if not isinstance(v, np.ndarray):
            raise ValueError("Image data must be a numpy array")
        if v.ndim not in [2, 3]:
            raise ValueError("Image must be 2D (grayscale) or 3D (color)")
        return v


class OCRConfig(BaseModel):
    """Configuration for OCR execution."""
    
    language: str = Field(default="spa", pattern="^[a-z]{3}$")
    oem: int = Field(default=1, ge=0, le=3)
    psm: int = Field(default=6, ge=0, le=13)
    fallback_language: str = Field(default="eng", pattern="^[a-z]{3}$")


class OCRResult(BaseModel):
    """Result from OCR execution with confidence metrics."""
    
    text: str
    confidence_avg: float = Field(ge=0.0, le=100.0)
    confidence_median: float = Field(ge=0.0, le=100.0)
    confidences: List[float] = Field(default_factory=list)
    language_used: str
    
    @field_validator('confidences')
    def validate_confidences(cls, v):
        return [max(0.0, min(100.0, conf)) for conf in v]


class AmountData(BaseModel):
    """Represents a monetary amount with currency."""
    
    currency: str = Field(default="MXN", pattern="^[A-Z]{3}$")
    value: float = Field(ge=0.0)
    original_text: str


class ExtractedFields(BaseModel):
    """Structured data extracted from OCR text."""
    
    expediente: Optional[str] = None
    causa: Optional[str] = None
    accion_solicitada: Optional[str] = None
    fechas: List[str] = Field(default_factory=list)
    montos: List[AmountData] = Field(default_factory=list)
    
    @field_validator('fechas')
    def validate_dates(cls, v):
        # Dates should be in YYYY-MM-DD format
        import re
        date_pattern = re.compile(r'^\d{4}-\d{2}-\d{2}$')
        return [date for date in v if date_pattern.match(date)]


class ProcessingConfig(BaseModel):
    """Configuration for the entire processing pipeline."""
    
    remove_watermark: bool = True
    deskew: bool = True
    binarize: bool = True
    ocr_config: OCRConfig = Field(default_factory=OCRConfig)
    extract_sections: bool = True
    normalize_text: bool = True


class OutputData(BaseModel):
    """Data structure for output persistence."""
    
    text_content: str
    extracted_fields: ExtractedFields
    metadata: Dict[str, Any] = Field(default_factory=dict)
    

class ProcessingResult(BaseModel):
    """Final result of processing a single image."""
    
    source_path: str
    page_number: int
    ocr_result: Optional[OCRResult] = None
    extracted_fields: ExtractedFields
    output_path: Optional[str] = None
    processing_errors: List[str] = Field(default_factory=list)