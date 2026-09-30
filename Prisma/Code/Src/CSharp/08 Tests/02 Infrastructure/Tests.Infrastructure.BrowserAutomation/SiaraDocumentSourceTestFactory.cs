using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Interfaces.Navigation;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Builds <see cref="SiaraDocumentSource"/> instances over mocked collaborators (resolver, browser agent,
/// session context, SIARA navigation target) so the universal
/// <see cref="ExxerCube.Prisma.Testing.Contracts.SiaraDocumentSourceContract"/> and the mode-specific
/// mechanics tests run with no live browser and no real SIARA.
/// </summary>
/// <remarks>
/// Reuses the happy-path collaborator mocks from <see cref="SiaraDocumentDownloaderTestFactory"/> (the
/// resolver/provider/agent/session-context/navigation-target mocks are identical), since discovery is the
/// "list" half of the same scrape the downloader performs.
/// </remarks>
internal static class SiaraDocumentSourceTestFactory
{
    /// <summary>The URL of the single file the default navigation-target mock presents.</summary>
    public const string PresentDocumentUrl = "https://siara.local/files/contract-document.pdf";

    /// <summary>Builds a provider mock that acquires a healthy session, re-validates it, and releases it.</summary>
    public static ISiaraSessionProvider CreateProviderMock(SiaraSession? session = null)
    {
        var acquired = session ?? new SiaraSession
        {
            SessionId = "siara-session-1",
            Mode = SiaraAuthMode.SessionPassthrough,
            StorageStateRef = "captured-storage-state",
            ExpiresAt = null,
            AcquiredBy = new SiaraActor
            {
                ActorId = SiaraDocumentDownloaderTestFactory.ServiceAccountActorId,
                ActorType = SiaraActorType.ServiceAccount,
            },
        };

        var provider = Substitute.For<ISiaraSessionProvider>();

        provider.AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled<SiaraSession>()
                : Result<SiaraSession>.Success(acquired));

        // Default warm-session behavior: re-validation keeps the same session.
        provider.EnsureValidAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled<SiaraSession>()
                : Result<SiaraSession>.Success(acquired));

        provider.ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return provider;
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
                Url = PresentDocumentUrl,
                FileName = "contract-document",
                Format = FileFormat.Pdf,
            },
        }).ToList();

        nav.RetrieveDocumentsAsync(Arg.Any<IBrowserAutomationAgent>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(presented));

        return nav;
    }

    /// <summary>Wraps the collaborators in a discovery source; any omitted collaborator defaults to a healthy mock.</summary>
    public static SiaraDocumentSource CreateSource(
        ISiaraSessionProviderResolver? resolver = null,
        IBrowserAutomationAgent? agent = null,
        IBrowserSessionContext? sessionContext = null,
        INavigationTarget? navigationTarget = null,
        SiaraAuthOptions? authOptions = null) =>
        new(
            resolver ?? SiaraDocumentDownloaderTestFactory.CreateResolverMock(CreateProviderMock()),
            agent ?? SiaraDocumentDownloaderTestFactory.CreateAgentMock(),
            sessionContext ?? SiaraDocumentDownloaderTestFactory.CreateSessionContextMock(),
            navigationTarget ?? CreateNavigationTargetMock(),
            Options.Create(authOptions ?? new SiaraAuthOptions()),
            Substitute.For<ILogger<SiaraDocumentSource>>());
}
