namespace ExxerCube.Prisma.Testing.Abstractions;

/// <summary>
/// Base class for test fixtures that can be shared across test projects.
/// Concrete implementations must be in test projects, but base logic can be shared.
/// </summary>
/// <remarks>
/// This base class provides a simple pattern for test fixtures.
/// Concrete implementations should implement IAsyncLifetime from xUnit or Microsoft.Extensions.Hosting.
/// </remarks>
public abstract class TestFixtureBase
{
    /// <summary>
    /// Performs setup operations for the fixture.
    /// </summary>
    protected abstract Task SetupAsync();

    /// <summary>
    /// Performs teardown operations for the fixture.
    /// </summary>
    protected abstract Task TeardownAsync();

    /// <summary>
    /// Initializes the fixture. Should be called from test class constructor or IAsyncLifetime.InitializeAsync.
    /// </summary>
    public async Task InitializeAsync() => await SetupAsync();

    /// <summary>
    /// Disposes the fixture. Should be called from test class Dispose or IAsyncLifetime.DisposeAsync.
    /// </summary>
    public async Task DisposeAsync() => await TeardownAsync();
}

