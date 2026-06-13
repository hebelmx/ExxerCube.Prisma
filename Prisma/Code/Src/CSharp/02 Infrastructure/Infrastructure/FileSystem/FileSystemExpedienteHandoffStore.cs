using System.Text.Json;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Infrastructure.FileSystem;

/// <summary>
/// Production <see cref="IExpedienteHandoffStore"/> (MVP-PATH 1.4 Reconciliator edge, ADR-011): persists and
/// loads a fused expediente as JSON on the shared storage volume, so the Extractor (Athena) can hand it to the
/// Reconciliator over the owner-chosen shared-storage-reference contract.
/// </summary>
/// <remarks>
/// The security-sensitive traversal/confinement guard is delegated to <see cref="IStoragePathResolver"/> (the
/// same guard the document edge uses) — never re-implemented here. Tolerant of faults: a blank/escaping path,
/// an unconfigured base, a missing artifact, or a (de)serialization/IO error all fail closed as a
/// <see cref="Result{T}"/> failure (logged, never thrown), so the caller can log-and-continue.
/// </remarks>
public sealed class FileSystemExpedienteHandoffStore : IExpedienteHandoffStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly IStoragePathResolver _pathResolver;
    private readonly ILogger<FileSystemExpedienteHandoffStore> _logger;

    /// <summary>Initializes a new instance of the <see cref="FileSystemExpedienteHandoffStore"/> class.</summary>
    /// <param name="pathResolver">Resolves the storage-relative handoff path against this process's storage base.</param>
    /// <param name="logger">The logger.</param>
    public FileSystemExpedienteHandoffStore(IStoragePathResolver pathResolver, ILogger<FileSystemExpedienteHandoffStore> logger)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<string>> SaveAsync(Expediente expediente, string relativeStoragePath, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<string>();
        }

        if (expediente is null)
        {
            return Result<string>.WithFailure("Expediente cannot be null");
        }

        var resolution = _pathResolver.Resolve(relativeStoragePath);
        if (resolution.IsFailure)
        {
            return Result<string>.WithFailure(resolution.Errors);
        }

        var absolutePath = resolution.Value!;

        try
        {
            var directory = System.IO.Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(expediente, SerializerOptions);
            await File.WriteAllTextAsync(absolutePath, json, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Saved fused expediente handoff to {RelativePath} ({Bytes} bytes)",
                relativeStoragePath,
                json.Length);

            return Result<string>.Success(relativeStoragePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save expediente handoff to {RelativePath}", relativeStoragePath);
            return Result<string>.WithFailure($"Failed to save expediente handoff: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Expediente>> LoadAsync(string relativeStoragePath, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<Expediente>();
        }

        var resolution = _pathResolver.Resolve(relativeStoragePath);
        if (resolution.IsFailure)
        {
            return Result<Expediente>.WithFailure(resolution.Errors);
        }

        var absolutePath = resolution.Value!;

        if (!File.Exists(absolutePath))
        {
            return Result<Expediente>.WithFailure($"No handoff artifact at '{relativeStoragePath}'");
        }

        try
        {
            var json = await File.ReadAllTextAsync(absolutePath, cancellationToken).ConfigureAwait(false);
            var expediente = System.Text.Json.JsonSerializer.Deserialize<Expediente>(json, SerializerOptions);

            if (expediente is null)
            {
                return Result<Expediente>.WithFailure($"Handoff artifact at '{relativeStoragePath}' deserialized to null");
            }

            _logger.LogInformation("Loaded fused expediente handoff from {RelativePath}", relativeStoragePath);
            return Result<Expediente>.Success(expediente);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<Expediente>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expediente handoff from {RelativePath}", relativeStoragePath);
            return Result<Expediente>.WithFailure($"Failed to load expediente handoff: {ex.Message}");
        }
    }
}
