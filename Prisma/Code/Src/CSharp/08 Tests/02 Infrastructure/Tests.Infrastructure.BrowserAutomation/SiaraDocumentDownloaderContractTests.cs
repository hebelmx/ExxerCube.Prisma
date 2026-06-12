namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Implementation instance of <see cref="ExxerCube.Prisma.Testing.Contracts.DocumentDownloaderContract"/>
/// for the production <see cref="SiaraDocumentDownloader"/> (ADR-005, ADR-010, MVP-PATH 1.1).
/// </summary>
/// <remarks>
/// The SUT is the real downloader over mocked collaborators that model the happy path (see
/// <see cref="SiaraDocumentDownloaderTestFactory"/>), so the universal contract — Result semantics,
/// cancellation, blank-id handling, success-carries-provenance, and the no-credential document invariant —
/// runs with no live browser. The fail-closed and document-selection mechanics live in
/// <see cref="SiaraDocumentDownloaderTests"/>.
/// </remarks>
public sealed class SiaraDocumentDownloaderContractTests : ExxerCube.Prisma.Testing.Contracts.DocumentDownloaderContract
{
    /// <summary>Initializes the implementation instance with the real downloader over healthy mocks.</summary>
    public SiaraDocumentDownloaderContractTests()
        : base(SiaraDocumentDownloaderTestFactory.CreateDownloader())
    {
    }

    /// <summary>The default fixture presents a single file named <c>contract-document</c>.</summary>
    protected override string ValidDocumentId() => SiaraDocumentDownloaderTestFactory.PresentDocumentId;
}
