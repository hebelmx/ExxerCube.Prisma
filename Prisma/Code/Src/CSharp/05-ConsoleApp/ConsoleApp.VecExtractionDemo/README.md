# VEC Statement Extraction Console Demo

Console application for testing VEC statement extraction with CSnakes Python interop.

## Overview

This demo follows the **proven reliable pattern** from GOT-OCR2 Console Demo:
- Manual Python environment setup FIRST
- Exact versions (`==`) for all dependencies (including transitive)
- Create venv and install requirements manually
- CSnakes finds everything already satisfied when it starts

## Prerequisites

- **.NET SDK** (for running C# console app)
- **Python 3.13.1** (installed and in PATH)
- **10GB+ disk space** (for PyTorch and transformers models)
- **Internet connection** (for downloading models on first run)

## Quick Start (Manual Setup - Recommended)

### Step 1: Set Up Python Environment Manually

```powershell
# Navigate to Python module directory
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Fixtures\PRP2\python

# Run manual setup script
.\setup_environment_manual.ps1

# This will:
# - Check Python installation
# - Create virtual environment (.venv_vec)
# - Install ALL dependencies with exact versions
# - Verify installation
```

**Expected output:**
```
=== VEC Extraction - Manual Python Environment Setup ===

[1/6] Checking Python installation...
  ✓ Found: Python 3.13.1 at C:\Users\...\python.exe

[2/6] Creating virtual environment...
  ✓ Virtual environment created

[3/6] Activating virtual environment...
  ✓ Venv Python: ...\python\Scripts\python.exe
  ✓ Venv Pip: ...\python\Scripts\pip.exe

[4/6] Upgrading pip, setuptools, wheel...
  ✓ pip 24.3.1 from ...

[5/6] Installing requirements with EXACT versions...
  This may take 10-30 minutes (downloading PyTorch ~2GB + transformers)
  ✓ Requirements installed successfully

[6/6] Verifying installation...
  ✓ torch: 2.5.1
  ✓ transformers: 4.46.3
  ✓ pydantic: 2.10.3
  ✓ pdfplumber: 0.11.4
  ✓ PIL: 11.0.0
  ✓ cv2: 4.10.0.84

  ✓ All critical packages verified!

=== Setup Complete ===
Environment is ready for CSnakes integration!
```

### Step 2: Run Console Demo

```powershell
# Navigate to console demo
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\05-ConsoleApp\ConsoleApp.VecExtractionDemo

# Run with test fixture
dotnet run

# Or run with specific PDF
dotnet run "C:\path\to\vec_statement.pdf"
```

## Expected Console Output

```
=== ExxerCube.Prisma - VEC Statement Extraction Demo ===

[INFO] Python library path: ...\bin\Debug\net9.0\python
[INFO] Initializing Python environment (this may take several minutes on first run)...
[INFO] - Downloading Python 3.13 redistributable if needed
[INFO] - Creating virtual environment
[INFO] - Installing PyTorch, transformers, and VEC processing dependencies
[INFO] ✓ Python environment initialized

--- Health Check ---
✓ Module imported successfully
✓ Health check PASSED - VEC extraction module loaded successfully

--- VEC Statement Extraction Demo ---
[INFO] Using fixture PDF: 01+Dummie+VEC+jul_ago+20252.pdf
[INFO] PDF contains 3 page(s)
[INFO] Extracting VEC statement data (this may take 10-30 seconds)...
[INFO] - Running CLIP logo detection
[INFO] - Running font detection (Aptos compliance)
[INFO] - Running LayoutLMv3 header extraction
[INFO] - Running Table Transformer transaction extraction

--- Extraction Results ---
✓ SUCCESS - Processing time: 12.34s

Extracted Information:
--------------------------------------------------------------------------------
Document ID: VEC-DEMO-20251207143000
Total Pages: 3
Confidence: 0.95
Model: layoutlmv3-microsoft/layoutlmv3-base

Header Fields:
  Account: VEC-000000-00
  Holder: PLACEHOLDER NAME
  Product: Vista
  Balance: $0.00

Transactions: 0 found

Visual Compliance:
  Logo Detected: False
  Image Quality: 1.00
  Font Compliance: True
--------------------------------------------------------------------------------

Demo completed successfully
```

## Directory Structure

```
ConsoleApp.VecExtractionDemo/
├── Program.cs                          # Main console application
├── GlobalUsings.cs                     # Global using directives
├── ExxerCube.Prisma.ConsoleApp.VecExtractionDemo.csproj
├── README.md                           # This file
└── bin/Debug/net9.0/
    ├── python/                         # Python module (copied by build)
    │   ├── vec_visual_font_identification/
    │   │   ├── csnakes_integration.py  # CSnakes entry points
    │   │   ├── extraction/             # LayoutLMv3, Table Transformer
    │   │   ├── visual/                 # CLIP logo detection
    │   │   ├── font/                   # Font compliance
    │   │   └── models/                 # Pydantic models
    │   └── requirements.txt            # Exact versions
    ├── test_fixtures/                  # VEC PDF fixtures
    └── .venv_vec/                      # Virtual environment (created by setup)
```

## Manual Setup Script Details

The `setup_environment_manual.ps1` script performs the following steps:

### 1. Python Installation Check
- Looks for `python3` or `python` in PATH
- Verifies version compatibility (requires 3.13.x)
- Shows installation instructions if not found

### 2. Virtual Environment Creation
- Creates `.venv_vec` directory
- Uses `python -m venv` (standard library)
- Can force recreate with `-Force` flag

### 3. Pip Upgrade
- Upgrades pip to latest version
- Upgrades setuptools and wheel
- Ensures clean installation environment

### 4. Requirements Installation
- Installs with **exact versions** (`==`)
- Includes **ALL transitive dependencies**
- No dependency conflicts (all explicitly stated)

### 5. Verification
- Imports all critical packages
- Checks versions match requirements
- Reports any installation issues

## Troubleshooting

### Issue: Python not found

**Solution:**
```powershell
# Install Python 3.13.1 from:
https://www.python.org/downloads/

# Or use Windows Store:
winget install Python.Python.3.13

# Verify installation:
python --version  # Should show Python 3.13.x
```

### Issue: Pip installation fails

**Symptoms:**
```
ERROR: Could not find a version that satisfies the requirement torch==2.5.1
```

**Solutions:**
1. Check internet connection
2. Try without `--no-deps`:
   ```powershell
   cd Prisma\Fixtures\PRP2\python
   .\.venv_vec\Scripts\pip.exe install -r requirements.txt
   ```
3. Install PyTorch separately first:
   ```powershell
   .\.venv_vec\Scripts\pip.exe install torch==2.5.1 torchvision==0.20.1 --index-url https://download.pytorch.org/whl/cu130
   .\.venv_vec\Scripts\pip.exe install -r requirements.txt
   ```

### Issue: CSnakes can't find Python module

**Symptoms:**
```
ModuleNotFoundError: No module named 'vec_visual_font_identification'
```

**Solution:**
1. Verify Python files copied to output:
   ```powershell
   ls bin\Debug\net9.0\python\vec_visual_font_identification
   ```
2. Rebuild project to copy files:
   ```powershell
   dotnet clean
   dotnet build
   ```

### Issue: Models downloading on every run

**Solution:**
- Models are cached in `~/.cache/huggingface/`
- First run downloads ~3-5GB of models
- Subsequent runs reuse cached models
- Check disk space: `du -sh ~/.cache/huggingface/`

### Issue: Out of memory during model loading

**Symptoms:**
```
RuntimeError: CUDA out of memory
```

**Solution:**
1. Close other applications
2. Use CPU instead of GPU (automatic fallback)
3. Process one page at a time
4. Reduce batch size in configuration

## Requirements.txt Philosophy

Following the **proven reliable approach**, our `requirements.txt`:

✅ **DO:**
- Use **exact versions** (`==`) for ALL packages
- Explicitly state ALL transitive dependencies
- Group by category for clarity
- Include comments explaining why

❌ **DON'T:**
- Use version ranges (`>=`, `~=`)
- Rely on pip dependency resolution
- Install without verifying exact versions
- Mix production and dev dependencies

### Example:

```txt
# Direct dependency
torch==2.5.1

# Transitive dependencies of torch (explicitly stated)
sympy==1.13.1
networkx==3.4.2
jinja2==3.1.4
markupsafe==3.0.2  # Transitive of jinja2
```

## Performance Benchmarks

| Operation | Time (CPU) | Time (GPU) |
|-----------|-----------|-----------|
| Logo Detection (CLIP) | 2-3s/page | 0.5-1s/page |
| Font Detection | 1-2s/page | 1-2s/page |
| Header Extraction (LayoutLMv3) | 3-5s | 1-2s |
| Transaction Extraction (Table Transformer) | 5-8s | 2-3s |
| **Total per Statement** | **15-25s** | **5-10s** |

Target: <30 seconds per statement (95th percentile)

## Next Steps

1. **Test with Real VEC PDFs**
   - Place PDFs in `test_fixtures/` directory
   - Run console demo
   - Verify extraction accuracy

2. **Integrate with Domain Layer**
   - Create `IVecStatementExtractor` interface
   - Implement in Infrastructure layer
   - Add to Application services

3. **Add Validation Engine**
   - Implement 55 validation rules
   - Generate marked PDFs
   - Create email alerts

4. **Performance Optimization**
   - Batch processing
   - Model caching improvements
   - GPU acceleration tuning

## References

- **GOT-OCR2 Console Demo**: `ConsoleApp.GotOcr2Demo/` (proven pattern)
- **CSnakes Documentation**: https://github.com/tonybaloney/CSnakes
- **Python Module**: `Prisma/Fixtures/PRP2/python/`
- **Test Fixtures**: `Prisma/Fixtures/PRP2/python/tests/fixtures/`

## License

Internal use only - ExxerCube.Prisma.Veriqan
