# TODO Implementation Guide - ExxerCube.Prisma

## Overview

This guide provides step-by-step instructions for completing the TODO items in the ExxerCube.Prisma codebase. The focus is on replacing placeholder implementations with real Python module integration and updating tests to use actual functionality instead of mocks. **Updated for Sprint 5 with comprehensive quality assurance implementation.**

## Prerequisites

### Environment Setup

1. **Python Environment**
   ```bash
   # Install Python 3.9+ and required packages
   cd Python
   pip install -r requirements.txt
   
   # Verify Python modules are accessible
   python -c "import ocr_modules; print('Modules available')"
   ```

2. **Test Data Preparation**
   ```bash
   # Copy test documents to test directory
   cp Python/DumyPrisma1.png Tests/TestData/
   cp Python/Cleaned.png Tests/TestData/
   ```

3. **Build Environment**
   ```bash
   # Ensure solution builds without warnings
   dotnet build
   dotnet test --verbosity normal
   ```

4. **Quality Tools Setup**
   ```bash
   # Install Stryker.NET for mutation testing
   dotnet tool install -g StrykerMutator.Core
   
   # Install Playwright for E2E testing
   dotnet tool install -g Microsoft.Playwright.CLI
   playwright install
   ```

## Implementation Steps

### Step 1: Complete OcrProcessingAdapter Implementation

#### 1.1 Implement BinarizeAsync Method

**File**: `Infrastructure/Python/OcrProcessingAdapter.cs`  
**Line**: 99

**Current Implementation**:
```csharp
public Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
{
    _logger.LogInformation("Binarizing image {SourcePath}", imageData.SourcePath);
    // TODO: Implement binarization using Python interop service
    // For now, return the original image
    return Task.FromResult(Result<ImageData>.Success(imageData));
}
```

**Target Implementation**:
```csharp
public async Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
{
    _logger.LogInformation("Binarizing image {SourcePath}", imageData.SourcePath);
    
    try
    {
        // Call Python binarization module through interop service
        var result = await _pythonInteropService.BinarizeAsync(imageData);
        
        if (result.IsSuccess)
        {
            _logger.LogInformation("Image binarization completed for {SourcePath}", imageData.SourcePath);
        }
        else
        {
            _logger.LogWarning("Image binarization failed for {SourcePath}: {Error}", 
                imageData.SourcePath, result.Error);
        }
        
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during image binarization for {SourcePath}", imageData.SourcePath);
        return Result<ImageData>.Failure($"Image binarization failed: {ex.Message}");
    }
}
```

#### 1.2 Implement ExtractExpedienteAsync Method

**File**: `Infrastructure/Python/OcrProcessingAdapter.cs`  
**Line**: 112

**Current Implementation**:
```csharp
public Task<Result<string?>> ExtractExpedienteAsync(string text)
{
    _logger.LogInformation("Extracting expediente from text");
    // TODO: Implement expediente extraction using Python interop service
    // For now, return a placeholder
    return Task.FromResult(Result<string?>.Success("EXP-2024-001"));
}
```

**Target Implementation**:
```csharp
public async Task<Result<string?>> ExtractExpedienteAsync(string text)
{
    _logger.LogInformation("Extracting expediente from text");
    
    try
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string?>.Success(null);
        }
        
        // Call Python expediente extraction module through interop service
        var result = await _pythonInteropService.ExtractExpedienteAsync(text);
        
        if (result.IsSuccess)
        {
            var expediente = result.Value;
            _logger.LogInformation("Expediente extraction completed: {Expediente}", expediente);
            return Result<string?>.Success(expediente);
        }
        else
        {
            _logger.LogWarning("Expediente extraction failed: {Error}", result.Error);
            return Result<string?>.Success(null); // Return null instead of failure for missing expediente
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during expediente extraction");
        return Result<string?>.Failure($"Expediente extraction failed: {ex.Message}");
    }
}
```

#### 1.3 Implement ExtractCausaAsync Method

**File**: `Infrastructure/Python/OcrProcessingAdapter.cs`  
**Line**: 125

**Current Implementation**:
```csharp
public Task<Result<string?>> ExtractCausaAsync(string text)
{
    _logger.LogInformation("Extracting causa from text");
    // TODO: Implement causa extraction using Python interop service
    // For now, return a placeholder
    return Task.FromResult(Result<string?>.Success("Civil"));
}
```

