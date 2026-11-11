using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using IndFusion.Ember.Abstractions.Hubs;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Prisma.Orion.Worker.Ingestion;

/// <summary>
/// The real IndFusion.Ember transport for <see cref="DocumentDownloadedEvent"/> (MVP-PATH 1.3), replacing
/// <c>StubExxerHub</c>. It broadcasts events from the Orion <em>Downloader</em> actor to every connected
/// <em>Extractor</em> (Athena) over SignalR, on the Ember <c>"ReceiveMessage"</c> protocol.
/// </summary>
/// <remarks>
/// <para>
/// Outbound sends go through <see cref="IHubContext{THub}"/>, <strong>not</strong> a hub instance:
/// <see cref="ExxerHub{T}"/> derives from <see cref="Hub"/>, whose <c>Clients</c> property is only populated
/// by the SignalR runtime during an inbound invocation — a hub resolved from DI to send outbound has a null
/// <c>Clients</c> and fails. The hub context is the supported way to broadcast from outside the hub.
/// </para>
/// <para>
/// MVP-PATH 1.5 (A5): before broadcasting, a short-lived process clearance token is minted via
/// <see cref="IProcessClearanceTokenService"/> using the actor resolved from
/// <see cref="ISiaraActorIdentityProvider"/> and stamped on the event as <c>ClearanceToken</c>. If
/// minting fails (misconfigured secret, cancelled), the event is <strong>not</strong> sent (fail-closed).
/// </para>
/// <para>
/// Railway-Oriented: every method returns <see cref="Result"/> and never throws for transport outcomes; a
/// failed broadcast is a failure result the caller logs and continues on (event delivery must never break
/// ingestion). The <c>"ReceiveMessage"</c> method name and payload shape match <see cref="ExxerHub{T}"/> so
/// a plain SignalR client (Athena) receives with <c>connection.On&lt;DocumentDownloadedEvent&gt;("ReceiveMessage", …)</c>.
/// </para>
/// </remarks>
public sealed class SignalRIngestionBroadcaster : IExxerHub<DocumentDownloadedEvent>
{
    private const string ReceiveMessage = "ReceiveMessage";

    private readonly IHubContext<IngestionHub> _hubContext;
    private readonly IProcessClearanceTokenService _clearanceTokenService;
    private readonly ISiaraActorIdentityProvider _actorIdentityProvider;
    private readonly ProcessIdentityOptions _processIdentityOptions;
    private readonly ILogger<SignalRIngestionBroadcaster> _logger;

