using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExxerCube.Prisma.Infrastructure.Export;

/// <summary>
/// Implements PDF requirement summarization using rule-based classification to categorize requirements.
/// </summary>
public class PdfRequirementSummarizerService : IPdfRequirementSummarizer
{
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ILogger<PdfRequirementSummarizerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRequirementSummarizerService"/> class.
    /// </summary>
    /// <param name="metadataExtractor">The metadata extractor for extracting text from PDFs.</param>
    /// <param name="logger">The logger instance.</param>
    public PdfRequirementSummarizerService(
        IMetadataExtractor metadataExtractor,
        ILogger<PdfRequirementSummarizerService> logger)
    {
        _metadataExtractor = metadataExtractor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<RequirementSummary>> SummarizeRequirementsAsync(
        byte[] pdfContent,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("PDF requirement summarization cancelled before starting");
            return ResultExtensions.Cancelled<RequirementSummary>();
        }

        // Input validation
        if (pdfContent == null || pdfContent.Length == 0)
        {
            return Result<RequirementSummary>.WithFailure("PDF content cannot be null or empty");
        }

        try
        {
            _logger.LogDebug("Starting PDF requirement summarization");

            // Extract text from PDF using metadata extractor (uses OCR fallback)
            var textResult = await ExtractTextFromPdfAsync(pdfContent, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation
            if (textResult.IsCancelled())
            {
                _logger.LogWarning("PDF text extraction cancelled");
                return ResultExtensions.Cancelled<RequirementSummary>();
            }

            if (textResult.IsFailure)
            {
                _logger.LogError("Failed to extract text from PDF: {Error}", textResult.Error);
                return Result<RequirementSummary>.WithFailure($"Failed to extract text from PDF: {textResult.Error}");
            }

            var pdfText = textResult.Value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(pdfText))
            {
                return Result<RequirementSummary>.WithFailure("No text extracted from PDF");
            }

            // Summarize requirements from extracted text
            return await SummarizeRequirementsFromTextAsync(pdfText, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("PDF requirement summarization cancelled");
            return ResultExtensions.Cancelled<RequirementSummary>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing PDF requirements");
            return Result<RequirementSummary>.WithFailure($"Error summarizing PDF requirements: {ex.Message}", default(RequirementSummary), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<RequirementSummary>> SummarizeRequirementsFromTextAsync(
        string pdfText,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("PDF requirement summarization cancelled before starting");
            return ResultExtensions.Cancelled<RequirementSummary>();
        }

        // Input validation
        if (string.IsNullOrWhiteSpace(pdfText))
        {
            return Result<RequirementSummary>.WithFailure("PDF text cannot be null or empty");
        }

        try
        {
            _logger.LogDebug("Summarizing requirements from PDF text (length: {Length})", pdfText.Length);

            // Categorize requirements using rule-based classification
            var summary = new RequirementSummary
            {
                ExtractedAt = DateTime.UtcNow
            };

            // Extract all requirements from text
            var allRequirements = ExtractRequirementsFromText(pdfText);

            // Categorize requirements
            foreach (var requirement in allRequirements)
            {
                var category = CategorizeRequirement(requirement, pdfText);
                switch (category.ToLowerInvariant())
                {
                    case "bloqueo":
                        summary.Bloqueo.Add(requirement);
                        break;
                    case "desbloqueo":
                        summary.Desbloqueo.Add(requirement);
                        break;
                    case "documentacion":
                        summary.Documentacion.Add(requirement);
                        break;
                    case "transferencia":
                        summary.Transferencia.Add(requirement);
                        break;
                    case "informacion":
                        summary.Informacion.Add(requirement);
                        break;
                    default:
                        // Add to general requirements list if category is unclear
                        summary.Requirements.Add(requirement);
                        break;
                }
            }

            // Generate human-readable summary text
            summary.SummaryText = GenerateSummaryText(summary);
            summary.Requirements = allRequirements;
            summary.ConfidenceScore = CalculateConfidenceScore(summary);

            _logger.LogInformation("Successfully summarized {TotalCount} requirements into {Bloqueo} bloqueo, {Desbloqueo} desbloqueo, {Documentacion} documentacion, {Transferencia} transferencia, {Informacion} informacion",
                allRequirements.Count,
                summary.Bloqueo.Count,
                summary.Desbloqueo.Count,
                summary.Documentacion.Count,
                summary.Transferencia.Count,
                summary.Informacion.Count);

            return Result<RequirementSummary>.Success(summary);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("PDF requirement summarization cancelled");
            return ResultExtensions.Cancelled<RequirementSummary>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing requirements from text");
            return Result<RequirementSummary>.WithFailure($"Error summarizing requirements: {ex.Message}", default(RequirementSummary), ex);
        }
    }

    private async Task<Result<string>> ExtractTextFromPdfAsync(byte[] pdfContent, CancellationToken cancellationToken)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("PDF text extraction cancelled before starting");
            return ResultExtensions.Cancelled<string>();
        }

        try
        {
            // Try to extract text directly from PDF using PdfSharp
            var textBuilder = new StringBuilder();
            using var stream = new System.IO.MemoryStream(pdfContent);
            var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

            foreach (PdfPage page in document.Pages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ResultExtensions.Cancelled<string>();
                }

                // Extract text from page (PdfSharp basic text extraction)
                // Note: PdfSharp has limited text extraction - for better results, use IMetadataExtractor with OCR
                var pageText = ExtractTextFromPage(page);
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    textBuilder.AppendLine(pageText);
                }
            }

            var extractedText = textBuilder.ToString();

            // If very little text extracted, use metadata extractor with OCR fallback
            if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length < 50)
            {
                _logger.LogDebug("PdfSharp extracted minimal text, using metadata extractor with OCR fallback");
                var metadataResult = await _metadataExtractor.ExtractFromPdfAsync(pdfContent, cancellationToken).ConfigureAwait(false);

                if (metadataResult.IsCancelled())
                {
                    return ResultExtensions.Cancelled<string>();
                }

                if (metadataResult.IsFailure)
                {
                    return Result<string>.WithFailure($"Failed to extract text via metadata extractor: {metadataResult.Error}");
                }

                // Extract text from ExtractedFields if available
                var metadata = metadataResult.Value;
                if (metadata?.ExtractedFields != null)
                {
                    // Reconstruct text from extracted fields
                    extractedText = ReconstructTextFromExtractedFields(metadata.ExtractedFields);
                }
            }

