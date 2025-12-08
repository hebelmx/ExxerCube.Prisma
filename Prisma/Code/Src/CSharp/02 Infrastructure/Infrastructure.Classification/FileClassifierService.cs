using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Service for classifying documents into regulatory categories using deterministic rule-based classification.
/// </summary>
public class FileClassifierService : IFileClassifier
{
    private readonly ILogger<FileClassifierService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileClassifierService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public FileClassifierService(ILogger<FileClassifierService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ClassificationResult>> ClassifyAsync(
        ExtractedMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Classifying document based on metadata");

            var scores = new ClassificationScores();
            var expediente = metadata.Expediente;
            var areaDescripcion = expediente?.AreaDescripcion ?? string.Empty;
            var numeroExpediente = expediente?.NumeroExpediente ?? string.Empty;
            var legalReferences = metadata.LegalReferences ?? Array.Empty<string>();
            var allText = string.Join(" ", legalReferences);

            // Level 1 Classification - Deterministic rules based on keywords and patterns
            ClassifyLevel1(areaDescripcion, numeroExpediente, allText, scores);

            // Level 2 Classification - Subcategories based on metadata
            var level2 = ClassifyLevel2(areaDescripcion, numeroExpediente, allText);

            // Calculate overall confidence (average of all scores)
            var confidence = CalculateConfidence(scores);

            // Determine Level 1 category (highest score)
            var level1 = DetermineLevel1Category(scores);

            var result = new ClassificationResult
            {
                Level1 = level1,
                Level2 = level2,
                Scores = scores,
                Confidence = confidence
            };

            _logger.LogDebug("Document classified as {Level1}/{Level2} with confidence {Confidence}%", level1, level2, confidence);
            return Task.FromResult(Result<ClassificationResult>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying document");
            return Task.FromResult(Result<ClassificationResult>.WithFailure($"Error classifying document: {ex.Message}", default(ClassificationResult), ex));
        }
    }

    private static void ClassifyLevel1(string areaDescripcion, string numeroExpediente, string allText, ClassificationScores scores)
    {
        var combinedText = $"{areaDescripcion} {numeroExpediente} {allText}".ToUpperInvariant();

        // Aseguramiento (Asset Seizure)
        if (combinedText.Contains("ASEGURAMIENTO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("EMBARGO", StringComparison.OrdinalIgnoreCase) ||
            numeroExpediente.Contains("/AS", StringComparison.OrdinalIgnoreCase))
        {
            scores.AseguramientoScore = 90;
        }
        else if (combinedText.Contains("ASEGURAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.AseguramientoScore = 70;
        }
        else
        {
            scores.AseguramientoScore = 10;
        }

        // Desembargo (Asset Release)
        if (combinedText.Contains("DESEMBARGO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("LIBERAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.DesembargoScore = 90;
        }
        else if (combinedText.Contains("DESEMBARGAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.DesembargoScore = 70;
        }
        else
        {
            scores.DesembargoScore = 10;
        }

        // Documentacion (Documentation Request)
        if (combinedText.Contains("DOCUMENTACION", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("DOCUMENTO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SOLICITUD DOCUMENTAL", StringComparison.OrdinalIgnoreCase))
        {
            scores.DocumentacionScore = 90;
        }
        else if (combinedText.Contains("DOCUMENTAR", StringComparison.OrdinalIgnoreCase))
        {
            scores.DocumentacionScore = 70;
        }
        else
        {
            scores.DocumentacionScore = 10;
        }

        // Informacion (Information Request)
        if (combinedText.Contains("INFORMACION", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("INFORMAR", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("REPORTE", StringComparison.OrdinalIgnoreCase))
        {
            scores.InformacionScore = 90;
        }
        else if (combinedText.Contains("INFORMATIVO", StringComparison.OrdinalIgnoreCase))
        {
            scores.InformacionScore = 70;
        }
        else
        {
            scores.InformacionScore = 10;
        }

        // Transferencia (Transfer)
        if (combinedText.Contains("TRANSFERENCIA", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("TRANSFERIR", StringComparison.OrdinalIgnoreCase))
        {
            scores.TransferenciaScore = 90;
        }
        else if (combinedText.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase))
        {
            scores.TransferenciaScore = 70;
        }
        else
        {
            scores.TransferenciaScore = 10;
        }

        // OperacionesIlicitas (Illicit Operations)
        if (combinedText.Contains("OPERACIONES ILICITAS", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("LAVADO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("FINANCIAMIENTO TERRORISMO", StringComparison.OrdinalIgnoreCase))
        {
            scores.OperacionesIlicitasScore = 90;
        }
        else if (combinedText.Contains("ILICITO", StringComparison.OrdinalIgnoreCase))
        {
            scores.OperacionesIlicitasScore = 70;
        }
        else
        {
            scores.OperacionesIlicitasScore = 10;
        }
    }

    private static ClassificationLevel2? ClassifyLevel2(string areaDescripcion, string numeroExpediente, string allText)
    {
        var combinedText = $"{areaDescripcion} {numeroExpediente} {allText}".ToUpperInvariant();

        // Especial (Special)
        if (combinedText.Contains("ESPECIAL", StringComparison.OrdinalIgnoreCase) ||
            numeroExpediente.Contains("/AS", StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationLevel2.Especial;
        }

        // Judicial (Judicial)
        if (combinedText.Contains("JUDICIAL", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("JUEZ", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("TRIBUNAL", StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationLevel2.Judicial;
        }

        // Hacendario (Tax-related)
        if (combinedText.Contains("HACENDARIO", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SAT", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SHCP", StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationLevel2.Hacendario;
        }

        return null;
    }

    private static int CalculateConfidence(ClassificationScores scores)
    {
        var scoresArray = new[]
        {
            scores.AseguramientoScore,
            scores.DesembargoScore,
            scores.DocumentacionScore,
            scores.InformacionScore,
            scores.TransferenciaScore,
            scores.OperacionesIlicitasScore
        };

        var maxScore = scoresArray.Max();
        var averageScore = (int)scoresArray.Average();

        // Confidence is based on how clear the classification is
        // If max score is very high and others are low, confidence is high
        var scoreDifference = maxScore - scoresArray.Where(s => s != maxScore).DefaultIfEmpty(0).Max();
        
        if (scoreDifference >= 60)
        {
            return Math.Min(100, maxScore);
        }
        else if (scoreDifference >= 40)
        {
            return Math.Min(85, maxScore);
        }
        else
        {
            return Math.Min(70, averageScore);
        }
    }

    private static ClassificationLevel1 DetermineLevel1Category(ClassificationScores scores)
    {
        var scoresDict = new System.Collections.Generic.Dictionary<ClassificationLevel1, int>
        {
            { ClassificationLevel1.Aseguramiento, scores.AseguramientoScore },
            { ClassificationLevel1.Desembargo, scores.DesembargoScore },
            { ClassificationLevel1.Documentacion, scores.DocumentacionScore },
            { ClassificationLevel1.Informacion, scores.InformacionScore },
            { ClassificationLevel1.Transferencia, scores.TransferenciaScore },
            { ClassificationLevel1.OperacionesIlicitas, scores.OperacionesIlicitasScore }
        };

        return scoresDict.OrderByDescending(kvp => kvp.Value).First().Key;
    }
}

