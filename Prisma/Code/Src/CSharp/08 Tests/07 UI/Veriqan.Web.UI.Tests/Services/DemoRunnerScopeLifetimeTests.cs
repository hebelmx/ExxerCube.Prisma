using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Web.UI.Models;
using ExxerCube.Prisma.Veriqan.Web.UI.Options;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Services;

/// <summary>
/// Verifies <see cref="DemoRunner"/> resolves <see cref="IVerificationPipeline"/> from a FRESH
/// scope per <see cref="DemoRunner.RunAsync"/> call (it is registered scoped) and disposes that
/// scope afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mocking approach note:</b> <c>IServiceScopeFactory.CreateAsyncScope()</c> (what
/// <see cref="DemoRunner"/> actually calls) is a static extension method
/// (<c>Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions</c>), not an
/// interface member — NSubstitute cannot intercept or verify calls to it directly. Its real
/// implementation is <c>new AsyncServiceScope(serviceScopeFactory.CreateScope())</c>, so this
/// test substitutes <see cref="IServiceScopeFactory.CreateScope"/> (the real interface member the
/// extension method delegates to) and verifies THAT call count instead. Scope disposal is proven
/// with a small hand-written <see cref="SpyServiceScope"/> (implementing both
/// <see cref="IServiceScope"/> and <see cref="IAsyncDisposable"/>) rather than an NSubstitute
/// double, because <c>AsyncServiceScope.DisposeAsync()</c> special-cases
/// <see cref="IAsyncDisposable"/> scopes — a real spy makes that branch unambiguous to verify.
/// </para>
/// </remarks>
public sealed class DemoRunnerScopeLifetimeTests
{
    private static readonly byte[] SmallPdf = [0x25, 0x50, 0x44, 0x46];

    [Fact]
    public async Task RunAsync_Live_ResolvesPipelineFromFreshScopePerCall()
    {
        var pipeline = Substitute.For<IVerificationPipeline>();
        var mapper = Substitute.For<IVerificationOutcomeMapper>();
        var demoDataService = new DemoDataService();

        var dummyOutcome = new VerificationOutcome(
            Job: new VerificationJob(Guid.NewGuid(), "dummy-hash", DateTimeOffset.UtcNow, VerificationJobStatus.Completed),
            // VerdictSummary has no public constructor outside Veriqan.Application — null! is
            // safe because IVerificationOutcomeMapper is mocked and never dereferences it.
            Summary: null!,
            Findings: Array.Empty<RuleFinding>());

        pipeline.ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithSuccess(dummyOutcome)));

        var cannedMappedCase = demoDataService.GetBySignal(VerdictSignal.Green)!;
        mapper.Map(
                Arg.Any<VerificationOutcome>(),
                Arg.Any<IReadOnlyDictionary<int, byte[]>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<DemoStatementCase>.WithSuccess(cannedMappedCase));

        var spyScope = new SpyServiceScope(pipeline);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(spyScope);

        // Hero chain is not this test's concern (scope lifetime is) — the generator is
        // configured to fail so DemoRunner's best-effort fallback kicks in with an empty PNG
        // dict, without needing to also configure the renderer.
        var markedPdfGenerator = Substitute.For<IMarkedPdfGenerator>();
        markedPdfGenerator.Generate(
                Arg.Any<byte[]>(),
                Arg.Any<IReadOnlyList<RuleFinding>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IReadOnlyDictionary<string, ChecklistTier>>())
            .Returns(Result<byte[]>.WithFailure("hero chain not under test here"));
        var markedPageRenderer = Substitute.For<IMarkedPageRenderer>();
        var realCheckLedger = new RealCheckLedger();

        var sut = new DemoRunner(
            scopeFactory,
            mapper,
            demoDataService,
            MsOptions.Create(new DemoOptions { LiveModeEnabled = true }),
            MsOptions.Create(new PdfExtractionOptions()),
            markedPdfGenerator,
            markedPageRenderer,
            realCheckLedger,
            NullLogger<DemoRunner>.Instance);

        var result = await sut.RunAsync(SmallPdf, "good.pdf", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsLive.ShouldBeTrue();

        // Exactly one fresh scope per call — the real interface member the CreateAsyncScope()
        // extension method delegates to.
        scopeFactory.Received(1).CreateScope();
        await pipeline.Received(1).ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());

        // The scope must be disposed after RunAsync completes (await using in DemoRunner).
        spyScope.DisposeAsyncCallCount.ShouldBe(1);
        spyScope.DisposeCallCount.ShouldBe(0); // AsyncServiceScope prefers DisposeAsync when available.
    }

    /// <summary>
    /// Minimal hand-written <see cref="IServiceScope"/> + <see cref="IAsyncDisposable"/> spy.
    /// Its <see cref="ServiceProvider"/> resolves <see cref="IVerificationPipeline"/> to the
    /// mocked pipeline instance supplied to the constructor; disposal calls are counted so the
    /// test can assert the scope was actually torn down.
    /// </summary>
    private sealed class SpyServiceScope(IVerificationPipeline pipeline) : IServiceScope, IAsyncDisposable, IServiceProvider
    {
        public int DisposeCallCount { get; private set; }

        public int DisposeAsyncCallCount { get; private set; }

        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IVerificationPipeline) ? pipeline : null;

        public void Dispose() => DisposeCallCount++;

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCallCount++;
            return ValueTask.CompletedTask;
        }
    }
}
