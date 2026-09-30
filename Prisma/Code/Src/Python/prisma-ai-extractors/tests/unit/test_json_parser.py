"""
Unit tests for JsonParser module.
"""

import pytest
import json
from src.modules.json_parser import JsonParser


class TestJsonParser:
    """Test JsonParser functionality."""
    
    def test_init(self):
        """Test JsonParser initialization."""
        parser = JsonParser()
        assert hasattr(parser, 'json_patterns')
        assert len(parser.json_patterns) > 0
    
    def test_parse_valid_json(self, sample_json_responses):
        """Test parsing valid JSON."""
        parser = JsonParser()
        
        result = parser.parse(sample_json_responses['valid'])
        
        assert isinstance(result, dict)
        assert result['fecha'] == "2024-01-15"
        assert result['autoridadEmisora'] == "CONDUSEF"
        assert result['expediente'] == "EXP-001"
    
    def test_parse_malformed_json(self, sample_json_responses):
        """Test parsing malformed JSON with repair."""
        parser = JsonParser()
        
        result = parser.parse(sample_json_responses['malformed'])
        
        # Should still extract some data or provide fallback
        assert isinstance(result, dict)
        # May contain raw_output if parsing completely fails
        if 'raw_output' in result:
            assert 'parse_error' in result
    
    def test_parse_markdown_json(self, sample_json_responses):
        """Test parsing JSON in markdown code blocks."""
        parser = JsonParser()
        
        result = parser.parse(sample_json_responses['with_markdown'])
        
        assert isinstance(result, dict)
        assert result['fecha'] == "2024-01-15"
        assert result['autoridadEmisora'] == "CONDUSEF"
    
    def test_parse_json_with_extra_text(self, sample_json_responses):
        """Test parsing JSON embedded in text."""
        parser = JsonParser()
        
        result = parser.parse(sample_json_responses['with_extra_text'])
        
        assert isinstance(result, dict)
        assert result['fecha'] == "2024-01-15"
        assert result['autoridadEmisora'] == "CONDUSEF"
    
    def test_parse_empty_input(self, sample_json_responses):
        """Test parsing empty input."""
        parser = JsonParser()
        
        result = parser.parse(sample_json_responses['empty'])
        
        # Should return fallback structure
        assert isinstance(result, dict)
        assert 'raw_output' in result
        assert 'parse_error' in result
    
    def test_parse_invalid_input(self, sample_json_responses):
        """Test parsing completely invalid input."""
        parser = JsonParser()
        
        result = parser.parse(sample_json_responses['invalid'])
        
        # Should return fallback structure
        assert isinstance(result, dict)
        assert result['raw_output'] == sample_json_responses['invalid']
        assert 'parse_error' in result
    
    def test_parse_complex_json(self, sample_json_responses):
        """Test parsing complex nested JSON."""
        parser = JsonParser()
        
        result = parser.parse(sample_json_responses['complex'])
        
        assert isinstance(result, dict)
        assert result['fecha'] == "2024-01-15"
        assert result['autoridadEmisora'] == "COMISIÓN NACIONAL BANCARIA Y DE VALORES"
        assert isinstance(result['partes'], list)
        assert isinstance(result['detalle'], dict)
        assert result['detalle']['monto'] is None
    
    def test_extract_json_simple(self):
        """Test JSON extraction from simple object."""
        parser = JsonParser()
        
        text = '{"key": "value"}'
        extracted = parser.extract_json(text)
        
        assert extracted == '{"key": "value"}'
    
    def test_extract_json_nested(self):
        """Test JSON extraction from nested structure."""
        parser = JsonParser()
        
        text = 'Here is the data: {"outer": {"inner": "value"}} and more text.'
        extracted = parser.extract_json(text)
        
        assert '{"outer": {"inner": "value"}}' in extracted
    
    def test_extract_json_code_block(self):
        """Test JSON extraction from code blocks."""
        parser = JsonParser()
        
        text = '```json\\n{"formatted": "json"}\\n```'
        extracted = parser.extract_json(text)
        
        assert 'formatted' in extracted
        assert 'json' in extracted
    
    def test_extract_json_no_match(self):
        """Test JSON extraction when no JSON found."""
        parser = JsonParser()
        
        text = 'This is just plain text with no JSON.'
        extracted = parser.extract_json(text)
        
        assert extracted is None
    
    def test_clean_json_text_markdown(self):
        """Test cleaning JSON text with markdown."""
        parser = JsonParser()
        
        text = '```json\\n{"clean": "me"}\\n```'
        cleaned = parser.clean_json_text(text)
        
        assert '```' not in cleaned
        assert 'clean' in cleaned
        assert 'me' in cleaned
    
    def test_clean_json_text_single_quotes(self):
        """Test cleaning JSON with single quotes."""
        parser = JsonParser()
        
        text = \"{'single': 'quotes'}\"\n        cleaned = parser.clean_json_text(text)\n        \n        assert '\"single\"' in cleaned\n        assert '\"quotes\"' in cleaned\n        assert \"'\" not in cleaned\n    \n    def test_clean_json_text_unquoted_keys(self):\n        \"\"\"Test cleaning JSON with unquoted keys.\"\"\"\n        parser = JsonParser()\n        \n        text = '{key: \"value\", another: \"test\"}'\n        cleaned = parser.clean_json_text(text)\n        \n        assert '\"key\"' in cleaned\n        assert '\"another\"' in cleaned\n        assert '\"value\"' in cleaned\n        assert '\"test\"' in cleaned\n    \n    def test_clean_json_text_trailing_commas(self):\n        \"\"\"Test cleaning JSON with trailing commas.\"\"\"\n        parser = JsonParser()\n        \n        text = '{\"key\": \"value\", \"another\": \"test\",}'\n        cleaned = parser.clean_json_text(text)\n        \n        # Should remove trailing comma\n        assert cleaned.endswith('}')\n        assert ',}' not in cleaned\n    \n    def test_parse_flexible_repair_strategies(self):\n        \"\"\"Test flexible parsing with different repair strategies.\"\"\"\n        parser = JsonParser()\n        \n        # Test different broken JSON formats\n        broken_formats = [\n            '{\"key\": \"value\"',  # Missing closing brace\n            '\"key\": \"value\"}',  # Missing opening brace\n            '{key: value}',      # Unquoted keys and values\n            '{\"key\": value,}',   # Trailing comma and unquoted value\n        ]\n        \n        for broken_json in broken_formats:\n            result = parser.parse_flexible(broken_json)\n            \n            # Should return some result (may be fallback)\n            assert isinstance(result, dict)\n            \n            # Either successfully parsed or has error info\n            if 'parse_error' not in result:\n                assert 'key' in result or len(result) > 0\n    \n    def test_fix_quotes(self):\n        \"\"\"Test quote fixing functionality.\"\"\"\n        parser = JsonParser()\n        \n        text = \"{'single': 'quotes', unquoted: 'mixed'}\"\n        fixed = parser._fix_quotes(text)\n        \n        # Should convert single quotes to double quotes\n        assert '\"single\"' in fixed\n        assert '\"quotes\"' in fixed\n        assert '\"unquoted\"' in fixed\n        assert '\"mixed\"' in fixed\n    \n    def test_fix_structure(self):\n        \"\"\"Test structure fixing functionality.\"\"\"\n        parser = JsonParser()\n        \n        # Test missing braces\n        text = '\"key\": \"value\"'\n        fixed = parser._fix_structure(text)\n        \n        assert fixed.startswith('{')\n        assert fixed.endswith('}')\n    \n    def test_extract_key_values(self):\n        \"\"\"Test key-value extraction from unstructured text.\"\"\"\n        parser = JsonParser()\n        \n        text = 'key1: \"value1\", key2 = \"value2\", \"key3\": value3'\n        extracted = parser._extract_key_values(text)\n        \n        # Should create valid JSON string\n        result = json.loads(extracted)\n        assert isinstance(result, dict)\n        assert len(result) > 0\n    \n    def test_fallback_parse(self):\n        \"\"\"Test fallback parsing for completely broken input.\"\"\"\n        parser = JsonParser()\n        \n        text = 'This is completely invalid and cannot be parsed as JSON'\n        result = parser._fallback_parse(text)\n        \n        assert isinstance(result, dict)\n        assert result['raw_output'] == text\n        assert 'parse_error' in result\n    \n    def test_validate_json_valid_string(self):\n        \"\"\"Test JSON validation with valid string.\"\"\"\n        parser = JsonParser()\n        \n        valid_json = '{\"key\": \"value\"}'\n        is_valid, error = parser.validate_json(valid_json)\n        \n        assert is_valid is True\n        assert error is None\n    \n    def test_validate_json_invalid_string(self):\n        \"\"\"Test JSON validation with invalid string.\"\"\"\n        parser = JsonParser()\n        \n        invalid_json = '{\"key\": \"value\"'\n        is_valid, error = parser.validate_json(invalid_json)\n        \n        assert is_valid is False\n        assert error is not None\n        assert 'Expecting' in error or 'Unterminated' in error\n    \n    def test_validate_json_dict(self):\n        \"\"\"Test JSON validation with dictionary.\"\"\"\n        parser = JsonParser()\n        \n        valid_dict = {'key': 'value', 'number': 42}\n        is_valid, error = parser.validate_json(valid_dict)\n        \n        assert is_valid is True\n        assert error is None\n    \n    def test_validate_json_invalid_type(self):\n        \"\"\"Test JSON validation with invalid type.\"\"\"\n        parser = JsonParser()\n        \n        invalid_data = [1, 2, 3]  # Lists are not supported in our validation\n        is_valid, error = parser.validate_json(invalid_data)\n        \n        assert is_valid is False\n        assert 'Invalid type' in error\n\n\nclass TestJsonParserEdgeCases:\n    \"\"\"Test edge cases and error conditions.\"\"\"\n    \n    def test_very_large_json(self):\n        \"\"\"Test parsing very large JSON.\"\"\"\n        parser = JsonParser()\n        \n        # Create large JSON structure\n        large_data = {f'key_{i}': f'value_{i}' for i in range(1000)}\n        large_json = json.dumps(large_data)\n        \n        result = parser.parse(large_json)\n        \n        assert isinstance(result, dict)\n        assert len(result) == 1000\n        assert result['key_0'] == 'value_0'\n        assert result['key_999'] == 'value_999'\n    \n    def test_deeply_nested_json(self):\n        \"\"\"Test parsing deeply nested JSON.\"\"\"\n        parser = JsonParser()\n        \n        # Create nested structure\n        nested = {'level_0': {}}\n        current = nested['level_0']\n        for i in range(1, 10):\n            current[f'level_{i}'] = {}\n            current = current[f'level_{i}']\n        current['deep_value'] = 'found'\n        \n        nested_json = json.dumps(nested)\n        result = parser.parse(nested_json)\n        \n        assert isinstance(result, dict)\n        # Navigate to deep value\n        current = result['level_0']\n        for i in range(1, 10):\n            current = current[f'level_{i}']\n        assert current['deep_value'] == 'found'\n    \n    def test_json_with_unicode(self):\n        \"\"\"Test parsing JSON with unicode characters.\"\"\"\n        parser = JsonParser()\n        \n        unicode_json = '{\"spanish\": \"Comisión Nacional Bancaria\", \"chinese\": \"中文\", \"emoji\": \"📄\"}'\n        result = parser.parse(unicode_json)\n        \n        assert isinstance(result, dict)\n        assert result['spanish'] == \"Comisión Nacional Bancaria\"\n        assert result['chinese'] == \"中文\"\n        assert result['emoji'] == \"📄\"\n    \n    def test_json_with_special_characters(self):\n        \"\"\"Test parsing JSON with special characters.\"\"\"\n        parser = JsonParser()\n        \n        special_json = '{\"newline\": \"line1\\\\nline2\", \"quote\": \"He said \\\\\"hello\\\\\"\"}'\n        result = parser.parse(special_json)\n        \n        assert isinstance(result, dict)\n        assert 'line1' in result['newline']\n        assert 'hello' in result['quote']\n    \n    def test_multiple_json_objects_in_text(self):\n        \"\"\"Test extracting from text with multiple JSON objects.\"\"\"\n        parser = JsonParser()\n        \n        text = 'First: {\"a\": 1} and second: {\"b\": 2, \"c\": {\"d\": 3}}'\n        extracted = parser.extract_json(text)\n        \n        # Should extract the longer/more complex JSON\n        assert extracted is not None\n        if '{\"b\": 2' in extracted:\n            # Got the complex one\n            result = json.loads(extracted)\n            assert 'b' in result\n            assert 'c' in result\n        else:\n            # Got the simple one\n            result = json.loads(extracted)\n            assert 'a' in result\n\n\nclass TestJsonParserPerformance:\n    \"\"\"Performance-related tests.\"\"\"\n    \n    @pytest.mark.slow\n    def test_parse_many_documents(self):\n        \"\"\"Test parsing performance with many documents.\"\"\"\n        parser = JsonParser()\n        \n        # Create many JSON documents\n        documents = []\n        for i in range(100):\n            doc = {\n                'id': i,\n                'fecha': f'2024-01-{i%30+1:02d}',\n                'autoridad': f'AUTORIDAD_{i}',\n                'data': {'nested': f'value_{i}'}\n            }\n            documents.append(json.dumps(doc))\n        \n        # Parse all documents\n        results = []\n        for doc_json in documents:\n            result = parser.parse(doc_json)\n            results.append(result)\n        \n        # Verify all parsed correctly\n        assert len(results) == 100\n        for i, result in enumerate(results):\n            assert result['id'] == i\n            assert result['autoridad'] == f'AUTORIDAD_{i}'\n    \n    def test_parse_malformed_batch(self):\n        \"\"\"Test performance with many malformed documents.\"\"\"\n        parser = JsonParser()\n        \n        # Create malformed JSON documents\n        malformed_docs = [\n            '{\"key\": \"value\"',  # Missing brace\n            'key: \"value\"}',   # Missing brace and quotes\n            '{\"key\": value}',   # Unquoted value\n            '{\"key\": \"value\", \"extra\": }',  # Missing value\n            'Not JSON at all',\n        ] * 20  # Repeat to get 100 docs\n        \n        results = []\n        for doc in malformed_docs:\n            result = parser.parse(doc)\n            results.append(result)\n        \n        # All should return some result (even if fallback)\n        assert len(results) == 100\n        for result in results:\n            assert isinstance(result, dict)