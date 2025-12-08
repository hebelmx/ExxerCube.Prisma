# GROUND TRUTH: Working GOT-OCR2 Configuration
## As of 2025-11-24 - ALL TESTS PASSING ✅

**Status:** ✅ **ALL 10 TESTS PASSING** - PDF text extraction working!
**Latest Update:** 2025-11-24 16:03 - Fixed xUnit Collection Fixture for Python environment sharing

This document records the EXACT working configuration, independent of how we got here.
Use this as the source of truth for reproducing on workstation with GPU.

---

## Environment Verified Working

### Hardware
- **System:** Laptop (slow, CPU-only for now)
- **GPU:** None detected by torch (CPU fallback working)
- **RAM:** Sufficient for model loading

### Software Versions (EXACT - DO NOT CHANGE)

```
OS: Windows 11
.NET SDK: 10.0 (net10.0 target framework)
Python: 3.13.2 (CRITICAL - must match CSnakes redistributable exactly)
CSnakes: 1.2.1 (via NuGet, stable version)
```

### Python Packages (EXACT versions from working venv)

```
Python 3.13.2

Core Packages:
- torch==2.9.1 (CPU version, no +cu130 suffix in this environment)
- torchvision==0.24.1
- transformers==4.57.1
- numpy==2.3.5
- Pillow==12.0.0

PDF Processing (CRITICAL):
- PyMuPDF==1.26.6 (fitz module)

Supporting:
- accelerate==1.12.0
- huggingface-hub==0.36.0
- safetensors==0.7.0
- pyyaml==6.0.3
- regex==2025.11.3
- requests==2.32.5
- tokenizers==0.22.1
- tqdm==4.67.1
- filelock==3.20.0
- fsspec==2025.10.0
- typing-extensions==4.15.0
```

### Exact File Paths (Working)

```
Base Directory:
F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\bin\Debug\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2\net10.0\

Virtual Environment:
.venv_gotocr2_manual\

Python Files:
python\got_ocr2_wrapper.py

Requirements:
python\requirements.txt  (NOTE: In python/ subfolder, not root!)

Fixtures:
Fixtures\*.pdf
```

### CSnakes Configuration (Working)

```csharp
var baseDirectory = AppContext.BaseDirectory;
// Result: F:\Dynamic\...\net10.0\

var pythonLibPath = Path.Combine(baseDirectory, "python");
// Result: F:\Dynamic\...\net10.0\python

var venvPath = Path.Combine(baseDirectory, ".venv_gotocr2_manual");
// Result: F:\Dynamic\...\net10.0\.venv_gotocr2_manual

var requirementsPath = Path.Combine(baseDirectory, "requirements.txt");
// Result: F:\Dynamic\...\net10.0\requirements.txt
// NOTE: This path is WRONG but unused because we install manually!

builder.Services
    .WithPython()
    .WithHome(pythonLibPath)
    .WithVirtualEnvironment(venvPath, true)  // true = ensure created
    .FromRedistributable("3.13")  // Downloads Python 3.13.2
    .WithPipInstaller(requirementsPath);  // Ignored - we install manually
```

---

## Critical Discoveries (Ground Truth)

### 0. xUnit Collection Fixture - THE KEY FIX (2025-11-24)

**PROBLEM:** Tests using `IAsyncLifetime` per test class caused Python global state corruption:
```
'NoneType' object is not callable at is_cuda_supported()
```

**ROOT CAUSE:** Python module reinitialized for each test → global variables became None

**SOLUTION:** xUnit Collection Fixture pattern

