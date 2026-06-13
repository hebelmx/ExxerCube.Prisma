using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prisma.Athena.Worker.Reconciliation;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Athena.Worker.Tests;

/// <summary>
/// Unit tests for <see cref="SignalRReconciliationBroadcaster"/> (MVP-PATH 1.4 + 1.5): it must broadcast
/// via the hub context on the Ember <c>"ReceiveMessage"</c> protocol, honor Railway-Oriented semantics, and
/// (1.5) stamp a clearance token on the event before sending — mirroring
/// <c>SignalRIngestionBroadcasterTests</c> on the Downloader → Extractor edge.
/// </summary>
public sealed class SignalRReconciliationBroadcasterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly SiaraActor SampleActor = new()
    {
        ActorId = "athena-extractor-test",
        ActorType = SiaraActorType.ServiceAccount,
    };

    private static ExtractionCompletedEvent SampleEvent() => new()
    {
        FileId = Guid.NewGuid(),
        Path = "2026/06/12/doc.fusion.json",
        CorrelationId = Guid.NewGuid(),
    };

    private static (
        SignalRReconciliationBroadcaster sut,
        IClientProxy all,
        IProcessClearanceTokenService clearanceService,
        ISiaraActorIdentityProvider actorProvider) CreateSut(
            IProcessClearanceTokenService? clearanceService = null,
            ISiaraActorIdentityProvider? actorProvider = null)
    {
        var hubContext = Substitute.For<IHubContext<ReconciliationHub>>();
        var clients = Substitute.For<IHubClients>();
        var all = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(all);

        clearanceService ??= CreateSucceedingClearanceService();
        actorProvider ??= CreateSucceedingActorProvider();

        var sut = new SignalRReconciliationBroadcaster(
            hubContext,
            clearanceService,
            actorProvider,
            NullLogger<SignalRReconciliationBroadcaster>.Instance);

        return (sut, all, clearanceService, actorProvider);
    }

    private static IProcessClearanceTokenService CreateSucceedingClearanceService()
    {
        var svc = Substitute.For<IProcessClearanceTokenService>();
        svc.MintAsync(Arg.Any<SiaraActor>(), Arg.Any<ProcessClearance>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<string>.Success($"fake-token-for-{ci.ArgAt<Guid>(2):D}"));
        return svc;
    }

    private static ISiaraActorIdentityProvider CreateSucceedingActorProvider()
    {
        var prov = Substitute.For<ISiaraActorIdentityProvider>();
        prov.GetCurrentActorAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SiaraActor>.Success(SampleActor));
        return prov;
    }

    [Fact]
    public void Constructor_NullHubContext_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SignalRReconciliationBroadcaster(
                null!,
                Substitute.For<IProcessClearanceTokenService>(),
                Substitute.For<ISiaraActorIdentityProvider>(),
                NullLogger<SignalRReconciliationBroadcaster>.Instance));

    [Fact]
    public void Constructor_NullClearanceTokenService_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SignalRReconciliationBroadcaster(
                Substitute.For<IHubContext<ReconciliationHub>>(),
                null!,
                Substitute.For<ISiaraActorIdentityProvider>(),
                NullLogger<SignalRReconciliationBroadcaster>.Instance));

    [Fact]
    public void Constructor_NullActorIdentityProvider_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SignalRReconciliationBroadcaster(
                Substitute.For<IHubContext<ReconciliationHub>>(),
                Substitute.For<IProcessClearanceTokenService>(),
                null!,
                NullLogger<SignalRReconciliationBroadcaster>.Instance));

    [Fact]
    public async Task SendToAllAsync_BroadcastsOnReceiveMessageProtocol()
    {
        var (sut, all, _, _) = CreateSut();
        var evt = SampleEvent();

        var result = await sut.SendToAllAsync(evt, Ct);

        result.IsSuccess.ShouldBeTrue();
        await all.Received(1).SendCoreAsync(
            "ReceiveMessage",
            Arg.Is<object?[]>(args => args.Length == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_StampsClearanceTokenOnEvent()
    {
        var (sut, all, _, _) = CreateSut();
        var evt = SampleEvent();

        await sut.SendToAllAsync(evt, Ct);

        await all.Received(1).SendCoreAsync(
            "ReceiveMessage",
            Arg.Is<object?[]>(args =>
                args.Length == 1
                && args[0] != null
                && !string.IsNullOrWhiteSpace(((ExtractionCompletedEvent)args[0]!).ClearanceToken)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_PreCancelledToken_ReturnsCancelledWithoutSending()
    {
        var (sut, all, _, _) = CreateSut();

        var result = await sut.SendToAllAsync(SampleEvent(), new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
        await all.DidNotReceive().SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_NullData_ReturnsFailureWithoutSending()
    {
        var (sut, all, _, _) = CreateSut();

        var result = await sut.SendToAllAsync(null!, Ct);

        result.IsFailure.ShouldBeTrue();
        await all.DidNotReceive().SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_WhenTransportThrows_ReturnsFailure()
    {
        var (sut, all, _, _) = CreateSut();
        all.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("transport down"));

        var result = await sut.SendToAllAsync(SampleEvent(), Ct);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Contains("transport down"));
    }

    [Fact]
    public async Task SendToAllAsync_WhenMintingFails_DoesNotSendAndReturnsFailure()
    {
        var failingClearanceService = Substitute.For<IProcessClearanceTokenService>();
        failingClearanceService.MintAsync(Arg.Any<SiaraActor>(), Arg.Any<ProcessClearance>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.WithFailure("secret not configured"));

        var (sut, all, _, _) = CreateSut(clearanceService: failingClearanceService);

        var result = await sut.SendToAllAsync(SampleEvent(), Ct);

        result.IsFailure.ShouldBeTrue();
        await all.DidNotReceive().SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_WhenActorResolutionFails_DoesNotSendAndReturnsFailure()
    {
        var failingActorProvider = Substitute.For<ISiaraActorIdentityProvider>();
        failingActorProvider.GetCurrentActorAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SiaraActor>.WithFailure("actor not configured"));

        var (sut, all, _, _) = CreateSut(actorProvider: failingActorProvider);

        var result = await sut.SendToAllAsync(SampleEvent(), Ct);

        result.IsFailure.ShouldBeTrue();
        await all.DidNotReceive().SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
