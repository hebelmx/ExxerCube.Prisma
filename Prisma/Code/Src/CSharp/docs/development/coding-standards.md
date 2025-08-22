# Coding Standards and Guidelines

## Overview

This document defines the coding standards and guidelines for the ExxerCube OCR Pipeline C# implementation, ensuring consistency, maintainability, and quality across the codebase.

## Technology Stack

### .NET Framework
- **.NET 10**: Latest framework with performance improvements and new features
- **C# 12**: Latest language features and syntax improvements
- **Nullable Reference Types**: Enabled for better null safety

### Testing Framework
- **xUnit v3**: Latest testing framework with improved performance
- **Shouldly**: Fluent assertion library for readable tests
- **NSubstitute**: Modern mocking framework
- **Coverlet**: Code coverage reporting

### Quality Tools
- **Warnings as Errors**: All warnings treated as compilation errors
- **Code Analysis**: Built-in .NET analyzers enabled
- **StyleCop**: Code style enforcement
- **SonarQube**: Code quality analysis

## Railway Oriented Programming

### Result<T> Pattern

Use the `Result<T>` pattern for error handling instead of exceptions:

```csharp
/// <summary>
/// Represents a result that can be either a success or failure.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public class Result<T>
{
    /// <summary>
    /// Gets a value indicating whether the result is successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the success value if the result is successful.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message if the result is a failure.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful result.</returns>
    public static Result<T> Success(T value) => new(value, true, null);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failure result.</returns>
    public static Result<T> Failure(string error) => new(default, false, error);

    private Result(T? value, bool isSuccess, string? error)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
    }
}
```

### Fluent API Design

```csharp
/// <summary>
/// Processes a document using OCR and returns the extracted fields.
/// </summary>
/// <param name="document">The document to process.</param>
/// <returns>A result containing the extracted fields or an error.</returns>
public async Task<Result<ExtractedFields>> ProcessDocumentAsync(Document document)
{
    return await Result<Document>
        .Success(document)
        .Bind(ValidateDocument)
        .Bind(PreprocessImage)
        .Bind(ExecuteOcr)
        .Bind(ExtractFields);
}

/// <summary>
/// Validates the document for processing.
/// </summary>
/// <param name="document">The document to validate.</param>
/// <returns>A result indicating validation success or failure.</returns>
private static Result<Document> ValidateDocument(Document document)
{
    if (document == null)
        return Result<Document>.Failure("Document cannot be null");

    if (string.IsNullOrEmpty(document.FilePath))
        return Result<Document>.Failure("Document file path is required");

    if (!File.Exists(document.FilePath))
        return Result<Document>.Failure($"Document file not found: {document.FilePath}");

    return Result<Document>.Success(document);
}
```

## XML Documentation Requirements

### Mandatory XML Documentation

**ALL public classes, methods, and properties MUST have XML documentation:**

```csharp
/// <summary>
/// Represents a document image with metadata for OCR processing.
/// </summary>
public class ImageData
{
    /// <summary>
    /// Gets or sets the raw image data as a byte array.
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the source file path of the image.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Processes the image using OCR and returns the extracted text.
    /// </summary>
    /// <param name="config">The OCR processing configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ProcessAsync(ProcessingConfig config)
    {
        // Implementation
    }
}
```

### XML Documentation Elements

#### Required Elements

1. **`<summary>`** - Required for all public members
   - Describe what the member does
   - Be concise but descriptive
   - Use present tense

2. **`<param>`** - Required for method parameters
   - Describe the parameter's purpose
   - Include any constraints or requirements

3. **`<returns>`** - Required for non-void methods
   - Describe what the method returns
   - Include any special conditions

#### Optional Elements

4. **`<exception>`** - For methods that throw exceptions (avoid when possible)
   - Document all public exceptions
   - Include the exception type and condition

5. **`<remarks>`** - For additional information
   - Use for complex behavior or examples
   - Include usage notes or warnings

6. **`<example>`** - For code examples
   - Show typical usage patterns
   - Include complete, compilable examples

## Testing Standards

### Unit Test Structure

