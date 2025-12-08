"""Model caching utilities for efficient model loading."""

from typing import Any, Optional

from transformers import (
    AutoModel,
    AutoProcessor,
    CLIPModel,
    CLIPProcessor,
    LayoutLMv3ForTokenClassification,
    LayoutLMv3Processor,
    TableTransformerForObjectDetection,
    TrOCRProcessor,
    VisionEncoderDecoderModel,
)


class ModelCache:
    """
    Singleton cache for HuggingFace models to avoid repeated loading.

    Models are loaded once on first access and cached for subsequent uses.
    This significantly improves performance in batch processing scenarios.
    """

    _instance: Optional["ModelCache"] = None
    _models: dict[str, Any] = {}
    _processors: dict[str, Any] = {}

    def __new__(cls) -> "ModelCache":
        """Singleton pattern implementation."""
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance

    def get_clip_model(
        self, model_name: str = "openai/clip-vit-large-patch14"
    ) -> tuple[CLIPModel, CLIPProcessor]:
        """
        Get CLIP model and processor (cached).

        Args:
            model_name: HuggingFace model identifier

        Returns:
            Tuple of (CLIPModel, CLIPProcessor)
        """
        cache_key = f"clip_{model_name}"

        if cache_key not in self._models:
            self._models[cache_key] = CLIPModel.from_pretrained(model_name)
            self._processors[cache_key] = CLIPProcessor.from_pretrained(model_name)

        return self._models[cache_key], self._processors[cache_key]

    def get_layoutlmv3_model(
        self, model_name: str = "microsoft/layoutlmv3-base"
    ) -> tuple[LayoutLMv3ForTokenClassification, LayoutLMv3Processor]:
        """
        Get LayoutLMv3 model and processor (cached).

        Args:
            model_name: HuggingFace model identifier

        Returns:
            Tuple of (LayoutLMv3ForTokenClassification, LayoutLMv3Processor)
        """
        cache_key = f"layoutlmv3_{model_name}"

        if cache_key not in self._models:
            self._models[cache_key] = LayoutLMv3ForTokenClassification.from_pretrained(
                model_name
            )
            self._processors[cache_key] = LayoutLMv3Processor.from_pretrained(model_name)

        return self._models[cache_key], self._processors[cache_key]

    def get_table_transformer_model(
        self, model_name: str = "microsoft/table-transformer-detection"
    ) -> tuple[TableTransformerForObjectDetection, AutoProcessor]:
        """
        Get Table Transformer model and processor (cached).

        Args:
            model_name: HuggingFace model identifier

        Returns:
            Tuple of (TableTransformerForObjectDetection, AutoProcessor)
        """
        cache_key = f"table_transformer_{model_name}"

        if cache_key not in self._models:
            self._models[cache_key] = (
                TableTransformerForObjectDetection.from_pretrained(model_name)
            )
            self._processors[cache_key] = AutoProcessor.from_pretrained(model_name)

        return self._models[cache_key], self._processors[cache_key]

    def get_trocr_model(
        self, model_name: str = "microsoft/trocr-large-printed"
    ) -> tuple[VisionEncoderDecoderModel, TrOCRProcessor]:
        """
        Get TrOCR model and processor (cached).

        Args:
            model_name: HuggingFace model identifier

        Returns:
            Tuple of (VisionEncoderDecoderModel, TrOCRProcessor)
        """
        cache_key = f"trocr_{model_name}"

        if cache_key not in self._models:
            self._models[cache_key] = VisionEncoderDecoderModel.from_pretrained(
                model_name
            )
            self._processors[cache_key] = TrOCRProcessor.from_pretrained(model_name)

        return self._models[cache_key], self._processors[cache_key]

    def get_donut_model(
        self, model_name: str = "naver-clova-ix/donut-base-finetuned-cord-v2"
    ) -> tuple[VisionEncoderDecoderModel, AutoProcessor]:
        """
        Get Donut model and processor (cached).

        Args:
            model_name: HuggingFace model identifier

        Returns:
            Tuple of (VisionEncoderDecoderModel, AutoProcessor)
        """
        cache_key = f"donut_{model_name}"

        if cache_key not in self._models:
            self._models[cache_key] = VisionEncoderDecoderModel.from_pretrained(
                model_name
            )
            self._processors[cache_key] = AutoProcessor.from_pretrained(model_name)

        return self._models[cache_key], self._processors[cache_key]

    def clear_cache(self) -> None:
        """Clear all cached models (use for testing or memory management)."""
        self._models.clear()
        self._processors.clear()

    def get_cache_info(self) -> dict[str, int]:
        """
        Get cache statistics.

        Returns:
            Dictionary with cache information
        """
        return {
            "models_cached": len(self._models),
            "processors_cached": len(self._processors),
        }


# Singleton instance
model_cache = ModelCache()
