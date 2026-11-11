"""
Tests for text processing modules.
"""
import pytest
from ..text_normalizer import (
    remove_extra_spaces, fix_punctuation_spacing, normalize_line_breaks,
    capitalize_sentences, normalize_text
)
from ..date_extractor import (
    normalize_month_name, parse_spanish_date, extract_spanish_dates, extract_dates
)
from ..amount_extractor import (
    normalize_number_format, parse_amount_value, extract_currency_amounts, extract_amounts
)
from ..expediente_extractor import (
    normalize_expediente_format, extract_expediente_patterns, validate_expediente, extract_expediente
)
from ..section_extractor import (
    normalize_for_matching, find_section_boundaries, extract_section
)


class TestTextNormalizer:
    def test_remove_extra_spaces(self):
        """Test removing extra spaces."""
        text = "This  has   too    many spaces"
        result = remove_extra_spaces(text)
        assert result == "This has too many spaces"

    def test_fix_punctuation_spacing(self):
        """Test fixing punctuation spacing."""
        text = "Hello , world ! How are you ?"
        result = fix_punctuation_spacing(text)
        assert result == "Hello, world! How are you?"

    def test_normalize_line_breaks(self):
        """Test normalizing line breaks."""
        text = "Line 1\r\nLine 2\rLine 3\n\n\n\nLine 4"
        result = normalize_line_breaks(text)
        assert result == "Line 1\nLine 2\nLine 3\n\nLine 4"

    def test_capitalize_sentences(self):
        """Test capitalizing sentences."""
        text = "hello world. this is a test! how are you?"
        result = capitalize_sentences(text)
        assert result == "Hello world. This is a test! How are you?"

    def test_normalize_text_full(self):
        """Test complete text normalization."""
        text = "hello   world . this  is  a  test !\n\n\nhow are you ?"
        result = normalize_text(text, capitalize=True)
        expected = "Hello world. This is a test!\n\nHow are you?"
        assert result == expected


class TestDateExtractor:
    def test_normalize_month_name(self):
        """Test normalizing Spanish month names."""
        assert normalize_month_name("enero") == "01"
        assert normalize_month_name("DICIEMBRE") == "12"
        assert normalize_month_name("septiembre") == "09"
        assert normalize_month_name("setiembre") == "09"  # Alternative spelling
        assert normalize_month_name("invalid") is None

    def test_parse_spanish_date(self):
        """Test parsing Spanish date format."""
        assert parse_spanish_date("15", "octubre", "2023") == "2023-10-15"
        assert parse_spanish_date("1", "enero", "2024") == "2024-01-01"
        assert parse_spanish_date("32", "enero", "2024") is None  # Invalid day
        assert parse_spanish_date("15", "invalid", "2023") is None  # Invalid month

    def test_extract_spanish_dates(self):
        """Test extracting Spanish dates from text."""
        text = "El 15 de octubre de 2023 y el 1 de enero de 2024"
        dates = extract_spanish_dates(text)
        assert len(dates) == 2
        assert ("15 de octubre de 2023", "2023-10-15") in dates
        assert ("1 de enero de 2024", "2024-01-01") in dates

    def test_extract_dates(self):
        """Test complete date extraction."""
        text = "Fecha: 15 de octubre de 2023 y también 2023-12-25"
        dates = extract_dates(text)
        assert "2023-10-15" in dates
        assert "2023-12-25" in dates


class TestAmountExtractor:
    def test_normalize_number_format(self):
        """Test normalizing number format."""
        assert normalize_number_format("1,500.75") == "150075"
        assert normalize_number_format("1 500 000") == "1500000"

    def test_parse_amount_value(self):
        """Test parsing amount values."""
        assert parse_amount_value("1500", "75") == 1500.75
        assert parse_amount_value("1000") == 1000.0
        assert parse_amount_value("500", "5") == 500.50  # Single digit becomes 50 cents
        assert parse_amount_value("invalid") is None

    def test_extract_currency_amounts(self):
        """Test extracting currency amounts."""
        text = "El costo es $1,500.75 y también $250"
        amounts = extract_currency_amounts(text, "$")
        assert len(amounts) == 2
        assert ("$1,500.75", "1,500", "75") in amounts
        assert ("$250", "250", None) in amounts

    def test_extract_amounts(self):
        """Test complete amount extraction."""
        text = "Monto: $1,500.75 y total: $2,000.00"
        amounts = extract_amounts(text)
        assert len(amounts) >= 2
        values = [amt.value for amt in amounts]
        assert 1500.75 in values
        assert 2000.0 in values


class TestExpedienteExtractor:
    def test_normalize_expediente_format(self):
        """Test normalizing expediente format."""
        result = normalize_expediente_format("exp - 123 / 2023")
        assert result == "EXP-123/2023"

    def test_extract_expediente_patterns(self):
        """Test extracting expediente patterns."""
        text = "Expediente: ABC-123/2023 y también Exp. No. 456/24"
        patterns = extract_expediente_patterns(text)
        assert len(patterns) >= 1
        # Should find at least one expediente
        found_values = [match[1] for match in patterns]
        assert any("ABC-123/2023" in val for val in found_values)

    def test_validate_expediente(self):
        """Test expediente validation."""
        assert validate_expediente("ABC-123/2023") is True
        assert validate_expediente("EXP456") is True
        assert validate_expediente("X") is False  # Too short
        assert validate_expediente("de") is False  # Common false positive

    def test_extract_expediente(self):
        """Test complete expediente extraction."""
        text = "En relación al Expediente: ABC-123/2023, se requiere..."
        result = extract_expediente(text)
        assert result is not None
        assert "ABC-123/2023" in result


class TestSectionExtractor:
    def test_normalize_for_matching(self):
        """Test text normalization for matching."""
        result = normalize_for_matching("ACCIÓN Solicitada")
        assert result == "ACCION SOLICITADA"

    def test_find_section_boundaries(self):
        """Test finding section boundaries."""
        text = "CAUSA QUE MOTIVA EL REQUERIMIENTO: Texto de la causa. ACCIÓN SOLICITADA: Texto de la acción."
        norm_text = normalize_for_matching(text)
        
        start_aliases = ["CAUSA QUE MOTIVA EL REQUERIMIENTO"]
        end_aliases = ["ACCIÓN SOLICITADA"]
        
        start, end = find_section_boundaries(norm_text, start_aliases, end_aliases)
        assert start >= 0
        assert end > start

    def test_extract_section(self):
        """Test complete section extraction."""
        text = "CAUSA QUE MOTIVA EL REQUERIMIENTO: Esta es la causa del problema. ACCIÓN SOLICITADA: Esta es la acción requerida."
        
        start_aliases = ["CAUSA QUE MOTIVA EL REQUERIMIENTO"]
        end_aliases = ["ACCIÓN SOLICITADA"]
        
        result = extract_section(text, start_aliases, end_aliases, include_header=False)
        assert result is not None
        assert "Esta es la causa" in result
        assert "ACCIÓN SOLICITADA" not in result  # Header should be excluded