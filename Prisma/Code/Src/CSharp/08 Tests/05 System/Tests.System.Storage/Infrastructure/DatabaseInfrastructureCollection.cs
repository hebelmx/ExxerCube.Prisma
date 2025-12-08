namespace ExxerCube.Prisma.Tests.System.Storage.Infrastructure;

/// <summary>
/// xUnit collection definition for database infrastructure tests.
/// Ensures all tests in this collection share the same SqlServerContainerFixture instance,
/// which means the container is started once and reused across all tests.
/// DisableParallelization ensures tests run sequentially to avoid database conflicts.
/// </summary>
[CollectionDefinition("DatabaseInfrastructure", DisableParallelization = true)]
public sealed class DatabaseInfrastructureCollection : ICollectionFixture<SqlServerContainerFixture>
{
    // This class is just a marker for xUnit collection fixture
    // The actual fixture implementation is SqlServerContainerFixture
}