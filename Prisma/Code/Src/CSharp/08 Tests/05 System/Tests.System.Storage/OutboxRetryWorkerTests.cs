// <copyright file="OutboxRetryWorkerTests.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Infrastructure.Database;
using ExxerCube.Prisma.Infrastructure.Database.Services;
using IndQuestResults.Operations;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// Tests for <see cref="OutboxRetryWorker"/> — the event-processing reliability outbox/retry
/// background worker (NFR14).
/// <para>
/// Uses EF Core InMemory for the outbox store and NSubstitute for <see cref="IEventPublisher"/>.
/// No Testcontainers SQL Server required for these fast unit-level scenarios.
/// </para>
/// </summary>
public sealed class OutboxRetryWorkerTests : IDisposable
{
    private const int TestMaxRetries = 3;

    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OutboxRetryWorker> _logger;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxRetryWorkerTests"/> class.
    /// </summary>
    public OutboxRetryWorkerTests(ITestOutputHelper output)
    {
        _output = output;

        // Use a uniquely-named InMemory database per test class instance so tests are isolated.
        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase($"OutboxRetry_{Guid.NewGuid()}")
            .Options;

        _eventPublisher = Substitute.For<IEventPublisher>();
        _logger = XUnitLogger.CreateLogger<OutboxRetryWorker>(output);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private OutboxRetryWorker CreateWorker(int maxRetries = TestMaxRetries, TimeSpan? scanInterval = null)
    {
        var options = Options.Create(new OutboxRetryOptions
        {
            MaxRetries = maxRetries,
            ScanInterval = scanInterval ?? TimeSpan.FromSeconds(60), // Never fires in tests
            BatchSize = 50,
        });

        var services = new ServiceCollection();
        services.AddSingleton<IPrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        var sp = services.BuildServiceProvider();

        return new OutboxRetryWorker(
            _eventPublisher,
            sp.GetRequiredService<IServiceScopeFactory>(),
            _logger,
            options);
    }

    private PrismaDbContext OpenContext() => new(_dbOptions);

    /// <summary>
    /// Inserts a raw pending outbox event (simulating a dropped/failed event that was never
    /// processed by the EventPersistenceWorker — ProcessedAt remains null).
    /// </summary>
    private async Task<OutboxEvent> SeedPendingOutboxEventAsync(DomainEvent domainEvent)
    {
        using var ctx = OpenContext();
        var entry = new OutboxEvent
        {
            OutboxEventId = domainEvent.EventId,
            EventType = domainEvent.EventType,
            Payload = JsonSerializer.Serialize(domainEvent, JsonOptions),
            OccurredAt = domainEvent.Timestamp,
            ProcessedAt = null, // not yet processed — the "dropped event" scenario
            RetryCount = 0,
        };
        ctx.OutboxEvents.Add(entry);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry;
    }

    // -------------------------------------------------------------------------
    // Core DoD test: detect + persist + re-raise
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves the DoD scenario: a domain event that was persisted to the outbox but never
    /// acknowledged as processed (ProcessedAt = null, simulating a dropped subscription or
    /// transient DB failure) is detected by the worker, re-published via the event publisher,
    /// and marked as processed.
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_DroppedEvent_IsDetectedAndRePublished()
    {
        // Arrange — seed a pending (unprocessed) outbox entry
        var droppedEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(DocumentDownloadedEvent),
            CorrelationId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FileName = "dropped.pdf",
            Source = "SIARA",
        };

        await SeedPendingOutboxEventAsync(droppedEvent);

        var worker = CreateWorker();

        // Act — run one scan tick
        var result = await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);

