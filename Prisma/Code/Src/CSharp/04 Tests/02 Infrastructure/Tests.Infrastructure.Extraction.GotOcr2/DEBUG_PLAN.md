# GOT-OCR2 Unit Test Debugging Plan
## Critical Context and 3-Stage Systematic Approach

**Date:** 2025-11-23
**Status:** Phase 3 - Unit Testing (BLOCKED by critical bugs)
**Git Branch:** `kat`

---

## Executive Summary

### The Problem
Unit tests in `Tests.Infrastructure.Extraction.GotOcr2` are failing with **5 out of 6 tests** broken. The previous agent made incorrect assumptions about dependency injection and started changing the Python implementation code without understanding the root cause.

### The Root Cause (DISCOVERED)
**CRITICAL BUG: Python `print()` function not available in CSnakes runtime**

The error is:
```
NameError: name 'print' is not defined
File "...python\got_ocr2_wrapper.py", line 244, in execute_ocr
    print("[DEBUG] ========================================")
```

**This is NOT a dependency injection issue!** This is a Python sandboxing/environment issue where CSnakes is running Python code in a restricted mode where builtin functions are not available.

### What's Working (Source of Truth)
- ✅ **GotOcr2Sample** - Working playground project with proven configuration
- ✅ **ConsoleApp.GotOcr2Demo** - Working console app in main solution
- ✅ Both successfully execute OCR with 88%+ confidence on CNBV documents

### What's Broken
- ❌ **Tests.Infrastructure.Extraction.GotOcr2** - Unit test project (5/6 tests failing)
- ❌ **Infrastructure.Python.GotOcr2** - Has debug `print()` statements that fail in test environment

---

## Sources of Truth - Reference Projects

### 1. GotOcr2Sample (Playground/Working Sample)
**Location:** `F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Samples\GotOcr2Sample\`

**Critical Documentation:**
- `README.md` - Architecture and setup
- `LESSONS_LEARNED.md` - All pitfalls and solutions (288 lines of hard-won knowledge)
- `HANDOFF_NEXT_SESSION.md` - Complete working configuration and success metrics
- `NET10_MIGRATION_GUIDE.md` - Migration path (but we're on .NET 10 already)

**Working Configuration:**
```
ConsoleDemo/Program.cs:
- Python path: Relative path to PythonOcrLib using AppDomain.CurrentDomain.BaseDirectory
- Venv: .venv_clean
- Python version: 3.13 (FromRedistributable)
- Requirements: requirements.txt with CUDA 13.0 packages

Infrastructure/GotOcr2Executor.cs:
- Direct instantiation: new GotOcr2Executor(pythonEnv, logger)
- Uses strongly-typed CSnakes interface: pythonEnv.GotOcr2Wrapper()
- Returns IndQuestResults Result<T>

PythonOcrLib/got_ocr2_wrapper.py:
- sys.path cleaning at module level
- Lazy imports (import inside functions)
- Device auto-selection
- NO debug print() statements (or they work in this environment)
```

### 2. ConsoleApp.GotOcr2Demo (Main Solution Console)
**Location:** `F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\ConsoleApp.GotOcr2Demo\`

**Configuration:**
```csharp
// Program.cs
var pythonLibPath = Path.Combine(baseDirectory, "python");
var venvPath = Path.Combine(baseDirectory, ".venv_gotor2");
var requirementsPath = Path.Combine(baseDirectory, "requirements.txt");

builder.Services
    .WithPython()
    .WithHome(pythonLibPath)
    .WithVirtualEnvironment(venvPath)
    .FromRedistributable("3.13")
    .WithPipInstaller(requirementsPath);

// Register OCR executor
builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();
```

**Key differences from Sample:**
- Uses DI registration: `AddScoped<IOcrExecutor, GotOcr2OcrExecutor>()`
- Uses Serilog instead of basic logging
- Uses `FixtureFileLocator` helper
- Python files copied to `bin/python/` via build

---

## Current Test Project State

### Tests.Infrastructure.Extraction.GotOcr2

**Configuration (in GotOcr2OcrExecutorTests.cs InitializeAsync()):**
```csharp
var baseDirectory = AppContext.BaseDirectory;  // bin/net10.0/
var pythonLibPath = Path.Combine(baseDirectory, "python");
var venvPath = Path.Combine(baseDirectory, ".venv_gotor2_tests");
var requirementsPath = Path.Combine(baseDirectory, "requirements.txt");

