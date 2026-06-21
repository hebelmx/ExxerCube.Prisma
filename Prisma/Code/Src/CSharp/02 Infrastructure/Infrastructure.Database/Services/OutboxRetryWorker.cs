// <copyright file="OutboxRetryWorker.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Infrastructure.Database.Services;

/// <summary>
/// Background service that implements the event-processing reliability outbox pattern (NFR14).
/// <para>
/// On each scan tick the worker:
/// <list type="number">
///   <item><description>Queries <c>OutboxEvents</c> for rows that are pending (not yet processed, not dead-lettered, below <see cref="MaxRetries"/>).</description></item>
///   <item><description>Deserialises the payload back into a <see cref="DomainEvent"/> and re-publishes it via <see cref="IEventPublisher.Publish{TEvent}"/>.</description></item>
///   <item><description>On success: stamps <c>ProcessedAt = UtcNow</c>.</description></item>
///   <item><description>On failure: increments <c>RetryCount</c> and records <c>LastError</c>.
///   When <c>RetryCount</c> reaches <see cref="MaxRetries"/> the row is moved to dead-letter (<c>DeadLetteredAt = UtcNow</c>) so it is never retried again.</description></item>
/// </list>
/// </para>
/// <para>
/// This worker is intentionally separated from <see cref="EventPersistenceWorker"/>: the persistence
/// worker <em>writes</em> incoming events to the outbox; this worker <em>recovers</em> those that
/// were not confirmed as processed.
/// </para>
/// <para>
/// Schema applied via <c>EnsureCreated</c> / EF migrations — never via live DDL.
/// </para>
/// </summary>
public sealed class OutboxRetryWorker : BackgroundService
{
    /// <summary>
    /// Default maximum number of re-publish attempts before a row is dead-lettered.
    /// Configurable via <see cref="OutboxRetryOptions"/>.
    /// </summary>
    public const int DefaultMaxRetries = 5;

    /// <summary>
    /// Default interval between scan ticks (30 seconds).
    /// </summary>
    public static readonly TimeSpan DefaultScanInterval = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IEventPublisher _eventPublisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRetryWorker> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _scanInterval;
    private readonly int _batchSize;

    /// <summary>
    /// Gets the configured maximum retry count (exposed for testing).
    /// </summary>
    public int MaxRetries => _maxRetries;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxRetryWorker"/> class.
    /// </summary>
    /// <param name="eventPublisher">Event publisher used to re-publish recovered events.</param>
    /// <param name="scopeFactory">Scope factory for creating a scoped <see cref="IPrismaDbContext"/>.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Optional configuration; defaults apply when not provided.</param>
    public OutboxRetryWorker(
        IEventPublisher eventPublisher,
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRetryWorker> logger,
        IOptions<OutboxRetryOptions>? options = null)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var cfg = options?.Value ?? new OutboxRetryOptions();
        _maxRetries = cfg.MaxRetries > 0 ? cfg.MaxRetries : DefaultMaxRetries;
        _scanInterval = cfg.ScanInterval > TimeSpan.Zero ? cfg.ScanInterval : DefaultScanInterval;
        _batchSize = cfg.BatchSize > 0 ? cfg.BatchSize : 50;
    }

    /// <summary>
    /// Executes the scan loop until the host signals cancellation.
    /// </summary>
    /// <param name="stoppingToken">Token signalled when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxRetryWorker started. ScanInterval={ScanInterval}, MaxRetries={MaxRetries}, BatchSize={BatchSize}",
            _scanInterval, _maxRetries, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndRetryAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Defensive Intelligence: log and continue — a single scan failure must not
                // terminate the retry loop.
                _logger.LogError(ex, "OutboxRetryWorker scan tick failed; will retry after interval");
            }

            try
            {
                await Task.Delay(_scanInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxRetryWorker stopped");
    }

    /// <summary>
    /// Performs one scan-and-retry tick: fetches pending outbox events in FIFO order
    /// and attempts to re-publish each one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> wrapping the number of events processed in this tick.</returns>
    public async Task<Result<int>> ScanAndRetryAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<int>();

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IPrismaDbContext>();

        List<OutboxEvent> pending;
        try
        {
            pending = await dbContext.OutboxEvents
                .Where(e =>
                    e.ProcessedAt == null &&
                    e.DeadLetteredAt == null &&
                    e.RetryCount < _maxRetries)
                .OrderBy(e => e.OccurredAt)
                .Take(_batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OutboxRetryWorker: failed to query pending outbox events");
            return Result<int>.WithFailure($"DB query failed: {ex.Message}");
        }

        if (pending.Count == 0)
        {
            _logger.LogDebug("OutboxRetryWorker: no pending events in this tick");
            return Result<int>.WithSuccess(0);
        }

        _logger.LogInformation("OutboxRetryWorker: found {Count} pending outbox event(s) to retry", pending.Count);

        var processed = 0;
        foreach (var outboxEvent in pending)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var result = await ProcessSingleEventAsync(outboxEvent, dbContext, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
                processed++;
        }

        _logger.LogInformation(
            "OutboxRetryWorker: tick complete — {Processed}/{Total} event(s) re-published",
            processed, pending.Count);

        return Result<int>.WithSuccess(processed);
    }

    /// <summary>
    /// Attempts to re-publish a single outbox event and updates its status in the database.
    /// </summary>
    private async Task<Result<bool>> ProcessSingleEventAsync(
        OutboxEvent outboxEvent,
        IPrismaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Deserialise back to the polymorphic DomainEvent hierarchy.
            var domainEvent = JsonSerializer.Deserialize<DomainEvent>(outboxEvent.Payload, JsonOptions);

            if (domainEvent is null)
            {
                var deserialiseError = $"Payload for {outboxEvent.OutboxEventId} deserialised to null — skipping";
                _logger.LogWarning(deserialiseError);
                return await MarkFailedAsync(outboxEvent, dbContext, deserialiseError, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Re-publish via the event stream. Publish<T> is fire-and-forget and never throws.
            _eventPublisher.Publish(domainEvent);

            outboxEvent.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "OutboxRetryWorker: re-published {EventType} ({EventId}) after {Retries} prior attempt(s)",
                outboxEvent.EventType, outboxEvent.OutboxEventId, outboxEvent.RetryCount);

            return Result<bool>.WithSuccess(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "OutboxRetryWorker: re-publish failed for {EventId} (attempt {Attempt}/{Max})",
                outboxEvent.OutboxEventId, outboxEvent.RetryCount + 1, _maxRetries);

            return await MarkFailedAsync(outboxEvent, dbContext, ex.Message, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records a failed attempt; dead-letters the event when the retry ceiling is reached.
    /// </summary>
    private async Task<Result<bool>> MarkFailedAsync(
        OutboxEvent outboxEvent,
        IPrismaDbContext dbContext,
        string error,
        CancellationToken cancellationToken)
    {
        outboxEvent.RetryCount++;
        outboxEvent.LastError = error.Length > 2000 ? error[..2000] : error;

        if (outboxEvent.RetryCount >= _maxRetries)
        {
            outboxEvent.DeadLetteredAt = DateTime.UtcNow;
            _logger.LogError(
                "OutboxRetryWorker: event {EventId} ({EventType}) dead-lettered after {MaxRetries} failed attempt(s). LastError: {Error}",
                outboxEvent.OutboxEventId, outboxEvent.EventType, _maxRetries, outboxEvent.LastError);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception saveEx)
        {
            _logger.LogError(saveEx, "OutboxRetryWorker: failed to persist retry-count update for {EventId}", outboxEvent.OutboxEventId);
        }

        return Result<bool>.WithFailure(error);
    }
}
