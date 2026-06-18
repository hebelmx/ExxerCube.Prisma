using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Services;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;
using IndQuestResults;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Unit tests for <see cref="DispositionService"/> covering the human-disposition invariants
/// (FR-18, AR-9): human-actor required, append-only, no auto-disposition.
/// </summary>
public sealed class DispositionServiceTests
{
    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    private static readonly Guid JobId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid FindingId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset FrozenTime =
        new(2026, 6, 18, 0, 0, 0, TimeSpan.Zero);

    private static (DispositionService Service, IDispositionRepository Repo) CreateSut()
    {
        var repo = Substitute.For<IDispositionRepository>();
        var clock = new FakeTimeProvider(FrozenTime);
        var logger = XUnitLogger.CreateLogger<DispositionService>();
        var svc = new DispositionService(repo, clock, logger);
        return (svc, repo);
    }

    // -----------------------------------------------------------------------
    // Helpers — make the repo stub return a successful append
    // -----------------------------------------------------------------------

    private static void StubAppendSuccess(IDispositionRepository repo)
    {
        repo.AppendAsync(Arg.Any<Disposition>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var d = call.Arg<Disposition>();
                return Task.FromResult(Result<Disposition>.WithSuccess(d));
            });
    }

    private static void StubGetForJobSuccess(
        IDispositionRepository repo,
        IReadOnlyList<Disposition> dispositions)
    {
        repo.GetForJobAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<IReadOnlyList<Disposition>>.WithSuccess(dispositions)));
    }

    // -----------------------------------------------------------------------
    // AC: Disposition_WithActor_AppendsAuditRow
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DispositionFinding_WithActor_AppendsAuditRow()
    {
        // Arrange
        var (svc, repo) = CreateSut();
        StubAppendSuccess(repo);

        // Act
        var result = await svc.DispositionFindingAsync(
            jobId: JobId,
            findingId: FindingId,
            action: DispositionAction.Accept,
            actor: "reviewer@example.com",
            notes: "Looks fine.",
            beforeState: "Fail",
            afterState: "Accepted",
            engineVersion: "1.2.3",
            referenceBundleVersion: "BDL-2026-06",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var disposition = result.Value;
        disposition.ShouldNotBeNull();
        disposition!.VerificationJobId.ShouldBe(JobId);
        disposition.FindingId.ShouldBe(FindingId);
        disposition.Action.ShouldBe(DispositionAction.Accept);
        disposition.Actor.ShouldBe("reviewer@example.com");
        disposition.DispositionedAtUtc.ShouldBe(FrozenTime);
        disposition.BeforeState.ShouldBe("Fail");
        disposition.AfterState.ShouldBe("Accepted");
        disposition.Notes.ShouldBe("Looks fine.");
        disposition.EngineVersion.ShouldBe("1.2.3");
        disposition.ReferenceBundleVersion.ShouldBe("BDL-2026-06");

        await repo.Received(1).AppendAsync(
            Arg.Is<Disposition>(d => d.Actor == "reviewer@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispositionStatement_WithActor_AppendsAuditRowWithNullFindingId()
    {
        // Arrange
        var (svc, repo) = CreateSut();
        StubAppendSuccess(repo);

        // Act
        var result = await svc.DispositionStatementAsync(
            jobId: JobId,
            action: DispositionAction.Reject,
            actor: "supervisor@example.com",
            notes: "Overriding RED verdict.",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FindingId.ShouldBeNull("statement-level disposition must have null FindingId");
        result.Value.Actor.ShouldBe("supervisor@example.com");
        result.Value.Action.ShouldBe(DispositionAction.Reject);
    }

    // -----------------------------------------------------------------------
    // AC: Disposition_NoActor_ReturnsFailure_NoRow (human-required invariant)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DispositionFinding_NoActor_ReturnsFailure_NoRowAppended(string? actor)
    {
        // Arrange
        var (svc, repo) = CreateSut();

        // Act
        var result = await svc.DispositionFindingAsync(
            jobId: JobId,
            findingId: FindingId,
            action: DispositionAction.Accept,
            actor: actor!,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert — returns failure, NO row written
        result.IsSuccess.ShouldBeFalse(
            "null/blank actor must be rejected — VEC never auto-dispositions");
        result.Error.ShouldNotBeNullOrEmpty();

        await repo.DidNotReceive().AppendAsync(
            Arg.Any<Disposition>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DispositionStatement_NoActor_ReturnsFailure_NoRowAppended(string? actor)
    {
        // Arrange
        var (svc, repo) = CreateSut();

        // Act
        var result = await svc.DispositionStatementAsync(
            jobId: JobId,
            action: DispositionAction.Reject,
            actor: actor!,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();

        await repo.DidNotReceive().AppendAsync(
            Arg.Any<Disposition>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // AC: Disposition_Append_IsAdditive (two calls → two rows, first unchanged)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DispositionFinding_CalledTwice_AppendsTwoIndependentRows()
    {
        // Arrange — track every appended disposition
        var (svc, repo) = CreateSut();
        var appended = new List<Disposition>();

        repo.AppendAsync(Arg.Any<Disposition>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var d = call.Arg<Disposition>();
                appended.Add(d);
                return Task.FromResult(Result<Disposition>.WithSuccess(d));
            });

        // Act — disposition the same finding twice with different decisions
        var first = await svc.DispositionFindingAsync(
            jobId: JobId,
            findingId: FindingId,
            action: DispositionAction.Accept,
            actor: "reviewer-a@example.com",
            cancellationToken: TestContext.Current.CancellationToken);

        var second = await svc.DispositionFindingAsync(
            jobId: JobId,
            findingId: FindingId,
            action: DispositionAction.Reject,
            actor: "reviewer-b@example.com",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert — both succeed and produce separate, distinct rows
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();

        appended.Count.ShouldBe(2, "two independent rows must exist — append-only, first row not overwritten");
        appended[0].Actor.ShouldBe("reviewer-a@example.com");
        appended[0].Action.ShouldBe(DispositionAction.Accept);
        appended[1].Actor.ShouldBe("reviewer-b@example.com");
        appended[1].Action.ShouldBe(DispositionAction.Reject);
        appended[0].Id.ShouldNotBe(appended[1].Id, "each append must produce a distinct Guid");

        await repo.Received(2).AppendAsync(
            Arg.Any<Disposition>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // AC: GetForJob_ReturnsAllDispositions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDispositionsForJob_ReturnsAllDispositions()
    {
        // Arrange
        var (svc, repo) = CreateSut();

        var existing = new List<Disposition>
        {
            new(
                id: Guid.NewGuid(),
                verificationJobId: JobId,
                findingId: FindingId,
                action: DispositionAction.Accept,
                actor: "user-a@example.com",
                dispositionedAtUtc: FrozenTime.AddHours(-1)),
            new(
                id: Guid.NewGuid(),
                verificationJobId: JobId,
                findingId: null,
                action: DispositionAction.Reject,
                actor: "user-b@example.com",
                dispositionedAtUtc: FrozenTime),
        };

        StubGetForJobSuccess(repo, existing);

        // Act
        var result = await svc.GetDispositionsForJobAsync(
            JobId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(2);
        result.Value[0].Actor.ShouldBe("user-a@example.com");
        result.Value[1].Actor.ShouldBe("user-b@example.com");
    }

    [Fact]
    public async Task GetDispositionsForJob_NoDispositions_ReturnsEmptyList()
    {
        // Arrange
        var (svc, repo) = CreateSut();
        StubGetForJobSuccess(repo, new List<Disposition>());

        // Act
        var result = await svc.GetDispositionsForJobAsync(
            JobId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // AC: Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DispositionFinding_Cancelled_ReturnsCancelledResult()
    {
        // Arrange
        var (svc, _) = CreateSut();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await svc.DispositionFindingAsync(
            jobId: JobId,
            findingId: FindingId,
            action: DispositionAction.Accept,
            actor: "reviewer@example.com",
            cancellationToken: cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task DispositionStatement_Cancelled_ReturnsCancelledResult()
    {
        // Arrange
        var (svc, _) = CreateSut();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await svc.DispositionStatementAsync(
            jobId: JobId,
            action: DispositionAction.Reject,
            actor: "reviewer@example.com",
            cancellationToken: cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task GetDispositionsForJob_Cancelled_ReturnsCancelledResult()
    {
        // Arrange
        var (svc, _) = CreateSut();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await svc.GetDispositionsForJobAsync(
            JobId, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // AC: Domain entity invariant — Disposition ctor rejects blank actor
    // -----------------------------------------------------------------------

    [Fact]
    public void Disposition_ConstructedWithEmptyActor_Throws()
    {
        // The domain entity itself also enforces the actor invariant at construction.
        Should.Throw<ArgumentException>(() =>
            new Disposition(
                id: Guid.NewGuid(),
                verificationJobId: JobId,
                findingId: null,
                action: DispositionAction.Accept,
                actor: "   ",         // whitespace — must throw
                dispositionedAtUtc: FrozenTime));
    }

    [Fact]
    public void Disposition_ConstructedWithNullActor_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            new Disposition(
                id: Guid.NewGuid(),
                verificationJobId: JobId,
                findingId: null,
                action: DispositionAction.Accept,
                actor: null!,
                dispositionedAtUtc: FrozenTime));
    }

    // -----------------------------------------------------------------------
    // AC: InMemory round-trip — append is additive (EF InMemory)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EfInMemory_DispositionAppend_IsAdditive_TwoRowsPersisted()
    {
        // Arrange — wire a real EfDispositionRepository against EF InMemory
        var dbName = $"DispositionTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var ctx = new VeriqanDbContext(options);
        var repoLogger = XUnitLogger.CreateLogger<EfDispositionRepository>();
        var repo = new EfDispositionRepository(ctx, repoLogger);
        var clock = new FakeTimeProvider(FrozenTime);
        var svcLogger = XUnitLogger.CreateLogger<DispositionService>();
        var svc = new DispositionService(repo, clock, svcLogger);

        // Act — append two dispositions on the same finding
        var r1 = await svc.DispositionFindingAsync(
            JobId, FindingId, DispositionAction.Accept, "user-1@example.com",
            cancellationToken: TestContext.Current.CancellationToken);

        var r2 = await svc.DispositionFindingAsync(
            JobId, FindingId, DispositionAction.Reject, "user-2@example.com",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert — both rows written, first row unmodified
        r1.IsSuccess.ShouldBeTrue();
        r2.IsSuccess.ShouldBeTrue();

        var listResult = await svc.GetDispositionsForJobAsync(
            JobId, TestContext.Current.CancellationToken);

        listResult.IsSuccess.ShouldBeTrue();
        listResult.Value!.Count.ShouldBe(2);
        listResult.Value[0].Actor.ShouldBe("user-1@example.com");
        listResult.Value[0].Action.ShouldBe(DispositionAction.Accept);
        listResult.Value[1].Actor.ShouldBe("user-2@example.com");
        listResult.Value[1].Action.ShouldBe(DispositionAction.Reject);
    }
}
