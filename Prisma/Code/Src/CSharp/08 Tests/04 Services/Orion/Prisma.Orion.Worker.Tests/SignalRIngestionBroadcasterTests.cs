using ExxerCube.Prisma.Domain.Events;
using IndQuestResults.Operations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prisma.Orion.Worker.Ingestion;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Orion.Worker.Tests;

/// <summary>
/// Unit tests for <see cref="SignalRIngestionBroadcaster"/> (MVP-PATH 1.3): it must broadcast via the hub
/// context on the Ember <c>"ReceiveMessage"</c> protocol and honor Railway-Oriented semantics.
/// </summary>
public sealed class SignalRIngestionBroadcasterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DocumentDownloadedEvent SampleEvent() => new()
    {
        FileId = Guid.NewGuid(),
        FileName = "doc.pdf",
        Source = "SIARA",
        CorrelationId = Guid.NewGuid(),
    };

    private static (SignalRIngestionBroadcaster sut, IClientProxy all) CreateSut()
    {
        var hubContext = Substitute.For<IHubContext<IngestionHub>>();
        var clients = Substitute.For<IHubClients>();
        var all = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(all);

        var sut = new SignalRIngestionBroadcaster(hubContext, NullLogger<SignalRIngestionBroadcaster>.Instance);
        return (sut, all);
    }

    [Fact]
    public void Constructor_NullHubContext_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SignalRIngestionBroadcaster(null!, NullLogger<SignalRIngestionBroadcaster>.Instance));

    [Fact]
    public async Task SendToAllAsync_BroadcastsOnReceiveMessageProtocol()
    {
        var (sut, all) = CreateSut();
        var evt = SampleEvent();

        var result = await sut.SendToAllAsync(evt, Ct);

        result.IsSuccess.ShouldBeTrue();
        // SendAsync(method, arg, ct) lowers to SendCoreAsync(method, new[]{arg}, ct).
        await all.Received(1).SendCoreAsync(
            "ReceiveMessage",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], evt)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_PreCancelledToken_ReturnsCancelledWithoutSending()
    {
        var (sut, all) = CreateSut();

        var result = await sut.SendToAllAsync(SampleEvent(), new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
        await all.DidNotReceive().SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_NullData_ReturnsFailureWithoutSending()
    {
        var (sut, all) = CreateSut();

        var result = await sut.SendToAllAsync(null!, Ct);

        result.IsFailure.ShouldBeTrue();
        await all.DidNotReceive().SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToAllAsync_WhenTransportThrows_ReturnsFailure()
    {
        var (sut, all) = CreateSut();
        all.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("transport down"));

        var result = await sut.SendToAllAsync(SampleEvent(), Ct);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Contains("transport down"));
    }
}
