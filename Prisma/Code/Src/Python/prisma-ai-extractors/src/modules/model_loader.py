"""
Model Loader Module - Single Responsibility: Loading and caching ML models.
"""

from typing import Dict, Any, Optional, Protocol
from functools import lru_cache
import torch
from abc import ABC, abstractmethod


class ModelProtocol(Protocol):
    """Protocol for ML models that can be loaded."""
    def to(self, device): ...
    def eval(self): ...


class ModelLoader:
    """
    Responsible ONLY for loading and caching models.
    Single Responsibility: Model lifecycle management.
    """
    
    def __init__(self):
        self._model_cache: Dict[str, Any] = {}
        self._processor_cache: Dict[str, Any] = {}
    
    @lru_cache(maxsize=4)
    def load_model(
        self, 
        model_id: str, 
        model_class: str,
        device: str = 'cpu',
        dtype: Optional[torch.dtype] = None,
        **kwargs
    ) -> ModelProtocol:
        """
        Load a model with caching support.
        
        Args:
            model_id: HuggingFace model ID or path
            model_class: Class name to use for loading (e.g., 'AutoModelForImageTextToText')
            device: Target device
            dtype: Model dtype
            **kwargs: Additional model loading arguments
            
        Returns:
            Loaded model instance
        """
        cache_key = f"{model_id}_{device}_{dtype}"
        
        if cache_key not in self._model_cache:
            # Dynamic import based on model class
            from transformers import AutoModelForImageTextToText, AutoModelForCausalLM
            
            model_classes = {
                'AutoModelForImageTextToText': AutoModelForImageTextToText,
                'AutoModelForCausalLM': AutoModelForCausalLM,
            }
            
            ModelClass = model_classes.get(model_class)
            if not ModelClass:
                raise ValueError(f"Unknown model class: {model_class}")
            
            model_kwargs = {'torch_dtype': dtype} if dtype else {}
            model_kwargs.update(kwargs)
            
            model = ModelClass.from_pretrained(model_id, **model_kwargs)
            model = model.to(device).eval()
            
            self._model_cache[cache_key] = model
            
        return self._model_cache[cache_key]
    
    @lru_cache(maxsize=4)
    def load_processor(self, model_id: str):
        """
        Load a processor with caching.
        
        Args:
            model_id: HuggingFace model ID
            
        Returns:
            Loaded processor instance
        """
        if model_id not in self._processor_cache:
            from transformers import AutoProcessor
            self._processor_cache[model_id] = AutoProcessor.from_pretrained(model_id)
            
        return self._processor_cache[model_id]
    
    def clear_cache(self, model_id: Optional[str] = None):
        """
        Clear model cache.
        
        Args:
            model_id: Specific model to clear, or None to clear all
        """
        if model_id:
            self._model_cache = {k: v for k, v in self._model_cache.items() 
                                if not k.startswith(model_id)}
            self._processor_cache.pop(model_id, None)
        else:
            self._model_cache.clear()
            self._processor_cache.clear()
            self.load_model.cache_clear()
            self.load_processor.cache_clear()
    
    def get_cached_models(self) -> Dict[str, Any]:
        """Get information about cached models."""
        return {
            'models': list(self._model_cache.keys()),
            'processors': list(self._processor_cache.keys()),
            'cache_size': len(self._model_cache) + len(self._processor_cache)
        }