using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Xunit;

// One shared SQL Server container for the WHOLE test assembly (xUnit v3 assembly fixture).
// Test classes inject SqlServerContainerFixture via their constructor and call
// CreateIsolatedDatabaseAsync(...) to get a private database — so writer classes run in
// PARALLEL across the shared container instead of serializing on one database.
[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]