**Target Implementation**:
```csharp
public async Task<Result<string?>> ExtractCausaAsync(string text)
{
    _logger.LogInformation("Extracting causa from text");
    
    try
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string?>.Success(null);
        }
        
        // Call Python section extraction module for causa
        var result = await _pythonInteropService.ExtractCausaAsync(text);
        
        if (result.IsSuccess)
        {
            var causa = result.Value;
            _logger.LogInformation("Causa extraction completed: {Causa}", causa);
            return Result<string?>.Success(causa);
        }
        else
        {
            _logger.LogWarning("Causa extraction failed: {Error}", result.Error);
            return Result<string?>.Success(null);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during causa extraction");
        return Result<string?>.Failure($"Causa extraction failed: {ex.Message}");
    }
}
```

#### 1.4 Implement ExtractAccionSolicitadaAsync Method

**File**: `Infrastructure/Python/OcrProcessingAdapter.cs`  
**Line**: 138

**Current Implementation**:
```csharp
public Task<Result<string?>> ExtractAccionSolicitadaAsync(string text)
{
    _logger.LogInformation("Extracting accion solicitada from text");
    // TODO: Implement accion solicitada extraction using Python interop service
    // For now, return a placeholder
    return Task.FromResult(Result<string?>.Success("Compensación"));
}
```

**Target Implementation**:
```csharp
public async Task<Result<string?>> ExtractAccionSolicitadaAsync(string text)
{
    _logger.LogInformation("Extracting accion solicitada from text");
    
    try
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string?>.Success(null);
        }
        
        // Call Python section extraction module for accion solicitada
        var result = await _pythonInteropService.ExtractAccionSolicitadaAsync(text);
        
        if (result.IsSuccess)
        {
            var accion = result.Value;
            _logger.LogInformation("Accion solicitada extraction completed: {Accion}", accion);
            return Result<string?>.Success(accion);
        }
        else
        {
            _logger.LogWarning("Accion solicitada extraction failed: {Error}", result.Error);
            return Result<string?>.Success(null);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during accion solicitada extraction");
        return Result<string?>.Failure($"Accion solicitada extraction failed: {ex.Message}");
    }
}
```

#### 1.5 Implement ExtractDatesAsync Method

**File**: `Infrastructure/Python/OcrProcessingAdapter.cs`  
**Line**: 151

**Current Implementation**:
```csharp
public Task<Result<List<string>>> ExtractDatesAsync(string text)
{
    _logger.LogInformation("Extracting dates from text");
    // TODO: Implement date extraction using Python interop service
    // For now, return a placeholder
    return Task.FromResult(Result<List<string>>.Success(new List<string> { "2024-01-15" }));
}
```

**Target Implementation**:
```csharp
public async Task<Result<List<string>>> ExtractDatesAsync(string text)
{
    _logger.LogInformation("Extracting dates from text");
    
    try
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<List<string>>.Success(new List<string>());
        }
        
        // Call Python date extraction module through interop service
        var result = await _pythonInteropService.ExtractDatesAsync(text);
        
        if (result.IsSuccess)
        {
            var dates = result.Value;
            _logger.LogInformation("Date extraction completed: {DateCount} dates found", dates.Count);
            return Result<List<string>>.Success(dates);
        }
        else
        {
            _logger.LogWarning("Date extraction failed: {Error}", result.Error);
            return Result<List<string>>.Success(new List<string>());
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during date extraction");
        return Result<List<string>>.Failure($"Date extraction failed: {ex.Message}");
    }
}
```

#### 1.6 Implement ExtractAmountsAsync Method

**File**: `Infrastructure/Python/OcrProcessingAdapter.cs`  
**Line**: 164

**Current Implementation**:
```csharp
public Task<Result<List<AmountData>>> ExtractAmountsAsync(string text)
{
    _logger.LogInformation("Extracting amounts from text");
    // TODO: Implement amount extraction using Python interop service
    // For now, return a placeholder
    return Task.FromResult(Result<List<AmountData>>.Success(new List<AmountData> 
    { 
        new AmountData { Value = 1000.00m, Currency = "MXN" } 
    }));
}
```

