"""
Unit tests for DocumentValidator module.
"""

import pytest
from pydantic import BaseModel, ValidationError
from src.modules.document_validator import DocumentValidator
from src.models import Requerimiento


class TestDocumentValidator:
    """Test DocumentValidator functionality."""
    
    def test_init_without_schema(self):
        """Test initialization without schema."""
        validator = DocumentValidator()
        assert validator.schema is None
        assert hasattr(validator, 'validation_rules')
        assert len(validator.validation_rules) > 0
    
    def test_init_with_schema(self):
        """Test initialization with schema."""
        validator = DocumentValidator(schema=Requerimiento)
        assert validator.schema == Requerimiento
    
    def test_validate_with_schema_success(self, sample_legal_document):
        """Test successful validation with schema."""
        validator = DocumentValidator(schema=Requerimiento)
        
        is_valid, validated_data, errors = validator.validate(sample_legal_document)
        
        assert is_valid is True
        assert isinstance(validated_data, dict)
        assert len(errors) == 0
        assert validated_data['fecha'] == sample_legal_document['fecha']
        assert validated_data['autoridadEmisora'] == sample_legal_document['autoridadEmisora']
    
    def test_validate_with_schema_failure(self):
        """Test validation failure with schema."""
        validator = DocumentValidator(schema=Requerimiento)
        
        invalid_data = {
            'fecha': '2024-13-45',  # Invalid date
            'autoridadEmisora': 123,  # Wrong type
        }
        
        is_valid, validated_data, errors = validator.validate(invalid_data)
        
        assert is_valid is False
        assert len(errors) > 0
        assert any('fecha' in error for error in errors)
    
    def test_validate_without_schema(self):
        """Test validation without schema (basic validation)."""
        validator = DocumentValidator()
        
        data = {
            'fecha': '2024-01-15',
            'email': 'test@example.com',
            'phone': '+52-55-1234-5678'
        }
        
        is_valid, validated_data, errors = validator.validate(data)
        
        assert is_valid is True
        assert validated_data == data
        assert len(errors) == 0
    
    def test_validate_required_fields_success(self):
        """Test required fields validation success."""
        validator = DocumentValidator()
        
        data = {
            'fecha': '2024-01-15',
            'autoridadEmisora': 'CONDUSEF',
            'optional_field': 'value'
        }
        
        is_valid, missing = validator.validate_required_fields(
            data, ['fecha', 'autoridadEmisora']
        )
        
        assert is_valid is True
        assert len(missing) == 0
    
    def test_validate_required_fields_missing(self):
        """Test required fields validation with missing fields."""
        validator = DocumentValidator()
        
        data = {
            'fecha': '2024-01-15',
            'optional_field': 'value'
        }
        
        is_valid, missing = validator.validate_required_fields(
            data, ['fecha', 'autoridadEmisora', 'expediente']
        )
        
        assert is_valid is False
        assert len(missing) == 2
        assert any('autoridadEmisora' in error for error in missing)
        assert any('expediente' in error for error in missing)
    
    def test_validate_required_fields_empty_values(self):
        """Test required fields with empty/unknown values."""
        validator = DocumentValidator()
        
        data = {
            'fecha': '',
            'autoridadEmisora': 'unknown',
            'expediente': None
        }
        
        is_valid, missing = validator.validate_required_fields(
            data, ['fecha', 'autoridadEmisora', 'expediente']
        )
        
        assert is_valid is False
        assert len(missing) == 3
    
    def test_validate_date_formats(self):
        """Test date validation with different formats."""
        validator = DocumentValidator()
        
        valid_dates = [
            '2024-01-15',
            '15/01/2024',
            '15-01-2024',
            'unknown'  # Allow unknown
        ]
        
        for date_value in valid_dates:
            assert validator._validate_date(date_value) is True
        
        invalid_dates = [
            '2024-13-45',
            '32/01/2024',
            'not-a-date',
            '2024/1/1'  # Wrong format
        ]
        
        for date_value in invalid_dates:
            assert validator._validate_date(date_value) is False
    
    def test_validate_email_formats(self):
        """Test email validation."""
        validator = DocumentValidator()
        
        valid_emails = [
            'test@example.com',
            'user.name@domain.co.uk',
            'firstname+lastname@example.org',
            'unknown'  # Allow unknown
        ]
        
        for email in valid_emails:
            assert validator._validate_email(email) is True
        
        invalid_emails = [
            'invalid-email',
            '@domain.com',
            'user@',
            'user@domain',
            'user name@domain.com'  # Space in email
        ]
        
        for email in invalid_emails:
            assert validator._validate_email(email) is False
    
    def test_validate_phone_formats(self):
        """Test phone validation."""
        validator = DocumentValidator()
        \n        valid_phones = [\n            '+52-55-1234-5678',\n            '5555551234',\n            '+1 (555) 555-1234',\n            '01 55 1234 5678',\n            'unknown'  # Allow unknown\n        ]\n        \n        for phone in valid_phones:\n            assert validator._validate_phone(phone) is True\n        \n        invalid_phones = [\n            '123',  # Too short\n            'abcd-efgh',  # Not numeric\n            '+++invalid',\n        ]\n        \n        for phone in invalid_phones:\n            assert validator._validate_phone(phone) is False\n    \n    def test_validate_url_formats(self):\n        \"\"\"Test URL validation.\"\"\"\n        validator = DocumentValidator()\n        \n        valid_urls = [\n            'https://example.com',\n            'http://www.example.com/path',\n            'https://subdomain.example.com:8080/path?param=value',\n            'unknown'  # Allow unknown\n        ]\n        \n        for url in valid_urls:\n            assert validator._validate_url(url) is True\n        \n        invalid_urls = [\n            'not-a-url',\n            'ftp://example.com',  # Wrong protocol\n            'https://',  # Incomplete\n            'example.com'  # Missing protocol\n        ]\n        \n        for url in invalid_urls:\n            assert validator._validate_url(url) is False\n    \n    def test_validate_numeric_values(self):\n        \"\"\"Test numeric validation.\"\"\"\n        validator = DocumentValidator()\n        \n        valid_numbers = [\n            '123',\n            '123.45',\n            '-123',\n            '0',\n            123,\n            123.45,\n            -123,\n            0\n        ]\n        \n        for number in valid_numbers:\n            assert validator._validate_numeric(number) is True\n        \n        invalid_numbers = [\n            'not-a-number',\n            '123abc',\n            '',\n            None\n        ]\n        \n        for number in invalid_numbers:\n            assert validator._validate_numeric(number) is False\n    \n    def test_validate_required_values(self):\n        \"\"\"Test required value validation.\"\"\"\n        validator = DocumentValidator()\n        \n        valid_required = [\n            'some value',\n            123,\n            [],  # Empty list is still a value\n            False  # False is still a value\n        ]\n        \n        for value in valid_required:\n            assert validator._validate_required(value) is True\n        \n        invalid_required = [\n            None,\n            '',\n            'unknown'\n        ]\n        \n        for value in invalid_required:\n            assert validator._validate_required(value) is False\n    \n    def test_validate_length(self):\n        \"\"\"Test length validation.\"\"\"\n        validator = DocumentValidator()\n        \n        # Test with default limits (0-1000)\n        assert validator._validate_length('short') is True\n        assert validator._validate_length('a' * 500) is True\n        assert validator._validate_length('a' * 1000) is True\n        assert validator._validate_length('a' * 1001) is False\n        \n        # Test with custom limits\n        assert validator._validate_length('abc', min_len=5, max_len=10) is False\n        assert validator._validate_length('abcdefgh', min_len=5, max_len=10) is True\n        assert validator._validate_length('a' * 15, min_len=5, max_len=10) is False\n    \n    def test_add_custom_rule(self):\n        \"\"\"Test adding custom validation rules.\"\"\"\n        validator = DocumentValidator()\n        \n        # Add custom rule\n        def validate_mexican_rfc(value):\n            \"\"\"Validate Mexican RFC format.\"\"\"\n            if not value or value == 'unknown':\n                return True\n            # Simplified RFC validation\n            return len(str(value)) in [12, 13] and str(value).isalnum()\n        \n        validator.add_custom_rule('mexican_rfc', validate_mexican_rfc)\n        \n        # Test custom rule\n        assert 'mexican_rfc' in validator.validation_rules\n        assert validator.validation_rules['mexican_rfc']('XAXX010101000') is True\n        assert validator.validation_rules['mexican_rfc']('invalid') is False\n    \n    def test_validate_batch_success(self, create_test_documents):\n        \"\"\"Test batch validation with all valid documents.\"\"\"\n        validator = DocumentValidator(schema=Requerimiento)\n        \n        documents = [\n            create_test_documents(expediente='EXP-001'),\n            create_test_documents(expediente='EXP-002'),\n            create_test_documents(expediente='EXP-003')\n        ]\n        \n        valid, invalid, stats = validator.validate_batch(documents)\n        \n        assert len(valid) == 3\n        assert len(invalid) == 0\n        assert stats['total'] == 3\n        assert stats['valid'] == 3\n        assert stats['invalid'] == 0\n        assert stats['success_rate'] == 1.0\n    \n    def test_validate_batch_mixed(self, create_test_documents):\n        \"\"\"Test batch validation with mixed results.\"\"\"\n        validator = DocumentValidator(schema=Requerimiento)\n        \n        documents = [\n            create_test_documents(expediente='EXP-001'),  # Valid\n            {'fecha': 'invalid-date', 'autoridadEmisora': 123},  # Invalid\n            create_test_documents(expediente='EXP-003'),  # Valid\n            {'incomplete': 'document'}  # Invalid\n        ]\n        \n        valid, invalid, stats = validator.validate_batch(documents)\n        \n        assert len(valid) == 2\n        assert len(invalid) == 2\n        assert stats['total'] == 4\n        assert stats['valid'] == 2\n        assert stats['invalid'] == 2\n        assert stats['success_rate'] == 0.5\n        \n        # Check invalid documents have error information\n        for invalid_doc in invalid:\n            assert 'data' in invalid_doc\n            assert 'errors' in invalid_doc\n            assert len(invalid_doc['errors']) > 0\n    \n    def test_validate_batch_empty(self):\n        \"\"\"Test batch validation with empty list.\"\"\"\n        validator = DocumentValidator()\n        \n        valid, invalid, stats = validator.validate_batch([])\n        \n        assert len(valid) == 0\n        assert len(invalid) == 0\n        assert stats['total'] == 0\n        assert stats['success_rate'] == 0\n\n\nclass TestDocumentValidatorEdgeCases:\n    \"\"\"Test edge cases and error conditions.\"\"\"\n    \n    def test_validate_with_none_data(self):\n        \"\"\"Test validation with None data.\"\"\"\n        validator = DocumentValidator()\n        \n        is_valid, validated_data, errors = validator.validate(None)\n        \n        # Should handle None gracefully\n        assert is_valid is False\n        assert len(errors) > 0\n    \n    def test_validate_with_non_dict_data(self):\n        \"\"\"Test validation with non-dictionary data.\"\"\"\n        validator = DocumentValidator(schema=Requerimiento)\n        \n        non_dict_data = \"This is not a dictionary\"\n        \n        is_valid, validated_data, errors = validator.validate(non_dict_data)\n        \n        assert is_valid is False\n        assert len(errors) > 0\n    \n    def test_field_validation_with_nested_keys(self):\n        \"\"\"Test field validation with nested key names.\"\"\"\n        validator = DocumentValidator()\n        \n        data = {\n            'contact_email': 'invalid-email',\n            'birth_date': 'invalid-date',\n            'phone_number': '123'  # Too short\n        }\n        \n        is_valid, validated_data, errors = validator.validate(data)\n        \n        # Should detect issues in nested key names\n        assert is_valid is False\n        assert len(errors) > 0\n    \n    def test_validate_with_circular_reference_data(self):\n        \"\"\"Test validation with data that might cause issues.\"\"\"\n        validator = DocumentValidator()\n        \n        # Create data with potential issues\n        data = {\n            'normal_field': 'value',\n            'number_field': float('inf'),  # Infinity\n            'another_field': float('nan')  # NaN\n        }\n        \n        # Should handle special float values\n        is_valid, validated_data, errors = validator.validate(data)\n        \n        # Basic validation should still work\n        assert isinstance(validated_data, dict)\n        assert 'normal_field' in validated_data\n\n\nclass TestDocumentValidatorPerformance:\n    \"\"\"Performance-related tests.\"\"\"\n    \n    @pytest.mark.slow\n    def test_validate_large_batch(self, create_test_documents):\n        \"\"\"Test validation performance with large batch.\"\"\"\n        validator = DocumentValidator(schema=Requerimiento)\n        \n        # Create large batch of documents\n        documents = []\n        for i in range(1000):\n            doc = create_test_documents(\n                expediente=f'EXP-{i:04d}',\n                fecha=f'2024-{(i%12)+1:02d}-{(i%28)+1:02d}'\n            )\n            documents.append(doc)\n        \n        valid, invalid, stats = validator.validate_batch(documents)\n        \n        assert len(valid) == 1000\n        assert len(invalid) == 0\n        assert stats['success_rate'] == 1.0\n    \n    def test_validate_complex_nested_documents(self):\n        \"\"\"Test validation performance with complex nested data.\"\"\"\n        validator = DocumentValidator(schema=Requerimiento)\n        \n        # Create complex nested document\n        complex_doc = {\n            'fecha': '2024-01-15',\n            'autoridadEmisora': 'CONDUSEF',\n            'expediente': 'EXP-COMPLEX-001',\n            'partes': [f'Parte {i}' for i in range(100)],  # Large list\n            'detalle': {\n                'descripcion': 'Complex case with multiple elements',\n                'monto': 1000000.50,\n                'moneda': 'MXN',\n                'elementos': [{\n                    'id': i,\n                    'descripcion': f'Element {i}',\n                    'valor': i * 100.0\n                } for i in range(50)]  # Nested complex data\n            }\n        }\n        \n        is_valid, validated_data, errors = validator.validate(complex_doc)\n        \n        assert is_valid is True\n        assert len(validated_data['partes']) == 100\n        assert len(validated_data['detalle']['elementos']) == 50