namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="SiaraDocumentDownloader"/> that the universal
/// <see cref="ExxerCube.Prisma.Testing.Contracts.DocumentDownloaderContract"/> intentionally leaves to the
/// implementation (ADR-005 §5): fail-closed on resolver/acquire failure, authenticated-state hydration,
/// document selection, empty-content rejection, failure propagation, and the always-release guarantee.
/// </summary>
public sealed class SiaraDocumentDownloaderTests
{
    private const string DocId = SiaraDocumentDownloaderTestFactory.PresentDocumentId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_NullResolver_Throws() =>
        Should.Throw<ArgumentNullException>(() => new SiaraDocumentDownloader(
            null!,
            SiaraDocumentDownloaderTestFactory.CreateAgentMock(),
            SiaraDocumentDownloaderTestFactory.CreateSessionContextMock(),
            SiaraDocumentDownloaderTestFactory.CreateNavigationTargetMock(),
            Options.Create(new SiaraAuthOptions()),
            Substitute.For<ILogger<SiaraDocumentDownloader>>()));

    [Fact]
    public async Task DownloadAsync_WhenResolverFails_FailsClosedWithoutTouchingBrowser()
    {
        var resolver = Substitute.For<ISiaraSessionProviderResolver>();
        resolver.Resolve().Returns(Result<ISiaraSessionProvider>.WithFailure("No provider for the configured SIARA auth mode"));
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(resolver: resolver, agent: agent);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
        await agent.DidNotReceive().LaunchBrowserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenAcquireFails_FailsClosedWithoutScraping()
    {
        var provider = Substitute.For<ISiaraSessionProvider>();
        provider.AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<SiaraSession>.WithFailure("could not authenticate"));
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(resolver: resolver, agent: agent);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsFailure.ShouldBeTrue();
        // Passthrough pre-launches the browser before acquisition, but a failed acquire must never lead to
        // navigation or a download.
        await agent.DidNotReceive().NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await agent.DidNotReceive().DownloadFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_HappyPath_CarriesActorAndSessionProvenance_AndReleases()
    {
        var provider = SiaraDocumentDownloaderTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(resolver: resolver);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.DocumentId.ShouldBe(DocId);
        result.Value.AcquiredBy.ActorId.ShouldBe(SiaraDocumentDownloaderTestFactory.ServiceAccountActorId);
        result.Value.SessionId.ShouldBe("siara-session-1");
        result.Value.SourceUrl.ShouldContain(DocId);
        result.Value.Content.ShouldNotBeEmpty();
        await provider.Received(1).ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_HydratesAuthenticatedStorageState()
    {
        var sessionContext = SiaraDocumentDownloaderTestFactory.CreateSessionContextMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(sessionContext: sessionContext);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsSuccess.ShouldBeTrue();
        await sessionContext.Received(1).LoadStorageStateAsync("captured-storage-state", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenDocumentNotPresented_ReturnsNotFoundFailureWithoutDownloading()
    {
        var nav = SiaraDocumentDownloaderTestFactory.CreateNavigationTargetMock(new List<DownloadableFile>
        {
            new() { Url = "https://siara.local/files/other.pdf", FileName = "other-document", Format = FileFormat.Pdf },
        });
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(agent: agent, navigationTarget: nav);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain(DocId);
        await agent.DidNotReceive().DownloadFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_SelectsByContains_WhenNoExactFilenameMatch()
    {
        const string url = "https://siara.local/files/REQ-contract-document-2026.pdf";
        var nav = SiaraDocumentDownloaderTestFactory.CreateNavigationTargetMock(new List<DownloadableFile>
        {
            new() { Url = url, FileName = "REQ-contract-document-2026.pdf", Format = FileFormat.Pdf },
        });
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(agent: agent, navigationTarget: nav);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsSuccess.ShouldBeTrue();
        await agent.Received(1).DownloadFileAsync(url, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenIdIsAbsoluteUrl_DownloadsThatUrlDirectly()
    {
        const string url = "https://siara.local/direct/doc-9.pdf";
        var nav = SiaraDocumentDownloaderTestFactory.CreateNavigationTargetMock(new List<DownloadableFile>());
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(agent: agent, navigationTarget: nav);

        var result = await sut.DownloadAsync(url, Ct);

        result.IsSuccess.ShouldBeTrue();
        await agent.Received(1).DownloadFileAsync(url, Arg.Any<CancellationToken>());
        result.Value!.SourceUrl.ShouldBe(url);
    }

    [Fact]
    public async Task DownloadAsync_WhenIdIsAbsoluteUrl_SkipsFullPortalScrape()
    {
        // Finding #1: the orchestrator passes the case file's absolute URL (already learned from discovery).
        // Re-scraping the WHOLE portal per file is O(portal) and redundant — SelectDocument's absolute-URL
        // fallback already downloads the URL without requiring it in the presented set. So for an absolute
        // URL the downloader must download directly WITHOUT calling RetrieveDocumentsAsync.
        const string url = "https://siara.local/direct/doc-9.pdf";
        var nav = SiaraDocumentDownloaderTestFactory.CreateNavigationTargetMock();
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(agent: agent, navigationTarget: nav);

        var result = await sut.DownloadAsync(url, Ct);

        result.IsSuccess.ShouldBeTrue();
        await agent.Received(1).DownloadFileAsync(url, Arg.Any<CancellationToken>());
        await nav.DidNotReceive().RetrieveDocumentsAsync(Arg.Any<IBrowserAutomationAgent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenIdIsNotAUrl_StillScrapesThePresentedSet()
    {
        // The presented-set security gate (ADR-010) is preserved for non-URL ids: the downloader must still
        // scrape and select from what SIARA actually presents.
        var nav = SiaraDocumentDownloaderTestFactory.CreateNavigationTargetMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(navigationTarget: nav);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsSuccess.ShouldBeTrue();
        await nav.Received(1).RetrieveDocumentsAsync(Arg.Any<IBrowserAutomationAgent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_ClosesBrowserAfterDownload_SoPerFileLaunchesDoNotAccumulate()
    {
        // Finding #2: the orchestrator calls DownloadAsync once per case file on the same Scoped adapter.
        // Each call must balance its browser launch with a close (after the awaited download) so Chromium
        // instances never accumulate and exhaust the box. The close runs only AFTER this call's download has
        // completed, so it never tears down an in-flight download.
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(agent: agent);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsSuccess.ShouldBeTrue();
        await agent.Received(1).CloseBrowserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenDownloadFails_ClosesBrowserAndReleasesSession()
    {
        var provider = SiaraDocumentDownloaderTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        agent.DownloadFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<DownloadedFile>.WithFailure("network error"));
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(resolver: resolver, agent: agent);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsFailure.ShouldBeTrue();
        await provider.Received(1).ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
        await agent.Received(1).CloseBrowserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenContentEmpty_FailsAndReleasesSession()
    {
        var provider = SiaraDocumentDownloaderTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock(content: Array.Empty<byte>());
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(resolver: resolver, agent: agent);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsFailure.ShouldBeTrue();
        await provider.Received(1).ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenDownloadFails_PropagatesFailureAndReleasesSession()
    {
        var provider = SiaraDocumentDownloaderTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        agent.DownloadFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<DownloadedFile>.WithFailure("network error"));
        var sut = SiaraDocumentDownloaderTestFactory.CreateDownloader(resolver: resolver, agent: agent);

        var result = await sut.DownloadAsync(DocId, Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("network error");
        await provider.Received(1).ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
    }
}