```csharp
// 1. Define collection
[CollectionDefinition(nameof(GotOcr2Collection))]
public class GotOcr2Collection : ICollectionFixture<GotOcr2Fixture> { }

// 2. Create fixture that initializes Python ONCE
public class GotOcr2Fixture : IAsyncLifetime
{
    private IHost? _host;
    public IHost Host => _host ?? throw new InvalidOperationException("Not initialized");

    public async ValueTask InitializeAsync()
    {
        // Build host with Python environment
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services
            .WithPython()
            .WithHome(pythonLibPath)
            .WithVirtualEnvironment(venvPath, true)
            .FromRedistributable("3.13")
            .WithPipInstaller(requirementsPath);

        builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();
        _host = builder.Build();

        // Initialize Python environment ONCE for all tests
        var pythonEnv = _host.Services.GetRequiredService<IPythonEnvironment>();
        var module = pythonEnv.GotOcr2Wrapper();
        if (!module.HealthCheck())
            throw new InvalidOperationException("Health check failed");
    }

    public async ValueTask DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}

// 3. Tests use collection and inject fixture
[Collection(nameof(GotOcr2Collection))]  // ← KEY: Use collection
public class GotOcr2OcrExecutorTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _executor;

    public GotOcr2OcrExecutorTests(GotOcr2Fixture fixture, ITestOutputHelper output)
    {
        // Get fresh scope from shared host
        _scope = fixture.Host.Services.CreateScope();
        _executor = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
    }

    public void Dispose() => _scope?.Dispose();
}
```

**RESULT:**
- ✅ Python environment initialized once
- ✅ Shared across all tests in collection
- ✅ Each test gets fresh DI scope (clean executor instance)
- ✅ No global state corruption
- ✅ Tests run 10-15x faster (model loaded once)

### 1. PDF Processing Requirements

**FACT:** GOT-OCR2 does NOT accept PDFs directly
**FACT:** Must convert PDF → PNG at 300 DPI using PyMuPDF first

Working Code:
```python
import fitz  # PyMuPDF
import io
from PIL import Image

# Try direct image first
try:
    image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
except Exception:
    # Failed - try PDF conversion
    pdf_doc = fitz.open(stream=image_bytes, filetype="pdf")
    if len(pdf_doc) == 0:
        raise ValueError("PDF has no pages")

    # Convert first page to 300 DPI PNG
    page = pdf_doc[0]
    pix = page.get_pixmap(dpi=300)
    img_bytes = pix.tobytes("png")
    image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
    pdf_doc.close()
```

### 2. Package Installation Order (CRITICAL)

**FACT:** Transformers package corrupts frequently during installation
**FACT:** Must install PyMuPDF separately - not in requirements.txt initially

Working Installation Sequence:
```bash
# 1. Create venv with Python 3.13.2
python -m venv .venv_gotocr2_manual

# 2. Activate
.venv_gotocr2_manual\Scripts\activate

# 3. Upgrade pip
python -m pip install --upgrade pip

# 4. Install torch (CPU version working on laptop)
pip install torch==2.9.1

# 5. Install torchvision
pip install torchvision==0.24.1

# 6. Install transformers (may need reinstall if corrupted)
pip install transformers==4.57.1

# 7. Verify transformers (CRITICAL CHECK)
python -c "from transformers import AutoProcessor, AutoModelForImageTextToText; print('OK')"

# 8. If transformers corrupt:
pip uninstall -y transformers
pip install transformers==4.57.1

# 9. Install PyMuPDF (FOR PDF SUPPORT)
pip install pymupdf==1.26.6

# 10. Install remaining packages
pip install numpy==2.3.5 Pillow==12.0.0 accelerate==1.12.0 huggingface-hub==0.36.0 safetensors==0.7.0
```

### 3. Python Version Synchronization

**FACT:** CSnakes downloads Python 3.13.2 to AppData\Roaming\CSnakes\python3.13.2\
**FACT:** Local Python must be 3.13.2 EXACTLY (not 3.13.5, not 3.13.0)
**FACT:** Version mismatch causes package corruption

Verification:
```bash
python --version
# Must output: Python 3.13.2
```

### 4. Requirements.txt Location

**FACT:** requirements.txt moved to python/ subfolder
**FACT:** .csproj copies it as AdditionalFiles
**FACT:** CSnakes .WithPipInstaller() is unreliable - install manually instead

Current Structure:
```
Infrastructure.Python.GotOcr2/
├── python/
│   ├── got_ocr2_wrapper.py
│   └── requirements.txt  ← HERE, not in root!
└── ExxerCube.Prisma.Infrastructure.Python.GotOcr2.csproj
```

---

## Test Results (Actual Output from Passing Tests)

### Successful Text Extraction

