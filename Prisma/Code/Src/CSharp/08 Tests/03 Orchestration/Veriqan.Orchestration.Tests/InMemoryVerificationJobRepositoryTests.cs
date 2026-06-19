using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Orchestration.InMemory;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit tests for <see cref="InMemoryVerificationJobRepository"/> focusing on the
/// concurrency-safety contract introduced in VERIQAN-E2-S16: two concurrent callers
/// that race to add the same ContentHash must both receive the same single job instance.
/// </summary>
public sealed class InMemoryVerificationJobRepositoryTests
{
    // ---------------------------------------------------------------------------
    // Helper: create a minimal VerificationJob with a given hash
    // ---------------------------------------------------------------------------

    private static VerificationJob MakeJob(string contentHash) =>
        new(
            id: Guid.NewGuid(),
            contentHash: contentHash,
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Pending);

    // ---------------------------------------------------------------------------
    // Test 1: concurrent AddAsync — both callers must observe the same GUID
    // ---------------------------------------------------------------------------

    /// <summary>
    /// When two tasks race to call <see cref="InMemoryVerificationJobRepository.AddAsync"/>
    /// with distinct <see cref="VerificationJob"/> instances that share the same
    /// <see cref="VerificationJob.ContentHash"/>, the repository must return the same
    /// persisted job (same Id) to both callers — the atomic
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}.GetOrAdd"/>
    /// guarantee ensures exactly one entry survives, eliminating the TOCTOU race.
    /// </summary>
    [Fact]
    public async Task AddAsync_ConcurrentSameContentHash_BothCallersReceiveSameJobId()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new InMemoryVerificationJobRepository();

        const string sharedHash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

        // Two distinct VerificationJob instances — same hash, different GUIDs.
        var jobA = MakeJob(sharedHash);
        var jobB = MakeJob(sharedHash);

        // Use a barrier so both tasks enter AddAsync as simultaneously as possible.
        using var barrier = new Barrier(2);

        // ConfigureAwait(false) is permitted inside Task.Run lambdas (not a test method).
        var taskA = Task.Run(async () =>
        {
            barrier.SignalAndWait(ct);
            return await repo.AddAsync(jobA, ct).ConfigureAwait(false);
        }, ct);

        var taskB = Task.Run(async () =>
        {
            barrier.SignalAndWait(ct);
            return await repo.AddAsync(jobB, ct).ConfigureAwait(false);
        }, ct);

        // xUnit1030: test methods must not use ConfigureAwait(false).
        var results = await Task.WhenAll(taskA, taskB);

        var resultA = results[0];
        var resultB = results[1];

        // Both must succeed.
        resultA.IsSuccess.ShouldBeTrue("Task A should succeed");
        resultB.IsSuccess.ShouldBeTrue("Task B should succeed");

        // Both must surface the SAME stored job — the one that won the GetOrAdd slot.
        resultA.Value!.Id.ShouldBe(resultB.Value!.Id,
            "Both concurrent callers must receive the same job Id; " +
            "GetOrAdd guarantees exactly one entry for a given ContentHash.");

        // FindByContentHashAsync must also reflect only one entry.
        var findResult = await repo.FindByContentHashAsync(sharedHash, ct);
        findResult.IsSuccess.ShouldBeTrue();
        findResult.Value.ShouldNotBeNull();
        findResult.Value!.Id.ShouldBe(resultA.Value!.Id,
            "The persisted entry must be the same as what both Add callers received.");
    }

    // ---------------------------------------------------------------------------
    // Test 2: sequential AddAsync — second call returns the first job (idempotent)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A sequential second <c>AddAsync</c> for the same hash returns the originally
    /// stored job, not the newer instance — the repository is idempotent.
    /// </summary>
    [Fact]
    public async Task AddAsync_SequentialSameContentHash_ReturnsFirstJob()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new InMemoryVerificationJobRepository();

        const string sharedHash = "1122334455667788990011223344556677889900112233445566778899001122";

        var first = MakeJob(sharedHash);
        var second = MakeJob(sharedHash);

        // xUnit1030: no ConfigureAwait(false) in test methods.
        var r1 = await repo.AddAsync(first, ct);
        var r2 = await repo.AddAsync(second, ct);

        r1.IsSuccess.ShouldBeTrue();
        r2.IsSuccess.ShouldBeTrue();

        // The repository must return the first stored entry on both calls.
        r1.Value!.Id.ShouldBe(first.Id);
        r2.Value!.Id.ShouldBe(first.Id,
            "Second AddAsync must return the existing entry, not the newer instance.");
    }
}
