"""
DocTR Modular Extractor - Clean separation using modules.
"""

from typing import Dict, Any, Optional
from PIL import Image
import numpy as np

from .base_extractor_v2 import ModularExtractor


class DocTRModularExtractor(ModularExtractor):
    """
    DocTR extractor using modular architecture.
    Single Responsibility: Coordinate DocTR-specific extraction.
    """
    
    def _initialize_extractor(self):
        """Initialize DocTR-specific components."""
        # Get configuration
        extractor_config = self.config_manager.get_extractor_config('doctr')
        
        self.det_arch = extractor_config.get('det_arch', 'db_resnet50')
        self.reco_arch = extractor_config.get('reco_arch', 'crnn_vgg16_bn')
        self.pretrained = extractor_config.get('pretrained', True)
        
        # Load DocTR model
        self._load_components()
    
    def _load_components(self):
        \"\"\"Load DocTR model.\"\"\"\n        with self.performance_monitor.track_operation(\"model_loading\"):\n            from doctr.models import ocr_predictor\n            \n            self.ocr_model = ocr_predictor(\n                det_arch=self.det_arch,\n                reco_arch=self.reco_arch,\n                pretrained=self.pretrained\n            )\n    \n    def _extract_text(self, image: Image.Image) -> str:\n        \"\"\"\n        Extract text using DocTR through TextExtractor.\n        \n        Args:\n            image: PIL Image\n            \n        Returns:\n            Extracted text\n        \"\"\"\n        with self.performance_monitor.track_operation(\n            \"text_extraction\",\n            model_type=\"doctr\"\n        ):\n            # Convert PIL image to numpy array\n            image_array = np.array(image)\n            \n            # Use TextExtractor module for OCR\n            raw_text = self.text_extractor.extract_with_ocr_model(\n                model=self.ocr_model,\n                image=image_array\n            )\n            \n            # Apply post-processing\n            return self._post_process_text(raw_text)\n    \n    def _post_process_text(self, text: str) -> str:\n        \"\"\"\n        Post-process DocTR extracted text for better JSON parsing.\n        \n        Args:\n            text: Raw extracted text\n            \n        Returns:\n            Processed text formatted for JSON parsing\n        \"\"\"\n        # DocTR typically returns well-structured text\n        # Apply basic cleaning and JSON formatting\n        lines = [line.strip() for line in text.split('\\n') if line.strip()]\n        \n        # Try to identify key-value pairs\n        json_data = {}\n        \n        for line in lines:\n            # Common Spanish legal document patterns\n            if any(keyword in line.lower() for keyword in ['fecha', 'date']):\n                # Extract date\n                import re\n                date_match = re.search(r'\\d{1,2}[/-]\\d{1,2}[/-]\\d{2,4}', line)\n                if date_match:\n                    json_data['fecha'] = date_match.group()\n                    \n            elif any(keyword in line.lower() for keyword in ['autoridad', 'emisor', 'authority']):\n                # Extract authority\n                parts = line.split(':')\n                if len(parts) > 1:\n                    json_data['autoridadEmisora'] = parts[1].strip()\n                    \n            elif any(keyword in line.lower() for keyword in ['expediente', 'número', 'no.']):\n                # Extract case number\n                import re\n                num_match = re.search(r'[A-Z0-9/-]+', line)\n                if num_match:\n                    json_data['expediente'] = num_match.group()\n                    \n            elif any(keyword in line.lower() for keyword in ['requerimiento', 'tipo']):\n                # Extract requirement type\n                parts = line.split(':')\n                if len(parts) > 1:\n                    json_data['tipoDeRequerimiento'] = parts[1].strip()\n        \n        # Return as JSON string\n        import json\n        if json_data:\n            return json.dumps(json_data, ensure_ascii=False, indent=2)\n        else:\n            # Fallback: return original text\n            return text\n    \n    @property\n    def device_info(self) -> Dict[str, Any]:\n        \"\"\"Get DocTR-specific information.\"\"\"\n        base_info = super().device_info\n        base_info.update({\n            \"model_type\": \"doctr\",\n            \"det_arch\": self.det_arch,\n            \"reco_arch\": self.reco_arch,\n            \"pretrained\": self.pretrained\n        })\n        return base_info