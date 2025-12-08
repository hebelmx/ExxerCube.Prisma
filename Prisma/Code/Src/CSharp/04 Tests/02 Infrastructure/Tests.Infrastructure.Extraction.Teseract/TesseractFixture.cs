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

        // Register Tesseract executor
        // TODO: Implement TesseractOcrExecutor in Infrastructure.Extraction
        builder.Services.AddScoped<IOcrExecutor, TesseractOcrExecutor>();

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