        // Assert — worker returned success and processed 1 event
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);

        // The event was re-published via the IEventPublisher
        _eventPublisher.Received(1).Publish(Arg.Any<DomainEvent>());

        // The outbox entry is now marked as processed
        using var ctx = OpenContext();
        var persisted = await ctx.OutboxEvents
            .SingleAsync(e => e.OutboxEventId == droppedEvent.EventId,
                TestContext.Current.CancellationToken);

        persisted.ProcessedAt.ShouldNotBeNull(
            "OutboxRetryWorker must stamp ProcessedAt after a successful re-publish");
        persisted.RetryCount.ShouldBe(0, "no failure occurred so RetryCount stays at 0");
        persisted.DeadLetteredAt.ShouldBeNull("event was recovered, not dead-lettered");
    }

    // -------------------------------------------------------------------------
    // Dead-letter cap test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves that after MaxRetries failed re-publish attempts the outbox entry is
    /// moved to dead-letter status and no further re-publish attempts are made.
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_PublisherThrowsRepeatedly_EventIsDeadLetteredAfterMaxRetries()
    {
        // Arrange — make the publisher throw on every attempt
        _eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(_ => throw new InvalidOperationException("Simulated downstream failure"));

        var failingEvent = new OcrCompletedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(OcrCompletedEvent),
            CorrelationId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            OcrEngine = "Tesseract",
            Confidence = 80m,
        };

        await SeedPendingOutboxEventAsync(failingEvent);

        var worker = CreateWorker(maxRetries: TestMaxRetries);

        // Act — run MaxRetries ticks to exhaust the retry budget
        for (var i = 0; i < TestMaxRetries; i++)
        {
            await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);
        }

        // Assert — after MaxRetries failed ticks the event is dead-lettered
        using var ctx = OpenContext();
        var persisted = await ctx.OutboxEvents
            .SingleAsync(e => e.OutboxEventId == failingEvent.EventId,
                TestContext.Current.CancellationToken);

        persisted.DeadLetteredAt.ShouldNotBeNull(
            "event must be dead-lettered after MaxRetries failed attempts");
        persisted.ProcessedAt.ShouldBeNull(
            "event was never successfully processed");
        persisted.RetryCount.ShouldBe(TestMaxRetries);
        persisted.LastError.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Proves that once an event is dead-lettered the worker does NOT re-attempt it
    /// on subsequent ticks (DeadLetteredAt acts as a final exclusion filter).
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_DeadLetteredEvent_IsNotRetried()
    {
        // Arrange — insert an event already in dead-letter state
        using (var ctx = OpenContext())
        {
            ctx.OutboxEvents.Add(new OutboxEvent
            {
                OutboxEventId = Guid.NewGuid(),
                EventType = nameof(DocumentDownloadedEvent),
                Payload = "{}",
                OccurredAt = DateTime.UtcNow.AddMinutes(-10),
                RetryCount = TestMaxRetries,
                DeadLetteredAt = DateTime.UtcNow.AddMinutes(-1),
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var worker = CreateWorker(maxRetries: TestMaxRetries);

        // Act
        var result = await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);

        // Assert — worker found nothing to process (dead-lettered rows are filtered out)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
        _eventPublisher.DidNotReceive().Publish(Arg.Any<DomainEvent>());
    }

    // -------------------------------------------------------------------------
    // Already-processed event must not be re-raised
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves that an outbox entry with ProcessedAt already set is not re-published
    /// (idempotency guard — the happy path that EventPersistenceWorker stamps).
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_AlreadyProcessedEvent_IsNotRePublished()
    {
        // Arrange — insert an event that was already processed
        using (var ctx = OpenContext())
        {
            ctx.OutboxEvents.Add(new OutboxEvent
            {
                OutboxEventId = Guid.NewGuid(),
                EventType = nameof(OcrCompletedEvent),
                Payload = "{}",
                OccurredAt = DateTime.UtcNow.AddMinutes(-5),
                ProcessedAt = DateTime.UtcNow.AddMinutes(-4),
                RetryCount = 0,
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var worker = CreateWorker();

        // Act
        var result = await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
        _eventPublisher.DidNotReceive().Publish(Arg.Any<DomainEvent>());
    }

    // -------------------------------------------------------------------------
    // Empty outbox — no-op
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves the worker returns success with 0 when there are no pending outbox events.
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_EmptyOutbox_ReturnsZeroProcessed()
    {
        var worker = CreateWorker();
        var result = await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
        _eventPublisher.DidNotReceive().Publish(Arg.Any<DomainEvent>());
    }

    // -------------------------------------------------------------------------
    // Cancellation handling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves that passing a pre-cancelled token returns a cancelled Result immediately
    /// without querying the database.
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var worker = CreateWorker();
        var result = await worker.ScanAndRetryAsync(cts.Token);

        result.IsCancelled().ShouldBeTrue();
        _eventPublisher.DidNotReceive().Publish(Arg.Any<DomainEvent>());
    }

    // -------------------------------------------------------------------------
    // Multiple pending events — all recovered in one tick
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves that all pending outbox events are recovered (not just the first) in a
    /// single scan tick, and that processed events are stamped correctly.
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_MultiplePendingEvents_AllRePublished()
    {
        var correlationId = Guid.NewGuid();

        // Seed 3 pending events
        var events = new[]
        {
            new DocumentDownloadedEvent
            {
                EventId = Guid.NewGuid(),
                EventType = nameof(DocumentDownloadedEvent),
                CorrelationId = correlationId,
                FileId = Guid.NewGuid(),
                FileName = "file1.pdf",
                Source = "SIARA",
            },
            new DocumentDownloadedEvent
            {
                EventId = Guid.NewGuid(),
                EventType = nameof(DocumentDownloadedEvent),
                CorrelationId = correlationId,
                FileId = Guid.NewGuid(),
                FileName = "file2.pdf",
                Source = "SIARA",
            },
            new DocumentDownloadedEvent
            {
                EventId = Guid.NewGuid(),
                EventType = nameof(DocumentDownloadedEvent),
                CorrelationId = correlationId,
                FileId = Guid.NewGuid(),
                FileName = "file3.pdf",
                Source = "SIARA",
            },
        };

        foreach (var ev in events)
            await SeedPendingOutboxEventAsync(ev);

        var worker = CreateWorker();
        var result = await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);

        _eventPublisher.Received(3).Publish(Arg.Any<DomainEvent>());

        // All entries processed
        using var ctx = OpenContext();
        var all = await ctx.OutboxEvents.ToListAsync(TestContext.Current.CancellationToken);
        all.ShouldAllBe(e => e.ProcessedAt.HasValue);
    }

    // -------------------------------------------------------------------------
    // Retry count increments on transient failure, then recovers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves that when the publisher throws on the first attempt but succeeds on
    /// the second, the outbox entry ends up marked as processed with RetryCount = 1.
    /// </summary>
    [Fact]
    public async Task ScanAndRetry_TransientFailureThenSuccess_EventIsRecoveredAfterRetry()
    {
        var transientEvent = new ClassificationCompletedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(ClassificationCompletedEvent),
            CorrelationId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            RequirementTypeId = 5,
            RequirementTypeName = "Aseguramiento",
            Confidence = Confidence.FromInt(90),
        };

        await SeedPendingOutboxEventAsync(transientEvent);

        // First call throws; second call succeeds (default NSubstitute behavior after exception)
        var callCount = 0;
        _eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(_ =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Transient failure");
                // second call succeeds (no throw)
            });

        var worker = CreateWorker(maxRetries: TestMaxRetries);

        // First tick — fails
        var tick1 = await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);
        tick1.IsSuccess.ShouldBeTrue(); // worker tick itself succeeded (defensive)
        tick1.Value.ShouldBe(0);        // but 0 events actually processed

        // Verify retry count was incremented
        using (var ctx = OpenContext())
        {
            var entry = await ctx.OutboxEvents
                .SingleAsync(e => e.OutboxEventId == transientEvent.EventId,
                    TestContext.Current.CancellationToken);
            entry.RetryCount.ShouldBe(1);
            entry.ProcessedAt.ShouldBeNull();
            entry.DeadLetteredAt.ShouldBeNull();
        }

        // Second tick — succeeds
        var tick2 = await worker.ScanAndRetryAsync(TestContext.Current.CancellationToken);
        tick2.IsSuccess.ShouldBeTrue();
        tick2.Value.ShouldBe(1);

        using (var ctx = OpenContext())
        {
            var entry = await ctx.OutboxEvents
                .SingleAsync(e => e.OutboxEventId == transientEvent.EventId,
                    TestContext.Current.CancellationToken);
            entry.ProcessedAt.ShouldNotBeNull("recovered on second attempt");
            entry.RetryCount.ShouldBe(1);
            entry.DeadLetteredAt.ShouldBeNull();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // InMemory DbContext is garbage-collected; no explicit dispose needed.
    }
}
