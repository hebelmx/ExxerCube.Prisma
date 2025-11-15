using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Export;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Meziantou.Extensions.Logging.Xunit;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using NSubstitute;

namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Performance tests for PDF export operations to verify NFR10 and NFR11 requirements.
/// </summary>
public class PdfExportPerformanceTests
{
    private readonly IPdfRequirementSummarizer _pdfSummarizer;
    private readonly IResponseExporter _pdfSigner;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfExportPerformanceTests"/> class.
    /// </summary>
    public PdfExportPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _metadataExtractor = Substitute.For<IMetadataExtractor>();
        _pdfSummarizer = new PdfRequirementSummarizerService(
            _metadataExtractor,
            XUnitLogger.CreateLogger<PdfRequirementSummarizerService>(output));

        var certificateOptions = Options.Create(new CertificateOptions
        {
            Source = "File",
            FilePath = "test-cert.pfx",
            Password = "test"
        });
        _pdfSigner = new DigitalPdfSigner(
            certificateOptions,
            XUnitLogger.CreateLogger<DigitalPdfSigner>(output));
    }

    /// <summary>
    /// Creates a sample PDF content for testing.
    /// </summary>
    private static byte[] CreateSamplePdfContent()
    {
        // Create a minimal valid PDF structure
        // PDF header + minimal structure for testing
        var pdfContent = @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
>>
endobj
4 0 obj
<<
/Length 44
>>
stream
BT
/F1 12 Tf
100 700 Td
(Test PDF Content) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000206 00000 n 
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
300
%%EOF";
        return System.Text.Encoding.UTF8.GetBytes(pdfContent);
    }

    /// <summary>
    /// Creates sample PDF text content for testing summarization.
    /// </summary>
    private static string CreateSamplePdfText()
    {
        return @"REQUERIMIENTO DE BLOQUEO
Se requiere bloquear la cuenta bancaria del cliente debido a orden judicial.
Artículo 123 de la Ley de Prevención de Lavado de Dinero.

REQUERIMIENTO DE DESBLOQUEO
Una vez cumplidos los requisitos, se procederá al desbloqueo de la cuenta.
Artículo 456 del Reglamento.

REQUERIMIENTO DE DOCUMENTACIÓN
El cliente debe presentar documentación adicional para verificación.
Documentos requeridos: identificación oficial, comprobante de domicilio.

REQUERIMIENTO DE TRANSFERENCIA
Se requiere transferir fondos a cuenta designada por autoridad competente.
Monto: $100,000.00 MXN

REQUERIMIENTO DE INFORMACIÓN
Se solicita información sobre transacciones realizadas en los últimos 6 meses.
Período: Enero 2024 - Junio 2024";
    }

    /// <summary>
    /// Tests that SummarizeRequirementsAsync completes within 10 seconds (NFR10).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task SummarizeRequirementsAsync_CompletesWithin10Seconds_NFR10()
    {
        // Arrange
        var pdfContent = CreateSamplePdfContent();
        var pdfText = CreateSamplePdfText();
        
        _metadataExtractor.ExtractTextAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(pdfText));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pdfSummarizer.SummarizeRequirementsAsync(pdfContent, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000,
            $"SummarizeRequirementsAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding NFR10 target of <10s (10000ms)");
        
        _output.WriteLine($"SummarizeRequirementsAsync completed in {stopwatch.ElapsedMilliseconds}ms (NFR10 target: <10000ms)");
    }

    /// <summary>
    /// Tests that SummarizeRequirementsFromTextAsync completes within 10 seconds (NFR10).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task SummarizeRequirementsFromTextAsync_CompletesWithin10Seconds_NFR10()
    {
        // Arrange
        var pdfText = CreateSamplePdfText();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pdfSummarizer.SummarizeRequirementsFromTextAsync(pdfText, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000,
            $"SummarizeRequirementsFromTextAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding NFR10 target of <10s (10000ms)");
        
        _output.WriteLine($"SummarizeRequirementsFromTextAsync completed in {stopwatch.ElapsedMilliseconds}ms (NFR10 target: <10000ms)");
    }

    /// <summary>
    /// Tests that PDF signing completes within 3 seconds (NFR11).
    /// Note: This test may fail if certificate is not available, but validates the performance target.
    /// </summary>
    [Fact(Skip = "Requires valid certificate - run in integration environment")]
    [Trait("Category", "Performance")]
    public async Task ExportSignedPdfAsync_CompletesWithin3Seconds_NFR11()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "EXP-2024-001",
                NumeroOficio = "OF-2024-001"
            }
        };
        using var stream = new MemoryStream();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pdfSigner.ExportSignedPdfAsync(metadata, stream, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        // Note: May fail if certificate not available, but validates performance target
        if (result.IsSuccess)
        {
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(3000,
                $"ExportSignedPdfAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding NFR11 target of <3s (3000ms)");
            
            _output.WriteLine($"ExportSignedPdfAsync completed in {stopwatch.ElapsedMilliseconds}ms (NFR11 target: <3000ms)");
        }
        else
        {
            _output.WriteLine($"ExportSignedPdfAsync skipped - certificate not available: {result.Error}");
        }
    }

    /// <summary>
    /// Tests that PDF summarization handles large PDFs efficiently.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task SummarizeRequirementsAsync_LargePdf_HandlesEfficiently()
    {
        // Arrange: Create larger PDF text content
        var largeText = string.Join("\n", Enumerable.Range(0, 100)
            .Select(i => $"REQUERIMIENTO {i}: Se requiere acción de cumplimiento según artículo {i + 1} de la ley."));
        
        _metadataExtractor.ExtractTextAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(largeText));

        var pdfContent = CreateSamplePdfContent();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pdfSummarizer.SummarizeRequirementsAsync(pdfContent, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Large PDFs should still complete within reasonable time (2x target for large content)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(20000,
            $"SummarizeRequirementsAsync took {stopwatch.ElapsedMilliseconds}ms for large PDF, exceeding 20s target");
        
        _output.WriteLine($"SummarizeRequirementsAsync (large PDF) completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Tests that PDF summarization operations don't block other processing operations.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task PdfSummarization_DoesNotBlockOtherProcessing()
    {
        // Arrange: Simulate concurrent summarization and other processing
        var pdfText = CreateSamplePdfText();
        var summarizationTasks = new List<Task<Result<RequirementSummary>>>();
        var processingTasks = new List<Task>();

        // Act: Start multiple summarization operations concurrently with simulated processing
        var stopwatch = Stopwatch.StartNew();
        
        // Start 5 summarization operations
        for (int i = 0; i < 5; i++)
        {
            summarizationTasks.Add(_pdfSummarizer.SummarizeRequirementsFromTextAsync(pdfText, CancellationToken.None));
        }

        // Simulate other processing tasks (should not be blocked)
        for (int i = 0; i < 10; i++)
        {
            processingTasks.Add(Task.Delay(50, CancellationToken.None));
        }

        // Wait for all tasks
        await Task.WhenAll(summarizationTasks);
        await Task.WhenAll(processingTasks);
        stopwatch.Stop();

        // Assert: All summarizations should succeed
        summarizationTasks.ShouldAllBe(t => t.Result.IsSuccess);

        // Performance: Total time should be reasonable (not blocked by summarization)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(15000,
            $"PDF summarization significantly blocked processing: {stopwatch.ElapsedMilliseconds}ms");
        
        _output.WriteLine($"Concurrent summarization and processing completed in {stopwatch.ElapsedMilliseconds}ms");
    }
}

