namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

/// <summary>
/// Service for comparing extracted data from different sources (XML vs OCR).
/// Implements exact match with fuzzy fallback strategy using FuzzySharp.
/// </summary>
public class DocumentComparisonService : IDocumentComparisonService
{
    private readonly ILogger<DocumentComparisonService> _logger;

    // Similarity thresholds
    private const float PartialMatchThreshold = 0.8f; // 80% similarity = "Partial"
    private const float ExactMatchThreshold = 1.0f;   // 100% similarity = "Match"

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentComparisonService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DocumentComparisonService(ILogger<DocumentComparisonService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compares two Expediente objects and returns detailed field-level comparison results.
    /// </summary>
    public async Task<ComparisonResult> CompareExpedientesAsync(
        Expediente xmlExpediente,
        Expediente ocrExpediente,
        CancellationToken cancellationToken = default)
    {
        if (xmlExpediente == null) throw new ArgumentNullException(nameof(xmlExpediente));
        if (ocrExpediente == null) throw new ArgumentNullException(nameof(ocrExpediente));

        _logger.LogInformation("Comparing expedientes: XML vs OCR");

        var comparisons = new List<FieldComparison>();

        // Compare all string fields
        comparisons.Add(CompareField("NumeroExpediente", xmlExpediente.NumeroExpediente, ocrExpediente.NumeroExpediente));
        comparisons.Add(CompareField("NumeroOficio", xmlExpediente.NumeroOficio, ocrExpediente.NumeroOficio));
        comparisons.Add(CompareField("SolicitudSiara", xmlExpediente.SolicitudSiara, ocrExpediente.SolicitudSiara));
        comparisons.Add(CompareField("AreaDescripcion", xmlExpediente.AreaDescripcion, ocrExpediente.AreaDescripcion));
        comparisons.Add(CompareField("AutoridadNombre", xmlExpediente.AutoridadNombre, ocrExpediente.AutoridadNombre));
        comparisons.Add(CompareField("AutoridadEspecificaNombre", xmlExpediente.AutoridadEspecificaNombre, ocrExpediente.AutoridadEspecificaNombre));
        comparisons.Add(CompareField("NombreSolicitante", xmlExpediente.NombreSolicitante, ocrExpediente.NombreSolicitante));
        comparisons.Add(CompareField("Referencia", xmlExpediente.Referencia, ocrExpediente.Referencia));
        comparisons.Add(CompareField("Referencia1", xmlExpediente.Referencia1, ocrExpediente.Referencia1));
        comparisons.Add(CompareField("Referencia2", xmlExpediente.Referencia2, ocrExpediente.Referencia2));

        // Compare numeric fields (converted to strings for consistency)
        comparisons.Add(CompareField("Folio", xmlExpediente.Folio.ToString(), ocrExpediente.Folio.ToString()));
        comparisons.Add(CompareField("OficioYear", xmlExpediente.OficioYear.ToString(), ocrExpediente.OficioYear.ToString()));
        comparisons.Add(CompareField("AreaClave", xmlExpediente.AreaClave.ToString(), ocrExpediente.AreaClave.ToString()));
        comparisons.Add(CompareField("DiasPlazo", xmlExpediente.DiasPlazo.ToString(), ocrExpediente.DiasPlazo.ToString()));

        // Compare date fields
        comparisons.Add(CompareField("FechaPublicacion",
            xmlExpediente.FechaPublicacion.ToString("yyyy-MM-dd"),
            ocrExpediente.FechaPublicacion.ToString("yyyy-MM-dd")));

        // Compare boolean fields
        comparisons.Add(CompareField("TieneAseguramiento",
            xmlExpediente.TieneAseguramiento.ToString(),
            ocrExpediente.TieneAseguramiento.ToString()));

        // Calculate overall statistics
        var matchCount = comparisons.Count(c => c.Status == "Match");
        var totalFields = comparisons.Count;
        var overallSimilarity = comparisons.Average(c => c.Similarity);

        var result = new ComparisonResult
        {
            FieldComparisons = comparisons,
            MatchCount = matchCount,
            TotalFields = totalFields,
            OverallSimilarity = overallSimilarity
        };

        _logger.LogInformation("Comparison complete: {MatchCount}/{TotalFields} exact matches ({OverallSimilarity:P0} overall similarity)",
            matchCount, totalFields, overallSimilarity);

        return await Task.FromResult(result);
    }

    /// <summary>
    /// Compares a single field value from XML and OCR sources.
    /// Uses exact match first, then falls back to fuzzy matching.
    /// </summary>
    public FieldComparison CompareField(
        string fieldName,
        string? xmlValue,
        string? ocrValue,
        float? ocrConfidence = null)
    {
        // Normalize values (trim whitespace)
        var normalizedXml = xmlValue?.Trim() ?? string.Empty;
        var normalizedOcr = ocrValue?.Trim() ?? string.Empty;

        // Handle null/empty cases
        if (string.IsNullOrEmpty(normalizedXml) && string.IsNullOrEmpty(normalizedOcr))
        {
            // Both empty = match
            return new FieldComparison
            {
                FieldName = fieldName,
                XmlValue = normalizedXml,
                OcrValue = normalizedOcr,
                Status = "Match",
                Similarity = 1.0f,
                OcrConfidence = ocrConfidence
            };
        }

        if (string.IsNullOrEmpty(normalizedXml) || string.IsNullOrEmpty(normalizedOcr))
        {
            // One empty, one not = missing
            return new FieldComparison
            {
                FieldName = fieldName,
                XmlValue = normalizedXml,
                OcrValue = normalizedOcr,
                Status = "Missing",
                Similarity = 0.0f,
                OcrConfidence = ocrConfidence
            };
        }

        // 1. Try exact match first (case-sensitive)
        if (string.Equals(normalizedXml, normalizedOcr, StringComparison.Ordinal))
        {
            return new FieldComparison
            {
                FieldName = fieldName,
                XmlValue = normalizedXml,
                OcrValue = normalizedOcr,
                Status = "Match",
                Similarity = 1.0f,
                OcrConfidence = ocrConfidence
            };
        }

        // 2. Fallback to fuzzy matching using FuzzySharp
        var fuzzyScore = Fuzz.Ratio(normalizedXml, normalizedOcr);
        var similarityScore = fuzzyScore / 100.0f; // Convert 0-100 to 0.0-1.0

        // Determine status based on similarity
        string status;
        if (similarityScore >= PartialMatchThreshold)
        {
            status = "Partial";
        }
        else
        {
            status = "Different";
        }

        _logger.LogDebug("Fuzzy match for '{FieldName}': '{XmlValue}' vs '{OcrValue}' = {Score:P0} ({Status})",
            fieldName, normalizedXml, normalizedOcr, similarityScore, status);

        return new FieldComparison
        {
            FieldName = fieldName,
            XmlValue = normalizedXml,
            OcrValue = normalizedOcr,
            Status = status,
            Similarity = similarityScore,
            OcrConfidence = ocrConfidence
        };
    }
}
