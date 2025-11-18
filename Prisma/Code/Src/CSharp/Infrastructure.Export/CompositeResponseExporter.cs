using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export;

/// <summary>
/// Composite response exporter that delegates to specialized exporters for XML and PDF formats.
/// </summary>
public class CompositeResponseExporter : IResponseExporter
{
    private readonly SiroXmlExporter _xmlExporter;
    private readonly DigitalPdfSigner _pdfSigner;
    private readonly ILogger<CompositeResponseExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeResponseExporter"/> class.
    /// </summary>
    /// <param name="xmlExporter">The XML exporter for SIRO XML exports.</param>
    /// <param name="pdfSigner">The PDF signer for digitally signed PDF exports.</param>
    /// <param name="logger">The logger instance.</param>
    public CompositeResponseExporter(
        SiroXmlExporter xmlExporter,
        DigitalPdfSigner pdfSigner,
        ILogger<CompositeResponseExporter> logger)
    {
        _xmlExporter = xmlExporter;
        _pdfSigner = pdfSigner;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result> ExportSiroXmlAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        return _xmlExporter.ExportSiroXmlAsync(metadata, outputStream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> ExportSignedPdfAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        return _pdfSigner.ExportSignedPdfAsync(metadata, outputStream, cancellationToken);
    }
}

