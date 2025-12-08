using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileSystem;

/// <summary>
/// Contains unit tests for <see cref="FileSystemOutputWriter"/>.
/// </summary>
public class FileSystemOutputWriterTests : IDisposable
{
    private readonly ILogger<FileSystemOutputWriter> _logger;
    private readonly FileSystemOutputWriter _writer;
    private readonly string _testDirectory;
    private readonly ProcessingResult _testResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemOutputWriterTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public FileSystemOutputWriterTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<FileSystemOutputWriter>(output);
        _writer = new FileSystemOutputWriter(_logger);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FileSystemOutputWriterTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        
        _testResult = CreateTestProcessingResult();
    }

    /// <summary>
    /// Tests that WriteResultAsync writes a JSON file successfully.
    /// </summary>
    [Fact]
    public async Task WriteResultAsync_JsonFormat_WritesFileSuccessfully()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.json");

        // Act
        var result = await _writer.WriteResultAsync(_testResult, outputPath, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that WriteResultAsync writes a text file successfully.
    /// </summary>
    [Fact]
    public async Task WriteResultAsync_TextFormat_WritesFileSuccessfully()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.txt");

        // Act
        var result = await _writer.WriteResultAsync(_testResult, outputPath, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
        
        var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        content.ShouldNotBeNullOrEmpty();
        content.ShouldContain("Processing Result");
    }

    /// <summary>
    /// Tests that WriteResultAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task WriteResultAsync_WithCancellationRequested_ReturnsCancelledResult()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.json");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _writer.WriteResultAsync(_testResult, outputPath, cts.Token);

        // Assert
        // Service MUST respect cancellation token and propagate cancellation signal
        result.IsCancelled().ShouldBeTrue();
        File.Exists(outputPath).ShouldBeFalse();
    }

    /// <summary>
    /// Tests that WriteResultAsync returns a failure result for an unsupported format.
    /// </summary>
    [Fact]
    public async Task WriteResultAsync_UnsupportedFormat_ReturnsFailureResult()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.xyz");

        // Act
        var result = await _writer.WriteResultAsync(_testResult, outputPath, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
        result.Error.ShouldContain("Unsupported output format");
    }

    /// <summary>
    /// Tests that WriteResultsAsync writes multiple results successfully.
    /// </summary>
    [Fact]
    public async Task WriteResultsAsync_MultipleResults_WritesFilesSuccessfully()
    {
        // Arrange
        var results = new List<ProcessingResult>
        {
            CreateTestProcessingResult("file1.pdf"),
            CreateTestProcessingResult("file2.pdf"),
            CreateTestProcessingResult("file3.pdf")
        };

        // Act
        var result = await _writer.WriteResultsAsync(results, _testDirectory, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        
        // Verify JSON files were created
        File.Exists(Path.Combine(_testDirectory, "file1.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_testDirectory, "file2.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_testDirectory, "file3.json")).ShouldBeTrue();
        
        // Verify text files were created
        File.Exists(Path.Combine(_testDirectory, "file1.txt")).ShouldBeTrue();
        File.Exists(Path.Combine(_testDirectory, "file2.txt")).ShouldBeTrue();
        File.Exists(Path.Combine(_testDirectory, "file3.txt")).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that WriteResultsAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task WriteResultsAsync_WithCancellationRequested_ReturnsCancelledResult()
    {
        // Arrange
        var results = new List<ProcessingResult>
        {
            CreateTestProcessingResult("file1.pdf"),
            CreateTestProcessingResult("file2.pdf")
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _writer.WriteResultsAsync(results, _testDirectory, cts.Token);

        // Assert
        // Service MUST respect cancellation token and propagate cancellation signal
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that WriteJsonAsync writes a JSON file successfully.
    /// </summary>
    [Fact]
    public async Task WriteJsonAsync_ValidResult_WritesJsonFile()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.json");

        // Act
        var result = await _writer.WriteJsonAsync(_testResult, outputPath, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
        
        var jsonContent = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        jsonContent.ShouldNotBeNullOrEmpty();
        jsonContent.ShouldContain("SourcePath");
    }

    /// <summary>
    /// Tests that WriteJsonAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task WriteJsonAsync_WithCancellationRequested_ReturnsCancelledResult()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.json");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _writer.WriteJsonAsync(_testResult, outputPath, cts.Token);

        // Assert
        // Service MUST respect cancellation token and propagate cancellation signal
        result.IsCancelled().ShouldBeTrue();
        File.Exists(outputPath).ShouldBeFalse();
    }

    /// <summary>
    /// Tests that WriteTextAsync writes a text file successfully.
    /// </summary>
    [Fact]
    public async Task WriteTextAsync_ValidResult_WritesTextFile()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.txt");

        // Act
        var result = await _writer.WriteTextAsync(_testResult, outputPath, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        File.Exists(outputPath).ShouldBeTrue();
        
        var textContent = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        textContent.ShouldNotBeNullOrEmpty();
        textContent.ShouldContain("Processing Result");
        textContent.ShouldContain("OCR Result");
    }

    /// <summary>
    /// Tests that WriteTextAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task WriteTextAsync_WithCancellationRequested_ReturnsCancelledResult()
    {
        // Arrange
        var outputPath = Path.Combine(_testDirectory, "result.txt");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _writer.WriteTextAsync(_testResult, outputPath, cts.Token);

        // Assert
        // Service MUST respect cancellation token and propagate cancellation signal
        result.IsCancelled().ShouldBeTrue();
        File.Exists(outputPath).ShouldBeFalse();
    }

    /// <summary>
    /// Tests that WriteTextAsync includes all extracted fields in the output.
    /// </summary>
    [Fact]
    public async Task WriteTextAsync_IncludesAllFields_WritesCompleteText()
    {
        // Arrange
        var resultWithFields = CreateTestProcessingResult("test.pdf");
        resultWithFields.ExtractedFields.Expediente = "EXP-2024-001";
        resultWithFields.ExtractedFields.Causa = "Test Cause";
        resultWithFields.ExtractedFields.AccionSolicitada = "Test Action";
        resultWithFields.ExtractedFields.Fechas = new List<string> { "2024-01-15", "2024-02-20" };
        resultWithFields.ExtractedFields.Montos = new List<AmountData>
        {
            new AmountData("MXN", 1500.00m, "MXN 1,500.00")
        };
        
        var outputPath = Path.Combine(_testDirectory, "result.txt");

        // Act
        var result = await _writer.WriteTextAsync(resultWithFields, outputPath, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var textContent = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        textContent.ShouldContain("EXP-2024-001");
        textContent.ShouldContain("Test Cause");
        textContent.ShouldContain("Test Action");
        textContent.ShouldContain("2024-01-15");
        textContent.ShouldContain("MXN 1,500.00");
    }

    /// <summary>
    /// Creates a test processing result.
    /// </summary>
    /// <param name="sourcePath">The source path for the result.</param>
    /// <returns>A test processing result.</returns>
    private static ProcessingResult CreateTestProcessingResult(string sourcePath = "test.pdf")
    {
        return new ProcessingResult
        {
            SourcePath = sourcePath,
            PageNumber = 1,
            OCRResult = new OCRResult
            {
                Text = "Sample OCR text",
                ConfidenceAvg = 95.5f,
                ConfidenceMedian = 97.0f,
                Confidences = new List<float> { 95.0f, 97.0f, 94.5f },
                LanguageUsed = "spa"
            },
            ExtractedFields = new ExtractedFields
            {
                Expediente = null,
                Causa = null,
                AccionSolicitada = null,
                Fechas = new List<string>(),
                Montos = new List<AmountData>()
            },
            ProcessingErrors = new List<string>()
        };
    }

    /// <summary>
    /// Disposes of the test resources.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

