# QUICK FIX GUIDE - GOT-OCR2 Unit Tests
## URGENT: 3 Critical Fixes Needed

**Problem:** Tests fail with `NameError: name 'print' is not defined`
**Root Cause:** Python `print()` doesn't work in CSnakes test environment
**Solution:** Use Python `logging` module instead

---

## FIX 1: Replace print() with Python logging

### Option 1.C (RECOMMENDED - Use Python logging):

Add this at the top of `got_ocr2_wrapper.py` after imports:

```python
import logging

# Configure Python logging (works in CSnakes)
logging.basicConfig(
    level=logging.DEBUG,
    format='[%(levelname)s] %(message)s'
)
logger = logging.getLogger(__name__)
```

Then replace ALL `print()` calls:
```python
# BEFORE (doesn't work):
print("[DEBUG] execute_ocr() called")
print(f"[DEBUG] Parameters: {language}")

# AFTER (works):
logger.debug("execute_ocr() called")
logger.debug(f"Parameters: {language}")
```

### Quick sed command to replace all:
```bash
cd "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\Infrastructure.Python.GotOcr2\python"

# Replace print("[DEBUG] ... with logger.debug("...
# Replace print("[INFO] ... with logger.info("...
# Replace print("[ERROR] ... with logger.error("...
# Replace print("[SUCCESS] ... with logger.info("...
# Replace print(f"[DEBUG] ... with logger.debug(f"...
```

---

## FIX 2: Fix DI Configuration + Add Timeouts

### Current Issues:
1. Tests create executor manually but need to match ConsoleDemo DI pattern for long-running apps
2. NO TIMEOUTS on tests - can hang indefinitely (unacceptable for CI/CD)

### File: `GotOcr2OcrExecutorTests.cs`

**Change 1 - InitializeAsync() DI:**
```csharp
// OLD - Wrong logger registration
builder.Services.AddSingleton(_logger);

// NEW - Proper DI like ConsoleDemo
builder.Services.AddSingleton<ILogger<GotOcr2OcrExecutor>>(_logger);
```

**Change 2 - Add Timeouts to Tests:**
```csharp
// OCR tests (long-running):
[Theory(Timeout = 120000)]  // 2 minutes
public async Task ExecuteOcrAsync_WithRealCNBVFixtures_ReturnsHighConfidenceResults(...)

// Validation tests (fast):
[Fact(Timeout = 5000)]  // 5 seconds
public async Task ExecuteOcrAsync_WithNullImageData_ReturnsFailure()

[Fact(Timeout = 5000)]  // 5 seconds
public async Task ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()
```

**Why Timeouts Matter:**
- OCR tests could hang if model download fails
- Validation tests should fail fast (5s max)
- CRITICAL for CI/CD pipelines - no infinite hangs allowed

---

## FIX 3: Manual venv Creation (Lesson Learned the Hard Way)

### The "Protected Environment" is a Myth

CSnakes doesn't create the environment correctly. You must create manually.

### Steps:

```bash
cd "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\bin\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2\net10.0"

# 1. Create venv manually
python -m venv .venv_gotor2_tests

# 2. Activate it
.venv_gotor2_tests\Scripts\activate

# 3. Upgrade pip
python -m pip install --upgrade pip

# 4. Install packages ONE BY ONE (addressing issues manually)
pip install torch==2.9.1 --index-url https://download.pytorch.org/whl/cu130
pip install torchvision==0.24.1 --index-url https://download.pytorch.org/whl/cu130
pip install numpy==2.3.5
pip install transformers==4.57.1
pip install Pillow==12.0.0
pip install accelerate==1.12.0
pip install huggingface-hub==0.36.0
pip install safetensors==0.7.0

# 5. Verify torch works
python -c "import torch; print(torch.__version__)"

# 6. Verify transformers works
python -c "from transformers import AutoProcessor; print('OK')"

# 7. Deactivate
deactivate
```

### Why Manual?
- CSnakes tries to be smart but fails
- Need to handle CUDA index-url manually
- Need to verify each package
- Need to address errors as they appear

---

## COMPLETE FIX SEQUENCE

### Step 1: Fix Python logging (5 min)
```bash
cd Infrastructure.Python.GotOcr2/python
# Edit got_ocr2_wrapper.py
# Add logging import and logger
# Replace all print() with logger.debug/info/error
```

### Step 2: Rebuild (1 min)
```bash
dotnet build Infrastructure.Python.GotOcr2
dotnet build Tests.Infrastructure.Extraction.GotOcr2
```

### Step 3: Fix DI in tests (2 min)
```bash
# Edit GotOcr2OcrExecutorTests.cs
# Change logger registration to ILogger<GotOcr2OcrExecutor>
```

### Step 4: Create manual venv (10 min)
```bash
# Follow manual venv creation above
# This is THE CRITICAL STEP
```

### Step 5: Run tests (1 min)
```bash
dotnet test Tests.Infrastructure.Extraction.GotOcr2 --verbosity normal
```

### Expected: All 6 tests PASS

---

## Python Logging Cheat Sheet

```python
import logging

logging.basicConfig(level=logging.DEBUG, format='[%(levelname)s] %(message)s')
logger = logging.getLogger(__name__)

# Use these instead of print():
logger.debug("Debug message")           # Replaces print("[DEBUG] ...")
logger.info("Info message")             # Replaces print("[INFO] ...")
logger.warning("Warning message")       # Replaces print("[WARNING] ...")
logger.error("Error message")           # Replaces print("[ERROR] ...")
logger.exception("Exception message")   # For exceptions with traceback

# With formatting:
logger.debug(f"Processing {filename} with {size} bytes")
```

---

## If Still Failing

### Check 1: Python file deployed?
```bash
ls bin/.../net10.0/python/got_ocr2_wrapper.py
# Should exist and have logging, not print()
```

### Check 2: Venv created?
```bash
ls bin/.../net10.0/.venv_gotor2_tests/
# Should have Scripts/ and Lib/ folders
```

### Check 3: Packages installed?
```bash
.venv_gotor2_tests\Scripts\python -c "import torch; import transformers; print('OK')"
# Should print "OK"
```

### Check 4: DI working?
Look for error: `Unable to resolve service for type 'Microsoft.Extensions.Logging.ILogger'`
If yes: Logger registration is wrong (Fix 2)

---

## Time Estimate
- Fix 1 (logging): 5 min
- Fix 2 (DI): 2 min
- Fix 3 (venv): 10 min
- Testing: 2 min
- **Total: ~20 minutes**

## Success Criteria
✅ No `NameError: name 'print' is not defined`
✅ No DI resolution errors
✅ Virtual environment exists with all packages
✅ All 6 tests pass
✅ Tests complete in <2 minutes

**GO!** 🚀
