# Getting Started - VEC Python Visual & Font Identification

## Overview

This Python package provides visual identification (logo detection) and font identification capabilities for VEC statement processing. It uses HuggingFace Transformers (CLIP), PyTorch, Pydantic, and pytest following clean architecture principles.

## What Was Created

### 1. User Stories (`stories/python-visual-font-identification.md`)
- **PYTHON-001:** Logo Detection Model (CLIP-based)
- **PYTHON-002:** Font Detection Model
- **PYTHON-003:** Quality Verification Orchestration
- **PYTHON-004:** CLIP Fine-tuning Infrastructure
- **PYTHON-005:** Font Classifier Training
- **PYTHON-006:** CSnakes Integration Wrapper

### 2. Package Structure

```
python/
├── vec_visual_font_identification/     # Main package
│   ├── models/                         # Pydantic data models
│   │   ├── logo_detection.py          # Logo detection models
│   │   ├── font_detection.py          # Font detection models
│   │   └── quality_report.py          # Quality report models
│   ├── visual/                         # Visual identification
│   │   ├── logo_detector.py           # CLIP-based logo detection
│   │   ├── image_quality.py           # Image quality analysis
│   │   └── visual_compliance.py       # Visual compliance checker
│   ├── font/                           # Font identification
│   │   ├── font_detector.py           # Font detection from PDF
│   │   ├── typography_analyzer.py     # Typography metrics
│   │   └── overlap_detector.py        # Text overlap detection
│   ├── quality/                        # Quality orchestration
│   │   └── quality_verifier.py         # Complete quality check
│   ├── utils/                          # Utilities
│   │   └── image_utils.py             # PDF to image conversion
│   └── csnakes_integration.py         # CSnakes wrapper for C#
├── tests/                              # Test suite
│   ├── unit/                           # Unit tests
│   └── integration/                   # Integration tests
├── requirements.txt                    # Production dependencies
├── requirements-dev.txt               # Development dependencies
└── setup.py                           # Package setup
```

## Installation

### Prerequisites
- Python 3.9+
- pip
- (Optional) CUDA for GPU acceleration

### Setup Steps

1. **Create virtual environment:**
```bash
cd python
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate
```

2. **Install dependencies:**
```bash
pip install -r requirements.txt
pip install -r requirements-dev.txt
```

3. **Install package in development mode:**
```bash
pip install -e .
```

## Usage Examples

### Logo Detection

```python
from vec_visual_font_identification.visual import LogoDetector

# Initialize detector (loads CLIP model)
detector = LogoDetector()

# Detect logos in PDF
with open("statement.pdf", "rb") as f:
    pdf_bytes = f.read()

result = detector.detect_logos(pdf_bytes)
print(f"Found {len(result.logos)} logos")
for logo in result.logos:
    print(f"Logo: {logo.family}, Confidence: {logo.confidence:.2f}")
    print(f"Quality Score: {logo.quality.clarity_score:.2f}")
```

### Font Detection

```python
from vec_visual_font_identification.font import FontDetector

# Initialize detector
detector = FontDetector(
    approved_fonts={"Arial", "Times New Roman", "Aptos"},
    font_size_ranges={
        "header": (12, 16),
        "body": (9, 11),
        "footnote": (7, 8),
    }
)

# Detect fonts in PDF
result = detector.detect_fonts(pdf_bytes)
print(f"Found {len(result.fonts)} font instances")
for font in result.fonts:
    print(f"Font: {font.family}, Size: {font.size}pt, Approved: {font.is_approved}")
```

### Complete Quality Verification

```python
from vec_visual_font_identification.quality import QualityVerifier

# Initialize verifier
verifier = QualityVerifier()

# Verify complete document
report = verifier.verify_document(pdf_bytes, document_id="VEC-001")
print(f"Overall Quality Score: {report.overall_score:.2f}")
print(f"Issues Found: {len(report.issues)}")
for issue in report.issues:
    print(f"{issue.code.value}: {issue.message} (Severity: {issue.severity.value})")
```

### CSnakes Integration (for C#)

```python
from vec_visual_font_identification.csnakes_integration import (
    detect_logos_csnakes,
    detect_fonts_csnakes,
    verify_quality_csnakes,
)

# CSnakes-compatible functions return dictionaries
logo_result = detect_logos_csnakes(pdf_bytes)
font_result = detect_fonts_csnakes(pdf_bytes)
quality_report = verify_quality_csnakes(pdf_bytes, document_id="VEC-001")
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
pytest tests/integration/ -m integration
```

## Architecture Principles

1. **Clean Architecture:** Separation of models, business logic, and infrastructure
2. **Type Safety:** Pydantic models for all data structures
3. **Testability:** Comprehensive unit and integration tests
4. **Performance:** Model loading once, reuse across requests (singleton pattern)
5. **Error Handling:** Structured error responses (Pydantic models)
6. **CSnakes Compatibility:** JSON-serializable outputs for C# interop

## Next Steps

### Phase 1: Core Implementation (Weeks 1-2)
1. ✅ Package structure created
2. ✅ Pydantic models defined
3. ✅ Logo detector skeleton (needs fine-tuning)
4. ✅ Font detector skeleton (needs testing with real PDFs)
5. ⚠️ Add real PDF fixtures for testing
6. ⚠️ Implement missing visual_compliance.py
7. ⚠️ Test with actual VEC PDFs

### Phase 2: Model Training (Weeks 3-4)
1. ⚠️ Collect 100-200 anonymized VEC statements
2. ⚠️ Annotate logos (bounding boxes)
3. ⚠️ Fine-tune CLIP on VEC logos
4. ⚠️ Train font classifier
5. ⚠️ Evaluate model accuracy

### Phase 3: Integration (Week 5)
1. ⚠️ Test CSnakes integration from C#
2. ⚠️ Performance optimization
3. ⚠️ Load testing

## Performance Targets

- **Logo Detection:** <2 seconds per page
- **Font Detection:** <3 seconds per page
- **Complete Quality Check:** <10 seconds per document
- **Logo Detection Accuracy:** ≥95%
- **Font Detection Accuracy:** ≥90%

## Dependencies

### Core
- `transformers>=4.35.0` - HuggingFace Transformers (CLIP)
- `torch>=2.1.0` - PyTorch
- `pydantic>=2.5.0` - Data validation

### PDF Processing
- `pdfplumber>=0.10.0` - PDF text extraction
- `pdf2image>=1.16.0` - PDF to image conversion

### Image Processing
- `pillow>=10.1.0` - Image manipulation
- `opencv-python>=4.8.0` - Image quality analysis

### Spatial Analysis
- `shapely>=2.0.0` - Overlap detection

## Known Limitations

1. **Logo Detection:** Currently uses center position approximation. Fine-tuned CLIP with bounding box regression needed for accurate position detection.
2. **Font Detection:** Typography metrics (line spacing, character spacing) are simplified. Full implementation requires text layout analysis.
3. **Layout Verification:** Placeholder implementation. Needs full layout analysis.
4. **Marketing Compliance:** Placeholder implementation. Needs brand guideline integration.

## Support

For questions or issues, refer to:
- User Stories: `stories/python-visual-font-identification.md`
- Architecture Document: `architecture.md`
- PRP Requirements: `PRP.md`
