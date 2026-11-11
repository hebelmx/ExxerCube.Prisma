using System.Text.Json;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Orion.Worker.Tests;

/// <summary>
/// TDD tests for Orion Worker dashboard endpoints (stats/metrics).
/// </summary>
/// <remarks>
/// Stage 4 Requirements:
/// - /dashboard endpoint returns basic stats
/// - Response includes documents processed count
/// - Response includes last event timestamp
/// - Response includes queue depth (if available)
/// - Response includes last heartbeat time
/// </remarks>
public sealed class DashboardEndpointTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dashboard_Returns200()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/dashboard");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dashboard_ReturnsProcessingStats()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/dashboard");
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<DashboardStats>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        stats.ShouldNotBeNull();
        stats.DocumentsProcessed.ShouldBeGreaterThanOrEqualTo(0);
        stats.WorkerName.ShouldBe("Orion Ingestion Worker");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dashboard_IncludesLastHeartbeat()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/dashboard");
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<DashboardStats>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        stats.ShouldNotBeNull();
        stats.LastHeartbeat.ShouldNotBeNull();
        stats.LastHeartbeat.Value.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dashboard_IncludesLastEventTime()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/dashboard");
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<DashboardStats>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        stats.ShouldNotBeNull();
        // LastEventTime may be null if no events processed yet
        if (stats.LastEventTime.HasValue)
        {
            stats.LastEventTime.Value.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dashboard_IncludesQueueDepth()
    {
        // Arrange
        await using var application = new OrionWorkerApplication();
        using var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/dashboard");
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<DashboardStats>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        stats.ShouldNotBeNull();
        stats.QueueDepth.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dashboard_ReflectsRecordedDocuments_AfterThreeRecords()
    {
        // Arrange — shared application so the singleton IDashboardService is the same
        // instance that backs the /dashboard endpoint.
        await using var application = new OrionWorkerApplication();
        var dashboardService = application.Services.GetRequiredService<IDashboardService>();

        // Act — record 3 documents via the service, then query the HTTP endpoint.
        dashboardService.RecordDocumentProcessed();
        dashboardService.RecordDocumentProcessed();
        dashboardService.RecordDocumentProcessed();

        using var client = application.CreateClient();
        var response = await client.GetAsync("/dashboard");
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<DashboardStats>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert — proves the HTTP endpoint reflects the recorded count (not always zero).
        stats.ShouldNotBeNull();
        stats.DocumentsProcessed.ShouldBe(3);
    }
}