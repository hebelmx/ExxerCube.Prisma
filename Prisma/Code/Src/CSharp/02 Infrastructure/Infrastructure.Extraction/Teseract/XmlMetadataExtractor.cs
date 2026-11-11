namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

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

    /// <inheritdoc />
    public Task<Result<string>> ExtractTextAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("XML text extraction cancelled before starting");
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }

        try
        {
            _logger.LogDebug("Extracting text from XML document");
            var text = System.Text.Encoding.UTF8.GetString(fileContent);
            return Task.FromResult(Result<string>.Success(text));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("XML text extraction cancelled");
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from XML");
            return Task.FromResult(Result<string>.WithFailure($"Error extracting XML text: {ex.Message}", default(string), ex));
        }
    }
}

