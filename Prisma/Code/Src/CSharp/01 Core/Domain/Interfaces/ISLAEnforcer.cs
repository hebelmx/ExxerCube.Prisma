namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Defines the SLA enforcer service for tracking SLA deadlines and managing escalations.
/// </summary>
public interface ISLAEnforcer
{
    /// <summary>
    /// Calculates and tracks SLA status for a file based on intake date and days plazo (business days).
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="intakeDate">The date when the file was received.</param>
    /// <param name="daysPlazo">The number of business days granted for compliance.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the calculated SLA status or an error.</returns>
    Task<Result<SLAStatus>> CalculateSLAStatusAsync(
        string fileId,
        DateTime intakeDate,
        int daysPlazo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates SLA status for a file, recalculating deadline and escalation level.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the updated SLA status or an error.</returns>
    Task<Result<SLAStatus>> UpdateSLAStatusAsync(
        string fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets SLA status for a specific file.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the SLA status or null if not found, or an error.</returns>
    Task<Result<SLAStatus?>> GetSLAStatusAsync(
        string fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all SLA statuses that are at risk (within critical threshold).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of at-risk SLA statuses or an error.</returns>
    Task<Result<List<SLAStatus>>> GetAtRiskCasesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all SLA statuses that have breached their deadline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of breached SLA statuses or an error.</returns>
    Task<Result<List<SLAStatus>>> GetBreachedCasesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active SLA statuses (not breached).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of active SLA statuses or an error.</returns>
    Task<Result<List<SLAStatus>>> GetActiveCasesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Escalates a case to the specified escalation level and triggers notifications.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="escalationLevel">The escalation level to set.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> EscalateCaseAsync(
        string fileId,
        EscalationLevel escalationLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates business days between two dates, accounting for weekends and optionally Mexican holidays.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the number of business days or an error.</returns>
    Task<Result<int>> CalculateBusinessDaysAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