builder.Services
    .WithPython()
    .WithHome(pythonLibPath)
    .WithVirtualEnvironment(venvPath)
    .FromRedistributable("3.13")
    .WithPipInstaller(requirementsPath);

builder.Services.AddSingleton(_logger);  // XUnit logger
builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();
```

**Test Results:**
```
Failed! - Failed: 5, Passed: 1, Skipped: 0, Total: 6
Duration: 36s 564ms

PASSED:
✓ GOT-OCR2 should reject empty image data

FAILED (all with same error):
✗ GOT-OCR2 should reject null image data
✗ GOT-OCR2 should process CNBV PDF fixtures (4 fixtures)
```

**Actual Error:**
```
CSnakes.Runtime.PythonInvocationException: The Python runtime raised a NameError exception
  ---> CSnakes.Runtime.PythonRuntimeException: name 'print' is not defined
  File ".../python/got_ocr2_wrapper.py", line 244, in execute_ocr
      print("[DEBUG] ========================================")
```

---

## Root Cause Analysis

### Issue 1: Python Builtins Not Available (CRITICAL)
**Symptom:** `NameError: name 'print' is not defined`

**Root Cause:** CSnakes runs Python in a restricted/sandboxed environment in certain configurations where builtin functions are not automatically available.

**Evidence:**
- The sample project's Python wrapper has NO debug `print()` statements
- The Infrastructure.Python.GotOcr2 Python wrapper has MANY debug `print()` statements (lines 244+)
- Sample works, tests fail with exact same error on `print()`

**Solution:** Remove ALL `print()` statements from Python code, or import builtins explicitly

### Issue 2: File Path Mismatches (MINOR)
**Symptom:** Requirements.txt and Python files need to be in correct location

**Root Cause:** .csproj file copies from Sample project:
```xml
<!-- Copy fixture files -->
<None Include="..\..\..\..\Samples\GotOcr2Sample\PythonOcrLib\PRP1\*.pdf" />

<!-- Copy requirements.txt -->
<None Include="..\..\..\..\Samples\GotOcr2Sample\PythonOcrLib\requirements.txt" />
```

**Issue:** Should copy from `Infrastructure.Python.GotOcr2` project, not Sample

### Issue 3: Python Wrapper Version Mismatch (CONFIRMED)
**Symptom:** Test is using wrong version of `got_ocr2_wrapper.py`

**Source of Truth:** `Samples/GotOcr2Sample/PythonOcrLib/got_ocr2_wrapper.py` (working, no debug prints)
**Current Deployment:** `Infrastructure.Python.GotOcr2/python/got_ocr2_wrapper.py` (broken, has debug prints)

**The infrastructure project's Python wrapper was modified with extensive debug logging that breaks in test environment!**

---

## The 3-Stage Systematic Plan

### Stage 1: Fix Python Wrapper - Match Source of Truth
**Goal:** Make `Infrastructure.Python.GotOcr2/python/got_ocr2_wrapper.py` match the working sample

**Critical Principle:** NO GUESSING ALLOWED. The sample project works. Copy it exactly.

**Tasks:**
1. **Compare Python wrappers line by line:**
   - Source: `Samples/GotOcr2Sample/PythonOcrLib/got_ocr2_wrapper.py` (447 lines, working)
   - Target: `Infrastructure.Python.GotOcr2/python/got_ocr2_wrapper.py` (558 lines, broken)

2. **Identify differences:**
   - Debug `print()` statements (the infrastructure version has many)
   - Any other modifications

3. **Decision Point - Choose ONE approach:**

   **Option A: Copy Exact Working Version (RECOMMENDED)**
   ```bash
   # Replace broken version with working version
   cp Samples/GotOcr2Sample/PythonOcrLib/got_ocr2_wrapper.py \
      Infrastructure.Python.GotOcr2/python/got_ocr2_wrapper.py
   ```
   - ✅ Guaranteed to work (it's the source of truth)
   - ✅ No guessing
   - ❌ Loses debug logging (but it doesn't work anyway)

   **Option B: Remove All print() Statements**
   - Systematically remove every `print()` call
   - Keep structural logic intact
   - ⚠️ More risky, requires validation

   **Option C: Import builtins explicitly**
   ```python
   import builtins
   print = builtins.print  # Make print available
   ```
   - ⚠️ Experimental, may not work in CSnakes sandbox
   - ⚠️ Requires understanding of CSnakes internals

**Recommendation:** **Option A** - Copy the exact working version. Debug logging is useless if tests don't run.

**Validation:**
```bash
# After making changes, rebuild and verify file is copied
dotnet build Infrastructure.Python.GotOcr2
dotnet build Tests.Infrastructure.Extraction.GotOcr2

