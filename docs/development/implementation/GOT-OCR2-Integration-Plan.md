# GOT-OCR2 Integration Plan

## Project Overview
Integration of GOT-OCR2 (General OCR Theory 2.0) transformer model into ExxerCube.Prisma for high-accuracy OCR of complex Spanish CNBV documents.

**Repository**: ExxerCube.Prisma
**Target Framework**: .NET 10.0
**Python Version**: 3.13 (via CSnakes redistributable)
**Start Date**: 2025-11-23

---

## End Goal

**Primary Objective**: Dual OCR engine system with intelligent fallback strategy
- Fast Tesseract for quick processing
- High-accuracy GOT-OCR2 for complex documents
- Runtime engine selection via keyed DI
- Automatic fallback: Tesseract (fast) → GOT-OCR2 (accurate) when confidence is low

**Success Criteria**:
- ✅ GOT-OCR2 integrated and working on .NET 10
- ✅ CSnakes source generation functioning
- ✅ Clean architecture maintained (SRP)
- ✅ ConsoleDemo working with main solution
- ✅ PDF processing with PyMuPDF
- ✅ 88.85% confidence on real CNBV documents
- ✅ Comprehensive logging infrastructure
- ⏳ Unit tests passing
- ⏳ Tesseract implementation added
- ⏳ Keyed DI for runtime engine selection
- ⏳ Integration tests with real CNBV documents

---

## Progress Tracker

### ✅ Phase 1: Foundation (COMPLETED)
**Commit 1**: `941bba4` - feat: CSnakes + GOT-OCR2 working on .NET 10.0

- [x] Verify GotOcr2Sample works on .NET 10
- [x] Document CSnakes configuration
- [x] Confirm Python interop stability
- [x] Validate CUDA support
- [x] Test with sample CNBV documents (88.94% confidence achieved)

**Key Files Created**:
- `Prisma/Samples/GotOcr2Sample/NET10_MIGRATION_GUIDE.md`
- `Prisma/Samples/GotOcr2Sample/HANDOFF_NEXT_SESSION.md`

---

### ✅ Phase 2: Core Integration (COMPLETED)
**Commit 2**: `6f48ac7` - feat: Integrate GOT-OCR2 into main project with SRP architecture

- [x] Create `Infrastructure.Python.GotOcr2` project (dedicated Python interop)
- [x] Copy Python wrapper (`got_ocr2_wrapper.py`)
- [x] Copy `requirements.txt`
- [x] Configure CSnakes source generation
- [x] Suppress CS1591 warnings for generated code
- [x] Create `GotOcr2OcrExecutor` in `Infrastructure.Extraction/GotOcr2/`
- [x] Implement `IOcrExecutor` interface
- [x] Register in DI as default OCR engine
- [x] Verify build succeeds (0 warnings, 0 errors)
- [x] Add `CSnakes.Runtime` to `Infrastructure` and `Infrastructure.Extraction`
- [x] Update `GlobalUsings.cs` with CSnakes namespace

**Project Structure Created**:
```
Infrastructure.Python.GotOcr2/
├── python/got_ocr2_wrapper.py
├── requirements.txt
├── DependencyInjection/ServiceCollectionExtensions.cs
├── GlobalUsings.cs
└── ExxerCube.Prisma.Infrastructure.Python.GotOcr2.csproj

Infrastructure.Extraction/
└── GotOcr2/GotOcr2OcrExecutor.cs
```

**Technical Achievements**:
- CSnakes successfully generates `GotOcr2Wrapper()` extension method
- Strongly-typed Python interop working
- SRP: Python code isolated in dedicated project
- Clean architecture maintained

---

### ✅ Phase 3: Console Demo & Testing (COMPLETED)
**Commit 3**: `fe410bc` - feat: Complete GOT-OCR2 integration with PDF support and comprehensive logging

**Status**: ✅ FULLY FUNCTIONAL - PDF OCR working with 88.85% confidence!

