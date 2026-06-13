using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prisma.Orion.Worker.Ingestion;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Orion.Worker.Tests;

/// <summary>
/// Unit tests for <see cref="SignalRIngestionBroadcaster"/> (MVP-PATH 1.3 + 1.5): it must broadcast via
/// the hub context on the Ember <c>"ReceiveMessage"</c> protocol, honor Railway-Oriented semantics, and
/// (1.5) stamp a clearance token on the event before sending.
/// </summary>
public sealed class SignalRIngestionBroadcasterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly SiaraActor SampleActor = new()
    {
        ActorId = "orion-downloader-test",
        ActorType = SiaraActorType.ServiceAccount,
    };

    private static DocumentDownloadedEvent SampleEvent() => new()
    {
        FileId = Guid.NewGuid(),
        FileName = "doc.pdf",
        Source = "SIARA",
        CorrelationId = Guid.NewGuid(),
    };

    private static (
        SignalRIngestionBroadcaster sut,
        IClientProxy all,
        IProcessClearanceTokenService clearanceService,
        ISiaraActorIdentityProvider actorProvider) CreateSut(
            IProcessClearanceTokenService? clearanceService = null,
            ISiaraActorIdentityProvider? actorProvider = null)
    {
        var hubContext = Substitute.For<IHubContext<IngestionHub>>();
        var clients = Substitute.For<IHubClients>();
        var all = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(all);

        clearanceService ??= CreateSucceedingClearanceService();
        actorProvider ??= CreateSucceedingActorProvider();

        var sut = new SignalRIngestionBroadcaster(
            hubContext,
            clearanceService,
            actorProvider,
            NullLogger<SignalRIngestionBroadcaster>.Instance);

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
            new SignalRIngestionBroadcaster(
                null!,
                Substitute.For<IProcessClearanceTokenService>(),
                Substitute.For<ISiaraActorIdentityProvider>(),
                NullLogger<SignalRIngestionBroadcaster>.Instance));

    [Fact]
    public void Constructor_NullClearanceTokenService_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SignalRIngestionBroadcaster(
                Substitute.For<IHubContext<IngestionHub>>(),
                null!,
                Substitute.For<ISiaraActorIdentityProvider>(),
                NullLogger<SignalRIngestionBroadcaster>.Instance));

    [Fact]
    public void Constructor_NullActorIdentityProvider_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SignalRIngestionBroadcaster(
                Substitute.For<IHubContext<IngestionHub>>(),
                Substitute.For<IProcessClearanceTokenService>(),
                null!,
                NullLogger<SignalRIngestionBroadcaster>.Instance));

    [Fact]
    public async Task SendToAllAsync_BroadcastsOnReceiveMessageProtocol()
    {
        var (sut, all, _, _) = CreateSut();
        var evt = SampleEvent();

        var result = await sut.SendToAllAsync(evt, Ct);

        result.IsSuccess.ShouldBeTrue();
        // SendAsync(method, arg, ct) lowers to SendCoreAsync(method, new[]{arg}, ct).
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

        // The stamped event passed to SendCoreAsync must have a non-empty ClearanceToken.
        await all.Received(1).SendCoreAsync(
            "ReceiveMessage",
            Arg.Is<object?[]>(args =>
                args.Length == 1
                && args[0] != null
                && !string.IsNullOrWhiteSpace(((DocumentDownloadedEvent)args[0]!).ClearanceToken)),
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
