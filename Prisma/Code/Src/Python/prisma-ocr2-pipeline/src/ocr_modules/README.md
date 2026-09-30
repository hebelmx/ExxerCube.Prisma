# Modular OCR Pipeline

A decomposed, testable OCR pipeline following Single Responsibility Principle (SRP) and functional programming principles.

## Architecture Overview

The original monolithic `ocr_pipeline.py` has been decomposed into 14 focused modules:

### 📊 Data Models (`models.py`)
- **Single Responsibility**: Define type-safe data structures
- **Key Models**: `ImageData`, `OCRResult`, `ExtractedFields`, `ProcessingConfig`
- **Type Safety**: Pydantic models with validation

### 📁 File Operations
- **`file_loader.py`**: Load images and PDFs with metadata
- **`output_writer.py`**: Persist results to text and JSON files

### 🖼️ Image Processing (Pure Functions)
- **`watermark_remover.py`**: Remove red diagonal watermarks
- **`image_deskewer.py`**: Detect and correct document skew
- **`image_binarizer.py`**: Apply adaptive thresholding

### 🔤 OCR Execution
- **`ocr_executor.py`**: Execute Tesseract with fallback languages

### 📝 Text Processing (Pure Functions)
- **`text_normalizer.py`**: Clean and normalize OCR output
- **`section_extractor.py`**: Extract document sections by headers
- **`expediente_extractor.py`**: Extract case file identifiers
- **`date_extractor.py`**: Extract and normalize Spanish dates
- **`amount_extractor.py`**: Extract monetary amounts

### 🔧 Pipeline Orchestration
- **`pipeline.py`**: Compose all modules into complete workflow
- **`__init__.py`**: Package interface and exports

## Key Principles Implemented

### ✅ Single Responsibility Principle (SRP)
- Each module has one clear purpose
- Functions are focused and do one thing well
- Easy to understand, test, and maintain

### ✅ Functional Programming Style
- Pure functions: same input → same output
- No hidden side effects (except file I/O)
- Composable and predictable behavior

### ✅ Type Safety with Pydantic
- Runtime type validation
- Clear data structure definitions
- Automatic serialization/deserialization

### ✅ Comprehensive Testing
- Unit tests for each module
- Mocked dependencies where appropriate
- Edge cases and error conditions covered

## Usage Examples

### Basic Usage
```python
from ocr_modules import process_path

# Process with default configuration
results = process_path("/path/to/image.jpg", output_directory="/output")

for result in results:
    print(f"Confidence: {result.ocr_result.confidence_avg:.1f}%")
    print(f"Expediente: {result.extracted_fields.expediente}")
```

### Custom Configuration
```python
from ocr_modules import process_path, ProcessingConfig, OCRConfig

# Create custom configuration
config = ProcessingConfig(
    remove_watermark=True,
    deskew=True,
    binarize=True,
    ocr_config=OCRConfig(language="eng", fallback_language="spa"),
    extract_sections=True,
    normalize_text=True
)

results = process_path("/path/to/documents/", config, "/output")
```

### Using Individual Modules
```python
from ocr_modules.watermark_remover import remove_red_watermark
from ocr_modules.date_extractor import extract_dates
from ocr_modules.amount_extractor import extract_amounts

# Use modules individually
cleaned_image = remove_red_watermark(image)
dates = extract_dates("El 15 de octubre de 2023")
amounts = extract_amounts("El monto es $1,500.75")
```

## CLI Usage

Use the new modular CLI that replaces the original script:

```bash
# Basic usage (same interface as original)
python modular_ocr_cli.py --input /path/to/image.jpg --outdir ./output

# With custom options
python modular_ocr_cli.py \
    --input /path/to/documents/ \
    --outdir ./output \
    --language eng \
    --no-watermark-removal \
    --verbose

# Advanced configuration
python modular_ocr_cli.py \
    --input document.pdf \
    --outdir ./results \
    --language spa \
    --fallback-language eng \
    --oem 1 \
    --psm 6 \
    --verbose
```

## Testing

Run the comprehensive test suite:

```bash
# Run all tests
uv run pytest ocr_modules/tests/

# Run specific test modules
uv run pytest ocr_modules/tests/test_models.py -v
uv run pytest ocr_modules/tests/test_text_processing.py -v
uv run pytest ocr_modules/tests/test_image_processing.py -v

# Run with coverage
uv run pytest ocr_modules/tests/ --cov=ocr_modules --cov-report=html
```

## Module Dependencies

```
models.py (base types)
├── file_loader.py
├── watermark_remover.py
├── image_deskewer.py  
├── image_binarizer.py
├── ocr_executor.py
├── text_normalizer.py
├── section_extractor.py
├── expediente_extractor.py
├── date_extractor.py
├── amount_extractor.py
├── output_writer.py
└── pipeline.py (orchestrates all)
```

## File Structure

```
ocr_modules/
├── __init__.py                 # Package interface
├── models.py                   # Pydantic data models
├── file_loader.py             # File and image loading
├── watermark_remover.py       # Red watermark removal
├── image_deskewer.py          # Document deskewing
├── image_binarizer.py         # Image binarization
├── ocr_executor.py            # Tesseract execution
├── text_normalizer.py         # Text cleaning
├── section_extractor.py       # Document section extraction
├── expediente_extractor.py    # Case ID extraction
├── date_extractor.py          # Date extraction/normalization
├── amount_extractor.py        # Monetary amount extraction
├── output_writer.py           # Result persistence
├── pipeline.py                # Main orchestrator
├── README.md                  # This documentation
└── tests/                     # Test suite
    ├── __init__.py
    ├── test_models.py
    ├── test_text_processing.py
    ├── test_image_processing.py
    └── test_file_operations.py
```

## Migration from Original

The modular pipeline maintains **100% compatibility** with the original script:

1. **Same functionality**: All original features preserved
2. **Same interface**: CLI arguments unchanged
3. **Same output**: TXT and JSON files in identical format
4. **Same dependencies**: No new external dependencies
5. **Better maintainability**: Each component is now testable and focused

## Benefits

✅ **Testable**: Each module has comprehensive unit tests  
✅ **Maintainable**: Clear separation of concerns  
✅ **Extensible**: Easy to add new processing steps  
✅ **Reusable**: Modules can be used independently  
✅ **Type-Safe**: Pydantic models prevent runtime errors  
✅ **Pure Functions**: Predictable, side-effect-free behavior  
✅ **Professional**: Follows industry best practices