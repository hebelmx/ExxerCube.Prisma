# OCR Pipeline Manual & Developer Documentation

## Overview

The `ocr_pipeline.py` script is a specialized OCR (Optical Character Recognition) pipeline designed for processing Spanish legal documents, particularly those with red diagonal watermarks. It combines image preprocessing, OCR extraction, and intelligent field parsing to extract structured data from legal documents.

## Features

### Core Capabilities
- **Red Watermark Removal**: Automatically detects and removes red diagonal watermarks
- **Document Deskewing**: Corrects document rotation for better OCR accuracy
- **Spanish OCR**: Optimized for Spanish text with English fallback
- **Legal Field Extraction**: Automatically extracts common legal document sections
- **Structured Output**: Generates both raw text (.txt) and structured JSON (.json) files

### Supported Formats
- **Images**: PNG, JPG, JPEG, TIFF, BMP
- **PDFs**: Multi-page PDF support (requires pdf2image)
- **Batch Processing**: Process entire folders of documents

## Installation & Dependencies

### System Requirements
```bash
# Ubuntu/Debian
sudo apt-get install tesseract-ocr tesseract-ocr-spa poppler-utils

# For PDF support, install poppler
sudo apt-get install poppler-utils
```

### Python Dependencies
```bash
# Using uv (recommended)
uv add opencv-python pillow pytesseract numpy unidecode

# Optional: For PDF support
uv add pdf2image
```

### Verify Installation
```bash
# Check tesseract and Spanish language pack
tesseract --version
tesseract --list-langs  # Should include 'spa'

# Test dependencies
uv run python -c "import cv2, pytesseract, numpy, PIL, unidecode; print('All dependencies OK')"
```

## Usage

### Basic Usage
```bash
# Single document
uv run python ocr_pipeline.py --input document.pdf --outdir ./output

# Folder of documents
uv run python ocr_pipeline.py --input ./documents/ --outdir ./output --verbose

# With verbose logging
uv run python ocr_pipeline.py --input file.png --outdir ./results --verbose
```

### Command Line Arguments
- `--input`: Path to image/PDF file or folder containing documents
- `--outdir`: Output directory for generated files
- `--verbose`: Enable detailed logging and warnings

### Output Files
For each processed document, the pipeline generates:
- `{filename}.txt`: Raw extracted text
- `{filename}.json`: Structured data with extracted fields

For multi-page PDFs:
- `{filename}_p1.txt`, `{filename}_p1.json`: Page 1
- `{filename}_p2.txt`, `{filename}_p2.json`: Page 2, etc.

## Technical Architecture

### Processing Pipeline

1. **Image Loading**
   - Single images: Direct OpenCV loading
   - PDFs: Convert to images using pdf2image (300 DPI)

2. **Preprocessing**
   - Red watermark detection and removal using HSV color space
   - Document deskewing using minimum area rectangle
   - Adaptive thresholding (Sauvola/Niblack or Gaussian fallback)

3. **OCR Processing**
   - Primary: Spanish language model (`spa`)
   - Fallback: English language model (`eng`)
   - Configuration: `--oem 1 --psm 6` (LSTM + uniform text block)

4. **Field Extraction**
   - Legal sections identification
   - Date parsing (Spanish format)
   - Monetary amount extraction
   - Confidence scoring

### Key Algorithms

#### Red Watermark Removal
```python
# HSV color space thresholding for red detection
mask1 = (H < 10) & (S > 80) & (V > 80)      # Red hue lower range
mask2 = (H > 170) & (S > 80) & (V > 80)     # Red hue upper range
# Inpainting to fill detected regions
```

#### Document Deskewing
```python
# Find minimum area rectangle of text regions
rect = cv2.minAreaRect(text_coordinates)
angle = rect[-1]  # Extract rotation angle
# Apply rotation transformation
```

## Extracted Fields

### JSON Output Structure
```json
{
  "expediente": "Case number/file reference",
  "causa": "Extracted 'CAUSA QUE MOTIVA EL REQUERIMIENTO' section",
  "accion_solicitada": "Extracted 'ACCIÓN SOLICITADA' section",
  "fechas": ["2023-01-01", "2025-08-13"],
  "montos": [
    {
      "moneda": "MXN",
      "valor": 100000.00
    }
  ],
  "confianza_ocr": 0.896
}
```

### Field Detection Logic

#### Legal Sections
- **Causa**: Searches for variants of "CAUSA QUE MOTIVA EL REQUERIMIENTO"
- **Acción**: Searches for variants of "ACCIÓN SOLICITADA"
- Uses fuzzy matching with unidecode normalization

#### Date Extraction
- Pattern: `(\d{1,2})\s+de\s+([A-Za-zñÑáéíóúÁÉÍÓÚ]+)\s+de\s+(\d{4})`
- Spanish months: enero, febrero, marzo, etc.
- Output format: YYYY-MM-DD

#### Monetary Amounts
- Pattern: `\$[\s]*([0-9]{1,3}(?:[.,][0-9]{3})*|[0-9]+)(?:[.,]([0-9]{2}))?`
- Handles Mexican peso format with thousands separators
- Normalizes to decimal format

## Configuration & Customization

