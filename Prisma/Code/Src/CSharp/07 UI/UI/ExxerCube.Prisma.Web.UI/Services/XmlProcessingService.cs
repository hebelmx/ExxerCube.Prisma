namespace ExxerCube.Prisma.Web.UI.Services;

using System.Text;
using System.Text.Json;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for processing XML fixture files.
/// Encapsulates XML loading, parsing, and field counting logic.
/// </summary>
public sealed class XmlProcessingService
{
    private readonly IXmlNullableParser<Expediente> _xmlParser;
    private readonly FixtureLoaderService _fixtureLoader;
    private readonly ILogger<XmlProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlProcessingService"/> class.
    /// </summary>
    /// <param name="xmlParser">XML parser for Expediente entities</param>
    /// <param name="fixtureLoader">Fixture file loader service</param>
    /// <param name="logger">Logger instance</param>
    public XmlProcessingService(
        IXmlNullableParser<Expediente> xmlParser,
        FixtureLoaderService fixtureLoader,
        ILogger<XmlProcessingService> logger)
    {
        _xmlParser = xmlParser;
        _fixtureLoader = fixtureLoader;
        _logger = logger;
    }

    /// <summary>
    /// Loads and processes an XML fixture file.
    /// </summary>
    /// <param name="fixtureName">The fixture file name (e.g., "222AAA-44444444442025.xml")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>XmlProcessingResult containing parsed Expediente and metadata</returns>
    public async Task<XmlProcessingResult> LoadFixtureAsync(
        string fixtureName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading XML fixture: {FixtureName}", fixtureName);

            // Load fixture bytes
            var xmlBytes = await _fixtureLoader.LoadFixtureBytesAsync(fixtureName, cancellationToken);
            var xmlContent = Encoding.UTF8.GetString(xmlBytes);

            _logger.LogDebug("XML fixture loaded: {Size} bytes", xmlBytes.Length);

            // Parse XML
            var parseResult = await _xmlParser.ParseAsync(xmlBytes, cancellationToken);

            if (!parseResult.IsSuccess || parseResult.Value == null)
            {
                var error = parseResult.Error ?? "XML parsing failed";
                _logger.LogError("Failed to parse XML fixture {FixtureName}: {Error}", fixtureName, error);
                throw new InvalidOperationException($"Failed to parse XML: {error}");
            }

            var expediente = parseResult.Value;

            // Count extracted fields
            var fieldCount = CountExtractedFields(expediente);

            // Create extraction metadata
            var metadata = CreateExtractionMetadata(expediente, fixtureName);

            // Serialize to JSON for display
            var jsonResult = JsonSerializer.Serialize(expediente, new JsonSerializerOptions { WriteIndented = true });

            _logger.LogInformation(
                "Successfully processed XML fixture {FixtureName}: {FieldCount} fields, {PartesCount} partes, {EspecificasCount} específicas",
                fixtureName, fieldCount, expediente.SolicitudPartes.Count, expediente.SolicitudEspecificas.Count);

            return new XmlProcessingResult
            {
                Expediente = expediente,
                FixtureName = fixtureName,
                SourceXml = xmlContent,
                FieldCount = fieldCount,
                Metadata = metadata,
                JsonResult = jsonResult
            };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "XML fixture not found: {FixtureName}", fixtureName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing XML fixture: {FixtureName}", fixtureName);
            throw;
        }
    }

    /// <summary>
    /// Counts the number of extracted fields in an Expediente.
    /// </summary>
    /// <param name="expediente">The Expediente entity</param>
    /// <returns>Total count of populated fields</returns>
    private int CountExtractedFields(Expediente expediente)
    {
        int count = 0;

        // Count root level fields (non-empty strings and non-default values)
        if (!string.IsNullOrWhiteSpace(expediente.NumeroExpediente)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.NumeroOficio)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.SolicitudSiara)) count++;
        if (expediente.Folio != 0) count++;
        if (expediente.OficioYear != DateTime.Now.Year && expediente.OficioYear != 0) count++;
        if (expediente.AreaClave != 0) count++;
        if (!string.IsNullOrWhiteSpace(expediente.AreaDescripcion)) count++;
        if (expediente.FechaPublicacion != DateTime.MinValue) count++;
        if (expediente.DiasPlazo != 0) count++;
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadNombre)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.AutoridadEspecificaNombre)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.NombreSolicitante)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia1)) count++;
        if (!string.IsNullOrWhiteSpace(expediente.Referencia2)) count++;

        // Count SolicitudPartes fields (10 fields per parte)
        count += expediente.SolicitudPartes.Count * 10;

        // Count SolicitudEspecificas fields (2 base + nested PersonasSolicitud)
        foreach (var especifica in expediente.SolicitudEspecificas)
        {
            count += 2; // SolicitudEspecificaId + InstruccionesCuentasPorConocer
            count += especifica.PersonasSolicitud.Count * 10; // 10 fields per persona
        }

        return count;
    }

    /// <summary>
    /// Creates extraction metadata for fusion service.
    /// </summary>
    /// <param name="expediente">The Expediente entity</param>
    /// <param name="sourceName">Name of the source file</param>
    /// <returns>ExtractionMetadata for fusion</returns>
    private ExtractionMetadata CreateExtractionMetadata(Expediente expediente, string sourceName)
    {
        return new ExtractionMetadata
        {
            Source = SourceType.XML_HandFilled,
            RegexMatches = 0, // TODO: calculate from validation
            TotalFieldsExtracted = CountExtractedFields(expediente),
            PatternViolations = 0,
            CatalogValidations = 0
        };
    }
}

/// <summary>
/// Result of processing an XML fixture file.
/// </summary>
public sealed record XmlProcessingResult
{
    /// <summary>
    /// Parsed Expediente entity.
    /// </summary>
    public required Expediente Expediente { get; init; }

    /// <summary>
    /// Name of the fixture file.
    /// </summary>
    public required string FixtureName { get; init; }

    /// <summary>
    /// Raw XML content for display.
    /// </summary>
    public required string SourceXml { get; init; }

    /// <summary>
    /// Count of successfully extracted fields.
    /// </summary>
    public required int FieldCount { get; init; }

    /// <summary>
    /// Extraction metadata for fusion service.
    /// </summary>
    public required ExtractionMetadata Metadata { get; init; }

    /// <summary>
    /// JSON-serialized representation of Expediente.
    /// </summary>
    public required string JsonResult { get; init; }
}