**Steps Completed**:
- [x] Copy ConsoleDemo from sample to main project
- [x] Refactor `ConsoleApp.GotOcr2Demo.csproj` for main solution dependencies
- [x] Refactor `Program.cs` to use main project DI registration
- [x] Add ConsoleDemo to solution file
- [x] Create dedicated Python venv (`.venv_gotor2`)
- [x] Install PyTorch 2.9.1 with CUDA 13.0 support
- [x] Install transformers 4.57.1, torchvision, and dependencies
- [x] Install PyMuPDF 1.26.6 for PDF processing
- [x] Run ConsoleDemo and verify health check passes
- [x] Test OCR with sample CNBV PDF document
- [x] Achieve 88.85% confidence on real Spanish CNBV document
- [x] Add comprehensive Serilog logging (console + file)
- [x] Create FixtureFileLocator helper for test files
- [x] Implement native PDF support via PyMuPDF
- [x] Fix Python wrapper for PDF-to-image conversion
- [x] Add enhanced debug logging to Python script
- [x] Fix test infrastructure (WebApplicationFactory)
- [x] Document .NET 8→9→10 migration history
- [x] **Commit 3**: "feat: Complete GOT-OCR2 integration with PDF support"

**Major Achievements**:

1. **PDF Processing Success**:
   - PyMuPDF integration for PDF-to-image conversion
   - 300 DPI high-quality rendering
   - 4-page CNBV document processed successfully
   - 1,794 characters extracted with 88.85% confidence
   - Processing time: 128.39s (includes first-time model loading)

2. **Logging Infrastructure**:
   - Serilog with dual sinks (console + rolling file)
   - Structured logging with detailed debug information
   - Python execution tracing for troubleshooting
   - 7-day log retention policy

3. **Intelligent File Handling**:
   - FixtureFileLocator with 5 fallback paths
   - Supports JPG, PNG, PDF (case insensitive)
   - Detailed search location logging
   - Command-line argument support

4. **Test Infrastructure**:
   - Fixed TestWebApplicationFactory
   - MockOcrExecutor for UI tests
   - Removed Python dependencies from test projects

**Python Environment Setup**:
```bash
# Location: bin/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo/net10.0/.venv_gotor2
# Python: 3.13 (CSnakes redistributable)
# Packages (from requirements.txt):
torch==2.9.1 --index-url https://download.pytorch.org/whl/cu130
torchvision==0.24.1 --index-url https://download.pytorch.org/whl/cu130
numpy==2.3.5
transformers==4.57.1
Pillow==12.0.0
accelerate==1.12.0
huggingface-hub==0.36.0
safetensors==0.7.0
pymupdf==1.26.6  # NEW - PDF processing
```

**Console Commands**:
```bash
# Build
dotnet build ConsoleApp.GotOcr2Demo/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj

# Run (will install packages on first run)
dotnet run --project ConsoleApp.GotOcr2Demo/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj

# Run with specific image
dotnet run --project ConsoleApp.GotOcr2Demo/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj -- path/to/image.jpg
```

---

### ⏳ Phase 4: Unit Tests (PENDING)
**Target**: `Tests.Infrastructure.Extraction/GotOcr2OcrExecutorTests.cs`

**Test Cases to Implement**:
- [ ] `Constructor_WithNullPythonEnvironment_ThrowsArgumentNullException()`
- [ ] `Constructor_WithNullLogger_ThrowsArgumentNullException()`
- [ ] `ExecuteOcrAsync_WithEmptyImageData_ReturnsFailure()`
- [ ] `ExecuteOcrAsync_WithNullImageData_ReturnsFailure()`
- [ ] Integration test with mock Python environment (optional)

**Template** (from GotOcr2Sample):
```csharp
public class GotOcr2OcrExecutorTests
{
    private readonly IPythonEnvironment _mockPythonEnv;
    private readonly ILogger<GotOcr2OcrExecutor> _mockLogger;
    private readonly IOcrExecutor _sut;

    public GotOcr2OcrExecutorTests()
    {
        _mockPythonEnv = Substitute.For<IPythonEnvironment>();
        _mockLogger = Substitute.For<ILogger<GotOcr2OcrExecutor>>();
        _sut = new GotOcr2OcrExecutor(_mockPythonEnv, _mockLogger);
    }

    [Fact]
    public void Constructor_WithNullPythonEnvironment_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GotOcr2OcrExecutor(null!, _mockLogger));
    }

    // ... more tests
}
```

**Commit 4**: "test: Add unit tests for GotOcr2OcrExecutor"

---

### ⏳ Phase 5: Tesseract Implementation (PENDING)
**Goal**: Create second OCR engine for fast processing

**Tasks**:
- [ ] Create `TesseractOcrExecutor` in `Infrastructure.Extraction/Tesseract/`
- [ ] Implement `IOcrExecutor` interface
- [ ] Add Tesseract NuGet package dependency
- [ ] Register in DI with keyed services
- [ ] Create unit tests for TesseractOcrExecutor