### Modifying Section Headers
```python
# Add new aliases for legal sections
HEADER_ALIASES_CAUSA = [
    "CAUSA QUE MOTIVA EL REQUERIMIENTO",
    "YOUR_CUSTOM_HEADER",
    # Add more variants
]
```

### OCR Configuration
```python
# Modify tesseract parameters
config = "--oem 1 --psm 6"  # Current setting
# Alternative configurations:
# "--oem 1 --psm 3"  # Fully automatic page segmentation
# "--oem 1 --psm 1"  # Automatic page segmentation with OSD
```

### Preprocessing Tuning
```python
# Red watermark detection thresholds
mask1 = (H < 10) & (S > 80) & (V > 80)     # Adjust S/V for sensitivity
mask2 = (H > 170) & (S > 80) & (V > 80)    # Adjust S/V for sensitivity

# Morphological operations
kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3,3))  # Adjust size
```

## Performance & Quality

### Typical Results
- **OCR Confidence**: 85-95% for clean documents
- **Processing Speed**: ~2-5 seconds per page
- **Memory Usage**: ~100-200MB per document

### Quality Indicators
- **High Quality**: Confidence > 90%
- **Good Quality**: Confidence 70-90%
- **Review Needed**: Confidence < 70%

### Common Issues & Solutions

#### Low OCR Confidence
```bash
# Solutions:
1. Check image resolution (minimum 300 DPI recommended)
2. Verify document orientation
3. Ensure adequate lighting/contrast
4. Check for heavy watermarks or artifacts
```

#### Missing Field Extraction
```bash
# Debugging:
1. Check section header variations in document
2. Verify Spanish language pack installation
3. Review text normalization (accents, special characters)
4. Add custom header aliases if needed
```

#### PDF Processing Errors
```bash
# Requirements:
1. Install poppler: sudo apt-get install poppler-utils
2. Install pdf2image: uv add pdf2image
3. Check PDF permissions (encrypted PDFs may fail)
```

## Development & Extension

### Adding New Field Types
```python
def extract_custom_field(text: str) -> Optional[str]:
    # Add your custom extraction logic
    pattern = r"YOUR_REGEX_PATTERN"
    match = re.search(pattern, text, re.IGNORECASE)
    return match.group(1) if match else None

# Add to ExtractedFields dataclass
@dataclass
class ExtractedFields:
    # ... existing fields ...
    custom_field: Optional[str]
```

### Custom Preprocessing
```python
def custom_preprocess(bgr: np.ndarray) -> np.ndarray:
    # Add your custom preprocessing steps
    # Example: additional noise removal, contrast enhancement
    cleaned = your_custom_function(bgr)
    return cleaned
```

### Batch Processing Enhancements
```python
# Add progress tracking
from tqdm import tqdm

for p in tqdm(inputs, desc="Processing documents"):
    try:
        process_file(p, args.outdir, verbose=args.verbose)
    except Exception as e:
        print(f"[ERROR] {p}: {e}", file=sys.stderr)
```

## Testing & Validation

### Quality Assurance
```bash
# Test with known documents
uv run python ocr_pipeline.py --input test_documents/ --outdir test_output --verbose

# Verify extracted fields
python -c "
import json
with open('test_output/document.json') as f:
    data = json.load(f)
    print(f'Confidence: {data[\"confianza_ocr\"]}')
    print(f'Fields found: {len([k for k,v in data.items() if v])}')
"
```

### Performance Benchmarking
```python
import time

start_time = time.time()
process_file(document_path, output_dir)
processing_time = time.time() - start_time
print(f"Processing time: {processing_time:.2f} seconds")
```

## Troubleshooting

### Common Error Messages

#### "pytesseract not available"
```bash
# Solution:
uv add pytesseract
```

#### "PDF support requires pdf2image"
```bash
# Solution:
uv add pdf2image
sudo apt-get install poppler-utils
```

#### "Could not read image"
```bash
# Causes:
1. Unsupported format
2. Corrupted file
3. Insufficient permissions

# Check file:
file your_document.pdf
ls -la your_document.pdf
```

#### Low confidence warnings
```bash
# When you see: "[WARN] Sections not fully detected"
# Solutions:
1. Check document quality
2. Verify section headers match expected patterns
3. Add custom header aliases
4. Review preprocessing results
```

### Debug Mode
```python
# Add debug image saving to troubleshoot preprocessing
def debug_preprocess(bgr: np.ndarray, output_path: str) -> np.ndarray:
    cleaned = remove_red_watermark(bgr)
    cv2.imwrite(f"{output_path}_1_cleaned.png", cleaned)
    
    gray = cv2.cvtColor(cleaned, cv2.COLOR_BGR2GRAY)
    cv2.imwrite(f"{output_path}_2_gray.png", gray)
    
    deskewed = deskew(gray)
    cv2.imwrite(f"{output_path}_3_deskewed.png", deskewed)
    
    binary = sauvola_threshold(deskewed)
    cv2.imwrite(f"{output_path}_4_binary.png", binary)
    
    return cv2.bitwise_not(binary)
```

## License & Support

This script is designed for processing legal documents and should be used in compliance with applicable data protection and privacy laws. Ensure proper handling of sensitive legal information.

For technical support or feature requests, refer to the project documentation or contact the development team.