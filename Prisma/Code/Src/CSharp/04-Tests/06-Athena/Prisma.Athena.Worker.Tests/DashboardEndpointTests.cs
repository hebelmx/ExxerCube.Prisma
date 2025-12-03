using Microsoft.AspNetCore.Mvc.Testing;
using Prisma.Athena.HealthChecks;
using Prisma.Athena.Worker;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace Prisma.Athena.Worker.Tests;

/// <summary>
/// TDD tests for Athena Worker dashboard endpoints (stats/metrics).
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
        await using var application = new AthenaWorkerApplication();
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
        await using var application = new AthenaWorkerApplication();
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
        stats.WorkerName.ShouldBe("Athena Processing Worker");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Dashboard_IncludesLastHeartbeat()
    {
        // Arrange
        await using var application = new AthenaWorkerApplication();
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
        await using var application = new AthenaWorkerApplication();
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
        await using var application = new AthenaWorkerApplication();
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
}
