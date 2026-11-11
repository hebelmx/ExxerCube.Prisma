using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Adapter that implements IResponseExporter using the adaptive template system.
/// Enables zero-downtime migration from hardcoded SiroXmlExporter to AdaptiveExporter.
/// </summary>
/// <remarks>
/// Adapter Pattern for Backward Compatibility:
///
/// OLD (Hardcoded):
/// services.AddScoped&lt;IResponseExporter, SiroXmlExporter&gt;();
///
/// NEW (Adaptive):
/// services.AddScoped&lt;IResponseExporter, AdaptiveResponseExporterAdapter&gt;();
///
/// This one-line DI change enables adaptive templates WITHOUT breaking existing consumers.
/// </remarks>
public class AdaptiveResponseExporterAdapter : IResponseExporter
{
    private readonly IAdaptiveExporter _adaptiveExporter;
    private readonly ILogger<AdaptiveResponseExporterAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveResponseExporterAdapter"/> class.
    /// </summary>
    /// <param name="adaptiveExporter">The adaptive exporter.</param>
    /// <param name="logger">The logger.</param>
    public AdaptiveResponseExporterAdapter(
        IAdaptiveExporter adaptiveExporter,
        ILogger<AdaptiveResponseExporterAdapter> logger)
    {
        _adaptiveExporter = adaptiveExporter ?? throw new System.ArgumentNullException(nameof(adaptiveExporter));
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result> ExportSiroXmlAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Adaptive XML export requested for expediente: {Expediente}",
            metadata.Expediente?.NumeroExpediente ?? "Unknown");

        // Delegate to adaptive exporter with "XML" template type
        var exportResult = await _adaptiveExporter.ExportAsync(
            sourceObject: metadata,
            templateType: "XML",
            cancellationToken: cancellationToken);

        if (exportResult.IsFailure)
        {
            _logger.LogError(
                "Adaptive XML export failed for expediente: {Expediente}. Error: {Error}",
                metadata.Expediente?.NumeroExpediente ?? "Unknown",
                exportResult.Error);

            return Result.WithFailure(exportResult.Error ?? "Unknown error");
        }

        // Write the exported bytes to the output stream
        var bytes = exportResult.Value;
        if (bytes == null)
        {
            _logger.LogError("Export succeeded but returned null bytes");
            return Result.WithFailure("Export succeeded but returned null bytes");
        }

        await outputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Adaptive XML export successful for expediente: {Expediente}",
            metadata.Expediente?.NumeroExpediente ?? "Unknown");

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ExportSignedPdfAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("PDF signing not yet implemented - will be added in Story 1.8");

        // This will be implemented when PDF templates are added
        // For now, return the same message as SiroXmlExporter
        return Result.WithFailure("PDF signing functionality will be implemented in Story 1.8");
    }
}