**Target Implementation**:
```csharp
public async Task<Result<List<AmountData>>> ExtractAmountsAsync(string text)
{
    _logger.LogInformation("Extracting amounts from text");
    
    try
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<List<AmountData>>.Success(new List<AmountData>());
        }
        
        // Call Python amount extraction module through interop service
        var result = await _pythonInteropService.ExtractAmountsAsync(text);
        
        if (result.IsSuccess)
        {
            var amounts = result.Value;
            _logger.LogInformation("Amount extraction completed: {AmountCount} amounts found", amounts.Count);
            return Result<List<AmountData>>.Success(amounts);
        }
        else
        {
            _logger.LogWarning("Amount extraction failed: {Error}", result.Error);
            return Result<List<AmountData>>.Success(new List<AmountData>());
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during amount extraction");
        return Result<List<AmountData>>.Failure($"Amount extraction failed: {ex.Message}");
    }
}
```

### Step 2: Update CSnakesOcrProcessingAdapter

#### 2.1 Add Missing Methods to IPythonInteropService Interface

**File**: `Domain/Interfaces/IPythonInteropService.cs`

Add the following methods to the interface:

```csharp
/// <summary>
/// Binarizes an image using the Python binarization module.
/// </summary>
/// <param name="imageData">The image data to binarize.</param>
/// <returns>A result containing the binarized image or an error.</returns>
Task<Result<ImageData>> BinarizeAsync(ImageData imageData);

/// <summary>
/// Extracts expediente (case file number) from text using the Python expediente extractor.
/// </summary>
/// <param name="text">The text to process.</param>
/// <returns>A result containing the extracted expediente or an error.</returns>
Task<Result<string?>> ExtractExpedienteAsync(string text);

/// <summary>
/// Extracts causa (cause) from text using the Python section extractor.
/// </summary>
/// <param name="text">The text to process.</param>
/// <returns>A result containing the extracted causa or an error.</returns>
Task<Result<string?>> ExtractCausaAsync(string text);

/// <summary>
/// Extracts accion solicitada (requested action) from text using the Python section extractor.
/// </summary>
/// <param name="text">The text to process.</param>
/// <returns>A result containing the extracted accion solicitada or an error.</returns>
Task<Result<string?>> ExtractAccionSolicitadaAsync(string text);

/// <summary>
/// Extracts dates from text using the Python date extractor.
/// </summary>
/// <param name="text">The text to process.</param>
/// <returns>A result containing the extracted dates or an error.</returns>
Task<Result<List<string>>> ExtractDatesAsync(string text);

/// <summary>
/// Extracts monetary amounts from text using the Python amount extractor.
/// </summary>
/// <param name="text">The text to process.</param>
/// <returns>A result containing the extracted amounts or an error.</returns>
Task<Result<List<AmountData>>> ExtractAmountsAsync(string text);
```

#### 2.2 Implement Missing Methods in CSnakesOcrProcessingAdapter

**File**: `Infrastructure/Python/CSnakesOcrProcessingAdapter.cs`

Add the following implementations:

