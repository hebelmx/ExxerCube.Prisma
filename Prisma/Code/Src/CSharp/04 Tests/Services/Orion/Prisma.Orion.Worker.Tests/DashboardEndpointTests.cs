using Microsoft.AspNetCore.Mvc.Testing;
using Prisma.Orion.HealthChecks;
using Prisma.Orion.Worker;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace Prisma.Orion.Worker.Tests;

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
}

/// <summary>
/// Dashboard statistics DTO for testing.
/// </summary>
public record DashboardStats
{
    public string WorkerName { get; init; } = string.Empty;
    public int DocumentsProcessed { get; init; }
    public DateTime? LastEventTime { get; init; }
    public DateTime? LastHeartbeat { get; init; }
    public int QueueDepth { get; init; }
    public string Status { get; init; } = string.Empty;
}
