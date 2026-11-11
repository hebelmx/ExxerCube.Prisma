using ExxerCube.Prisma.Domain.Interfaces.Factories;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Service for querying file metadata from the database.
/// </summary>
public class FileMetadataQueryService
{
    private readonly IRepository<FileMetadata, string> _metadataRepository;
    private readonly ISpecificationFactory _specificationFactory;
    private readonly ILogger<FileMetadataQueryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMetadataQueryService"/> class.
    /// </summary>
    /// <param name="metadataRepository">The repository used to query metadata.</param>
    /// <param name="specificationFactory">The factory for creating query specifications.</param>
    /// <param name="logger">The logger instance.</param>
    public FileMetadataQueryService(
        IRepository<FileMetadata, string> metadataRepository,
        ISpecificationFactory specificationFactory,
        ILogger<FileMetadataQueryService> logger)
    {
        _metadataRepository = metadataRepository;
        _specificationFactory = specificationFactory;
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
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("File metadata query was cancelled before starting");
            return ResultExtensions.Cancelled<List<FileMetadata>>();
        }

        var specification = _specificationFactory.CreateFileMetadataFilters(startDate, endDate, format);
        var filesResult = await _metadataRepository
            .ListAsync(specification, cancellationToken)
            .ConfigureAwait(false);

        if (filesResult.IsCancelled())
        {
            _logger.LogInformation("File metadata query was cancelled");
            return ResultExtensions.Cancelled<List<FileMetadata>>();
        }

        if (!filesResult.IsSuccess)
        {
            _logger.LogError(
                "Error retrieving file metadata with filters StartDate={StartDate}, EndDate={EndDate}, Format={Format}: {Error}",
                startDate,
                endDate,
                format,
                filesResult.Error);

            return Result<List<FileMetadata>>.WithFailure(
                filesResult.Error ?? "Error retrieving file metadata");
        }

        var files = filesResult.Value?.ToList() ?? new List<FileMetadata>();

        _logger.LogInformation(
            "Retrieved {Count} file metadata records with filters: StartDate={StartDate}, EndDate={EndDate}, Format={Format}",
            files.Count,
            startDate,
            endDate,
            format);

        return Result<List<FileMetadata>>.Success(files);
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
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("File metadata query by ID was cancelled before starting");
            return ResultExtensions.Cancelled<FileMetadata?>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result<FileMetadata?>.WithFailure("File ID cannot be null or empty");
        }

        var fileResult = await _metadataRepository
            .GetByIdAsync(fileId, cancellationToken)
            .ConfigureAwait(false);

        if (fileResult.IsCancelled())
        {
            _logger.LogInformation("File metadata query by ID was cancelled");
            return ResultExtensions.Cancelled<FileMetadata?>();
        }

        if (!fileResult.IsSuccess)
        {
            _logger.LogError(
                "Error retrieving file metadata for FileId: {FileId}. {Error}",
                fileId,
                fileResult.Error);

            return Result<FileMetadata?>.WithFailure(
                fileResult.Error ?? "Error retrieving file metadata");
        }

        if (fileResult.Value == null)
        {
            _logger.LogWarning("File metadata not found for FileId: {FileId}", fileId);
            return Result<FileMetadata?>.Success(null);
        }

        return Result<FileMetadata?>.Success(fileResult.Value);
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
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Download statistics query was cancelled before starting");
            return ResultExtensions.Cancelled<DownloadStatistics>();
        }

        var specification = _specificationFactory.CreateFileMetadataFilters(startDate, endDate, null);
        var filesResult = await _metadataRepository
            .ListAsync(specification, cancellationToken)
            .ConfigureAwait(false);

        if (filesResult.IsCancelled())
        {
            _logger.LogInformation("Download statistics query was cancelled");
            return ResultExtensions.Cancelled<DownloadStatistics>();
        }

        if (!filesResult.IsSuccess)
        {
            _logger.LogError(
                "Error retrieving download statistics between {StartDate} and {EndDate}: {Error}",
                startDate,
                endDate,
                filesResult.Error);

            return Result<DownloadStatistics>.WithFailure(
                filesResult.Error ?? "Error retrieving download statistics");
        }

        var files = filesResult.Value ?? Array.Empty<FileMetadata>();

        var statistics = new DownloadStatistics
        {
            TotalFiles = files.Count,
            TotalSizeBytes = files.Sum(f => f.FileSize),
            FilesByFormat = files
                .GroupBy(f => f.Format)
                .ToDictionary(g => g.Key, g => g.Count()),
            StartDate = startDate,
            EndDate = endDate
        };

        _logger.LogInformation(
            "Retrieved download statistics: TotalFiles={TotalFiles}, TotalSizeBytes={TotalSizeBytes}, Period={StartDate} to {EndDate}",
            statistics.TotalFiles,
            statistics.TotalSizeBytes,
            startDate,
            endDate);

        return Result<DownloadStatistics>.Success(statistics);
    }
}
