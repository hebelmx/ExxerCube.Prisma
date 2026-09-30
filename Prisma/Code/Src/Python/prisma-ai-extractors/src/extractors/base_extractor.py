"""
Base extractor interface for all AI document extractors.
"""

from abc import ABC, abstractmethod
from typing import Union, Dict, Any, Optional
from pathlib import Path
import time
from PIL import Image

from ..models import ExtractionResult, ExtractedDocument


class BaseExtractor(ABC):
    """Abstract base class for document extractors."""
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """
        Initialize the extractor.
        
        Args:
            config: Configuration dictionary for the extractor
        """
        self.config = config or {}
        self.name = self.__class__.__name__
        self._initialize()
    
    @abstractmethod
    def _initialize(self):
        """Initialize model and resources."""
        pass
    
    @abstractmethod
    def _extract_impl(self, image: Image.Image) -> Dict[str, Any]:
        """
        Implementation of extraction logic.
        
        Args:
            image: PIL Image object
            
        Returns:
            Dictionary with extracted data
        """
        pass
    
    def extract(self, image_path: Union[str, Path]) -> ExtractionResult:
        """
        Extract information from an image.
        
        Args:
            image_path: Path to the image file
            
        Returns:
            ExtractionResult with success status and extracted data
        """
        start_time = time.time()
        
        try:
            # Load image
            image_path = Path(image_path)
            if not image_path.exists():
                return ExtractionResult(
                    success=False,
                    error_message=f"Image file not found: {image_path}",
                    extractor_name=self.name
                )
            
            image = Image.open(image_path).convert("RGB")
            
            # Extract data
            extracted_data = self._extract_impl(image)
            
            # Create result
            document = ExtractedDocument(
                document_type="legal_requirement",
                structured_data=extracted_data,
                metadata={
                    "source_file": str(image_path),
                    "extractor": self.name
                }
            )
            
            processing_time = time.time() - start_time
            
            return ExtractionResult(
                success=True,
                document=document,
                processing_time=processing_time,
                extractor_name=self.name
            )
            
        except Exception as e:
            processing_time = time.time() - start_time
            return ExtractionResult(
                success=False,
                error_message=str(e),
                processing_time=processing_time,
                extractor_name=self.name
            )
    
    def batch_extract(self, image_paths: list) -> list:
        """
        Extract from multiple images.
        
        Args:
            image_paths: List of image paths
            
        Returns:
            List of ExtractionResult objects
        """
        results = []
        for path in image_paths:
            results.append(self.extract(path))
        return results
    
    @property
    def device_info(self) -> Dict[str, Any]:
        """Get device and configuration information."""
        return {
            "extractor": self.name,
            "config": self.config
        }