namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mutation-killing coverage for the three providers' <strong>fail-safe branches</strong>: each post-await
/// <c>IsCancelled()</c> guard (so a "remove the early-return" block mutation is observable), the
/// best-effort release logging branch, and the capture <c>||</c>/<c>&amp;&amp;</c> logical guards. These
/// paths exist precisely so the system never proceeds on a cancelled/half-captured session.
/// </summary>
public sealed class SiaraProviderFailClosedMutationTests
{
    private static SiaraSessionRequest Request() => new() { RequestedBy = "mutation-test" };

    private static SiaraSession SessionFor(SiaraAuthMode mode, string storageRef) => new()
    {
        SessionId = $"{mode}-fixture",
        Mode = mode,
        StorageStateRef = storageRef,
        ExpiresAt = null,
        AcquiredBy = new SiaraActor { ActorId = "actor", ActorType = SiaraActorType.ServiceAccount },
    };

    private static ISiaraActorIdentityProvider CancellingActor()
    {
        var actor = Substitute.For<ISiaraActorIdentityProvider>();
        actor.GetCurrentActorAsync(Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<SiaraActor>());
        return actor;
    }

    // ---------------------------------------------------------------- AutomatedLogin

    [Fact]
    public async Task Automated_AcquireAsync_CancelledAtEachStep_ReturnsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;

