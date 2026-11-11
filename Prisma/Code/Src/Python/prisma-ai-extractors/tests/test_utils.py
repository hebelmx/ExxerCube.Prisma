"""
Unit tests for utility functions.
"""

import pytest
import json
import torch
from unittest.mock import patch, MagicMock
from PIL import Image
import numpy as np
import tempfile
from pathlib import Path

from src.utils import (
    get_optimal_device_config,
    is_cuda_supported,
    parse_json_flexible,
    extract_json_from_text,
    load_image,
    preprocess_image
)
from src.utils.json_utils import fix_common_json_errors, validate_json_schema
from src.utils.image_utils import resize_image, get_image_info, is_valid_image


class TestDeviceUtils:
    """Test device utility functions."""
    
    def test_is_cuda_supported_no_cuda(self):
        """Test CUDA detection when not available."""
        with patch('torch.cuda.is_available', return_value=False):
            assert is_cuda_supported() is False
    
    def test_is_cuda_supported_with_cuda(self):
        """Test CUDA detection when available."""
        with patch('torch.cuda.is_available', return_value=True):
            with patch('torch._C._cuda_getDeviceCount', return_value=1):
                with patch('torch.cuda.get_device_capability', return_value=(7, 5)):
                    assert is_cuda_supported() is True
    
    def test_get_optimal_device_config_cpu(self):
        """Test device config for CPU-only system."""
        with patch('src.utils.device_utils.is_cuda_supported', return_value=False):
            config = get_optimal_device_config()
            
            assert config['cuda_available'] is False
            assert config['device'] == torch.device('cpu')
            assert config['dtype'] == torch.float32
            assert config['attn_impl'] is None
    
    def test_get_optimal_device_config_ampere_gpu(self):
        """Test device config for Ampere GPU."""
        with patch('src.utils.device_utils.is_cuda_supported', return_value=True):
            with patch('torch.cuda.get_device_name', return_value='RTX 3090'):
                with patch('torch.cuda.get_device_capability', return_value=(8, 6)):
                    config = get_optimal_device_config()
                    
                    assert config['cuda_available'] is True
                    assert config['device'] == torch.device('cuda')
                    assert config['dtype'] == torch.bfloat16
                    assert config['gpu_name'] == 'RTX 3090'


class TestJsonUtils:
    """Test JSON utility functions."""
    
    def test_parse_json_flexible_valid(self):
        """Test parsing valid JSON."""
        valid_json = '{"key": "value", "number": 42}'
        result = parse_json_flexible(valid_json)
        
        assert result['key'] == 'value'
        assert result['number'] == 42
    
    def test_parse_json_flexible_with_text(self):
        """Test extracting JSON from text."""
        text = 'Here is the result: {"data": "extracted"} Done.'
        result = parse_json_flexible(text)
        
        assert result['data'] == 'extracted'
    
    def test_parse_json_flexible_single_quotes(self):
        """Test parsing JSON with single quotes."""
        text = "{'key': 'value'}"
        result = parse_json_flexible(text)
        
        assert result['key'] == 'value'
    
    def test_extract_json_from_text(self):
        """Test JSON extraction from text."""
        text = 'Some text {"nested": {"key": "value"}} more text'
        json_str = extract_json_from_text(text)
        
        assert json_str == '{"nested": {"key": "value"}}'
        parsed = json.loads(json_str)
        assert parsed['nested']['key'] == 'value'
    
    def test_extract_json_from_text_no_json(self):
        """Test extraction when no JSON present."""
        text = 'No JSON here'
        result = extract_json_from_text(text)
        
        assert result is None
    
    def test_fix_common_json_errors(self):
        """Test fixing common JSON errors."""
        # Python literals
        text = '{"value": None, "flag": True, "other": False}'
        fixed = fix_common_json_errors(text)
        assert 'null' in fixed
        assert 'true' in fixed
        assert 'false' in fixed
        
        # Trailing commas
        text = '{"a": 1, "b": 2,}'
        fixed = fix_common_json_errors(text)
        parsed = json.loads(fixed)
        assert parsed['a'] == 1
        assert parsed['b'] == 2
    
    def test_validate_json_schema(self):
        """Test JSON schema validation."""
        schema = {
            'name': 'str',
            'age': 'int',
            'scores': 'list'
        }
        
        # Valid data
        valid_data = {
            'name': 'Test',
            'age': 25,
            'scores': [1, 2, 3]
        }
        assert validate_json_schema(valid_data, schema) is True
        
        # Invalid data (wrong type)
        invalid_data = {
            'name': 'Test',
            'age': '25',  # String instead of int
            'scores': [1, 2, 3]
        }
        assert validate_json_schema(invalid_data, schema) is False
        
        # Missing field
        incomplete_data = {
            'name': 'Test',
            'age': 25
        }
        assert validate_json_schema(incomplete_data, schema) is False