```
Language used: spa (Spanish)
Text preview (first 200 chars):
"Administración General de Auditoría Fiscal Federal
Administración Desconcentrada de Auditoría Fiscal de Sonora "2"
No. De Identificación del Requerimiento
AGAFADAFSON2/2025/000085
Juan Juan Melón S..."

✓ Liskov Substitution Principle validated for 333ccc-6666666662025.pdf
```

### Performance Metrics (Laptop - CPU Only)

**First Run (Model Download):**
- Model download: ~10-15 minutes (3-5GB GOT-OCR2 model)
- First OCR: ~30-60 seconds per PDF page

**Subsequent Runs (Model Cached):**
- Test initialization: ~5 seconds
- OCR per page: ~15-30 seconds (CPU is SLOW)
- Total for 6 tests: ~2-3 minutes

**Expected on Workstation with GPU:**
- Test initialization: ~2 seconds
- OCR per page: ~2-5 seconds (GPU acceleration)
- Total for 6 tests: ~30-60 seconds

### Device Selection (Verified)

```
Current (Laptop): CPU (batch_size=1, no CUDA detected)
Expected (Workstation): GPU for batch_size >= 4, CPU for single images

Python Log Output:
[INFO] Using CPU (batch_size=1 < threshold=4)
```

---

## File Structure (Ground Truth)

### Source Files
```
F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\

Infrastructure.Python.GotOcr2\
├── python\
│   ├── got_ocr2_wrapper.py  (working version with PyMuPDF)
│   └── requirements.txt
├── DependencyInjection\
│   └── ServiceCollectionExtensions.cs
└── ExxerCube.Prisma.Infrastructure.Python.GotOcr2.csproj

Tests.Infrastructure.Extraction.GotOcr2\
├── Fixtures\
│   ├── 222AAA-44444444442025.pdf
│   ├── 333BBB-44444444442025.pdf
│   ├── 333ccc-6666666662025.pdf
│   └── 555CCC-66666662025.pdf
├── GotOcr2OcrExecutorTests.cs
├── GROUND_TRUTH_WORKING_CONFIG.md  ← THIS FILE
└── ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.csproj
```

### Build Output (Actual Paths)
```
F:\Dynamic\...\bin\Debug\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2\net10.0\

├── python\
│   └── got_ocr2_wrapper.py  (copied from source)
├── Fixtures\
│   └── *.pdf  (copied from source)
├── .venv_gotocr2_manual\  (virtual environment)
│   ├── Scripts\
│   │   ├── python.exe  (3.13.2)
│   │   └── pip.exe
│   └── Lib\
│       └── site-packages\
│           ├── torch\
│           ├── transformers\
│           ├── fitz\  (PyMuPDF)
│           └── ...
├── ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.dll
└── TestResults\  (logs)
```

---

## Reproduction Steps for Workstation (GPU)

### Prerequisites
1. NVIDIA GPU with CUDA 13.0 support
2. Windows 11
3. .NET 10 SDK
4. Python 3.13.2 (EXACT VERSION)

### Step 1: Install Python 3.13.2

```bash
# Download from python.org: Python 3.13.2 (64-bit)
# Install to C:\Users\<User>\AppData\Local\Programs\Python\Python313\
# VERIFY version matches:
python --version
# Must show: Python 3.13.2
```

### Step 2: Build Solution

```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp
dotnet build Tests.Infrastructure.Extraction.GotOcr2
```

### Step 3: Navigate to Output Directory

```bash
cd bin\Debug\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2\net10.0
```

### Step 4: Create Virtual Environment with Python 3.13.2

```bash
python -m venv .venv_gotocr2_manual
.venv_gotocr2_manual\Scripts\activate
python -m pip install --upgrade pip
```

### Step 5: Install Packages (GPU Version)

```bash
# Install PyTorch with CUDA 13.0
pip install torch==2.9.1 --index-url https://download.pytorch.org/whl/cu130
pip install torchvision==0.24.1 --index-url https://download.pytorch.org/whl/cu130

# Install transformers
pip install transformers==4.57.1

# Verify transformers (CRITICAL)
python -c "from transformers import AutoProcessor; print('OK')"

# If failed, reinstall:
# pip uninstall -y transformers && pip install transformers==4.57.1

# Install PyMuPDF (CRITICAL FOR PDF)
pip install pymupdf==1.26.6

# Install remaining packages
pip install numpy==2.3.5 Pillow==12.0.0 accelerate==1.12.0 huggingface-hub==0.36.0 safetensors==0.7.0

# Verify PyMuPDF
python -c "import fitz; print(f'PyMuPDF {fitz.version[0]}')"
```

