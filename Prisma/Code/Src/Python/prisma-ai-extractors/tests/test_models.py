"""
Unit tests for document models.
"""

import pytest
from datetime import datetime
from src.models import (
    RequerimientoDetalle,
    Requerimiento,
    ExtractedDocument,
    ExtractionResult
)


class TestRequerimientoDetalle:
    """Test RequerimientoDetalle model."""
    
    def test_default_values(self):
        """Test default values are set correctly."""
        detalle = RequerimientoDetalle()
        assert detalle.descripcion == "unknown"
        assert detalle.monto is None
        assert detalle.moneda == "unknown"
        assert detalle.activoVirtual == "unknown"
    
    def test_with_values(self):
        """Test model with provided values."""
        detalle = RequerimientoDetalle(
            descripcion="Embargo de cuenta",
            monto=50000.0,
            moneda="MXN",
            activoVirtual="Bitcoin"
        )
        assert detalle.descripcion == "Embargo de cuenta"
        assert detalle.monto == 50000.0
        assert detalle.moneda == "MXN"
        assert detalle.activoVirtual == "Bitcoin"
    
    def test_currency_validation(self):
        """Test currency code validation."""
        # Valid currency
        detalle = RequerimientoDetalle(moneda="USD")
        assert detalle.moneda == "USD"
        
        # Invalid currency should default to unknown
        detalle = RequerimientoDetalle(moneda="XYZ")
        assert detalle.moneda == "unknown"


class TestRequerimiento:
    """Test Requerimiento model."""
    
    def test_default_values(self):
        """Test default values are set correctly."""
        req = Requerimiento()
        assert req.fecha == "unknown"
        assert req.autoridadEmisora == "unknown"
        assert req.expediente == "unknown"
        assert req.partes == []
        assert isinstance(req.detalle, RequerimientoDetalle)
    
    def test_date_validation(self):
        """Test date format validation."""
        # Valid date
        req = Requerimiento(fecha="2024-01-15")
        assert req.fecha == "2024-01-15"
        
        # Invalid date format
        req = Requerimiento(fecha="15/01/2024")
        assert req.fecha == "unknown"
        
        # Invalid date
        req = Requerimiento(fecha="2024-13-45")
        assert req.fecha == "unknown"
    
    def test_complete_document(self):
        """Test complete document with all fields."""
        req = Requerimiento(
            fecha="2024-01-15",
            autoridadEmisora="CONDUSEF",
            expediente="EXP-2024-001",
            tipoRequerimiento="EMBARGO",
            subtipoRequerimiento="CUENTA_BANCARIA",
            fundamentoLegal="Artículo 142 Ley de Instituciones de Crédito",
            motivacion="Incumplimiento de obligaciones",
            partes=["Juan Pérez", "Banco XYZ"],
            detalle=RequerimientoDetalle(
                descripcion="Embargo total",
                monto=100000.0,
                moneda="MXN"
            )
        )
        
        assert req.fecha == "2024-01-15"
        assert req.expediente == "EXP-2024-001"
        assert len(req.partes) == 2
        assert req.detalle.monto == 100000.0
    
    def test_model_dump(self):
        """Test model serialization."""
        req = Requerimiento(
            fecha="2024-01-15",
            expediente="EXP-001"
        )
        
        data = req.model_dump()
        assert isinstance(data, dict)
        assert data['fecha'] == "2024-01-15"
        assert data['expediente'] == "EXP-001"
        assert 'detalle' in data


class TestExtractedDocument:
    """Test ExtractedDocument model."""
    
    def test_creation(self):
        """Test document creation."""
        doc = ExtractedDocument(
            document_type="legal_requirement",
            confidence_score=0.95,
            raw_text="Sample text",
            structured_data={"key": "value"},
            metadata={"source": "test.pdf"}
        )
        
        assert doc.document_type == "legal_requirement"
        assert doc.confidence_score == 0.95
        assert doc.raw_text == "Sample text"
        assert doc.structured_data["key"] == "value"
        assert doc.metadata["source"] == "test.pdf"
    
    def test_confidence_validation(self):
        """Test confidence score validation."""
        # Valid range
        doc = ExtractedDocument(
            document_type="test",
            confidence_score=0.5
        )
        assert doc.confidence_score == 0.5
        
        # Test bounds
        with pytest.raises(ValueError):
            ExtractedDocument(
                document_type="test",
                confidence_score=1.5  # Out of range
            )
        
        with pytest.raises(ValueError):
            ExtractedDocument(
                document_type="test",
                confidence_score=-0.1  # Out of range
            )


class TestExtractionResult:
    """Test ExtractionResult model."""
    
    def test_success_result(self):
        """Test successful extraction result."""
        doc = ExtractedDocument(
            document_type="test",
            structured_data={"data": "value"}
        )
        
        result = ExtractionResult(
            success=True,
            document=doc,
            processing_time=2.5,
            extractor_name="TestExtractor"
        )
        
        assert result.success is True
        assert result.document is not None
        assert result.error_message is None
        assert result.processing_time == 2.5
        assert result.extractor_name == "TestExtractor"
    
    def test_failure_result(self):
        """Test failed extraction result."""
        result = ExtractionResult(
            success=False,
            error_message="Failed to load image",
            processing_time=0.1,
            extractor_name="TestExtractor"
        )
        
        assert result.success is False
        assert result.document is None
        assert result.error_message == "Failed to load image"
        assert result.processing_time == 0.1
    
    def test_model_dump_json(self):
        """Test JSON serialization."""
        result = ExtractionResult(
            success=True,
            processing_time=1.5,
            extractor_name="Test"
        )
        
        json_str = result.model_dump_json()
        assert isinstance(json_str, str)
        assert '"success": true' in json_str or '"success":true' in json_str
        assert "Test" in json_str