class TestImageUtils:
    """Test image utility functions."""
    
    def test_load_image_valid(self):
        """Test loading a valid image."""
        # Create a temporary image
        with tempfile.NamedTemporaryFile(suffix='.png', delete=False) as tmp:
            img = Image.new('RGB', (100, 100), color='red')
            img.save(tmp.name)
            tmp_path = tmp.name
        
        try:
            loaded_img = load_image(tmp_path)
            assert isinstance(loaded_img, Image.Image)
            assert loaded_img.mode == 'RGB'
            assert loaded_img.size == (100, 100)
        finally:
            Path(tmp_path).unlink()
    
    def test_load_image_not_found(self):
        """Test loading non-existent image."""
        with pytest.raises(FileNotFoundError):
            load_image('nonexistent.png')
    
    def test_preprocess_image_resize(self):
        """Test image preprocessing with resize."""
        img = Image.new('RGB', (200, 200), color='blue')
        
        processed = preprocess_image(img, target_size=(100, 100))
        assert processed.size == (100, 100)
    
    def test_preprocess_image_normalize(self):
        """Test image preprocessing with normalization."""
        img = Image.new('RGB', (50, 50), color='white')
        
        processed = preprocess_image(img, normalize=True)
        assert isinstance(processed, np.ndarray)
        assert processed.max() <= 1.0
        assert processed.min() >= 0.0
    
    def test_resize_image_maintain_aspect(self):
        """Test resizing with aspect ratio preservation."""
        img = Image.new('RGB', (200, 100), color='green')
        
        resized = resize_image(img, (100, 100), maintain_aspect=True)
        assert resized.size == (100, 100)
        # Original aspect ratio should be preserved within the new size
    
    def test_resize_image_no_aspect(self):
        """Test resizing without aspect ratio preservation."""
        img = Image.new('RGB', (200, 100), color='green')
        
        resized = resize_image(img, (100, 100), maintain_aspect=False)
        assert resized.size == (100, 100)
    
    def test_get_image_info(self):
        """Test getting image information."""
        with tempfile.NamedTemporaryFile(suffix='.png', delete=False) as tmp:
            img = Image.new('RGB', (150, 200), color='yellow')
            img.save(tmp.name)
            tmp_path = tmp.name
        
        try:
            info = get_image_info(tmp_path)
            assert info['width'] == 150
            assert info['height'] == 200
            assert info['format'] == 'PNG'
            assert info['mode'] == 'RGB'
            assert 'file_size_bytes' in info
        finally:
            Path(tmp_path).unlink()
    
    def test_is_valid_image(self):
        """Test image validation."""
        # Valid image
        with tempfile.NamedTemporaryFile(suffix='.png', delete=False) as tmp:
            img = Image.new('RGB', (10, 10))
            img.save(tmp.name)
            tmp_path = tmp.name
        
        try:
            assert is_valid_image(tmp_path) is True
        finally:
            Path(tmp_path).unlink()
        
        # Invalid file
        with tempfile.NamedTemporaryFile(suffix='.txt', delete=False) as tmp:
            tmp.write(b'Not an image')
            tmp_path = tmp.name
        
        try:
            assert is_valid_image(tmp_path) is False
        finally:
            Path(tmp_path).unlink()