using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Athena.Worker.Tests;

/// <summary>
/// End-to-end wire tests for the MVP-PATH 1.4 Reconciliator edge (hub delivery) and the connection-level
/// JWT bearer authentication added as a follow-up to MVP-PATH 1.5.
/// </summary>
/// <remarks>
/// The tests boot the real Athena Extractor worker (which hosts
/// <see cref="Prisma.Athena.Worker.Reconciliation.ReconciliationHub"/>)
/// via <see cref="AthenaWorkerApplication"/> and exercise the hub at the transport level.
/// <list type="bullet">
///   <item>Happy path — an authenticated client with <c>ProcessClearance.Reconcile</c> connects and receives
///   a broadcast event.</item>
///   <item>Unauthenticated — a client with no token is refused (connection throws).</item>
///   <item>Wrong clearance — a client with a valid JWT but <c>ProcessClearance.Extract</c> is refused.</item>
/// </list>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ReconciliationHubWireTests
{
    [Fact]
    public async Task Broadcast_ExtractionCompletedEvent_IsReceivedByConnectedClient()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var application = new AthenaWorkerApplication();
        _ = application.Services;
        var server = application.Server;

        var token = MintToken(AthenaWorkerApplication.TestJwtSecret, "reconciliator-test", "Reconcile");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                server.BaseAddress + "hubs/reconciliation",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
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

    /// <summary>
    /// A client that presents no bearer token must be refused: <c>StartAsync</c> throws (HTTP 401/403
    /// on the negotiate / WebSocket upgrade). The connection never reaches <c>Connected</c> state.
    /// </summary>
    [Fact]
    public async Task Connect_WithNoToken_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var application = new AthenaWorkerApplication();
        _ = application.Services;
        var server = application.Server;

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                server.BaseAddress + "hubs/reconciliation",
                options => options.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();

        // The hub requires the RequireReconcileClearance policy; no token → 401 on negotiate →
        // StartAsync throws. The connection must NOT reach Connected state.
        await Should.ThrowAsync<Exception>(
            () => connection.StartAsync(ct));

        connection.State.ShouldNotBe(HubConnectionState.Connected);
    }

    /// <summary>
    /// A client that presents a valid JWT signed with the correct key but carrying a different clearance
    /// (<c>ProcessClearance.Extract</c>) must be refused: the <c>RequireReconcileClearance</c> policy
    /// requires exactly <c>Reconcile</c>. <c>StartAsync</c> throws (HTTP 403 on the upgrade).
    /// </summary>
    [Fact]
    public async Task Connect_WithWrongClearanceToken_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var application = new AthenaWorkerApplication();
        _ = application.Services;
        var server = application.Server;

        // Mint a token signed with the correct secret but clearance = Extract (not Reconcile).
        var wrongToken = MintToken(AthenaWorkerApplication.TestJwtSecret, "athena-extractor-test", "Extract");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                server.BaseAddress + "hubs/reconciliation",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(wrongToken);
                })
            .Build();

        // Valid JWT, wrong clearance claim → 403 on negotiate → StartAsync throws.
        await Should.ThrowAsync<Exception>(
            () => connection.StartAsync(ct));

        connection.State.ShouldNotBe(HubConnectionState.Connected);
    }

    /// <summary>
    /// RC6 3.9 (zero-downtime HMAC secret rotation, ADR-012 addendum): a client presenting a bearer token
    /// signed with a <em>retired</em> secret must still be accepted at the hub-connection level as long as
    /// that secret is listed in <c>ProcessIdentity:PreviousJwtSecrets</c>. This exercises the same
    /// <see cref="ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity.ProcessIdentitySigningKeys.BuildAcceptedKeys"/>
    /// path used by <c>AddJwtBearer</c> in <c>Program.cs</c>, and — because the grace secret is supplied
    /// purely via in-memory <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> keys
    /// (<c>ProcessIdentity:PreviousJwtSecrets:0</c>) rather than an object initializer — also pins that the
    /// options binder correctly materializes the list from indexed configuration keys.
    /// </summary>
    [Fact]
    public async Task Connect_WithTokenSignedByPreviousSecret_InGraceList_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        const string oldSecret = AthenaWorkerApplication.TestJwtSecret;
        const string newSecret = "ATHENA-WORKER-TESTS-JWT-SECRET-ROTATED-NEW-32-CHARS+";

        await using var application = new AthenaWorkerApplication(new Dictionary<string, string?>
        {
            ["ProcessIdentity:JwtSecret"] = newSecret,
            ["ProcessIdentity:PreviousJwtSecrets:0"] = oldSecret,
        });
        _ = application.Services;
        var server = application.Server;

        // Token minted with the OLD (now-retired-for-minting) secret must still be accepted because the
        // host's grace list carries it.
        var token = MintToken(oldSecret, "reconciliator-test", "Reconcile");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                server.BaseAddress + "hubs/reconciliation",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
            .Build();

        await connection.StartAsync(ct);
        connection.State.ShouldBe(HubConnectionState.Connected);

        await connection.StopAsync(ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Mints a test JWT with the given secret, actor, and clearance claim, using the same algorithm and
    /// issuer/audience values that <c>JwtProcessClearanceTokenService</c> uses — so the hub's bearer
    /// validation accepts it.
    /// </summary>
    private static string MintToken(string secret, string actorId, string clearance)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, actorId),
            new Claim("actor_type", "ServiceAccount"),
            new Claim("clearance", clearance),
            new Claim("file_id", Guid.Empty.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
        };

        var token = new JwtSecurityToken(
            issuer: "prisma-pipeline",
            audience: "prisma-pipeline",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
