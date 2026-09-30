using System.Text.Json;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Service for generating audit reports in CSV and JSON formats.
/// </summary>
public class AuditReportingService
{
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AuditReportingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditReportingService"/> class.
    /// </summary>
    /// <param name="auditLogger">The audit logger service.</param>
    /// <param name="logger">The logger instance.</param>
    public AuditReportingService(
        IAuditLogger auditLogger,
        ILogger<AuditReportingService> logger)
    {
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a classification report in CSV format for the specified date range.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the CSV report content or an error.</returns>
    public async Task<Result<string>> GenerateClassificationReportCsvAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Classification report generation cancelled before starting");
            return ResultExtensions.Cancelled<string>();
        }

        if (endDate < startDate)
        {
            return Result<string>.WithFailure("EndDate must be greater than or equal to StartDate");
        }

        try
        {
            // Get classification audit records
            var recordsResult = await _auditLogger.GetAuditRecordsAsync(
                startDate,
                endDate,
                AuditActionType.Classification,
                null,
                cancellationToken).ConfigureAwait(false);

            if (recordsResult.IsCancelled())
            {
                _logger.LogWarning("Classification report generation cancelled while retrieving records");
                return ResultExtensions.Cancelled<string>();
            }

            if (recordsResult.IsFailure)
            {
                return Result<string>.WithFailure($"Failed to retrieve audit records: {recordsResult.Error}");
            }

            if (recordsResult.Value == null)
            {
                return Result<string>.WithFailure("No audit records returned");
            }

            var records = recordsResult.Value;

            // Generate CSV content
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,FileId,CorrelationId,Stage,Success,ActionDetails,ErrorMessage");

            foreach (var record in records.OrderBy(r => r.Timestamp))
            {
                csv.AppendLine($"{record.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                    $"{EscapeCsvField(record.FileId ?? string.Empty)}," +
                    $"{EscapeCsvField(record.CorrelationId)}," +
                    $"{record.Stage}," +
                    $"{record.Success}," +
                    $"{EscapeCsvField(record.ActionDetails ?? string.Empty)}," +
                    $"{EscapeCsvField(record.ErrorMessage ?? string.Empty)}");
            }

            _logger.LogInformation("Generated classification report CSV: {RecordCount} records, period: {StartDate} to {EndDate}",
                records.Count, startDate, endDate);

            return Result<string>.Success(csv.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Classification report generation cancelled");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating classification report CSV");
            return Result<string>.WithFailure(
                error: $"Error generating classification report: {ex.Message}",
                value: default,
                exception: ex);
        }
    }

    /// <summary>
    /// Generates a classification report in JSON format for the specified date range.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the JSON report content or an error.</returns>
    public async Task<Result<string>> GenerateClassificationReportJsonAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Classification report generation cancelled before starting");
            return ResultExtensions.Cancelled<string>();
        }

        if (endDate < startDate)
        {
            return Result<string>.WithFailure("EndDate must be greater than or equal to StartDate");
        }

        try
        {
            // Get classification audit records
            var recordsResult = await _auditLogger.GetAuditRecordsAsync(
                startDate,
                endDate,
                AuditActionType.Classification,
                null,
                cancellationToken).ConfigureAwait(false);

            if (recordsResult.IsCancelled())
            {
                _logger.LogWarning("Classification report generation cancelled while retrieving records");
                return ResultExtensions.Cancelled<string>();
            }

            if (recordsResult.IsFailure)
            {
                return Result<string>.WithFailure($"Failed to retrieve audit records: {recordsResult.Error}");
            }

            if (recordsResult.Value == null)
            {
                return Result<string>.WithFailure("No audit records returned");
            }

            var records = recordsResult.Value;

            // Generate JSON content
            var report = new
            {
                StartDate = startDate,
                EndDate = endDate,
                RecordCount = records.Count,
                Records = records.OrderBy(r => r.Timestamp).Select(r => new
                {
                    r.AuditId,
                    r.Timestamp,
                    r.FileId,
                    r.CorrelationId,
                    r.Stage,
                    r.Success,
                    r.ActionDetails,
                    r.ErrorMessage
                })
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Generated classification report JSON: {RecordCount} records, period: {StartDate} to {EndDate}",
                records.Count, startDate, endDate);

            return Result<string>.Success(json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Classification report generation cancelled");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating classification report JSON");
            return Result<string>.WithFailure(
                error: $"Error generating classification report: {ex.Message}",
                value: default,
                exception: ex);
        }
    }

    /// <summary>
    /// Exports audit log to CSV format for compliance reporting.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="actionType">The action type filter (nullable for all types).</param>
    /// <param name="userId">The user ID filter (nullable for all users).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the CSV export content or an error.</returns>
    public async Task<Result<string>> ExportAuditLogCsvAsync(
        DateTime startDate,
        DateTime endDate,
        AuditActionType? actionType,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Audit log export cancelled before starting");
            return ResultExtensions.Cancelled<string>();
        }

        if (endDate < startDate)
        {
            return Result<string>.WithFailure("EndDate must be greater than or equal to StartDate");
        }

        try
        {
            var recordsResult = await _auditLogger.GetAuditRecordsAsync(
                startDate,
                endDate,
                actionType,
                userId,
                cancellationToken).ConfigureAwait(false);

            if (recordsResult.IsCancelled())
            {
                _logger.LogWarning("Audit log export cancelled while retrieving records");
                return ResultExtensions.Cancelled<string>();
            }

            if (recordsResult.IsFailure)
            {
                return Result<string>.WithFailure($"Failed to retrieve audit records: {recordsResult.Error}");
            }

            if (recordsResult.Value == null)
            {
                return Result<string>.WithFailure("No audit records returned");
            }

            var records = recordsResult.Value;

            // Generate CSV content
            var csv = new StringBuilder();
            csv.AppendLine("AuditId,Timestamp,CorrelationId,FileId,ActionType,Stage,UserId,Success,ActionDetails,ErrorMessage");

            foreach (var record in records.OrderBy(r => r.Timestamp))
            {
                csv.AppendLine($"{record.AuditId}," +
                    $"{record.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                    $"{EscapeCsvField(record.CorrelationId)}," +
                    $"{EscapeCsvField(record.FileId ?? string.Empty)}," +
                    $"{record.ActionType}," +
                    $"{record.Stage}," +
                    $"{EscapeCsvField(record.UserId ?? string.Empty)}," +
                    $"{record.Success}," +
                    $"{EscapeCsvField(record.ActionDetails ?? string.Empty)}," +
                    $"{EscapeCsvField(record.ErrorMessage ?? string.Empty)}");
            }

            _logger.LogInformation("Exported audit log CSV: {RecordCount} records, period: {StartDate} to {EndDate}",
                records.Count, startDate, endDate);

            return Result<string>.Success(csv.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Audit log export cancelled");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit log CSV");
            return Result<string>.WithFailure(
                error: $"Error exporting audit log: {ex.Message}",
                value: default,
                exception: ex);
        }
    }

    /// <summary>
    /// Exports audit log to JSON format for compliance reporting.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="actionType">The action type filter (nullable for all types).</param>
    /// <param name="userId">The user ID filter (nullable for all users).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the JSON export content or an error.</returns>
    public async Task<Result<string>> ExportAuditLogJsonAsync(
        DateTime startDate,
        DateTime endDate,
        AuditActionType? actionType,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Audit log export cancelled before starting");
            return ResultExtensions.Cancelled<string>();
        }

        if (endDate < startDate)
        {
            return Result<string>.WithFailure("EndDate must be greater than or equal to StartDate");
        }

        try
        {
            var recordsResult = await _auditLogger.GetAuditRecordsAsync(
                startDate,
                endDate,
                actionType,
                userId,
                cancellationToken).ConfigureAwait(false);

            if (recordsResult.IsCancelled())
            {
                _logger.LogWarning("Audit log export cancelled while retrieving records");
                return ResultExtensions.Cancelled<string>();
            }

            if (recordsResult.IsFailure)
            {
                return Result<string>.WithFailure($"Failed to retrieve audit records: {recordsResult.Error}");
            }

            if (recordsResult.Value == null)
            {
                return Result<string>.WithFailure("No audit records returned");
            }

            var records = recordsResult.Value;

            // Generate JSON content
            var export = new
            {
                StartDate = startDate,
                EndDate = endDate,
                ActionType = actionType?.ToString(),
                UserId = userId,
                RecordCount = records.Count,
                Records = records.OrderBy(r => r.Timestamp).Select(r => new
                {
                    r.AuditId,
                    r.Timestamp,
                    r.CorrelationId,
                    r.FileId,
                    ActionType = r.ActionType.ToString(),
                    Stage = r.Stage.ToString(),
                    r.UserId,
                    r.Success,
                    r.ActionDetails,
                    r.ErrorMessage
                })
            };

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Exported audit log JSON: {RecordCount} records, period: {StartDate} to {EndDate}",
                records.Count, startDate, endDate);

            return Result<string>.Success(json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Audit log export cancelled");
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit log JSON");
            return Result<string>.WithFailure(
                error: $"Error exporting audit log: {ex.Message}",
                value: default,
                exception: ex);
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        // Escape quotes and wrap in quotes if contains comma, newline, or quote
        if (field.Contains(',') || field.Contains('\n') || field.Contains('"'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}

