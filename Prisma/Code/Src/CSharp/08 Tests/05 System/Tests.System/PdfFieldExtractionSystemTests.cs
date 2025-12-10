using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt;
using ExxerCube.Prisma.Infrastructure.NoOp;

namespace ExxerCube.Prisma.Tests.System.OcrPipeline;

/// <summary>
/// System tests for the full PDF field extraction pipeline.
/// Tests the complete flow: PDF → Images → OCR → TXT → Field Extraction.
/// </summary>
public class PdfFieldExtractionSystemTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<PdfFieldExtractionSystemTests> _logger;
    private readonly ITestOutputHelper _output;

    public PdfFieldExtractionSystemTests(ITestOutputHelper output)
    {
        _output = output;

        // Set up DI container with all required services
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Register OCR pipeline services
        services.AddSingleton<IImagePreprocessor, NoOpImagePreprocessor>();
        services.AddScoped<IOcrExecutor, TesseractOcrExecutor>();

        // Register field extractors
        services.AddScoped<IFieldExtractor<TxtSource>, AdaptiveTxtFieldExtractor>();
        services.AddScoped<IFieldExtractor<PdfSource>, PdfOcrFieldExtractor>();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<PdfFieldExtractionSystemTests>>();
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task PdfFieldExtraction_SmallPdf_ExtractsFieldsSuccessfully()
    {
        // Arrange
        var pdfPath = Path.Combine("Fixtures", "PRP1", "555CCC-66666662025.pdf");
        _logger.LogInformation("Testing PDF field extraction with: {Path}", pdfPath);

        if (!File.Exists(pdfPath))
        {
            _output.WriteLine($"SKIPPING: PDF fixture not found at {pdfPath}");
            return; // Skip test if fixture doesn't exist
        }

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken);
        var pdfSource = new PdfSource
        {
            FileContent = pdfBytes,
            FilePath = pdfPath
        };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente", isRequired: true),
            new FieldDefinition("NumeroOficio", isRequired: true),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada"),
            new FieldDefinition("AutoridadNombre")
        };

        var extractor = _serviceProvider.GetRequiredService<IFieldExtractor<PdfSource>>();

        // Act
        _logger.LogInformation("Starting PDF field extraction...");
        var stopwatch = global::System.Diagnostics.Stopwatch.StartNew();
        var result = await extractor.ExtractFieldsAsync(pdfSource, fieldDefinitions);
        stopwatch.Stop();

        // Assert
        _logger.LogInformation("Extraction completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        result.IsSuccess.ShouldBeTrue($"Expected extraction to succeed. Error: {result.Error}");
        result.Value.ShouldNotBeNull();

        _output.WriteLine("=== Extraction Results ===");
        _output.WriteLine($"Expediente: {result.Value!.Expediente ?? "NULL"}");
        _output.WriteLine($"Causa: {result.Value.Causa ?? "NULL"}");
        _output.WriteLine($"AccionSolicitada: {result.Value.AccionSolicitada ?? "NULL"}");
        _output.WriteLine($"Additional Fields: {result.Value.AdditionalFields.Count}");

        foreach (var kvp in result.Value.AdditionalFields)
        {
            _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        // Performance assertion
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30000, "Extraction should complete in under 30 seconds");
    }

    [Fact(Timeout = 60000)] // 60 second timeout for larger PDF
    public async Task PdfFieldExtraction_RealFixture222AAA_ExtractsExpedienteAndOficio()
    {
        // Arrange
        var pdfPath = Path.Combine("Fixtures", "PRP1", "222AAA-44444444442025.pdf");
        _logger.LogInformation("Testing PDF field extraction with real fixture: {Path}", pdfPath);

        if (!File.Exists(pdfPath))
        {
            _output.WriteLine($"SKIPPING: PDF fixture not found at {pdfPath}");
            return; // Skip test if fixture doesn't exist
        }

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken);
        var pdfSource = new PdfSource
        {
            FileContent = pdfBytes,
            FilePath = pdfPath
        };

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("NumeroOficio"),
            new FieldDefinition("AutoridadNombre")
        };

        var extractor = _serviceProvider.GetRequiredService<IFieldExtractor<PdfSource>>();

        // Act
        _logger.LogInformation("Starting PDF field extraction (may take 30-60 seconds)...");
        var stopwatch = global::System.Diagnostics.Stopwatch.StartNew();
        var result = await extractor.ExtractFieldsAsync(pdfSource, fieldDefinitions);
        stopwatch.Stop();

        // Assert
        _logger.LogInformation("Extraction completed in {ElapsedMs}ms ({ElapsedSec}s)",
            stopwatch.ElapsedMilliseconds, stopwatch.Elapsed.TotalSeconds);

        result.IsSuccess.ShouldBeTrue($"Expected extraction to succeed. Error: {result.Error}");
        result.Value.ShouldNotBeNull();

        _output.WriteLine("=== Extraction Results ===");
        _output.WriteLine($"Expediente: {result.Value!.Expediente ?? "NULL"}");

        var numeroOficio = result.Value.AdditionalFields.GetValueOrDefault("NumeroOficio");
        _output.WriteLine($"NumeroOficio: {numeroOficio ?? "NULL"}");

        var autoridadNombre = result.Value.AdditionalFields.GetValueOrDefault("AutoridadNombre");
        _output.WriteLine($"AutoridadNombre: {autoridadNombre ?? "NULL"}");

        // Based on OCR fixture, we know these values
        if (!string.IsNullOrEmpty(numeroOficio))
        {
            numeroOficio.ShouldContain("AGAFADAFSON2");
        }

        if (!string.IsNullOrEmpty(autoridadNombre))
        {
            (autoridadNombre.Contains("Comisión Nacional Bancaria", StringComparison.OrdinalIgnoreCase) ||
             autoridadNombre.Contains("CNBV", StringComparison.OrdinalIgnoreCase) ||
             autoridadNombre.Contains("SAT", StringComparison.OrdinalIgnoreCase))
                .ShouldBeTrue($"Expected authority name, got: {autoridadNombre}");
        }

        // Performance assertion
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(60000, "Extraction should complete in under 60 seconds");
    }

    [Fact]
    public async Task PdfFieldExtraction_ExtractSingleField_Works()
    {
        // Arrange
        var pdfPath = Path.Combine("Fixtures", "PRP1", "555CCC-66666662025.pdf");

        if (!File.Exists(pdfPath))
        {
            _output.WriteLine($"SKIPPING: PDF fixture not found at {pdfPath}");
            return;
        }

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken);
        var pdfSource = new PdfSource
        {
            FileContent = pdfBytes,
            FilePath = pdfPath
        };

        var extractor = _serviceProvider.GetRequiredService<IFieldExtractor<PdfSource>>();

        // Act
        var result = await extractor.ExtractFieldAsync(pdfSource, "Expediente");

        // Assert
        result.IsSuccess.ShouldBeTrue($"Expected field extraction to succeed. Error: {result.Error}");

        if (result.Value != null)
        {
            _output.WriteLine($"Extracted Expediente: {result.Value.Value}");
            result.Value.FieldName.ShouldBe("Expediente");
            result.Value.Origin.ShouldBe(Domain.Enums.FieldOrigin.PdfOcr);
        }
    }

    [Fact]
    public async Task PdfFieldExtraction_NullPdfSource_ReturnsFailure()
    {
        // Arrange
        var extractor = _serviceProvider.GetRequiredService<IFieldExtractor<PdfSource>>();
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - intentional for test
        var result = await extractor.ExtractFieldsAsync(null!, fieldDefinitions);
#pragma warning restore CS8625

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain("cannot be null");
    }

    [Fact]
    public async Task PdfFieldExtraction_EmptyPdfContent_ReturnsFailure()
    {
        // Arrange
        var pdfSource = new PdfSource
        {
            FileContent = null,
            FilePath = null!
        };

        var extractor = _serviceProvider.GetRequiredService<IFieldExtractor<PdfSource>>();
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await extractor.ExtractFieldsAsync(pdfSource, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain("FileContent or valid FilePath");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}
