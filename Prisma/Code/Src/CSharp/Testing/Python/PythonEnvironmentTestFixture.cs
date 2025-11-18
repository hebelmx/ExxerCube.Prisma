using CSnakes.Runtime;
using ExxerCube.Prisma.Infrastructure.Python;
using ExxerCube.Prisma.Testing.Abstractions;

namespace ExxerCube.Prisma.Testing.Python;

/// <summary>
/// Base fixture for Python/CSnakes test environments.
/// Concrete implementations should be in test projects.
/// </summary>
public abstract class PythonEnvironmentTestFixture : TestFixtureBase
{
    /// <summary>
    /// Gets the Python environment instance.
    /// </summary>
    protected IPythonEnvironment PythonEnvironment => PrismaPythonEnvironment.Env;

    /// <summary>
    /// Performs Python environment setup.
    /// </summary>
    protected override async Task SetupAsync()
    {
        // Ensure Python environment is initialized
        _ = PrismaPythonEnvironment.Env;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Performs Python environment cleanup.
    /// </summary>
    protected override async Task TeardownAsync()
    {
        // Python environment cleanup if needed
        await Task.CompletedTask;
    }
}

