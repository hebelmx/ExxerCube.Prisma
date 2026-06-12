using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Orion.Worker.Tests;

/// <summary>
/// End-to-end wire test for the MVP-PATH 1.3 ingestion transport: it boots the real Orion worker (which
/// hosts <see cref="Prisma.Orion.Worker.Ingestion.IngestionHub"/>), connects a real SignalR client to
/// <c>/hubs/ingestion</c>, broadcasts a <see cref="DocumentDownloadedEvent"/> through the production
/// <see cref="IExxerHub{T}"/> broadcaster, and asserts the client receives it — proving the Downloader→
/// Extractor edge delivers over the real Ember transport (not the old stub).
/// </summary>
[Trait("Category", "Integration")]
public sealed class IngestionHubWireTests
{
    [Fact]
    public async Task Broadcast_DocumentDownloadedEvent_IsReceivedByConnectedClient()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var application = new OrionWorkerApplication();
        // Force the host to build so the TestServer + hub endpoint exist.
        _ = application.Services;
        var server = application.Server;

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                server.BaseAddress + "hubs/ingestion",
                options => options.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();

        DocumentDownloadedEvent? received = null;
        using var gate = new ManualResetEventSlim(false);
        connection.On<DocumentDownloadedEvent>("ReceiveMessage", evt =>
        {
            received = evt;
            gate.Set();
        });

        await connection.StartAsync(ct);
        connection.State.ShouldBe(HubConnectionState.Connected);

        var broadcaster = application.Services.GetRequiredService<IExxerHub<DocumentDownloadedEvent>>();
        var sent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "expediente.pdf",
            Source = "SIARA",
            FileSizeBytes = 1234,
            CorrelationId = Guid.NewGuid(),
        };

        var broadcastResult = await broadcaster.SendToAllAsync(sent, ct);

        broadcastResult.IsSuccess.ShouldBeTrue();
        gate.Wait(TimeSpan.FromSeconds(10), ct).ShouldBeTrue("the connected client should receive the broadcast within 10s");
        received.ShouldNotBeNull();
        received!.FileId.ShouldBe(sent.FileId);
        received.FileName.ShouldBe(sent.FileName);
        received.CorrelationId.ShouldBe(sent.CorrelationId);

        await connection.StopAsync(ct);
    }
}
