using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Services;

/// <summary>
/// Coverage for <see cref="PipelineWarmupHostedService"/> — previously ZERO tests existed for the
/// warm-up service or <see cref="IPipelineReadiness"/>. These tests pin down the fix for the
/// "readiness flag lies on reference-data load failure" bug: <c>MarkReady()</c> must be called
/// only when the warm-up genuinely succeeds, never unconditionally.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mocking approach note:</b> <c>IServiceScopeFactory.CreateAsyncScope()</c> (what the SUT
/// actually calls) is a static extension method, not an interface member — NSubstitute cannot
/// intercept it directly. Following the exact pattern already established in
/// <c>DemoRunnerScopeLifetimeTests.SpyServiceScope</c>, this file substitutes
/// <see cref="IServiceScopeFactory.CreateScope"/> (the real interface member the extension method
/// delegates to) with a small hand-written <see cref="SpyServiceScope"/> resolving
/// <see cref="IVecReferenceDataProvider"/>.
/// </para>
/// <para>
/// <b>Determinism note:</b> the warm-up runs as a fire-and-forget background task
/// (<c>_ = WarmUpAsync(...)</c> in <c>StartAsync</c>). Rather than poll <c>IsReady</c> with a
/// sleep, these tests await the SUT's <c>internal Task? WarmupTask</c> hook (exposed for exactly
/// this purpose, gated by <c>InternalsVisibleTo</c> on the production csproj) to deterministically
/// wait for the background work to finish before asserting.
/// </para>
/// </remarks>
public sealed class PipelineWarmupHostedServiceTests
{
    private static VecReferenceBundle CreateMinimalBundle() =>
        new(
            BundleMetadata: new BundleMetadata("1.0.0", "Demo Bank (Iqubica)", null, null, null, null),
            Products: null,
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static PipelineWarmupHostedService CreateSut(
        IVecReferenceDataProvider provider,
        IPipelineReadiness readiness,
        out IServiceScopeFactory scopeFactory)
    {
        var spyScope = new SpyServiceScope(provider);
        scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(spyScope);

        return new PipelineWarmupHostedService(
            scopeFactory,
            readiness,
            NullLogger<PipelineWarmupHostedService>.Instance);
    }

    [Fact]
    public async Task StartAsync_WarmupSucceeds_MarksReady()
    {
        var provider = Substitute.For<IVecReferenceDataProvider>();
        provider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VecReferenceBundle>.WithSuccess(CreateMinimalBundle())));

        var readiness = Substitute.For<IPipelineReadiness>();
        var sut = CreateSut(provider, readiness, out _);
        var cancellationToken = TestContext.Current.CancellationToken;

        await sut.StartAsync(cancellationToken);
        await sut.WarmupTask!;

        readiness.Received(1).MarkReady();
    }

    [Fact]
    public async Task StartAsync_WarmupFails_DoesNotMarkReady()
    {
        var provider = Substitute.For<IVecReferenceDataProvider>();
        provider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VecReferenceBundle>.WithFailure("simulated reference-data load failure")));

        var readiness = Substitute.For<IPipelineReadiness>();
        var sut = CreateSut(provider, readiness, out _);
        var cancellationToken = TestContext.Current.CancellationToken;

        await sut.StartAsync(cancellationToken);
        await sut.WarmupTask!;

        readiness.DidNotReceive().MarkReady();
    }

    [Fact]
    public async Task StartAsync_WarmupThrows_DoesNotMarkReadyAndDoesNotThrow()
    {
        var provider = Substitute.For<IVecReferenceDataProvider>();
        provider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom — simulated reference-data provider crash"));

        var readiness = Substitute.For<IPipelineReadiness>();
        var sut = CreateSut(provider, readiness, out _);
        var cancellationToken = TestContext.Current.CancellationToken;

        await sut.StartAsync(cancellationToken);

        // Never throws out of the hosted service — awaiting WarmupTask must complete normally,
        // not fault, matching the "never throw out of a hosted service background task" contract.
        await sut.WarmupTask!;

        readiness.DidNotReceive().MarkReady();
    }

    [Fact]
    public async Task StartAsync_DoesNotBlock()
    {
        // Provider deliberately slow — proves StartAsync itself never awaits the warm-up.
        var provider = Substitute.For<IVecReferenceDataProvider>();
        provider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(1);
                await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
                return Result<VecReferenceBundle>.WithSuccess(CreateMinimalBundle());
            });

        var readiness = Substitute.For<IPipelineReadiness>();
        var sut = CreateSut(provider, readiness, out _);
        var cancellationToken = TestContext.Current.CancellationToken;

        var startTask = sut.StartAsync(cancellationToken);

        // StartAsync returns Task.CompletedTask synchronously — this must hold true regardless
        // of how slow (or how it fails) the background warm-up is.
        startTask.IsCompletedSuccessfully.ShouldBeTrue();

        // Clean up: let the background warm-up finish before the test exits.
        await sut.WarmupTask!;
        readiness.Received(1).MarkReady();
    }

    /// <summary>
    /// Minimal hand-written <see cref="IServiceScope"/> + <see cref="IAsyncDisposable"/> spy,
    /// mirroring <c>DemoRunnerScopeLifetimeTests.SpyServiceScope</c>. Its
    /// <see cref="ServiceProvider"/> resolves <see cref="IVecReferenceDataProvider"/> to the
    /// mocked provider instance supplied to the constructor.
    /// </summary>
    private sealed class SpyServiceScope(IVecReferenceDataProvider provider) : IServiceScope, IAsyncDisposable, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IVecReferenceDataProvider) ? provider : null;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
