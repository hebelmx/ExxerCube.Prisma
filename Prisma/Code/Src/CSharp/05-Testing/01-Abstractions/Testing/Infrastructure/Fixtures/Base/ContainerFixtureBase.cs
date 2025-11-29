using DotNet.Testcontainers.Containers;

namespace ExxerCube.Prisma.Testing.Infrastructure.Fixtures.Base;

/// <summary>
/// Abstract base class for Docker container fixtures implementing defensive programming patterns
/// and Docker integration best practices for production-grade testing.
/// Implements xUnit v3 IAsyncLifetime pattern for proper container lifecycle management.
/// Uses TestContext.Current.SendMessage() for logging to provide real output for debugging.
/// </summary>
/// <typeparam name="TContainer">The Testcontainers container type.</typeparam>
public abstract class ContainerFixtureBase<TContainer> : IAsyncLifetime
    where TContainer : IContainer
{
    /// <summary>
    /// The Docker container instance.
    /// </summary>
    protected TContainer? Container;

    /// <summary>
    /// Gets the container hostname.
    /// </summary>
    public abstract string Hostname { get; }

    /// <summary>
    /// Gets the main container port.
    /// </summary>
    public abstract int Port { get; }

    /// <summary>
    /// Gets the connection string for the container service.
    /// </summary>
    public abstract string ConnectionString { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether the container is available and ready.
    /// </summary>
    public virtual bool IsAvailable => Container != null && !string.IsNullOrEmpty(ConnectionString);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerFixtureBase{TContainer}"/> class.
    /// Logging is done via TestContext.Current.SendMessage() for real test output.
    /// </summary>
    protected ContainerFixtureBase()
    {
        ConnectionString = string.Empty;
    }

    /// <summary>
    /// Logs a message to the test output using TestContext.Current.SendDiagnosticMessage().
    /// Defensive - does nothing if TestContext.Current is not available.
    /// </summary>
    /// <param name="message">The message to log.</param>
    protected void LogMessage(string message)
    {
        try
        {
            // TestContext.Current is available during test execution
            // This provides real output for debugging container issues
            TestContext.Current?.SendDiagnosticMessage(message);
        }
        catch
        {
            // Ignore if TestContext is not available (shouldn't happen in fixtures, but be defensive)
        }
    }

    /// <summary>
    /// Logs an error message with exception details to the test output.
    /// Defensive - does nothing if TestContext.Current is not available.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The error message.</param>
    protected void LogMessage(Exception ex, string message)
    {
        try
        {
            // Format exception details for diagnostic output
            var fullMessage = $"{message}\nException: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
            TestContext.Current?.SendDiagnosticMessage(fullMessage);
        }
        catch
        {
            // Ignore if TestContext is not available (shouldn't happen in fixtures, but be defensive)
        }
    }

    /// <summary>
    /// Asynchronously initializes the container.
    /// Called once per test collection by xUnit v3.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous initialization operation.</returns>
    public async ValueTask InitializeAsync()
    {
        try
        {
            LogMessage($"🚀 Initializing {GetContainerTypeName()} container...");

            // Build container with defensive patterns
            Container = await BuildContainerAsync();

            LogMessage($"⏳ Starting {GetContainerTypeName()} container...");

            // Start container and wait for readiness
            await Container.StartAsync();

            // Configure connection after successful start
            await ConfigureConnectionAsync();

            LogMessage($"✅ {GetContainerTypeName()} container started successfully!");
            LogMessage($"🔗 Connection: {ConnectionString}");
            LogMessage($"🌐 Host: {Hostname}:{Port}");
        }
        catch (Exception ex) when (ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("daemon", StringComparison.OrdinalIgnoreCase))
        {
            LogMessage($"⚠️ Docker-related error - {GetContainerTypeName()} container will not be started. Ensure Docker Desktop is running.");
            LogMessage($"   Error: {ex.Message}");

            // DO NOT set connection string - leave it null/empty so tests FAIL clearly
            // Tests will check IsAvailable and fail with meaningful error messages
            // This is correct integration test behavior - FAIL HARD when infrastructure unavailable
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Failed to initialize {GetContainerTypeName()} container: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Builds the container with all defensive programming patterns:
    /// - Verified image tags
    /// - Defensive wait strategies
    /// - Proper port bindings
    /// - Auto-cleanup enabled
    /// </summary>
    /// <returns>A configured container instance ready to start.</returns>
    protected abstract Task<TContainer> BuildContainerAsync();

    /// <summary>
    /// Configures connection string and any post-startup initialization.
    /// Called after container successfully starts.
    /// </summary>
    /// <returns>A Task representing the asynchronous configuration operation.</returns>
    protected abstract Task ConfigureConnectionAsync();

    /// <summary>
    /// Gets a human-readable name for the container type (for logging).
    /// </summary>
    /// <returns>The container type name.</returns>
    protected abstract string GetContainerTypeName();

    /// <summary>
    /// Ensures the container is available for testing.
    /// Throws InvalidOperationException if container is not ready.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when container is not available.</exception>
    public void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                $"{GetContainerTypeName()} container is not available. " +
                "Ensure Docker Desktop is running and containers have started.");
        }
    }

    /// <summary>
    /// Asynchronously disposes of the container resources.
    /// Called once per test collection by xUnit v3.
    /// Implements proper cleanup with explicit stop before dispose.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            LogMessage($"🧹 Cleaning up {GetContainerTypeName()} container...");

            if (Container != null)
            {
                // Perform any custom cleanup before stopping
                await PerformCustomCleanupAsync();

                // Stop container explicitly before disposing
                try
                {
                    await Container.StopAsync();
                    LogMessage($"✅ {GetContainerTypeName()} container stopped");
                }
                catch (Exception stopEx)
                {
                    LogMessage($"⚠️ Error stopping {GetContainerTypeName()} container (non-fatal): {stopEx.Message}");
                }

                // Dispose container
                await Container.DisposeAsync();
                LogMessage($"✅ {GetContainerTypeName()} container cleaned up successfully");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"⚠️ Error during {GetContainerTypeName()} container cleanup (non-fatal): {ex.Message}");
        }
    }

    /// <summary>
    /// Performs any custom cleanup operations before stopping the container.
    /// Override in derived classes if needed (e.g., database cleanup, collection removal).
    /// </summary>
    /// <returns>A Task representing the asynchronous cleanup operation.</returns>
    protected virtual Task PerformCustomCleanupAsync()
    {
        return Task.CompletedTask;
    }
}