**Keyed DI Registration**:
```csharp
// In ServiceCollectionExtensions.cs
services.AddKeyedScoped<IOcrExecutor, TesseractOcrExecutor>("tesseract");
services.AddKeyedScoped<IOcrExecutor, GotOcr2OcrExecutor>("got-ocr2");

// Default: use Tesseract for speed
services.AddScoped<IOcrExecutor>(sp =>
    sp.GetRequiredKeyedService<IOcrExecutor>("tesseract"));
```

**Commit 5**: "feat: Add Tesseract OCR executor with keyed DI"

---

### ⏳ Phase 6: Intelligent Fallback Strategy (PENDING)
**Goal**: Automatic fallback from fast→accurate based on confidence

**Implementation**:
```csharp
public class SmartOcrExecutor : IOcrExecutor
{
    private readonly IOcrExecutor _tesseract;
    private readonly IOcrExecutor _gotOcr2;
    private readonly ILogger _logger;
    private const float CONFIDENCE_THRESHOLD = 75.0f;

    public SmartOcrExecutor(
        [FromKeyedServices("tesseract")] IOcrExecutor tesseract,
        [FromKeyedServices("got-ocr2")] IOcrExecutor gotOcr2,
        ILogger<SmartOcrExecutor> logger)
    {
        _tesseract = tesseract;
        _gotOcr2 = gotOcr2;
        _logger = logger;
    }

    public async Task<Result<OCRResult>> ExecuteOcrAsync(
        ImageData imageData, OCRConfig config)
    {
        // Try Tesseract first (fast)
        var tesseractResult = await _tesseract.ExecuteOcrAsync(imageData, config);

        if (tesseractResult.IsSuccess &&
            tesseractResult.Value!.ConfidenceAvg >= CONFIDENCE_THRESHOLD)
        {
            _logger.LogInformation("Tesseract succeeded with {Confidence}% confidence",
                tesseractResult.Value.ConfidenceAvg);
            return tesseractResult;
        }

        // Fallback to GOT-OCR2 (accurate but slower)
        _logger.LogWarning("Tesseract confidence {Confidence}% below threshold, " +
            "falling back to GOT-OCR2", tesseractResult.Value?.ConfidenceAvg ?? 0);

        return await _gotOcr2.ExecuteOcrAsync(imageData, config);
    }
}
```

**Registration**:
```csharp
services.AddScoped<IOcrExecutor, SmartOcrExecutor>();
```

**Commit 6**: "feat: Implement smart OCR with automatic fallback"

---

### ⏳ Phase 7: Integration & Documentation (PENDING)
**Final Steps**:
- [ ] Run full integration tests with real CNBV documents
- [ ] Verify both engines work correctly
- [ ] Test fallback mechanism
- [ ] Update README.md with usage examples
- [ ] Document configuration options
- [ ] Performance benchmarks (Tesseract vs GOT-OCR2)
- [ ] CUDA vs CPU performance comparison

**Commit 7**: "docs: Complete GOT-OCR2 integration documentation"

---

## Key Lessons Learned (from GotOcr2Sample sessions)

### Critical Configuration
1. **CSnakes Version**: Use stable 1.2.1 (not beta) for .NET 9/10
2. **torchvision Dependency**: MUST be installed with transformers
   - Missing torchvision causes cryptic "Could not import module 'AutoProcessor'" errors
   - Match versions: `torch 2.9.1` → `torchvision 0.24.1`
3. **Python sys.path**: Clean before importing torch to avoid C extension conflicts
4. **Lazy Imports**: Import heavy libraries inside functions, not at module level
5. **CS1591 Suppression**: Required for CSnakes generated code

### Python Environment
- **DO NOT** use `WithPipInstaller()` alone - it only registers, doesn't execute
- **MUST** call `InstallPackagesFromRequirements()` explicitly after building host
- Use pip (not uv) for PyTorch compatibility
- Keep `requirements.txt` simple - let pip resolve versions

### CUDA Support
- Driver 580+ required for CUDA 13.0
- Small laptop GPUs (RTX A2000) slower than CPU for single images due to overhead
- GPU beneficial for batch processing (batch_size ≥ 4)
- Intelligent device selection implemented: auto/cuda/cpu/force_cuda strategies

---

## File Locations Reference

