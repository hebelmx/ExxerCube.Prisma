using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Events;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prisma.Athena.Processing.Reconciliation;

namespace Prisma.Reconciliator.Worker;

/// <summary>
/// The Reconciliator actor's subscriber to the Athena Extractor actor (MVP-PATH 1.4 Reconciliator edge, the
/// second cross-process edge of the Three-Actors split — ADR-009/ADR-011). It opens a SignalR connection to the
/// Athena reconciliation hub, listens for <see cref="ExtractionCompletedEvent"/> on the Ember
/// <c>"ReceiveMessage"</c> protocol, and hands each one to the <see cref="ReconciliationEventForwarder"/> which
/// republishes it onto the local event stream so the Reconciliator pipeline runs. Mirrors the 1.3
/// <c>SiaraIngestionHubClient</c>.
/// </summary>
/// <remarks>
/// Resilient by construction: it retries the initial connect while the hub is unreachable and uses SignalR
/// automatic reconnect for drops thereafter, so a transient Extractor outage never crashes the Reconciliator.
/// When no hub URL is configured the subscriber stays idle (logged), so a single-service or test deployment
/// boots cleanly.
/// </remarks>
public sealed class ReconciliationHubClient : BackgroundService
{
    private readonly ReconciliationClientOptions _options;
    private readonly ReconciliationEventForwarder _forwarder;
    private readonly ILogger<ReconciliationHubClient> _logger;
    private HubConnection? _connection;

    /// <summary>Initializes a new instance of the <see cref="ReconciliationHubClient"/> class.</summary>
    /// <param name="options">The subscriber options (hub URL + reconnect cadence).</param>
    /// <param name="forwarder">The forwarder that republishes received events onto the local stream.</param>
    /// <param name="logger">The logger.</param>
    public ReconciliationHubClient(
        IOptions<ReconciliationClientOptions> options,
        ReconciliationEventForwarder forwarder,
        ILogger<ReconciliationHubClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.HubUrl))
        {
            _logger.LogWarning(
                "No reconciliation hub URL configured (Reconciliation:HubUrl); the Reconciliator subscriber is idle.");
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Receive on the Ember protocol method name and republish onto the local pipeline.
        _connection.On<ExtractionCompletedEvent>("ReceiveMessage", evt => _forwarder.Forward(evt));

        await ConnectWithRetryAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _connection!.State != HubConnectionState.Connected)
        {
            try
            {
                await _connection.StartAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("Connected to the Athena reconciliation hub at {HubUrl}", _options.HubUrl);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not reach the Athena reconciliation hub at {HubUrl}; retrying in {Delay}",
                    _options.HubUrl,
                    _options.ReconnectDelay);

                try
                {
                    await Task.Delay(_options.ReconnectDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
