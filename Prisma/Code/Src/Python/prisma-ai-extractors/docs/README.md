# Prisma AI Extractors

## Overview

Prisma AI Extractors - AI-powered document extraction system using state-of-the-art vision-language models for processing Spanish legal documents. This modular system supports multiple AI models and provides a unified interface for document information extraction.

## Features

- 🤖 **Multiple AI Models**: SmolVLM2, GOT-OCR2, PaddleOCR, DocTR
- 📄 **Spanish Legal Documents**: Optimized for Mexican legal requirements
- 🔧 **Modular Architecture**: Easy to extend with new models
- 🚀 **GPU Acceleration**: Automatic CUDA optimization
- 📊 **Structured Output**: Pydantic-validated JSON results
- 🔄 **Batch Processing**: Process multiple documents efficiently

## Quick Start

```bash
# Install dependencies
pip install -r requirements.txt

# Extract using SmolVLM (default)
python src/main.py --image document.png

# Use specific extractor
python src/main.py --image document.pdf --extractor paddle

# Batch processing
python src/main.py --batch documents/ --output results.json
```

## Supported Models

### SmolVLM2
- **Model**: HuggingFaceTB/SmolVLM2-2.2B-Instruct
- **Size**: 2.2B parameters
- **Best for**: General document understanding
- **GPU Memory**: ~4GB

### GOT-OCR2
- **Model**: stepfun-ai/GOT-OCR2_0
- **Size**: Variable
- **Best for**: Complex layouts
- **GPU Memory**: ~6GB

### PaddleOCR
- **Model**: PaddlePaddle OCR
- **Best for**: Fast processing
- **GPU Memory**: ~2GB

### DocTR
- **Model**: mindee/doctr
- **Best for**: Structured documents
- **GPU Memory**: ~3GB

## Installation

### Requirements

- Python 3.9+
- CUDA 11.8+ (optional, for GPU acceleration)
- 8GB+ RAM
- 4GB+ GPU VRAM (recommended)

### Setup

```bash
# Clone repository
git clone <repository>
cd ai-extractors

# Create virtual environment
python -m venv venv
source venv/bin/activate  # Linux/Mac
# or
venv\Scripts\activate  # Windows

# Install dependencies
pip install -r requirements.txt

# Download models (automatic on first run)
python src/main.py --list-extractors
```

## Usage Examples

### Basic Extraction

```python
from src.extractors import SmolVLMExtractor

# Initialize extractor
extractor = SmolVLMExtractor()

# Extract from image
result = extractor.extract("document.png")

if result.success:
    print(result.document.structured_data)
else:
    print(f"Error: {result.error_message}")
```

### Custom Configuration

```python
config = {
    "max_new_tokens": 1024,
    "device": "cuda",
    "dtype": "float16"
}

extractor = SmolVLMExtractor(config)
```

### Batch Processing

```python
images = ["doc1.png", "doc2.pdf", "doc3.jpg"]
results = extractor.batch_extract(images)

for result in results:
    if result.success:
        print(f"Extracted: {result.document.structured_data}")
```

## CLI Reference

```bash
# Show help
python src/main.py --help

# List available extractors
python src/main.py --list-extractors

# Show device configuration
python src/main.py --device-info

# Extract with verbose output
python src/main.py --image doc.png --verbose

# Custom model configuration
python src/main.py --image doc.pdf --config '{"max_new_tokens": 1024}'
```

## Output Format

```json
{
  "success": true,
  "document": {
    "document_type": "legal_requirement",
    "confidence_score": 0.95,
    "structured_data": {
      "fecha": "2024-01-15",
      "autoridadEmisora": "CONDUSEF",
      "expediente": "EXP-2024-001",
      "tipoRequerimiento": "EMBARGO",
      "partes": ["Juan Pérez", "Banco XYZ"],
      "detalle": {
        "monto": 100000.0,
        "moneda": "MXN"
      }
    },
    "metadata": {
      "source_file": "document.png",
      "extractor": "SmolVLMExtractor"
    }
  },
  "processing_time": 2.5,
  "extractor_name": "SmolVLMExtractor"
}
```

## Architecture

```
ai-extractors/
├── src/
│   ├── extractors/        # Model implementations
│   │   ├── base_extractor.py
│   │   ├── smolvlm_extractor.py
│   │   └── ...
│   ├── models/            # Data models
│   │   └── document_models.py
│   ├── utils/             # Utilities
│   │   ├── device_utils.py
│   │   ├── json_utils.py
│   │   └── image_utils.py
│   └── main.py           # CLI entry point
├── tests/                # Unit tests
├── docs/                 # Documentation
└── config/               # Configuration files
```

## Performance

| Model | Speed (sec/doc) | Accuracy | GPU Memory |
|-------|----------------|----------|------------|
| SmolVLM2 | 2-3 | 95% | 4GB |
| GOT-OCR2 | 3-4 | 97% | 6GB |
| PaddleOCR | 1-2 | 92% | 2GB |
| DocTR | 2-3 | 93% | 3GB |

## Troubleshooting

### CUDA Not Available
```bash
# Check CUDA installation
python -c "import torch; print(torch.cuda.is_available())"

# Use CPU fallback
python src/main.py --image doc.png --config '{"device": "cpu"}'
```

### Out of Memory
```bash
# Reduce batch size
python src/main.py --config '{"max_new_tokens": 256}'

# Use smaller model
python src/main.py --extractor paddle
```

### Model Download Issues
```bash
# Set custom cache directory
export HF_HOME=/path/to/cache
export TRANSFORMERS_CACHE=/path/to/cache
```

## Contributing

See [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md) for development setup and guidelines.

## License

Proprietary - ExxerCube

## Support

For issues and questions, please refer to the documentation or contact the development team.