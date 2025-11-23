using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CSnakes.Runtime;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Tests.UI.Infrastructure;

/// <summary>
/// Web application factory for integration testing of the Prisma UI.
/// Starts the web server programmatically for Playwright tests.
/// </summary>
public class PrismaWebApplicationFactory : WebApplicationFactory<ExxerCube.Prisma.Web.UI.Program>
{
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
