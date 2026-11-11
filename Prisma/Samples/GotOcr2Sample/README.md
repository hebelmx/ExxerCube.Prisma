# GOT-OCR2 Sample Project

## Overview
This sample demonstrates how to implement the `IOcrExecutor` interface using CSnakes to bridge C# with Python's Transformers library and the GOT-OCR2 model.

## Architecture
This project follows hexagonal architecture principles:

```
┌─────────────────────────────────────────────────────────┐
│                   Domain Layer                          │
│  (Pure C# interfaces and models - No dependencies)     │
│                                                         │
│  - IOcrExecutor (interface)                            │
│  - ImageData, OCRResult, OCRConfig (models)            │
└─────────────────────────────────────────────────────────┘
                        ▲
                        │
┌─────────────────────────────────────────────────────────┐
│              Infrastructure Layer                        │
│  (Implementation using CSnakes + Python)                │
│                                                         │
│  - GotOcr2Executor : IOcrExecutor                      │
│  - CSnakes bridge to Python                            │
└─────────────────────────────────────────────────────────┘
                        ▲
                        │
┌─────────────────────────────────────────────────────────┐
│                Python Layer                             │
│  (got_ocr2_wrapper.py)                                 │
│                                                         │
│  - execute_ocr() function                              │
│  - GOT-OCR2 model loading and inference                │
└─────────────────────────────────────────────────────────┘
```

## Project Structure

```
GotOcr2Sample/
├── Domain/                      # Pure domain layer (copied from Prisma)
│   ├── Interfaces/
│   │   └── IOcrExecutor.cs
│   ├── ValueObjects/
│   │   ├── ImageData.cs
│   │   └── OCRResult.cs
│   └── Models/
│       └── OCRConfig.cs
├── PythonOcrLib/               # Python library with CSnakes integration
│   ├── got_ocr2_wrapper.py     # Main Python OCR wrapper
│   ├── requirements.txt
│   └── tests/
│       └── test_got_ocr2_wrapper.py
├── Infrastructure/             # C# implementation
│   ├── GotOcr2Executor.cs      # IOcrExecutor implementation
│   └── CSnakesOcrAdapter.cs
├── Tests/                      # C# unit and integration tests
│   ├── GotOcr2ExecutorTests.cs
│   └── IntegrationTests.cs
└── ConsoleDemo/               # Demo console application
    └── Program.cs
```

## Requirements

### Python Requirements
- Python 3.10+
- transformers
- torch (with CUDA support recommended)
- Pillow

### C# Requirements
- .NET 9.0 or later
- CSnakes.Runtime package
- IndQuestResults (for Result<T> type)

## Setup Instructions

### 1. Create Python Virtual Environment
```bash
cd PythonOcrLib
python -m venv .venv
.venv\Scripts\activate  # Windows
pip install -r requirements.txt
```

### 2. Download GOT-OCR2 Model
The model will be automatically downloaded on first run (requires ~5GB disk space).

### 3. Build C# Projects
```bash
cd ..
dotnet restore
dotnet build
```

## Usage

### Running Python Tests
```bash
cd PythonOcrLib
pytest tests/ -v
```

### Running C# Tests
```bash
dotnet test Tests/Tests.csproj
```

### Running Console Demo
```bash
dotnet run --project ConsoleDemo/ConsoleDemo.csproj
```

## A/B Testing vs Tesseract

This implementation allows for direct A/B comparison with Tesseract:

```csharp
// Inject different implementations
IOcrExecutor tesseractOcr = new TesseractExecutor();
IOcrExecutor gotOcr2 = new GotOcr2Executor();

// Same interface, different engines
var result1 = await tesseractOcr.ExecuteOcrAsync(imageData, config);
var result2 = await gotOcr2.ExecuteOcrAsync(imageData, config);
```

## Performance Notes

- **First Run**: Model download and initialization (~30 seconds)
- **Subsequent Runs**: Cached model loads faster (~3-5 seconds)
- **CUDA**: Recommended for production use (10-20x faster)
- **CPU**: Works but slower, suitable for development/testing

## Integration with Main Prisma Solution

Once validated, this implementation can be integrated into the main Prisma solution:
1. Copy `GotOcr2Executor` to `Prisma.Infrastructure` project
2. Register in DI container alongside existing `OcrProcessingAdapter`
3. Add configuration to select between Tesseract and GOT-OCR2

## License
Part of ExxerCube.Prisma solution
