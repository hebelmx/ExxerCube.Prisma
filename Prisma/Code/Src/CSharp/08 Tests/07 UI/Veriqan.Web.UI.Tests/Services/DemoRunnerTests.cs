using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Web.UI.Models;
using ExxerCube.Prisma.Veriqan.Web.UI.Options;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Services;

/// <summary>
/// Branch-coverage tests for <see cref="DemoRunner"/> (VLD-S2): live/canned fallback decision,
/// oversized-PDF guard, and failure-propagation modes.
/// </summary>
/// <remarks>
/// <see cref="IVerificationPipeline"/> and <see cref="IVerificationOutcomeMapper"/> are
/// NSubstitute mocks throughout — these tests exercise <see cref="DemoRunner"/>'s own branching,
/// not the real pipeline or mapping richness (VLD-S4). <see cref="IServiceScopeFactory"/> is also
/// mocked; see <see cref="DemoRunnerScopeLifetimeTests"/> for the scope-creation/disposal
/// verification, which needs a slightly different (real, spy-backed) setup.
/// </remarks>
public sealed class DemoRunnerTests
{
    private static readonly byte[] SmallPdf = [0x25, 0x50, 0x44, 0x46]; // "%PDF" — well under any size cap

    private readonly IVerificationPipeline _pipeline = Substitute.For<IVerificationPipeline>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IVerificationOutcomeMapper _mapper = Substitute.For<IVerificationOutcomeMapper>();
    private readonly DemoDataService _demoDataService = new();

    private DemoRunner CreateSut(DemoOptions demoOptions, long maxSizeBytes = PdfExtractionOptions.DefaultMaxSizeBytes)
    {
        // Wires _scopeFactory.CreateScope() (and therefore the CreateAsyncScope() extension
        // DemoRunner actually calls) to a scope whose ServiceProvider resolves the mocked pipeline.
        var scope = Substitute.For<IServiceScope>();
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IVerificationPipeline)).Returns(_pipeline);
        scope.ServiceProvider.Returns(provider);
        _scopeFactory.CreateScope().Returns(scope);

        return new DemoRunner(
            _scopeFactory,
            _mapper,
            _demoDataService,
            MsOptions.Create(demoOptions),
            MsOptions.Create(new PdfExtractionOptions { MaxSizeBytes = maxSizeBytes }),
            NullLogger<DemoRunner>.Instance);
    }

    private static VerificationOutcome CreateDummyOutcome() =>
        new(
            Job: new VerificationJob(Guid.NewGuid(), "dummy-hash", DateTimeOffset.UtcNow, VerificationJobStatus.Completed),
            // Summary (VerdictSummary) has no public constructor/factory outside its own assembly
            // (Veriqan.Application) — null! is safe here because IVerificationOutcomeMapper is
            // mocked and never dereferences it.
            Summary: null!,
            Findings: Array.Empty<RuleFinding>());

    [Fact]
    public async Task RunAsync_LiveDisabled_ReturnsCannedCase()
    {
        var sut = CreateSut(new DemoOptions { LiveModeEnabled = false });
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await sut.RunAsync(SmallPdf, "estado-cuenta-visa-demo.pdf", cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsLive.ShouldBeFalse();
        result.Value.Case.FileName.ShouldBe("estado-cuenta-visa-demo.pdf");
        result.Value.Case.Signal.ShouldBe(VerdictSignal.Green);

        _scopeFactory.DidNotReceive().CreateScope();
        _ = _pipeline.DidNotReceive().ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LiveFails_FallbackEnabled_ReturnsCannedCase()
    {
        var sut = CreateSut(new DemoOptions { LiveModeEnabled = true, FallbackOnFailure = true });
        var cancellationToken = TestContext.Current.CancellationToken;

        _pipeline.ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithFailure("simulated pipeline failure")));

        var result = await sut.RunAsync(SmallPdf, "estado-cuenta-mc-demo.pdf", cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsLive.ShouldBeFalse();
        result.Value.Case.FileName.ShouldBe("estado-cuenta-mc-demo.pdf");
        _mapper.DidNotReceive().Map(
            Arg.Any<VerificationOutcome>(),
            Arg.Any<IReadOnlyDictionary<int, byte[]>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LiveThrows_FallbackEnabled_ReturnsCannedCase()
    {
        var sut = CreateSut(new DemoOptions { LiveModeEnabled = true, FallbackOnFailure = true });
        var cancellationToken = TestContext.Current.CancellationToken;

        _pipeline.ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom — simulated live pipeline crash"));

        var result = await sut.RunAsync(SmallPdf, "estado-cuenta-gold-demo.pdf", cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsLive.ShouldBeFalse();
        result.Value.Case.FileName.ShouldBe("estado-cuenta-gold-demo.pdf");
    }

    [Fact]
    public async Task RunAsync_LiveTimesOut_FallbackEnabled_ReturnsCannedCase()
    {
        var sut = CreateSut(new DemoOptions
        {
            LiveModeEnabled = true,
            FallbackOnFailure = true,
            LiveTimeout = TimeSpan.FromMilliseconds(50),
        });
        var cancellationToken = TestContext.Current.CancellationToken;

        _pipeline.ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(1);
                // Deliberately longer than LiveTimeout (50ms) so the linked token trips first.
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return Result<VerificationOutcome>.WithSuccess(CreateDummyOutcome());
            });

        var result = await sut.RunAsync(SmallPdf, "estado-cuenta-escaneado.pdf", cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsLive.ShouldBeFalse();
        result.Value.Case.FileName.ShouldBe("estado-cuenta-escaneado.pdf");
    }

    [Fact]
    public async Task RunAsync_LiveFails_FallbackDisabled_PropagatesFailure()
    {
        var sut = CreateSut(new DemoOptions { LiveModeEnabled = true, FallbackOnFailure = false });
        var cancellationToken = TestContext.Current.CancellationToken;

        _pipeline.ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<VerificationOutcome>.WithFailure("simulated pipeline failure")));

        var result = await sut.RunAsync(SmallPdf, "estado-cuenta-mc-demo.pdf", cancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_OversizedPdf_PipelineNeverInvoked()
    {
        // MaxSizeBytes = 3 bytes; SmallPdf is 4 bytes → guaranteed oversized.
        var sut = CreateSut(new DemoOptions { LiveModeEnabled = true }, maxSizeBytes: 3);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await sut.RunAsync(SmallPdf, "estado-cuenta-visa-demo.pdf", cancellationToken);

        result.IsFailure.ShouldBeTrue();

        _scopeFactory.DidNotReceive().CreateScope();
        _ = _pipeline.DidNotReceive().ProcessAsync(Arg.Any<StatementSubmission>(), Arg.Any<CancellationToken>());
    }
}
