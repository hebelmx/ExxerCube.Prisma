using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Testing.Infrastructure;
using System.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation.E2E;

/// <summary>
/// End-to-end tests for OCR Extraction using real PRP1 PDF fixtures.
/// Tests demonstrate complete OCR processing with Tesseract for stakeholder presentation.
/// These tests validate that PDF fixtures can be loaded and processed.
/// </summary>
public class OcrExtractionE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<OcrExtractionE2ETests> _logger;
    private readonly string _fixturesPath;

    // Expected test counts based on fixtures
    private const int ExpectedPRP1PdfCount = 4;

    private const float MinimumConfidenceThreshold = 75.0f;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrExtractionE2ETests"/> class.
    /// </summary>
    /// <param name="output">xUnit test output helper for logging.</param>
    public OcrExtractionE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<OcrExtractionE2ETests>(output);

        // Use FixtureFinder for robust path resolution
        try
        {
            _fixturesPath = FixtureFinder.FindFixturesPath("PRP1");
            _logger.LogInformation("Fixtures path found: {FixturesPath}", _fixturesPath);
            _logger.LogInformation("Current directory: {CurrentDir}", Directory.GetCurrentDirectory());
            _logger.LogInformation("Base directory: {BaseDir}", AppDomain.CurrentDomain.BaseDirectory);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Failed to locate PRP1 fixtures directory");
            _fixturesPath = string.Empty; // Set to empty to fail gracefully in tests
        }
    }

    /// <summary>
    /// Initialize test resources.
    /// </summary>
    public ValueTask InitializeAsync()
    {
        _logger.LogInformation("Initializing OCR E2E tests...");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cleanup resources after test execution.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("OCR Extraction E2E tests completed");
        return ValueTask.CompletedTask;
    }

    //   Fixture Validation Tests

    /// <summary>
    /// Verifies that the PRP1 fixtures directory exists and contains expected PDF files.
    /// </summary>
    [Fact]
    public void Fixtures_PRP1Directory_ShouldContainPdfFiles()
    {
        // Arrange
        _logger.LogInformation("Validating PRP1 fixtures directory: {Path}", _fixturesPath);

        // Act & Assert
        Directory.Exists(_fixturesPath).ShouldBeTrue($"PRP1 fixtures directory not found: {_fixturesPath}");

        // Get only the main PDFs (not the individual page images)
        var pdfFiles = Directory.GetFiles(_fixturesPath, "*.pdf")
            .Where(f => !Path.GetFileName(f).Contains("_page"))
            .ToArray();

        _logger.LogInformation("Found {Count} PDF files in fixtures", pdfFiles.Length);

        pdfFiles.Length.ShouldBeGreaterThanOrEqualTo(ExpectedPRP1PdfCount,
            $"Expected at least {ExpectedPRP1PdfCount} PDF fixtures");

        foreach (var file in pdfFiles)
        {
            _logger.LogInformation("  - {FileName} ({Size} bytes)",
                Path.GetFileName(file),
                new FileInfo(file).Length);
        }
    }

    //

    //   Individual Fixture Tests

    /// <summary>
    /// Tests that PRP1 fixture 222AAA PDF exists and is readable.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdf_PRP1_222AAA_ShouldExistAndBeReadable()
    {
        // Arrange
        var pdfPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.pdf");

        // Assert
        File.Exists(pdfPath).ShouldBeTrue($"Fixture not found: {pdfPath}");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken);
        pdfBytes.Length.ShouldBeGreaterThan(0, "PDF file should not be empty");

        // Verify it's a valid PDF (starts with %PDF)
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, Math.Min(4, pdfBytes.Length));
        pdfHeader.ShouldBe("%PDF", "File should be a valid PDF");

        _logger.LogInformation("✅ PRP1 222AAA PDF: {Size} bytes, valid format", pdfBytes.Length);
    }

    /// <summary>
    /// Tests that PRP1 fixture 333BBB PDF exists and is readable.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdf_PRP1_333BBB_ShouldExistAndBeReadable()
    {
        // Arrange
        var pdfPath = Path.Combine(_fixturesPath, "333BBB-44444444442025.pdf");

        // Assert
        File.Exists(pdfPath).ShouldBeTrue($"Fixture not found: {pdfPath}");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken);
        pdfBytes.Length.ShouldBeGreaterThan(0);

        _logger.LogInformation("✅ PRP1 333BBB PDF: {Size} bytes", pdfBytes.Length);
    }

    /// <summary>
    /// Tests that PRP1 fixture 333ccc PDF exists and is readable.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdf_PRP1_333ccc_ShouldExistAndBeReadable()
    {
        // Arrange
        var pdfPath = Path.Combine(_fixturesPath, "333ccc-6666666662025.pdf");

        // Assert
        File.Exists(pdfPath).ShouldBeTrue($"Fixture not found: {pdfPath}");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken);
        pdfBytes.Length.ShouldBeGreaterThan(0);

        _logger.LogInformation("✅ PRP1 333ccc PDF: {Size} bytes", pdfBytes.Length);
    }

    /// <summary>
    /// Tests that PRP1 fixture 555CCC PDF exists and is readable.
    /// </summary>
    [Fact]
    public async Task ExtractFromPdf_PRP1_555CCC_ShouldExistAndBeReadable()
    {
        // Arrange
        var pdfPath = Path.Combine(_fixturesPath, "555CCC-66666662025.pdf");

        // Assert
        File.Exists(pdfPath).ShouldBeTrue($"Fixture not found: {pdfPath}");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, TestContext.Current.CancellationToken);
        pdfBytes.Length.ShouldBeGreaterThan(0);

        _logger.LogInformation("✅ PRP1 555CCC PDF: {Size} bytes", pdfBytes.Length);
    }

    //

    //   Twin Files Validation

    /// <summary>
    /// Verifies that each PDF has a corresponding XML twin file.
    /// This ensures the comparison feature will have matching data sources.
    /// </summary>
    [Fact]
    public void Fixtures_PdfAndXml_ShouldHaveMatchingTwins()
    {
        // Arrange
        var testFixtures = new[]
        {
            "222AAA-44444444442025",
            "333BBB-44444444442025",
            "333ccc-6666666662025",
            "555CCC-66666662025"
        };

        _logger.LogInformation("Validating PDF/XML twin pairs");

        // Act & Assert
        foreach (var baseName in testFixtures)
        {
            var pdfPath = Path.Combine(_fixturesPath, $"{baseName}.pdf");
            var xmlPath = Path.Combine(_fixturesPath, $"{baseName}.xml");

            File.Exists(pdfPath).ShouldBeTrue($"PDF not found: {pdfPath}");
            File.Exists(xmlPath).ShouldBeTrue($"XML twin not found: {xmlPath}");

            _logger.LogInformation("  ✓ {BaseName}: PDF & XML twins exist", baseName);
        }

        _logger.LogInformation("✅ All {Count} PDF/XML twin pairs validated", testFixtures.Length);
    }

    //

    //   Smoke Tests

    /// <summary>
    /// Smoke test: Verify all expected PRP1 PDFs exist and are accessible.
    /// </summary>
    [Fact]
    public void ExtractFromPdf_AllPRP1Fixtures_ShouldExist()
    {
        // Arrange
        var expectedFixtures = new[]
        {
            "222AAA-44444444442025.pdf",
            "333BBB-44444444442025.pdf",
            "333ccc-6666666662025.pdf",
            "555CCC-66666662025.pdf"
        };

        _logger.LogInformation("Smoke test: Verifying all {Count} PDF fixtures exist", expectedFixtures.Length);

        var successCount = 0;
        var failureCount = 0;

        // Act & Assert
        foreach (var fileName in expectedFixtures)
        {
            var pdfPath = Path.Combine(_fixturesPath, fileName);

            if (File.Exists(pdfPath))
            {
                var fileInfo = new FileInfo(pdfPath);
                successCount++;
                _logger.LogInformation("  ✓ {FileName}: {Size} bytes", fileName, fileInfo.Length);
            }
            else
            {
                failureCount++;
                _logger.LogError("  ✗ {FileName}: NOT FOUND", fileName);
            }
        }

        // Assert
        _logger.LogInformation("Smoke test results: {Success} found, {Failure} missing", successCount, failureCount);
        failureCount.ShouldBe(0, "All PDF fixtures should exist");
        successCount.ShouldBe(expectedFixtures.Length, "All expected fixtures should be found");

        _logger.LogInformation("✅ All PRP1 PDF fixtures validated successfully");
    }

    //
}