using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Net;

namespace ExxerCube.Prisma.Tests.UI.Infrastructure;

/// <summary>
/// Web application factory for integration testing of the Prisma UI.
/// Starts the web server programmatically for Playwright tests.
/// </summary>
public class PrismaWebApplicationFactory : WebApplicationFactory<ExxerCube.Prisma.Web.UI.Program>
{
    private IHost? _host;
    public Uri? HostedBaseAddress { get; private set; }

    /// <summary>
    /// Starts the Kestrel host if it is not already running.
    /// </summary>
    public void EnsureStarted()
    {
        if (_host is not null)
        {
            return;
        }

        var hostBuilder = CreateHostBuilder()!;
        _ = CreateHost(hostBuilder);
    }

    /// <summary>
    /// Configures the web host for testing.
    /// </summary>
    /// <param name="builder">The web host builder to configure.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the Python environment registration (not needed for UI tests)
            var pythonEnvDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPythonEnvironment));
            if (pythonEnvDescriptor != null)
            {
                services.Remove(pythonEnvDescriptor);
            }

            // Remove GOT-OCR2 executor registration (not needed for UI tests)
            var ocrExecutorDescriptors = services.Where(d => d.ServiceType == typeof(IOcrExecutor)).ToList();
            foreach (var descriptor in ocrExecutorDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add a mock OCR executor for UI tests
            services.AddScoped<IOcrExecutor, MockOcrExecutor>();
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Override host creation to run Kestrel instead of the in-memory TestServer so Playwright can reach it.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureWebHost(webHost =>
        {
            webHost.UseKestrel(options =>
            {
                // Bind to dynamic ports to avoid collisions with a locally running instance
                options.Listen(System.Net.IPAddress.Loopback, 0);
            });
        });

        _host = builder.Build();
        _host.Start();

        // Capture the bound address so Playwright can navigate to the actual port
        var addresses = _host.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;
        var firstAddress = addresses?.FirstOrDefault();
        if (firstAddress is not null)
        {
            HostedBaseAddress = new Uri(firstAddress);
        }

        return _host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Mock OCR executor for UI testing that doesn't require Python.
    /// </summary>
    private class MockOcrExecutor : IOcrExecutor
    {
        public Task<IndQuestResults.Result<ExxerCube.Prisma.Domain.ValueObjects.OCRResult>> ExecuteOcrAsync(
            ExxerCube.Prisma.Domain.ValueObjects.ImageData imageData,
            ExxerCube.Prisma.Domain.Models.OCRConfig config)
        {
            // Return a mock success result for UI tests
            var mockResult = new ExxerCube.Prisma.Domain.ValueObjects.OCRResult(
                text: "Mock OCR Text",
                confidenceAvg: 95.0f,
                confidenceMedian: 95.0f,
                confidences: new List<float> { 95.0f },
                languageUsed: config.Language
            );
            return Task.FromResult(IndQuestResults.Result<ExxerCube.Prisma.Domain.ValueObjects.OCRResult>.Success(mockResult));
        }
    }
}
