"""
Text Extractor Module - Single Responsibility: Text extraction from models.
"""

from typing import Dict, Any, Protocol, Optional
import torch
from PIL import Image


class ModelProtocol(Protocol):
    """Protocol for models that can generate text."""
    def generate(self, **kwargs): ...


class ProcessorProtocol(Protocol):
    """Protocol for processors."""
    def apply_chat_template(self, messages, **kwargs): ...
    def batch_decode(self, ids, **kwargs): ...


class TextExtractor:
    """
    Responsible ONLY for extracting text from models.
    Single Responsibility: Model inference and text generation.
    """
    
    def __init__(self):
        self.generation_config = {
            'do_sample': False,
            'max_new_tokens': 512,
            'temperature': 0.1,
            'top_p': 0.95
        }
    
    def extract_with_vision_model(
        self,
        model: ModelProtocol,
        processor: ProcessorProtocol,
        image: Image.Image,
        prompt: str,
        system_prompt: Optional[str] = None,
        device: str = 'cpu',
        dtype: Optional[torch.dtype] = None,
        **generation_kwargs
    ) -> str:
        """
        Extract text using a vision-language model.
        
        Args:
            model: Vision-language model
            processor: Model processor
            image: Input image
            prompt: User prompt
            system_prompt: System instructions
            device: Computation device
            dtype: Model dtype
            **generation_kwargs: Additional generation parameters
            
        Returns:
            Generated text
        """
        # Build messages
        messages = self._build_messages(image, prompt, system_prompt)
        
        # Prepare inputs
        inputs = processor.apply_chat_template(
            messages,
            add_generation_prompt=True,
            tokenize=True,
            return_dict=True,
            return_tensors="pt"
        )
        
        # Move to device
        inputs = self._prepare_inputs(inputs, device, dtype)
        
        # Generate
        gen_config = {**self.generation_config, **generation_kwargs}
        
        with torch.no_grad():
            generated_ids = model.generate(**inputs, **gen_config)
        
        # Decode
        text = processor.batch_decode(
            generated_ids,
            skip_special_tokens=True
        )[0].strip()
        
        return text
    
    def extract_with_ocr_model(
        self,
        model: Any,
        image: Image.Image,
        **kwargs
    ) -> str:
        """
        Extract text using an OCR model.
        
        Args:
            model: OCR model instance
            image: Input image
            **kwargs: Model-specific parameters
            
        Returns:
            Extracted text
        """
        # This is a generic interface - specific OCR models
        # will have their own extraction methods
        if hasattr(model, 'ocr'):
            # PaddleOCR style
            result = model.ocr(image, **kwargs)
            return self._parse_ocr_result(result)
        elif hasattr(model, 'predict'):
            # DocTR style
            result = model.predict(image, **kwargs)
            return self._parse_doctr_result(result)
        else:
            raise ValueError(f"Unknown OCR model type: {type(model)}")
    
    def _build_messages(
        self,
        image: Image.Image,
        prompt: str,
        system_prompt: Optional[str] = None
    ) -> list:
        """Build chat messages for vision models."""
        messages = []
        
        if system_prompt:
            messages.append({
                "role": "system",
                "content": [{"type": "text", "text": system_prompt}]
            })
        
        messages.append({
            "role": "user",
            "content": [
                {"type": "image", "image": image},
                {"type": "text", "text": prompt}
            ]
        })
        
        return messages
    
    def _prepare_inputs(
        self,
        inputs: Dict,
        device: str,
        dtype: Optional[torch.dtype] = None
    ) -> Dict:
        """Prepare model inputs for the target device."""
        prepared = {}
        for k, v in inputs.items():
            if torch.is_tensor(v):
                if dtype and v.dtype.is_floating_point:
                    prepared[k] = v.to(device, dtype=dtype)
                else:
                    prepared[k] = v.to(device)
            else:
                prepared[k] = v
        return prepared
    
    def _parse_ocr_result(self, result: Any) -> str:
        """Parse PaddleOCR-style results."""
        if not result or not result[0]:
            return ""
        
        lines = []
        for line in result[0]:
            if len(line) >= 2:
                text = line[1][0]
                lines.append(text)
        
        return "\n".join(lines)
    
    def _parse_doctr_result(self, result: Any) -> str:
        """Parse DocTR-style results."""
        if hasattr(result, 'render'):
            return result.render()
        return str(result)
    
    def set_generation_config(self, **kwargs):
        """Update generation configuration."""
        self.generation_config.update(kwargs)