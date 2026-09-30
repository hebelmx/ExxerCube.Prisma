using System.Collections.Concurrent;
using System.Text.Json;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Repositories;

/// <summary>
/// In-memory repository for storing and querying OCR processing sessions.
/// Used for data collection, analysis, and model retraining.
/// Thread-safe implementation using ConcurrentDictionary.
/// </summary>
public class OcrSessionRepository : IOcrSessionRepository
{
    private readonly ConcurrentDictionary<Guid, OcrSession> _sessions = new();
    private readonly ILogger<OcrSessionRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrSessionRepository"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OcrSessionRepository(ILogger<OcrSessionRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<OcrSession>> StoreSessionAsync(OcrSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            if (session.Id == Guid.Empty)
            {
                session.Id = Guid.NewGuid();
            }

            if (_sessions.TryAdd(session.Id, session))
            {
                _logger.LogInformation("Stored OCR session {SessionId} with hash {ImageHash}", session.Id, session.ImageHash);
                return Task.FromResult(Result<OcrSession>.Success(session));
            }

            return Task.FromResult(Result<OcrSession>.WithFailure($"Session with ID {session.Id} already exists"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store OCR session");
            return Task.FromResult(Result<OcrSession>.WithFailure($"Failed to store session: {ex.Message}", default, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<OcrSession>> GetSessionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_sessions.TryGetValue(id, out var session))
            {
                _logger.LogDebug("Retrieved OCR session {SessionId}", id);
                return Task.FromResult(Result<OcrSession>.Success(session));
            }

            return Task.FromResult(Result<OcrSession>.WithFailure($"Session with ID {id} not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OCR session {SessionId}", id);
            return Task.FromResult(Result<OcrSession>.WithFailure($"Failed to get session: {ex.Message}", default, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<OcrSession>>> GetUnreviewedSessionsAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var unreviewed = _sessions.Values
                .Where(s => !s.IsReviewed)
                .OrderByDescending(s => s.ProcessedAt)
                .Skip(skip)
                .Take(take)
                .ToList();

            _logger.LogInformation("Retrieved {Count} unreviewed sessions (skip={Skip}, take={Take})",
                unreviewed.Count, skip, take);

            return Task.FromResult(Result<IEnumerable<OcrSession>>.Success(unreviewed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unreviewed sessions");
            return Task.FromResult(Result<IEnumerable<OcrSession>>.WithFailure($"Failed to get unreviewed sessions: {ex.Message}", default!, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<OcrSession>>> GetTrainingDataAsync(
        int minQualityRating = 3,
        string? modelVersion = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _sessions.Values
                .Where(s => s.IsReviewed &&
                           s.IncludeInTraining &&
                           s.QualityRating.HasValue &&
                           s.QualityRating.Value >= minQualityRating &&
                           !string.IsNullOrWhiteSpace(s.GroundTruth));

            if (!string.IsNullOrWhiteSpace(modelVersion))
            {
                query = query.Where(s => s.ModelVersion == modelVersion);
            }

            var trainingData = query.OrderByDescending(s => s.QualityRating).ToList();

            _logger.LogInformation("Retrieved {Count} training-ready sessions (minQuality={MinQuality}, model={Model})",
                trainingData.Count, minQualityRating, modelVersion ?? "all");

            return Task.FromResult(Result<IEnumerable<OcrSession>>.Success(trainingData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get training data");
            return Task.FromResult(Result<IEnumerable<OcrSession>>.WithFailure($"Failed to get training data: {ex.Message}", default, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<OcrSession>> UpdateReviewAsync(
        Guid sessionId,
        string groundTruth,
        int qualityRating,
        string? reviewNotes = null,
        string? reviewedBy = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<OcrSession>.WithFailure($"Session with ID {sessionId} not found"));
            }

            if (qualityRating < 1 || qualityRating > 5)
            {
                return Task.FromResult(Result<OcrSession>.WithFailure("Quality rating must be between 1 and 5"));
            }

            session.GroundTruth = groundTruth;
            session.QualityRating = qualityRating;
            session.ReviewNotes = reviewNotes;
            session.ReviewedBy = reviewedBy;
            session.IsReviewed = true;
            session.ReviewedAt = DateTime.UtcNow;

            // Calculate Levenshtein distances
            if (!string.IsNullOrEmpty(session.BaselineOcrText))
            {
                session.BaselineLevenshteinDistance = CalculateLevenshteinDistance(session.BaselineOcrText, groundTruth);
            }

            if (!string.IsNullOrEmpty(session.EnhancedOcrText))
            {
                session.EnhancedLevenshteinDistance = CalculateLevenshteinDistance(session.EnhancedOcrText, groundTruth);
            }

            // Calculate improvement percentage
            if (session.BaselineLevenshteinDistance.HasValue && session.EnhancedLevenshteinDistance.HasValue)
            {
                var baseline = session.BaselineLevenshteinDistance.Value;
                var enhanced = session.EnhancedLevenshteinDistance.Value;

                if (baseline > 0)
                {
                    session.ImprovementPercent = ((baseline - enhanced) / (double)baseline) * 100.0;
                }
                else
                {
                    session.ImprovementPercent = 0.0;
                }
            }

            _logger.LogInformation("Updated review for session {SessionId} - Quality: {Quality}, Improvement: {Improvement:F2}%",
                sessionId, qualityRating, session.ImprovementPercent ?? 0);

            return Task.FromResult(Result<OcrSession>.Success(session));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update review for session {SessionId}", sessionId);
            return Task.FromResult(Result<OcrSession>.WithFailure($"Failed to update review: {ex.Message}", default, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<OcrSession>> UpdateOptimalParametersAsync(
        Guid sessionId,
        PolynomialFilterParams optimalParams,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<OcrSession>.WithFailure($"Session with ID {sessionId} not found"));
            }

            session.OptimalContrast = optimalParams.Contrast;
            session.OptimalBrightness = optimalParams.Brightness;
            session.OptimalSharpness = optimalParams.Sharpness;
            session.OptimalUnsharpRadius = optimalParams.UnsharpRadius;
            session.OptimalUnsharpPercent = optimalParams.UnsharpPercent;

            _logger.LogInformation("Updated optimal parameters for session {SessionId}: {Params}",
                sessionId, optimalParams.ToString());

            return Task.FromResult(Result<OcrSession>.Success(session));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update optimal parameters for session {SessionId}", sessionId);
            return Task.FromResult(Result<OcrSession>.WithFailure($"Failed to update optimal parameters: {ex.Message}", default, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<OcrSessionStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allSessions = _sessions.Values.ToList();
            var reviewedSessions = allSessions.Where(s => s.IsReviewed).ToList();
            var trainingReady = reviewedSessions.Where(s => s.IncludeInTraining && s.QualityRating >= 3).ToList();

            var statistics = new OcrSessionStatistics
            {
                TotalSessions = allSessions.Count,
                ReviewedSessions = reviewedSessions.Count,
                TrainingReadySessions = trainingReady.Count,
                AverageImprovementPercent = reviewedSessions
                    .Where(s => s.ImprovementPercent.HasValue)
                    .Select(s => s.ImprovementPercent!.Value)
                    .DefaultIfEmpty(0)
                    .Average(),
                AverageQualityRating = reviewedSessions
                    .Where(s => s.QualityRating.HasValue)
                    .Select(s => (double)s.QualityRating!.Value)
                    .DefaultIfEmpty(0)
                    .Average(),
                SessionsByQualityLevel = allSessions
                    .GroupBy(s => s.QualityLevel)
                    .ToDictionary(g => g.Key, g => g.Count()),
                SessionsByFilterType = allSessions
                    .GroupBy(s => s.FilterType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                SessionsByModelVersion = allSessions
                    .GroupBy(s => s.ModelVersion)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            _logger.LogInformation("Generated statistics: {Total} total, {Reviewed} reviewed, {Training} training-ready",
                statistics.TotalSessions, statistics.ReviewedSessions, statistics.TrainingReadySessions);

            return Task.FromResult(Result<OcrSessionStatistics>.Success(statistics));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics");
            return Task.FromResult(Result<OcrSessionStatistics>.WithFailure($"Failed to get statistics: {ex.Message}", default, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<int>> ExportTrainingDataAsync(
        string outputPath,
        string format = "json",
        int minQualityRating = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var trainingDataResult = GetTrainingDataAsync(minQualityRating, null, cancellationToken).Result;

            if (!trainingDataResult.IsSuccess)
            {
                return Task.FromResult(Result<int>.WithFailure(trainingDataResult.Errors.FirstOrDefault() ?? "Failed to get training data"));
            }

            var trainingData = trainingDataResult.Value!.ToList();

            if (format.ToLowerInvariant() == "json")
            {
                var json = JsonSerializer.Serialize(trainingData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(outputPath, json);
            }
            else if (format.ToLowerInvariant() == "csv")
            {
                var csv = ConvertToCsv(trainingData);
                File.WriteAllText(outputPath, csv);
            }
            else
            {
                return Task.FromResult(Result<int>.WithFailure($"Unsupported format: {format}. Use 'json' or 'csv'"));
            }

            _logger.LogInformation("Exported {Count} training sessions to {Path} ({Format})",
                trainingData.Count, outputPath, format);

            return Task.FromResult(Result<int>.Success(trainingData.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export training data to {Path}", outputPath);
            return Task.FromResult(Result<int>.WithFailure($"Failed to export training data: {ex.Message}", default, ex));
        }
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    /// <param name="source">Source string.</param>
    /// <param name="target">Target string.</param>
    /// <returns>Levenshtein distance (minimum number of edits required).</returns>
    private static int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.IsNullOrEmpty(target) ? 0 : target.Length;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        var sourceLength = source.Length;
        var targetLength = target.Length;
        var distance = new int[sourceLength + 1, targetLength + 1];

        // Initialize first column and row
        for (var i = 0; i <= sourceLength; i++)
        {
            distance[i, 0] = i;
        }

        for (var j = 0; j <= targetLength; j++)
        {
            distance[0, j] = j;
        }

        // Calculate distances
        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,      // deletion
                        distance[i, j - 1] + 1),     // insertion
                    distance[i - 1, j - 1] + cost);  // substitution
            }
        }

        return distance[sourceLength, targetLength];
    }

    /// <summary>
    /// Converts OCR sessions to CSV format.
    /// </summary>
    /// <param name="sessions">Sessions to convert.</param>
    /// <returns>CSV string.</returns>
    private static string ConvertToCsv(List<OcrSession> sessions)
    {
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine("Id,ProcessedAt,ImageHash,QualityLevel,FilterType,ModelVersion," +
                     "BlurScore,Contrast,NoiseEstimate,EdgeDensity," +
                     "PredictedContrast,PredictedBrightness,PredictedSharpness,PredictedUnsharpRadius,PredictedUnsharpPercent," +
                     "OptimalContrast,OptimalBrightness,OptimalSharpness,OptimalUnsharpRadius,OptimalUnsharpPercent," +
                     "BaselineLevenshteinDistance,EnhancedLevenshteinDistance,ImprovementPercent," +
                     "QualityRating,ReviewedBy");

        // CSV Data
        foreach (var session in sessions)
        {
            sb.AppendLine($"{session.Id},{session.ProcessedAt:O},{EscapeCsv(session.ImageHash)}," +
                         $"{EscapeCsv(session.QualityLevel)},{EscapeCsv(session.FilterType)},{EscapeCsv(session.ModelVersion)}," +
                         $"{session.BlurScore},{session.Contrast},{session.NoiseEstimate},{session.EdgeDensity}," +
                         $"{session.PredictedContrast},{session.PredictedBrightness},{session.PredictedSharpness}," +
                         $"{session.PredictedUnsharpRadius},{session.PredictedUnsharpPercent}," +
                         $"{session.OptimalContrast},{session.OptimalBrightness},{session.OptimalSharpness}," +
                         $"{session.OptimalUnsharpRadius},{session.OptimalUnsharpPercent}," +
                         $"{session.BaselineLevenshteinDistance},{session.EnhancedLevenshteinDistance},{session.ImprovementPercent}," +
                         $"{session.QualityRating},{EscapeCsv(session.ReviewedBy ?? string.Empty)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes CSV field values.
    /// </summary>
    /// <param name="value">Value to escape.</param>
    /// <returns>Escaped value.</returns>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
