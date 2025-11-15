using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Service for querying file metadata from the database.
/// </summary>
public class FileMetadataQueryService
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<FileMetadataQueryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMetadataQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public FileMetadataQueryService(
        PrismaDbContext dbContext,
        ILogger<FileMetadataQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets all file metadata records with optional filtering.
    /// </summary>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="format">Optional file format filter.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of file metadata records.</returns>
    public async Task<Result<List<FileMetadata>>> GetFileMetadataAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        FileFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("File metadata query was cancelled before starting");
            return ResultExtensions.Cancelled<List<FileMetadata>>();
        }

        try
        {
            var query = _dbContext.FileMetadata.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(f => f.DownloadTimestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(f => f.DownloadTimestamp <= endDate.Value);
            }

            if (format.HasValue)
            {
                query = query.Where(f => f.Format == format.Value);
            }

            var files = await query
                .OrderByDescending(f => f.DownloadTimestamp)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Retrieved {Count} file metadata records with filters: StartDate={StartDate}, EndDate={EndDate}, Format={Format}",
                files.Count, startDate, endDate, format);

            return Result<List<FileMetadata>>.Success(files);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("File metadata query was cancelled");
            return ResultExtensions.Cancelled<List<FileMetadata>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file metadata");
            return Result<List<FileMetadata>>.WithFailure($"Error retrieving file metadata: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Gets file metadata by file ID.
    /// </summary>
    /// <param name="fileId">The file ID.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the file metadata if found, or an error.</returns>
    public async Task<Result<FileMetadata?>> GetFileMetadataByIdAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("File metadata query by ID was cancelled before starting");
            return ResultExtensions.Cancelled<FileMetadata?>();
        }

        // Input validation
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result<FileMetadata?>.WithFailure("File ID cannot be null or empty");
        }

        try
        {
            var file = await _dbContext.FileMetadata
                .FirstOrDefaultAsync(f => f.FileId == fileId, cancellationToken)
                .ConfigureAwait(false);

            if (file == null)
            {
                _logger.LogWarning("File metadata not found for FileId: {FileId}", fileId);
                return Result<FileMetadata?>.Success(null);
            }

            return Result<FileMetadata?>.Success(file);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("File metadata query by ID was cancelled");
            return ResultExtensions.Cancelled<FileMetadata?>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file metadata for FileId: {FileId}", fileId);
            return Result<FileMetadata?>.WithFailure($"Error retrieving file metadata: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Gets download statistics for a given time period.
    /// </summary>
    /// <param name="startDate">Start date for statistics.</param>
    /// <param name="endDate">End date for statistics.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing download statistics.</returns>
    public async Task<Result<DownloadStatistics>> GetDownloadStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Download statistics query was cancelled before starting");
            return ResultExtensions.Cancelled<DownloadStatistics>();
        }

        try
        {
            var files = await _dbContext.FileMetadata
                .Where(f => f.DownloadTimestamp >= startDate && f.DownloadTimestamp <= endDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var statistics = new DownloadStatistics
            {
                TotalFiles = files.Count,
                TotalSizeBytes = files.Sum(f => f.FileSize),
                FilesByFormat = files.GroupBy(f => f.Format)
                    .ToDictionary(g => g.Key, g => g.Count()),
                StartDate = startDate,
                EndDate = endDate
            };

            _logger.LogInformation(
                "Retrieved download statistics: TotalFiles={TotalFiles}, TotalSizeBytes={TotalSizeBytes}, Period={StartDate} to {EndDate}",
                statistics.TotalFiles, statistics.TotalSizeBytes, startDate, endDate);

            return Result<DownloadStatistics>.Success(statistics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Download statistics query was cancelled");
            return ResultExtensions.Cancelled<DownloadStatistics>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving download statistics");
            return Result<DownloadStatistics>.WithFailure($"Error retrieving download statistics: {ex.Message}", default, ex);
        }
    }
}

/// <summary>
/// Represents download statistics for a time period.
/// </summary>
public class DownloadStatistics
{
    /// <summary>
    /// Gets or sets the total number of files downloaded.
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Gets or sets the total size of downloaded files in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the count of files by format.
    /// </summary>
    public Dictionary<FileFormat, int> FilesByFormat { get; set; } = new();

    /// <summary>
    /// Gets or sets the start date of the statistics period.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the statistics period.
    /// </summary>
    public DateTime EndDate { get; set; }
}

