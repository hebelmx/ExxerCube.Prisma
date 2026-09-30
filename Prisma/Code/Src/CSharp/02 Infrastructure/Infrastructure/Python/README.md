# CSnakes Configuration for Prisma Infrastructure

This document describes the CSnakes configuration for the ExxerCube.Prisma.Infrastructure project, which provides type-safe Python integration for OCR processing.

## Configuration Overview

The project is configured to use CSnakes for seamless Python integration with the following key features:

### Project File Configuration

The `ExxerCube.Prisma.Infrastructure.csproj` includes the following CSnakes-specific settings:

```xml
<PropertyGroup>
    <!-- CSnakes Configuration -->
    <EmbedPythonSources>true</EmbedPythonSources>
    <DefaultPythonItems>true</DefaultPythonItems>
    <PythonRoot>Python</PythonRoot>
</PropertyGroup>
```

### Key Configuration Options

- **`EmbedPythonSources`**: Set to `true` to embed Python source files into the generated .NET assemblies
- **`DefaultPythonItems`**: Set to `true` to automatically discover all `.py` and `.pyi` files in the project
- **`PythonRoot`**: Set to `Python` to define the root namespace for Python modules

## Python Module Structure

The project includes the following Python modules:

```
Infrastructure/Python/
├── python/
│   └── prisma_ocr_wrapper.py    # Main OCR wrapper module
└── PrismaPythonEnvironment.cs   # CSnakes environment manager
```

### Prisma OCR Wrapper

The `prisma_ocr_wrapper.py` module provides CSnakes-compatible functions for OCR processing:

- `execute_ocr(image_data: bytes, config: Dict[str, Any]) -> Dict[str, Any]`
- `extract_fields_from_text(text: str, confidence: float) -> Dict[str, Any]`

## Usage Examples

### Basic Usage

```csharp
using ExxerCube.Prisma.Infrastructure.Python;

// Get the Python environment
var pythonEnv = PrismaPythonEnvironment.Env;

// Access the generated module wrapper
dynamic prismaModule = pythonEnv.PrismaOcrWrapper();

// Call Python functions with type-safe bindings
var result = prismaModule.ExecuteOcr(imageData, config);
```

### Service Integration

```csharp
// Register the service in DI container
services.AddScoped<IOcrProcessingService, PrismaOcrService>();

// Use in application code
public class MyService
{
    private readonly IOcrProcessingService _ocrService;
    
    public MyService(IOcrProcessingService ocrService)
    {
        _ocrService = ocrService;
    }
    
    public async Task<OcrResult> ProcessDocument(ImageData imageData)
    {
        var config = new OcrConfig
        {
            Language = "spa",
            FallbackLanguage = "eng",
            Oem = 3,
            Psm = 6
        };
        
        return await _ocrService.ProcessImageAsync(imageData, config);
    }
}
```

## Error Handling

The configuration includes proper error handling for Python exceptions:

```csharp
try
{
    var result = prismaModule.ExecuteOcr(imageData, config);
}
catch (PythonInvocationException ex)
{
    // Handle Python-specific errors
    _logger.LogError("Python error: {Message}", ex.Message);
}
catch (Exception ex)
{
    // Handle general errors
    _logger.LogError("Unexpected error: {Message}", ex.Message);
}
```

## Environment Setup

The `PrismaPythonEnvironment` class automatically:

1. Creates a Python environment in `%LOCALAPPDATA%\PrismaPython`
2. Installs required Python packages from `requirements.txt`
3. Configures virtual environment isolation
4. Provides type-safe access to Python modules

### Required Python Packages

The following packages are automatically installed:

- `pytesseract` - OCR engine
- `Pillow` - Image processing
- `opencv-python` - Computer vision
- `numpy` - Numerical computing
- `pandas` - Data manipulation
- `python-dateutil` - Date parsing
- `regex` - Advanced regex support

## Build Process

During the build process, CSnakes will:

1. Discover all Python files in the project
2. Generate C# bindings for type-annotated functions
3. Embed Python sources (if enabled)
4. Create strongly-typed method signatures

## Troubleshooting

### Common Issues

1. **Python Environment Not Found**: Ensure Python 3.12+ is installed or use the redistributable option
2. **Missing Dependencies**: Check that all required Python packages are listed in `requirements.txt`
3. **Type Generation Errors**: Verify that Python functions have proper type annotations

### Debugging

Enable detailed logging to troubleshoot issues:

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

## References

- [CSnakes Documentation](../docs/Csnakes/)
- [Basic Usage Guide](../docs/Csnakes/basic-usage.md)
- [Configuration Guide](../docs/Csnakes/configuration.md)
- [Type System Guide](../docs/Csnakes/type-system.md)