```csharp
/// <summary>
/// Tests for the OCR processing service.
/// </summary>
public class OcrProcessingServiceTests
{
    private readonly IOcrProcessingService _service;
    private readonly IImagePreprocessor _preprocessor;
    private readonly ILogger<OcrProcessingService> _logger;

    public OcrProcessingServiceTests()
    {
        _preprocessor = Substitute.For<IImagePreprocessor>();
        _logger = Substitute.For<ILogger<OcrProcessingService>>();
        _service = new OcrProcessingService(_preprocessor, _logger);
    }

    [Fact]
    public async Task ProcessDocument_ValidDocument_ReturnsSuccessResult()
    {
        // Arrange
        var document = CreateValidDocument();
        var expectedResult = CreateExpectedOcrResult();
        
        _preprocessor.PreprocessAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageData>.Success(CreatePreprocessedImage()));

        // Act
        var result = await _service.ProcessDocumentAsync(document);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Text.ShouldBe(expectedResult.Text);
        result.Value!.ConfidenceAvg.ShouldBe(expectedResult.ConfidenceAvg, 0.01f);
    }

    [Fact]
    public async Task ProcessDocument_InvalidDocument_ReturnsFailureResult()
    {
        // Arrange
        var document = CreateInvalidDocument();

        // Act
        var result = await _service.ProcessDocumentAsync(document);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
        result.Error.ShouldContain("validation failed");
    }

    [Theory]
    [InlineData(null, "Document cannot be null")]
    [InlineData("", "Document file path is required")]
    [InlineData("nonexistent.pdf", "Document file not found")]
    public async Task ProcessDocument_InvalidInputs_ReturnsAppropriateErrors(string? filePath, string expectedError)
    {
        // Arrange
        var document = new Document { FilePath = filePath };

        // Act
        var result = await _service.ProcessDocumentAsync(document);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain(expectedError);
    }

    private static Document CreateValidDocument() => new()
    {
        FilePath = "test-documents/valid-document.pdf",
        PageNumber = 1,
        TotalPages = 1
    };

    private static Document CreateInvalidDocument() => new()
    {
        FilePath = "test-documents/invalid-document.pdf",
        PageNumber = 0,
        TotalPages = 0
    };

    private static ImageData CreatePreprocessedImage() => new()
    {
        Data = new byte[] { 1, 2, 3, 4 },
        SourcePath = "test-documents/valid-document.pdf",
        PageNumber = 1,
        TotalPages = 1
    };

    private static OCRResult CreateExpectedOcrResult() => new()
    {
        Text = "Sample OCR text",
        ConfidenceAvg = 0.95f,
        ConfidenceMedian = 0.97f,
        Confidences = new List<float> { 0.95f, 0.97f, 0.93f },
        LanguageUsed = "spa"
    };
}
```

### Test Naming Conventions

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    // Act
    // Assert
}

[Theory]
[InlineData("input1", "expected1")]
[InlineData("input2", "expected2")]
public async Task MethodName_WithDifferentInputs_ReturnsExpectedResults(string input, string expected)
{
    // Test implementation
}
```

### Mocking Guidelines

```csharp
// Use NSubstitute for mocking
var mockService = Substitute.For<IOcrProcessingService>();
mockService.ProcessAsync(Arg.Any<Document>())
    .Returns(Result<OCRResult>.Success(expectedResult));

// Verify interactions
mockService.Received(1).ProcessAsync(Arg.Any<Document>());
mockService.DidNotReceive().SomeOtherMethod();
```

## Logging and Telemetry

### Structured Logging

```csharp
/// <summary>
/// Processes documents with comprehensive logging and telemetry.
/// </summary>
public class DocumentProcessor
{
    private readonly ILogger<DocumentProcessor> _logger;
    private readonly IMetrics _metrics;
    private readonly ITelemetry _telemetry;

    public DocumentProcessor(ILogger<DocumentProcessor> logger, IMetrics metrics, ITelemetry telemetry)
    {
        _logger = logger;
        _metrics = metrics;
        _telemetry = telemetry;
    }

    /// <summary>
    /// Processes a batch of documents with logging and metrics.
    /// </summary>
    /// <param name="documents">The documents to process.</param>
    /// <returns>A result containing the processing results.</returns>
    public async Task<Result<List<ProcessingResult>>> ProcessBatchAsync(IEnumerable<Document> documents)
    {
        using var activity = _telemetry.StartActivity("ProcessBatch");
        
        try
        {
            _logger.LogInformation("Starting batch processing of {DocumentCount} documents", documents.Count());
            _metrics.IncrementCounter("documents_processed_total", documents.Count());

            var results = new List<ProcessingResult>();
            var stopwatch = Stopwatch.StartNew();

            foreach (var document in documents)
            {
                using var documentActivity = _telemetry.StartActivity("ProcessDocument");
                documentActivity?.SetTag("document.path", document.FilePath);

                var result = await ProcessDocumentAsync(document);
                results.Add(result);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Successfully processed document {DocumentPath}", document.FilePath);
                    _metrics.IncrementCounter("documents_success_total");
                }
                else
                {
                    _logger.LogWarning("Failed to process document {DocumentPath}: {Error}", 
                        document.FilePath, result.Error);
                    _metrics.IncrementCounter("documents_failed_total");
                }
            }

            stopwatch.Stop();
            _metrics.RecordHistogram("batch_processing_duration_seconds", stopwatch.Elapsed.TotalSeconds);
            
            _logger.LogInformation("Completed batch processing in {Duration}ms", stopwatch.ElapsedMilliseconds);
            
            return Result<List<ProcessingResult>>.Success(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during batch processing");
            _metrics.IncrementCounter("batch_processing_errors_total");
            return Result<List<ProcessingResult>>.Failure($"Batch processing failed: {ex.Message}");
        }
    }
}
```

### Metrics and Telemetry

```csharp
/// <summary>
/// Defines metrics for the OCR processing system.
/// </summary>
public static class OcrMetrics
{
    /// <summary>
    /// Counter for total documents processed.
    /// </summary>
    public static readonly Counter ProcessedDocuments = Metrics.CreateCounter(
        "ocr_documents_processed_total", 
        "Total number of documents processed");