# Verify the copied file matches source
diff Samples/GotOcr2Sample/PythonOcrLib/got_ocr2_wrapper.py \
     bin/.../Tests.Infrastructure.Extraction.GotOcr2/net10.0/python/got_ocr2_wrapper.py
```

---

### Stage 2: Fix Test Environment Configuration
**Goal:** Make test initialization match ConsoleApp.GotOcr2Demo (which works)

**Current Issues:**
1. ✅ Python environment configuration is correct (matches ConsoleDemo)
2. ⚠️ Logger injection might differ (tests use XUnit logger)
3. ⚠️ File paths (requirements.txt copied from Sample instead of Infrastructure project)

**Tasks:**

1. **Fix requirements.txt path in .csproj:**
   ```xml
   <!-- WRONG (current) -->
   <None Include="..\..\..\..\Samples\GotOcr2Sample\PythonOcrLib\requirements.txt" />

   <!-- RIGHT (should be) -->
   <None Include="..\Infrastructure.Python.GotOcr2\requirements.txt" />
   ```

2. **Verify fixture files path:**
   ```xml
   <!-- Current (seems OK if PRP1 folder exists in Sample) -->
   <None Include="..\..\..\..\Samples\GotOcr2Sample\PythonOcrLib\PRP1\*.pdf"
         Link="Fixtures\%(Filename)%(Extension)"
         CopyToOutputDirectory="PreserveNewest" />
   ```

3. **Compare test DI registration with ConsoleDemo:**
   ```csharp
   // Tests (current)
   builder.Services.AddSingleton(_logger);  // XUnit logger singleton
   builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();

   // ConsoleDemo (working)
   builder.Services.AddSerilog(Log.Logger);  // Serilog
   builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();
   ```

   **Analysis:** Logger difference should NOT cause Python `print()` errors. But verify `GotOcr2OcrExecutor` constructor requirements match.

4. **Verify Python file deployment:**
   ```bash
   # Check that Python wrapper is actually copied to test output
   ls -la bin/.../Tests.Infrastructure.Extraction.GotOcr2/net10.0/python/
   # Should contain: got_ocr2_wrapper.py

   # Check it's the RIGHT version (no debug prints)
   grep "print(\[DEBUG\]" bin/.../Tests.Infrastructure.Extraction.GotOcr2/net10.0/python/got_ocr2_wrapper.py
   # Should return nothing if Stage 1 was done correctly
   ```

**Validation:**
```bash
# Run a single simple test
dotnet test --filter "FullyQualifiedName~GOT-OCR2 should reject empty image data"

