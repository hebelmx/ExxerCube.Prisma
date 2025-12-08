namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Domain.Sources;

/// <summary>
/// Adapter that wraps <see cref="IAdaptiveDocxExtractor"/> to implement <see cref="IFieldExtractor{T}"/> for backward compatibility.
/// </summary>
/// <remarks>
/// <para>
/// This adapter enables transparent migration from the old DocxFieldExtractor to the new
/// adaptive extraction system without breaking existing consumer code. It implements the classic **Adapter Pattern**.
/// </para>
/// <para>
/// <strong>Migration Strategy:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Consumers continue using <see cref="IFieldExtractor{DocxSource}"/> interface</description></item>
///   <item><description>DI container injects this adapter instead of old DocxFieldExtractor</description></item>
///   <item><description>Adapter delegates to new <see cref="IAdaptiveDocxExtractor"/> with all 5 strategies</description></item>
///   <item><description>Zero consumer code changes required - transparent migration</description></item>
///   <item><description>Rollback: Simply swap DI registration back to old DocxFieldExtractor</description></item>
/// </list>
/// <para>
/// <strong>Extraction Flow:</strong> DocxSource → Extract Text → IAdaptiveDocxExtractor → ExtractedFields
/// </para>
/// </remarks>
public sealed class AdaptiveDocxFieldExtractorAdapter : IFieldExtractor<DocxSource>
{
    private readonly IAdaptiveDocxExtractor _adaptiveExtractor;
    private readonly ILogger<AdaptiveDocxFieldExtractorAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveDocxFieldExtractorAdapter"/> class.
    /// </summary>
    /// <param name="adaptiveExtractor">The adaptive DOCX extractor orchestrator.</param>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public AdaptiveDocxFieldExtractorAdapter(
        IAdaptiveDocxExtractor adaptiveExtractor,
        ILogger<AdaptiveDocxFieldExtractorAdapter> logger)
    {
        _adaptiveExtractor = adaptiveExtractor ?? throw new ArgumentNullException(nameof(adaptiveExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(DocxSource source, FieldDefinition[] fieldDefinitions)
    {
        if (source == null)
        {
            return Result<ExtractedFields>.WithFailure("DocxSource cannot be null");
        }

        try
        {
            _logger.LogDebug("AdaptiveDocxAdapter: Extracting fields from DOCX document ({FieldCount} field definitions)",
                fieldDefinitions?.Length ?? 0);

            // Extract text from DOCX source
            var textResult = await ExtractTextFromDocxSourceAsync(source);
            if (textResult.IsFailure)
            {
                return Result<ExtractedFields>.WithFailure(textResult.Error ?? "Failed to extract text from DOCX");
            }

            var docxText = textResult.Value ?? string.Empty;
            _logger.LogDebug("AdaptiveDocxAdapter: Extracted {Length} characters from DOCX", docxText.Length);

            // Use adaptive extraction (BestStrategy mode by default)
            var extractedFields = await _adaptiveExtractor.ExtractAsync(
                docxText,
                ExtractionMode.BestStrategy,
                null,
                CancellationToken.None);

            if (extractedFields == null)
            {
                _logger.LogWarning("AdaptiveDocxAdapter: No fields extracted from DOCX (all strategies returned null)");
                return Result<ExtractedFields>.Success(new ExtractedFields());
            }

            _logger.LogInformation("AdaptiveDocxAdapter: Successfully extracted fields - Expediente: {Expediente}, Causa: {Causa}",
                extractedFields.Expediente, extractedFields.Causa);

            return Result<ExtractedFields>.Success(extractedFields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveDocxAdapter: Error extracting fields from DOCX");
            return Result<ExtractedFields>.WithFailure($"Error extracting DOCX fields: {ex.Message}", default(ExtractedFields), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<FieldValue>> ExtractFieldAsync(DocxSource source, string fieldName)
    {
        if (source == null)
        {
            return Result<FieldValue>.WithFailure("DocxSource cannot be null");
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return Result<FieldValue>.WithFailure("Field name cannot be null or empty");
        }

        try
        {
            _logger.LogDebug("AdaptiveDocxAdapter: Extracting field '{FieldName}' from DOCX document", fieldName);

            // Extract text from DOCX source
            var textResult = await ExtractTextFromDocxSourceAsync(source);
            if (textResult.IsFailure)
            {
                return Result<FieldValue>.WithFailure(textResult.Error ?? "Failed to extract text from DOCX");
            }

            var docxText = textResult.Value ?? string.Empty;

            // Use adaptive extraction
            var extractedFields = await _adaptiveExtractor.ExtractAsync(
                docxText,
                ExtractionMode.BestStrategy,
                null,
                CancellationToken.None);

            if (extractedFields == null)
            {
                _logger.LogWarning("AdaptiveDocxAdapter: No fields extracted for '{FieldName}'", fieldName);
                return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in DOCX document");
            }

            // Map extracted field to FieldValue
            var fieldValue = MapExtractedFieldToFieldValue(extractedFields, fieldName);
            if (fieldValue == null)
            {
                _logger.LogDebug("AdaptiveDocxAdapter: Field '{FieldName}' not found in extraction result", fieldName);
                return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in DOCX document");
            }

            _logger.LogDebug("AdaptiveDocxAdapter: Successfully extracted field '{FieldName}': {Value}", fieldName, fieldValue.Value);
            return Result<FieldValue>.Success(fieldValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveDocxAdapter: Error extracting field '{FieldName}' from DOCX", fieldName);
            return Result<FieldValue>.WithFailure($"Error extracting field from DOCX: {ex.Message}", default(FieldValue), ex);
        }
    }

    //
    // Helper Methods
    //

    /// <summary>
    /// Extracts text content from DocxSource (byte[] or file path).
    /// </summary>
    private async Task<Result<string>> ExtractTextFromDocxSourceAsync(DocxSource source)
    {
        try
        {
            byte[] fileContent;

            // Get file content from source
            if (source.FileContent != null && source.FileContent.Length > 0)
            {
                fileContent = source.FileContent;
                _logger.LogDebug("AdaptiveDocxAdapter: Using FileContent ({Size} bytes)", fileContent.Length);
            }
            else if (!string.IsNullOrEmpty(source.FilePath) && File.Exists(source.FilePath))
            {
                fileContent = await File.ReadAllBytesAsync(source.FilePath);
                _logger.LogDebug("AdaptiveDocxAdapter: Read file from FilePath ({Size} bytes)", fileContent.Length);
            }
            else
            {
                return Result<string>.WithFailure("DocxSource must have either FileContent or valid FilePath");
            }

            // Extract text using DocumentFormat.OpenXml
            var textResult = ExtractTextFromDocxBytes(fileContent);
            return textResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveDocxAdapter: Error reading DOCX source");
            return Result<string>.WithFailure($"Error reading DOCX source: {ex.Message}", string.Empty, ex);
        }
    }

    /// <summary>
    /// Extracts text content from DOCX byte array using DocumentFormat.OpenXml.
    /// </summary>
    private Result<string> ExtractTextFromDocxBytes(byte[] fileContent)
    {
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return Result<string>.WithFailure("DOCX document has no main document part");
            }

            var body = mainPart.Document?.Body;
            if (body == null)
            {
                return Result<string>.WithFailure("DOCX document has no body");
            }

            // Extract text content preserving structure
            var textParts = body.Descendants<Text>().Select(t => t.Text);
            var textContent = string.Join(" ", textParts);

            _logger.LogDebug("AdaptiveDocxAdapter: Extracted {Length} characters from DOCX body", textContent.Length);
            return Result<string>.Success(textContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveDocxAdapter: Error parsing DOCX with OpenXml");
            return Result<string>.WithFailure($"Error reading DOCX: {ex.Message}", string.Empty, ex);
        }
    }

    /// <summary>
    /// Maps ExtractedFields to FieldValue for specific field name.
    /// </summary>
    private static FieldValue? MapExtractedFieldToFieldValue(ExtractedFields fields, string fieldName)
    {
        var normalizedFieldName = fieldName.ToLowerInvariant();

        var value = normalizedFieldName switch
        {
            "expediente" => fields.Expediente,
            "causa" => fields.Causa,
            "accionsolicitada" or "accion_solicitada" => fields.AccionSolicitada,
            _ => null
        };

        if (value != null)
        {
            // Confidence is implicit (adaptive extractor already selected best strategy)
            return new FieldValue(fieldName, value, 1.0f, "AdaptiveDOCX", FieldOrigin.Docx);
        }

        return null;
    }
}