### Main Project
| Component | Path |
|-----------|------|
| GOT-OCR2 Executor | `Infrastructure.Extraction/GotOcr2/GotOcr2OcrExecutor.cs` |
| Python Wrapper | `Infrastructure.Python.GotOcr2/python/got_ocr2_wrapper.py` |
| Requirements | `Infrastructure.Python.GotOcr2/requirements.txt` |
| DI Extensions | `Infrastructure.Extraction/DependencyInjection/ServiceCollectionExtensions.cs` |
| IOcrExecutor Interface | `Domain/Interfaces/IOcrExecutor.cs` |
| OCR Models | `Domain/Models/OCRConfig.cs`, `Domain/ValueObjects/OCRResult.cs`, `ImageData.cs` |
| ConsoleDemo | `ConsoleApp.GotOcr2Demo/Program.cs` |
| Unit Tests | `Tests.Infrastructure.Extraction/GotOcr2OcrExecutorTests.cs` (to be created) |

### Sample Project (Reference)
| Component | Path |
|-----------|------|
| Working Sample | `Prisma/Samples/GotOcr2Sample/` |
| Lessons Learned | `Prisma/Samples/GotOcr2Sample/LESSONS_LEARNED.md` |
| Handoff Document | `Prisma/Samples/GotOcr2Sample/HANDOFF_NEXT_SESSION.md` |
| .NET 10 Guide | `Prisma/Samples/GotOcr2Sample/NET10_MIGRATION_GUIDE.md` |

---

## Context for Next Session

### Current State (as of Commit 2)
- ✅ GOT-OCR2 integrated into main solution
- ✅ CSnakes source generation working
- ✅ Build succeeds (0 warnings, 0 errors)
- ✅ ConsoleDemo refactored for main solution
- ⏳ Python environment not yet created for main solution
- ⏳ ConsoleDemo not yet tested
- ⏳ Unit tests not yet added
- ⏳ Tesseract implementation pending

### Immediate Next Steps
1. **Create Python venv**: `Infrastructure.Python.GotOcr2/.venv_gotor2`
2. **Install packages**: Run ConsoleDemo to trigger package installation
3. **Test OCR**: Verify GOT-OCR2 works with CNBV documents
4. **Commit 3**: Working ConsoleDemo
5. **Add unit tests**: Create `GotOcr2OcrExecutorTests.cs`
6. **Implement Tesseract**: Second OCR engine
7. **Keyed DI**: Runtime engine selection
8. **Smart fallback**: Automatic Tesseract → GOT-OCR2 based on confidence

### Commands to Run
```bash
# Navigate to solution
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp

# Build ConsoleDemo
dotnet build ConsoleApp.GotOcr2Demo/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj

# Run ConsoleDemo (will create venv and install packages on first run)
dotnet run --project ConsoleApp.GotOcr2Demo/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj

# If successful, test with CNBV document
dotnet run --project ConsoleApp.GotOcr2Demo/ExxerCube.Prisma.ConsoleApp.GotOcr2Demo.csproj -- "../../../../../../Fixtures/PRP1/sample.jpg"
```

### Git State
```bash
# Current branch: kat
# Commits:
#   941bba4 - feat: CSnakes + GOT-OCR2 working on .NET 10.0
#   6f48ac7 - feat: Integrate GOT-OCR2 into main project with SRP architecture
#
# Next commit: feat: Working ConsoleDemo with GOT-OCR2 integration
```

### Dependencies
- **CSnakes.Runtime**: 1.2.1
- **IndQuestResults**: 1.1.0
- **PyTorch**: 2.9.1+cu130
- **torchvision**: 0.24.1
- **transformers**: 4.57.1
- **Python**: 3.13 (redistributable)

### Hardware
- **GPU**: NVIDIA RTX A2000 8GB (CUDA 13.0 compatible)
- **Driver**: 581.80
- **CUDA**: 13.0 support confirmed

### Known Issues
- None currently - build is clean

### Success Metrics Target
- **OCR Confidence**: 88%+ (already achieved in sample)
- **Processing Time**: 5-15s CPU, 1-5s GPU (batch)
- **Language**: Spanish (spa) primary, English (eng) fallback
- **Document Type**: CNBV PRP1 format

---

## Additional Resources

- [CSnakes Documentation](https://github.com/tonybaloney/CSnakes)
- [GOT-OCR2 Model](https://huggingface.co/stepfun-ai/GOT-OCR-2.0-hf)
- [PyTorch Installation](https://pytorch.org/get-started/locally/)
- [TransformersSharp Reference](https://github.com/tonybaloney/TransformersSharp)

---

**Document Version**: 1.0
**Last Updated**: 2025-11-23
**Author**: Abel Briones (with Claude Code assistance)
**Status**: Active Development
