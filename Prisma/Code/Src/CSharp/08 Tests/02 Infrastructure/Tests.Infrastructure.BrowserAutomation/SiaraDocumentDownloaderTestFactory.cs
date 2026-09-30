using ExxerCube.Prisma.Domain.Interfaces.Navigation;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Builds <see cref="SiaraDocumentDownloader"/> instances over mocked collaborators (resolver, browser
/// agent, session context, SIARA navigation target) so the universal
/// <see cref="ExxerCube.Prisma.Testing.Contracts.DocumentDownloaderContract"/> and the mode-specific
/// mechanics tests run with no live browser and no real SIARA.
/// </summary>
/// <remarks>
/// The default mocks model the happy path: the resolver returns an authenticated provider, the agent
/// launches/navigates/downloads successfully, the session context hydrates, and SIARA presents a single
/// file named <see cref="PresentDocumentId"/>. The mechanics tests override individual collaborators to
/// exercise the fail-closed and selection paths.
/// </remarks>
internal static class SiaraDocumentDownloaderTestFactory
{
    /// <summary>The id of the document the default navigation-target mock presents.</summary>
    public const string PresentDocumentId = "contract-document";

    /// <summary>The actor id stamped on the session by the default provider mock.</summary>
    public const string ServiceAccountActorId = "siara-service-account";

    /// <summary>Builds a provider mock that acquires a healthy session and releases successfully.</summary>
    public static ISiaraSessionProvider CreateProviderMock(SiaraSession? session = null)
    {
        var provider = Substitute.For<ISiaraSessionProvider>();
        var acquired = session ?? new SiaraSession
        {
            SessionId = "siara-session-1",
            Mode = SiaraAuthMode.SessionPassthrough,
            StorageStateRef = "captured-storage-state",
            ExpiresAt = null,
            AcquiredBy = new SiaraActor
            {
                ActorId = ServiceAccountActorId,
                ActorType = SiaraActorType.ServiceAccount,
            },
        };

        provider.AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled<SiaraSession>()
                : Result<SiaraSession>.Success(acquired));

        provider.ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return provider;
    }

    /// <summary>Builds a resolver mock that returns the given (or a default healthy) provider.</summary>
    public static ISiaraSessionProviderResolver CreateResolverMock(ISiaraSessionProvider? provider = null)
    {
        // Build the provider first so the Returns() argument contains no nested mock setup (NS4000).
        var resolved = provider ?? CreateProviderMock();
        var resolver = Substitute.For<ISiaraSessionProviderResolver>();
        resolver.Resolve().Returns(Result<ISiaraSessionProvider>.Success(resolved));
        return resolver;
    }

    /// <summary>Builds a browser-agent mock that launches, navigates, and downloads successfully.</summary>
    public static IBrowserAutomationAgent CreateAgentMock(byte[]? content = null)
    {
        var agent = Substitute.For<IBrowserAutomationAgent>();

        agent.LaunchBrowserAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        agent.NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        agent.CloseBrowserAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        agent.DownloadFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<DownloadedFile>.Success(new DownloadedFile
            {
                Url = call.ArgAt<string>(0),
                FileName = PresentDocumentId,
                Format = FileFormat.Pdf,
                Content = content ?? new byte[] { 0x25, 0x50, 0x44, 0x46 }, // %PDF
            }));

        return agent;
    }

    /// <summary>Builds a session-context mock that hydrates the authenticated storage-state successfully.</summary>
    public static IBrowserSessionContext CreateSessionContextMock()
    {
        var context = Substitute.For<IBrowserSessionContext>();
        context.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        context.ConnectToExistingContextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return context;
    }

    /// <summary>Builds a SIARA navigation-target mock that presents the given files (default: one matching file).</summary>
    public static INavigationTarget CreateNavigationTargetMock(IReadOnlyList<DownloadableFile>? files = null)
    {
        var nav = Substitute.For<INavigationTarget>();
        nav.BaseUrl.Returns("https://siara.local");

        var presented = (files ?? new List<DownloadableFile>
        {
            new()
            {
                Url = $"https://siara.local/files/{PresentDocumentId}.pdf",
                FileName = PresentDocumentId,
                Format = FileFormat.Pdf,
            },
        }).ToList();

        nav.RetrieveDocumentsAsync(Arg.Any<IBrowserAutomationAgent>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(presented));

        return nav;
    }

    /// <summary>Wraps the collaborators in a downloader; any omitted collaborator defaults to a healthy mock.</summary>
    public static SiaraDocumentDownloader CreateDownloader(
        ISiaraSessionProviderResolver? resolver = null,
        IBrowserAutomationAgent? agent = null,
        IBrowserSessionContext? sessionContext = null,
        INavigationTarget? navigationTarget = null,
        SiaraAuthOptions? authOptions = null) =>
        new(
            resolver ?? CreateResolverMock(),
            agent ?? CreateAgentMock(),
            sessionContext ?? CreateSessionContextMock(),
            navigationTarget ?? CreateNavigationTargetMock(),
            Options.Create(authOptions ?? new SiaraAuthOptions()),
            Substitute.For<ILogger<SiaraDocumentDownloader>>());
}
