namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="SessionPassthroughSiaraSessionProvider"/> that the universal
/// <see cref="SiaraSessionProviderContract"/> intentionally leaves to the implementation (ADR-005 §5):
/// transport selection, fail-closed confirmation, reference fallback, and storage-state capture.
/// </summary>
public sealed class SessionPassthroughSiaraSessionProviderTests
{
    private static SiaraSessionRequest RequestWith(string? endpoint, string? requestedBy = "watch-loop") =>
        new() { ExistingContextEndpoint = endpoint, RequestedBy = requestedBy };

    [Fact]
    public void Mode_IsSessionPassthrough()
    {
        var sut = SessionPassthroughTestFactory.CreateProvider(
            SessionPassthroughTestFactory.CreateAuthenticatedContextMock());

        sut.Mode.ShouldBe(SiaraAuthMode.SessionPassthrough);
    }

    [Fact]
    public async Task AcquireAsync_StorageStateTransport_AttachesViaLoadStorageState()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(
            context,
            new SiaraPassthroughOptions { Transport = SiaraPassthroughTransport.StorageState, PostLoginSelector = "#dashboard" });

        var result = await sut.AcquireAsync(RequestWith("captured-state"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await context.Received(1).LoadStorageStateAsync("captured-state", Arg.Any<CancellationToken>());
        await context.DidNotReceive().ConnectToExistingContextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_CdpTransport_AttachesViaConnect()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(
            context,
            new SiaraPassthroughOptions { Transport = SiaraPassthroughTransport.Cdp, PostLoginSelector = "#dashboard" });

        var result = await sut.AcquireAsync(RequestWith("ws://localhost:9222/cdp"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await context.Received(1).ConnectToExistingContextAsync("ws://localhost:9222/cdp", Arg.Any<CancellationToken>());
        await context.DidNotReceive().LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenProbeReportsUnauthenticated_FailsClosed()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        context.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        var sut = SessionPassthroughTestFactory.CreateProvider(context);

        var result = await sut.AcquireAsync(RequestWith("captured-state"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
        // Fail-closed: no storage-state is captured from an unauthenticated context.
        await context.DidNotReceive().ExportStorageStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenAttachFails_ReturnsFailureWithoutProbing()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        context.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("no browser launched"));
        var sut = SessionPassthroughTestFactory.CreateProvider(context);

        var result = await sut.AcquireAsync(RequestWith("captured-state"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await context.DidNotReceive().IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_NoRequestEndpointAndNoConfiguredRef_FailsBeforeTouchingBrowser()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(
            context,
            new SiaraPassthroughOptions { StorageStateRef = null, PostLoginSelector = "#dashboard" });

        var result = await sut.AcquireAsync(RequestWith(endpoint: null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await context.DidNotReceive().LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await context.DidNotReceive().ConnectToExistingContextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_NoRequestEndpoint_UsesConfiguredStorageStateRef()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(
            context,
            new SiaraPassthroughOptions { StorageStateRef = "configured-default-state", PostLoginSelector = "#dashboard" });

        var result = await sut.AcquireAsync(RequestWith(endpoint: null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await context.Received(1).LoadStorageStateAsync("configured-default-state", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_CapturesExportedStorageState()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(context);

        var result = await sut.AcquireAsync(RequestWith("imported-ref"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StorageStateRef.ShouldBe("passthrough-captured-storage-state");
    }

    [Fact]
    public async Task AcquireAsync_WhenExportUnavailable_FallsBackToImportedRef()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        context.ExportStorageStateAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.WithFailure("no session to export"));
        var sut = SessionPassthroughTestFactory.CreateProvider(context);

        var result = await sut.AcquireAsync(RequestWith("imported-ref"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StorageStateRef.ShouldBe("imported-ref");
    }

    [Fact]
    public async Task AcquireAsync_RecordsTrustworthyAcquiringActor()
    {
        // The actor comes from the identity provider (the fake), not from the request's RequestedBy.
        var actorIdentity = new FakeSiaraActorIdentityProvider(actorId: "passthrough-service-account");
        var sut = SessionPassthroughTestFactory.CreateProvider(
            SessionPassthroughTestFactory.CreateAuthenticatedContextMock(),
            actorIdentity: actorIdentity);

        var result = await sut.AcquireAsync(RequestWith("imported-ref", requestedBy: "orion-watcher"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AcquiredBy.ActorId.ShouldBe("passthrough-service-account");
        result.Value.Mode.ShouldBe(SiaraAuthMode.SessionPassthrough);
    }

    [Fact]
    public async Task EnsureValidAsync_WhenExternalSessionDied_FailsClosed()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(context);
        var acquired = await sut.AcquireAsync(RequestWith("imported-ref"), TestContext.Current.CancellationToken);
        acquired.IsSuccess.ShouldBeTrue();

        // The external session dies underneath us: the probe now reports unauthenticated.
        context.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        var result = await sut.EnsureValidAsync(acquired.Value!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task EnsureValidAsync_WhenStillAuthenticated_ReturnsSession()
    {
        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(context);
        var acquired = await sut.AcquireAsync(RequestWith("imported-ref"), TestContext.Current.CancellationToken);

        var result = await sut.EnsureValidAsync(acquired.Value!, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.SessionId.ShouldBe(acquired.Value!.SessionId);
    }

    [Fact]
    public async Task AcquireAsync_WhenActorCannotBeResolved_FailsClosed()
    {
        var actor = Substitute.For<ISiaraActorIdentityProvider>();
        actor.GetCurrentActorAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SiaraActor>.WithFailure("no trustworthy actor"));

        var context = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        var sut = SessionPassthroughTestFactory.CreateProvider(context, actorIdentity: actor);

        var result = await sut.AcquireAsync(RequestWith("imported-ref"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await context.DidNotReceive().LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await context.DidNotReceive().ConnectToExistingContextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