```csharp
/// <summary>
/// Binarizes an image using the Python binarization module.
/// </summary>
/// <param name="imageData">The image data to binarize.</param>
/// <returns>A result containing the binarized image or an error.</returns>
public async Task<Result<ImageData>> BinarizeAsync(ImageData imageData)
{
    if (imageData == null) throw new ArgumentNullException(nameof(imageData));

    _logger.LogInformation("Binarizing image {SourcePath} using Python binarization module", imageData.SourcePath);
    
    return await Task.Run(() =>
    {
        try
        {
            // Create temporary file for the image data
            var tempInputPath = Path.GetTempFileName() + ".png";
            var tempOutputDir = Path.Combine(Path.GetTempPath(), $"binarize_output_{Guid.NewGuid()}");
            
            try
            {
                // Write image data to temporary file
                File.WriteAllBytes(tempInputPath, imageData.Data);
                
                // Create output directory
                Directory.CreateDirectory(tempOutputDir);
                
                // Call Python binarization module
                var pythonScriptPath = Path.Combine(_pythonModulesPath, "image_binarizer.py");
                var arguments = $"\"{pythonScriptPath}\" --input \"{tempInputPath}\" --output \"{tempOutputDir}\" --method adaptive_gaussian";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(pythonScriptPath)
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                
                process.WaitForExit(30000);
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Python binarization failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                    return Result<ImageData>.Failure($"Python binarization failed: {error}");
                }
                
                // Read the binarized image
                var outputPath = Path.Combine(tempOutputDir, Path.GetFileName(tempInputPath));
                if (!File.Exists(outputPath))
                {
                    _logger.LogError("Python binarization did not generate expected output file: {OutputPath}", outputPath);
                    return Result<ImageData>.Failure("Python binarization did not generate expected output file");
                }
                
                var binarizedData = File.ReadAllBytes(outputPath);
                
                var binarizedImage = new ImageData
                {
                    Data = binarizedData,
                    SourcePath = imageData.SourcePath,
                    PageNumber = imageData.PageNumber,
                    TotalPages = imageData.TotalPages
                };

                _logger.LogInformation("Image binarization completed for {SourcePath}", imageData.SourcePath);
                return Result<ImageData>.Success(binarizedImage);
            }
            finally
            {
                // Cleanup temporary files
                try
                {
                    if (File.Exists(tempInputPath))
                        File.Delete(tempInputPath);
                    
                    if (Directory.Exists(tempOutputDir))
                        Directory.Delete(tempOutputDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temporary files");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error binarizing image {SourcePath}", imageData.SourcePath);
            return Result<ImageData>.Failure($"Image binarization failed: {ex.Message}");
        }
    });
}

/// <summary>
/// Extracts expediente (case file number) from text using the Python expediente extractor.
/// </summary>
/// <param name="text">The text to process.</param>
/// <returns>A result containing the extracted expediente or an error.</returns>
public async Task<Result<string?>> ExtractExpedienteAsync(string text)
{
    if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text cannot be null or empty", nameof(text));

    _logger.LogInformation("Extracting expediente from text using Python expediente extractor");
    
    return await Task.Run(() =>
    {
        try
        {
            // Create temporary file for the text
            var tempInputPath = Path.GetTempFileName() + ".txt";
            var tempOutputDir = Path.Combine(Path.GetTempPath(), $"expediente_output_{Guid.NewGuid()}");
            
            try
            {
                // Write text to temporary file
                File.WriteAllText(tempInputPath, text);
                
                // Create output directory
                Directory.CreateDirectory(tempOutputDir);
                
                // Call Python expediente extraction module
                var pythonScriptPath = Path.Combine(_pythonModulesPath, "expediente_extractor.py");
                var arguments = $"\"{pythonScriptPath}\" --input \"{tempInputPath}\" --output \"{tempOutputDir}\"";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(pythonScriptPath)
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                
                process.WaitForExit(30000);
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Python expediente extraction failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                    return Result<string?>.Failure($"Python expediente extraction failed: {error}");
                }
                
                // Read the extracted expediente
                var outputPath = Path.Combine(tempOutputDir, "expediente.txt");
                if (!File.Exists(outputPath))
                {
                    _logger.LogWarning("No expediente found in text");
                    return Result<string?>.Success(null);
                }
                
                var expediente = File.ReadAllText(outputPath).Trim();
                
                if (string.IsNullOrWhiteSpace(expediente))
                {
                    return Result<string?>.Success(null);
                }

                _logger.LogInformation("Expediente extraction completed: {Expediente}", expediente);
                return Result<string?>.Success(expediente);
            }
            finally
            {
                // Cleanup temporary files
                try
                {
                    if (File.Exists(tempInputPath))
                        File.Delete(tempInputPath);
                    
                    if (Directory.Exists(tempOutputDir))
                        Directory.Delete(tempOutputDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temporary files");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting expediente from text");
            return Result<string?>.Failure($"Expediente extraction failed: {ex.Message}");
        }
    });
}

// Similar implementations for ExtractCausaAsync, ExtractAccionSolicitadaAsync, 
// ExtractDatesAsync, and ExtractAmountsAsync methods...
```

### Step 3: Update Tests to Use Real Python Modules

#### 3.1 Update PythonInteropServiceTests.cs

**File**: `Tests/Infrastructure/PythonInteropServiceTests.cs`

Replace mock-based tests with real integration tests:

```csharp
/// <summary>
/// Tests for the Python interop service using real Python modules.
/// </summary>
public class PythonInteropServiceTests : IDisposable
{
    private readonly ILogger<CSnakesOcrProcessingAdapter> _logger;
    private readonly string _pythonModulesPath;
    private readonly string _testDataPath;
    private readonly CSnakesOcrProcessingAdapter _adapter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonInteropServiceTests"/> class.
    /// </summary>
    public PythonInteropServiceTests()
    {
        _logger = XUnitLogger.CreateLogger<CSnakesOcrProcessingAdapter>(new TestOutputHelper());
        _pythonModulesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Python", "ocr_modules");
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        
        // Ensure test data directory exists
        if (!Directory.Exists(_testDataPath))
        {
            Directory.CreateDirectory(_testDataPath);
        }
        
        _adapter = new CSnakesOcrProcessingAdapter(_logger, _pythonModulesPath);
    }

    /// <summary>
    /// Tests that expediente extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractExpediente_WithRealDocument_ReturnsActualExpediente()
    {
        // Arrange
        var testText = "En relación al Expediente: ABC-123/2023, se requiere...";
        
        // Act
        var result = await _adapter.ExtractExpedienteAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("ABC-123/2023");
    }

    /// <summary>
    /// Tests that date extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractDates_WithRealDocument_ReturnsActualDates()
    {
        // Arrange
        var testText = "Fecha: 15 de octubre de 2023 y también 2023-12-25";
        
        // Act
        var result = await _adapter.ExtractDatesAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("2023-10-15");
        result.Value.ShouldContain("2023-12-25");
    }

    /// <summary>
    /// Tests that amount extraction works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractAmounts_WithRealDocument_ReturnsActualAmounts()
    {
        // Arrange
        var testText = "Monto: $1,500.75 y total: $2,000.00";
        
        // Act
        var result = await _adapter.ExtractAmountsAsync(testText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThan(0);
        
        var values = result.Value.Select(a => a.Value).ToList();
        values.ShouldContain(1500.75m);
        values.ShouldContain(2000.00m);
    }

    /// <summary>
    /// Tests that image binarization works with real Python module.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task BinarizeImage_WithRealImage_ReturnsBinarizedImage()
    {
        // Arrange
        var testImagePath = Path.Combine(_testDataPath, "DumyPrisma1.png");
        if (!File.Exists(testImagePath))
        {
            // Skip test if test image not available
            return;
        }
        
        var imageData = new ImageData
        {
            Data = File.ReadAllBytes(testImagePath),
            SourcePath = "DumyPrisma1.png",
            PageNumber = 1,
            TotalPages = 1
        };
        
        // Act
        var result = await _adapter.BinarizeAsync(imageData);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Data.ShouldNotBeNull();
        result.Value.Data.Length.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Tests that the adapter handles empty text gracefully.
    /// </summary>
    [Fact]
    public async Task ExtractExpediente_WithEmptyText_ReturnsNull()
    {
        // Arrange
        var emptyText = "";
        
        // Act
        var result = await _adapter.ExtractExpedienteAsync(emptyText);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    /// <summary>
    /// Tests that the adapter handles null text gracefully.
    /// </summary>
    [Fact]
    public async Task ExtractExpediente_WithNullText_ThrowsArgumentException()
    {
        // Arrange
        string? nullText = null;
        
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await _adapter.ExtractExpedienteAsync(nullText!);
        });
    }

    /// <summary>
    /// Disposes the test resources.
    /// </summary>
    public void Dispose()
    {
        _adapter?.Dispose();
    }
}
```

#### 3.2 Create Integration Test Configuration

**File**: `Tests/TestConfiguration.cs`

```csharp
/// <summary>
/// Configuration for integration tests.
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Gets the Python modules path for testing.
    /// </summary>
    public static string PythonModulesPath => Path.Combine(
        Directory.GetCurrentDirectory(), 
        "..", "..", "..", "Python", "ocr_modules");

    /// <summary>
    /// Gets the test data path.
    /// </summary>
    public static string TestDataPath => Path.Combine(
        Directory.GetCurrentDirectory(), "TestData");

    /// <summary>
    /// Checks if Python environment is available for testing.
    /// </summary>
    /// <returns>True if Python environment is available.</returns>
    public static bool IsPythonEnvironmentAvailable()
    {
        try
        {
            var pythonPath = PythonModulesPath;
            return Directory.Exists(pythonPath) && 
                   File.Exists(Path.Combine(pythonPath, "expediente_extractor.py"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets test documents for integration testing.
    /// </summary>
    /// <returns>List of test document paths.</returns>
    public static List<string> GetTestDocuments()
    {
        var testDataPath = TestDataPath;
        if (!Directory.Exists(testDataPath))
        {
            return new List<string>();
        }

        return Directory.GetFiles(testDataPath, "*.png")
            .Concat(Directory.GetFiles(testDataPath, "*.jpg"))
            .Concat(Directory.GetFiles(testDataPath, "*.pdf"))
            .ToList();
    }
}
```

