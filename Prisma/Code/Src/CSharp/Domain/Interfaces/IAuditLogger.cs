namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the audit logger service for logging all processing steps with correlation IDs.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit record for a processing action.
    /// </summary>
    /// <param name="actionType">The type of action being logged.</param>
    /// <param name="stage">The processing stage where the action occurred.</param>
    /// <param name="fileId">The file identifier (nullable if not applicable).</param>
    /// <param name="correlationId">The correlation ID for tracking requests across stages.</param>
    /// <param name="userId">The user ID who performed the action (nullable for system actions).</param>
    /// <param name="actionDetails">JSON serialized action details (nullable).</param>
    /// <param name="success">Whether the action succeeded.</param>
    /// <param name="errorMessage">Error message if action failed (nullable).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> LogAuditAsync(
        AuditActionType actionType,
        ProcessingStage stage,
        string? fileId,
        string correlationId,
        string? userId,
        string? actionDetails,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit records filtered by file ID.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of audit records or an error.</returns>
    Task<Result<List<AuditRecord>>> GetAuditRecordsByFileIdAsync(
        string fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit records filtered by correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of audit records or an error.</returns>
    Task<Result<List<AuditRecord>>> GetAuditRecordsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit records filtered by date range, action type, and user.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="actionType">The action type filter (nullable for all types).</param>
    /// <param name="userId">The user ID filter (nullable for all users).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of audit records or an error.</returns>
    Task<Result<List<AuditRecord>>> GetAuditRecordsAsync(
        DateTime startDate,
        DateTime endDate,
        AuditActionType? actionType,
        string? userId,
        CancellationToken cancellationToken = default);
}

