using ExxerCube.Prisma.Infrastructure.FileSystem;
using ExxerCube.Prisma.Testing.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileSystem;

/// <summary>
/// Production-implementation instance of <see cref="StoragePathResolverContract"/> (ADR-005, ADR-011):
/// proves the real <see cref="SharedStoragePathResolver"/> (driven by <see cref="StorageOptions"/>) honors
/// the same resolution + confinement contract as the reference fake.
/// </summary>
public sealed class SharedStoragePathResolverContractTests : StoragePathResolverContract
{
    private static readonly string ContractBasePath =
        Path.Combine(Path.GetTempPath(), "prisma-shared-storage-contract");

    /// <summary>Initializes the production-implementation instance with a temp storage base.</summary>
    public SharedStoragePathResolverContractTests()
        : base(new SharedStoragePathResolver(
            Options.Create(new StorageOptions { BasePath = ContractBasePath }),
            NullLogger<SharedStoragePathResolver>.Instance))
    {
    }
}
