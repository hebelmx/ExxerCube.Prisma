using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Extraction;

/// <summary>
/// XML metadata extractor implementation for extracting metadata from XML documents.
/// </summary>
public class XmlMetadataExtractor : IMetadataExtractor
{
    private readonly IXmlNullableParser<Expediente> _xmlParser;
    private readonly ILogger<XmlMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlMetadataExtractor"/> class.
    /// </summary>
    /// <param name="xmlParser">The XML parser for Expediente entities.</param>
    /// <param name="logger">The logger instance.</param>
    public XmlMetadataExtractor(
        IXmlNullableParser<Expediente> xmlParser,
        ILogger<XmlMetadataExtractor> logger)
    {
        _xmlParser = xmlParser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ExtractedMetadata>> ExtractFromXmlAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Extracting metadata from XML document");

            var expedienteResult = await _xmlParser.ParseAsync(fileContent, cancellationToken);
            if (expedienteResult.IsFailure)
            {
                return Result<ExtractedMetadata>.WithFailure(expedienteResult.Error ?? "Failed to parse XML");
            }

            var expediente = expedienteResult.Value;
            if (expediente == null)
            {
                return Result<ExtractedMetadata>.WithFailure("Parsed expediente is null");
            }

            // Extract RFC values from parties
            var rfcValues = expediente.SolicitudPartes
                .Where(p => !string.IsNullOrEmpty(p.Rfc))
                .Select(p => p.Rfc!)
                .ToArray();

            // Extract names from parties
            var names = expediente.SolicitudPartes
                .Select(p => $"{p.Nombre} {p.Paterno ?? string.Empty} {p.Materno ?? string.Empty}".Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();

            // Extract dates
            var dates = new[] { expediente.FechaPublicacion }
                .Where(d => d != DateTime.MinValue)
                .ToArray();

            // Extract legal references
            var legalReferences = new[]
            {
                expediente.Referencia,
                expediente.Referencia1,
                expediente.Referencia2
            }
            .Where(r => !string.IsNullOrEmpty(r))
            .ToArray();

            var metadata = new ExtractedMetadata
            {
                Expediente = expediente,
                RfcValues = rfcValues.Length > 0 ? rfcValues : null,
                Names = names.Length > 0 ? names : null,
                Dates = dates.Length > 0 ? dates : null,
                LegalReferences = legalReferences.Length > 0 ? legalReferences : null
            };

            _logger.LogDebug("Successfully extracted metadata from XML document");
            return Result<ExtractedMetadata>.Success(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from XML");
            return Result<ExtractedMetadata>.WithFailure($"Error extracting XML metadata: {ex.Message}", default(ExtractedMetadata), ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromDocxAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("Docx extraction not supported by XmlMetadataExtractor. Use DocxMetadataExtractor instead."));
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromPdfAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("PDF extraction not supported by XmlMetadataExtractor. Use PdfMetadataExtractor instead."));
    }
}

