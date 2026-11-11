"""
Unit tests for ModelLoader module.
"""

import pytest
from unittest.mock import Mock, MagicMock, patch
import torch
from src.modules.model_loader import ModelLoader


class TestModelLoader:
    """Test ModelLoader functionality."""
    
    def test_init(self):
        """Test ModelLoader initialization."""
        loader = ModelLoader()
        assert loader._model_cache == {}
        assert loader._processor_cache == {}
    
    @patch('src.modules.model_loader.AutoModelForImageTextToText')
    def test_load_model_success(self, mock_model_class):
        """Test successful model loading."""
        # Setup mock
        mock_model = Mock()
        mock_model.to.return_value = mock_model
        mock_model.eval.return_value = mock_model
        mock_model_class.from_pretrained.return_value = mock_model
        
        loader = ModelLoader()
        
        # Load model
        result = loader.load_model(
            'test-model',
            'AutoModelForImageTextToText',
            device='cpu',
            dtype=torch.float32
        )
        
        # Verify
        assert result == mock_model
        mock_model_class.from_pretrained.assert_called_once_with(
            'test-model',
            torch_dtype=torch.float32
        )
        mock_model.to.assert_called_once_with('cpu')
        mock_model.eval.assert_called_once()
    
    @patch('src.modules.model_loader.AutoModelForImageTextToText')
    def test_load_model_caching(self, mock_model_class):
        """Test model caching functionality."""
        mock_model = Mock()
        mock_model.to.return_value = mock_model
        mock_model.eval.return_value = mock_model
        mock_model_class.from_pretrained.return_value = mock_model
        
        loader = ModelLoader()
        
        # Load same model twice
        result1 = loader.load_model('test-model', 'AutoModelForImageTextToText', device='cpu')
        result2 = loader.load_model('test-model', 'AutoModelForImageTextToText', device='cpu')
        
        # Should be same instance (cached)
        assert result1 is result2
        
        # from_pretrained should only be called once due to caching
        assert mock_model_class.from_pretrained.call_count == 1
    
    def test_load_model_unknown_class(self):
        """Test loading with unknown model class."""
        loader = ModelLoader()
        
        with pytest.raises(ValueError, match="Unknown model class"):
            loader.load_model('test-model', 'UnknownModelClass')
    
    @patch('src.modules.model_loader.AutoProcessor')
    def test_load_processor_success(self, mock_processor_class):
        """Test successful processor loading."""
        mock_processor = Mock()
        mock_processor_class.from_pretrained.return_value = mock_processor
        
        loader = ModelLoader()
        result = loader.load_processor('test-model')
        
        assert result == mock_processor
        mock_processor_class.from_pretrained.assert_called_once_with('test-model')
    
    @patch('src.modules.model_loader.AutoProcessor')
    def test_load_processor_caching(self, mock_processor_class):
        """Test processor caching."""
        mock_processor = Mock()
        mock_processor_class.from_pretrained.return_value = mock_processor
        
        loader = ModelLoader()
        
        # Load same processor twice
        result1 = loader.load_processor('test-model')
        result2 = loader.load_processor('test-model')
        
        # Should be same instance
        assert result1 is result2
        assert mock_processor_class.from_pretrained.call_count == 1
    
    @patch('src.modules.model_loader.AutoModelForImageTextToText')
    def test_clear_cache_specific_model(self, mock_model_class):
        """Test clearing cache for specific model."""
        mock_model = Mock()
        mock_model.to.return_value = mock_model
        mock_model.eval.return_value = mock_model
        mock_model_class.from_pretrained.return_value = mock_model
        
        loader = ModelLoader()
        
        # Load models
        loader.load_model('model1', 'AutoModelForImageTextToText', device='cpu')
        loader.load_model('model2', 'AutoModelForImageTextToText', device='cpu')
        
        # Clear specific model
        loader.clear_cache('model1')
        
        # Check cache state
        cached_models = loader.get_cached_models()
        assert 'model1' not in str(cached_models['models'])
        assert any('model2' in model_key for model_key in cached_models['models'])
    
    def test_clear_cache_all(self):
        """Test clearing all cache."""
        loader = ModelLoader()
        
        # Add some dummy cache entries
        loader._model_cache['test1'] = Mock()
        loader._processor_cache['test1'] = Mock()
        
        # Clear all
        loader.clear_cache()
        
        # Verify empty
        assert loader._model_cache == {}
        assert loader._processor_cache == {}
    
    def test_get_cached_models(self):
        """Test getting cached models info."""
        loader = ModelLoader()
        
        # Add some mock cache entries
        loader._model_cache['model1_cpu_torch.float32'] = Mock()
        loader._model_cache['model2_cuda_torch.float16'] = Mock()
        loader._processor_cache['model1'] = Mock()
        
        result = loader.get_cached_models()
        
        assert 'models' in result
        assert 'processors' in result
        assert 'cache_size' in result
        assert result['cache_size'] == 3
        assert len(result['models']) == 2
        assert len(result['processors']) == 1
    
    @patch('src.modules.model_loader.AutoModelForImageTextToText')
    def test_load_model_with_additional_kwargs(self, mock_model_class):
        """Test loading model with additional keyword arguments."""
        mock_model = Mock()
        mock_model.to.return_value = mock_model
        mock_model.eval.return_value = mock_model
        mock_model_class.from_pretrained.return_value = mock_model
        
        loader = ModelLoader()
        
        # Load with additional kwargs
        result = loader.load_model(
            'test-model',
            'AutoModelForImageTextToText',
            device='cpu',
            dtype=torch.float32,
            _attn_implementation='flash_attention_2',
            trust_remote_code=True
        )
        
        # Verify kwargs passed correctly
        mock_model_class.from_pretrained.assert_called_once_with(
            'test-model',
            torch_dtype=torch.float32,
            _attn_implementation='flash_attention_2',
            trust_remote_code=True
        )
    
    @patch('src.modules.model_loader.AutoModelForCausalLM')
    def test_load_different_model_types(self, mock_model_class):
        """Test loading different model types."""
        mock_model = Mock()
        mock_model.to.return_value = mock_model
        mock_model.eval.return_value = mock_model
        mock_model_class.from_pretrained.return_value = mock_model
        
        loader = ModelLoader()
        
        result = loader.load_model(
            'test-model',
            'AutoModelForCausalLM',
            device='cpu'
        )
        
        assert result == mock_model
        mock_model_class.from_pretrained.assert_called_once()


