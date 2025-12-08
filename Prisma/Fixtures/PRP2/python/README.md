# VEC Statement Processing - Python ML Pipeline

Clean Python implementation for visual identification and font identification using HuggingFace Transformers, PyTorch, Pydantic, and pytest.

## Project Structure

```
python/
├── vec_visual_font_identification/     # Main package
│   ├── __init__.py
│   ├── models/                         # Pydantic data models
│   │   ├── __init__.py
│   │   ├── logo_detection.py
│   │   ├── font_detection.py
│   │   └── quality_report.py
│   ├── visual/                          # Visual identification module
│   │   ├── __init__.py
│   │   ├── logo_detector.py
│   │   ├── image_quality.py
│   │   └── visual_compliance.py
│   ├── font/                            # Font identification module
│   │   ├── __init__.py
│   │   ├── font_detector.py
│   │   ├── typography_analyzer.py
│   │   └── overlap_detector.py
│   ├── quality/                          # Quality verification orchestration
│   │   ├── __init__.py
│   │   └── quality_verifier.py
│   └── utils/                           # Utilities
│       ├── __init__.py
│       ├── pdf_processor.py
│       └── image_utils.py
├── tests/                                # Test suite
│   ├── __init__.py
│   ├── unit/
│   │   ├── test_logo_detector.py
│   │   ├── test_font_detector.py
│   │   └── test_quality_verifier.py
│   ├── integration/
│   │   ├── test_visual_pipeline.py
│   │   └── test_font_pipeline.py
│   └── fixtures/                        # Test fixtures
│       └── sample_pdfs/
├── training/                             # Model training scripts
│   ├── train_clip_logo.py
│   ├── train_font_classifier.py
│   └── data_preparation/
├── requirements.txt
├── requirements-dev.txt
├── setup.py
└── README.md
```

## Installation

```bash
# Create virtual environment
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Install development dependencies
pip install -r requirements-dev.txt

# Install package in development mode
pip install -e .
```

## Usage

### Logo Detection

```python
from vec_visual_font_identification.visual import LogoDetector
from PIL import Image

# Initialize detector (loads CLIP model)
detector = LogoDetector()

# Detect logos in PDF page
with open("statement.pdf", "rb") as f:
    pdf_bytes = f.read()

results = detector.detect_logos(pdf_bytes, page_number=0)
print(f"Found {len(results.logos)} logos")
for logo in results.logos:
    print(f"Logo: {logo.family}, Confidence: {logo.confidence:.2f}")
```

### Font Identification

```python
from vec_visual_font_identification.font import FontDetector

# Initialize detector
detector = FontDetector()

# Detect fonts in PDF
results = detector.detect_fonts(pdf_bytes, page_number=0)
print(f"Found {len(results.fonts)} font instances")
for font in results.fonts:
    print(f"Font: {font.family}, Size: {font.size}pt")
```

### Complete Quality Verification

```python
from vec_visual_font_identification.quality import QualityVerifier

# Initialize verifier
verifier = QualityVerifier()

# Verify complete document
report = verifier.verify_document(pdf_bytes)
print(f"Overall Quality Score: {report.overall_score:.2f}")
print(f"Issues Found: {len(report.issues)}")
```

## CSnakes Integration

For C# integration via CSnakes.Runtime:

```python
# Main entry point for CSnakes
def detect_logos_csnakes(pdf_bytes: bytes) -> dict:
    """CSnakes-compatible function for logo detection."""
    detector = LogoDetector()
    results = detector.detect_logos(pdf_bytes)
    return results.model_dump()  # Pydantic to dict

def detect_fonts_csnakes(pdf_bytes: bytes) -> dict:
    """CSnakes-compatible function for font detection."""
    detector = FontDetector()
    results = detector.detect_fonts(pdf_bytes)
    return results.model_dump()

def verify_quality_csnakes(pdf_bytes: bytes) -> dict:
    """CSnakes-compatible function for quality verification."""
    verifier = QualityVerifier()
    report = verifier.verify_document(pdf_bytes)
    return report.model_dump()
```

## Testing

```bash
# Run all tests
pytest

# Run with coverage
pytest --cov=vec_visual_font_identification --cov-report=html

# Run specific test file
pytest tests/unit/test_logo_detector.py

# Run integration tests
pytest tests/integration/
```

## Model Training

See `training/` directory for model fine-tuning scripts.

## Architecture Principles

1. **Clean Architecture:** Separation of concerns (models, business logic, infrastructure)
2. **Type Safety:** Pydantic models for all data structures
3. **Testability:** Comprehensive unit and integration tests
4. **Performance:** Model loading once, reuse across requests
5. **Error Handling:** Structured error responses (Pydantic models)
6. **CSnakes Compatibility:** JSON-serializable outputs for C# interop

## Dependencies

- **transformers:** HuggingFace Transformers (CLIP, TrOCR)
- **torch:** PyTorch for model inference
- **pydantic:** Data validation and serialization
- **pytest:** Testing framework
- **pdfplumber/pymupdf:** PDF text extraction
- **pillow:** Image processing
- **opencv-python:** Image quality analysis

## License

Internal use only - ExxerCube.Prisma.Veriqan
