using ExxerCube.Prisma.Infrastructure.Database.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Regression tests for GitHub issue #1: the SLA update cycle ran a whole batch of updates in parallel on the one
/// scoped <see cref="ISLAEnforcer"/> (one EF Core <c>DbContext</c>), which EF Core does not support, so ~75 of 81
/// updates failed every minute ("A second operation was started on this context instance").
/// </summary>
/// <remarks>
/// The enforcer is a scoped fake that counts how many updates run at the same time on its instance, the way a
/// <c>DbContext</c> would see them. Each update yields, so overlapping calls really overlap.
/// </remarks>
public sealed class SLAUpdateBackgroundServiceTests
{
    [Fact]
    public async Task UpdateCycle_SeveralActiveCasesWithBatchSizeAboveOne_UpdatesEveryCaseOneAtATime()
    {
        var enforcer = new ConcurrencyRecordingEnforcer(Enumerable.Range(1, 7).Select(i => $"file-{i}").ToList());

        await RunOneCycleAsync(enforcer, batchSize: 3);

        enforcer.Updated.OrderBy(f => f).ShouldBe(enforcer.ActiveFileIds.OrderBy(f => f));
        enforcer.MaxConcurrentUpdates.ShouldBe(1, "updates on one scoped DbContext must never overlap");
    }

    [Fact]
    public async Task UpdateCycle_OneUpdateFails_StillUpdatesTheOthers()
    {
        var enforcer = new ConcurrencyRecordingEnforcer(new List<string> { "file-1", "file-bad", "file-3" }, failFor: "file-bad");

        await RunOneCycleAsync(enforcer, batchSize: 100);

        enforcer.Updated.ShouldBe(new[] { "file-1", "file-3" });
    }

    private static async Task RunOneCycleAsync(ConcurrencyRecordingEnforcer enforcer, int batchSize)
    {
        var services = new ServiceCollection();
        services.AddScoped<ISLAEnforcer>(_ => enforcer);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var options = Options.Create(new SLAUpdateOptions { BatchSize = batchSize, UpdateIntervalSeconds = 3600, RetryDelaySeconds = 3600 });

        var service = new SLAUpdateBackgroundService(scopeFactory, NullLogger<SLAUpdateBackgroundService>.Instance, options);
        await service.StartAsync(TestContext.Current.CancellationToken);

        // The first cycle runs immediately; wait for it to try every active case, then stop.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (enforcer.Attempts < enforcer.ActiveFileIds.Count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await service.StopAsync(CancellationToken.None);
        enforcer.Attempts.ShouldBe(enforcer.ActiveFileIds.Count);
    }

    /// <summary>A scoped enforcer that records how many updates are in flight on it at once.</summary>
    private sealed class ConcurrencyRecordingEnforcer(List<string> activeFileIds, string? failFor = null) : ISLAEnforcer
    {
        private int _inFlight;
        private int _attempts;
        private int _maxConcurrent;
        private readonly List<string> _updated = new();

        public List<string> ActiveFileIds { get; } = activeFileIds;

        public int Attempts => Volatile.Read(ref _attempts);

        public int MaxConcurrentUpdates => Volatile.Read(ref _maxConcurrent);

        public IReadOnlyList<string> Updated
        {
            get { lock (_updated) { return _updated.ToList(); } }
        }

        public Task<Result<List<SLAStatus>>> GetActiveCasesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<List<SLAStatus>>.Success(ActiveFileIds.Select(id => new SLAStatus { FileId = id }).ToList()));

        public async Task<Result<SLAStatus>> UpdateSLAStatusAsync(string fileId, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _inFlight);
            int seen;
            while (now > (seen = Volatile.Read(ref _maxConcurrent)) && Interlocked.CompareExchange(ref _maxConcurrent, now, seen) != seen)
            {
            }

            try
            {
                await Task.Delay(15, cancellationToken);
                if (fileId == failFor)
                {
                    return Result<SLAStatus>.WithFailure("update failed");
                }

                lock (_updated)
                {
                    _updated.Add(fileId);
                }

                return Result<SLAStatus>.Success(new SLAStatus { FileId = fileId });
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
                Interlocked.Increment(ref _attempts);
            }
        }

        public Task<Result<SLAStatus>> CalculateSLAStatusAsync(string fileId, DateTime intakeDate, int daysPlazo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<SLAStatus?>> GetSLAStatusAsync(string fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<SLAStatus>>> GetAtRiskCasesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<SLAStatus>>> GetBreachedCasesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> EscalateCaseAsync(string fileId, EscalationLevel escalationLevel, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<int>> CalculateBusinessDaysAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
