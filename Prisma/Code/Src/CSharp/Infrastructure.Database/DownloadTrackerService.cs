namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Service for tracking downloaded files and detecting duplicates using checksums.
/// </summary>
public class DownloadTrackerService : IDownloadTracker
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<DownloadTrackerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadTrackerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DownloadTrackerService(
        PrismaDbContext dbContext,
        ILogger<DownloadTrackerService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> IsDuplicateAsync(
        string checksum,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking for duplicate file with checksum: {Checksum}", checksum);

            var exists = await _dbContext.FileMetadata
                .AnyAsync(f => f.Checksum == checksum, cancellationToken);

            _logger.LogDebug("Duplicate check result for checksum {Checksum}: {IsDuplicate}", checksum, exists);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for duplicate file with checksum: {Checksum}", checksum);
            return Result<bool>.WithFailure($"Failed to check for duplicate: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<FileMetadata?>> GetFileMetadataByChecksumAsync(
        string checksum,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving file metadata for checksum: {Checksum}", checksum);

            var fileMetadata = await _dbContext.FileMetadata
                .FirstOrDefaultAsync(f => f.Checksum == checksum, cancellationToken);

            if (fileMetadata == null)
            {
                _logger.LogDebug("No file metadata found for checksum: {Checksum}", checksum);
            }

            return Result<FileMetadata?>.Success(fileMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve file metadata for checksum: {Checksum}", checksum);
            return Result<FileMetadata?>.WithFailure($"Failed to retrieve file metadata: {ex.Message}", default, ex);
        }
    }
}

