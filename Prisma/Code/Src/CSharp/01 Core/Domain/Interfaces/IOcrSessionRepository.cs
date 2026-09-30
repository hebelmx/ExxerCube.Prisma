using ExxerCube.Prisma.Domain.Models;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Repository for storing and querying OCR processing sessions.
/// Used for data collection, analysis, and model retraining.
/// </summary>
public interface IOcrSessionRepository
{
    /// <summary>
    /// Stores a complete OCR processing session.
    /// </summary>
    /// <param name="session">The OCR session to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored session with ID populated.</returns>
    Task<Result<OcrSession>> StoreSessionAsync(OcrSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an OCR session by ID.
    /// </summary>
    /// <param name="id">Session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCR session if found.</returns>
    Task<Result<OcrSession>> GetSessionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sessions that need manual review.
    /// </summary>
    /// <param name="skip">Number of sessions to skip (pagination).</param>
    /// <param name="take">Number of sessions to take (page size).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of unreviewed sessions.</returns>
    Task<Result<IEnumerable<OcrSession>>> GetUnreviewedSessionsAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sessions ready for training (reviewed with ground truth).
    /// </summary>
    /// <param name="minQualityRating">Minimum quality rating (1-5).</param>
    /// <param name="modelVersion">Filter by model version (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of training-ready sessions.</returns>
    Task<Result<IEnumerable<OcrSession>>> GetTrainingDataAsync(
        int minQualityRating = 3,
        string? modelVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an OCR session with review data.
    /// </summary>
    /// <param name="sessionId">Session ID to update.</param>
    /// <param name="groundTruth">Corrected ground truth text.</param>
    /// <param name="qualityRating">Quality rating (1-5).</param>
    /// <param name="reviewNotes">Optional review notes.</param>
    /// <param name="reviewedBy">Reviewer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated session.</returns>
    Task<Result<OcrSession>> UpdateReviewAsync(
        Guid sessionId,
        string groundTruth,
        int qualityRating,
        string? reviewNotes = null,
        string? reviewedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates optimal parameters for a session (from manual tuning or grid search).
    /// </summary>
    /// <param name="sessionId">Session ID to update.</param>
    /// <param name="optimalParams">Optimal filter parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated session.</returns>
    Task<Result<OcrSession>> UpdateOptimalParametersAsync(
        Guid sessionId,
        PolynomialFilterParams optimalParams,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about collected sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session statistics.</returns>
    Task<Result<OcrSessionStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports training data to CSV/JSON for Python retraining.
    /// </summary>
    /// <param name="outputPath">Path to export file.</param>
    /// <param name="format">Export format (csv or json).</param>
    /// <param name="minQualityRating">Minimum quality rating.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of sessions exported.</returns>
    Task<Result<int>> ExportTrainingDataAsync(
        string outputPath,
        string format = "json",
        int minQualityRating = 3,
        CancellationToken cancellationToken = default);
}