# Should PASS (it already passes, so this validates environment)
```

---

### Stage 3: Run Full Test Suite and Validate
**Goal:** All 6 tests pass, matching Tesseract test pattern

**Expected Test Behavior:**

According to the user: *"the unit test have to be identical to the tesseract since we are making another implementation of ExxerCube.Prisma.Tests.Infrastructure.Extraction"*

**Test Pattern (from existing tests):**
1. ✅ **Empty image data test** - Should fail gracefully (ALREADY PASSING)
2. ✅ **Null image data test** - Should fail gracefully (SHOULD PASS after Stage 1 fix)
3. ✅ **4 CNBV PDF fixtures** - Should extract text with >75% confidence (SHOULD PASS after Stage 1 fix)

**Tasks:**

1. **Run full test suite:**
   ```bash
   dotnet test Tests.Infrastructure.Extraction.GotOcr2 --verbosity normal
   ```

2. **Expected output:**
   ```
   Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
   ```

3. **If tests still fail, check:**
   - Virtual environment created? (`bin/.../net10.0/.venv_gotor2_tests/` should exist)
   - Python packages installed? (check `.venv_gotor2_tests/Lib/site-packages/`)
   - Model downloaded? (first run takes time ~3-5GB download)
   - Fixture files copied? (`bin/.../net10.0/Fixtures/*.pdf` should exist)

4. **Test output validation:**
   - Each test should log: "=== Initializing GOT-OCR2 Test Environment ==="
   - Python environment should initialize once (shared across tests via `IAsyncLifetime`)
   - OCR execution should complete in 5-15 seconds per test (CPU) or 1-5 seconds (GPU)
   - Confidence scores should be 75%+
   - No Python errors

5. **Compare with Tesseract tests:**
   - Find Tesseract test project: `Tests.Infrastructure.Extraction` (or similar)
   - Verify test structure matches:
     - Same test method names pattern
     - Same fixture files
     - Same assertions
     - Same confidence thresholds

**Validation Checklist:**
- [ ] All 6 tests pass
- [ ] No Python `NameError` exceptions
- [ ] No dependency injection errors
- [ ] Virtual environment created automatically
- [ ] Python packages install automatically (or use cached)
- [ ] Model downloads on first run (or uses cached)
- [ ] Tests complete in reasonable time (<2 minutes total)
- [ ] Confidence scores meet minimum thresholds (75%+)

---

## Environment Requirements (From LESSONS_LEARNED.md)

### Working Configuration (DO NOT CHANGE)
```
.NET: 10.0 (target, but 9.0 also tested working)
CSnakes: Stable version from NuGet
Python: 3.13 (via CSnakes redistributable)
PyTorch: 2.9.1+cu130
torchvision: 0.24.1 (CRITICAL - must be installed)
transformers: 4.57.1
Model: stepfun-ai/GOT-OCR-2.0-hf
```

### Critical Lessons from LESSONS_LEARNED.md
1. **Always use lazy imports** - Import torch/transformers inside functions, not at module level
2. **Clean sys.path** - Remove module directory from sys.path before importing torch
3. **torchvision is REQUIRED** - Missing torchvision causes cryptic errors
4. **Don't use complex requirements.txt** - Let pip resolve versions
5. **CSnakes generates type-safe interfaces** - Use them (not dynamic)
6. **Package installation is NOT automatic** - Must explicitly call or let first run install

### What NOT to Do (Pitfalls from Sample)
❌ Don't use .NET 9/10 with beta CSnakes (use stable)
❌ Don't import torch at module level
❌ Don't assume packages install automatically
❌ Don't add complex version constraints in requirements.txt
❌ Don't forget to clean sys.path
❌ Don't use dynamic instead of typed interfaces
❌ **DON'T ADD DEBUG PRINT STATEMENTS (New finding!)**

---

## Team Decision Points

### Before Starting ANY Stage:
1. **READ this entire document** - No skipping, no guessing
2. **Verify you understand the root cause** - It's NOT DI, it's Python print()
3. **Choose Stage 1 Option** - A (copy), B (remove prints), or C (import builtins)
4. **Get team approval** - This is a critical decision

### During Stage 1:
1. **Compare files before modifying** - Document all differences
2. **Backup current version** - Git commit before changes
3. **Test immediately after** - Don't pile up changes

### During Stage 2:
1. **One change at a time** - Fix requirements.txt, test. Fix paths, test.
2. **Verify file deployment** - Check bin output after each build
3. **Don't modify Python code** - Stage 1 is done, don't touch it

### During Stage 3:
1. **Run tests individually first** - Easier to debug
2. **Check logs carefully** - Python errors are verbose but informative
3. **Compare with working ConsoleDemo** - If tests fail but ConsoleDemo works, it's environment

---

## Success Criteria

### Stage 1 Complete When:
- [ ] Python wrapper matches source of truth OR has no `print()` calls
- [ ] File builds and copies to bin output
- [ ] No syntax errors in Python

### Stage 2 Complete When:
- [ ] requirements.txt copies from correct location
- [ ] Python wrapper copies from correct location
- [ ] Fixtures copy to correct location
- [ ] Test environment initializes without errors

### Stage 3 Complete When:
- [ ] All 6 tests pass
- [ ] Tests run in <2 minutes
- [ ] Confidence scores are 75%+
- [ ] No Python exceptions
- [ ] Test pattern matches Tesseract tests

### Overall Success (Phase 3 Complete):
- [ ] Unit tests are production-ready
- [ ] Tests are identical in pattern to Tesseract tests
- [ ] Tests use same fixtures
- [ ] Tests validate IOcrExecutor contract (Liskov Substitution Principle)
- [ ] Ready for Phase 4: Real-world integration in solution

---

## Phase 4 Preview (Future Work)

After Phase 3 (unit tests) is complete, Phase 4 will be integrating GOT-OCR2 into the actual Prisma solution for real document processing.

**Current Status:** "These is consider a fact, but in reality is not but we will see when we face that part"

**What Phase 4 Likely Involves:**
1. Register `GotOcr2OcrExecutor` in main solution DI container
2. Add configuration to select Tesseract vs GOT-OCR2
3. A/B testing with real CNBV documents
4. Performance optimization
5. Error handling for production scenarios
6. Deployment considerations (model size, GPU requirements)

**Don't worry about Phase 4 now - Focus on getting Phase 3 (unit tests) working first.**

---

## Quick Start Commands

### Stage 1: Fix Python Wrapper
```bash
# Option A (recommended): Copy working version
cp "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Samples\GotOcr2Sample\PythonOcrLib\got_ocr2_wrapper.py" \
   "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\Infrastructure.Python.GotOcr2\python\got_ocr2_wrapper.py"

# Rebuild to verify
cd "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp"
dotnet build Infrastructure.Python.GotOcr2
dotnet build Tests.Infrastructure.Extraction.GotOcr2

# Verify the file was copied correctly
ls -la "bin/ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2/net10.0/python/"
```

### Stage 2: Fix Configuration
```bash
# Edit .csproj file for requirements.txt path
# Then rebuild
dotnet build Tests.Infrastructure.Extraction.GotOcr2
```

### Stage 3: Run Tests
```bash
# Run full test suite
dotnet test Tests.Infrastructure.Extraction.GotOcr2 --verbosity normal

# Run single test for debugging
dotnet test Tests.Infrastructure.Extraction.GotOcr2 \
  --filter "FullyQualifiedName~should reject empty image data" \
  --verbosity detailed
```

---

## Appendix: File Locations Reference

### Sample Project (Source of Truth)
```
F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Samples\GotOcr2Sample\
├── ConsoleDemo/Program.cs (working DI setup)
├── Infrastructure/GotOcr2Executor.cs (working executor)
├── PythonOcrLib/
│   ├── got_ocr2_wrapper.py (WORKING VERSION - source of truth)
│   ├── requirements.txt (CUDA 13.0, working)
│   ├── PRP1/*.pdf (fixture files)
│   └── .venv_clean/ (working virtual environment)
├── README.md
├── LESSONS_LEARNED.md (CRITICAL - READ THIS)
└── HANDOFF_NEXT_SESSION.md (working configuration details)
```

### Main Solution Projects
```
F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\
├── ConsoleApp.GotOcr2Demo/ (working console app)
│   └── Program.cs (working DI setup)
├── Infrastructure.Extraction/ (base OCR infrastructure)
│   └── GotOcr2/
│       └── GotOcr2OcrExecutor.cs (production executor)
├── Infrastructure.Python.GotOcr2/ (Python interop layer)
│   ├── python/
│   │   └── got_ocr2_wrapper.py (BROKEN VERSION - has debug prints)
│   └── requirements.txt (should match sample)
└── Tests.Infrastructure.Extraction.GotOcr2/ (FAILING TESTS)
    ├── GotOcr2OcrExecutorTests.cs (5/6 tests failing)
    ├── GlobalUsings.cs
    └── ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2.csproj
```

### Key Build Outputs
```
F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\bin\
└── ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2/net10.0/
    ├── python/
    │   └── got_ocr2_wrapper.py (deployed version - check this!)
    ├── Fixtures/
    │   └── *.pdf (fixture files copied here)
    ├── requirements.txt (copied here)
    ├── .venv_gotor2_tests/ (virtual environment created here)
    └── TestResults/ (test logs - check for errors here)
```

---

## Final Notes

**The most important lesson:** The working sample exists. It works. It has detailed documentation of every problem and solution. **USE IT.**

Don't try to fix problems that are already solved. Don't add features that break working code. Don't guess when you have a source of truth.

The previous agent tried to "fix" dependency injection when the real problem was Python `print()` statements. This is exactly what NOT to do.

**Systematic approach:**
1. Understand what works (sample project)
2. Understand what's broken (tests fail on `print()`)
3. Make broken thing match working thing (copy Python wrapper)
4. Verify it works (run tests)
5. Don't change anything else

**If you're unsure:** Read LESSONS_LEARNED.md again. It's 290 lines of hard-won knowledge from someone who already debugged all these issues.

**Good luck!** 🚀

---

**Document created:** 2025-11-23
**By:** Claude Code Agent (systematic analysis)
**For:** Next agent/team member debugging GOT-OCR2 unit tests
**Status:** Ready for Stage 1 implementation