### Step 4: Implement Quality Assurance Tools

#### 4.1 Configure Test Coverage

**File**: `Tests/ExxerCube.Prisma.Tests.csproj`

Update the test project to include coverage configuration:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    
    <PackageReference Include="xunit.runner.visualstudio" >
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Shouldly"  />
    <PackageReference Include="NSubstitute"  />
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Logging" />
    <PackageReference Include="Meziantou.Extensions.Logging.Xunit.v3" />
    <PackageReference Include="Microsoft.Playwright" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Domain\ExxerCube.Prisma.Domain.csproj" />
    <ProjectReference Include="..\Application\ExxerCube.Prisma.Application.csproj" />
    <ProjectReference Include="..\Infrastructure\ExxerCube.Prisma.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="TestData\**\*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

#### 4.2 Add Stryker.NET Configuration

**File**: `stryker-config.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/stryker-mutator/stryker/master/packages/core/schema/stryker-schema.json",
  "stryker-config": {
    "$schema": "./node_modules/@stryker-mutator/core/schema/stryker-schema.json",
    "packageManager": "dotnet",
    "reporters": [
      "html",
      "cleartext",
      "progress"
    ],
    "testRunner": "dotnet",
    "coverageAnalysis": "perTest",
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 0
    },
    "mutate": [
      "**/*.cs"
    ],
    "excludedMutations": [
      "string"
    ],
    "testProjects": [
      "Tests/ExxerCube.Prisma.Tests.csproj"
    ]
  }
}
```

#### 4.3 Add Playwright Configuration

**File**: `playwright.config.cs`

```csharp
using Microsoft.Playwright;

namespace ExxerCube.Prisma.Tests;

/// <summary>
/// Playwright configuration for E2E tests.
/// </summary>
public class PlaywrightConfig
{
    /// <summary>
    /// Gets the Playwright configuration.
    /// </summary>
    /// <returns>The Playwright configuration.</returns>
    public static IPlaywright CreatePlaywright()
    {
        return Playwright.CreateAsync().Result;
    }

    /// <summary>
    /// Gets the browser configuration.
    /// </summary>
    /// <returns>The browser configuration.</returns>
    public static BrowserTypeLaunchOptions GetBrowserOptions()
    {
        return new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 1000
        };
    }

    /// <summary>
    /// Gets the browser context options.
    /// </summary>
    /// <returns>The browser context options.</returns>
    public static BrowserNewContextOptions GetContextOptions()
    {
        return new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1920,
                Height = 1080
            },
            RecordVideoDir = "videos/"
        };
    }
}
```

### Step 5: Update CI/CD Configuration

#### 5.1 Add Python Environment Setup

**File**: `.github/workflows/test.yml`

```yaml
name: Test

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Setup Python
      uses: actions/setup-python@v4
      with:
        python-version: '3.9'
    
    - name: Install Python dependencies
      run: |
        cd Prisma/Code/Src/CSharp/Python
        pip install -r requirements.txt
    
    - name: Copy test data
      run: |
        mkdir -p Prisma/Code/Src/CSharp/Tests/TestData
        cp Prisma/Code/Src/CSharp/Python/DumyPrisma1.png Prisma/Code/Src/CSharp/Tests/TestData/
        cp Prisma/Code/Src/CSharp/Python/Cleaned.png Prisma/Code/Src/CSharp/Tests/TestData/
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
    
    - name: Upload coverage reports
      uses: codecov/codecov-action@v3
      with:
        file: ./TestResults/coverage.cobertura.xml

  mutation-test:
    runs-on: ubuntu-latest
    needs: test
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Install Stryker.NET
      run: dotnet tool install -g StrykerMutator.Core
    
    - name: Run mutation tests
      run: |
        cd Prisma/Code/Src/CSharp
        stryker run --config-file-path stryker-config.json

  e2e-test:
    runs-on: ubuntu-latest
    needs: test
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Install Playwright
      run: |
        dotnet tool install -g Microsoft.Playwright.CLI
        playwright install
    
    - name: Start application
      run: |
        cd Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI
        dotnet run &
        sleep 30
    
    - name: Run E2E tests
      run: |
        cd Prisma/Code/Src/CSharp/Tests
        dotnet test --filter "Category=E2E" --verbosity normal
```

