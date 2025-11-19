namespace ExxerCube.Prisma.Testing.Abstractions;

/// <summary>
/// Provides a common pattern for reusable test fixtures across projects, encapsulating setup and teardown flows.
/// </summary>
/// <remarks>
/// Deriving fixtures should implement xUnit's see cref="Xunit.IAsyncLifetime" or equivalent contract
/// and delegate to <see cref="InitializeAsync"/> and <see cref="DisposeAsync"/> to ensure consistent lifecycle handling.
/// </remarks>
public abstract class TestFixtureBase
{
    /// <summary>
    /// Performs fixture setup, such as allocating shared resources or seeding dependencies.
    /// </summary>
    /// <returns>A task that completes when setup has finished.</returns>
    protected abstract Task SetupAsync();

    /// <summary>
    /// Performs fixture teardown to release resources or clean persisted state.
    /// </summary>
    /// <returns>A task that completes when teardown has finished.</returns>
    protected abstract Task TeardownAsync();

    /// <summary>
    /// Initializes the fixture; call from the test class constructor or <c>IAsyncLifetime.InitializeAsync</c>.
    /// </summary>
    /// <returns>A task that completes after setup has run.</returns>
    public async Task InitializeAsync() => await SetupAsync();

    /// <summary>
    /// Disposes the fixture; call from test class <c>Dispose</c> or <c>IAsyncLifetime.DisposeAsync</c>.
    /// </summary>
    /// <returns>A task that completes after teardown has run.</returns>
    public async Task DisposeAsync() => await TeardownAsync();
}