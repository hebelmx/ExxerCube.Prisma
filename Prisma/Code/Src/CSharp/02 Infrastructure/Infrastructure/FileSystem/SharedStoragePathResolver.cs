using ExxerCube.Prisma.Domain.Services;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.FileSystem;

/// <summary>
/// Production <see cref="IStoragePathResolver"/> (MVP-PATH 1.3, ADR-011): resolves a storage-relative
/// document path against the shared storage base this process mounts (<see cref="StorageOptions.BasePath"/>),
/// producing a rooted, base-confined absolute path the file loader can open.
/// </summary>
/// <remarks>
/// Deterministic pure path math — no disk access (file existence is the loader's concern, kept separate so
/// resolution is testable without a volume). Tolerant of configuration faults: a blank input, an unconfigured
/// base, a rooted "relative" path, or any path that would escape the base all fail closed as a
/// <see cref="Result{T}"/> failure (logged, never thrown), so the caller can log-and-continue.
/// </remarks>
public sealed class SharedStoragePathResolver : IStoragePathResolver
{
    private readonly StorageOptions _options;
    private readonly ILogger<SharedStoragePathResolver> _logger;

    /// <summary>Initializes a new instance of the <see cref="SharedStoragePathResolver"/> class.</summary>
    /// <param name="options">The shared storage options (the base path this process mounts).</param>
    /// <param name="logger">The logger.</param>
    public SharedStoragePathResolver(IOptions<StorageOptions> options, ILogger<SharedStoragePathResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Result<string> Resolve(string relativeStoragePath)
    {
        var result = StoragePathResolution.Resolve(_options.BasePath, relativeStoragePath);
        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Could not resolve storage-relative path {RelativePath} against base {BasePath}: {Error}",
                relativeStoragePath,
                _options.BasePath,
                result.Error);
        }

        return result;
    }
}
