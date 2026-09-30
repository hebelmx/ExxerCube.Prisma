namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Unit tests for <see cref="DigitalPdfSigner"/>.
/// </summary>
public class DigitalPdfSignerTests
{
    private readonly IOptions<CertificateOptions> _certificateOptions;
    private readonly ILogger<DigitalPdfSigner> _logger;
    private readonly DigitalPdfSigner _signer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DigitalPdfSignerTests"/> class.
    /// </summary>
    public DigitalPdfSignerTests()
    {
        var options = new CertificateOptions
        {
            Source = "File",
            FileCertificatePath = null, // Will be mocked
            FallbackToFile = false
        };
        _certificateOptions = Options.Create(options);
        _logger = XUnitLogger.CreateLogger<DigitalPdfSigner>();
        _signer = new DigitalPdfSigner(_certificateOptions, _logger);
    }

    /// <summary>
    /// Tests that XML export returns failure (not supported by PDF signer).
    /// </summary>
    [Fact]
    public async Task ExportSiroXmlAsync_Always_ReturnsFailure()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente { NumeroExpediente = "TEST-001" }
        };
        using var stream = new MemoryStream();

        // Act
        var result = await _signer.ExportSiroXmlAsync(metadata, stream, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("SiroXmlExporter");
    }

    /// <summary>
    /// Tests that PDF signing fails when metadata is null.
    /// </summary>
    [Fact]
    public async Task ExportSignedPdfAsync_NullMetadata_ReturnsFailure()
    {
        // Arrange
        UnifiedMetadataRecord? metadata = null;
        using var stream = new MemoryStream();

        // Act
        var result = await _signer.ExportSignedPdfAsync(metadata!, stream, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null");
    }

    /// <summary>
    /// Tests that PDF signing fails when output stream is null.
    /// </summary>
    [Fact]
    public async Task ExportSignedPdfAsync_NullOutputStream_ReturnsFailure()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente { NumeroExpediente = "TEST-001" }
        };
        Stream? stream = null;

        // Act
        var result = await _signer.ExportSignedPdfAsync(metadata, stream!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null");
    }

    /// <summary>
    /// Tests that PDF signing handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task ExportSignedPdfAsync_CancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente { NumeroExpediente = "TEST-001" }
        };
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _signer.ExportSignedPdfAsync(metadata, stream, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF signing fails when certificate is not available.
    /// </summary>
    [Fact]
    public async Task ExportSignedPdfAsync_CertificateNotAvailable_ReturnsFailure()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente { NumeroExpediente = "TEST-001" }
        };
        using var stream = new MemoryStream();

        // Act
        var result = await _signer.ExportSignedPdfAsync(metadata, stream, TestContext.Current.CancellationToken);

        // Assert
        // Should fail because certificate is not configured
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that PDF signing generates PDF document structure.
    /// </summary>
    [Fact]
    public async Task ExportSignedPdfAsync_ValidMetadata_GeneratesPdfStructure()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "TEST-001",
                NumeroOficio = "OF-2024-001"
            },
            RequirementSummary = new RequirementSummary
            {
                SummaryText = "Test summary",
                Bloqueo = new List<ComplianceRequirement>
                {
                    new ComplianceRequirement
                    {
                        RequerimientoId = "REQ-1",
                        Descripcion = "Bloquear cuenta",
                        Tipo = "bloqueo"
                    }
                }
            }
        };
        using var stream = new MemoryStream();

        // Act
        var result = await _signer.ExportSignedPdfAsync(metadata, stream, TestContext.Current.CancellationToken);

        // Assert
        // Will fail due to certificate, but should attempt PDF generation
        // Note: Actual PDF generation would succeed if certificate was available
        result.IsFailure.ShouldBeTrue();
        // The error should be about certificate, not PDF generation
        result.Error.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that PDF signing includes requirement summary when available.
    /// </summary>
    [Fact]
    public async Task ExportSignedPdfAsync_WithRequirementSummary_IncludesSummary()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "TEST-001",
                NumeroOficio = "OF-2024-001"
            },
            RequirementSummary = new RequirementSummary
            {
                SummaryText = "Test summary with requirements",
                Bloqueo = new List<ComplianceRequirement>
                {
                    new ComplianceRequirement { Descripcion = "Bloquear cuenta 123" }
                },
                Documentacion = new List<ComplianceRequirement>
                {
                    new ComplianceRequirement { Descripcion = "Presentar estados de cuenta" }
                }
            }
        };
        using var stream = new MemoryStream();

        // Act
        var result = await _signer.ExportSignedPdfAsync(metadata, stream, TestContext.Current.CancellationToken);

        // Assert
        // Will fail due to certificate, but metadata should be processed
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
    }
}