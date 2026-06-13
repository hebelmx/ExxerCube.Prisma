using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
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
/// <para>
/// Resilient by construction: it retries the initial connect while the hub is unreachable and uses SignalR
/// automatic reconnect for drops thereafter, so a transient Orion outage never crashes Athena. When no hub
/// URL is configured the subscriber stays idle (logged), so a single-service or test deployment boots
/// cleanly.
/// </para>
/// <para>
/// Connection-level auth (follow-up to MVP-PATH 1.5): a short-lived connection-scoped clearance token
/// (<c>fileId = Guid.Empty</c> as the connection-scope sentinel) is minted before each connect attempt and
/// supplied via the SignalR <c>access_token</c> query-string hook. If minting fails the connection is not
/// attempted (fail-closed). The Orion hub validates the token and requires
/// <c>ProcessClearance.Extract</c> — only the Athena Extractor actor is authorized to connect.
/// </para>
/// </remarks>
public sealed class SiaraIngestionHubClient : BackgroundService
{
    private readonly IngestionClientOptions _options;
    private readonly IngestionEventForwarder _forwarder;
    private readonly IProcessClearanceTokenService _clearanceTokenService;
    private readonly ISiaraActorIdentityProvider _actorIdentityProvider;
    private readonly ProcessIdentityOptions _processIdentityOptions;
    private readonly ILogger<SiaraIngestionHubClient> _logger;
    private HubConnection? _connection;

    /// <summary>Initializes a new instance of the <see cref="SiaraIngestionHubClient"/> class.</summary>
    /// <param name="options">The subscriber options (hub URL + reconnect cadence).</param>
    /// <param name="forwarder">The forwarder that republishes received events onto the local stream.</param>
    /// <param name="clearanceTokenService">Mints the connection-scoped bearer token for hub auth.</param>
    /// <param name="actorIdentityProvider">Resolves the Athena Extractor actor identity for token minting.</param>
    /// <param name="processIdentityOptions">The per-process identity options (clearance level etc.).</param>
    /// <param name="logger">The logger.</param>
    public SiaraIngestionHubClient(
        IOptions<IngestionClientOptions> options,
        IngestionEventForwarder forwarder,
        IProcessClearanceTokenService clearanceTokenService,
        ISiaraActorIdentityProvider actorIdentityProvider,
        IOptions<ProcessIdentityOptions> processIdentityOptions,
        ILogger<SiaraIngestionHubClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clearanceTokenService);
        ArgumentNullException.ThrowIfNull(actorIdentityProvider);
        ArgumentNullException.ThrowIfNull(processIdentityOptions);
        _options = options.Value;
        _forwarder = forwarder ?? throw new ArgumentNullException(nameof(forwarder));
        _clearanceTokenService = clearanceTokenService;
        _actorIdentityProvider = actorIdentityProvider;
        _processIdentityOptions = processIdentityOptions.Value;
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
            .WithUrl(_options.HubUrl, connectionOptions =>
            {
                // Connection-level auth: mint a fresh token each time SignalR needs one (initial connect
                // and automatic reconnect). Guid.Empty is the connection-scope sentinel (no per-document
                // binding at connect time — the per-message clearance token on the event is the
                // document-level binding). Fail-closed: return null on any error so SignalR does not
                // connect without a valid token.
                connectionOptions.AccessTokenProvider = async () =>
                {
                    var actorResult = await _actorIdentityProvider
                        .GetCurrentActorAsync(stoppingToken).ConfigureAwait(false);

                    if (!actorResult.IsSuccess)
                    {
                        _logger.LogWarning(
                            "Could not resolve Athena actor identity for ingestion hub connection token; connection will not proceed. Reason: {Reason}",
                            string.Join("; ", actorResult.Errors));
                        return null;
                    }

                    var mintResult = await _clearanceTokenService.MintAsync(
                        actorResult.Value!,
                        _processIdentityOptions.Clearance,
                        Guid.Empty,   // connection-scope sentinel
                        stoppingToken).ConfigureAwait(false);

                    if (!mintResult.IsSuccess)
                    {
                        _logger.LogWarning(
                            "Could not mint connection clearance token for ingestion hub; connection will not proceed. Reason: {Reason}",
                            string.Join("; ", mintResult.Errors));
                        return null;
                    }

                    return mintResult.Value;
                };

                // Optional handler factory (non-null in tests to route through an in-memory TestServer;
                // null in production so SignalR uses its default real TCP handler).
                if (_options.HttpMessageHandlerFactory is not null)
                {
                    connectionOptions.HttpMessageHandlerFactory = _ => _options.HttpMessageHandlerFactory();
                }
            })
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