    /// <summary>Initializes a new instance of the <see cref="SignalRIngestionBroadcaster"/> class.</summary>
    /// <param name="hubContext">The SignalR hub context used to broadcast from outside the hub.</param>
    /// <param name="clearanceTokenService">Mints the per-document process clearance token (A5).</param>
    /// <param name="actorIdentityProvider">Resolves the Orion Downloader actor identity for the token.</param>
    /// <param name="processIdentityOptions">
    /// The per-process identity options; <see cref="ProcessIdentityOptions.Clearance"/> determines which
    /// clearance level is stamped on outbound tokens — config-driven (Orion appsettings sets
    /// <see cref="ProcessClearance.Download"/>).
    /// </param>
    /// <param name="logger">The logger instance.</param>
    public SignalRIngestionBroadcaster(
        IHubContext<IngestionHub> hubContext,
        IProcessClearanceTokenService clearanceTokenService,
        ISiaraActorIdentityProvider actorIdentityProvider,
        IOptions<ProcessIdentityOptions> processIdentityOptions,
        ILogger<SignalRIngestionBroadcaster> logger)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(clearanceTokenService);
        ArgumentNullException.ThrowIfNull(actorIdentityProvider);
        ArgumentNullException.ThrowIfNull(processIdentityOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _hubContext = hubContext;
        _clearanceTokenService = clearanceTokenService;
        _actorIdentityProvider = actorIdentityProvider;
        _processIdentityOptions = processIdentityOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> SendToAllAsync(DocumentDownloadedEvent data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (data is null)
        {
            return Result.WithFailure("Event data cannot be null");
        }

        // MVP-PATH 1.5 (A5): mint a clearance token and stamp it on the event before broadcasting.
        var stampResult = await MintAndStampAsync(data, cancellationToken).ConfigureAwait(false);
        if (!stampResult.IsSuccess)
        {
            _logger.LogWarning(
                "Clearance token minting failed for DocumentDownloadedEvent {FileId} — event will not be sent. Reason: {Reason}",
                data.FileId,
                string.Join("; ", stampResult.Errors));
            return Result.WithFailure(string.Join("; ", stampResult.Errors));
        }

        var stampedData = stampResult.Value!;

        try
        {
            await _hubContext.Clients.All.SendAsync(ReceiveMessage, stampedData, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Broadcast DocumentDownloadedEvent {FileId} (corr {CorrelationId}) to all ingestion subscribers",
                stampedData.FileId,
                stampedData.CorrelationId);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast DocumentDownloadedEvent {FileId}", data.FileId);
            return Result.WithFailure($"Failed to broadcast event: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SendToClientAsync(string connectionId, DocumentDownloadedEvent data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Result.WithFailure("Connection ID cannot be null or empty");
        }

        if (data is null)
        {
            return Result.WithFailure("Event data cannot be null");
        }

        var stampResult = await MintAndStampAsync(data, cancellationToken).ConfigureAwait(false);
        if (!stampResult.IsSuccess)
        {
            _logger.LogWarning(
                "Clearance token minting failed for DocumentDownloadedEvent {FileId} — event will not be sent. Reason: {Reason}",
                data.FileId,
                string.Join("; ", stampResult.Errors));
            return Result.WithFailure(string.Join("; ", stampResult.Errors));
        }

        var stampedData = stampResult.Value!;

        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(ReceiveMessage, stampedData, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send DocumentDownloadedEvent to client {ConnectionId}", connectionId);
            return Result.WithFailure($"Failed to send event to client {connectionId}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SendToGroupAsync(string groupName, DocumentDownloadedEvent data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Result.WithFailure("Group name cannot be null or empty");
        }

        if (data is null)
        {
            return Result.WithFailure("Event data cannot be null");
        }

        var stampResult = await MintAndStampAsync(data, cancellationToken).ConfigureAwait(false);
        if (!stampResult.IsSuccess)
        {
            _logger.LogWarning(
                "Clearance token minting failed for DocumentDownloadedEvent {FileId} — event will not be sent. Reason: {Reason}",
                data.FileId,
                string.Join("; ", stampResult.Errors));
            return Result.WithFailure(string.Join("; ", stampResult.Errors));
        }

        var stampedData = stampResult.Value!;

        try
        {
            await _hubContext.Clients.Group(groupName).SendAsync(ReceiveMessage, stampedData, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send DocumentDownloadedEvent to group {GroupName}", groupName);
            return Result.WithFailure($"Failed to send event to group {groupName}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default) =>
        // SignalR exposes no direct connection count; tracking would require OnConnected/OnDisconnected
        // bookkeeping in the hub, which the ingestion edge does not need. Matches ExxerHub<T>'s contract.
        Task.FromResult(cancellationToken.IsCancellationRequested
            ? ResultExtensions.Cancelled<int>()
            : Result<int>.WithFailure("Connection count tracking not implemented"));

    // ── Private helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the current actor, mints a clearance token, and returns the event stamped with it.
    /// Returns a failure result (never throws) if actor resolution or minting fails.
    /// </summary>
    private async Task<Result<DocumentDownloadedEvent>> MintAndStampAsync(
        DocumentDownloadedEvent data,
        CancellationToken cancellationToken)
    {
        var actorResult = await _actorIdentityProvider.GetCurrentActorAsync(cancellationToken).ConfigureAwait(false);
        if (!actorResult.IsSuccess)
        {
            return Result<DocumentDownloadedEvent>.WithFailure(
                $"Actor identity resolution failed: {string.Join("; ", actorResult.Errors)}");
        }

        var mintResult = await _clearanceTokenService.MintAsync(
            actorResult.Value!,
            _processIdentityOptions.Clearance,
            data.FileId,
            cancellationToken).ConfigureAwait(false);

        if (!mintResult.IsSuccess)
        {
            return Result<DocumentDownloadedEvent>.WithFailure(
                $"Clearance token minting failed: {string.Join("; ", mintResult.Errors)}");
        }

        return Result<DocumentDownloadedEvent>.Success(data with { ClearanceToken = mintResult.Value! });
    }
}
