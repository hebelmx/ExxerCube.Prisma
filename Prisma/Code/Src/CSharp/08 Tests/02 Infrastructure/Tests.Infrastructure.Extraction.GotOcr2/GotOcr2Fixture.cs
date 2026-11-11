namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

/// <summary>
/// Shared fixture for GOT-OCR2 Python environment and host.
/// Initialized once per test collection to avoid Python module state corruption.
/// </summary>
public class GotOcr2Fixture : IAsyncLifetime
{
    private IHost? _host;

    public IHost Host => _host ?? throw new InvalidOperationException("Host not initialized");

    public async ValueTask InitializeAsync()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        var baseDirectory = AppContext.BaseDirectory;
        var pythonLibPath = Path.Combine(baseDirectory, "python");
        var venvPath = Path.Combine(baseDirectory, ".venv_gotocr2_manual");
        var requirementsPath = Path.Combine(baseDirectory, "requirements.txt");

        // Configure CSnakes Python environment (once for all tests)
        builder.Services
            .WithPython()
            .WithHome(pythonLibPath)
            .WithVirtualEnvironment(venvPath, true)
            .FromRedistributable("3.13")
            .WithPipInstaller(requirementsPath);

        // Register GOT-OCR2 executor with proper DI
        builder.Services.AddScoped<IOcrExecutor, GotOcr2OcrExecutor>();

        _host = builder.Build();

        // Initialize Python environment once (triggers model loading)
        var pythonEnv = _host.Services.GetRequiredService<IPythonEnvironment>();
        var module = pythonEnv.GotOcr2Wrapper();

        // Warm up: Verify module is healthy
        var version = module.GetVersion();
        var modelInfo = module.GetModelInfo();
        var isHealthy = module.HealthCheck();

        if (!isHealthy)
        {
            throw new InvalidOperationException("GOT-OCR2 Python environment health check failed");
        }

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}