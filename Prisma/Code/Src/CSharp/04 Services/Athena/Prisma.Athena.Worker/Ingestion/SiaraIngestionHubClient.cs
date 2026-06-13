using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Events;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prisma.Athena.Processing.Ingestion;

namespace Prisma.Athena.Worker.Ingestion;

/// <summary>
/// The Athena <em>Extractor</em> actor's subscriber to the Orion <em>Downloader</em> actor (MVP-PATH 1.3,
/// the first cross-process edge of the Three-Actors split — ADR-009/ADR-011). It opens a SignalR connection
/// to the Orion ingestion hub, listens for <see cref="DocumentDownloadedEvent"/> on the Ember
/// <c>"ReceiveMessage"</c> protocol, and hands each one to the <see cref="IngestionEventForwarder"/> which
/// republishes it onto Athena's local event stream so the processing pipeline runs.
/// </summary>
/// <remarks>
/// Resilient by construction: it retries the initial connect while the hub is unreachable and uses SignalR
/// automatic reconnect for drops thereafter, so a transient Orion outage never crashes Athena. When no hub
/// URL is configured the subscriber stays idle (logged), so a single-service or test deployment boots
/// cleanly.
/// </remarks>
public sealed class SiaraIngestionHubClient : BackgroundService
{
    private readonly IngestionClientOptions _options;
    private readonly IngestionEventForwarder _forwarder;
    private readonly ILogger<SiaraIngestionHubClient> _logger;
    private HubConnection? _connection;

    /// <summary>Initializes a new instance of the <see cref="SiaraIngestionHubClient"/> class.</summary>
    /// <param name="options">The subscriber options (hub URL + reconnect cadence).</param>
    /// <param name="forwarder">The forwarder that republishes received events onto the local stream.</param>
    /// <param name="logger">The logger.</param>
    public SiaraIngestionHubClient(
        IOptions<IngestionClientOptions> options,
        IngestionEventForwarder forwarder,
        ILogger<SiaraIngestionHubClient> logger)
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
                "No ingestion hub URL configured (Ingestion:HubUrl); the Athena ingestion subscriber is idle.");
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Receive on the Ember protocol method name and republish onto the local pipeline.
        _connection.On<DocumentDownloadedEvent>("ReceiveMessage", async evt => await _forwarder.ForwardAsync(evt).ConfigureAwait(false));

        await ConnectWithRetryAsync(stoppingToken).ConfigureAwait(false);

        // Stay alive until shutdown; automatic reconnect keeps the connection healthy across drops.
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
                _logger.LogInformation("Connected to the Orion ingestion hub at {HubUrl}", _options.HubUrl);
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
                    "Could not reach the Orion ingestion hub at {HubUrl}; retrying in {Delay}",
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
