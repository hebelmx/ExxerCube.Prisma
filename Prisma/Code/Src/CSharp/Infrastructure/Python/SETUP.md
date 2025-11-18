# Python Module Setup Guide

## Prisma OCR Wrapper Module

The `prisma_ocr_wrapper` Python module is used by CSnakes for OCR processing. This guide explains how to ensure the module is available for tests and runtime.

## Module Location

The Python module is located at:
- Source: `Infrastructure/Python/python/prisma_ocr_wrapper.py`
- Output: Copied to `bin/Debug/net10.0/Python/python/prisma_ocr_wrapper.py` during build

## Dependencies

The module depends on `ocr_modules` from `Prisma/Code/Src/Python/prisma-ocr-pipeline/src/ocr_modules/`.

## Troubleshooting "No module named 'prisma_ocr_wrapper'"

If you encounter this error:

1. **CSnakes Code Generation (Build-time)**: CSnakes needs to import `ocr_modules` during code generation to analyze types. 

   **Recommended**: Use the provided build script that automatically sets PYTHONPATH:
   ```powershell
   .\build-infrastructure.ps1
   ```
   
   **Manual Setup**: If building manually, ensure:
   - Set `PYTHONPATH` environment variable before building to include `Prisma/Code/Src/Python/prisma-ocr-pipeline/src`
   - Or ensure `ocr_modules` is available in Python's default search path during build
   - The Python file must be parseable by CSnakes - avoid complex conditional imports that confuse the generator

2. **Runtime Module Availability**: At runtime, ensure:
   - `prisma_ocr_wrapper.py` exists in output directory: `bin/Debug/net10.0/Python/python/prisma_ocr_wrapper.py`
   - `ocr_modules` is in Python path (configured by `PrismaPythonEnvironment`)
   - Python environment is properly initialized

3. **Verify ocr_modules dependency**: Ensure the `ocr_modules` package is available:
   - Location: `Prisma/Code/Src/Python/prisma-ocr-pipeline/src/ocr_modules/`
   - Must be importable by Python at both build-time (for CSnakes) and runtime

4. **Rebuild the project**: CSnakes generates code during build. If the module isn't found:
   - **Use the build script**: `.\build-infrastructure.ps1` (recommended - handles PYTHONPATH automatically)
   - Or manually: Clean and rebuild the solution with PYTHONPATH set
   - Ensure CSnakes source generation completed successfully
   - Check build output for CSnakes generator errors
   - Verify the project file has CSnakes configuration properties (EmbedPythonSources, DefaultPythonItems, PythonRoot)

## Test Setup

For tests that use the Python module:

1. Ensure the Python environment is initialized before tests run
2. The module will be automatically discovered if it's in the output directory
3. If tests fail with module not found, verify the test project references the Infrastructure project correctly

## Environment Variables

You can configure the Python virtual environment location:
- `PRISMA_PYTHON_VENV_PATH`: Override the default venv location (default: `%LOCALAPPDATA%\PrismaPython\venv`)

## Required Python Packages

The following packages are automatically installed:
- pytesseract
- Pillow
- opencv-python
- numpy
- pandas
- python-dateutil
- regex

These are installed automatically when the Python environment is first initialized.