### Step 6: Update Test Categories

#### 6.1 Add Test Categories

Update test attributes to include proper categories:

```csharp
[Fact]
[Trait("Category", "Unit")]
public void TestName() { }

[Fact]
[Trait("Category", "Integration")]
public void TestName() { }

[Fact]
[Trait("Category", "E2E")]
public void TestName() { }
```

#### 6.2 Run Tests by Category

```bash
# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run only E2E tests
dotnet test --filter "Category=E2E"

# Run all tests except E2E
dotnet test --filter "Category!=E2E"
```

## Validation Steps

### 1. Build Validation

```bash
# Ensure solution builds without warnings
dotnet build --verbosity normal

# Check for any remaining TODO comments
grep -r "TODO" . --include="*.cs" --exclude-dir=obj --exclude-dir=bin
```

### 2. Test Validation

```bash
# Run all tests
dotnet test --verbosity normal

# Run integration tests specifically
dotnet test --filter "Category=Integration" --verbosity normal

# Check test coverage
dotnet test --collect:"XPlat Code Coverage" --verbosity normal

# Run mutation tests
stryker run --config-file-path stryker-config.json

# Run E2E tests
dotnet test --filter "Category=E2E" --verbosity normal
```

### 3. Integration Validation

```bash
# Test Python module availability
cd Python
python -c "import ocr_modules; print('Modules available')"

# Test Python CLI
python modular_ocr_cli.py --input DumyPrisma1.png --outdir test_output --verbose
```

### 4. Manual Testing

1. **Upload Test Document**: Use the web interface to upload `DumyPrisma1.png`
2. **Verify Processing**: Check that real expediente, dates, and amounts are extracted
3. **Check Logs**: Verify that Python integration is working in logs
4. **Test Error Scenarios**: Try uploading invalid files and check error handling

## Troubleshooting

### Common Issues

1. **Python Module Not Found**
   - Ensure Python modules are in the correct path
   - Check Python environment and dependencies
   - Verify module imports work in Python

2. **Test Failures**
   - Check test data availability
   - Verify Python environment in CI/CD
   - Check test isolation and cleanup

3. **Performance Issues**
   - Monitor Python process execution time
   - Check for memory leaks in Python processes
   - Implement proper timeout handling

4. **Quality Tool Issues**
   - Ensure Stryker.NET is properly installed
   - Check Playwright browser installation
   - Verify coverage tool configuration

### Debug Steps

1. **Enable Debug Logging**
   ```csharp
   // In test setup
   var logger = XUnitLogger.CreateLogger<CSnakesOcrProcessingAdapter>(output);
   ```

2. **Check Python Output**
   ```csharp
   // In Python interop service
   _logger.LogDebug("Python output: {Output}", output);
   _logger.LogDebug("Python error: {Error}", error);
   ```

3. **Verify File Paths**
   ```csharp
   // Check if Python modules exist
   var modulePath = Path.Combine(_pythonModulesPath, "expediente_extractor.py");
   if (!File.Exists(modulePath))
   {
       _logger.LogError("Python module not found: {ModulePath}", modulePath);
   }
   ```

## Success Criteria

### Technical Success
- [ ] All TODO comments in OcrProcessingAdapter.cs are resolved
- [ ] All tests pass with real Python modules
- [ ] No build warnings (TreatWarningsAsErrors)
- [ ] Test coverage remains above 90%
- [ ] Integration tests validate real functionality
- [ ] Mutation testing score above 80%
- [ ] E2E tests are implemented and passing

### Functional Success
- [ ] Real expediente extraction works
- [ ] Real date extraction works
- [ ] Real amount extraction works
- [ ] Real image binarization works
- [ ] Error handling works properly
- [ ] Performance is acceptable

### Quality Success
- [ ] Code follows project standards
- [ ] XML documentation is complete
- [ ] Error logging is comprehensive
- [ ] Tests are reliable and maintainable
- [ ] CI/CD pipeline passes consistently
- [ ] Quality gates are implemented and passing
- [ ] Coverage thresholds are maintained
