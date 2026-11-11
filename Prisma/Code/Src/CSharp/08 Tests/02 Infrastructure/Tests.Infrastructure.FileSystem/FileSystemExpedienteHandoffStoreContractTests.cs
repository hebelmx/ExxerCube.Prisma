using ExxerCube.Prisma.Infrastructure.FileSystem;
using ExxerCube.Prisma.Testing.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileSystem;

/// <summary>
/// Production-implementation instance of <see cref="ExpedienteHandoffStoreContract"/> (ADR-005, ADR-011
/// Reconciliator edge): proves the real <see cref="FileSystemExpedienteHandoffStore"/> (JSON on disk, driven
/// by the real <see cref="SharedStoragePathResolver"/>) honors the same handoff contract as the reference fake.
/// </summary>
public sealed class FileSystemExpedienteHandoffStoreContractTests : ExpedienteHandoffStoreContract
{
    private static readonly string ContractBasePath =
        Path.Combine(Path.GetTempPath(), "prisma-handoff-store-contract");

    /// <summary>Initializes the production-implementation instance with a temp storage base.</summary>
    public FileSystemExpedienteHandoffStoreContractTests()
        : base(new FileSystemExpedienteHandoffStore(
            new SharedStoragePathResolver(
                Options.Create(new StorageOptions { BasePath = ContractBasePath }),
                NullLogger<SharedStoragePathResolver>.Instance),
            NullLogger<FileSystemExpedienteHandoffStore>.Instance))
    {
    }
}
