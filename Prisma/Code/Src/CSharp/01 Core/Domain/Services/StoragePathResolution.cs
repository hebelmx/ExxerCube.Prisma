namespace ExxerCube.Prisma.Domain.Services;

/// <summary>
/// Pure, deterministic resolution of a storage-relative document path against a storage base path
/// (MVP-PATH 1.3 shared storage, ADR-011). This is the single source of truth for the security-sensitive
/// confinement guard, shared by the production <c>SharedStoragePathResolver</c> and the reference
/// <c>FakeStoragePathResolver</c> so the traversal defense can never diverge between them.
/// </summary>
/// <remarks>
/// No disk access — file existence is the loader's concern, kept out so resolution is testable without a
/// volume. Fails closed (a <see cref="Result{T}"/> failure, never an exception) on a blank input, an
/// unconfigured base, a rooted "relative" path, invalid path characters, or any path that would escape the
/// configured base.
/// </remarks>
public static class StoragePathResolution
{
    /// <summary>
    /// Resolves <paramref name="relativeStoragePath"/> against <paramref name="basePath"/> to a rooted,
    /// base-confined absolute path.
    /// </summary>
    /// <param name="basePath">The storage base this process mounts; blank fails closed.</param>
    /// <param name="relativeStoragePath">The storage-relative path (either separator accepted); blank fails closed.</param>
    /// <returns>A success result wrapping the absolute path, or a failure describing why it was rejected.</returns>
    public static Result<string> Resolve(string? basePath, string? relativeStoragePath)
    {
        if (string.IsNullOrWhiteSpace(relativeStoragePath))
        {
            return Result<string>.WithFailure("Storage-relative path is blank.");
        }

        if (string.IsNullOrWhiteSpace(basePath))
        {
            return Result<string>.WithFailure("Shared storage base path is not configured (Storage:BasePath).");
        }

        // Normalize either wire separator to this platform's separator so a forward-slash path resolves anywhere.
        var normalizedRelative = relativeStoragePath
            .Replace('\\', '/')
            .Replace('/', Path.DirectorySeparatorChar)
            .Trim();

        // A "relative" path that is actually rooted (e.g. "/etc/passwd", "C:\x") is an escape attempt.
        if (Path.IsPathRooted(normalizedRelative))
        {
            return Result<string>.WithFailure($"Storage path must be relative, but was rooted: '{relativeStoragePath}'.");
        }

        string baseFull;
        string resolved;
        try
        {
            baseFull = Path.GetFullPath(basePath);
            resolved = Path.GetFullPath(Path.Combine(baseFull, normalizedRelative));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<string>.WithFailure($"Invalid storage path '{relativeStoragePath}': {ex.Message}");
        }

        // Confinement guard: the resolved path must stay within the configured base (blocks ../ traversal).
        var baseWithSeparator = baseFull.EndsWith(Path.DirectorySeparatorChar)
            ? baseFull
            : baseFull + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.WithFailure($"Resolved storage path escapes the configured base: '{relativeStoragePath}'.");
        }

        return Result<string>.Success(resolved);
    }
}
