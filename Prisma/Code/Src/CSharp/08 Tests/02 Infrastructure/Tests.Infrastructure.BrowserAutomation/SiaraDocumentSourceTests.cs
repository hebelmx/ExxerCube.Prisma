using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Interfaces.Navigation;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="SiaraDocumentSource"/> that the universal
/// <see cref="ExxerCube.Prisma.Testing.Contracts.SiaraDocumentSourceContract"/> intentionally leaves to the
/// implementation (ADR-005 §5): fail-closed on resolver/acquire failure, authenticated-state hydration, the
/// warm-session lifecycle (acquire once, re-validate thereafter, re-acquire on death), listing as file
/// URLs, and the release-on-dispose guarantee.
/// </summary>
public sealed class SiaraDocumentSourceTests
{
    private const string DocUrl = SiaraDocumentSourceTestFactory.PresentDocumentUrl;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_NullResolver_Throws() =>
        Should.Throw<ArgumentNullException>(() => new SiaraDocumentSource(
            null!,
            SiaraDocumentDownloaderTestFactory.CreateAgentMock(),
            SiaraDocumentDownloaderTestFactory.CreateSessionContextMock(),
            SiaraDocumentSourceTestFactory.CreateNavigationTargetMock(),
            Options.Create(new SiaraAuthOptions()),
            Substitute.For<ILogger<SiaraDocumentSource>>()));

    [Fact]
    public async Task DiscoverDocumentIdsAsync_WhenResolverFails_FailsClosedWithoutTouchingBrowser()
    {
        var resolver = Substitute.For<ISiaraSessionProviderResolver>();
        resolver.Resolve().Returns(Result<ISiaraSessionProvider>.WithFailure("No provider for the configured SIARA auth mode"));
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver, agent: agent);

        var result = await sut.DiscoverDocumentIdsAsync(Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
        await agent.DidNotReceive().LaunchBrowserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverDocumentIdsAsync_WhenAcquireFails_FailsClosedWithoutListing()
    {
        var provider = Substitute.For<ISiaraSessionProvider>();
        provider.AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<SiaraSession>.WithFailure("could not authenticate"));
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver, agent: agent);

        var result = await sut.DiscoverDocumentIdsAsync(Ct);

        result.IsFailure.ShouldBeTrue();
        // Passthrough pre-launches the browser before acquisition, but a failed acquire must never navigate.
        await agent.DidNotReceive().NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverDocumentIdsAsync_HappyPath_ReturnsFileUrlsAsIds()
    {
        var sut = SiaraDocumentSourceTestFactory.CreateSource();

        var result = await sut.DiscoverDocumentIdsAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain(DocUrl);
    }

    [Fact]
    public async Task DiscoverDocumentIdsAsync_HydratesAuthenticatedStorageState()
    {
        var sessionContext = SiaraDocumentDownloaderTestFactory.CreateSessionContextMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(sessionContext: sessionContext);

        var result = await sut.DiscoverDocumentIdsAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        await sessionContext.Received(1).LoadStorageStateAsync("captured-storage-state", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverDocumentIdsAsync_WhenSiaraPresentsNothing_ReturnsEmptyList()
    {
        var nav = SiaraDocumentSourceTestFactory.CreateNavigationTargetMock(new List<DownloadableFile>());
        var sut = SiaraDocumentSourceTestFactory.CreateSource(navigationTarget: nav);

        var result = await sut.DiscoverDocumentIdsAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldBeEmpty();
    }

    [Fact]
    public async Task DiscoverDocumentIdsAsync_SecondCycle_KeepsSessionWarm_ReValidatesInsteadOfReacquiring()
    {
        var provider = SiaraDocumentSourceTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver, agent: agent);

        (await sut.DiscoverDocumentIdsAsync(Ct)).IsSuccess.ShouldBeTrue();
        (await sut.DiscoverDocumentIdsAsync(Ct)).IsSuccess.ShouldBeTrue();

        // Acquire and the (non-idempotent) browser launch happen exactly once; the second cycle re-validates.
        await provider.Received(1).AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>());
        await provider.Received(1).EnsureValidAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
        await agent.Received(1).LaunchBrowserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverDocumentIdsAsync_WhenWarmSessionDies_ReacquiresInsteadOfFailing()
    {
        var provider = SiaraDocumentSourceTestFactory.CreateProviderMock();
        // The warm session has died underneath us: re-validation fails, so the source must re-acquire.
        provider.EnsureValidAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>())
            .Returns(Result<SiaraSession>.WithFailure("SIARA session is no longer authenticated."));
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver);

        (await sut.DiscoverDocumentIdsAsync(Ct)).IsSuccess.ShouldBeTrue();
        var second = await sut.DiscoverDocumentIdsAsync(Ct);

        second.IsSuccess.ShouldBeTrue();
        await provider.Received(2).AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_ReleasesWarmSession()
    {
        var provider = SiaraDocumentSourceTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver);

        (await sut.DiscoverDocumentIdsAsync(Ct)).IsSuccess.ShouldBeTrue();
        await sut.DisposeAsync();

        await provider.Received(1).ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
    }
}
