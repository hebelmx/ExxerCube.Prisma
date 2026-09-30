# Developer Guide - AI Document Extractors

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Adding New Extractors](#adding-new-extractors)
3. [Model Integration](#model-integration)
4. [Testing](#testing)
5. [Performance Optimization](#performance-optimization)
6. [API Reference](#api-reference)

## Architecture Overview

### Design Principles

- **Single Responsibility**: Each extractor handles one model
- **Open/Closed**: Easy to extend without modifying core
- **Dependency Inversion**: Extractors implement base interface
- **DRY**: Shared utilities and base classes

### Component Structure

```
┌─────────────────────────────────────┐
│            CLI Interface            │
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│         Main Application            │
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│        Extractor Factory            │
└──────┬──────┬──────┬────────────────┘
       │      │      │
┌──────▼──┐ ┌─▼──┐ ┌─▼──────────────┐
│SmolVLM  │ │GOT │ │   PaddleOCR    │
└─────────┘ └────┘ └────────────────┘
       │      │      │
┌──────▼──────▼──────▼────────────────┐
│          Base Extractor             │
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│            Utilities                │
│  (Device, JSON, Image Processing)   │
└─────────────────────────────────────┘
```

## Adding New Extractors

### Step 1: Create Extractor Class

```python
# src/extractors/new_model_extractor.py
from .base_extractor import BaseExtractor
from ..models import Requerimiento

class NewModelExtractor(BaseExtractor):
    """Your model description."""
    
    DEFAULT_MODEL_ID = "org/model-name"
    
    def _initialize(self):
        """Initialize your model."""
        # Load model
        self.model = load_your_model(self.config.get('model_id', self.DEFAULT_MODEL_ID))
        
    def _extract_impl(self, image: Image.Image) -> Dict[str, Any]:
        """Implement extraction logic."""
        # Process image with your model
        result = self.model.process(image)
        
        # Parse and return structured data
        return self._parse_result(result)
```

### Step 2: Register Extractor

```python
# src/extractors/__init__.py
from .new_model_extractor import NewModelExtractor

__all__ = [
    # ... existing extractors
    'NewModelExtractor'
]
```

### Step 3: Add to CLI

```python
# src/main.py
EXTRACTORS = {
    # ... existing extractors
    'new-model': NewModelExtractor
}
```

### Step 4: Create Tests

```python
# tests/test_new_model_extractor.py
import pytest
from src.extractors import NewModelExtractor

class TestNewModelExtractor:
    def test_initialization(self):
        extractor = NewModelExtractor()
        assert extractor.name == "NewModelExtractor"
    
    def test_extraction(self, sample_image):
        extractor = NewModelExtractor()
        result = extractor.extract(sample_image)
        assert result.success
```

## Model Integration

### HuggingFace Models

```python
from transformers import AutoModel, AutoProcessor

class HFModelExtractor(BaseExtractor):
    def _initialize(self):
        model_id = self.config.get('model_id', 'default/model')
        self.processor = AutoProcessor.from_pretrained(model_id)
        self.model = AutoModel.from_pretrained(model_id)
```

### Custom Models

```python
class CustomModelExtractor(BaseExtractor):
    def _initialize(self):
        model_path = self.config.get('model_path', 'models/custom.pt')
        self.model = torch.load(model_path)
        self.model.eval()
```

### ONNX Models

```python
import onnxruntime as ort

class ONNXExtractor(BaseExtractor):
    def _initialize(self):
        model_path = self.config.get('model_path', 'models/model.onnx')
        self.session = ort.InferenceSession(model_path)
```

## Testing

### Unit Tests

```bash
# Run all tests
pytest tests/

# Run specific test file
pytest tests/test_models.py

# Run with coverage
pytest --cov=src tests/

# Run with verbose output
pytest -v tests/
```

### Integration Tests

```python
# tests/test_integration.py
def test_end_to_end_extraction():
    """Test complete extraction pipeline."""
    extractor = SmolVLMExtractor()
    result = extractor.extract("test_data/sample.png")
    
    assert result.success
    assert result.document is not None
    assert 'expediente' in result.document.structured_data
```

### Performance Tests

```python
# tests/test_performance.py
import time

def test_extraction_speed():
    """Test extraction performance."""
    extractor = SmolVLMExtractor()
    
    start = time.time()
    result = extractor.extract("test_data/sample.png")
    duration = time.time() - start
    
    assert duration < 5.0  # Should complete within 5 seconds
```

## Performance Optimization

### GPU Optimization

```python
# Optimal batch size for GPU
def optimize_batch_size(gpu_memory_gb):
    if gpu_memory_gb >= 16:
        return 32
    elif gpu_memory_gb >= 8:
        return 16
    elif gpu_memory_gb >= 4:
        return 8
    else:
        return 4
```

### Memory Management

```python
# Clear GPU cache after processing
def clear_gpu_memory():
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
        torch.cuda.synchronize()
```

### Model Quantization

```python
# Reduce model size with quantization
def quantize_model(model):
    quantized = torch.quantization.quantize_dynamic(
        model,
        {torch.nn.Linear},
        dtype=torch.qint8
    )
    return quantized
```

## API Reference

### BaseExtractor

```python
class BaseExtractor(ABC):
    """Abstract base class for extractors."""
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """Initialize extractor with configuration."""
        
    @abstractmethod
    def _initialize(self):
        """Initialize model and resources."""
        
    @abstractmethod
    def _extract_impl(self, image: Image.Image) -> Dict[str, Any]:
        """Implement extraction logic."""
        
    def extract(self, image_path: Union[str, Path]) -> ExtractionResult:
        """Extract information from image."""
        
    def batch_extract(self, image_paths: list) -> list:
        """Extract from multiple images."""
```

### ExtractionResult

```python
class ExtractionResult(BaseModel):
    success: bool
    document: Optional[ExtractedDocument]
    error_message: Optional[str]
    processing_time: Optional[float]
    extractor_name: str
```

### Configuration Options

```python
config = {
    # Model configuration
    "model_id": "huggingface/model",
    "model_path": "/path/to/model",
    
    # Device configuration
    "device": "cuda",  # or "cpu"
    "dtype": "float16",  # or "float32", "bfloat16"
    
    # Processing configuration
    "max_new_tokens": 512,
    "batch_size": 8,
    "num_workers": 4,
    
    # Optimization
    "use_flash_attention": True,
    "compile_model": False,
    "quantize": False
}
```

## Best Practices

### Error Handling

```python
def safe_extraction(self, image_path):
    try:
        return self.extract(image_path)
    except torch.cuda.OutOfMemoryError:
        # Clear cache and retry with smaller batch
        torch.cuda.empty_cache()
        self.config['batch_size'] = 1
        return self.extract(image_path)
    except Exception as e:
        logger.error(f"Extraction failed: {e}")
        return ExtractionResult(
            success=False,
            error_message=str(e),
            extractor_name=self.name
        )
```

### Logging

```python
import logging

logger = logging.getLogger(__name__)

class ExtractorWithLogging(BaseExtractor):
    def _extract_impl(self, image):
        logger.info(f"Starting extraction for image size: {image.size}")
        
        try:
            result = self._process(image)
            logger.info("Extraction successful")
            return result
        except Exception as e:
            logger.error(f"Extraction failed: {e}")
            raise
```

### Resource Management

```python
class ManagedExtractor(BaseExtractor):
    def __enter__(self):
        self._initialize()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        # Clean up resources
        if hasattr(self, 'model'):
            del self.model
        torch.cuda.empty_cache()
```

## Debugging

### Enable Debug Mode

```python
# Set environment variable
export AI_EXTRACTOR_DEBUG=1

# Or in code
import os
os.environ['AI_EXTRACTOR_DEBUG'] = '1'
```

### Profile Performance

```python
import cProfile
import pstats

def profile_extraction():
    profiler = cProfile.Profile()
    profiler.enable()
    
    # Run extraction
    extractor = SmolVLMExtractor()
    result = extractor.extract("test.png")
    
    profiler.disable()
    stats = pstats.Stats(profiler)
    stats.sort_stats('cumulative')
    stats.print_stats(10)
```

### Memory Profiling

```python
from memory_profiler import profile

@profile
def memory_intensive_extraction():
    extractor = SmolVLMExtractor()
    result = extractor.extract("large_image.png")
    return result
```

## Deployment

### Docker

```dockerfile
FROM python:3.9-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt

COPY src/ src/
COPY config/ config/

CMD ["python", "src/main.py"]
```

### Production Configuration

```python
# config/production.py
PRODUCTION_CONFIG = {
    "device": "cuda",
    "dtype": "float16",
    "max_new_tokens": 512,
    "batch_size": 16,
    "num_workers": 8,
    "use_flash_attention": True,
    "compile_model": True,
    "cache_dir": "/var/cache/models"
}
```

## Troubleshooting

### Common Issues

1. **Model Loading Errors**
   - Check model ID/path
   - Verify internet connection
   - Check disk space for cache

2. **CUDA Errors**
   - Verify CUDA installation
   - Check GPU memory
   - Update drivers

3. **Slow Performance**
   - Enable GPU acceleration
   - Reduce batch size
   - Use model quantization

4. **Memory Leaks**
   - Clear GPU cache regularly
   - Delete unused tensors
   - Use context managers

## Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/new-extractor`)
3. Commit changes (`git commit -am 'Add new extractor'`)
4. Push to branch (`git push origin feature/new-extractor`)
5. Create Pull Request

## Resources

- [Transformers Documentation](https://huggingface.co/docs/transformers)
- [PyTorch Documentation](https://pytorch.org/docs/stable/)
- [Pydantic Documentation](https://docs.pydantic.dev/)
- [Vision-Language Models](https://github.com/huggingface/transformers)