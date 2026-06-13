using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Athena.Worker.Tests;

/// <summary>
/// End-to-end wire test for the MVP-PATH 1.4 Reconciliator edge: it boots the real Athena Extractor worker
/// (which hosts <see cref="Prisma.Athena.Worker.Reconciliation.ReconciliationHub"/>), connects a real SignalR
/// client to <c>/hubs/reconciliation</c>, broadcasts an <see cref="ExtractionCompletedEvent"/> through the
/// production <see cref="IExxerHub{T}"/> broadcaster, and asserts the client receives it — proving the
/// Extractor → Reconciliator edge delivers over the real Ember transport.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReconciliationHubWireTests
{
    [Fact]
    public async Task Broadcast_ExtractionCompletedEvent_IsReceivedByConnectedClient()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var application = new AthenaWorkerApplication();
        // Force the host to build so the TestServer + hub endpoint exist.
        _ = application.Services;
        var server = application.Server;

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                server.BaseAddress + "hubs/reconciliation",
                options => options.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();

        ExtractionCompletedEvent? received = null;
        using var gate = new ManualResetEventSlim(false);
        connection.On<ExtractionCompletedEvent>("ReceiveMessage", evt =>
        {
            received = evt;
            gate.Set();
        });

        await connection.StartAsync(ct);
        connection.State.ShouldBe(HubConnectionState.Connected);

        var broadcaster = application.Services.GetRequiredService<IExxerHub<ExtractionCompletedEvent>>();
        var sent = new ExtractionCompletedEvent
        {
            FileId = Guid.NewGuid(),
            Path = "2026/06/12/expediente.fusion.json",
            FieldsFused = 7,
            ConflictsDetected = 1,
            CorrelationId = Guid.NewGuid(),
        };

        var broadcastResult = await broadcaster.SendToAllAsync(sent, ct);

        broadcastResult.IsSuccess.ShouldBeTrue();
        gate.Wait(TimeSpan.FromSeconds(10), ct).ShouldBeTrue("the connected client should receive the broadcast within 10s");
        received.ShouldNotBeNull();
        received!.FileId.ShouldBe(sent.FileId);
        received.Path.ShouldBe(sent.Path);
        received.CorrelationId.ShouldBe(sent.CorrelationId);

        await connection.StopAsync(ct);
    }
}
