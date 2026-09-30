using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using System.Net;
using Xunit;

namespace ExxerCube.Prisma.Reconciliator.Worker.Tests;

/// <summary>
/// End-to-end readiness probe tests for the Reconciliator Worker (MVP-PATH 4.2 / E1-S4).
/// </summary>
/// <remarks>
/// Primary acceptance criteria:
/// - <c>/health/ready</c> returns HTTP 503 while the Reconciliation pipeline has not subscribed (IsReady = false).
/// - <c>/health/ready</c> returns HTTP 200 once the pipeline is subscribed (IsReady = true).
/// - <c>/health/live</c> always returns HTTP 200 regardless of readiness state.
/// - <c>/health</c> returns HTTP 503 (Degraded) when not ready, HTTP 200 when ready.
/// </remarks>
public sealed class ReconciliatorReadinessProbeTests
{
    /// <summary>
    /// /health/ready must return 503 when the Reconciliation pipeline is not yet started (IsReady = false).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthReady_PipelineNotStarted_Returns503()
    {
        await using var application = new ReconciliatorProbeApplication(isReady: false);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,
            "/health/ready must return 503 when the Reconciliation pipeline has not subscribed. " +
            "If this returns 200, the probe is hardcoded instead of driven by IReadinessProbe.");
    }

    /// <summary>
    /// /health/ready must return 200 once the Reconciliation pipeline is subscribed (IsReady = true).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthReady_PipelineStarted_Returns200()
    {
        await using var application = new ReconciliatorProbeApplication(isReady: true);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "/health/ready must return 200 when the Reconciliation pipeline is subscribed.");
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Healthy", Case.Insensitive);
    }

    /// <summary>
    /// /health/live must always return 200 regardless of the pipeline readiness state.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthLive_Always_Returns200()
    {
        await using var application = new ReconciliatorProbeApplication(isReady: false);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "/health/live must always return 200 — it is a liveness (process-alive) check, not a readiness check.");
    }

    /// <summary>
    /// /health returns 503 (Degraded) when the pipeline is not started.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_WhenNotReady_Returns503()
    {
        await using var application = new ReconciliatorProbeApplication(isReady: false);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,
            "/health must return 503 when the Reconciliation pipeline has not subscribed.");
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Degraded", Case.Insensitive);
    }

    /// <summary>
    /// /health returns 200 when the pipeline is ready.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_WhenReady_Returns200()
    {
        await using var application = new ReconciliatorProbeApplication(isReady: true);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "/health must return 200 when the Reconciliation pipeline is subscribed.");
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Healthy", Case.Insensitive);
    }
}

/// <summary>
/// Specialized test application factory that replaces <see cref="IReadinessProbe"/> with a configurable
/// substitute so that probe endpoint behaviour (503 / 200) can be asserted without starting the real pipeline.
/// </summary>
internal sealed class ReconciliatorProbeApplication
    : WebApplicationFactory<global::Prisma.Reconciliator.Worker.Program>
{
    private readonly bool _isReady;

    /// <param name="isReady">Controls <see cref="IReadinessProbe.IsReady"/> reported to the health endpoints.</param>
    internal ReconciliatorProbeApplication(bool isReady) => _isReady = isReady;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Minimal ProcessIdentity config so AddProcessIdentity does not throw on startup.
                ["ProcessIdentity:JwtSecret"] = "RECONCILIATOR-WORKER-TESTS-JWT-SECRET-FOR-TESTS-ONLY-32+",
                ["ProcessIdentity:JwtIssuer"] = "prisma-pipeline",
                ["ProcessIdentity:JwtAudience"] = "prisma-pipeline",
                ["ProcessIdentity:TokenLifetime"] = "00:05:00",
                ["ProcessIdentity:Clearance"] = "Reconcile",
                ["Siara:Actor:ActorId"] = "reconciliator-probe-test",
                ["Siara:Actor:DisplayName"] = "Reconciliator Probe (Test)",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all real infrastructure registrations that have external dependencies.
            services.RemoveAll<IEventPublisher>();
            services.RemoveAll<IFileClassifier>();
            services.RemoveAll<IExpedienteHandoffStore>();

            // Replace IReadinessProbe with a stub that reports the controlled isReady value.
            // This directly tests the health-endpoint wiring without requiring a running pipeline.
            services.RemoveAll<IReadinessProbe>();

            var stubProbe = Substitute.For<IReadinessProbe>();
            stubProbe.IsReady.Returns(_isReady);
            services.AddSingleton(stubProbe);

            // Stub remaining infrastructure so the DI graph resolves.
            var mockEventPublisher = Substitute.For<IEventPublisher>();
            mockEventPublisher
                .GetEventStream<ExxerCube.Prisma.Domain.Events.ExtractionCompletedEvent>()
                .Returns(System.Reactive.Linq.Observable
                    .Empty<ExxerCube.Prisma.Domain.Events.ExtractionCompletedEvent>());

            services.AddSingleton(mockEventPublisher);
            services.AddSingleton(Substitute.For<IFileClassifier>());
            services.AddSingleton(Substitute.For<IExpedienteHandoffStore>());
        });
    }
}
