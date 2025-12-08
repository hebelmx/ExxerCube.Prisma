// <copyright file="DemoAdminService.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Service for demo administration tasks - DATABASE CLEANUP ONLY FOR DEMO ENVIRONMENTS!
/// WARNING: This service performs HARD DELETES. NEVER use in production!
/// </summary>
public class DemoAdminService
{
    private readonly PrismaDbContext _context;
    private readonly ILogger<DemoAdminService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoAdminService"/> class.
    /// </summary>
    public DemoAdminService(
        PrismaDbContext context,
        ILogger<DemoAdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets database statistics for demo verification.
    /// </summary>
    public async Task<DemoDataStats> GetDatabaseStatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving database statistics for demo admin panel");

        var stats = new DemoDataStats
        {
            AuditRecordsCount = await _context.AuditRecords.CountAsync(cancellationToken),
            FileMetadataCount = await _context.FileMetadata.CountAsync(cancellationToken),
            Timestamp = DateTime.UtcNow,
        };

        _logger.LogInformation(
            "Database stats: AuditRecords={AuditCount}, FileMetadata={FileCount}",
            stats.AuditRecordsCount,
            stats.FileMetadataCount);

        return stats;
    }

    /// <summary>
    /// Cleans all demo data using HARD DELETES.
    /// WARNING: This is IRREVERSIBLE! Only for demo environments!
    /// </summary>
    public async Task<DemoCleanupResult> CleanDemoDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Starting demo data cleanup - HARD DELETES will be performed!");

        var result = new DemoCleanupResult
        {
            StartTime = DateTime.UtcNow,
            Success = false,
        };

        try
        {
            // Get counts before deletion
            var statsBeforeCleanup = await GetDatabaseStatsAsync(cancellationToken);
            result.AuditRecordsDeleted = statsBeforeCleanup.AuditRecordsCount;
            result.FileMetadataDeleted = statsBeforeCleanup.FileMetadataCount;

            // Execute cleanup in transaction
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogInformation("Disabling foreign key constraints...");

                // Disable FK constraints (SQL Server specific)
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'",
                    cancellationToken);

                _logger.LogInformation("Deleting AuditRecords...");
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM AuditRecords",
                    cancellationToken);

                _logger.LogInformation("Deleting FileMetadata...");
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM FileMetadata",
                    cancellationToken);

                _logger.LogInformation("Re-enabling foreign key constraints...");
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'",
                    cancellationToken);

                _logger.LogInformation("Resetting identity seeds...");

                // Reset identity seeds for clean demo
                await _context.Database.ExecuteSqlRawAsync(
                    "IF OBJECT_ID('AuditRecords', 'U') IS NOT NULL DBCC CHECKIDENT ('AuditRecords', RESEED, 0)",
                    cancellationToken);

                await _context.Database.ExecuteSqlRawAsync(
                    "IF OBJECT_ID('FileMetadata', 'U') IS NOT NULL DBCC CHECKIDENT ('FileMetadata', RESEED, 0)",
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                result.Success = true;
                result.EndTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Demo data cleanup completed successfully. Deleted {AuditRecords} audit records, {FileMetadata} file metadata",
                    result.AuditRecordsDeleted,
                    result.FileMetadataDeleted);

                // Verify cleanup
                var statsAfterCleanup = await GetDatabaseStatsAsync(cancellationToken);
                result.Message = $"Successfully cleaned demo data. " +
                                $"AuditRecords: {result.AuditRecordsDeleted} → {statsAfterCleanup.AuditRecordsCount}, " +
                                $"FileMetadata: {result.FileMetadataDeleted} → {statsAfterCleanup.FileMetadataCount}";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("Failed to clean demo data. Transaction rolled back.", ex);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.EndTime = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            result.Message = $"Cleanup failed: {ex.Message}";

            _logger.LogError(
                ex,
                "Demo data cleanup failed after {Duration}ms",
                (result.EndTime - result.StartTime).TotalMilliseconds);
        }

        return result;
    }
}

/// <summary>
/// Statistics about current demo data in database.
/// </summary>
public record DemoDataStats
{
    /// <summary>
    /// Number of audit records (events).
    /// </summary>
    public int AuditRecordsCount { get; init; }

    /// <summary>
    /// Number of file metadata records.
    /// </summary>
    public int FileMetadataCount { get; init; }

    /// <summary>
    /// When these statistics were captured.
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Result of demo data cleanup operation.
/// </summary>
public record DemoCleanupResult
{
    /// <summary>
    /// Whether cleanup succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of audit records deleted.
    /// </summary>
    public int AuditRecordsDeleted { get; set; }

    /// <summary>
    /// Number of file metadata records deleted.
    /// </summary>
    public int FileMetadataDeleted { get; set; }

    /// <summary>
    /// When cleanup started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When cleanup ended.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Duration of cleanup operation.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Success or error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error message if cleanup failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
