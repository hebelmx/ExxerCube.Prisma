"""
SmolVLM2 model extractor for Spanish legal documents.
"""

import os
import re
import json
import torch
import warnings
from typing import Dict, Any, Optional
from PIL import Image
from pydantic import ValidationError

from .base_extractor import BaseExtractor
from ..models import Requerimiento
from ..utils.device_utils import get_optimal_device_config
from ..utils.json_utils import parse_json_flexible

# Suppress CUDA warnings
warnings.filterwarnings("ignore", message="CUDA initialization: Unexpected error from cudaGetDeviceCount()")


class SmolVLMExtractor(BaseExtractor):
    """SmolVLM2-based document extractor."""
    
    DEFAULT_MODEL_ID = "HuggingFaceTB/SmolVLM2-2.2B-Instruct"
    
    SYSTEM_INSTRUCTIONS = (
        "You are an information extraction assistant for Spanish legal documents. "
        "Extract structured fields. If a field is missing or ambiguous, set it to 'unknown'. "
        "Only output a single valid JSON object and nothing else."
    )
    
    USER_PROMPT = (
        "Read this Spanish legal document and extract: fecha (date), autoridad emisora (issuing authority), "
        "expediente (case number), and tipo de requerimiento (type of requirement). "
        "Output as valid JSON only. Example: {'fecha': '2024-01-15', 'autoridadEmisora': 'CONDUSEF'}"
    )
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """
        Initialize SmolVLM extractor.
        
        Args:
            config: Configuration dictionary with optional keys:
                - model_id: HuggingFace model ID
                - max_new_tokens: Maximum tokens to generate
                - device: torch device (auto-detected if not specified)
                - dtype: torch dtype (auto-selected if not specified)
        """
        super().__init__(config)
        
    def _initialize(self):
        """Initialize SmolVLM2 model and processor."""
        from transformers import AutoProcessor, AutoModelForImageTextToText
        
        # Get model configuration
        model_id = self.config.get('model_id', self.DEFAULT_MODEL_ID)
        self.max_new_tokens = self.config.get('max_new_tokens', 512)
        
        # Set up device and dtype
        device_config = get_optimal_device_config()
        self.device = self.config.get('device', device_config['device'])
        self.dtype = self.config.get('dtype', device_config['dtype'])
        self.attn_impl = device_config.get('attn_impl')
        
        print(f"[INFO] Initializing {self.name}: device={self.device}, dtype={self.dtype}, attn_impl={self.attn_impl}")
        
        # Load processor and model
        self.processor = AutoProcessor.from_pretrained(model_id)
        
        model_kwargs = {
            'torch_dtype': self.dtype,
        }
        if self.attn_impl:
            model_kwargs['_attn_implementation'] = self.attn_impl
            
        self.model = AutoModelForImageTextToText.from_pretrained(
            model_id,
            **model_kwargs
        ).to(self.device).eval()
        
    def _extract_impl(self, image: Image.Image) -> Dict[str, Any]:
        """
        Extract information using SmolVLM2.
        
        Args:
            image: PIL Image object
            
        Returns:
            Dictionary with extracted data
        """
        # Build chat messages
        messages = [
            {
                "role": "system", 
                "content": [{"type": "text", "text": self.SYSTEM_INSTRUCTIONS}]
            },
            {
                "role": "user",
                "content": [
                    {"type": "image", "image": image},
                    {"type": "text", "text": self.USER_PROMPT},
                ],
            },
        ]
        
        # Apply chat template
        inputs = self.processor.apply_chat_template(
            messages,
            add_generation_prompt=True,
            tokenize=True,
            return_dict=True,
            return_tensors="pt",
        )
        
        # Move tensors to device
        inputs = self._prepare_inputs(inputs)
        
        # Generate response
        with torch.no_grad():
            generated_ids = self.model.generate(
                **inputs,
                do_sample=False,  # Deterministic
                max_new_tokens=self.max_new_tokens
            )
        
        # Decode response
        result_text = self.processor.batch_decode(
            generated_ids, 
            skip_special_tokens=True
        )[0].strip()
        
        # Parse and validate JSON
        try:
            data = parse_json_flexible(result_text)
            validated = Requerimiento(**data)
            return validated.model_dump()
        except (json.JSONDecodeError, ValidationError) as e:
            # Return raw text if parsing fails
            return {
                "raw_output": result_text,
                "parse_error": str(e)
            }
    
    def _prepare_inputs(self, inputs: Dict) -> Dict:
        """Prepare inputs for the model."""
        prepared = {}
        for k, v in inputs.items():
            if torch.is_tensor(v):
                if v.dtype.is_floating_point:
                    prepared[k] = v.to(self.device, dtype=self.dtype)
                else:
                    prepared[k] = v.to(self.device)
            else:
                prepared[k] = v
        return prepared
    
    @property
    def device_info(self) -> Dict[str, Any]:
        """Get device and model information."""
        info = super().device_info
        info.update({
            "model_id": self.config.get('model_id', self.DEFAULT_MODEL_ID),
            "device": str(self.device),
            "dtype": str(self.dtype),
            "attention_implementation": self.attn_impl,
            "max_new_tokens": self.max_new_tokens
        })
        return info