        // actor
        (await AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(),
            new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker(),
            actorIdentity: CancellingActor())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        // credentials
        var source = Substitute.For<ISiaraCredentialSource>();
        source.GetCredentialsAsync(Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<SiaraCredential>());
        (await AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(),
            source,
            AutomatedLoginTestFactory.CreateCircuitBreaker())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        // launch
        var launchAgent = AutomatedLoginTestFactory.CreateSuccessfulAgentMock();
        launchAgent.LaunchBrowserAsync(Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await AutomatedLoginTestFactory.CreateProvider(
            launchAgent, AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        // navigate
        var navAgent = AutomatedLoginTestFactory.CreateSuccessfulAgentMock();
        navAgent.NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await AutomatedLoginTestFactory.CreateProvider(
            navAgent, AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        // login
        var login = Substitute.For<ISiaraLoginService>();
        login.LoginAsync(Arg.Any<IBrowserAutomationAgent>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled());
        (await AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(), AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            login, new FakeSiaraCredentialSource(), AutomatedLoginTestFactory.CreateCircuitBreaker())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        // probe
        var probeCtx = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        probeCtx.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<bool>());
        (await AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(), probeCtx,
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        // export
        var exportCtx = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        exportCtx.ExportStorageStateAsync(Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<string>());
        (await AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(), exportCtx,
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Automated_AcquireAsync_ExportSucceedsButEmpty_FailsClosed()
    {
        var ctx = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        ctx.ExportStorageStateAsync(Arg.Any<CancellationToken>()).Returns(Result<string>.Success("   "));
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(), ctx,
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker());

        var result = await sut.AcquireAsync(Request(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue("a whitespace-only captured session is not a usable bearer secret");
    }

    [Fact]
    public async Task Automated_EnsureValidAsync_CancelledAtEachStep_ReturnsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = SessionFor(SiaraAuthMode.AutomatedLogin, "ref");

        var rehydrateCtx = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        rehydrateCtx.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(), rehydrateCtx,
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker())
            .EnsureValidAsync(session, ct)).IsCancelled().ShouldBeTrue();

        var probeCtx = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        probeCtx.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<bool>());
        (await AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(), probeCtx,
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker())
            .EnsureValidAsync(session, ct)).IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Automated_ReleaseAsync_WhenCloseFails_StillReleases()
    {
        var agent = AutomatedLoginTestFactory.CreateSuccessfulAgentMock();
        agent.CloseBrowserAsync(Arg.Any<CancellationToken>()).Returns(Result.WithFailure("already closed"));
        var sut = AutomatedLoginTestFactory.CreateProvider(
            agent, AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(), new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker());

        var result = await sut.ReleaseAsync(SessionFor(SiaraAuthMode.AutomatedLogin, "ref"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("release is best-effort: a failed close still releases the session");
    }

    // ---------------------------------------------------------------- InteractiveLogin

    [Fact]
    public async Task Interactive_AcquireAsync_CancelledAtEachStep_ReturnsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;

        (await InteractiveLoginTestFactory.CreateProvider(actorIdentity: CancellingActor())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        var launchAgent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        launchAgent.LaunchBrowserAsync(Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await InteractiveLoginTestFactory.CreateProvider(launchAgent, InteractiveLoginTestFactory.CreateAuthenticatedContextMock())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        var navAgent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        navAgent.NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await InteractiveLoginTestFactory.CreateProvider(navAgent, InteractiveLoginTestFactory.CreateAuthenticatedContextMock())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        var waitAgent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        waitAgent.WaitForSelectorAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await InteractiveLoginTestFactory.CreateProvider(waitAgent, InteractiveLoginTestFactory.CreateAuthenticatedContextMock())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        var exportCtx = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        exportCtx.ExportStorageStateAsync(Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<string>());
        (await InteractiveLoginTestFactory.CreateProvider(InteractiveLoginTestFactory.CreateSuccessfulAgentMock(), exportCtx)
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Interactive_AcquireAsync_ExportSucceedsButEmpty_FailsClosed()
    {
        var ctx = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        ctx.ExportStorageStateAsync(Arg.Any<CancellationToken>()).Returns(Result<string>.Success(""));
        var sut = InteractiveLoginTestFactory.CreateProvider(InteractiveLoginTestFactory.CreateSuccessfulAgentMock(), ctx);

        var result = await sut.AcquireAsync(Request(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue("an empty captured session is not a usable bearer secret");
    }

    [Fact]
    public async Task Interactive_EnsureValidAsync_CancelledAtEachStep_ReturnsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = SessionFor(SiaraAuthMode.InteractiveLogin, "ref");

        var rehydrateCtx = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        rehydrateCtx.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await InteractiveLoginTestFactory.CreateProvider(InteractiveLoginTestFactory.CreateSuccessfulAgentMock(), rehydrateCtx)
            .EnsureValidAsync(session, ct)).IsCancelled().ShouldBeTrue();

        var probeCtx = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        probeCtx.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<bool>());
        (await InteractiveLoginTestFactory.CreateProvider(InteractiveLoginTestFactory.CreateSuccessfulAgentMock(), probeCtx)
            .EnsureValidAsync(session, ct)).IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Interactive_ReleaseAsync_WhenCloseFails_StillReleases()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        agent.CloseBrowserAsync(Arg.Any<CancellationToken>()).Returns(Result.WithFailure("already closed"));
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, InteractiveLoginTestFactory.CreateAuthenticatedContextMock());

        var result = await sut.ReleaseAsync(SessionFor(SiaraAuthMode.InteractiveLogin, "ref"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    // ---------------------------------------------------------------- SessionPassthrough

    private static SiaraPassthroughOptions PassthroughOpts() =>
        new() { StorageStateRef = "imported-ref", PostLoginSelector = "#dashboard" };

    [Fact]
    public async Task Passthrough_AcquireAsync_CancelledAtEachStep_ReturnsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;

        (await SessionPassthroughTestFactory.CreateProvider(
            SessionPassthroughTestFactory.CreateAuthenticatedContextMock(), PassthroughOpts(), CancellingActor())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        var attachCtx = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        attachCtx.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled());
        (await SessionPassthroughTestFactory.CreateProvider(attachCtx, PassthroughOpts())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        var probeCtx = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        probeCtx.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<bool>());
        (await SessionPassthroughTestFactory.CreateProvider(probeCtx, PassthroughOpts())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();

        var exportCtx = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        exportCtx.ExportStorageStateAsync(Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<string>());
        (await SessionPassthroughTestFactory.CreateProvider(exportCtx, PassthroughOpts())
            .AcquireAsync(Request(), ct)).IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task Passthrough_AcquireAsync_ExportEmpty_FallsBackToImportedRef()
    {
        // Kills the `export.IsSuccess && !IsNullOrWhiteSpace(export.Value)` (-> `||`) logical mutant: when
        // export yields whitespace, the session must fall back to the imported reference, not the empty value.
        var ctx = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        ctx.ExportStorageStateAsync(Arg.Any<CancellationToken>()).Returns(Result<string>.Success("   "));
        var sut = SessionPassthroughTestFactory.CreateProvider(ctx, PassthroughOpts());

        var result = await sut.AcquireAsync(Request(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StorageStateRef.ShouldBe("imported-ref");
    }

    [Fact]
    public async Task Passthrough_EnsureValidAsync_CancelledAtProbe_ReturnsCancelled()
    {
        // Passthrough does not re-hydrate (the external owner holds the context); EnsureValid only re-probes.
        var ct = TestContext.Current.CancellationToken;
        var session = SessionFor(SiaraAuthMode.SessionPassthrough, "ref");

        var probeCtx = SessionPassthroughTestFactory.CreateAuthenticatedContextMock();
        probeCtx.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ResultExtensions.Cancelled<bool>());
        (await SessionPassthroughTestFactory.CreateProvider(probeCtx, PassthroughOpts())
            .EnsureValidAsync(session, ct)).IsCancelled().ShouldBeTrue();
    }
}
