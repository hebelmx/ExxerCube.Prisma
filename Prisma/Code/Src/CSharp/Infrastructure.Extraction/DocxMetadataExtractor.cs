using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Extraction;

/// <summary>
/// DOCX metadata extractor implementation for extracting metadata from DOCX documents using DocumentFormat.OpenXml.
/// </summary>
public class DocxMetadataExtractor : IMetadataExtractor
{
    private readonly ILogger<DocxMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxMetadataExtractor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DocxMetadataExtractor(ILogger<DocxMetadataExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromDocxAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Extracting metadata from DOCX document");

            using var stream = new System.IO.MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return Task.FromResult(Result<ExtractedMetadata>.WithFailure("DOCX document has no main document part"));
            }

            var body = mainPart.Document?.Body;
            if (body == null)
            {
                return Task.FromResult(Result<ExtractedMetadata>.WithFailure("DOCX document has no body"));
            }

            // Extract text content
            var textContent = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));

            // Extract structured fields using pattern matching
            var expediente = ExtractExpediente(textContent);
            var rfcValues = ExtractRfcValues(textContent);
            var names = ExtractNames(textContent);
            var dates = ExtractDates(textContent);
            var legalReferences = ExtractLegalReferences(textContent);

            var metadata = new ExtractedMetadata
            {
                Expediente = expediente,
                RfcValues = rfcValues.Length > 0 ? rfcValues : null,
                Names = names.Length > 0 ? names : null,
                Dates = dates.Length > 0 ? dates : null,
                LegalReferences = legalReferences.Length > 0 ? legalReferences : null
            };

            _logger.LogDebug("Successfully extracted metadata from DOCX document");
            return Task.FromResult(Result<ExtractedMetadata>.Success(metadata));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from DOCX");
            return Task.FromResult(Result<ExtractedMetadata>.WithFailure($"Error extracting DOCX metadata: {ex.Message}", default(ExtractedMetadata), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromXmlAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("XML extraction not supported by DocxMetadataExtractor. Use XmlMetadataExtractor instead."));
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromPdfAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ExtractedMetadata>.WithFailure("PDF extraction not supported by DocxMetadataExtractor. Use PdfMetadataExtractor instead."));
    }

    private static Expediente? ExtractExpediente(string text)
    {
        // Pattern: A/AS1-2505-088637-PHM or similar
        var expedientePattern = @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+";
        var match = System.Text.RegularExpressions.Regex.Match(text, expedientePattern);
        if (match.Success)
        {
            return new Expediente
            {
                NumeroExpediente = match.Value,
                AreaDescripcion = ExtractAreaDescripcion(text)
            };
        }

        return null;
    }

    private static string ExtractAreaDescripcion(string text)
    {
        var areas = new[] { "ASEGURAMIENTO", "HACENDARIO", "JUDICIAL" };
        return areas.FirstOrDefault(a => text.Contains(a, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private static string[] ExtractRfcValues(string text)
    {
        // RFC pattern: 4 letters, 6 digits, 3 alphanumeric
        var rfcPattern = @"[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, rfcPattern);
        return matches.Select(m => m.Value).Distinct().ToArray();
    }

    private static string[] ExtractNames(string text)
    {
        // Simple name extraction - look for capitalized words sequences
        var namePattern = @"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, namePattern);
        return matches.Select(m => m.Value.Trim()).Distinct().Take(10).ToArray();
    }

    private static DateTime[] ExtractDates(string text)
    {
        // Date patterns: DD/MM/YYYY, DD-MM-YYYY, etc.
        var datePatterns = new[]
        {
            @"\d{2}/\d{2}/\d{4}",
            @"\d{2}-\d{2}-\d{4}",
            @"\d{4}-\d{2}-\d{2}"
        };

        var dates = new System.Collections.Generic.List<DateTime>();
        foreach (var pattern in datePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (DateTime.TryParse(match.Value, out var date))
                {
                    dates.Add(date);
                }
            }
        }

        return dates.Distinct().ToArray();
    }

    private static string[] ExtractLegalReferences(string text)
    {
        // Look for legal reference patterns
        var referencePatterns = new[]
        {
            @"(?:Referencia|REF|Ref\.?)\s*:?\s*([A-Z0-9/-]+)",
            @"(?:Artículo|Art\.?)\s+\d+",
            @"(?:Ley|LEY)\s+[A-Z0-9]+"
        };

        var references = new System.Collections.Generic.List<string>();
        foreach (var pattern in referencePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            references.AddRange(matches.Select(m => m.Value.Trim()));
        }

        return references.Distinct().ToArray();
    }

    /// <inheritdoc />
    public Task<Result<string>> ExtractTextAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("DOCX text extraction cancelled before starting");
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }

        try
        {
            _logger.LogDebug("Extracting text from DOCX document");

            using var stream = new System.IO.MemoryStream(fileContent);
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            var mainPart = wordDocument.MainDocumentPart;
            if (mainPart == null)
            {
                return Task.FromResult(Result<string>.WithFailure("DOCX document has no main document part"));
            }

            var body = mainPart.Document?.Body;
            if (body == null)
            {
                return Task.FromResult(Result<string>.WithFailure("DOCX document has no body"));
            }

            // Extract text content
            var textContent = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));

            _logger.LogDebug("Successfully extracted text from DOCX document (length: {Length})", textContent.Length);
            return Task.FromResult(Result<string>.Success(textContent));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("DOCX text extraction cancelled");
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from DOCX");
            return Task.FromResult(Result<string>.WithFailure($"Error extracting DOCX text: {ex.Message}", default(string), ex));
        }
    }
}