            return Result<string>.Success(extractedText);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("PDF text extraction cancelled");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text directly from PDF, will use metadata extractor");
            // Fallback to metadata extractor
            var metadataResult = await _metadataExtractor.ExtractFromPdfAsync(pdfContent, cancellationToken).ConfigureAwait(false);

            if (metadataResult.IsCancelled())
            {
                return ResultExtensions.Cancelled<string>();
            }

            if (metadataResult.IsFailure)
            {
                return Result<string>.WithFailure($"Failed to extract text: {metadataResult.Error}");
            }

            var metadata = metadataResult.Value;
            if (metadata?.ExtractedFields == null)
            {
                return Result<string>.WithFailure("No text could be extracted from PDF and metadata extractor returned no fields");
            }

            var text = ReconstructTextFromExtractedFields(metadata.ExtractedFields);
            return Result<string>.Success(text ?? string.Empty);
        }
    }

    private static string ExtractTextFromPage(PdfPage page)
    {
        // PdfSharp has limited text extraction capabilities
        // This is a basic implementation - for production, consider using a more advanced PDF library
        // or rely on IMetadataExtractor with OCR fallback
        try
        {
            // PdfSharp doesn't have built-in text extraction, so we return empty
            // and rely on OCR fallback via IMetadataExtractor
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReconstructTextFromExtractedFields(ExtractedFields? extractedFields)
    {
        if (extractedFields == null)
        {
            return string.Empty;
        }

        var textBuilder = new StringBuilder();
        
        // Reconstruct text from ExtractedFields properties
        if (!string.IsNullOrWhiteSpace(extractedFields.Expediente))
        {
            textBuilder.AppendLine($"Expediente: {extractedFields.Expediente}");
        }
        
        if (!string.IsNullOrWhiteSpace(extractedFields.Causa))
        {
            textBuilder.AppendLine($"Causa: {extractedFields.Causa}");
        }
        
        if (!string.IsNullOrWhiteSpace(extractedFields.AccionSolicitada))
        {
            textBuilder.AppendLine($"Acción Solicitada: {extractedFields.AccionSolicitada}");
        }
        
        foreach (var fecha in extractedFields.Fechas)
        {
            if (!string.IsNullOrWhiteSpace(fecha))
            {
                textBuilder.AppendLine($"Fecha: {fecha}");
            }
        }
        
        foreach (var monto in extractedFields.Montos)
        {
            textBuilder.AppendLine($"Monto: {monto.Value} {monto.Currency}");
        }

        return textBuilder.ToString();
    }

    private static List<ComplianceRequirement> ExtractRequirementsFromText(string text)
    {
        var requirements = new List<ComplianceRequirement>();
        var requirementId = 1;

        // Look for requirement patterns in Spanish regulatory documents
        var requirementPatterns = new[]
        {
            @"(?:REQUERIMIENTO|REQUISITO|SOLICITUD)[\s:]+(.+?)(?=\n|REQUERIMIENTO|REQUISITO|SOLICITUD|$)",
            @"(?:BLOQUEO|BLOQUEAR)[\s:]+(.+?)(?=\n|DESBLOQUEO|TRANSFERENCIA|DOCUMENTACIÓN|INFORMACIÓN|$)",
            @"(?:DESBLOQUEO|DESBLOQUEAR)[\s:]+(.+?)(?=\n|BLOQUEO|TRANSFERENCIA|DOCUMENTACIÓN|INFORMACIÓN|$)",
            @"(?:TRANSFERENCIA|TRANSFERIR)[\s:]+(.+?)(?=\n|BLOQUEO|DESBLOQUEO|DOCUMENTACIÓN|INFORMACIÓN|$)",
            @"(?:DOCUMENTACIÓN|DOCUMENTO)[\s:]+(.+?)(?=\n|BLOQUEO|DESBLOQUEO|TRANSFERENCIA|INFORMACIÓN|$)",
            @"(?:INFORMACIÓN|INFORMAR)[\s:]+(.+?)(?=\n|BLOQUEO|DESBLOQUEO|TRANSFERENCIA|DOCUMENTACIÓN|$)"
        };

        foreach (var pattern in requirementPatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    requirements.Add(new ComplianceRequirement
                    {
                        RequerimientoId = $"REQ-{requirementId++}",
                        Descripcion = match.Groups[1].Value.Trim(),
                        Tipo = DetermineRequirementType(match.Value),
                        EsObligatorio = true // Default to mandatory unless specified otherwise
                    });
                }
            }
        }

        // If no structured requirements found, extract sentences that might be requirements
        if (requirements.Count == 0)
        {
            var sentences = text.Split(new[] { '.', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (trimmed.Length > 20 && trimmed.Length < 500)
                {
                    // Check if sentence contains requirement keywords
                    if (ContainsRequirementKeywords(trimmed))
                    {
                        requirements.Add(new ComplianceRequirement
                        {
                            RequerimientoId = $"REQ-{requirementId++}",
                            Descripcion = trimmed,
                            Tipo = DetermineRequirementType(trimmed),
                            EsObligatorio = true
                        });
                    }
                }
            }
        }

        return requirements;
    }

    private static bool ContainsRequirementKeywords(string text)
    {
        var keywords = new[]
        {
            "bloqueo", "bloquear", "desbloqueo", "desbloquear",
            "transferencia", "transferir", "documentación", "documento",
            "información", "informar", "solicitar", "requerir",
            "proporcionar", "presentar", "entregar"
        };

        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string DetermineRequirementType(string text)
    {
        var lowerText = text.ToLowerInvariant();

        // Check desbloqueo BEFORE bloqueo since "desbloqueo" contains "bloqueo" as substring
        if (lowerText.Contains("desbloqueo") || lowerText.Contains("desbloquear"))
        {
            return "desbloqueo";
        }

        if (lowerText.Contains("bloqueo") || lowerText.Contains("bloquear"))
        {
            return "bloqueo";
        }

        if (lowerText.Contains("transferencia") || lowerText.Contains("transferir"))
        {
            return "transferencia";
        }

        if (lowerText.Contains("documentación") || lowerText.Contains("documento"))
        {
            return "documentacion";
        }

        if (lowerText.Contains("información") || lowerText.Contains("informar"))
        {
            return "informacion";
        }

        return "general";
    }

    private static string CategorizeRequirement(ComplianceRequirement requirement, string fullText)
    {
        // Use the requirement type if already determined
        if (!string.IsNullOrWhiteSpace(requirement.Tipo) && requirement.Tipo != "general")
        {
            return requirement.Tipo;
        }

        // Analyze requirement description for category keywords
        var description = requirement.Descripcion.ToLowerInvariant();

        // Check desbloqueo BEFORE bloqueo since "desbloqueo" contains "bloqueo" as substring
        // Desbloqueo keywords
        if (description.Contains("desbloqueo") || description.Contains("desbloquear") ||
            description.Contains("descongelar") || description.Contains("liberar"))
        {
            return "desbloqueo";
        }

        // Bloqueo keywords
        if (description.Contains("bloqueo") || description.Contains("bloquear") ||
            description.Contains("congelar") || description.Contains("inmovilizar"))
        {
            return "bloqueo";
        }

        // Transferencia keywords
        if (description.Contains("transferencia") || description.Contains("transferir") ||
            description.Contains("movimiento") || description.Contains("movilizar"))
        {
            return "transferencia";
        }

        // Documentación keywords
        if (description.Contains("documentación") || description.Contains("documento") ||
            description.Contains("presentar") || description.Contains("entregar") ||
            description.Contains("proporcionar") || description.Contains("adjuntar"))
        {
            return "documentacion";
        }

        // Información keywords
        if (description.Contains("información") || description.Contains("informar") ||
            description.Contains("reportar") || description.Contains("comunicar"))
        {
            return "informacion";
        }

        // Default to general if no category matches
        return "general";
    }

    private static string GenerateSummaryText(RequirementSummary summary)
    {
        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine("Resumen de Requerimientos de Cumplimiento");
        summaryBuilder.AppendLine($"Fecha de extracción: {summary.ExtractedAt:yyyy-MM-dd HH:mm:ss}");
        summaryBuilder.AppendLine();

        if (summary.Bloqueo.Count > 0)
        {
            summaryBuilder.AppendLine($"Bloqueos ({summary.Bloqueo.Count}):");
            foreach (var req in summary.Bloqueo)
            {
                summaryBuilder.AppendLine($"  - {req.Descripcion}");
            }
            summaryBuilder.AppendLine();
        }

        if (summary.Desbloqueo.Count > 0)
        {
            summaryBuilder.AppendLine($"Desbloqueos ({summary.Desbloqueo.Count}):");
            foreach (var req in summary.Desbloqueo)
            {
                summaryBuilder.AppendLine($"  - {req.Descripcion}");
            }
            summaryBuilder.AppendLine();
        }

        if (summary.Documentacion.Count > 0)
        {
            summaryBuilder.AppendLine($"Documentación ({summary.Documentacion.Count}):");
            foreach (var req in summary.Documentacion)
            {
                summaryBuilder.AppendLine($"  - {req.Descripcion}");
            }
            summaryBuilder.AppendLine();
        }

        if (summary.Transferencia.Count > 0)
        {
            summaryBuilder.AppendLine($"Transferencias ({summary.Transferencia.Count}):");
            foreach (var req in summary.Transferencia)
            {
                summaryBuilder.AppendLine($"  - {req.Descripcion}");
            }
            summaryBuilder.AppendLine();
        }

        if (summary.Informacion.Count > 0)
        {
            summaryBuilder.AppendLine($"Información ({summary.Informacion.Count}):");
            foreach (var req in summary.Informacion)
            {
                summaryBuilder.AppendLine($"  - {req.Descripcion}");
            }
            summaryBuilder.AppendLine();
        }

        if (summary.Requirements.Count > 0 && summary.Requirements.Count > 
            summary.Bloqueo.Count + summary.Desbloqueo.Count + summary.Documentacion.Count + 
            summary.Transferencia.Count + summary.Informacion.Count)
        {
            summaryBuilder.AppendLine($"Otros requerimientos ({summary.Requirements.Count}):");
            foreach (var req in summary.Requirements)
            {
                summaryBuilder.AppendLine($"  - {req.Descripcion}");
            }
        }

        return summaryBuilder.ToString();
    }

    private static int CalculateConfidenceScore(RequirementSummary summary)
    {
        // Calculate confidence based on categorization success
        var totalRequirements = summary.Requirements.Count;
        if (totalRequirements == 0)
        {
            return 0;
        }

        var categorizedCount = summary.Bloqueo.Count + summary.Desbloqueo.Count +
                              summary.Documentacion.Count + summary.Transferencia.Count +
                              summary.Informacion.Count;

        // Confidence is higher if more requirements were successfully categorized
        var categorizationRate = categorizedCount / (double)totalRequirements;
        var baseConfidence = (int)(categorizationRate * 100);

        // Adjust based on requirement count (more requirements = higher confidence in extraction)
        var countBonus = Math.Min(totalRequirements * 2, 20);

        return Math.Min(baseConfidence + countBonus, 100);
    }
}

