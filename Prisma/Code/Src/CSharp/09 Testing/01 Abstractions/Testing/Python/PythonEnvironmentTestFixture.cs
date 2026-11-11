using CSnakes.Runtime;
using ExxerCube.Prisma.Infrastructure.Python;
using ExxerCube.Prisma.Testing.Abstractions;

namespace ExxerCube.Prisma.Testing.Python;

/// <summary>
/// Base fixture for Python/CSnakes test environments shared by downstream test projects.
/// Concrete implementations should live in test assemblies and extend this type to reuse setup/teardown behavior.
/// </summary>
public abstract class PythonEnvironmentTestFixture : TestFixtureBase
{
    /// <summary>
    /// Gets the shared Python environment instance for integration tests.
    /// </summary>
    /// <remarks>Provides direct access to <see cref="PrismaPythonEnvironment.Env"/> for derived fixtures.</remarks>
    protected IPythonEnvironment PythonEnvironment => PrismaPythonEnvironment.Env;

    /// <summary>
    /// Performs Python environment setup required before running tests.
    /// </summary>
    /// <returns>A completed task once initialization has been ensured.</returns>
    protected override async Task SetupAsync()
    {
        // Ensure Python environment is initialized
        _ = PrismaPythonEnvironment.Env;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Performs Python environment cleanup after tests complete.
    /// </summary>
    /// <returns>A completed task when cleanup is finished.</returns>
    protected override async Task TeardownAsync()
    {
        // Python environment cleanup if needed
        await Task.CompletedTask;
    }
}

