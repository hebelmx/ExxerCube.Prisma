using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Services;
using IndQuestResults;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written reference fake of <see cref="IStoragePathResolver"/> (ADR-005 §6): an in-memory resolver
/// with a configurable storage base and no <c>IOptions</c>/Infrastructure dependency, so Domain- and
/// Processing-layer tests (for example the ingestion forwarder) can resolve shared-storage paths without a
/// real volume.
/// </summary>
/// <remarks>
/// It deliberately delegates to the same <see cref="StoragePathResolution"/> primitive as the production
/// <c>SharedStoragePathResolver</c>: the security-sensitive confinement guard is single-sourced, so the fake
/// can never drift from real behavior. The contract verifies that delegation is wired correctly.
/// </remarks>
public sealed class FakeStoragePathResolver : IStoragePathResolver
{
    /// <summary>A deterministic, platform-rooted default base so resolution succeeds without configuration.</summary>
    public static readonly string DefaultBasePath = Path.Combine(Path.GetTempPath(), "prisma-fake-storage");

    private readonly string _basePath;

    /// <summary>Initializes the fake.</summary>
    /// <param name="basePath">The storage base to resolve against; defaults to <see cref="DefaultBasePath"/>.</param>
    public FakeStoragePathResolver(string? basePath = null)
    {
        _basePath = string.IsNullOrWhiteSpace(basePath) ? DefaultBasePath : basePath!;
    }

    /// <inheritdoc />
    public Result<string> Resolve(string relativeStoragePath) =>
        StoragePathResolution.Resolve(_basePath, relativeStoragePath);
}
