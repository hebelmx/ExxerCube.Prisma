using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.InMemory;

/// <summary>
/// In-memory <see cref="IVerificationResultStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>.  Suitable for integration tests
/// and local dev without a database; data is not persisted between process restarts.
/// </summary>
/// <remarks>
/// Thread-safety: all operations use atomic dictionary primitives.
/// <see cref="SaveOutcomeAsync"/> uses <c>TryAdd</c> (first-write-wins).
/// <see cref="ReplaceOutcomeAsync"/> uses <c>AddOrUpdate</c> (always overwrites).
/// </remarks>
internal sealed class InMemoryVerificationResultStore : IVerificationResultStore
{
    private readonly ConcurrentDictionary<string, VerificationOutcome> _store = new();

    /// <inheritdoc />
    public Task<Result<bool>> IsCompletedAsync(string contentHash, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<bool>());

        return Task.FromResult(Result<bool>.WithSuccess(_store.ContainsKey(contentHash)));
    }

    /// <inheritdoc />
    public Task<Result> SaveOutcomeAsync(string contentHash, VerificationOutcome outcome, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled());

        // TryAdd is a no-op when the key already exists — first-write-wins
        _store.TryAdd(contentHash, outcome);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result> ReplaceOutcomeAsync(string contentHash, VerificationOutcome outcome, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled());

        // AddOrUpdate always writes the new value — exactly one entry per hash
        _store.AddOrUpdate(contentHash, outcome, (_, _) => outcome);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result<VerificationOutcome?>> GetOutcomeAsync(string contentHash, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<VerificationOutcome?>());

        _store.TryGetValue(contentHash, out var outcome);
        return Task.FromResult(Result<VerificationOutcome?>.WithSuccess(outcome));
    }
}