    /// <summary>
    /// Histogram for document processing time.
    /// </summary>
    public static readonly Histogram ProcessingTime = Metrics.CreateHistogram(
        "ocr_processing_time_seconds", 
        "Document processing time in seconds");

    /// <summary>
    /// Gauge for active processing operations.
    /// </summary>
    public static readonly Gauge ActiveProcessing = Metrics.CreateGauge(
        "ocr_active_processing", 
        "Number of currently active processing operations");

    /// <summary>
    /// Counter for processing errors.
    /// </summary>
    public static readonly Counter ProcessingErrors = Metrics.CreateCounter(
        "ocr_processing_errors_total", 
        "Total number of processing errors");
}
```

## Error Handling

### Railway Oriented Programming

```csharp
/// <summary>
/// Extension methods for Result<T> to enable fluent operations.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Binds a function to a result, continuing the railway if successful.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The function to bind.</param>
    /// <returns>A new result.</returns>
    public static async Task<Result<TResult>> Bind<T, TResult>(
        this Result<T> result,
        Func<T, Task<Result<TResult>>> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        return await func(result.Value!);
    }

    /// <summary>
    /// Maps a function over a successful result.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="result">The input result.</param>
    /// <param name="func">The function to map.</param>
    /// <returns>A new result.</returns>
    public static Result<TResult> Map<T, TResult>(
        this Result<T> result,
        Func<T, TResult> func)
    {
        if (!result.IsSuccess)
            return Result<TResult>.Failure(result.Error!);

        return Result<TResult>.Success(func(result.Value!));
    }
}
```

## Performance Guidelines

### Async/Await Best Practices

```csharp
/// <summary>
/// Processes multiple documents concurrently with proper async patterns.
/// </summary>
/// <param name="documents">The documents to process.</param>
/// <param name="maxConcurrency">Maximum concurrent operations.</param>
/// <returns>A result containing the processing results.</returns>
public async Task<Result<List<ProcessingResult>>> ProcessDocumentsConcurrentlyAsync(
    IEnumerable<Document> documents,
    int maxConcurrency = 5)
{
    var semaphore = new SemaphoreSlim(maxConcurrency);
    var tasks = documents.Select(async document =>
    {
        await semaphore.WaitAsync();
        try
        {
            return await ProcessDocumentAsync(document);
        }
        finally
        {
            semaphore.Release();
        }
    });

    var results = await Task.WhenAll(tasks);
    return Result<List<ProcessingResult>>.Success(results.ToList());
}
```

## Code Review Checklist

### Documentation
- [ ] All public classes have XML documentation
- [ ] All public methods have XML documentation
- [ ] All public properties have XML documentation
- [ ] Parameters are documented with `<param>` tags
- [ ] Return values are documented with `<returns>` tags
- [ ] Examples provided for complex APIs

### Code Quality
- [ ] Follows naming conventions
- [ ] Uses Railway Oriented Programming (Result<T>)
- [ ] No exception throwing for business logic
- [ ] Proper async/await usage
- [ ] Comprehensive logging and telemetry
- [ ] Warnings as errors enabled

### Testing
- [ ] Unit tests cover all public methods
- [ ] Tests use xUnit v3, Shouldly, and NSubstitute
- [ ] Test names are descriptive
- [ ] Tests follow Arrange-Act-Assert pattern
- [ ] 80%+ code coverage achieved
- [ ] Integration tests for external dependencies

### Performance
- [ ] Async operations properly implemented
- [ ] Memory usage optimized
- [ ] Metrics and telemetry included
- [ ] No blocking operations in async methods

## Build Configuration

### Project File Settings

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DocumentationFile>$(OutputPath)$(AssemblyName).xml</DocumentationFile>
    <CodeAnalysisRuleSet>$(MSBuildThisFileDirectory)CodeAnalysis.ruleset</CodeAnalysisRuleSet>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Telemetry" Version="10.0.0" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.8.0" />
    <PackageReference Include="OpenTelemetry.Metrics" Version="1.8.0" />
    <PackageReference Include="OpenTelemetry.Trace" Version="1.8.0" />
  </ItemGroup>
</Project>
```

### Test Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.7.0" />
    <PackageReference Include="Shouldly" Version="4.2.1" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.1" />
  </ItemGroup>
</Project>
```

## Continuous Integration

### Quality Gates
- **Build**: Must compile with warnings as errors
- **Tests**: All tests must pass with 80%+ coverage
- **Code Analysis**: No critical or major issues
- **Documentation**: All public APIs documented
- **Performance**: Meets performance benchmarks
