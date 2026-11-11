"""
Pydantic models for Spanish legal document extraction.
"""

from typing import List, Optional, Dict, Any
from pydantic import BaseModel, Field, validator
from datetime import datetime


class RequerimientoDetalle(BaseModel):
    """Details of a legal requirement including monetary information."""
    
    descripcion: Optional[str] = Field(
        default="unknown",
        description="Description of the requirement details"
    )
    monto: Optional[float] = Field(
        default=None,
        description="Monetary amount if applicable"
    )
    moneda: Optional[str] = Field(
        default="unknown",
        description="Currency code (e.g., MXN, USD)"
    )
    activoVirtual: Optional[str] = Field(
        default="unknown",
        description="Virtual asset information if applicable"
    )
    
    @validator('moneda')
    def validate_currency(cls, v):
        """Validate currency code."""
        valid_currencies = ['MXN', 'USD', 'EUR', 'unknown']
        if v and v not in valid_currencies:
            return 'unknown'
        return v


class Requerimiento(BaseModel):
    """Spanish legal requirement document model."""
    
    fecha: Optional[str] = Field(
        default="unknown",
        description="Date in YYYY-MM-DD format"
    )
    autoridadEmisora: Optional[str] = Field(
        default="unknown",
        description="Issuing authority"
    )
    expediente: Optional[str] = Field(
        default="unknown",
        description="Case/file number"
    )
    tipoRequerimiento: Optional[str] = Field(
        default="unknown",
        description="Type of requirement"
    )
    subtipoRequerimiento: Optional[str] = Field(
        default="unknown",
        description="Subtype of requirement"
    )
    fundamentoLegal: Optional[str] = Field(
        default="unknown",
        description="Legal foundation/basis"
    )
    motivacion: Optional[str] = Field(
        default="unknown",
        description="Motivation or cause"
    )
    partes: Optional[List[str]] = Field(
        default_factory=list,
        description="Parties involved"
    )
    detalle: Optional[RequerimientoDetalle] = Field(
        default_factory=RequerimientoDetalle,
        description="Requirement details"
    )
    
    @validator('fecha')
    def validate_date(cls, v):
        """Validate date format."""
        if v == "unknown":
            return v
        try:
            datetime.strptime(v, '%Y-%m-%d')
            return v
        except ValueError:
            return "unknown"


class ExtractedDocument(BaseModel):
    """Generic extracted document model."""
    
    document_type: str = Field(description="Type of document")
    confidence_score: Optional[float] = Field(
        default=None,
        ge=0.0,
        le=1.0,
        description="Extraction confidence score"
    )
    raw_text: Optional[str] = Field(
        default=None,
        description="Raw extracted text"
    )
    structured_data: Dict[str, Any] = Field(
        default_factory=dict,
        description="Structured extracted data"
    )
    metadata: Dict[str, Any] = Field(
        default_factory=dict,
        description="Additional metadata"
    )


class ExtractionResult(BaseModel):
    """Result of document extraction process."""
    
    success: bool = Field(description="Whether extraction was successful")
    document: Optional[ExtractedDocument] = Field(
        default=None,
        description="Extracted document if successful"
    )
    error_message: Optional[str] = Field(
        default=None,
        description="Error message if extraction failed"
    )
    processing_time: Optional[float] = Field(
        default=None,
        description="Processing time in seconds"
    )
    extractor_name: str = Field(description="Name of the extractor used")