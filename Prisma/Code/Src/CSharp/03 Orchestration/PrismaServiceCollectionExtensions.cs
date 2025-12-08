using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExxerCube.Prisma.Application;
using ExxerCube.Prisma.Infrastructure.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Classification.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.FileStorage;
using ExxerCube.Prisma.Infrastructure.FileStorage.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Export.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Metrics.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Python;

namespace ExxerCube.Prisma.Composition;

/// <summary>
/// Composition root for Prisma dependency injection orchestration.
/// Provides a single entry point for registering all infrastructure services.
/// </summary>
/// <remarks>
/// This class lives outside the Infrastructure layer to avoid architectural violations.
/// Infrastructure projects cannot depend on each other - this composition layer sits above
/// Infrastructure and orchestrates the dependency graph.
/// </remarks>
public static class PrismaServiceCollectionExtensions
{
    /// <summary>
    /// Registers ALL Prisma infrastructure services in the correct order.
    /// This is the single entry point for infrastructure DI configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pythonConfiguration">The Python configuration for OCR services.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="configureFileStorage">Action to configure file storage options.</param>
    /// <param name="configuration">The configuration instance (optional, for additional settings).</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method orchestrates service registration across all infrastructure projects:
    /// </para>
    /// <list type="number">
    /// <item><description>Infrastructure.Extraction (OCR + Field Extraction)</description></item>
    /// <item><description>Infrastructure.Extraction.Adaptive (5-Strategy Adaptive DOCX Extraction)</description></item>
    /// <item><description>Infrastructure.Classification (Data Fusion + CNBV Classification)</description></item>
    /// <item><description>Infrastructure.Database (EF Core + Repositories)</description></item>
    /// <item><description>Infrastructure.FileStorage (Azure Blob Storage)</description></item>
    /// <item><description>Infrastructure.Export (PDF Generation)</description></item>
    /// <item><description>Infrastructure.Export.Adaptive (Adaptive PDF Export)</description></item>
    /// <item><description>Infrastructure.Metrics (Processing Metrics)</description></item>
    /// <item><description>Infrastructure (Legacy OCR Adapter)</description></item>
    /// </list>
    /// <para>
    /// <strong>Usage in API/Host:</strong>
    /// </para>
    /// <code>
    /// // In Program.cs or Startup.cs:
    /// services.AddPrismaInfrastructure(pythonConfig, connectionString, options => configuration.GetSection("FileStorage").Bind(options), configuration);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddPrismaInfrastructure(
        this IServiceCollection services,
        PythonConfiguration pythonConfiguration,
        string connectionString,
        Action<FileStorageOptions> configureFileStorage,
        IConfiguration? configuration = null)
    {
        // 1. Core extraction services (OCR, field extractors)
        services.AddExtractionServices();

        // 2. Adaptive DOCX extraction (5 strategies)
        services.AddAdaptiveDocxExtraction();

        // 3. Data fusion and CNBV classification
        services.AddClassificationServices();

        // 4. Database services (EF Core + Repositories)
        services.AddDatabaseServices(connectionString, configuration);

        // 5. File storage services (Azure Blob)
        services.AddFileStorageServices(configureFileStorage);

        // 6. Export services (PDF generation)
        services.AddExportServices();

        // 7. Metrics services (processing metrics)
        services.AddMetricsServices(pythonConfiguration.MaxConcurrency);

        // 8. Legacy OCR processing adapter
        services.AddOcrProcessingServices(pythonConfiguration);

        return services;
    }
}