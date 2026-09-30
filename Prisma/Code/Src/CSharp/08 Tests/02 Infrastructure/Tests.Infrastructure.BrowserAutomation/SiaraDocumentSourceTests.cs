using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Interfaces.Navigation;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="SiaraDocumentSource"/> that the universal
/// <see cref="ExxerCube.Prisma.Testing.Contracts.SiaraDocumentSourceContract"/> intentionally leaves to the
/// implementation (ADR-005 §5): fail-closed on resolver/acquire failure, authenticated-state hydration, the
/// warm-session lifecycle (acquire once, re-validate thereafter, re-acquire on death), and the
/// release-on-dispose guarantee.
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
    public async Task DiscoverCasesAsync_WhenResolverFails_FailsClosedWithoutTouchingBrowser()
    {
        var resolver = Substitute.For<ISiaraSessionProviderResolver>();
        resolver.Resolve().Returns(Result<ISiaraSessionProvider>.WithFailure("No provider for the configured SIARA auth mode"));
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver, agent: agent);

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
        await agent.DidNotReceive().LaunchBrowserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverCasesAsync_WhenAcquireFails_FailsClosedWithoutListing()
    {
        var provider = Substitute.For<ISiaraSessionProvider>();
        provider.AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<SiaraSession>.WithFailure("could not authenticate"));
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver, agent: agent);

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsFailure.ShouldBeTrue();
        // Passthrough pre-launches the browser before acquisition, but a failed acquire must never navigate.
        await agent.DidNotReceive().NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverCasesAsync_HappyPath_ReturnsCasesContainingFileUrls()
    {
        var sut = SiaraDocumentSourceTestFactory.CreateSource();

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // All returned cases together must include the known document URL somewhere in their file lists.
        var allUrls = result.Value!.SelectMany(c => c.Files).Select(f => f.Url);
        allUrls.ShouldContain(DocUrl);
    }

    [Fact]
    public async Task DiscoverCasesAsync_HydratesAuthenticatedStorageState()
    {
        var sessionContext = SiaraDocumentDownloaderTestFactory.CreateSessionContextMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(sessionContext: sessionContext);

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        await sessionContext.Received(1).LoadStorageStateAsync("captured-storage-state", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverCasesAsync_WhenSiaraPresentsNothing_ReturnsEmptyList()
    {
        var nav = SiaraDocumentSourceTestFactory.CreateNavigationTargetMock(new List<DownloadableFile>());
        var sut = SiaraDocumentSourceTestFactory.CreateSource(navigationTarget: nav);

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldBeEmpty();
    }

    [Fact]
    public async Task DiscoverCasesAsync_SecondCycle_KeepsSessionWarm_ReValidatesInsteadOfReacquiring()
    {
        var provider = SiaraDocumentSourceTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var agent = SiaraDocumentDownloaderTestFactory.CreateAgentMock();
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver, agent: agent);

        (await sut.DiscoverCasesAsync(Ct)).IsSuccess.ShouldBeTrue();
        (await sut.DiscoverCasesAsync(Ct)).IsSuccess.ShouldBeTrue();

        // Acquire and the (non-idempotent) browser launch happen exactly once; the second cycle re-validates.
        await provider.Received(1).AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>());
        await provider.Received(1).EnsureValidAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
        await agent.Received(1).LaunchBrowserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverCasesAsync_WhenWarmSessionDies_ReacquiresInsteadOfFailing()
    {
        var provider = SiaraDocumentSourceTestFactory.CreateProviderMock();
        // The warm session has died underneath us: re-validation fails, so the source must re-acquire.
        provider.EnsureValidAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>())
            .Returns(Result<SiaraSession>.WithFailure("SIARA session is no longer authenticated."));
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver);

        (await sut.DiscoverCasesAsync(Ct)).IsSuccess.ShouldBeTrue();
        var second = await sut.DiscoverCasesAsync(Ct);

        second.IsSuccess.ShouldBeTrue();
        await provider.Received(2).AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_ReleasesWarmSession()
    {
        var provider = SiaraDocumentSourceTestFactory.CreateProviderMock();
        var resolver = SiaraDocumentDownloaderTestFactory.CreateResolverMock(provider);
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver);

        (await sut.DiscoverCasesAsync(Ct)).IsSuccess.ShouldBeTrue();
        await sut.DisposeAsync();

        await provider.Received(1).ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // DiscoverCasesAsync case-grouping mechanics (MVP-PATH 2.1)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverCasesAsync_PreCancelledToken_ReturnsCancelled()
    {
        var sut = SiaraDocumentSourceTestFactory.CreateSource();
        var cancelled = new CancellationToken(canceled: true);

        var result = await sut.DiscoverCasesAsync(cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task DiscoverCasesAsync_WhenResolverFails_FailsClosed()
    {
        var resolver = Substitute.For<ISiaraSessionProviderResolver>();
        resolver.Resolve().Returns(Result<ISiaraSessionProvider>.WithFailure("No provider configured"));
        var sut = SiaraDocumentSourceTestFactory.CreateSource(resolver: resolver);

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DiscoverCasesAsync_HappyPath_GroupsFilesIntoCase()
    {
        // The default navigation-target mock returns one file at PresentDocumentUrl, which is
        // "https://siara.local/files/contract-document.pdf" — path segments: ["files","contract-document.pdf"]
        // → case id = "files". This verifies the full wire (session + hydrate + navigate + group).
        var sut = SiaraDocumentSourceTestFactory.CreateSource();

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldNotBeEmpty();
        // All files from the mock end up in some case.
        result.Value!.SelectMany(c => c.Files).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DiscoverCasesAsync_WhenSiaraPresentsNothing_ReturnsEmptyCaseList()
    {
        var nav = SiaraDocumentSourceTestFactory.CreateNavigationTargetMock(new List<DownloadableFile>());
        var sut = SiaraDocumentSourceTestFactory.CreateSource(navigationTarget: nav);

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldBeEmpty();
    }

    [Fact]
    public async Task DiscoverCasesAsync_MultiFileSameCase_ReturnsSingleCase()
    {
        var files = new List<DownloadableFile>
        {
            new() { Url = "https://siara.local/document_store/EXP001/form.pdf",  FileName = "form",  Format = FileFormat.Pdf },
            new() { Url = "https://siara.local/document_store/EXP001/data.xml",  FileName = "data",  Format = FileFormat.Xml },
            new() { Url = "https://siara.local/document_store/EXP001/annex.docx", FileName = "annex", Format = FileFormat.Docx },
        };
        var nav = SiaraDocumentSourceTestFactory.CreateNavigationTargetMock(files);
        var sut = SiaraDocumentSourceTestFactory.CreateSource(navigationTarget: nav);

        var result = await sut.DiscoverCasesAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value![0].CaseId.ShouldBe("EXP001");
        result.Value![0].Files.Count.ShouldBe(3);
    }
}
