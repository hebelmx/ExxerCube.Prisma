using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Orchestrator for adaptive export operations using template-based configuration.
/// Coordinates ITemplateRepository and ITemplateFieldMapper to produce exports without code changes.
/// </summary>
public class AdaptiveExporter : IAdaptiveExporter
{
    private readonly ITemplateRepository _templateRepository;
    private readonly ITemplateFieldMapper _fieldMapper;
    private readonly ILogger<AdaptiveExporter> _logger;
    private readonly ConcurrentDictionary<string, TemplateDefinition> _templateCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveExporter"/> class.
    /// </summary>
    /// <param name="templateRepository">The template repository.</param>
    /// <param name="fieldMapper">The field mapper.</param>
    /// <param name="logger">The logger instance.</param>
    public AdaptiveExporter(
        ITemplateRepository templateRepository,
        ITemplateFieldMapper fieldMapper,
        ILogger<AdaptiveExporter> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _fieldMapper = fieldMapper ?? throw new ArgumentNullException(nameof(fieldMapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateCache = new ConcurrentDictionary<string, TemplateDefinition>();
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
        {
            return Result<byte[]>.Failure("Source object cannot be null");
        }

        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result<byte[]>.Failure("Template type cannot be null or empty");
        }

        try
        {
            // Get active template
            var templateResult = await GetActiveTemplateAsync(templateType, cancellationToken);

            if (templateResult.IsFailure)
            {
                return Result<byte[]>.Failure(templateResult.Error ?? "Failed to retrieve active template");
            }

            var template = templateResult.Value!;

            // Map fields using the field mapper
            var mappingResult = await _fieldMapper.MapAllFieldsAsync(sourceObject, template, cancellationToken);

            if (mappingResult.IsFailure)
            {
                return Result<byte[]>.Failure($"Field mapping failed: {mappingResult.Error}");
            }

            // Generate export based on template type
            var exportBytes = await GenerateExportAsync(template, mappingResult.Value!, cancellationToken);

            return Result<byte[]>.Success(exportBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data for template type '{TemplateType}'", templateType);
            return Result<byte[]>.Failure($"Export error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportWithVersionAsync(
        object sourceObject,
        string templateType,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
        {
            return Result<byte[]>.Failure("Source object cannot be null");
        }

        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result<byte[]>.Failure("Template type cannot be null or empty");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Result<byte[]>.Failure("Version cannot be null or empty");
        }

        try
        {
            // Get specific version of template
            var template = await _templateRepository.GetTemplateAsync(templateType, version, cancellationToken);

            if (template == null)
            {
                return Result<byte[]>.Failure($"Template version '{version}' not found for type '{templateType}'");
            }

            // Map fields using the field mapper
            var mappingResult = await _fieldMapper.MapAllFieldsAsync(sourceObject, template, cancellationToken);

            if (mappingResult.IsFailure)
            {
                return Result<byte[]>.Failure($"Field mapping failed: {mappingResult.Error}");
            }

            // Generate export based on template type
            var exportBytes = await GenerateExportAsync(template, mappingResult.Value!, cancellationToken);

            return Result<byte[]>.Success(exportBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data for template type '{TemplateType}' version '{Version}'",
                templateType, version);
            return Result<byte[]>.Failure($"Export error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<TemplateDefinition>> GetActiveTemplateAsync(
        string templateType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result<TemplateDefinition>.Failure("Template type cannot be null or empty");
        }

        try
        {
            // Check cache first
            var cacheKey = $"active_{templateType}";
            if (_templateCache.TryGetValue(cacheKey, out var cachedTemplate))
            {
                _logger.LogDebug("Retrieved template '{TemplateType}' from cache", templateType);
                return Result<TemplateDefinition>.Success(cachedTemplate);
            }

            // Get from repository
            var template = await _templateRepository.GetLatestTemplateAsync(templateType, cancellationToken);

            if (template == null)
            {
                return Result<TemplateDefinition>.Failure($"No active template found for type '{templateType}'");
            }

            // Cache the template
            _templateCache.TryAdd(cacheKey, template);

            return Result<TemplateDefinition>.Success(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active template for type '{TemplateType}'", templateType);
            return Result<TemplateDefinition>.Failure($"Error retrieving template: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ValidateExportAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
        {
            return Result.Failure("Source object cannot be null");
        }

        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result.Failure("Template type cannot be null or empty");
        }

        try
        {
            // Get active template
            var templateResult = await GetActiveTemplateAsync(templateType, cancellationToken);

            if (templateResult.IsFailure)
            {
                return Result.Failure($"Template '{templateType}' not found");
            }

            var template = templateResult.Value!;

            // Validate each field mapping
            foreach (var mapping in template.FieldMappings)
            {
                var mapResult = await _fieldMapper.MapFieldAsync(sourceObject, mapping, cancellationToken);

                if (mapResult.IsFailure && mapping.IsRequired)
                {
                    return Result.Failure(mapResult.Error ?? $"Required field '{mapping.SourceFieldPath}' validation failed");
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating export for template type '{TemplateType}'", templateType);
            return Result.Failure($"Validation error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, string>>> PreviewMappingAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
        {
            return Result<Dictionary<string, string>>.Failure("Source object cannot be null");
        }

        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result<Dictionary<string, string>>.Failure("Template type cannot be null or empty");
        }

        try
        {
            // Get active template
            var templateResult = await GetActiveTemplateAsync(templateType, cancellationToken);

            if (templateResult.IsFailure)
            {
                return Result<Dictionary<string, string>>.Failure($"Template '{templateType}' not found");
            }

            var template = templateResult.Value!;

            // Map all fields using field mapper
            var mappingResult = await _fieldMapper.MapAllFieldsAsync(sourceObject, template, cancellationToken);

            if (mappingResult.IsFailure)
            {
                return Result<Dictionary<string, string>>.Failure(mappingResult.Error ?? "Mapping preview failed");
            }

            return Result<Dictionary<string, string>>.Success(mappingResult.Value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing mapping for template type '{TemplateType}'", templateType);
            return Result<Dictionary<string, string>>.Failure($"Preview error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void ClearTemplateCache()
    {
        _templateCache.Clear();
        _logger.LogInformation("Template cache cleared");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> IsTemplateAvailableAsync(
        string templateType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result<bool>.Success(false);
        }

        try
        {
            var template = await _templateRepository.GetLatestTemplateAsync(templateType, cancellationToken);
            return Result<bool>.Success(template != null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking template availability for type '{TemplateType}'", templateType);
            return Result<bool>.Failure($"Error checking template availability: {ex.Message}");
        }
    }

    //
    // Private helper methods
    //

    private async Task<byte[]> GenerateExportAsync(
        TemplateDefinition template,
        Dictionary<string, string> mappedFields,
        CancellationToken cancellationToken)
    {
        // For now, create a simple placeholder export
        // In the future, this will delegate to specific exporters (Excel, XML, DOCX)
        // based on template.TemplateType

        await Task.CompletedTask; // Avoid unused parameter warning

        return template.TemplateType.ToLowerInvariant() switch
        {
            "excel" => GenerateExcelExport(template, mappedFields),
            "xml" => GenerateXmlExport(template, mappedFields),
            "docx" => GenerateDocxExport(template, mappedFields),
            _ => throw new NotSupportedException($"Template type '{template.TemplateType}' is not supported")
        };
    }

    private static byte[] GenerateExcelExport(TemplateDefinition template, Dictionary<string, string> mappedFields)
    {
        // Create a new Excel workbook using ClosedXML
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Export");

        // Get field mappings ordered by DisplayOrder
        var orderedMappings = template.FieldMappings
            .OrderBy(fm => fm.DisplayOrder)
            .ToList();

        // Write headers (row 1)
        var columnIndex = 1;
        foreach (var mapping in orderedMappings)
        {
            worksheet.Cell(1, columnIndex).Value = mapping.TargetField;
            columnIndex++;
        }

        // Write data (row 2)
        columnIndex = 1;
        foreach (var mapping in orderedMappings)
        {
            if (mappedFields.TryGetValue(mapping.TargetField, out var value))
            {
                worksheet.Cell(2, columnIndex).Value = value;
            }
            columnIndex++;
        }

        // Convert workbook to byte array
        using var memoryStream = new System.IO.MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static byte[] GenerateXmlExport(TemplateDefinition template, Dictionary<string, string> mappedFields)
    {
        // Create XML document with root element
        var xmlDoc = new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XElement("Export")
        );

        // Get field mappings ordered by DisplayOrder
        var orderedMappings = template.FieldMappings
            .OrderBy(fm => fm.DisplayOrder)
            .ToList();

        // Add elements in order
        foreach (var mapping in orderedMappings)
        {
            if (mappedFields.TryGetValue(mapping.TargetField, out var value))
            {
                xmlDoc.Root!.Add(new System.Xml.Linq.XElement(mapping.TargetField, value));
            }
        }

        // Convert XML document to byte array using StringWriter to avoid BOM issues
        using var stringWriter = new System.IO.StringWriter();
        xmlDoc.Save(stringWriter);
        var xmlString = stringWriter.ToString();
        return System.Text.Encoding.UTF8.GetBytes(xmlString);
    }

    private static byte[] GenerateDocxExport(TemplateDefinition template, Dictionary<string, string> mappedFields)
    {
        // Create DOCX document using DocumentFormat.OpenXml
        using var memoryStream = new System.IO.MemoryStream();

        // Create a new Word document
        using (var wordDocument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            memoryStream,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            // Add main document part
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());

            // Get field mappings ordered by DisplayOrder
            var orderedMappings = template.FieldMappings
                .OrderBy(fm => fm.DisplayOrder)
                .ToList();

            // Add paragraphs for each field
            foreach (var mapping in orderedMappings)
            {
                if (mappedFields.TryGetValue(mapping.TargetField, out var value))
                {
                    var paragraph = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                    var run = paragraph.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());

                    // Add text: "FieldLabel: FieldValue"
                    run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text($"{mapping.TargetField}: {value}"));
                }
            }

            // Save the document
            mainPart.Document.Save();
        }

        return memoryStream.ToArray();
    }
}