class TestModelLoaderIntegration:
    """Integration tests for ModelLoader with mocked dependencies."""
    
    @pytest.mark.mock
    def test_model_lifecycle(self):
        """Test complete model lifecycle."""
        loader = ModelLoader()
        
        # Initially empty
        assert loader.get_cached_models()['cache_size'] == 0
        
        # Mock loading
        with patch('src.modules.model_loader.AutoModelForImageTextToText') as mock_model_class:
            mock_model = Mock()
            mock_model.to.return_value = mock_model
            mock_model.eval.return_value = mock_model
            mock_model_class.from_pretrained.return_value = mock_model
            
            # Load model
            model = loader.load_model('test-model', 'AutoModelForImageTextToText')
            
            # Should be cached
            assert loader.get_cached_models()['cache_size'] == 1
            
            # Load again (should use cache)
            model2 = loader.load_model('test-model', 'AutoModelForImageTextToText')
            assert model is model2
            
            # Clear and verify
            loader.clear_cache()
            assert loader.get_cached_models()['cache_size'] == 0
    
    @pytest.mark.mock
    def test_error_handling(self):
        """Test error handling in model loading."""
        loader = ModelLoader()
        
        with patch('src.modules.model_loader.AutoModelForImageTextToText') as mock_model_class:
            # Simulate loading error
            mock_model_class.from_pretrained.side_effect = RuntimeError("Model loading failed")
            
            with pytest.raises(RuntimeError, match="Model loading failed"):
                loader.load_model('failing-model', 'AutoModelForImageTextToText')
    
    @pytest.mark.mock
    def test_memory_management(self):
        """Test memory management aspects."""
        loader = ModelLoader()
        
        with patch('src.modules.model_loader.AutoModelForImageTextToText') as mock_model_class:
            mock_model = Mock()
            mock_model.to.return_value = mock_model
            mock_model.eval.return_value = mock_model
            mock_model_class.from_pretrained.return_value = mock_model
            
            # Load multiple models
            models = []
            for i in range(3):
                model = loader.load_model(
                    f'model-{i}',
                    'AutoModelForImageTextToText',
                    device='cpu'
                )
                models.append(model)
            
            # Verify all cached
            assert loader.get_cached_models()['cache_size'] == 3
            
            # Clear specific models
            loader.clear_cache('model-1')
            assert loader.get_cached_models()['cache_size'] == 2
            
            # Clear all
            loader.clear_cache()
            assert loader.get_cached_models()['cache_size'] == 0