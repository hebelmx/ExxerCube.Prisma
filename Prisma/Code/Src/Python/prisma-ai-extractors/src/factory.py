"""
Extractor Factory - Clean interface for creating and using modular extractors.
"""

from typing import Dict, Any, Optional, Type, Union
from pathlib import Path

from .extractors_v2 import (
    ModularExtractor, 
    SmolVLMModularExtractor, 
    PaddleModularExtractor,
    DocTRModularExtractor
)
from .modules import ConfigManager


class ExtractorFactory:
    """
    Factory for creating extractor instances with clean interface.
    Single Responsibility: Extractor instantiation and configuration.
    """
    
    # Registry of available extractors
    EXTRACTORS = {
        'smolvlm': SmolVLMModularExtractor,
        'paddle': PaddleModularExtractor,
        'doctr': DocTRModularExtractor,
    }
    
    def __init__(self, config: Optional[Union[str, Path, Dict[str, Any]]] = None):
        \"\"\"Initialize factory with optional configuration.\"\"\"\n        if isinstance(config, (str, Path)):\n            # Load from file\n            self.config_manager = ConfigManager(config)\n        elif isinstance(config, dict):\n            # Use provided config\n            self.config_manager = ConfigManager()\n            for key, value in config.items():\n                self.config_manager.set(key, value)\n        else:\n            # Use defaults\n            self.config_manager = ConfigManager()\n    \n    def create_extractor(\n        self, \n        extractor_type: str, \n        config_override: Optional[Dict[str, Any]] = None\n    ) -> ModularExtractor:\n        \"\"\"\n        Create an extractor instance.\n        \n        Args:\n            extractor_type: Type of extractor ('smolvlm', 'paddle', 'doctr')\n            config_override: Override configuration\n            \n        Returns:\n            Configured extractor instance\n            \n        Raises:\n            ValueError: If extractor type is not supported\n        \"\"\"\n        if extractor_type not in self.EXTRACTORS:\n            available = list(self.EXTRACTORS.keys())\n            raise ValueError(\n                f\"Unknown extractor type: {extractor_type}. \"\n                f\"Available: {available}\"\n            )\n        \n        # Get base configuration\n        config = self.config_manager.to_dict().copy()\n        \n        # Apply overrides\n        if config_override:\n            config.update(config_override)\n        \n        # Create extractor\n        ExtractorClass = self.EXTRACTORS[extractor_type]\n        return ExtractorClass(config)\n    \n    def create_all_extractors(\n        self, \n        config_override: Optional[Dict[str, Any]] = None\n    ) -> Dict[str, ModularExtractor]:\n        \"\"\"\n        Create instances of all available extractors.\n        \n        Args:\n            config_override: Override configuration for all extractors\n            \n        Returns:\n            Dictionary mapping extractor names to instances\n        \"\"\"\n        extractors = {}\n        \n        for extractor_type in self.EXTRACTORS:\n            try:\n                extractors[extractor_type] = self.create_extractor(\n                    extractor_type, config_override\n                )\n            except Exception as e:\n                print(f\"Failed to create {extractor_type} extractor: {e}\")\n        \n        return extractors\n    \n    def get_available_extractors(self) -> list:\n        \"\"\"Get list of available extractor types.\"\"\"\n        return list(self.EXTRACTORS.keys())\n    \n    def register_extractor(self, name: str, extractor_class: Type[ModularExtractor]):\n        \"\"\"\n        Register a custom extractor.\n        \n        Args:\n            name: Extractor name\n            extractor_class: Extractor class\n        \"\"\"\n        self.EXTRACTORS[name] = extractor_class\n\n\ndef create_extractor(\n    extractor_type: str, \n    config: Optional[Union[str, Path, Dict[str, Any]]] = None,\n    **kwargs\n) -> ModularExtractor:\n    \"\"\"\n    Convenience function to create a single extractor.\n    \n    Args:\n        extractor_type: Type of extractor\n        config: Configuration (file path, dict, or None)\n        **kwargs: Additional configuration overrides\n        \n    Returns:\n        Configured extractor instance\n    \"\"\"\n    factory = ExtractorFactory(config)\n    return factory.create_extractor(extractor_type, kwargs)\n\n\ndef extract_from_image(\n    image_path: Union[str, Path],\n    extractor_type: str = 'smolvlm',\n    config: Optional[Union[str, Path, Dict[str, Any]]] = None,\n    **kwargs\n):\n    \"\"\"\n    High-level function to extract from a single image.\n    \n    Args:\n        image_path: Path to image\n        extractor_type: Type of extractor to use\n        config: Configuration\n        **kwargs: Additional configuration\n        \n    Returns:\n        ExtractionResult\n    \"\"\"\n    extractor = create_extractor(extractor_type, config, **kwargs)\n    return extractor.extract(image_path)\n\n\ndef extract_from_images(\n    image_paths: list,\n    extractor_type: str = 'smolvlm',\n    config: Optional[Union[str, Path, Dict[str, Any]]] = None,\n    **kwargs\n):\n    \"\"\"\n    High-level function to extract from multiple images.\n    \n    Args:\n        image_paths: List of image paths\n        extractor_type: Type of extractor to use\n        config: Configuration\n        **kwargs: Additional configuration\n        \n    Returns:\n        List of ExtractionResult\n    \"\"\"\n    extractor = create_extractor(extractor_type, config, **kwargs)\n    return extractor.batch_extract(image_paths)