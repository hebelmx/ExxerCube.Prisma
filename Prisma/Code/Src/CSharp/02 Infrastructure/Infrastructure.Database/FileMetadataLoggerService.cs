namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Service for logging file metadata to the database.
/// </summary>
public class FileMetadataLoggerService : IFileMetadataLogger
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<FileMetadataLoggerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMetadataLoggerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public FileMetadataLoggerService(
        PrismaDbContext dbContext,
        ILogger<FileMetadataLoggerService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> LogFileMetadataAsync(
        FileMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Logging file metadata for file: {FileId} ({FileName})", metadata.FileId, metadata.FileName);

            _dbContext.FileMetadata.Add(metadata);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully logged file metadata for file: {FileId}", metadata.FileId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log file metadata for file: {FileId}", metadata.FileId);
            return Result.WithFailure($"Failed to log file metadata: {ex.Message}", ex);
        }
    }
}

