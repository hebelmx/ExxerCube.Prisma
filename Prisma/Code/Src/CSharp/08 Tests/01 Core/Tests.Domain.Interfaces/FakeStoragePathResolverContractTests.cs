namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="StoragePathResolverContract"/> (ADR-005 §6): proves the
/// <see cref="FakeStoragePathResolver"/> honors the port contract, so it is a trustworthy stand-in for the
/// ingestion-forwarder tests that resolve shared-storage paths without a real volume.
/// </summary>
public sealed class FakeStoragePathResolverContractTests : StoragePathResolverContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeStoragePathResolverContractTests()
        : base(new FakeStoragePathResolver())
    {
    }
}
