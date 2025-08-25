# Technical Implementation Guide

## Reference Architecture (TransformersSharp Pattern)

### 1. Python Environment Setup
```csharp
// Pattern from TransformerEnvironment.cs
public static class PrismaPythonEnvironment
{
    private static readonly IPythonEnvironment? _env;
    private static readonly Lock _setupLock = new();

    static PrismaPythonEnvironment()
    {
        lock (_setupLock)
        {
            IHostBuilder builder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services
                        .WithPython()
                        .WithHome(appDataPath)
                        .WithVirtualEnvironment(venvPath)
                        .WithUvInstaller()
                        .FromRedistributable();
                });

            var app = builder.Build();
            _env = app.Services.GetRequiredService<IPythonEnvironment>();
        }
    }

    internal static IPrismaOcrWrapper PrismaOcrWrapper => Env.PrismaOcrWrapper();
    internal static IFieldExtractionWrapper FieldExtractionWrapper => Env.FieldExtractionWrapper();
}
```

### 2. Python Wrapper Module Structure
```python
# prisma_ocr_wrapper.py
from typing import Dict, List, Optional, Any
import json
from pathlib import Path

def execute_ocr(image_data: bytes, config: Dict[str, Any]) -> Dict[str, Any]:
    """
    Execute OCR on image data using CSnakes-compatible interface.
    
    Args:
        image_data: Raw image bytes
        config: OCR configuration dictionary
        
    Returns:
        Dictionary containing OCR results
    """
    # Implementation using existing OCR modules
    pass

def extract_fields(text: str, confidence: float) -> Dict[str, Any]:
    """
    Extract structured fields from OCR text.
    
    Args:
        text: OCR text content
        confidence: OCR confidence score
        
    Returns:
        Dictionary containing extracted fields
    """
    # Implementation using existing field extraction modules
    pass

def preprocess_image(image_data: bytes, config: Dict[str, Any]) -> bytes:
    """
    Preprocess image using Python modules.
    
    Args:
        image_data: Raw image bytes
        config: Processing configuration
        
    Returns:
        Preprocessed image bytes
    """
    # Implementation using existing preprocessing modules
    pass
```

### 3. CSnakes Wrapper Classes
```csharp
// OcrWrapper.cs
public class OcrWrapper : IDisposable
{
    private readonly PyObject _wrapperObject;
    private bool _disposed;

    internal OcrWrapper(PyObject wrapperObject)
    {
        _wrapperObject = wrapperObject;
    }

    public static OcrWrapper Create()
    {
        return new OcrWrapper(PrismaPythonEnvironment.PrismaOcrWrapper.CreateOcrWrapper());
    }

    public OCRResult ExecuteOcr(byte[] imageData, OCRConfig config)
    {
        var result = PrismaPythonEnvironment.PrismaOcrWrapper.ExecuteOcr(
            _wrapperObject, 
            imageData, 
            config.ToDictionary());
        
        return OCRResult.FromPythonResult(result);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _wrapperObject?.Dispose();
            _disposed = true;
        }
    }
}
```

### 4. Refactored CSnakesOcrProcessingAdapter
```csharp
public class CSnakesOcrProcessingAdapter : IPythonInteropService, IDisposable
{
    private readonly ILogger<CSnakesOcrProcessingAdapter> _logger;
    private readonly OcrWrapper _ocrWrapper;
    private readonly FieldExtractionWrapper _fieldExtractionWrapper;
    private bool _disposed;

    public CSnakesOcrProcessingAdapter(ILogger<CSnakesOcrProcessingAdapter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ocrWrapper = OcrWrapper.Create();
        _fieldExtractionWrapper = FieldExtractionWrapper.Create();
        
        _logger.LogInformation("Initializing CSnakes OCR processing adapter");
    }

    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        if (imageData == null) throw new ArgumentNullException(nameof(imageData));
        if (config == null) throw new ArgumentNullException(nameof(config));

        _logger.LogInformation("Executing OCR on image {SourcePath} using CSnakes", imageData.SourcePath);
        
        return await Task.Run(() =>
        {
            try
            {
                var ocrResult = _ocrWrapper.ExecuteOcr(imageData.Data, config);
                
                _logger.LogInformation("OCR execution completed for {SourcePath} with confidence {Confidence}", 
                    imageData.SourcePath, ocrResult.ConfidenceAvg);
                
                return Result<OCRResult>.Success(ocrResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing OCR on image {SourcePath}", imageData.SourcePath);
                return Result<OCRResult>.Failure($"OCR execution failed: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _ocrWrapper?.Dispose();
            _fieldExtractionWrapper?.Dispose();
            _disposed = true;
        }
    }
}
```

## Migration Steps

### Step 1: Create Python Environment Infrastructure
1. Create `PrismaPythonEnvironment.cs` following TransformersSharp pattern
2. Add Python wrapper module registration
3. Configure virtual environment and dependencies
4. Add dependency injection extensions

### Step 2: Create Python Wrapper Modules
1. Convert existing CLI scripts to CSnakes-compatible functions
2. Implement proper parameter passing and result conversion
3. Add error handling and logging
4. Test Python modules independently

### Step 3: Create CSnakes Wrapper Classes
1. Create wrapper classes for each Python module
2. Implement proper resource management
3. Add type-safe interfaces
4. Create conversion methods for C#/Python data types

### Step 4: Refactor CSnakesOcrProcessingAdapter
1. Replace process calls with CSnakes wrapper usage
2. Remove temporary file management
3. Implement proper async patterns
4. Add comprehensive error handling

### Step 5: Update Tests
1. Create unit tests for CSnakes wrappers
2. Update integration tests
3. Add performance benchmarks
4. Validate error scenarios

## Key Benefits of This Approach

### Performance Improvements
- **No Process Spawning** - Direct in-process Python execution
- **Reduced Memory Overhead** - No temporary file creation
- **Faster Data Transfer** - Direct object passing vs. file I/O
- **Better Resource Management** - Automatic disposal with CSnakes

### Development Experience
- **Type Safety** - Compile-time checking of Python object usage
- **Better Debugging** - Debug Python code from C# IDE
- **IntelliSense Support** - Full IDE support for Python objects
- **Incremental Binding** - Source code generation capabilities

### Maintainability
- **Cleaner Code** - No process management complexity
- **Better Error Handling** - Exception-based vs. exit code checking
- **Resource Safety** - Automatic disposal prevents leaks
- **Consistent Patterns** - Follows established CSnakes patterns

## Validation Checklist

### Before Implementation
- [ ] Python environment setup working
- [ ] Python wrapper modules tested independently
- [ ] CSnakes wrapper classes created and tested
- [ ] Data type conversion methods implemented
- [ ] Error handling patterns defined

### During Implementation
- [ ] Each method refactored individually
- [ ] Unit tests updated and passing
- [ ] Integration tests validate functionality
- [ ] Performance benchmarks recorded
- [ ] Resource usage monitored

### After Implementation
- [ ] All process-based code removed
- [ ] Performance improvements validated
- [ ] Error handling tested
- [ ] Documentation updated
- [ ] Team training completed