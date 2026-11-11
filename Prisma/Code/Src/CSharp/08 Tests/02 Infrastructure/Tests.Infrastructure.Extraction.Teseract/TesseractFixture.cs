using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Shared fixture for Tesseract OCR executor.
/// Simpler than GOT-OCR2 - Tesseract doesn't need Python environment or model loading.
/// </summary>
public class TesseractFixture : IAsyncLifetime
{
    private IHost? _host;

    public IHost Host => _host ?? throw new InvalidOperationException("Host not initialized");

    public async ValueTask InitializeAsync()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        // Singleton lifetime is MANDATORY: TesseractEngine (native) deadlocks if initialized twice
        // in the same process. AddSingleton ensures exactly one engine for the test host's lifetime.
        builder.Services.AddSingleton<IOcrExecutor, TesseractOcrExecutor>();

        _host = builder.Build();

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