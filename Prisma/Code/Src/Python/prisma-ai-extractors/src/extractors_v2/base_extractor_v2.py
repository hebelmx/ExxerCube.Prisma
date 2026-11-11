"""
Modular Base Extractor - Uses composition instead of inheritance for SRP.
"""

from abc import ABC, abstractmethod
from typing import Union, Dict, Any, Optional
from pathlib import Path
from PIL import Image

from ..modules import (
    ModelLoader, ImageProcessor, TextExtractor, JsonParser,
    DocumentValidator, PerformanceMonitor, ConfigManager, ErrorHandler
)
from ..models import ExtractionResult, ExtractedDocument


class ModularExtractor(ABC):
    """
    Base class for modular extractors using composition.
    Each responsibility is handled by a dedicated module.
    """
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """Initialize with modular components."""
        self.name = self.__class__.__name__
        
        # Initialize modules (dependency injection)
        self.model_loader = ModelLoader()
        self.image_processor = ImageProcessor()
        self.text_extractor = TextExtractor()
        self.json_parser = JsonParser()
        self.document_validator = DocumentValidator()
        self.performance_monitor = PerformanceMonitor()
        self.config_manager = ConfigManager()
        self.error_handler = ErrorHandler()
        
        # Load configuration
        self.config = config or {}\n        if config:\n            for key, value in config.items():\n                self.config_manager.set(key, value)\n        \n        # Initialize extractor-specific components\n        self._initialize_extractor()\n    \n    @abstractmethod\n    def _initialize_extractor(self):\n        \"\"\"Initialize extractor-specific components.\"\"\"\n        pass\n    \n    @abstractmethod\n    def _extract_text(self, image: Image.Image) -> str:\n        \"\"\"Extract text from image using specific method.\"\"\"\n        pass\n    \n    def extract(self, image_path: Union[str, Path]) -> ExtractionResult:\n        \"\"\"\n        Main extraction method using modular components.\n        \n        Args:\n            image_path: Path to image file\n            \n        Returns:\n            ExtractionResult with success status and data\n        \"\"\"\n        with self.performance_monitor.track_operation(\n            \"extract\", \n            extractor=self.name,\n            image_path=str(image_path)\n        ) as metrics:\n            try:\n                # 1. Load and validate image (ImageProcessor responsibility)\n                image = self.image_processor.load_image(image_path)\n                \n                # 2. Extract text (TextExtractor + specific implementation)\n                raw_text = self._extract_text(image)\n                \n                # 3. Parse JSON (JsonParser responsibility)\n                parsed_data = self.json_parser.parse(raw_text)\n                \n                # 4. Validate document (DocumentValidator responsibility)\n                is_valid, validated_data, validation_errors = self.document_validator.validate(parsed_data)\n                \n                # 5. Create result\n                document = ExtractedDocument(\n                    document_type=\"legal_requirement\",\n                    structured_data=validated_data,\n                    metadata={\n                        \"source_file\": str(image_path),\n                        \"extractor\": self.name,\n                        \"validation_errors\": validation_errors,\n                        \"raw_text\": raw_text[:500]  # Truncated for storage\n                    }\n                )\n                \n                return ExtractionResult(\n                    success=True,\n                    document=document,\n                    processing_time=metrics.duration,\n                    extractor_name=self.name\n                )\n                \n            except Exception as e:\n                # Handle error using ErrorHandler\n                error_info = self.error_handler.handle_error(\n                    e,\n                    context={\n                        \"extractor\": self.name,\n                        \"image_path\": str(image_path)\n                    }\n                )\n                \n                return ExtractionResult(\n                    success=False,\n                    error_message=str(e),\n                    processing_time=metrics.duration,\n                    extractor_name=self.name,\n                    metadata={\"error_info\": error_info}\n                )\n    \n    def batch_extract(self, image_paths: list) -> list:\n        \"\"\"\n        Extract from multiple images using performance monitoring.\n        \n        Args:\n            image_paths: List of image paths\n            \n        Returns:\n            List of ExtractionResult objects\n        \"\"\"\n        with self.performance_monitor.track_operation(\n            \"batch_extract\",\n            extractor=self.name,\n            batch_size=len(image_paths)\n        ):\n            results = []\n            for path in image_paths:\n                result = self.extract(path)\n                results.append(result)\n            \n            return results\n    \n    def get_performance_report(self) -> Dict[str, Any]:\n        \"\"\"Get performance metrics from the monitor.\"\"\"\n        return self.performance_monitor.get_report()\n    \n    def get_error_stats(self) -> Dict[str, Any]:\n        \"\"\"Get error statistics from the handler.\"\"\"\n        return self.error_handler.get_error_stats()\n    \n    def cleanup(self):\n        \"\"\"Cleanup resources.\"\"\"\n        self.model_loader.clear_cache()\n        self.performance_monitor.clear_history()\n        self.error_handler.clear_stats()\n    \n    @property\n    def device_info(self) -> Dict[str, Any]:\n        \"\"\"Get device and configuration information.\"\"\"\n        return {\n            \"extractor\": self.name,\n            \"config\": self.config_manager.to_dict(),\n            \"cached_models\": self.model_loader.get_cached_models(),\n            \"performance\": self.performance_monitor.get_operation_stats(),\n            \"errors\": self.error_handler.get_error_stats()\n        }