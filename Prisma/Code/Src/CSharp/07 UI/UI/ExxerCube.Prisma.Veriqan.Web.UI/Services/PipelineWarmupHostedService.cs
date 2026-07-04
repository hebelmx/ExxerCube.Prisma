using ExxerCube.Prisma.Veriqan.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Warms up the Veriqan pipeline's dependency graph on host start, so the first real demo
/// submission does not pay a cold-start penalty (JIT, reference-data CSV parse, etc.).
/// </summary>
/// <remarks>
/// <para>
/// <b>Never blocks <see cref="StartAsync"/>.</b> The warm-up runs as a fire-and-forget
/// background task on its own scope; failures are logged, never thrown out of
/// <see cref="StartAsync"/> — matching the "readiness probes present but real, never gate host
/// startup" convention already used elsewhere in this repo.
/// </para>
/// </remarks>
public sealed class PipelineWarmupHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPipelineReadiness _readiness;
    private readonly ILogger<PipelineWarmupHostedService> _logger;

    /// <summary>Initializes a new <see cref="PipelineWarmupHostedService"/>.</summary>
    public PipelineWarmupHostedService(
        IServiceScopeFactory scopeFactory,
        IPipelineReadiness readiness,
        ILogger<PipelineWarmupHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The in-flight (or completed) background warm-up task, exposed <c>internal</c> purely so
    /// tests can deterministically await it instead of polling/sleeping. Never awaited by
    /// production code — <see cref="StartAsync"/> remains fire-and-forget.
    /// </summary>
    internal Task? WarmupTask { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Returns immediately — the warm-up itself runs on a detached background task so a slow
    /// or failing warm-up can never delay or fail host startup.
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        WarmupTask = WarmUpAsync(cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            // Light warm-up: resolve the reference-data provider and trigger its bundle lookup
            // for the demo institution/period so the CSV parse + cache load happen once, here,
            // rather than on the first real user submission.
            var referenceDataProvider = scope.ServiceProvider.GetRequiredService<IVecReferenceDataProvider>();
            var warmupKey = new StatementContextKey("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026");
            var bundleResult = await referenceDataProvider.GetBundleAsync(warmupKey, cancellationToken);

            if (bundleResult.IsFailure)
            {
                // Do NOT mark ready: a failed reference-data load means live submissions are
                // doomed, so /health/ready must keep reporting not-ready (the canned fallback
                // still serves demo traffic in the meantime).
                _logger.LogWarning(
                    "Pipeline warm-up: reference-data bundle load failed — live mode is NOT " +
                    "ready (canned fallback will serve until this recovers): {Error}",
                    bundleResult.Error ?? "<none>");
                return;
            }

            _readiness.MarkReady();
            _logger.LogInformation("Pipeline warm-up completed.");
        }
        catch (Exception ex)
        {
            // Never throw out of a hosted service background task — log and move on. The demo
            // still functions in canned mode; live mode stays not-ready until warm-up recovers.
            _logger.LogWarning(ex, "Pipeline warm-up failed (non-fatal); live mode NOT ready.");
        }
    }
}