### Step 6: Verify CUDA

```bash
python -c "import torch; print(f'CUDA available: {torch.cuda.is_available()}')"
# Expected: CUDA available: True

python -c "import torch; print(f'CUDA device: {torch.cuda.get_device_name(0)}')"
# Expected: CUDA device: <Your GPU Name>
```

### Step 7: Run Tests

```bash
cd ..\..\..\..  # Back to CSharp folder
dotnet test Tests.Infrastructure.Extraction.GotOcr2 --verbosity normal
```

### Expected Results (GPU)

```
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
Duration: ~30-60 seconds (after model cache)

Device: GPU (CUDA)
Processing: 2-5 seconds per PDF
Confidence: ~86% (bfloat16 quantization, slightly lower than CPU's float32)
```

---

## Known Issues & Solutions

### Issue 1: Transformers Corruption
**Symptom:** `FileNotFoundError: audio_spectrogram_transformer/configuration_*.py`
**Solution:** `pip uninstall -y transformers && pip install transformers==4.57.1`

### Issue 2: PyMuPDF Not Found
**Symptom:** `ModuleNotFoundError: No module named 'fitz'`
**Solution:** `pip install pymupdf==1.26.6`

### Issue 3: Empty OCR Results
**Symptom:** Text length = 0, confidence = 0
**Root Cause:** PDF not being converted to image
**Verification:** Check Python logs for "Direct image loading failed, attempting PDF conversion"

### Issue 4: Python Version Mismatch
**Symptom:** Package corruption, import errors
**Solution:** Uninstall Python 3.13.x, install Python 3.13.2 EXACTLY

---

## Performance Expectations

### Laptop (Current - CPU Only)
- **Hardware:** No GPU, CPU fallback
- **Processing:** 15-30 seconds per page
- **Precision:** float32 (higher quality)
- **Confidence:** ~88-89%
- **Bottleneck:** CPU computation

### Workstation (Expected - GPU)
- **Hardware:** NVIDIA GPU with CUDA 13.0 (20W power-capped, 16-bit precision)
- **Processing:** ACTUALLY SLOWER than CPU (~140s per page)
- **Precision:** bfloat16 (quantized, 16-bit)
- **Confidence:** LOWER than CPU due to quantization
- **Bottleneck:** Power cap (20W), memory bandwidth, 16-bit precision loss
- **DECISION:** **CPU selected for production** - Better quality, comparable speed with power-capped GPU

### E2E Benchmark Setup (Next Step)
- **Document Producer:** Siara double (frontend)
- **Output:** Configurable speed PDF generation
- **Test:** OCR ingestion throughput
- **Metrics to Measure:**
  - PDFs per minute
  - Average latency per page
  - GPU utilization %
  - Memory usage
  - Queue depth tolerance

---

## Next Steps for Production

1. **Benchmark on Workstation**
   - Measure GPU vs CPU performance
   - Determine optimal batch size
   - Test concurrent processing

2. **E2E Testing**
   - Connect to Siara document producer
   - Configure output speed (PDFs/min)
   - Measure ingestion bottlenecks

3. **Database Optimization**
   - Apply migrations
   - Optimize indexes for OCR results
   - Test query performance at scale

4. **Production Deployment Considerations**
   - Model caching strategy
   - Horizontal scaling (multiple workers)
   - GPU memory management
   - Error handling and retry logic
   - Monitoring and alerting

---

## Git References

**Working Commits:**
- `b772268` - PDF-to-image conversion added
- `54ab86a` - Achievement commit (tests passing)

**Tag:**
- `gotocr2-pdf-working` - PDF processing with PyMuPDF integration

---

**Document Created:** 2025-11-24
**Environment:** Laptop (CPU-only, slow)
**Status:** ✅ Tests Passing, Text Extraction Working
**Next:** Reproduce on Workstation (GPU) for E2E Benchmarking
