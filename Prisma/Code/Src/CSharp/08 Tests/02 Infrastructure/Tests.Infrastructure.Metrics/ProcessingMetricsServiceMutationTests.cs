using System.Collections.Concurrent;
using System.Reflection;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Infrastructure.Metrics;

/// <summary>
/// Mutation-killing exact-value tests for <see cref="ProcessingMetricsService"/> (Unit 36).
/// Focuses on the deterministic surface: field aggregation, throughput maths, statistics
/// roll-ups and performance validation thresholds. Real-elapsed timing values are only
/// asserted for sign/non-negativity (they are not deterministic).
/// </summary>
public class ProcessingMetricsServiceMutationTests
{
    private readonly ILogger<ProcessingMetricsService> _logger;
    private readonly ProcessingMetricsService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingMetricsServiceMutationTests"/> class.
    /// </summary>
    public ProcessingMetricsServiceMutationTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<ProcessingMetricsService>(output);
        _service = new ProcessingMetricsService(_logger, maxConcurrency: 5);
    }

    // ── CompleteProcessingAsync: field/confidence extraction ───────────────────

    /// <summary>
    /// ExtractedFieldCount is the SUM of Fechas and Montos counts — distinct counts pin the
    /// addition (kills <c>+</c>→<c>-</c>/<c>*</c> and a swapped count source).
    /// </summary>
    [Fact]
    public async Task CompleteProcessingAsync_FieldCount_IsFechasPlusMontos()
    {
        var context = await _service.StartProcessingAsync("doc-fc", "p.pdf");

        await _service.CompleteProcessingAsync(context, MakeResult(0.9f, fechas: 3, montos: 2), isSuccess: true);

        var metrics = _service.GetDocumentMetrics("doc-fc");
        metrics.ShouldNotBeNull();
        metrics!.ExtractedFieldCount.ShouldBe(5); // 3 + 2
        metrics.Confidence.ShouldBe(0.9f);
        metrics.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// A null result yields zero confidence and zero fields (kills the <c>?? 0.0f</c> /
    /// <c>?? 0</c> null-coalescing fallbacks) while still honouring the isSuccess flag.
    /// </summary>
    [Fact]
    public async Task CompleteProcessingAsync_NullResult_ZeroConfidenceAndFields()
    {
        var context = await _service.StartProcessingAsync("doc-null", "p.pdf");

        await _service.CompleteProcessingAsync(context, null, isSuccess: true);

        var metrics = _service.GetDocumentMetrics("doc-null");
        metrics.ShouldNotBeNull();
        metrics!.Confidence.ShouldBe(0.0f);
        metrics.ExtractedFieldCount.ShouldBe(0);
        metrics.IsSuccess.ShouldBeTrue();
    }

    // ── Statistics roll-up via UpdateCurrentStatistics ─────────────────────────

    /// <summary>
    /// After one success and one failure the recent success-rate is exactly 0.5 and the
    /// average confidence is taken over the SUCCESSFUL events only.
    /// </summary>
    [Fact]
    public async Task GetCurrentStatistics_OneSuccessOneFailure_SuccessRateIsHalf()
    {
        var c1 = await _service.StartProcessingAsync("ok", "p1");
        await _service.CompleteProcessingAsync(c1, MakeResult(0.8f, fechas: 1, montos: 1), isSuccess: true);
        var c2 = await _service.StartProcessingAsync("bad", "p2");
        await _service.RecordErrorAsync(c2, "boom");

        var stats = await _service.GetCurrentStatisticsAsync();

        stats.SuccessRate.ShouldBe(0.5);
        stats.AverageConfidence.ShouldBe(0.8f, 0.0001f); // only the successful event contributes
    }

    // ── CalculateThroughput ────────────────────────────────────────────────────

    /// <summary>
    /// With no events every throughput figure is zero, but Period still echoes the request
    /// (kills the <c>!Any()</c> early-return removal and the zeroed-struct field values).
    /// </summary>
    [Fact]
    public void CalculateThroughput_NoEvents_ReturnsZeros()
    {
        var throughput = _service.CalculateThroughput(TimeSpan.FromHours(1));

        throughput.TotalDocuments.ShouldBe(0);
        throughput.SuccessfulDocuments.ShouldBe(0);
        throughput.FailedDocuments.ShouldBe(0);
        throughput.AverageProcessingTime.ShouldBe(0);
        throughput.DocumentsPerHour.ShouldBe(0);
        throughput.SuccessRate.ShouldBe(0);
        throughput.Period.ShouldBe(TimeSpan.FromHours(1));
    }

    /// <summary>
    /// With 2 successes and 1 failure the counts, success-rate and docs/hour are exact;
    /// the second window (2 hours) pins the division (kills <c>/</c>→<c>*</c>).
    /// </summary>
    [Fact]
    public async Task CalculateThroughput_MixedEvents_ComputesExactFigures()
    {
        var c1 = await _service.StartProcessingAsync("d1", "p1");
        await _service.CompleteProcessingAsync(c1, MakeResult(0.8f, 1, 1), isSuccess: true);
        var c2 = await _service.StartProcessingAsync("d2", "p2");
        await _service.CompleteProcessingAsync(c2, MakeResult(0.6f, 1, 1), isSuccess: true);
        var c3 = await _service.StartProcessingAsync("d3", "p3");
        await _service.RecordErrorAsync(c3, "x");

        var oneHour = _service.CalculateThroughput(TimeSpan.FromHours(1));
        oneHour.TotalDocuments.ShouldBe(3);
        oneHour.SuccessfulDocuments.ShouldBe(2);
        oneHour.FailedDocuments.ShouldBe(1); // total - successful
        oneHour.SuccessRate.ShouldBe(2.0 / 3.0);
        oneHour.DocumentsPerHour.ShouldBe(3.0); // 3 / 1h
        oneHour.Period.ShouldBe(TimeSpan.FromHours(1));
        oneHour.AverageProcessingTime.ShouldBeGreaterThanOrEqualTo(0);

        var twoHours = _service.CalculateThroughput(TimeSpan.FromHours(2));
        twoHours.DocumentsPerHour.ShouldBe(1.5); // 3 / 2h — kills '/'→'*'
    }

    // ── AggregateMetrics (timer callback, invoked deterministically via reflection) ─

    /// <summary>
    /// The timer aggregation rolls up ALL recorded document metrics: totals, success/failure
    /// split, success-rate, average confidence (successful only) and average field count (all).
    /// </summary>
    [Fact]
    public async Task AggregateMetrics_RollsUpAllDocumentMetrics()
    {
        var c1 = await _service.StartProcessingAsync("a1", "p1");
        await _service.CompleteProcessingAsync(c1, MakeResult(0.8f, fechas: 1, montos: 1), isSuccess: true); // 2 fields
        var c2 = await _service.StartProcessingAsync("a2", "p2");
        await _service.CompleteProcessingAsync(c2, MakeResult(0.6f, fechas: 2, montos: 2), isSuccess: true); // 4 fields
        var c3 = await _service.StartProcessingAsync("a3", "p3");
        await _service.RecordErrorAsync(c3, "x"); // 0 fields, failure

        InvokeAggregateMetrics(_service);

        var stats = _service.CurrentStatistics;
        stats.TotalDocumentsProcessed.ShouldBe(3);
        stats.SuccessfulDocuments.ShouldBe(2);
        stats.FailedDocuments.ShouldBe(1);
        stats.SuccessRate.ShouldBe(2.0 / 3.0);
        stats.AverageConfidence.ShouldBe(0.7f, 0.0001f);            // (0.8 + 0.6) / 2 successful
        stats.AverageExtractedFields.ShouldBe(2.0, 0.0001);          // (2 + 4 + 0) / 3 all
        stats.AverageProcessingTime.ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Aggregation over an empty metric set is a no-op (kills the <c>!Any()</c> early-return
    /// removal — without it Average() would throw and the catch would swallow it, leaving the
    /// default statistics, which we assert remain the construction-time defaults).
    /// </summary>
    [Fact]
    public void AggregateMetrics_NoMetrics_LeavesDefaultStatistics()
    {
        InvokeAggregateMetrics(_service);

        var stats = _service.CurrentStatistics;
        stats.TotalDocumentsProcessed.ShouldBe(0);
        stats.SuccessRate.ShouldBe(0);
    }

    // ── GetRecentEvents ────────────────────────────────────────────────────────

    /// <summary>
    /// GetRecentEvents returns the MOST RECENT events, not the oldest (kills <c>TakeLast</c>→<c>Take</c>).
    /// </summary>
    [Fact]
    public async Task GetRecentEvents_ReturnsMostRecentNotOldest()
    {
        foreach (var id in new[] { "first", "second", "third" })
        {
            var context = await _service.StartProcessingAsync(id, "p");
            await _service.CompleteProcessingAsync(context, null, isSuccess: true);
        }

        var events = _service.GetRecentEvents(2);

        events.Count.ShouldBe(2);
        events.Select(e => e.DocumentId).ShouldContain("third");
        events.Select(e => e.DocumentId).ShouldContain("second");
        events.ShouldNotContain(e => e.DocumentId == "first"); // 'first' is the oldest, must be dropped
    }

    // ── ValidatePerformanceAsync ───────────────────────────────────────────────

    /// <summary>
    /// A fresh service (zero throughput, zero success-rate, concurrency 5) fails exactly two
    /// requirements — throughput and success-rate — and nothing else (kills the time- and
    /// concurrency-branch operator mutants, which would each add a third message at these values).
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_FreshService_FailsThroughputAndSuccessRateOnly()
    {
        var result = await _service.ValidatePerformanceAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var validation = result.Value!;
        validation.IsMeetingRequirements.ShouldBeFalse();
        validation.ValidationResults.Count.ShouldBe(2);
        validation.ValidationResults.ShouldContain(r => r.Contains("Throughput", StringComparison.Ordinal));
        validation.ValidationResults.ShouldContain(r => r.Contains("Success rate", StringComparison.Ordinal));
        validation.ValidationResults.ShouldNotContain(r => r.Contains("Concurrency", StringComparison.Ordinal));
        validation.ValidationResults.ShouldNotContain(r => r.Contains("Processing time", StringComparison.Ordinal));
        validation.Throughput1Hour.ShouldNotBeNull();
        validation.Throughput5Minutes.ShouldNotBeNull();
        validation.CurrentStatistics.ShouldNotBeNull();
    }

    /// <summary>
    /// A service configured below the 5-concurrency minimum also reports a concurrency failure
    /// (kills the <c>MaxConcurrency &lt; 5</c> branch — three messages now instead of two).
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_LowConcurrency_AddsConcurrencyFailure()
    {
        var service = new ProcessingMetricsService(_logger, maxConcurrency: 3);

        var result = await service.ValidatePerformanceAsync();

        result.IsSuccess.ShouldBeTrue();
        var validation = result.Value!;
        validation.IsMeetingRequirements.ShouldBeFalse();
        validation.ValidationResults.Count.ShouldBe(3);
        validation.ValidationResults.ShouldContain(r => r.Contains("Concurrency", StringComparison.Ordinal));
    }

    /// <summary>
    /// When all four requirements are satisfied IsMeetingRequirements stays true with no messages
    /// (kills the <c>IsMeetingRequirements = true</c> initialization flip and the no-message side of
    /// every threshold, including the <c>DocumentsPerHour &lt; 100</c> boundary at exactly 100).
    /// State is injected deterministically (100 in-window events + a meeting statistics snapshot).
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_AllRequirementsMet_ReportsMeeting()
    {
        InjectSuccessEvents(_service, 100); // 100 docs / 1h ⇒ 100 docs/hour
        SetCurrentStatistics(_service, new ProcessingStatistics { SuccessRate = 1.0, AverageProcessingTime = 5 });

        var result = await _service.ValidatePerformanceAsync();

        result.IsSuccess.ShouldBeTrue();
        var validation = result.Value!;
        validation.IsMeetingRequirements.ShouldBeTrue();
        validation.ValidationResults.ShouldBeEmpty();
    }

    /// <summary>
    /// Throughput is the ONLY failing requirement → IsMeetingRequirements is set false solely by the
    /// throughput branch (kills that branch's <c>= false</c>→<c>= true</c> flip; exactly one message).
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_OnlyThroughputFails_SingleMessage()
    {
        // No events ⇒ 0 docs/hour (fails); statistics otherwise meeting.
        SetCurrentStatistics(_service, new ProcessingStatistics { SuccessRate = 1.0, AverageProcessingTime = 5 });

        var validation = (await _service.ValidatePerformanceAsync()).Value!;

        validation.IsMeetingRequirements.ShouldBeFalse();
        validation.ValidationResults.Count.ShouldBe(1);
        validation.ValidationResults.ShouldContain(r => r.Contains("Throughput", StringComparison.Ordinal));
    }

    /// <summary>
    /// Processing-time is the ONLY failing requirement (avg 31s &gt; 30) — covers and kills the
    /// time-branch flag flip, its message, and the <c>&gt; 30</c>→<c>&lt; 30</c> comparison mutant.
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_OnlyProcessingTimeFails_SingleMessage()
    {
        InjectSuccessEvents(_service, 100); // throughput meets
        SetCurrentStatistics(_service, new ProcessingStatistics { SuccessRate = 1.0, AverageProcessingTime = 31 });

        var validation = (await _service.ValidatePerformanceAsync()).Value!;

        validation.IsMeetingRequirements.ShouldBeFalse();
        validation.ValidationResults.Count.ShouldBe(1);
        validation.ValidationResults.ShouldContain(r => r.Contains("Processing time", StringComparison.Ordinal));
    }

    /// <summary>
    /// Concurrency is the ONLY failing requirement (kills the concurrency-branch flag flip).
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_OnlyConcurrencyFails_SingleMessage()
    {
        var service = new ProcessingMetricsService(_logger, maxConcurrency: 3);
        InjectSuccessEvents(service, 100); // throughput meets
        SetCurrentStatistics(service, new ProcessingStatistics { SuccessRate = 1.0, AverageProcessingTime = 5 });

        var validation = (await service.ValidatePerformanceAsync()).Value!;

        validation.IsMeetingRequirements.ShouldBeFalse();
        validation.ValidationResults.Count.ShouldBe(1);
        validation.ValidationResults.ShouldContain(r => r.Contains("Concurrency", StringComparison.Ordinal));
    }

    /// <summary>
    /// Success-rate is the ONLY failing requirement (kills the success-rate-branch flag flip).
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_OnlySuccessRateFails_SingleMessage()
    {
        InjectSuccessEvents(_service, 100); // throughput meets
        SetCurrentStatistics(_service, new ProcessingStatistics { SuccessRate = 0.5, AverageProcessingTime = 5 });

        var validation = (await _service.ValidatePerformanceAsync()).Value!;

        validation.IsMeetingRequirements.ShouldBeFalse();
        validation.ValidationResults.Count.ShouldBe(1);
        validation.ValidationResults.ShouldContain(r => r.Contains("Success rate", StringComparison.Ordinal));
    }

    /// <summary>
    /// Average processing time of exactly 30s still MEETS the requirement (the check is strictly
    /// <c>&gt; 30</c>) — kills the <c>&gt; 30</c>→<c>&gt;= 30</c> boundary mutant.
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_ProcessingTimeAtBoundary_StillMeets()
    {
        InjectSuccessEvents(_service, 100);
        SetCurrentStatistics(_service, new ProcessingStatistics { SuccessRate = 1.0, AverageProcessingTime = 30 });

        var validation = (await _service.ValidatePerformanceAsync()).Value!;

        validation.IsMeetingRequirements.ShouldBeTrue();
        validation.ValidationResults.ShouldNotContain(r => r.Contains("Processing time", StringComparison.Ordinal));
    }

    /// <summary>
    /// A success rate of exactly 0.99 MEETS the requirement (the check is strictly <c>&lt; 0.99</c>)
    /// — kills the <c>&lt; 0.99</c>→<c>&lt;= 0.99</c> boundary mutant.
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_SuccessRateAtBoundary_StillMeets()
    {
        InjectSuccessEvents(_service, 100);
        SetCurrentStatistics(_service, new ProcessingStatistics { SuccessRate = 0.99, AverageProcessingTime = 5 });

        var validation = (await _service.ValidatePerformanceAsync()).Value!;

        validation.IsMeetingRequirements.ShouldBeTrue();
        validation.ValidationResults.ShouldNotContain(r => r.Contains("Success rate", StringComparison.Ordinal));
    }

    /// <summary>
    /// Aggregating an all-failure metric set yields zero average confidence WITHOUT throwing on the
    /// empty successful set — kills the <c>successfulMetrics.Any() ? … : 0</c> guard forced to
    /// <c>true</c> (which would average an empty sequence, throw, and leave statistics un-updated).
    /// </summary>
    [Fact]
    public async Task AggregateMetrics_AllFailures_ZeroConfidenceAndCountsRolledUp()
    {
        var c1 = await _service.StartProcessingAsync("f1", "p1");
        await _service.RecordErrorAsync(c1, "x");
        var c2 = await _service.StartProcessingAsync("f2", "p2");
        await _service.RecordErrorAsync(c2, "y");

        InvokeAggregateMetrics(_service);

        var stats = _service.CurrentStatistics;
        stats.TotalDocumentsProcessed.ShouldBe(2); // would be 0 if the forced-true guard threw and was caught
        stats.SuccessfulDocuments.ShouldBe(0);
        stats.FailedDocuments.ShouldBe(2);
        stats.AverageConfidence.ShouldBe(0.0f);
        stats.SuccessRate.ShouldBe(0.0);
    }

    // ── Stopwatch lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Completing processing stops the context stopwatch (kills the <c>Stopwatch.Stop()</c> removal).
    /// </summary>
    [Fact]
    public async Task CompleteProcessingAsync_StopsStopwatch()
    {
        var context = await _service.StartProcessingAsync("sw-c", "p");
        context.Stopwatch.IsRunning.ShouldBeTrue();

        await _service.CompleteProcessingAsync(context, MakeResult(0.5f, 1, 1), isSuccess: true);

        context.Stopwatch.IsRunning.ShouldBeFalse();
    }

    /// <summary>
    /// Recording an error stops the stopwatch and records the context's id/path on the error metrics
    /// (kills the <c>Stopwatch.Stop()</c> removal and the <c>new ProcessingMetrics { … }</c> initializer
    /// emptying — both id and source path would otherwise become null).
    /// </summary>
    [Fact]
    public async Task RecordErrorAsync_StopsStopwatchAndPopulatesMetrics()
    {
        var context = await _service.StartProcessingAsync("sw-e", "src/err.pdf");

        await _service.RecordErrorAsync(context, "explode");

        context.Stopwatch.IsRunning.ShouldBeFalse();
        var metrics = _service.GetDocumentMetrics("sw-e");
        metrics.ShouldNotBeNull();
        metrics!.DocumentId.ShouldBe("sw-e");
        metrics.SourcePath.ShouldBe("src/err.pdf");
        metrics.IsSuccess.ShouldBeFalse();
        metrics.Confidence.ShouldBe(0.0f);
        metrics.ExtractedFieldCount.ShouldBe(0);
    }

    /// <summary>
    /// With two successful events of DIFFERENT confidence the rolled-up average confidence is the mean,
    /// not the min or max (kills the <c>Average()</c>→<c>Min()</c>/<c>Max()</c> Linq mutants).
    /// </summary>
    [Fact]
    public async Task GetCurrentStatistics_TwoDifferentConfidences_AveragesThem()
    {
        var c1 = await _service.StartProcessingAsync("hi", "p1");
        await _service.CompleteProcessingAsync(c1, MakeResult(0.9f, 1, 1), isSuccess: true);
        var c2 = await _service.StartProcessingAsync("lo", "p2");
        await _service.CompleteProcessingAsync(c2, MakeResult(0.5f, 1, 1), isSuccess: true);

        var stats = await _service.GetCurrentStatisticsAsync();

        stats.AverageConfidence.ShouldBe(0.7f, 0.0001f); // mean(0.9, 0.5); Min→0.5, Max→0.9 would fail
    }

    // ── Dispose ────────────────────────────────────────────────────────────────

    /// <summary>
    /// After disposal the internal semaphore is disposed, so further processing throws
    /// (kills <c>Dispose(true)</c>→<c>Dispose(false)</c>/removal, the <c>_metricsLock.Dispose()</c>
    /// removal, and the <c>!_disposed &amp;&amp; disposing</c> negation mutants — all would leave the
    /// lock usable).
    /// </summary>
    [Fact]
    public async Task Dispose_ThenStartProcessing_ThrowsObjectDisposed()
    {
        var service = new ProcessingMetricsService(_logger, maxConcurrency: 5);

        service.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(async () => await service.StartProcessingAsync("x", "y"));
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static void SetCurrentStatistics(ProcessingMetricsService service, ProcessingStatistics statistics)
    {
        var property = typeof(ProcessingMetricsService).GetProperty(nameof(ProcessingMetricsService.CurrentStatistics));
        property.ShouldNotBeNull();
        property!.SetValue(service, statistics);
    }

    private static void InjectSuccessEvents(ProcessingMetricsService service, int count)
    {
        var field = typeof(ProcessingMetricsService).GetField("_processingEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull();
        var queue = (ConcurrentQueue<ProcessingEvent>)field!.GetValue(service)!;
        for (var i = 0; i < count; i++)
        {
            queue.Enqueue(new ProcessingEvent
            {
                DocumentId = $"evt-{i}",
                IsSuccess = true,
                ProcessingTimeSeconds = 1.0,
                Confidence = 1.0f,
                Timestamp = DateTime.UtcNow,
            });
        }
    }

    private static ProcessingResult MakeResult(float confidence, int fechas, int montos)
    {
        return new ProcessingResult
        {
            OCRResult = new OCRResult
            {
                Text = "sample",
                Confidence = Confidence.FromOcr(confidence),
            },
            ExtractedFields = new ExtractedFields
            {
                Fechas = Enumerable.Range(1, fechas).Select(i => $"2024-01-{i:00}").ToList(),
                Montos = Enumerable.Range(1, montos).Select(_ => new AmountData("MXN", 1m, "1.00")).ToList(),
            },
        };
    }

    private static void InvokeAggregateMetrics(ProcessingMetricsService service)
    {
        var method = typeof(ProcessingMetricsService).GetMethod("AggregateMetrics", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();
        method!.Invoke(service, new object?[] { null });
    }
}
