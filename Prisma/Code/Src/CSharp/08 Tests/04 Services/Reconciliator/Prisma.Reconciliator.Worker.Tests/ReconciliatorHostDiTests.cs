using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Reconciliator.Worker.Tests;

/// <summary>
/// Host DI graph validation tests for the Reconciliator Worker.
/// These tests prove the service provider builds without errors when
/// ValidateOnBuild and ValidateScopes are enabled, and that SIRO export
/// services resolve correctly — specifically that IResponseExporter resolves
/// without requiring IMetadataExtractor (which is not registered in this host).
/// </summary>
/// <remarks>
/// Regression guard: before the fix the Reconciliator called AddExportServices which
/// registered PdfRequirementSummarizerService → IMetadataExtractor. Neither worker host
/// registers IMetadataExtractor, so the container threw:
///   "Unable to resolve service for type 'IMetadataExtractor'
///    while attempting to activate 'PdfRequirementSummarizerService'."
/// The fix switched the Reconciliator to AddSiroExportServices (no PdfRequirementSummarizer).
/// </remarks>
public sealed class ReconciliatorHostDiTests
{
    /// <summary>
    /// The Reconciliator host DI container must build successfully with ValidateOnBuild
    /// and ValidateScopes enabled.  Any unresolvable registration (missing IMetadataExtractor,
    /// captive-dependency scope violations, etc.) causes an immediate exception here.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task HostDi_BuildsWithoutErrors_WhenValidateOnBuildEnabled()
    {
        // Arrange + Act: WebApplicationFactory forces ValidateOnBuild via its internal host build.
        // If the DI graph is broken the constructor throws before we even get a client.
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var application = new ReconciliatorWorkerApplication();
            // CreateClient() triggers the full host build + DI validation.
            using var client = application.CreateClient();
        });

        // Assert
        exception.ShouldBeNull(
            "Reconciliator Worker DI graph must build without unresolved dependencies. " +
            "If this fails, a service registration is missing (e.g. IMetadataExtractor pulled " +
            "in by PdfRequirementSummarizerService) — use AddSiroExportServices, not AddExportServices.");
    }

    /// <summary>
    /// IResponseExporter must resolve to a SIRO-conformant exporter (CompositeResponseExporter
    /// wrapping SiroXmlExporter) from the Reconciliator host's service provider.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task HostDi_IResponseExporter_ResolvesToSiroCompositeExporter()
    {
        // Arrange
        await using var application = new ReconciliatorWorkerApplication();
        using var client = application.CreateClient();

        // Act
        using var scope = application.Services.CreateScope();
        var exporter = scope.ServiceProvider.GetService<IResponseExporter>();

        // Assert
        exporter.ShouldNotBeNull(
            "IResponseExporter must be registered in the Reconciliator Worker — " +
            "it drives Stage 5 SIRO XML export in ReconciliationOrchestrator.");

        exporter.GetType().Name.ShouldBe(
            "CompositeResponseExporter",
            "IResponseExporter must resolve to CompositeResponseExporter (SIRO path). " +
            "AdaptiveResponseExporterAdapter is NOT SIRO-conformant and must NOT be registered here.");
    }

    /// <summary>
    /// Health endpoint must return 200 — proves the host starts end-to-end, not just that the
    /// container builds.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_ReconciliatorRunning_Returns200()
    {
        // Arrange
        await using var application = new ReconciliatorWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Healthy", Case.Insensitive);
    }
}
